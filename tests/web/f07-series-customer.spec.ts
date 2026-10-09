import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const venueId = '0199f070-0000-7000-8000-000000000011';
const courtId = '0199f070-0000-7000-8000-000000000012';
const bookingId = '0199f070-0000-7000-8000-000000000031';
const seriesId = '0199f070-0000-7000-8000-000000000032';
const firstDate = '2026-10-13';
const dates = [firstDate, '2026-10-20', '2026-10-27', '2026-11-03', '2026-11-10'];
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');
const loginData = { tokenType: 'Bearer', accessToken: 'series-ui-access', ...browserSessionFields(), expiresInSeconds: 600,
  user: { accountType: 'CUSTOMER', status: 'ACTIVE' } };
const slots = (amount = 500000) => [0, 1, 2, 3].map(index => ({ startsAt: ['18:00', '18:30', '19:00', '19:30'][index],
  endsAt: ['18:30', '19:00', '19:30', '20:00'][index], pricePerSlot: amount / 4 }));
function quote() {
  return { quoteId: seriesId, expiresAt: new Date(Date.now() + 120_000).toISOString(), canCreate: true, courtId, venueId,
    courtName: 'Sân 1', venueName: 'Hoàng Cầu', timezone: 'Asia/Ho_Chi_Minh', dayOfWeek: 'TUESDAY', startsOn: firstDate,
    endsOn: '2026-11-13', localStartTime: '18:00', durationMinutes: 120, occurrenceCount: 5, amount: 2500000,
    amountExact: '2500000', currency: 'VND', holdMinutes: 20, minimumBookingMinutes: 120, bookingBlockMinutes: 60,
    occurrences: dates.map((date, index) => ({ date, localStart: '18:00', localEnd: '20:00', startsAt: `${date}T11:00:00Z`,
      endsAt: `${date}T13:00:00Z`, amount: 400000 + index * 50000, amountExact: `${400000 + index * 50000}`, slots: slots(400000 + index * 50000) })),
    conflicts: [] as { date: string; startsAt: string; endsAt: string; code: string }[] };
}
function booking(status = 'AWAITING_TRANSFER') {
  return { bookingId, bookingNo: 'BK-F07-ANCHOR', bookingType: 'RECURRING_OCCURRENCE', status, version: 1,
    venueName: 'Hoàng Cầu', courtName: 'Sân 1', date: firstDate, localStart: '18:00', localEnd: '20:00', timezone: 'Asia/Ho_Chi_Minh',
    amount: 2500000, amountExact: '2500000', paymentDeadline: new Date(Date.now() + 20 * 60_000).toISOString(),
    createdAt: '2026-10-07T04:00:00Z', expiredAt: null, slots: slots(400000), evidence: [], decisions: [],
    series: { seriesId, seriesNo: 'SR-F07-DEMO', startsOn: firstDate, endsOn: '2026-11-13', dayOfWeek: 'TUESDAY', localStartTime: '18:00',
      durationMinutes: 120, occurrenceCount: 5, paymentPlan: 'FULL_SERIES', occurrences: dates.map((date, index) => ({
        bookingId: index ? `${bookingId.slice(0, -2)}4${index}` : bookingId, date, localStart: '18:00', localEnd: '20:00',
        startsAt: `${date}T11:00:00Z`, endsAt: `${date}T13:00:00Z`, amount: 400000 + index * 50000,
        amountExact: `${400000 + index * 50000}`, status })) },
    payment: { status: 'AWAITING_TRANSFER', bankCode: 'DEMO', accountName: 'TEST CLUB', maskedAccountNumber: '******7890',
      expectedAmount: 2500000, expectedAmountExact: '2500000', transferContent: 'SR-F07-DEMO', qrUrl: `/api/v1/bookings/${bookingId}/qr`,
      firstReportedAt: null as string | null, confirmedAt: null as string | null } };
}
type Fixture = { preview: ReturnType<typeof quote>; current: ReturnType<typeof booking>; quoteBodies: unknown[];
  creates: { key?: string; body: unknown }[]; reports: { key?: string; version?: string; body: unknown }[];
  loseCreateResponse: boolean; rejectCreateCode: string | null; rejectCreateStatus: number; transferStatus: string | null };
async function fixture(page: Page): Promise<Fixture> {
  await page.clock.install({ time: new Date('2026-10-07T04:00:00Z') });
  const state: Fixture = { preview: quote(), current: booking(), quoteBodies: [], creates: [], reports: [],
    loseCreateResponse: false, rejectCreateCode: null, rejectCreateStatus: 409, transferStatus: null };
  state.preview.expiresAt = '2026-10-07T04:02:00Z';
  state.current.paymentDeadline = '2026-10-07T04:20:00Z';
  await page.route('https://cdn.maptiler.com/**', route => route.abort());
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (data: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data }) });
    if (path.endsWith('/browser-auth/customer/login') || path.endsWith('/browser-auth/customer/restore')) return reply(loginData);
    if (path.endsWith('/browser-auth/customer/logout')) return route.fulfill({ status: 204 });
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0, nextCursor: null }) });
    if (path === `/api/v1/venues/${venueId}`) return reply({ id: venueId, name: 'Hoàng Cầu', address: '31 ngõ 16 Hoàng Cầu, Hà Nội',
      contact: 'Liên hệ cơ sở', latitude: 21.02, longitude: 105.82, timezone: 'Asia/Ho_Chi_Minh', imageUrl: null,
      courts: [0, 1, 2].map(index => ({ id: index ? `${courtId.slice(0, -1)}${index + 2}` : courtId, name: `Sân ${index + 1}`,
        minimumBookingMinutes: 120, bookingBlockMinutes: 60, holdMinutes: 20 })) });
    if (path.endsWith('/availability')) return reply({ venueId, date: new URL(request.url()).searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh',
      generatedAt: new Date().toISOString(), stepMinutes: 30, courts: [0, 1, 2].map(index => ({ courtId: index ? `${courtId.slice(0, -1)}${index + 2}` : courtId,
        name: `Sân ${index + 1}`, minimumBookingMinutes: 120, bookingBlockMinutes: 60, holdMinutes: 20,
        slots: [...slots(), { startsAt: '20:00', endsAt: '20:30', pricePerSlot: 100000 }].map(slot => ({ ...slot, status: 'AVAILABLE',
          startsAtUtc: `${firstDate}T11:00:00Z`, endsAtUtc: `${firstDate}T11:30:00Z` })) })) });
    if (path === '/api/v1/booking-series/quote') {
      expect(request.headers().authorization).toBe('Bearer series-ui-access');
      state.quoteBodies.push(request.postDataJSON()); return reply(state.preview);
    }
    if (path === '/api/v1/booking-series') {
      state.creates.push({ key: request.headers()['idempotency-key'], body: request.postDataJSON() });
      if (state.loseCreateResponse && state.creates.length === 1) return route.abort('failed');
      if (state.rejectCreateCode) return route.fulfill({ status: state.rejectCreateStatus, contentType: 'application/problem+json', body: JSON.stringify({ code: state.rejectCreateCode,
        conflictDates: state.rejectCreateCode === 'SERIES_CONFLICT' ? ['2026-11-03'] : [] }) });
      return reply(state.current, 201);
    }
    if (path.endsWith('/transfer-evidence')) {
      state.reports.push({ key: request.headers()['idempotency-key'], version: request.headers()['if-match'], body: request.postDataJSON() });
      state.current = { ...state.current, status: 'AWAITING_OWNER_CONFIRMATION', version: 2,
        series: { ...state.current.series, occurrences: state.current.series.occurrences.map(occurrence => ({ ...occurrence, status: 'AWAITING_OWNER_CONFIRMATION' })) },
        payment: { ...state.current.payment, status: 'TRANSFER_REPORTED', firstReportedAt: new Date().toISOString() } };
      return reply(state.current);
    }
    if (path.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.startsWith('/api/v1/bookings/')) return reply(state.current);
    if (path.endsWith('/me/bookings')) return reply({ items: [state.current], nextCursor: null });
    return route.abort();
  });
  return state;
}
const reviewUrl = `/series-review?${new URLSearchParams({ venueId, courtId, date: firstDate, startsAt: '18:00', endsAt: '20:00' })}`;
async function login(page: Page, returnTo = reviewUrl) {
  await page.goto(`http://localhost:5173/login?returnTo=${encodeURIComponent(returnTo)}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Strong-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
}

test('fixed mode uses every court row and passes five half-hour slots through customer login', async ({ page }) => {
  const state = await fixture(page);
  await page.goto(`http://localhost:5173/venues/${venueId}?date=${firstDate}`);
  await expect(page.locator('.schedule-grid tbody tr')).toHaveCount(3);
  await page.getByRole('button', { name: 'Cố định hằng tuần', exact: true }).click();
  await page.getByRole('button', { name: /Sân 1, 18:00 đến 18:30/ }).click();
  await expect(page.getByRole('button', { name: 'Tiếp tục đặt cố định', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: /Sân 1, 20:00 đến 20:30/ }).click();
  await expect(page.getByRole('button', { name: 'Tiếp tục đặt cố định', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Tiếp tục đặt cố định', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  const returnTo = new URL(page.url()).searchParams.get('returnTo')!;
  expect(new URL(returnTo, 'http://localhost:5173').pathname).toBe('/series-review');
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Strong-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Thiết lập lịch cố định' })).toBeVisible();
  await expect(page.getByLabel('Thời lượng mỗi buổi (phút)')).toHaveValue('150');
  await expect(page.getByLabel('Ngày trong tuần')).toHaveValue('TUESDAY');
  await expect(page.getByLabel('Ngày kết thúc kỳ')).toHaveValue('2026-11-13');
  expect(state.quoteBodies).toHaveLength(0); expect(state.creates).toHaveLength(0);
});

test('fixed review validates court minimum, half-hour grid and calendar-month period before quote', async ({ page }) => {
  const state = await fixture(page); await login(page);
  await expect(page.getByRole('heading', { name: 'Thiết lập lịch cố định' })).toBeVisible();
  await page.getByLabel('Thời lượng mỗi buổi (phút)').fill('90');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('ít nhất 120 phút');
  await page.getByLabel('Thời lượng mỗi buổi (phút)').fill('150');
  await page.getByLabel('Giờ bắt đầu').fill('18:15');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('mốc 30 phút');
  await page.getByLabel('Giờ bắt đầu').fill('18:00');
  await page.getByLabel('Ngày kết thúc kỳ').fill('2026-11-10');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('ít nhất một tháng theo lịch');
  expect(state.quoteBodies).toHaveLength(0);
  await page.getByLabel('Ngày kết thúc kỳ').fill('2026-12-07');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('60 ngày đặt trước');
  expect(state.quoteBodies).toHaveLength(0);
  await page.getByLabel('Ngày kết thúc kỳ').fill('2026-11-13');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toBeVisible();
  expect(state.quoteBodies[0]).toEqual({ courtId, dayOfWeek: 'TUESDAY', localStartTime: '18:00', durationMinutes: 150,
    startsOn: firstDate, endsOn: '2026-11-13' });
  await page.getByLabel('Ngày trong tuần').selectOption('THURSDAY');
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toHaveCount(0);
  expect(state.creates).toHaveLength(0);
});

test('a fourth-week conflict prevents creating or silently skipping an occurrence', async ({ page }) => {
  const state = await fixture(page);
  state.preview = { ...state.preview, canCreate: false, quoteId: null!, expiresAt: null!,
    conflicts: [{ date: '2026-11-03', startsAt: '2026-11-03T11:00:00Z', endsAt: '2026-11-03T13:00:00Z', code: 'SLOT_UNAVAILABLE' }] };
  await login(page);
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('2026-11-03');
  await expect(page.getByRole('alert')).toContainText('không tự bỏ buổi bị trùng');
  await expect(page.locator('.series-quote-list > li')).toHaveCount(5);
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toHaveCount(0);
  expect(state.creates).toHaveLength(0);
  await page.screenshot({ path: test.info().outputPath('f07-series-conflict.png'), fullPage: true });
});

test('series quote displays exact large totals and independent occurrence/slot prices', async ({ page }) => {
  const state = await fixture(page);
  state.preview.amountExact = '90071992547410005'; state.preview.amount = Number(state.preview.amountExact);
  state.preview.occurrences = state.preview.occurrences.map(occurrence => ({ ...occurrence, amountExact: '18014398509482001', amount: Number('18014398509482001'),
    slots: slots().map((slot, index) => ({ ...slot, pricePerSlot: index === 3 ? 4503599627370501 : 4503599627370500,
      pricePerSlotExact: index === 3 ? '4503599627370501' : '4503599627370500' })) }));
  await login(page); await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByText('Tổng tiền cả kỳ: 90.071.992.547.410.005đ', { exact: true })).toBeVisible();
  await page.getByText('Chi tiết giá từng ca 30 phút', { exact: true }).first().click();
  await expect(page.getByText('4.503.599.627.370.501đ', { exact: true }).first()).toBeVisible();
  for (const width of [375, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  await page.setViewportSize({ width: test.info().project.name === 'mobile' ? 375 : 1440, height: 900 });
  await page.screenshot({ path: test.info().outputPath('f07-series-review.png'), fullPage: true });
});

test('lost create response retries the same series intent after quote expires and uses one total QR', async ({ page }) => {
  const state = await fixture(page); state.loseCreateResponse = true;
  await login(page); await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await page.clock.fastForward(121_000);
  await expect(page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' })).toBeEnabled();
  await page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' }).click();
  await expect(page).toHaveURL(new RegExp(`/bookings/${bookingId}$`));
  await expect(page.getByRole('heading', { name: 'Mã đơn: SR-F07-DEMO', exact: true })).toBeVisible();
  await expect(page.getByText('Số tiền cả kỳ: 2.500.000đ', { exact: true })).toBeVisible();
  await expect(page.locator('.series-occurrence-list > li')).toHaveCount(5);
  await expect(page.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toHaveCount(1);
  expect(state.creates).toHaveLength(2); expect(state.creates[0]).toEqual(state.creates[1]);
  expect(state.creates[0].body).toEqual({ quoteId: seriesId });
  expect(state.creates[0].key).toMatch(/^[a-f0-9-]{36}$/);
});

test('expired and changed quotes require a new review while a create race reports conflict dates', async ({ page }) => {
  const state = await fixture(page); await login(page);
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.locator('.quote-hold-notice')).toContainText('Đang giữ chỗ tạm toàn kỳ cho bạn');
  await expect(page.locator('.quote-hold-notice')).toContainText('các buổi sẽ tự trở về trống');
  await page.clock.fastForward(121_000);
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toBeDisabled();
  await expect(page.locator('.quote-hold-notice')).toContainText('Đã hết thời gian giữ chỗ tạm');
  await expect(page.locator('.quote-hold-notice strong')).toHaveText('0:00');
  state.preview.expiresAt = '2026-10-07T04:17:00Z';
  await page.getByRole('button', { name: 'Lấy báo giá mới cho kỳ' }).click();
  state.rejectCreateCode = 'QUOTE_CHANGED';
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('alert')).toContainText('Giá hoặc cấu hình sân đã thay đổi');
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toHaveCount(0);
  state.rejectCreateCode = 'SERIES_CONFLICT';
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('alert')).toContainText('2026-11-03');
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toHaveCount(0);
  expect(state.creates).toHaveLength(2); expect(state.creates[0].key).not.toBe(state.creates[1].key);
});

test('series detail reports payment once for all occurrences and F5 restores the same series', async ({ page }) => {
  const state = await fixture(page); await login(page, `/bookings/${bookingId}`);
  await expect(page.getByRole('heading', { name: 'Mã đơn: SR-F07-DEMO' })).toBeVisible();
  await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByText('Tất cả các buổi trong kỳ vẫn được giữ; bạn không cần chuyển thêm tiền cho từng buổi.')).toBeVisible();
  expect(state.reports).toHaveLength(1); expect(state.reports[0].body).toEqual({}); expect(state.reports[0].version).toBe('"1"');
  await expect(page.locator('.series-occurrence-list').getByText('Chờ xác nhận', { exact: true })).toHaveCount(5);
  await expect(page.getByRole('button', { name: /Hủy|Đổi lịch|Thanh toán buổi/ })).toHaveCount(0);
  await page.screenshot({ path: test.info().outputPath('f07-series-reported.png'), fullPage: true });
  await page.reload(); await expect(page.getByRole('heading', { name: 'Mã đơn: SR-F07-DEMO' })).toBeVisible();
  await expect(page.locator('.series-occurrence-list').getByText('Chờ xác nhận', { exact: true })).toHaveCount(5);
  expect(state.reports).toHaveLength(1);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('My Bookings shows one fixed group and occurrence deep links redirect to its payment anchor', async ({ page }) => {
  const state = await fixture(page); state.current = booking('CONFIRMED');
  await login(page, '/me/bookings');
  await expect(page.locator('.booking-list > li')).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'SR-F07-DEMO', exact: true })).toHaveCount(1);
  await expect(page.getByText(/5 buổi cố định/)).toBeVisible();
  await page.getByRole('link', { name: 'SR-F07-DEMO', exact: true }).click();
  await expect(page.locator('.series-occurrence-list > li')).toHaveCount(5);
  const occurrenceId = state.current.series.occurrences[3].bookingId;
  await page.evaluate(id => { history.pushState(null, '', `/bookings/${id}`); window.dispatchEvent(new Event('popstate')); }, occurrenceId);
  await expect(page).toHaveURL(new RegExp(`/bookings/${bookingId}$`));
  await expect(page.getByRole('heading', { name: 'Mã đơn: SR-F07-DEMO' })).toBeVisible();
  expect(state.creates).toHaveLength(0); expect(state.reports).toHaveLength(0);
});

test('month-end period clamps by calendar month and an interval ending at midnight is rejected', async ({ page }) => {
  const state = await fixture(page);
  await page.clock.setFixedTime(new Date('2026-01-28T04:00:00Z'));
  await login(page, '/series-review?' + new URLSearchParams({ venueId, courtId, date: '2026-01-31', startsAt: '22:00', endsAt: '23:30' }));
  await expect(page.getByLabel('Ngày kết thúc kỳ')).toHaveValue('2026-02-28');
  await expect(page.getByLabel('Thời lượng mỗi buổi (phút)')).toHaveValue('120');
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('alert')).toContainText('phải nằm trong cùng ngày');
  expect(state.quoteBodies).toHaveLength(0);
});

test('revoked create access clears the private quote and prevents retrying its intent', async ({ page }) => {
  const state = await fixture(page); await login(page);
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await expect(page.getByRole('region', { name: 'Báo giá toàn kỳ' })).toBeVisible();
  state.rejectCreateCode = 'FORBIDDEN'; state.rejectCreateStatus = 403;
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('alert')).toContainText('không còn quyền');
  await expect(page.getByRole('region', { name: 'Báo giá toàn kỳ' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' })).toHaveCount(0);
  expect(state.creates).toHaveLength(1);
});

test('consumed series quote clears retry intent and allows explicit fresh quote review', async ({ page }) => {
  const state = await fixture(page); state.loseCreateResponse = true; await login(page);
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' })).toBeVisible();
  state.rejectCreateCode = 'QUOTE_CONSUMED';
  await page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' }).click();
  await expect(page.getByRole('alert')).toContainText('Báo giá này đã được dùng để tạo đơn');
  await expect(page.getByRole('alert')).toContainText('Đơn của tôi');
  await expect(page.getByRole('region', { name: 'Báo giá toàn kỳ' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Thử lại tạo lịch cùng yêu cầu' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Xem Đơn của tôi' })).toBeVisible();
  expect(state.creates).toHaveLength(2); expect(state.creates[0]).toEqual(state.creates[1]);
  state.rejectCreateCode = null; state.preview.quoteId = venueId;
  await page.getByRole('button', { name: 'Xem báo giá toàn kỳ' }).click();
  await page.getByRole('button', { name: 'Xác nhận tạo lịch cố định' }).click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: SR-F07-DEMO' })).toBeVisible();
  expect(state.creates).toHaveLength(3); expect(state.creates[2].body).toEqual({ quoteId: venueId });
  expect(state.creates[2].key).not.toBe(state.creates[1].key);
});
