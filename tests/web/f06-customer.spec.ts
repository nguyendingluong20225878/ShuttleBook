import { expect, test, type Page } from '@playwright/test';
import { createHash } from 'node:crypto';

const bookingId = '0199f060-0000-7000-8000-000000000001';
const uploadId = '0199f060-0000-7000-8000-000000000002';
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');
const booking = (status = 'AWAITING_OWNER_CONFIRMATION') => ({
  bookingId, bookingNo: 'BK-F06-DEMO', status, version: 2, venueName: 'Hoàng Cầu', courtName: 'Sân 1', date: '2026-10-20',
  localStart: '18:00', localEnd: '19:00', timezone: 'Asia/Ho_Chi_Minh', amount: 250000,
  slots: [{ startsAt: '18:00', endsAt: '18:30', pricePerSlot: 100000 }, { startsAt: '18:30', endsAt: '19:00', pricePerSlot: 150000 }],
  paymentDeadline: '2020-01-01T00:00:00Z', createdAt: '2026-10-07T01:00:00Z', expiredAt: null,
  isOverdue: false, confirmationDueAt: '2026-10-07T01:35:00Z',
  payment: { status: 'TRANSFER_REPORTED', bankCode: 'TEST', accountName: 'TEST CLUB', maskedAccountNumber: '******7890',
    transferContent: 'BK-F06-DEMO', qrUrl: null, expectedAmount: 250000, firstReportedAt: '2026-10-07T01:05:00Z', confirmedAmount: null, confirmedAt: null },
  evidence: [{ evidenceId: uploadId, kind: 'INITIAL', bankReference: 'FT-F06-TEST' as string | null, note: 'Đã chuyển từ ứng dụng ngân hàng' as string | null,
    reportedAt: '2026-10-07T01:05:00Z', proofUrl: `/api/v1/uploads/${uploadId}/view` }],
  decisions: [] as object[],
});
async function login(page: Page, returnTo: string) {
  await page.goto(`http://localhost:5173/login?returnTo=${encodeURIComponent(returnTo)}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
}
const loginResponse = { tokenType: 'Bearer', accessToken: 'access-f06-test', refreshToken: 'refresh-f06-test', expiresInSeconds: 600,
  user: { accountType: 'CUSTOMER', status: 'ACTIVE' } };

test('reported booking survives old deadline, private proof/history and focus poll render confirmation', async ({ page }) => {
  let current = booking(); const mutations: string[] = [];
  await page.route('**/api/v1/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (request.method() === 'POST') mutations.push(path);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0, nextCursor: null }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path === `/api/v1/uploads/${uploadId}/view`) {
      expect(request.headers().authorization).toBe('Bearer access-f06-test');
      return route.fulfill({ contentType: 'image/png', body: png });
    }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible();
  await expect(page.getByText('Sân vẫn được giữ trong lúc chủ sân đối chiếu giao dịch.')).toBeVisible();
  await expect(page.getByText('Thời gian giữ chỗ còn:', { exact: false })).toHaveCount(0);
  await expect(page.getByRole('img', { name: 'Biên lai chuyển khoản đã gửi' })).toBeVisible();
  await expect(page.getByText('FT-F06-TEST', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Hủy|Đổi lịch|Đã chuyển khoản/ })).toHaveCount(0);
  current = { ...current, status: 'CONFIRMED', version: 3, payment: { ...current.payment, status: 'PAID' } };
  current.decisions = [{ decisionId: bookingId, resolution: 'CONFIRMED', reasonCode: null, reason: null, confirmedAmount: 250000,
    bankReference: 'FT-F06-TEST', note: 'Đã đối chiếu', decidedAt: '2026-10-07T01:12:00Z' }];
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  await expect(page.getByText('Đã xác nhận', { exact: true })).toBeVisible();
  await expect(page.getByText('Chủ sân đã xác nhận nhận tiền')).toBeVisible();
  expect(mutations).toEqual([]);
  await page.screenshot({ path: test.info().outputPath('f06-customer-confirmed.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('My Bookings uses all six statuses and requires login to recover the same booking after F5', async ({ page }) => {
  const statuses = ['AWAITING_TRANSFER', 'AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW', 'CONFIRMED', 'EXPIRED', 'PAYMENT_REJECTED'];
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path.endsWith('/me/bookings')) return reply({ items: statuses.map((status, index) => ({ ...booking(status), bookingId: `${bookingId.slice(0, -1)}${index}`, bookingNo: `BK-F06-${index}` })), nextCursor: null });
    if (path.startsWith('/api/v1/bookings/')) return reply(booking('NEEDS_REVIEW'));
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    return route.abort();
  });
  await login(page, '/me/bookings');
  for (const label of ['Đang chờ chuyển khoản', 'Chờ xác nhận', 'Cần bổ sung bằng chứng', 'Đã xác nhận', 'Đã hết hạn', 'Không xác nhận giao dịch'])
    await expect(page.getByText(label, { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'BK-F06-1', exact: true }).click();
  await expect(page.getByText('Cần bổ sung bằng chứng', { exact: true })).toBeVisible(); await page.reload();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-F06-DEMO' })).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('notifications retain unread/read, allow only customer booking links and recover page after F5', async ({ page }) => {
  let readAt: string | null = null;
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({
      data: [{ id: bookingId, title: 'Đã xác nhận đơn', body: 'Chủ sân đã nhận tiền', readAt, bookingId, action: 'CUSTOMER_BOOKING' },
        { id: uploadId, title: 'Thông báo khác', body: 'Không tạo link ngoài hệ thống', readAt: '2026-10-07T01:00:00Z', bookingId: 'https://example.test', action: 'OPERATOR_BOOKING' }],
      unreadCount: readAt ? 0 : 1, nextCursor: null }) });
    if (path.endsWith(`/notifications/${bookingId}/read`)) { readAt = '2026-10-07T01:12:00Z'; return reply({ id: bookingId, readAt }); }
    return route.abort();
  });
  await login(page, '/me/notifications');
  await expect(page.getByRole('heading', { name: 'Thông báo', exact: true })).toBeVisible();
  await expect(page.getByLabel('1 thông báo chưa đọc')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Xem đơn đặt sân', exact: true })).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'Xem đơn đặt sân', exact: true })).toHaveAttribute('href', `/bookings/${bookingId}`);
  await page.getByRole('button', { name: 'Đánh dấu đã đọc' }).click();
  await expect(page.getByText('0 thông báo chưa đọc', { exact: true })).toBeVisible();
  await page.reload(); await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Thông báo', exact: true })).toBeVisible();
});

function awaitingTransfer() {
  const current = booking('AWAITING_TRANSFER');
  return { ...current, version: 1, evidence: [], paymentDeadline: new Date(Date.now() + 20 * 60_000).toISOString(),
    payment: { ...current.payment, status: 'AWAITING_TRANSFER', firstReportedAt: null, qrUrl: `/api/v1/bookings/${bookingId}/qr` } };
}

test('screenshot-only report validates private proof, uses three phases once and retries one payment intent after network failure', async ({ page }) => {
  let current: ReturnType<typeof booking> = awaitingTransfer(); const phases: string[] = [];
  const intents: { key: string; version: string; body: string }[] = [];
  await page.route('**/api/v1/**', route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path.endsWith('/qr') || path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/proof-uploads/presign')) {
      phases.push('presign'); expect(request.postDataJSON()).toEqual({ contentType: 'image/png', sizeBytes: png.length, sha256Base64: createHash('sha256').update(png).digest('base64') });
      return reply({ id: uploadId, uploadUrl: `http://localhost:5080/api/v1/uploads/${uploadId}/content?expires=1&sig=synthetic`,
        uploadHeaders: { 'Content-Type': 'image/png' }, expiresAt: new Date(Date.now() + 5 * 60_000).toISOString() });
    }
    if (path.endsWith('/content')) { phases.push('put'); expect(request.method()).toBe('PUT'); expect(request.headers().authorization).toBeUndefined(); return route.fulfill({ status: 204 }); }
    if (path.endsWith('/complete')) { phases.push('complete'); expect(request.headers().authorization).toBe('Bearer access-f06-test'); return reply({ id: uploadId, status: 'READY' }); }
    if (path.endsWith('/transfer-evidence')) {
      phases.push('report'); intents.push({ key: request.headers()['idempotency-key'], version: request.headers()['if-match'], body: request.postData()! });
      expect(request.postDataJSON()).toEqual({ proofUploadId: uploadId });
      if (intents.length === 1) return route.abort();
      current = { ...booking(), version: 2, paymentDeadline: current.paymentDeadline, evidence: [{ ...booking().evidence[0], bankReference: null, note: null }] };
      return reply(current);
    }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeEnabled();
  await expect(page.getByLabel('Mã giao dịch ngân hàng', { exact: true })).toHaveCount(0);
  const file = page.getByLabel('Ảnh chụp màn hình chuyển khoản (không bắt buộc)', { exact: false });
  await file.setInputFiles({ name: 'invalid.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg/>') });
  await expect(page.getByRole('alert')).toContainText('PNG, JPEG hoặc WebP');
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeDisabled(); expect(phases).toEqual([]);
  await page.getByRole('button', { name: 'Bỏ ảnh biên lai' }).click();
  await file.setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: png });
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  await expect(page.getByText('Đã chọn: receipt.png', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Không thể kết nối');
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible();
  expect(phases).toEqual(['presign', 'put', 'complete', 'report', 'report']); expect(intents).toHaveLength(2);
  expect(intents[0]).toEqual(intents[1]); expect(intents[0].version).toBe('"1"');
  await expect(page.getByRole('img', { name: 'Biên lai chuyển khoản đã gửi' })).toBeVisible();
  await expect(page.getByText('Mã giao dịch:', { exact: false })).toHaveCount(0);
  await expect(page.getByText('Thời gian giữ chỗ còn:', { exact: false })).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('supplement is allowed after original deadline, keeps old evidence and does not upload an optional image', async ({ page }) => {
  let current = booking('NEEDS_REVIEW'); current.version = 3;
  current.decisions = [{ decisionId: bookingId, resolution: 'NEEDS_REVIEW', reasonCode: 'EVIDENCE_REQUIRED', reason: 'Bổ sung thông tin chuyển khoản để đối chiếu',
    confirmedAmount: null, bankReference: null, note: null, decidedAt: '2026-10-07T01:15:00Z' }];
  let reports = 0; let uploads = 0;
  await page.route('**/api/v1/**', route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/proof-uploads/presign')) { uploads++; return route.abort(); }
    if (path.endsWith('/transfer-evidence')) {
      reports++; expect(request.headers()['if-match']).toBe('"3"'); expect(request.postDataJSON()).toEqual({ note: 'Đã kiểm tra lại giao dịch' });
      current = { ...current, status: 'AWAITING_OWNER_CONFIRMATION', version: 4, evidence: [...current.evidence,
        { ...current.evidence[0], evidenceId: bookingId, kind: 'SUPPLEMENT', bankReference: null, note: 'Đã kiểm tra lại giao dịch', proofUrl: null, reportedAt: '2026-10-07T02:00:00Z' }] };
      return reply(current);
    }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await expect(page.getByText('Lý do: Bổ sung thông tin chuyển khoản để đối chiếu')).toBeVisible();
  await page.getByRole('button', { name: 'Bổ sung bằng chứng', exact: true }).click();
  await page.getByLabel('Ghi chú (không bắt buộc)', { exact: true }).fill('  Đã kiểm tra lại giao dịch  ');
  await page.getByRole('button', { name: 'Gửi bổ sung bằng chứng', exact: true }).click();
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible();
  await expect(page.getByText('FT-F06-TEST', { exact: true })).toBeVisible(); await expect(page.getByText('Ghi chú: Đã kiểm tra lại giao dịch', { exact: true })).toBeVisible();
  await expect(page.getByText('Thời gian giữ chỗ còn:', { exact: false })).toHaveCount(0); expect(reports).toBe(1); expect(uploads).toBe(0);
});

test('412 preserves draft, requires explicit reload and creates a new intent only after customer submits again', async ({ page }) => {
  let current: ReturnType<typeof booking> = awaitingTransfer(); const attempts: { key: string; version: string }[] = [];
  await page.route('**/api/v1/**', route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/transfer-evidence')) {
      attempts.push({ key: request.headers()['idempotency-key'], version: request.headers()['if-match'] });
      if (attempts.length === 1) { current = { ...current, version: 2 }; return route.fulfill({ status: 412, contentType: 'application/problem+json', body: JSON.stringify({ code: 'PRECONDITION_FAILED' }) }); }
      current = { ...booking(), version: 3 }; return reply(current);
    }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`); await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await page.getByLabel('Ghi chú (không bắt buộc)', { exact: true }).fill('Draft giữ nguyên khi version thay đổi');
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Đơn đã thay đổi');
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Tải lại để kiểm tra đơn' }).click();
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeEnabled();
  await expect(page.getByLabel('Ghi chú (không bắt buộc)', { exact: true })).toHaveValue('Draft giữ nguyên khi version thay đổi'); expect(attempts).toHaveLength(1);
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible(); expect(attempts).toHaveLength(2);
  expect(attempts[0].key).not.toBe(attempts[1].key); expect(attempts.map(item => item.version)).toEqual(['"1"', '"2"']);
});

test('past transfer deadline disables reporting and server 409 requires reload without automatic POST', async ({ page }) => {
  let current: ReturnType<typeof booking> = { ...awaitingTransfer(), paymentDeadline: '2020-01-01T00:00:00Z' }; let reports = 0;
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/transfer-evidence')) { reports++; current = { ...current, status: 'EXPIRED', version: 2, expiredAt: '2026-10-07T01:20:00Z' };
      return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: 'PAYMENT_DEADLINE_EXPIRED' }) }); }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`); await expect(page.getByRole('button', { name: 'Đã chuyển khoản', exact: true })).toBeDisabled(); expect(reports).toBe(0);
  current = awaitingTransfer(); await page.getByRole('button', { name: 'Làm mới đơn', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đã chuyển khoản', exact: true })).toBeEnabled(); await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Đã hết hạn báo chuyển khoản');
  await page.getByRole('button', { name: 'Tải lại để kiểm tra đơn' }).click();
  await expect(page.getByText('Đã hết hạn', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Đã chuyển khoản|Gửi báo chuyển khoản/ })).toHaveCount(0); expect(reports).toBe(1);
});

test('a stale detail GET cannot restore the transfer form after a successful report', async ({ page }) => {
  const original = awaitingTransfer(); let reported = false; let staleReads = 0;
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) { if (reported) staleReads++; return reply(original); }
    if (path.endsWith('/qr')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/transfer-evidence')) { expect(route.request().postDataJSON()).toEqual({}); reported = true; return reply({ ...booking(), version: 2 }); }
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await expect(page.getByLabel('Mã giao dịch ngân hàng', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect.poll(() => staleReads).toBeGreaterThan(0);
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Đã chuyển khoản|Gửi báo chuyển khoản/ })).toHaveCount(0);
  await expect(page.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toHaveCount(0);
  await expect(page.getByText('FT-F06-TEST', { exact: true })).toBeVisible();
});

test('401, 403 and 404 revalidation clear cached private booking details and proof', async ({ page }) => {
  let rejectedStatus = 0;
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login') || path.endsWith('/auth/refresh')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return rejectedStatus
      ? route.fulfill({ status: rejectedStatus, contentType: 'application/problem+json', body: JSON.stringify({ code: ({ 401: 'UNAUTHORIZED', 403: 'FORBIDDEN', 404: 'NOT_FOUND' } as Record<number, string>)[rejectedStatus] }) })
      : reply(booking());
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  for (const status of [403, 404, 401]) {
    await expect(page.getByRole('img', { name: 'Biên lai chuyển khoản đã gửi' })).toBeVisible();
    rejectedStatus = status; await page.getByRole('button', { name: 'Làm mới đơn', exact: true }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await expect(page.getByText('FT-F06-TEST', { exact: true })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Mã đơn: BK-F06-DEMO' })).toHaveCount(0);
    await expect(page.getByRole('img', { name: 'Biên lai chuyển khoản đã gửi' })).toHaveCount(0);
    if (status !== 401) { rejectedStatus = 0; await page.getByRole('button', { name: 'Làm mới đơn', exact: true }).click(); }
  }
});

test('payment detail preserves exact 18-digit amounts and overdue text does not claim an alert was delivered', async ({ page }) => {
  const exact = '999999999999999999'; const formatted = '999.999.999.999.999.999đ';
  let current = { ...booking('CONFIRMED'), amount: Number(exact), amountExact: exact,
    payment: { ...booking().payment, status: 'PAID', expectedAmount: Number(exact), expectedAmountExact: exact,
      confirmedAmount: Number(exact), confirmedAmountExact: exact },
    decisions: [{ decisionId: bookingId, resolution: 'CONFIRMED', reason: null, reasonCode: null, bankReference: 'FT-EXACT',
      note: null, confirmedAmount: Number(exact), confirmedAmountExact: exact, decidedAt: '2026-10-07T01:12:00Z' }] };
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(current);
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await expect(page.getByText(`Số tiền: ${formatted}`, { exact: true })).toBeVisible();
  await expect(page.getByText(`Số tiền đã nhận: ${formatted}`, { exact: true })).toBeVisible();
  await expect(page.getByText(`Số tiền đã xác nhận: ${formatted}`, { exact: true })).toBeVisible();
  current = { ...current, status: 'AWAITING_OWNER_CONFIRMATION', version: 4, isOverdue: true };
  await page.getByRole('button', { name: 'Làm mới đơn', exact: true }).click();
  await expect(page.getByText('Đơn đang chờ đối chiếu quá 30 phút.', { exact: false })).toBeVisible();
  await expect(page.getByText('Chủ sân đã được nhắc', { exact: false })).toHaveCount(0);
});

test('lost report response can replay the same intent after its old deadline while edited data remains blocked', async ({ page }) => {
  await page.clock.install();
  const original = { ...awaitingTransfer(), paymentDeadline: new Date(Date.now() + 60_000).toISOString() };
  const intents: { key: string; version: string; body: string }[] = [];
  await page.route('**/api/v1/**', route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: value }) });
    if (path.endsWith('/auth/login')) return reply(loginResponse);
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [], unreadCount: 0 }) });
    if (path === `/api/v1/bookings/${bookingId}`) return reply(original);
    if (path.endsWith('/qr') || path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.endsWith('/transfer-evidence')) {
      intents.push({ key: request.headers()['idempotency-key'], version: request.headers()['if-match'], body: request.postData()! });
      return intents.length === 1 ? route.abort() : reply(booking());
    }
    return route.abort();
  });
  await login(page, `/bookings/${bookingId}`);
  await page.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Không thể kết nối');
  await page.clock.fastForward(61_000);
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeEnabled();
  await page.getByLabel('Ghi chú (không bắt buộc)', { exact: true }).fill('Nội dung mới sau deadline');
  await expect(page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true })).toBeDisabled();
  await page.getByLabel('Ghi chú (không bắt buộc)', { exact: true }).fill('');
  await page.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  await expect(page.getByText('Chờ xác nhận', { exact: true })).toBeVisible();
  expect(intents).toHaveLength(2); expect(intents[0]).toEqual(intents[1]); expect(intents[0].body).toBe('{}');
});
