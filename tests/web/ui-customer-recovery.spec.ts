import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const venueId = '0199f050-1000-7000-8000-000000000002';
const courtId = '0199f050-1000-7000-8000-000000000001';
const bookingId = '0199f050-1000-7000-8000-000000000003';
const review = `/booking-review?${new URLSearchParams({ venueId, courtId, date: '2026-10-20', startsAt: '18:00', endsAt: '19:00' })}`;
const slots = [{ startsAt: '18:00', endsAt: '18:30', pricePerSlot: 100000 }, { startsAt: '18:30', endsAt: '19:00', pricePerSlot: 150000 }];
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');
const loginData = { tokenType: 'Bearer', accessToken: 'customer-recovery-access', ...browserSessionFields(), expiresInSeconds: 600,
  user: { accountType: 'CUSTOMER', status: 'ACTIVE' } };
function booking() {
  return { bookingId, bookingNo: 'BK-RECOVERY', venueId, courtId, status: 'AWAITING_TRANSFER', version: 1,
    venueName: 'Hoàng Cầu', courtName: 'Sân 1', date: '2026-10-20', localStart: '18:00', localEnd: '19:00', timezone: 'Asia/Ho_Chi_Minh',
    slots, amount: 250000, paymentDeadline: '2026-10-09T04:20:00Z', createdAt: '2026-10-09T04:00:00Z', expiredAt: null,
    payment: { status: 'AWAITING_TRANSFER', bankCode: 'TEST', accountName: 'TEST CLUB', maskedAccountNumber: '******7890',
      transferContent: 'BK-RECOVERY', qrUrl: `/api/v1/bookings/${bookingId}/qr` } };
}
type Fixture = { creates: { key: string; body: string; authorization: string }[]; quoteCount: number; loginCount: number;
  abortFirst: boolean; rejection: string | null; expired: boolean };
async function fixture(page: Page): Promise<Fixture> {
  await page.clock.install({ time: new Date('2026-10-09T04:00:00Z') });
  const state: Fixture = { creates: [], quoteCount: 0, loginCount: 0, abortFirst: false, rejection: null, expired: false };
  await page.route('https://cdn.maptiler.com/**', route => route.abort());
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (data: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data }) });
    if (path.endsWith('/browser-auth/customer/login')) { state.loginCount++; return reply(loginData); }
    if (path.endsWith('/browser-auth/customer/restore')) return reply(loginData);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0, nextCursor: null }) });
    if (path.endsWith('/availability/quote')) {
      state.quoteCount++;
      expect(request.headers().authorization).toBe('Bearer customer-recovery-access');
      return reply({ quoteId: courtId, courtId, venueId, courtName: 'Sân 1', venueName: 'Hoàng Cầu', date: '2026-10-20', timezone: 'Asia/Ho_Chi_Minh',
        startsAt: '2026-10-20T11:00:00Z', endsAt: '2026-10-20T12:00:00Z', slots, amount: 250000, holdMinutes: 20,
        expiresAt: state.expired ? '2026-10-09T03:59:59Z' : '2026-10-09T04:02:00Z' });
    }
    if (path === '/api/v1/bookings') {
      state.creates.push({ key: request.headers()['idempotency-key'], body: request.postData()!, authorization: request.headers().authorization });
      if (state.abortFirst && state.creates.length === 1) return route.abort('failed');
      if (state.rejection) return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: state.rejection }) });
      return reply(booking(), 201);
    }
    if (path.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(booking());
    if (path === '/api/v1/me/bookings') return reply({ items: [booking()], nextCursor: null });
    const court = { id: courtId, name: 'Sân 1', bookingBlockMinutes: 60, minimumBookingMinutes: 60, holdMinutes: 20 };
    if (path === `/api/v1/venues/${venueId}/availability`) return reply({ venueId, date: '2026-10-20', timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30,
      courts: [{ ...court, courtId, slots: slots.map(slot => ({ ...slot, status: 'AVAILABLE', startsAtUtc: '2026-10-20T11:00:00Z', endsAtUtc: '2026-10-20T11:30:00Z' })) }] });
    const venue = { id: venueId, name: 'Hoàng Cầu', address: '31 ngõ 16 Hoàng Cầu, Hà Nội', contact: 'Liên hệ cơ sở', latitude: 21.02,
      longitude: 105.82, timezone: 'Asia/Ho_Chi_Minh', imageUrl: null, courts: [court] };
    if (path === `/api/v1/venues/${venueId}`) return reply(venue);
    if (path === '/api/v1/venues') return reply({ items: [venue], nextCursor: null });
    return route.abort();
  });
  return state;
}
async function login(page: Page, returnTo = review) {
  await page.goto(`http://localhost:5173/login?returnTo=${encodeURIComponent(returnTo)}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
}

test('unattempted expired casual quote cannot create and can explicitly requote', async ({ page }) => {
  const state = await fixture(page); state.expired = true; await login(page);
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Lấy báo giá mới' })).toBeEnabled();
  await expect(page.getByText('Chưa xác định được kết quả tạo đơn.', { exact: false })).toHaveCount(0);
  expect(state.creates).toHaveLength(0);
  state.expired = false;
  await page.getByRole('button', { name: 'Lấy báo giá mới' }).click();
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toBeEnabled();
  expect(state.creates).toHaveLength(0);
});

test('lost casual create response replays immutable key and body after quote TTL without replacing quote', async ({ page }) => {
  const state = await fixture(page); state.abortFirst = true; await login(page);
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('alert')).toContainText('Không thể kết nối');
  await expect(page.getByRole('button', { name: 'Lấy báo giá mới' })).toBeDisabled();
  const quoteCount = state.quoteCount;
  await page.clock.fastForward(121000);
  await expect(page.getByRole('status').filter({ hasText: 'Việc gửi lại chỉ kiểm tra yêu cầu cũ' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toBeEnabled();
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-RECOVERY' })).toBeVisible();
  expect(state.creates).toHaveLength(2); expect(state.creates[0].key).toBeTruthy();
  expect(state.creates[1]).toEqual(state.creates[0]); expect(state.quoteCount).toBe(quoteCount);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

for (const code of ['QUOTE_EXPIRED', 'QUOTE_CHANGED', 'QUOTE_CONSUMED', 'SLOT_UNAVAILABLE', 'IDEMPOTENCY_KEY_REUSED', 'SCHEDULE_UNAVAILABLE', 'PRICE_UNAVAILABLE', 'PAYMENT_SETUP_UNAVAILABLE']) {
  test(`casual definitive ${code} clears pending intent after an uncertain create without silently requoting`, async ({ page }) => {
    const state = await fixture(page); state.abortFirst = true; await login(page);
    await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
    await expect(page.getByRole('alert')).toContainText('Không thể kết nối');
    const quoteCount = state.quoteCount;
    await page.clock.fastForward(121000); state.rejection = code;
    await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
    await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toHaveCount(0);
    await expect(page.locator('.quote-hold-notice')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Lấy báo giá mới' })).toBeEnabled();
    await expect(page.getByRole('link', { name: 'Xem Đơn của tôi' })).toBeVisible();
    expect(state.creates).toHaveLength(2); expect(state.creates[1]).toEqual(state.creates[0]);
    expect(state.quoteCount).toBe(quoteCount);
  });
}

test('venue list uses SPA navigation without a map and preserves authenticated Customer session', async ({ page }) => {
  const state = await fixture(page);
  let mapRequests = 0; let documentRequests = 0;
  page.on('request', request => {
    if (request.url().includes('cdn.maptiler.com') || request.url().includes('api.maptiler.com/maps/')) mapRequests++;
    if (request.isNavigationRequest() && request.frame() === page.mainFrame()) documentRequests++;
  });
  await login(page, '/venues');
  await expect(page.getByRole('heading', { name: 'Chọn cơ sở phù hợp với bạn' })).toBeVisible();
  await expect(page.locator('.venue-map, .discovery-map-panel')).toHaveCount(0);
  const documentCount = documentRequests;
  await page.getByRole('link', { name: 'Xem lịch các sân →', exact: true }).click();
  await expect(page).toHaveURL(new RegExp('/venues/' + venueId + '$'));
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
  expect(documentRequests).toBe(documentCount); expect(state.loginCount).toBe(1); expect(mapRequests).toBe(0);
  await page.getByLabel('Ngày chơi', { exact: true }).fill('2026-10-20');
  await page.getByRole('button', { name: /Sân 1, 18:00 đến 18:30/ }).click();
  await page.getByRole('button', { name: /Sân 1, 18:30 đến 19:00/ }).click();
  await page.getByRole('button', { name: 'Tiếp tục đặt vãng lai', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Xác nhận đặt vãng lai' })).toBeVisible();
  expect(state.quoteCount).toBeGreaterThan(0); expect(state.loginCount).toBe(1);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
