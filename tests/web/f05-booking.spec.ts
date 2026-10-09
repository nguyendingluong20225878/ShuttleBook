import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const courtId = '0199f050-0000-7000-8000-000000000001';
const venueId = '0199f050-0000-7000-8000-000000000002';
const bookingId = '0199f050-0000-7000-8000-000000000003';
const review = `/booking-review?venueId=${venueId}&courtId=${courtId}&date=2026-10-20&startsAt=18%3A00&endsAt=19%3A00`;
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');
const slots = [{ startsAt: '18:00', endsAt: '18:30', pricePerSlot: 100000 }, { startsAt: '18:30', endsAt: '19:00', pricePerSlot: 150000 }];
function booking() { return { bookingId, bookingNo: 'BK-F05-DEMO', venueId, courtId, status: 'AWAITING_TRANSFER', venueName: 'Hoàng Cầu', courtName: 'Sân 1',
  date: '2026-10-20', localStart: '18:00', localEnd: '19:00', timezone: 'Asia/Ho_Chi_Minh', slots, amount: 250000,
  version: 1, paymentDeadline: new Date(Date.now() + 20 * 60_000).toISOString(), createdAt: new Date().toISOString(), expiredAt: null,
  payment: { status: 'AWAITING_TRANSFER', bankCode: 'TEST', accountName: 'TEST CLUB', maskedAccountNumber: '******7890',
    transferContent: 'BK-F05-DEMO', qrUrl: `/api/v1/bookings/${bookingId}/qr` } }; }

test('guest returns after login, retries one intent, reads QR and recovers booking after F5', async ({ page }) => {
  const keys: string[] = []; let createCount = 0;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const url = new URL(route.request().url());
    const reply = (value: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (url.pathname.endsWith('/browser-auth/customer/login')) return reply({ tokenType: 'Bearer', accessToken: 'access-test', ...browserSessionFields(), expiresInSeconds: 600, user: { accountType: 'CUSTOMER', status: 'ACTIVE' } });
    if (url.pathname.endsWith('/availability/quote')) { expect(route.request().headers().authorization).toBe('Bearer access-test'); return reply({ quoteId: courtId, courtId, venueId, courtName: 'Sân 1', venueName: 'Hoàng Cầu', date: '2026-10-20',
      timezone: 'Asia/Ho_Chi_Minh', startsAt: '2026-10-20T11:00:00Z', endsAt: '2026-10-20T12:00:00Z',
      slots: slots.map(slot => ({ ...slot, pricePerSlot: slot.pricePerSlot + 1, pricePerSlotExact: String(slot.pricePerSlot) })),
      amount: 250001, amountExact: '250000', holdMinutes: 20, expiresAt: new Date(Date.now() + 120000).toISOString() }); }
    if (url.pathname === '/api/v1/bookings') { keys.push(route.request().headers()['idempotency-key']); createCount++;
      expect(route.request().headers().authorization).toBe('Bearer access-test');
      return createCount === 1 ? route.abort() : reply(booking(), 201); }
    if (url.pathname.endsWith('/qr')) return route.fulfill({ status: 200, contentType: 'image/png', body: png });
    if (url.pathname === '/api/v1/me/bookings') return reply({ items: [booking()], nextCursor: null });
    if (url.pathname === `/api/v1/bookings/${bookingId}`) return reply(booking());
    return route.abort();
  });
  await page.goto(`http://localhost:5173${review}`);
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Xác nhận đặt vãng lai' })).toBeVisible();
  await expect(page.getByText('Tổng tiền: 250.000đ')).toBeVisible();
  await page.getByText('Chi tiết giá từng ca 30 phút').click();
  await expect(page.getByText('100.000đ')).toBeVisible();
  await expect(page.getByText('150.000đ')).toBeVisible();
  await expect(page.locator('.quote-hold-notice')).toContainText('Đang giữ chỗ tạm cho bạn');
  await expect(page.locator('.quote-hold-notice')).toContainText('các ca sẽ tự trở về trống');
  await page.screenshot({ path: test.info().outputPath('f05-quote.png'), fullPage: true });
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('alert')).toContainText('Không thể kết nối');
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-F05-DEMO' })).toBeVisible();
  expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
  await expect(page.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đã chuyển khoản', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Hủy|Đổi lịch/ })).toHaveCount(0);
  await page.screenshot({ path: test.info().outputPath('f05-booking.png'), fullPage: true });
  await page.getByRole('link', { name: 'Đơn của tôi' }).first().click();
  await expect(page.getByRole('heading', { name: 'Đơn của tôi' })).toBeVisible();
  await page.getByRole('link', { name: 'BK-F05-DEMO' }).click(); await page.reload();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-F05-DEMO' })).toBeVisible();
  expect(createCount).toBe(2); expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('quote limit and active quote errors explain the next step without creating a booking', async ({ page }) => {
  let quotes = 0; let creates = 0;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/browser-auth/customer/login')) return route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ data: { tokenType: 'Bearer', accessToken: 'access-test', ...browserSessionFields(), expiresInSeconds: 600,
        user: { accountType: 'CUSTOMER', status: 'ACTIVE' } } }) });
    if (path.endsWith('/availability/quote')) {
      quotes++;
      const code = quotes === 1 ? 'ACTIVE_QUOTE_EXISTS' : 'AMOUNT_LIMIT_EXCEEDED';
      return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code }) });
    }
    if (path === '/api/v1/bookings') creates++;
    return route.abort();
  });
  await page.goto(`http://localhost:5173${review}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('alert')).toContainText('đang giữ một báo giá còn hiệu lực');
  await page.getByRole('button', { name: 'Lấy báo giá mới' }).click();
  await expect(page.getByRole('alert')).toContainText('vượt 10.000.000 ₫');
  expect(quotes).toBe(2); expect(creates).toBe(0);
});

test('expired quote and price change require a new quote', async ({ page }) => {
  let renewed = false;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/browser-auth/customer/login')) return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { tokenType: 'Bearer', accessToken: 'a', ...browserSessionFields(), expiresInSeconds: 600, user: { accountType: 'CUSTOMER', status: 'ACTIVE' } } }) });
    if (url.pathname.endsWith('/availability/quote')) { return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { quoteId: courtId, courtId, venueId, date: '2026-10-20', timezone: 'Asia/Ho_Chi_Minh',
      courtName: 'Sân 1', venueName: 'Hoàng Cầu', startsAt: '2026-10-20T11:00:00Z', endsAt: '2026-10-20T12:00:00Z', amount: 250000, slots, holdMinutes: 20,
      expiresAt: new Date(Date.now() + (renewed ? 120000 : -1000)).toISOString() } }) }); }
    return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: 'QUOTE_CHANGED' }) });
  });
  await page.goto(`http://localhost:5173/login?returnTo=${encodeURIComponent(review)}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test'); await page.getByLabel('Mật khẩu').fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toBeDisabled();
  await expect(page.locator('.quote-hold-notice')).toContainText('Đã hết thời gian giữ chỗ tạm');
  await expect(page.locator('.quote-hold-notice strong')).toHaveText('0:00');
  // Aborted reads during navigation must not advance the mock's business state.
  renewed = true;
  await page.getByRole('button', { name: 'Lấy báo giá mới' }).click();
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toBeEnabled(); await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('alert')).toContainText('đã thay đổi');
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toHaveCount(0);
});

test('consumed casual quote clears its create intent and directs customer to existing bookings', async ({ page }) => {
  const creates: { key?: string; quoteId: string }[] = []; let quoteCount = 0;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const request = route.request(); const pathname = new URL(request.url()).pathname;
    const reply = (data: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data }) });
    if (pathname.endsWith('/browser-auth/customer/login')) return reply({ tokenType: 'Bearer', accessToken: 'access-test', ...browserSessionFields(), expiresInSeconds: 600, user: { accountType: 'CUSTOMER', status: 'ACTIVE' } });
    if (pathname.endsWith('/availability/quote')) {
      quoteCount++; return reply({ quoteId: quoteCount === 1 ? courtId : venueId, courtId, venueId, date: '2026-10-20', timezone: 'Asia/Ho_Chi_Minh',
        courtName: 'Sân 1', venueName: 'Hoàng Cầu', startsAt: '2026-10-20T11:00:00Z', endsAt: '2026-10-20T12:00:00Z', slots, amount: 250000, holdMinutes: 20, expiresAt: new Date(Date.now() + 120000).toISOString() });
    }
    if (pathname === '/api/v1/bookings') {
      creates.push({ key: request.headers()['idempotency-key'], quoteId: request.postDataJSON().quoteId });
      if (creates.length === 1) return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: 'QUOTE_CONSUMED' }) });
      return reply(booking(), 201);
    }
    if (pathname === `/api/v1/bookings/${bookingId}`) return reply(booking());
    if (pathname.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    return route.abort();
  });
  await page.goto(`http://localhost:5173/login?returnTo=${encodeURIComponent(review)}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('alert')).toContainText('Báo giá này đã được dùng để tạo đơn');
  await expect(page.getByRole('alert')).toContainText('Đơn của tôi');
  await expect(page.getByRole('button', { name: 'Xác nhận tạo đơn' })).toHaveCount(0);
  await expect(page.locator('.quote-hold-notice')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Xem Đơn của tôi' })).toBeVisible();
  expect(creates).toHaveLength(1);
  await page.getByRole('button', { name: 'Lấy báo giá mới' }).click();
  await page.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-F05-DEMO' })).toBeVisible();
  expect(creates).toHaveLength(2); expect(creates[1].quoteId).toBe(venueId); expect(creates[1].key).not.toBe(creates[0].key);
});
