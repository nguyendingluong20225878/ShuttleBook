import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});
import { openPartnerPage } from './helpers/partner-navigation';

const bookingA = '019a1234-1111-7111-8111-111111111111';
const bookingB = '019a1234-2222-7222-8222-222222222222';
const uploadId = '019a1234-3333-7333-8333-333333333333';
async function fixture(page: Page, businessStatus = 'ACTIVE', exactAmount = '320000') {
  const calls: Array<{ path: string; method: string; query: string; headers: Record<string, string>; body: unknown; bodyText: string | null }> = [];
  let noticesFail = false; let read = false; let scopeDenied = false;
  const commandFailures: string[] = [];
  const updates = new Map<string, { status: string; version: number; decisions: object[]; payment: object }>();
  const businesses = [
    { id: 'b1', name: 'Hoàng Cầu Sports', status: businessStatus, version: 1, legalName: 'Test A', contact: 'Contact test',
      venues: [{ id: 'v1', name: 'Hoàng Cầu', status: 'ACTIVE', courts: [], address: 'Hà Nội', contact: 'Test' },
        { id: 'v2', name: 'Đống Đa', status: 'ACTIVE', courts: [], address: 'Hà Nội', contact: 'Test' }] },
    { id: 'b2', name: 'Doanh nghiệp B', status: businessStatus, version: 1, legalName: 'Test B', contact: 'Contact test',
      venues: [{ id: 'v3', name: 'Cơ sở B', status: 'ACTIVE', courts: [], address: 'Hà Nội', contact: 'Test' }] },
  ];
  const booking = (id: string) => {
    const original = { bookingId: id, businessId: id === bookingB ? 'b2' : 'b1', bookingNo: id === bookingB ? 'SB-B' : 'SB-A',
    venueId: id === bookingB ? 'v3' : 'v1', venueName: id === bookingB ? 'Cơ sở B' : 'Hoàng Cầu', courtName: 'Pickleball 1',
    status: 'AWAITING_OWNER_CONFIRMATION', date: '2026-10-10', localStart: '17:00:00', localEnd: '19:00:00',
    timezone: 'Asia/Ho_Chi_Minh', amount: Number(exactAmount), amountExact: exactAmount, version: 2, isOverdue: true,
    customer: { maskedContact: 'o***@example.test' }, createdAt: '2026-10-07T08:00:00Z', paymentDeadline: '2026-10-07T08:20:00Z',
    slots: [], payment: { paymentId: 'p1', status: 'TRANSFER_REPORTED', expectedAmount: Number(exactAmount), expectedAmountExact: exactAmount,
      firstReportedAt: '2026-10-07T08:05:00Z', lastReportedAt: '2026-10-07T08:05:00Z',
      confirmedAmount: null, confirmedAt: null, bankCode: 'VCB', accountName: 'OWNER SNAPSHOT', maskedAccountNumber: '****4321', transferContent: 'SB-A' },
    evidence: [{ evidenceId: 'e1', kind: 'INITIAL', bankReference: 'FT-TEST-001', note: 'Đã chuyển test',
      reportedAt: '2026-10-07T08:05:00Z', proofUrl: `/api/v1/uploads/${uploadId}/view` }], decisions: [],
    };
    const changed = updates.get(id);
    return changed ? { ...original, ...changed, isOverdue: ['AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW'].includes(changed.status),
      payment: { ...original.payment, ...changed.payment } } : original;
  };
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true', 'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match,Idempotency-Key',
      'Access-Control-Allow-Methods': 'GET,POST,OPTIONS' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    calls.push({ path, method: request.method(), query: url.search, headers: request.headers(), body: request.method() === 'GET' ? null : request.postDataJSON(), bodyText: request.postData() });
    const respond = (data: unknown, metadata = {}) => route.fulfill({ status: 200, contentType: 'application/json', headers, body: JSON.stringify({ data, ...metadata }) });
    if (path === '/api/v1/browser-auth/partner/login') return respond({ accessToken: 'partner-test', ...browserSessionFields(), user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/partner-onboarding/businesses') return respond(businesses);
    const business = businesses.find(item => path === `/api/v1/partner-onboarding/businesses/${item.id}`);
    if (business) return respond(business);
    if (path === '/api/v1/me/notifications/') {
      if (noticesFail) return route.fulfill({ status: 503, headers });
      return respond([{ id: 'n1', title: 'Khách báo chuyển khoản', body: 'Đơn SB-B chờ đối chiếu.', readAt: read ? '2026-10-07T08:10:00Z' : null,
        bookingId: bookingB, action: 'OPERATOR_BOOKING' }], { unreadCount: read ? 0 : 8, nextCursor: null });
    }
    if (path === '/api/v1/me/notifications/n1/read') { read = true; return respond({ id: 'n1', readAt: '2026-10-07T08:10:00Z' }); }
    if (scopeDenied && path.startsWith('/api/v1/operator/')) return route.fulfill({ status: 403,
      contentType: 'application/problem+json', headers, body: JSON.stringify({ code: 'FORBIDDEN' }) });
    if (path === `/api/v1/operator/bookings/${bookingA}/confirm-payment` || path === `/api/v1/operator/bookings/${bookingA}/reject-payment`) {
      const failure = commandFailures.shift();
      if (failure === 'NETWORK') return route.abort('failed');
      if (failure) {
        if (failure === 'PRECONDITION_FAILED') updates.set(bookingA, { status: 'AWAITING_OWNER_CONFIRMATION', version: 3, decisions: [], payment: {} });
        if (failure === 'FORBIDDEN') scopeDenied = true;
        return route.fulfill({ status: failure === 'PRECONDITION_FAILED' ? 412 : failure === 'FORBIDDEN' ? 403 : 409,
          contentType: 'application/problem+json', headers, body: JSON.stringify({ code: failure }) });
      }
      const input = request.postDataJSON(); const current = booking(bookingA);
      const confirmedAmountExact = request.postData()?.match(/"confirmedAmount":([0-9]+)/)?.[1] ?? null;
      const resolution = path.endsWith('/confirm-payment') ? 'CONFIRMED' : input.resolution;
      updates.set(bookingA, { status: resolution === 'FINAL_REJECTION' ? 'PAYMENT_REJECTED' : resolution, version: current.version + 1,
        decisions: [...current.decisions, { decisionId: `d${current.version}`, resolution, decidedAt: '2026-10-07T09:00:00Z',
          confirmedAmount: input.confirmedAmount ?? null, confirmedAmountExact, bankReference: input.bankReference ?? null, reason: input.reason ?? null,
          reasonCode: input.reasonCode ?? null, note: input.note ?? null }],
        payment: resolution === 'CONFIRMED' ? { status: 'PAID', confirmedAmount: input.confirmedAmount, confirmedAmountExact, confirmedAt: '2026-10-07T09:00:00Z' }
          : { status: resolution === 'FINAL_REJECTION' ? 'REJECTED' : 'NEEDS_REVIEW' } });
      return respond(booking(bookingA));
    }
    if (path === `/api/v1/operator/bookings/${bookingA}`) return respond(booking(bookingA));
    if (path === `/api/v1/operator/bookings/${bookingB}`) return respond(booking(bookingB));
    if (/\/operator\/venues\/v[123]\/bookings$/.test(path)) return respond({ items: path.includes('/v2/') ? [] : [booking(path.includes('/v3/') ? bookingB : bookingA)],
      nextCursor: null, counts: { awaitingOwnerConfirmation: 1, needsReview: 2 } });
    if (path === `/api/v1/uploads/${uploadId}/view`) return route.fulfill({ status: 200, headers, contentType: 'image/png',
      body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l5sAAAAASUVORK5CYII=', 'base64') });
    return respond({});
  });
  return { calls, failNotices: (value: boolean) => { noticesFail = value; }, failCommands: (...codes: string[]) => { commandFailures.push(...codes); } };
}
async function login(page: Page, hash = '#/bookings') {
  await page.goto(`http://localhost:5174/${hash}`);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Đơn đặt sân', exact: true })).toBeVisible();
}

test('F06 partner list filters, scoped detail, history and bearer private proof', async ({ page }, info) => {
  const api = await fixture(page); await login(page);
  await expect(page.locator('.booking-counts')).toContainText('Chờ xác nhận');
  await page.getByRole('combobox', { name: 'Trạng thái đơn', exact: true }).selectOption('NEEDS_REVIEW');
  await page.getByLabel('Từ ngày', { exact: true }).fill('2026-10-01'); await page.getByLabel('Đến ngày', { exact: true }).fill('2026-10-31');
  await page.getByRole('button', { name: 'Lọc đơn', exact: true }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/v1/bookings')).at(-1)?.query).toContain('status=NEEDS_REVIEW');
  const row = page.locator('.booking-list > li').filter({ has: page.getByText('SB-A', { exact: true }) });
  await expect(row.getByRole('button', { name: 'Xem đơn', exact: true })).toBeVisible();
  await row.getByRole('button', { name: 'Xem đơn', exact: true }).click();
  const detail = page.getByRole('region', { name: 'Chi tiết đơn đặt sân' });
  await expect(page).toHaveURL(new RegExp(`#/bookings/${bookingA}$`));
  await expect(page.locator('.booking-list')).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'Trạng thái đơn', exact: true })).toHaveCount(0);
  await expect(detail.getByRole('heading', { name: 'Chi tiết đơn đặt sân', exact: true })).toBeFocused();
  await expect(detail).toContainText('FT-TEST-001'); await expect(detail).toContainText('OWNER SNAPSHOT');
  await expect(detail).toContainText('****4321'); await expect(detail).toContainText('o***@example.test');
  await detail.getByRole('button', { name: 'Xem biên lai', exact: true }).click();
  await expect(detail.getByAltText('Biên lai chuyển khoản khách cung cấp')).toBeVisible();
  expect(api.calls.find(call => call.path.endsWith(`/${uploadId}/view`))?.headers.authorization).toBe('Bearer partner-test');
  await page.screenshot({ path: info.outputPath('f06-partner-detail.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(api.calls.filter(call => call.method === 'POST' && call.path.includes('/operator/'))).toHaveLength(0);
  await page.goBack();
  await expect(page).toHaveURL(/#\/bookings$/);
  await expect(detail).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'Trạng thái đơn', exact: true })).toHaveValue('NEEDS_REVIEW');
  await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveValue('2026-10-01');
  await page.goForward();
  await expect(detail).toContainText('SB-A');
  await expect(page.locator('.booking-list')).toHaveCount(0);
  await detail.getByRole('button', { name: 'Quay lại danh sách đơn', exact: true }).click();
  await expect(page).toHaveURL(/#\/bookings$/);
  await expect(page.getByLabel('Đến ngày', { exact: true })).toHaveValue('2026-10-31');
  await row.getByRole('button', { name: 'Xem đơn', exact: true }).click();
  await page.getByRole('combobox', { name: 'Doanh nghiệp', exact: true }).selectOption('b2');
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp B'); await expect(detail).toHaveCount(0);
  await expect(page.locator('.booking-list')).toContainText('SB-B'); await expect(page.locator('.booking-list')).not.toContainText('SB-A');
});

test('F06 partner notification retry, total unread badge and deep link follows business after login/F5', async ({ page }) => {
  const api = await fixture(page); api.failNotices(true); await login(page);
  await openPartnerPage(page, 'Thông báo'); await expect(page.getByText('Không tải được thông báo. Vui lòng thử lại.')).toBeVisible();
  api.failNotices(false); await page.getByRole('button', { name: 'Tải lại thông báo' }).click();
  await expect(page.locator('.notice-count')).toHaveText('8');
  await page.getByRole('button', { name: 'Xem đơn đặt sân', exact: true }).click();
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp B');
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('SB-B');
  await expect(page).toHaveURL(new RegExp(`#/bookings/${bookingB}$`));
  await page.reload();
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp B');
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('SB-B');
  await expect(page.locator('.booking-list')).toHaveCount(0);
  expect(api.calls.filter(call => call.method === 'POST' && call.path.includes('/operator/'))).toHaveLength(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('Partner legacy booking deep link becomes a child route and F5 restores only the detail page', async ({ page }) => {
  const api = await fixture(page); await login(page, `#/bookings?bookingId=${bookingA}`);
  await expect(page).toHaveURL(new RegExp(`#/bookings/${bookingA}$`));
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('SB-A');
  await expect(page.locator('.booking-list')).toHaveCount(0);
  await page.reload();
  await expect(page).toHaveURL(new RegExp(`#/bookings/${bookingA}$`));
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('SB-A');
  await expect(page.locator('.booking-filters')).toHaveCount(0);
  await expect(page.locator('.booking-list')).toHaveCount(0);
  expect(api.calls.filter(call => call.method === 'POST' && call.path.includes('/operator/'))).toHaveLength(0);
});

test('F06 pending partner cannot operate bookings and no scoped booking requests are made', async ({ page }) => {
  const api = await fixture(page, 'PENDING_APPROVAL'); await login(page, `#/bookings?bookingId=${bookingA}`);
  await expect(page.getByRole('heading', { name: 'Chưa thể xử lý đơn đặt sân' })).toBeVisible();
  expect(api.calls.filter(call => call.path.startsWith('/api/v1/operator/'))).toHaveLength(0);
});

test('F06 partner venue switch discards late list results and validates date range', async ({ page }) => {
  await fixture(page);
  let release!: () => void;
  const barrier = new Promise<void>(resolve => { release = resolve; });
  await page.route('http://localhost:5080/api/v1/operator/venues/v1/bookings?**', async route => {
    if (await browserSessionRoute(route, page)) return;
    if (route.request().method() === 'OPTIONS') return route.fallback();
    await barrier;
    await route.fulfill({ status: 200, contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': 'http://localhost:5174' },
      body: JSON.stringify({ data: { items: [{ bookingId: bookingA, bookingNo: 'STALE-A' }], nextCursor: null,
        counts: { awaitingOwnerConfirmation: 9, needsReview: 9 } } }) }).catch(() => {});
  });
  await login(page);
  await page.getByRole('combobox', { name: 'Cơ sở xem đơn', exact: true }).selectOption('v2');
  await expect(page.getByRole('heading', { name: 'Chưa có đơn phù hợp' })).toBeVisible();
  release(); await expect(page.locator('.booking-list')).not.toContainText('STALE-A');
  await page.getByLabel('Từ ngày', { exact: true }).fill('2026-10-20'); await page.getByLabel('Đến ngày', { exact: true }).fill('2026-10-01');
  await page.getByRole('button', { name: 'Lọc đơn', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Ngày kết thúc phải từ ngày bắt đầu trở đi.');
});

test('F06 owner confirm retries same intent and ends with confirmed detail', async ({ page }) => {
  const api = await fixture(page); api.failCommands('NETWORK'); await login(page, `#/bookings?bookingId=${bookingA}`);
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await expect(form).toBeVisible();
  await expect(form.getByLabel('Mã giao dịch đối chiếu')).toHaveCount(0);
  await form.getByLabel('Ghi chú đối chiếu').fill('Đã kiểm tra ngân hàng test.');
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('giữ nguyên mã yêu cầu');
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('Đã xác nhận thanh toán lúc');
  await expect(form).toHaveCount(0);
  const commands = api.calls.filter(item => item.path.endsWith('/confirm-payment'));
  expect(commands).toHaveLength(2);
  expect(commands[0].headers['idempotency-key']).toBe(commands[1].headers['idempotency-key']);
  expect(commands[0].headers['if-match']).toBe('"2"'); expect(commands[1].headers['if-match']).toBe('"2"');
  expect(commands[0].body).toEqual(commands[1].body);
  expect(commands[0].body).toEqual({ confirmedAmount: 320000, note: 'Đã kiểm tra ngân hàng test.' });
});

test('F06 owner amount mismatch leads to review; final rejection needs explicit acknowledgement', async ({ page }, info) => {
  const api = await fixture(page); await login(page, `#/bookings?bookingId=${bookingA}`);
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await form.getByLabel('Số tiền thực nhận (đ)').fill('319000'); await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('chưa khớp');
  expect(api.calls.filter(item => item.path.endsWith('/confirm-payment'))).toHaveLength(0);
  await form.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('NEEDS_REVIEW');
  await expect(form.getByRole('option', { name: 'Mã giao dịch không khớp', exact: true })).toHaveCount(0);
  await form.getByRole('combobox', { name: 'Lý do đối chiếu', exact: true }).selectOption('AMOUNT_MISMATCH');
  await form.getByLabel('Nội dung gửi khách').fill('Thiếu 1.000đ. Vui lòng cung cấp giao dịch để đối chiếu.');
  await form.getByRole('button', { name: 'Gửi yêu cầu bổ sung' }).click();
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('Chủ sân yêu cầu bổ sung');
  await form.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('FINAL_REJECTION');
  await form.getByRole('combobox', { name: 'Lý do đối chiếu', exact: true }).selectOption('TRANSACTION_NOT_FOUND');
  await form.getByLabel('Nội dung gửi khách').fill('Không tìm thấy giao dịch sau khi đối chiếu.');
  await form.getByRole('button', { name: 'Xác nhận từ chối cuối cùng' }).click();
  await expect(form.getByRole('alert')).toContainText('Xác nhận từ chối cuối cùng và giải phóng sân');
  expect(api.calls.filter(item => item.path.endsWith('/reject-payment'))).toHaveLength(1);
  await form.getByRole('checkbox').check(); await page.screenshot({ path: info.outputPath('f06-partner-reject.png'), fullPage: true });
  await form.getByRole('button', { name: 'Xác nhận từ chối cuối cùng' }).click();
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('Không xác nhận giao dịch');
  await expect(form).toHaveCount(0);
  const commands = api.calls.filter(item => item.path.endsWith('/reject-payment'));
  expect(commands).toHaveLength(2); expect(commands[0].headers['if-match']).toBe('"2"'); expect(commands[1].headers['if-match']).toBe('"3"');
  expect(commands[1].body).toEqual({ resolution: 'FINAL_REJECTION', reasonCode: 'TRANSACTION_NOT_FOUND', reason: 'Không tìm thấy giao dịch sau khi đối chiếu.' });
});

test('F06 stale decision blocks resend until explicit reload and server mismatch does not show success', async ({ page }) => {
  const api = await fixture(page); api.failCommands('PRECONDITION_FAILED', 'PAYMENT_AMOUNT_MISMATCH');
  await login(page, `#/bookings?bookingId=${bookingA}`);
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('Đơn vừa được cập nhật');
  await expect(form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' })).toBeDisabled();
  expect(api.calls.filter(item => item.path.endsWith('/confirm-payment'))).toHaveLength(1);
  await form.getByRole('button', { name: 'Tải lại chi tiết', exact: true }).click();
  await expect(form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' })).toBeEnabled();
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('Số tiền chưa khớp');
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).not.toContainText('Đã xác nhận thanh toán lúc');
  const commands = api.calls.filter(item => item.path.endsWith('/confirm-payment'));
  expect(commands).toHaveLength(2); expect(commands[1].headers['if-match']).toBe('"3"');
  expect(commands[1].headers['idempotency-key']).not.toBe(commands[0].headers['idempotency-key']);
});

test('F06 owner preserves amounts beyond safe integer in display and numeric JSON retry', async ({ page }) => {
  const exactAmount = '9007199254740993';
  const api = await fixture(page, 'ACTIVE', exactAmount); api.failCommands('NETWORK');
  await login(page, `#/bookings?bookingId=${bookingA}`);
  const form = page.getByRole('form', { name: 'Quyết định thanh toán' });
  await expect(form.getByLabel('Số tiền thực nhận (đ)')).toHaveValue(exactAmount);
  await expect(form).toContainText('9.007.199.254.740.993đ');
  await page.getByRole('button', { name: 'Quay lại danh sách đơn', exact: true }).click();
  await expect(page.locator('.booking-list')).toContainText('9.007.199.254.740.993đ');
  await page.getByRole('button', { name: 'Xem đơn', exact: true }).click();
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(form.getByRole('alert')).toContainText('giữ nguyên mã yêu cầu');
  await form.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(page.getByRole('region', { name: 'Chi tiết đơn đặt sân' })).toContainText('Đã xác nhận thanh toán lúc');
  await expect(page.locator('.booking-facts')).toContainText('9.007.199.254.740.993đ');
  const commands = api.calls.filter(item => item.path.endsWith('/confirm-payment'));
  expect(commands).toHaveLength(2); expect(commands[0].bodyText).toBe(commands[1].bodyText);
  expect(commands[0].bodyText).toBe(`{"confirmedAmount":${exactAmount}}`);
  expect(commands[0].bodyText).not.toContain(`"confirmedAmount":"${exactAmount}"`);
});

test('F06 revoked owner loses cached private detail and proof immediately after command denial', async ({ page }) => {
  const api = await fixture(page); api.failCommands('FORBIDDEN');
  await login(page, `#/bookings?bookingId=${bookingA}`);
  const detail = page.getByRole('region', { name: 'Chi tiết đơn đặt sân' });
  await detail.getByRole('button', { name: 'Xem biên lai', exact: true }).click();
  await expect(detail.getByAltText('Biên lai chuyển khoản khách cung cấp')).toBeVisible();
  await detail.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  await expect(detail.getByRole('alert')).toContainText('Bạn không còn quyền xử lý đơn này.');
  await expect(detail.getByAltText('Biên lai chuyển khoản khách cung cấp')).toHaveCount(0);
  await expect(detail).not.toContainText('FT-TEST-001');
  await expect(page.getByRole('form', { name: 'Quyết định thanh toán' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Quay lại danh sách đơn', exact: true }).click();
  await expect(page.locator('.booking-list > li')).toHaveCount(0);
  expect(api.calls.filter(item => item.path.endsWith('/confirm-payment'))).toHaveLength(1);
});
