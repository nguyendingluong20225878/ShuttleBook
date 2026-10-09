import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const anchor = '019a1234-1111-7111-8111-111111111111';
const seriesId = '019a1234-9999-7999-8999-999999999999';
const dates = ['2026-10-13', '2026-10-20', '2026-10-27', '2026-11-03', '2026-11-10'];

async function setup(page: Page, deniedPageStatus?: number) {
  const commands: Array<{ path: string; body: object; headers: Record<string, string> }> = [];
  const business = { id: 'b1', name: 'Hoàng Cầu Sports', status: 'ACTIVE', version: 1, legalName: 'Test', contact: 'Test',
    venues: [{ id: 'v1', name: 'Hoàng Cầu', status: 'ACTIVE', courts: [], address: 'Hà Nội', contact: 'Test' }] };
  let status = 'AWAITING_OWNER_CONFIRMATION'; let version = 2;
  const detail = () => ({ bookingId: anchor, businessId: 'b1', bookingNo: 'SR-TEST', bookingType: 'RECURRING_OCCURRENCE',
    venueId: 'v1', venueName: 'Hoàng Cầu', courtName: 'Pickleball 1', status, version, timezone: 'Asia/Ho_Chi_Minh',
    date: dates[0], localStart: '18:00:00', localEnd: '20:00:00', amount: 2000000, amountExact: '2000000',
    paymentDeadline: '2026-10-07T08:20:00Z', createdAt: '2026-10-07T08:00:00Z', customer: { maskedContact: 'c***@example.test' },
    series: { seriesId, seriesNo: 'SR-TEST', startsOn: '2026-10-13', endsOn: '2026-11-13', dayOfWeek: 'TUESDAY',
      localStartTime: '18:00:00', durationMinutes: 120, occurrenceCount: 5, paymentPlan: 'FULL_SERIES',
      occurrences: dates.map((date, index) => ({ bookingId: index === 0 ? anchor : `occurrence-${index}`, date,
        localStart: '18:00:00', localEnd: '20:00:00', startsAt: `${date}T11:00:00Z`, endsAt: `${date}T13:00:00Z`,
        amount: 400000, amountExact: '400000', status })) },
    slots: [], evidence: [], decisions: [],
    payment: { paymentId: 'p1', status: status === 'CONFIRMED' ? 'PAID' : status === 'NEEDS_REVIEW' ? 'NEEDS_REVIEW' : 'TRANSFER_REPORTED',
      expectedAmount: 2000000, expectedAmountExact: '2000000', firstReportedAt: '2026-10-07T08:05:00Z',
      lastReportedAt: '2026-10-07T08:05:00Z', confirmedAmount: status === 'CONFIRMED' ? 2000000 : null,
      confirmedAmountExact: status === 'CONFIRMED' ? '2000000' : null, confirmedAt: status === 'CONFIRMED' ? '2026-10-07T08:10:00Z' : null,
      bankCode: 'VCB', accountName: 'OWNER', maskedAccountNumber: '****4321', transferContent: 'SR-TEST' } });
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const req = route.request(); const path = new URL(req.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match,Idempotency-Key', 'Access-Control-Allow-Methods': 'GET,POST,OPTIONS' };
    if (req.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    const respond = (data: unknown) => route.fulfill({ status: 200, headers, contentType: 'application/json', body: JSON.stringify({ data }) });
    if (path === '/api/v1/browser-auth/partner/login') return respond({ accessToken: 'partner-test', ...browserSessionFields(),
      user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/partner-onboarding/businesses') return respond([business]);
    if (path === '/api/v1/partner-onboarding/businesses/b1') return respond(business);
    if (path === '/api/v1/me/notifications/') return respond([]);
    if (path === '/api/v1/operator/venues/v1/bookings' && new URL(req.url()).searchParams.has('before') && deniedPageStatus)
      return route.fulfill({ status: deniedPageStatus, headers, contentType: 'application/problem+json', body: JSON.stringify({ code: deniedPageStatus === 403 ? 'FORBIDDEN' : 'NOT_FOUND' }) });
    if (path === '/api/v1/operator/venues/v1/bookings') return respond({ items: [detail()], nextCursor: deniedPageStatus ? anchor : null,
      counts: { awaitingOwnerConfirmation: status === 'AWAITING_OWNER_CONFIRMATION' ? 1 : 0, needsReview: status === 'NEEDS_REVIEW' ? 1 : 0 } });
    if (path === `/api/v1/operator/bookings/${anchor}`) return respond(detail());
    if (req.method() === 'POST' && path.startsWith('/api/v1/operator/bookings/')) {
      const body = req.postDataJSON(); commands.push({ path, body, headers: req.headers() });
      status = path.endsWith('/confirm-payment') ? 'CONFIRMED' : body.resolution === 'FINAL_REJECTION' ? 'PAYMENT_REJECTED' : 'NEEDS_REVIEW';
      version++; return respond(detail());
    }
    return respond({});
  });
  await page.goto(`http://localhost:5174/#/bookings?bookingId=${anchor}`);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('region', { name: 'Lịch cố định toàn kỳ' })).toBeVisible();
  return { commands };
}

test('F07 partner shows one series, all occurrences and confirms the aggregate amount at the anchor', async ({ page }, info) => {
  const api = await setup(page);
  await expect(page).toHaveURL(new RegExp(`#/bookings/${anchor}$`));
  await expect(page.locator('.booking-list')).toHaveCount(0);
  await page.getByRole('button', { name: 'Quay lại danh sách đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(1);
  await page.getByRole('button', { name: 'Xem đơn', exact: true }).click();
  const schedule = page.getByRole('region', { name: 'Lịch cố định toàn kỳ', exact: true });
  await expect(schedule.locator('tbody tr')).toHaveCount(5);
  await expect(schedule).toContainText('Thứ Ba hàng tuần');
  await expect(schedule).toContainText('Thanh toán 100% cả kỳ');
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await expect(form).toContainText('toàn bộ 5 buổi');
  await expect(form.getByLabel('Số tiền thực nhận (đ)', { exact: true })).toHaveValue('2000000');
  await form.getByLabel('Số tiền thực nhận (đ)', { exact: true }).fill('400000');
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('chưa khớp'); expect(api.commands).toHaveLength(0);
  await form.getByLabel('Số tiền thực nhận (đ)', { exact: true }).fill('2000000');
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(schedule.locator('tbody')).toContainText('Đã xác nhận');
  expect(api.commands).toHaveLength(1); expect(api.commands[0].path).toBe(`/api/v1/operator/bookings/${anchor}/confirm-payment`);
  expect(api.commands[0].body).toEqual({ confirmedAmount: 2000000 });
  expect(api.commands[0].headers['if-match']).toBe('"2"');
  expect(api.commands[0].headers['idempotency-key']).toBeTruthy();
  await page.screenshot({ path: info.outputPath('f07-partner-series.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('F07 partner review retains all sessions and final rejection explicitly names the whole period', async ({ page }) => {
  const api = await setup(page);
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await form.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('NEEDS_REVIEW');
  await expect(form).toContainText('Tất cả buổi trong kỳ tiếp tục được giữ');
  await form.getByRole('textbox', { name: 'Nội dung gửi khách', exact: true }).fill('Vui lòng gửi ảnh chuyển khoản của cả kỳ.');
  await form.getByRole('button', { name: 'Gửi yêu cầu bổ sung' }).click();
  await expect(page.getByRole('region', { name: 'Lịch cố định toàn kỳ', exact: true }).locator('tbody tr').first()).toContainText('Cần bổ sung');
  await form.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('FINAL_REJECTION');
  await expect(form.getByRole('checkbox')).toHaveAccessibleName(/giải phóng toàn bộ 5 buổi/);
  await form.getByRole('textbox', { name: 'Nội dung gửi khách', exact: true }).fill('Không tìm thấy giao dịch sau đối chiếu.');
  await form.getByRole('checkbox').check();
  await form.getByRole('button', { name: 'Xác nhận từ chối cuối cùng' }).click();
  await expect(form).toHaveCount(0);
  expect(api.commands.map(call => call.path)).toEqual(Array(2).fill(`/api/v1/operator/bookings/${anchor}/reject-payment`));
});

for (const status of [403, 404]) test('F07 partner clears private list and group detail when pagination is denied ' + status, async ({ page }) => {
  await setup(page, status);
  await page.getByRole('button', { name: 'Quay lại danh sách đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(1);
  await page.getByRole('button', { name: 'Xem thêm đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Lịch cố định toàn kỳ', exact: true })).toHaveCount(0);
  await expect(page.locator('.booking-facts')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân', exact: true })).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Quản lý đơn đặt sân', exact: true }).getByRole('alert')).toBeVisible();
});
