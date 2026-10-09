import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const id = (n: number) => `019a1234-1111-7111-8111-${String(n).padStart(12, '0')}`;
const deferred = () => { let resolve!: () => void; const promise = new Promise<void>(done => { resolve = done; }); return { promise, resolve }; };
async function fixture(page: Page) {
  const row = (n: number, status = 'AWAITING_OWNER_CONFIRMATION') => ({ bookingId: id(n), bookingNo: `BK-${n}`,
    venueId: 'v1', venueName: 'Hoàng Cầu', courtName: 'Pickleball 1', date: '2026-10-20', timezone: 'Asia/Ho_Chi_Minh',
    localStart: '18:00:00', localEnd: '20:00:00', amount: 400000, status, version: 2 });
  const state = {
    bookings: Array.from({ length: 25 }, (_, n) => row(25 - n)),
    notices: Array.from({ length: 55 }, (_, n) => ({ id: `n${55 - n}`, title: `Thông báo ${55 - n}`, body: 'Hồ sơ cần xem', readAt: null as string | null })),
    listCalls: [] as string[], noticeCalls: [] as string[], denied: 0, noticeDenied: 0, previewFail: false,
    listGate: null as ReturnType<typeof deferred> | null, readGate: null as ReturnType<typeof deferred> | null, readStarted: false,
    row,
  };
  const venue = (venueId: string) => ({ id: venueId, name: venueId === 'v1' ? 'Hoàng Cầu' : 'Đống Đa', status: 'ACTIVE',
    courts: [{ id: 'c1', name: 'Pickleball 1', status: 'ACTIVE', hours: [], prices: [] }] });
  const business = { id: 'b1', name: 'Hoàng Cầu Sports', status: 'ACTIVE', legalName: 'Test', contact: 'Test', version: 1,
    venues: [venue('v1'), venue('v2')] };
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const req = route.request(), url = new URL(req.url()), path = url.pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true', 'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match,Idempotency-Key',
      'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS' };
    if (req.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    const respond = (data: unknown, extra = {}) => route.fulfill({ status: 200, headers, contentType: 'application/json', body: JSON.stringify({ data, ...extra }) });
    const deny = (status: number) => route.fulfill({ status, headers, contentType: 'application/problem+json', body: JSON.stringify({ code: status === 403 ? 'FORBIDDEN' : 'NOT_FOUND' }) });
    if (path === '/api/v1/browser-auth/partner/login') return respond({ accessToken: 'partner-test', ...browserSessionFields(), user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/partner-onboarding/businesses') return respond([business]);
    if (path === '/api/v1/partner-onboarding/businesses/b1') return respond(business);
    if (path === '/api/v1/me/notifications/') {
      state.noticeCalls.push(url.search); if (state.noticeDenied) return deny(state.noticeDenied);
      const before = url.searchParams.get('before'), start = before ? state.notices.findIndex(n => n.id === before) + 1 : 0;
      const items = state.notices.slice(start, start + 50);
      return respond(items, { unreadCount: state.notices.filter(n => !n.readAt).length,
        nextCursor: start + items.length < state.notices.length ? items.at(-1)!.id : null });
    }
    if (path.endsWith('/read')) {
      state.readStarted = true; if (state.readGate) await state.readGate.promise;
      const notice = state.notices.find(n => path.includes(`/${n.id}/`)); if (notice) notice.readAt = '2026-10-09T00:00:00Z';
      return respond({});
    }
    if (/\/operator\/venues\/v[12]\/bookings$/.test(path)) {
      state.listCalls.push(path + url.search); const gate = state.listGate; if (gate) await gate.promise;
      if (state.denied) return deny(state.denied);
      const scoped = path.includes('/v2/') ? [state.row(90)] : state.bookings;
      const filtered = scoped.filter(item => !url.searchParams.get('status') || item.status === url.searchParams.get('status'));
      const before = url.searchParams.get('before'), start = before ? filtered.findIndex(n => n.bookingId === before) + 1 : 0;
      const items = filtered.slice(start, start + 20);
      return respond({ items, nextCursor: start + items.length < filtered.length ? items.at(-1)!.bookingId : null,
        counts: { awaitingOwnerConfirmation: scoped.filter(n => n.status === 'AWAITING_OWNER_CONFIRMATION').length, needsReview: 0 } });
    }
    if (path.endsWith('/operations')) return respond({ id: 'c1', name: 'Pickleball 1', status: 'ACTIVE', version: 1,
      timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 30, minimumBookingMinutes: 60, holdMinutes: 20, hours: [], basePrices: [], rules: [] });
    if (path.endsWith('/price-preview')) return state.previewFail ? deny(400) : respond({ totalPrice: 200000, slots: [] });
    if (path.endsWith('/maintenance')) return respond([]);
    return respond({});
  });
  return state;
}
async function login(page: Page, hash = '#/bookings') {
  await page.goto(`http://localhost:5174/${hash}`);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.locator('.status-banner')).toContainText('Hoàng Cầu Sports');
}
const focus = (page: Page) => page.evaluate(() => window.dispatchEvent(new Event('focus')));

test('Partner refresh re-fetches loaded booking pages with fresh statuses, new rows, filter and cursor', async ({ page }) => {
  const api = await fixture(page); await login(page);
  await expect(page.locator('.booking-list > li')).toHaveCount(20);
  await page.getByRole('button', { name: 'Xem thêm đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(25);
  api.bookings.at(-1)!.status = 'CONFIRMED'; api.bookings.unshift(api.row(26));
  const before = api.listCalls.length; await focus(page);
  await expect(page.locator('.booking-list > li')).toHaveCount(26);
  await expect(page.locator('.booking-list > li').filter({ hasText: 'BK-1' }).filter({ hasText: 'Đã xác nhận' })).toHaveCount(1);
  expect(api.listCalls.slice(before).some(url => url.includes('before='))).toBe(true);
  await page.getByRole('button', { name: 'Tải lại đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(26);
  await page.getByLabel('Trạng thái đơn', { exact: true }).selectOption('AWAITING_OWNER_CONFIRMATION');
  await page.getByRole('button', { name: 'Lọc đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(20);
  await page.getByRole('button', { name: 'Xem thêm đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(25);
  await expect(page.locator('.booking-list')).not.toContainText('Đã xác nhận');
});

test('Partner pending refresh keeps rows visible and delayed old venue response cannot replace new scope', async ({ page }) => {
  const api = await fixture(page); await login(page);
  await expect(page.locator('.booking-list > li')).toHaveCount(20);
  await page.getByRole('button', { name: 'Xem thêm đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(25);
  const gate = deferred(); api.listGate = gate; const before = api.listCalls.length; await focus(page);
  await expect.poll(() => api.listCalls.length).toBeGreaterThan(before);
  await expect(page.locator('.booking-list > li')).toHaveCount(25);
  api.listGate = null;
  await page.getByLabel('Cơ sở xem đơn', { exact: true }).selectOption('v2');
  await expect(page.locator('.booking-list > li')).toHaveCount(1); await expect(page.locator('.booking-list')).toContainText('BK-90');
  gate.resolve(); await expect(page.locator('.booking-list > li')).toHaveCount(1);
});

for (const status of [403, 404]) test(`Partner denied refresh ${status} purges loaded booking history`, async ({ page }) => {
  const api = await fixture(page); await login(page);
  await expect(page.locator('.booking-list > li')).toHaveCount(20);
  await page.getByRole('button', { name: 'Xem thêm đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(25);
  api.denied = status; await focus(page);
  await expect(page.locator('.booking-list > li')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Quản lý đơn đặt sân' }).getByRole('alert').first()).toBeVisible();
});

test('Partner notification poll and delayed read retain older pages with authoritative unread/read state', async ({ page }) => {
  const api = await fixture(page); await login(page, '#/notifications');
  await expect(page.locator('.notice-list > li')).toHaveCount(50);
  await page.getByRole('button', { name: 'Thông báo trước đó', exact: true }).click();
  await expect(page.locator('.notice-list > li')).toHaveCount(55);
  api.notices.unshift({ id: 'n56', title: 'Thông báo 56', body: 'Mới', readAt: null }); await focus(page);
  await expect(page.locator('.notice-list > li')).toHaveCount(56);
  const oldest = page.locator('.notice-list > li').filter({ has: page.getByText('Thông báo 1', { exact: true }) });
  const gate = deferred(); api.readGate = gate;
  await oldest.getByRole('button', { name: 'Đã đọc', exact: true }).click();
  await expect.poll(() => api.readStarted).toBe(true); const calls = api.noticeCalls.length;
  await focus(page); expect(api.noticeCalls.length).toBe(calls);
  await expect(page.locator('.notice-list > li')).toHaveCount(56);
  gate.resolve();
  await expect(oldest.getByRole('button', { name: 'Đã đọc', exact: true })).toHaveCount(0);
  await expect(oldest.locator('.status-badge')).toHaveText('Đã đọc');
  await expect(page.locator('.notice-list > li')).toHaveCount(56);
  await expect(page.locator('.notice-list > li.unread')).toHaveCount(55);
  await page.getByRole('button', { name: 'Tải lại thông báo', exact: true }).click();
  await expect(page.locator('.notice-list > li')).toHaveCount(56);
});

test('Partner denied notification refresh clears older pages and unread state', async ({ page }) => {
  const api = await fixture(page); await login(page, '#/notifications');
  await expect(page.locator('.notice-list > li')).toHaveCount(50);
  await page.getByRole('button', { name: 'Thông báo trước đó', exact: true }).click();
  await expect(page.locator('.notice-list > li')).toHaveCount(55);
  api.noticeDenied = 403; await focus(page);
  await expect(page.locator('.notice-list > li')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Thông báo của bạn' }).getByRole('alert')).toBeVisible();
});

test('Partner preview reports calculated, write reports saved, failure has no stale price or success style', async ({ page }) => {
  const api = await fixture(page); await login(page, '#/schedule');
  const preview = page.locator('#operator-preview'); await expect(preview).toBeVisible();
  await preview.getByRole('button', { name: 'Xem giá', exact: true }).click();
  const operations = page.getByRole('region', { name: 'Vận hành sân', exact: true });
  await expect(operations.locator('.feedback.success')).toHaveText('Đã tính giá.');
  await expect(preview).toContainText('Tổng giá:');
  await page.locator('#operator-policy').getByRole('button', { name: 'Lưu quy định', exact: true }).click();
  await expect(operations.locator('.feedback.success')).toHaveText('Đã lưu.');
  api.previewFail = true; await preview.getByRole('button', { name: 'Xem giá', exact: true }).click();
  await expect(operations.locator('.feedback.error')).toBeVisible();
  await expect(operations.locator('.feedback.success')).toHaveCount(0);
  await expect(preview).not.toContainText('Tổng giá:');
});
