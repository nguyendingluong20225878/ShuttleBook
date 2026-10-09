import type { Page, Route } from '@playwright/test';

export function browserSessionFields() {
  return { tokenType: 'Bearer', expiresInSeconds: 600,
    refreshExpiresAt: new Date(Date.now() + 30 * 86400_000).toISOString(),
    idleExpiresAt: new Date(Date.now() + 30 * 60_000).toISOString() };
}

type Session = Record<string, unknown>;
const sessions = new WeakMap<Page, Session>();
const watched = new WeakSet<Page>();

// Business fixtures keep their login payload and assertions. This helper models the
// separate cookie-backed browser family: guest restore is 401, login enables restore,
// and refresh/activity never leak a refresh token into JSON.
export async function browserSessionRoute(route: Route, page: Page, customRestore = false): Promise<boolean> {
  if (!watched.has(page)) {
    watched.add(page);
    page.on('response', async response => {
      const path = new URL(response.url()).pathname;
      if (!/^\/api\/v1\/browser-auth\/(customer|partner)\/(login|restore|activity)$/.test(path) || !response.ok()) return;
      try { const body = await response.json(); if (body.data?.accessToken) sessions.set(page, body.data); } catch { /* Aborted response carries no session. */ }
    });
  }
  const request = route.request();
  const match = new URL(request.url()).pathname.match(/^\/api\/v1\/browser-auth\/(customer|partner)\/(restore|activity|logout)$/);
  if (!match) return false;
  const origin = `http://localhost:${match[1] === 'customer' ? 5173 : 5174}`;
  const headers = { 'Access-Control-Allow-Origin': origin, 'Access-Control-Allow-Credentials': 'true',
    'Access-Control-Allow-Methods': 'POST, OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type, X-ShuttleBook-Session', 'Cache-Control': 'no-store' };
  if (request.method() === 'OPTIONS') { await route.fulfill({ status: 204, headers }); return true; }
  if (match[2] === 'logout') { sessions.delete(page); await route.fulfill({ status: 204, headers }); return true; }
  const session = sessions.get(page);
  if (customRestore && session && match[2] === 'restore') return false;
  if (!session) {
    await route.fulfill({ status: 401, headers, contentType: 'application/problem+json', body: JSON.stringify({ code: 'INVALID_REFRESH_TOKEN' }) });
  } else {
    const data = match[2] === 'activity' ? { ...session, idleExpiresAt: new Date(Date.now() + 30 * 60_000).toISOString() } : session;
    await route.fulfill({ headers, contentType: 'application/json', body: JSON.stringify({ data }) });
  }
  return true;
}
