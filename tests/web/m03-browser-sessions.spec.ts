import { expect, test, type BrowserContext, type Page, type Route } from '@playwright/test';
import { partnerLogout } from './helpers/partner-navigation';

type Portal = 'customer' | 'partner';
const url = (portal: Portal) => `http://localhost:${portal === 'customer' ? 5173 : 5174}`;
const signedIn = (page: Page, portal: Portal) => portal === 'customer'
  ? page.getByRole('heading', { name: 'Đơn của tôi', exact: true })
  : page.getByRole('heading', { name: 'Hồ sơ chủ sân', exact: true });
const loginHeading = (page: Page, portal: Portal) => page.getByRole('heading', { name: portal === 'customer' ? 'Đăng nhập khách hàng' : 'Đăng nhập chủ sân', exact: true });

async function fixture(context: BrowserContext) {
  const state = { active: { customer: false, partner: false }, generation: { customer: 0, partner: 0 }, calls: [] as string[], activities: 0,
    idleDeadline: Date.now() + 30 * 60_000, absoluteDeadline: Date.now() + 30 * 86400_000,
    restoreFailure: false, logoutFailure: false, holdRestore: false, release: null as (() => Promise<void>) | null };
  const payload = (portal: Portal) => ({ data: { tokenType: 'Bearer', accessToken: `${portal}-access-${state.generation[portal]}`, expiresInSeconds: 600,
    idleExpiresAt: new Date(state.idleDeadline).toISOString(), refreshExpiresAt: new Date(state.absoluteDeadline).toISOString(),
    user: { accountType: portal === 'customer' ? 'CUSTOMER' : 'VENUE_OPERATOR', status: 'ACTIVE' } } });
  await context.route('**/api/v1/**', async (route: Route) => {
    const request = route.request(), path = new URL(request.url()).pathname;
    const match = path.match(/^\/api\/v1\/browser-auth\/(customer|partner)\/(login|restore|activity|logout)$/);
    const origin = request.headers().origin ?? 'http://localhost:5173';
    const headers = { 'Access-Control-Allow-Origin': origin, 'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Content-Type,Authorization', 'Access-Control-Allow-Methods': 'GET,POST,OPTIONS', 'Cache-Control': 'no-store' };
    const reply = (body: unknown, status = 200, extra = {}) => route.fulfill({ status, headers: { ...headers, ...extra }, contentType: 'application/json', body: JSON.stringify(body) });
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    if (match) {
      const portal = match[1] as Portal, action = match[2]; state.calls.push(`${portal}/${action}`);
      if (action !== 'login') expect(request.postDataJSON()).toEqual({});
      if (action === 'login') {
        expect(request.postDataJSON()).not.toHaveProperty('refreshToken');
        state.active[portal] = true; state.generation[portal]++;
        return reply(payload(portal), 200, { 'Set-Cookie': `shuttlebook_${portal}_refresh=${portal}-opaque; HttpOnly; SameSite=Strict; Path=/api/v1/browser-auth/${portal}; Max-Age=2592000` });
      }
      if (action === 'logout') {
        if (state.logoutFailure) return route.abort('failed');
        state.active[portal] = false;
        return route.fulfill({ status: 204, headers: { ...headers, 'Set-Cookie': `shuttlebook_${portal}_refresh=; HttpOnly; SameSite=Strict; Path=/api/v1/browser-auth/${portal}; Max-Age=0` } });
      }
      if (action === 'restore' && state.restoreFailure) return route.abort('failed');
      if (!state.active[portal]) return reply({ code: 'INVALID_REFRESH_TOKEN' }, 401);
      expect(request.headers().cookie).toContain(`shuttlebook_${portal}_refresh=${portal}-opaque`);
      if (action === 'activity') { state.activities++; state.idleDeadline = Date.now() + 30 * 60_000; }
      if (action === 'restore' && state.holdRestore) {
        const captured = payload(portal);
        state.release = () => reply(captured);
        return;
      }
      return reply(payload(portal));
    }
    if (path.includes('/notifications')) return reply({ data: [], unreadCount: 0, nextCursor: null });
    if (path === '/api/v1/me/bookings') return reply({ data: { items: [], nextCursor: null } });
    if (path === '/api/v1/partner-onboarding/businesses') return reply({ data: [] });
    if (path === '/api/v1/venues') return reply({ data: { items: [], nextCursor: null } });
    return route.abort();
  });
  return state;
}
async function login(page: Page, portal: Portal) {
  await page.goto(portal === 'customer' ? `${url(portal)}/login?returnTo=%2Fme%2Fbookings` : `${url(portal)}/#/profile`);
  if (portal === 'partner') await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill(`${portal}@example.test`);
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Browser-test-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(signedIn(page, portal)).toBeVisible();
}
async function logout(page: Page, portal: Portal) {
  if (portal === 'partner') await partnerLogout(page);
  else await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
}
async function emptyTokenStorage(page: Page) {
  expect(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage), cookies: document.cookie }))).toEqual({ local: [], session: [], cookies: '' });
}

for (const portal of ['customer', 'partner'] as const) {
  test(`M03 ${portal} F5 restores private URL from HttpOnly cookie without re-login or token storage`, async ({ page, context }) => {
    const state = await fixture(context); await login(page, portal);
    const privateUrl = page.url(); await page.reload();
    await expect(signedIn(page, portal)).toBeVisible(); await expect(page).toHaveURL(privateUrl);
    expect(state.calls.filter(call => call === `${portal}/login`)).toHaveLength(1);
    expect(state.calls.filter(call => call === `${portal}/restore`)).toHaveLength(2);
    const cookie = (await context.cookies()).find(item => item.name === `shuttlebook_${portal}_refresh`);
    expect(cookie).toMatchObject({ httpOnly: true, sameSite: 'Strict', path: `/api/v1/browser-auth/${portal}` });
    await emptyTokenStorage(page);
    // A restored Partner session must logout directly into the login form,
    // never temporarily expose the registration form remembered from startup.
    const [loggedOut] = await Promise.all([
      page.waitForResponse(response => response.url().endsWith(`/browser-auth/${portal}/logout`) && response.request().method() === 'POST'),
      logout(page, portal),
    ]);
    expect(loggedOut.status()).toBe(204);
    await expect(loginHeading(page, portal)).toBeVisible();
    await expect(page.getByLabel('Nhập lại mật khẩu')).toHaveCount(0);
  });
}

test('M03 same browser profile keeps Customer and Partner cookies independent and logs out only the chosen portal', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer');
  const partner = await context.newPage(); await login(partner, 'partner');
  const cookies = await context.cookies(); expect(cookies.filter(item => item.name.startsWith('shuttlebook_'))).toHaveLength(2);
  await page.reload(); await partner.reload();
  await expect(signedIn(page, 'customer')).toBeVisible(); await expect(signedIn(partner, 'partner')).toBeVisible();
  await logout(page, 'customer'); await partner.reload();
  await expect(signedIn(partner, 'partner')).toBeVisible(); expect(state.active).toEqual({ customer: false, partner: true });
  await emptyTokenStorage(page); await emptyTokenStorage(partner);
});

for (const portal of ['customer', 'partner'] as const) {
  test(`M03 ${portal} background polling and F5 never extend the thirty-minute idle deadline`, async ({ page, context }) => {
    const state = await fixture(context); await page.clock.install(); await login(page, portal);
    const deadline = state.idleDeadline;
    await page.clock.fastForward(29 * 60_000); await page.reload(); await expect(signedIn(page, portal)).toBeVisible();
    expect(state.idleDeadline).toBe(deadline); expect(state.activities).toBe(0);
    await page.clock.fastForward(61_000);
    await expect(signedIn(page, portal)).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toHaveCount(0);
    expect(state.activities).toBe(0);
  });
}

test('M03 real user action inside the throttle window is sent by the trailing timer without polling activity', async ({ page, context }) => {
  const state = await fixture(context); await page.clock.install(); await login(page, 'customer');
  await page.clock.runFor(10_000); await page.keyboard.press('Tab');
  // Login may complete before the heading assertion resolves. Keep the first
  // check clear of that variable offset while still proving no early send.
  await page.clock.runFor(45_000); expect(state.activities).toBe(0);
  await page.clock.runFor(10_000); await expect.poll(() => state.activities).toBe(1);
  await page.clock.runFor(120_000); expect(state.activities).toBe(1);
});

test('M03 absolute session expiry closes the UI even with a later idle deadline', async ({ page, context }) => {
  const state = await fixture(context); await page.clock.install();
  state.absoluteDeadline = Date.now() + 5000; state.idleDeadline = Date.now() + 30 * 60_000;
  await login(page, 'customer'); await page.clock.fastForward(6000);
  await expect(signedIn(page, 'customer')).toHaveCount(0);
  expect(state.activities).toBe(0);
});

for (const portal of ['customer', 'partner'] as const) {
  test(`M03 ${portal} logout closes a second tab and F5 cannot restore the revoked session`, async ({ page, context }) => {
    const state = await fixture(context); await login(page, portal);
    const tab = await context.newPage(); await tab.goto(page.url()); await expect(signedIn(tab, portal)).toBeVisible();
    await logout(page, portal); await expect(signedIn(tab, portal)).toHaveCount(0);
    await tab.reload(); await expect(signedIn(tab, portal)).toHaveCount(0);
    expect(state.active[portal]).toBe(false);
    expect((await context.cookies()).some(cookie => cookie.name === `shuttlebook_${portal}_refresh`)).toBe(false);
  });
}

test('M03 delayed successful restore cannot resurrect a session after logout in another tab', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer'); state.holdRestore = true;
  const tab = await context.newPage(); const navigation = tab.goto(`${url('customer')}/me/bookings`);
  await expect.poll(() => state.release !== null).toBe(true);
  await expect(tab.getByRole('status')).toContainText('Đang khôi phục phiên đăng nhập');
  await logout(page, 'customer'); await state.release!(); await navigation;
  await expect(signedIn(tab, 'customer')).toHaveCount(0);
  await expect(loginHeading(tab, 'customer')).toBeVisible();
});

test('M03 restore network failure displays connection retry without claiming bad credentials or losing private URL', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer'); const privateUrl = page.url();
  state.restoreFailure = true; await page.reload();
  await expect(page.getByRole('alert')).toBeVisible(); await expect(page.getByRole('button', { name: 'Thử lại kết nối', exact: true })).toBeVisible();
  await expect(loginHeading(page, 'customer')).toHaveCount(0); await expect(page).toHaveURL(privateUrl);
  state.restoreFailure = false; await page.getByRole('button', { name: 'Thử lại kết nối', exact: true }).click();
  await expect(signedIn(page, 'customer')).toBeVisible(); await expect(page).toHaveURL(privateUrl);
  expect(state.calls.filter(call => call === 'customer/login')).toHaveLength(1);
});

test('M03 failed network logout blocks cookie restore and retries revocation before any restore on F5', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer'); state.logoutFailure = true;
  await logout(page, 'customer'); await expect(signedIn(page, 'customer')).toHaveCount(0);
  await expect(page.getByText('Phiên trên trang đã đóng; máy chủ chưa xác nhận đăng xuất.', { exact: false })).toBeVisible();
  const before = state.calls.length; await page.reload();
  await expect(page.getByRole('button', { name: 'Thử lại kết nối', exact: true })).toBeVisible();
  expect(state.calls.slice(before)).toEqual(['customer/logout']);
  state.logoutFailure = false; await page.getByRole('button', { name: 'Thử lại kết nối', exact: true }).click();
  await expect(loginHeading(page, 'customer')).toBeVisible(); expect(state.active.customer).toBe(false);
  await emptyTokenStorage(page);
});

test('M03 account replacement from a second tab clears private children and ignores the old account response', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer');
  let releaseOld: (() => Promise<void>) | null = null;
  await context.route('**/api/v1/me/bookings', async route => {
    if (route.request().headers().authorization === 'Bearer customer-access-1') {
      releaseOld = () => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: { items: [{
        bookingId: '0199f050-0000-7000-8000-000000000003', bookingNo: 'OLD-ACCOUNT-PRIVATE', status: 'CONFIRMED', amount: 250000,
        venueName: 'Old private venue', courtName: 'Sân 1', date: '2026-10-20', localStart: '18:00', localEnd: '19:00', timezone: 'Asia/Ho_Chi_Minh',
      }], nextCursor: null } }) });
      return;
    }
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: { items: [], nextCursor: null } }) });
  });
  await page.reload(); await expect.poll(() => releaseOld !== null).toBe(true);
  const second = await context.newPage(); await second.goto(`${url('customer')}/venues`);
  // Model a completed browser login for a different account without a preceding
  // logout broadcast, so this exercises the account-replacement guard itself.
  await second.evaluate(async () => {
    const response = await fetch('http://localhost:5080/api/v1/browser-auth/customer/login', {
      method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ contactType: 'EMAIL', contact: 'second-customer@example.test', password: 'Browser-test-2026!' }),
    });
    if (!response.ok) throw new Error('Fixture replacement login failed');
    const bus = new BroadcastChannel('shuttlebook-session-customer'); bus.postMessage({ type: 'login' }); bus.close();
  });
  await expect(signedIn(page, 'customer')).toBeVisible(); await releaseOld!();
  await expect(page.getByRole('link', { name: 'OLD-ACCOUNT-PRIVATE', exact: true })).toHaveCount(0);
  expect(state.generation.customer).toBe(2);
  await emptyTokenStorage(page);
});

test('M03 delayed login JSON after response headers cannot resurrect a session after cross-tab logout', async ({ page, context }) => {
  const state = await fixture(context);
  await page.addInitScript(() => {
    const original = window.fetch.bind(window);
    const pending = window as unknown as { loginBodyWaiting?: boolean; releaseLoginBody?: () => void };
    window.fetch = async (...args: Parameters<typeof fetch>) => {
      const response = await original(...args);
      if (String(args[0]).endsWith('/browser-auth/customer/login')) {
        const json = response.json.bind(response);
        response.json = async () => {
          pending.loginBodyWaiting = true;
          await new Promise<void>(resolve => { pending.releaseLoginBody = resolve; });
          return json();
        };
      }
      return response;
    };
  });
  await page.goto(`${url('customer')}/login?returnTo=%2Fme%2Fbookings`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Browser-test-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect.poll(() => page.evaluate(() => Boolean((window as unknown as { loginBodyWaiting?: boolean }).loginBodyWaiting))).toBe(true);
  expect((await context.cookies()).some(cookie => cookie.name === 'shuttlebook_customer_refresh')).toBe(true);
  const second = await context.newPage(); await second.goto(`${url('customer')}/me/bookings`);
  await expect(signedIn(second, 'customer')).toBeVisible(); await logout(second, 'customer');
  await expect(page.getByText('Đã đăng xuất ở cửa sổ khác.', { exact: false })).toBeVisible();
  await page.evaluate(() => (window as unknown as { releaseLoginBody: () => void }).releaseLoginBody());
  await expect(page.getByText('Phiên đăng nhập đã thay đổi ở cửa sổ khác. Hãy thử lại.', { exact: false })).toBeVisible();
  await expect(signedIn(page, 'customer')).toHaveCount(0);
  await expect.poll(() => state.calls.filter(call => call === 'customer/logout').length).toBe(2);
  expect(state.active.customer).toBe(false);
  expect((await context.cookies()).some(cookie => cookie.name === 'shuttlebook_customer_refresh')).toBe(false);
  await emptyTokenStorage(page);
});

test('M03 an older restore finishing cannot clear loading while a replacement account restore is still pending', async ({ page, context }) => {
  const state = await fixture(context); await login(page, 'customer');
  const pending: { route: Route; payload: object }[] = [];
  await page.route('**/api/v1/browser-auth/customer/restore', async route => {
    pending.push({ route, payload: { data: { tokenType: 'Bearer', accessToken: `customer-access-${state.generation.customer}`,
      expiresInSeconds: 600, idleExpiresAt: new Date(state.idleDeadline).toISOString(), refreshExpiresAt: new Date(state.absoluteDeadline).toISOString(),
      user: { accountType: 'CUSTOMER', status: 'ACTIVE' } } } });
  });
  await page.reload(); await expect.poll(() => pending.length).toBe(1);
  const second = await context.newPage(); await second.goto(`${url('customer')}/venues`);
  await second.evaluate(async () => {
    await fetch('http://localhost:5080/api/v1/browser-auth/customer/login', { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ contactType: 'EMAIL', contact: 'replacement@example.test', password: 'Browser-test-2026!' }) });
    const bus = new BroadcastChannel('shuttlebook-session-customer'); bus.postMessage({ type: 'login' }); bus.close();
  });
  await expect.poll(() => pending.length).toBe(2);
  const oldResponse = page.waitForResponse(response => response.url().endsWith('/browser-auth/customer/restore'));
  await pending[0].route.fulfill({ contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': url('customer'), 'Access-Control-Allow-Credentials': 'true' }, body: JSON.stringify(pending[0].payload) });
  await oldResponse;
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(page.getByRole('status')).toContainText('Đang khôi phục phiên đăng nhập');
  await expect(loginHeading(page, 'customer')).toHaveCount(0); await expect(signedIn(page, 'customer')).toHaveCount(0);
  await expect(page).toHaveURL(`${url('customer')}/me/bookings`);
  await pending[1].route.fulfill({ contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': url('customer'), 'Access-Control-Allow-Credentials': 'true' }, body: JSON.stringify(pending[1].payload) });
  await expect(signedIn(page, 'customer')).toBeVisible(); expect(state.generation.customer).toBe(2);
});

test('M03 real action during a pending activity POST is retained and flushed after the next throttle interval', async ({ page, context }) => {
  const state = await fixture(context); await page.clock.install(); await login(page, 'customer');
  let first: Route | null = null, activityCalls = 0;
  await page.route('**/api/v1/browser-auth/customer/activity', async route => {
    activityCalls++;
    if (activityCalls === 1) { first = route; return; }
    await route.fulfill({ contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': url('customer'), 'Access-Control-Allow-Credentials': 'true' }, body: JSON.stringify({ data: {
      tokenType: 'Bearer', accessToken: 'customer-access-1', expiresInSeconds: 600,
      idleExpiresAt: new Date(Date.now() + 30 * 60_000).toISOString(), refreshExpiresAt: new Date(state.absoluteDeadline).toISOString(),
      user: { accountType: 'CUSTOMER', status: 'ACTIVE' },
    } }) });
  });
  await page.clock.runFor(10_000); await page.keyboard.press('Tab');
  await page.clock.runFor(51_000); await expect.poll(() => first !== null).toBe(true);
  await page.clock.runFor(5000); await page.keyboard.press('Tab');
  const completed = page.waitForResponse(response => response.url().endsWith('/browser-auth/customer/activity'));
  await first!.fulfill({ contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': url('customer'), 'Access-Control-Allow-Credentials': 'true' }, body: JSON.stringify({ data: {
    tokenType: 'Bearer', accessToken: 'customer-access-1', expiresInSeconds: 600,
    idleExpiresAt: new Date(Date.now() + 30 * 60_000).toISOString(), refreshExpiresAt: new Date(state.absoluteDeadline).toISOString(),
    user: { accountType: 'CUSTOMER', status: 'ACTIVE' },
  } }) });
  await completed; await page.clock.runFor(1000);
  await page.clock.runFor(58_000); expect(activityCalls).toBe(1);
  await page.clock.runFor(3000); await expect.poll(() => activityCalls).toBe(2);
  await page.clock.runFor(120_000); expect(activityCalls).toBe(2);
});
