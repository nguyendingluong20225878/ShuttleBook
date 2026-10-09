import { expect, Page, test } from '@playwright/test';

const row = (id: string, businessName: string, kind = 'ONBOARDING') => ({ id, businessId: `business-${id}`, businessName, kind, status: 'PENDING', submittedAt: '2026-10-09T01:00:00Z' });
const profile = { name: 'Hồ sơ kiểm thử', legalName: 'Doanh nghiệp kiểm thử', contact: 'owner@example.test', venues: [] };
const current = { venueName: 'Cơ sở Hoàng Cầu', version: 4, address: 'Địa chỉ đang công bố', contact: 'contact-old', timezone: 'Asia/Ho_Chi_Minh', latitude: 21, longitude: 105,
  bankCode: 'OLD_BANK', accountName: 'OLD OWNER', accountNumber: '1111', qrUploadId: 'old-qr' };
const proposed = { ...current, address: 'Địa chỉ đề nghị', bankCode: 'NEW_BANK', accountName: 'NEW OWNER', accountNumber: '2222', qrUploadId: 'new-qr' };
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=', 'base64');

type ApiHandler = (path: string, url: URL, body: unknown) => Promise<{ status?: number; data?: unknown; payload?: unknown }>;
async function mock(page: Page, handler: ApiHandler) {
  let active = false;
  await page.route('**/api/v1/**', async route => {
    const req = route.request(); const url = new URL(req.url()); const path = url.pathname;
    const reply = (value: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(value) });
    const session = { data: { tokenType: 'Bearer', accessToken: 'admin-improvements-token', refreshToken: 'admin-improvements-refresh', expiresInSeconds: 3600, user: { accountType: 'ADMIN', status: 'ACTIVE' } } };
    if (path === '/api/v1/admin-auth/restore') return active ? reply(session) : reply({}, 401);
    if (path === '/api/v1/admin-auth/login') { active = true; return reply(session); }
    if (path === '/api/v1/admin-auth/me') return reply({ data: { accountType: 'ADMIN', status: 'ACTIVE' } });
    expect(req.headers().authorization).toBe('Bearer admin-improvements-token');
    if (path.includes('/uploads/')) return route.fulfill({ contentType: 'image/png', body: png });
    const result = await handler(path, url, req.postData() ? req.postDataJSON() : null);
    return reply(result.payload ?? { data: result.data ?? [] }, result.status);
  });
}
async function login(page: Page, hash = '#approvals') {
  await page.goto(`http://localhost:5175/${hash}`);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
}
const emptyNotices = { payload: { data: [], unreadCount: 0, nextCursor: null } };

test('admin search and cursor pagination retain loaded queue and global summary', async ({ page }) => {
  const requests: URL[] = [];
  await mock(page, async (path, url) => {
    if (path === '/api/v1/me/notifications') return emptyNotices;
    if (path === '/api/v1/admin/approval-requests/') {
      requests.push(url); expect(url.searchParams.get('paged')).toBe('true'); expect(url.searchParams.get('limit')).toBe('20');
      const filtered = !!url.searchParams.get('q'); const more = !!url.searchParams.get('before');
      return { data: { items: filtered ? [row('filtered', 'Tìm đúng', 'VENUE_REVISION')] : [row(more ? 'second' : 'first', more ? 'Trang hai' : 'Trang một')],
        nextCursor: filtered || more ? null : 'cursor-one', totalCount: filtered ? 1 : 2, pendingCount: 151 } };
    }
    return { status: 404 };
  });
  await login(page);
  await expect(page.getByRole('status').filter({ hasText: '151 hồ sơ chờ xử lý toàn hệ thống' })).toBeVisible();
  await page.getByRole('button', { name: 'Xem thêm hồ sơ', exact: true }).click();
  await expect(page.getByRole('button', { name: /Trang hai/ })).toBeVisible();
  await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click();
  await expect(page.getByRole('button', { name: /Trang một/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Trang hai/ })).toBeVisible();
  expect(requests.map(url => url.searchParams.get('before'))).toEqual([null, 'cursor-one', null, 'cursor-one']);
  await page.getByLabel('Tìm tên doanh nghiệp').fill('  Tìm đúng  ');
  await page.getByLabel('Loại hồ sơ', { exact: true }).selectOption('VENUE_REVISION');
  await page.getByRole('button', { name: 'Tìm hồ sơ', exact: true }).click();
  await expect(page.getByRole('button', { name: /Tìm đúng/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Trang hai/ })).toHaveCount(0);
  expect(requests.at(-1)?.searchParams.get('q')).toBe('Tìm đúng');
  expect(requests.at(-1)?.searchParams.get('kind')).toBe('VENUE_REVISION');
  expect(requests.at(-1)?.searchParams.has('before')).toBe(false);
  await expect(page.getByRole('status').filter({ hasText: '151 hồ sơ chờ xử lý toàn hệ thống' })).toBeVisible();
  await page.getByRole('button', { name: 'Xóa bộ lọc', exact: true }).click();
  await expect(page.getByRole('button', { name: /Trang một/ })).toBeVisible();
});

test('admin cancels delayed search response when applying a new filter', async ({ page }) => {
  let started: (() => void) | undefined; let release: (() => void) | undefined;
  const began = new Promise<void>(resolve => { started = resolve; });
  const blocked = new Promise<void>(resolve => { release = resolve; });
  await mock(page, async (path, url) => {
    if (path === '/api/v1/me/notifications') return emptyNotices;
    if (path === '/api/v1/admin/approval-requests/') {
      const q = url.searchParams.get('q');
      if (q === 'Cũ') { started?.(); await blocked; }
      return { data: { items: [row(q ?? 'first', q ?? 'Ban đầu')], nextCursor: null, totalCount: 1, pendingCount: 151 } };
    }
    return { status: 404 };
  });
  await login(page);
  await page.getByLabel('Tìm tên doanh nghiệp').fill('Cũ');
  await page.getByRole('button', { name: 'Tìm hồ sơ', exact: true }).click(); await began;
  await page.getByLabel('Tìm tên doanh nghiệp').fill('Mới');
  await page.getByRole('button', { name: 'Tìm hồ sơ', exact: true }).click();
  await expect(page.getByRole('button', { name: /^Mới/ })).toBeVisible(); release?.();
  await expect(page.getByRole('button', { name: /^Cũ/ })).toHaveCount(0);
});

test('admin revision compares published bank and QR then requires final approval with conflict guidance', async ({ page }, info) => {
  let approved = 0;
  await mock(page, async path => {
    if (path === '/api/v1/me/notifications') return emptyNotices;
    if (path === '/api/v1/admin/approval-requests/') return { data: { items: [row('revision', 'Hồ sơ thay đổi', 'VENUE_REVISION')], nextCursor: null, totalCount: 1, pendingCount: 1 } };
    if (path === '/api/v1/admin/approval-requests/revision') return { data: { id: 'revision', kind: 'VENUE_REVISION', status: 'PENDING', snapshot: JSON.stringify(proposed), current } };
    if (path.endsWith('/approve')) { approved++; return { status: 409, payload: { code: 'REVISION_CONFLICT' } }; }
    return { status: 404 };
  });
  await login(page); await page.getByRole('button', { name: /Hồ sơ thay đổi/ }).click();
  const table = page.getByRole('region', { name: 'Đối chiếu thông tin cơ sở' });
  await expect(table.getByRole('cell', { name: '1111', exact: true })).toBeVisible();
  await expect(table.getByRole('cell', { name: '2222', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Xem QR đang công bố', exact: true }).click();
  await page.getByRole('button', { name: 'Xem QR mới', exact: true }).click();
  await expect(page.getByRole('img', { name: 'QR đang công bố', exact: true })).toHaveAttribute('src', /^blob:/);
  await expect(page.getByRole('img', { name: 'QR mới', exact: true })).toHaveAttribute('src', /^blob:/);
  await page.screenshot({ path: info.outputPath('admin-qr-before-approve.png'), fullPage: true });
  await page.getByRole('button', { name: 'Phê duyệt', exact: true }).click(); expect(approved).toBe(0);
  await page.getByRole('button', { name: 'Hủy phê duyệt', exact: true }).click(); expect(approved).toBe(0);
  await expect(page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Phê duyệt', exact: true }).click();
  await page.screenshot({ path: info.outputPath(`admin-revision-${info.project.name}.png`), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'Hồ sơ đã thay đổi' })).toBeVisible();
  expect(approved).toBe(1); await expect(page.getByText('Đã lưu quyết định.', { exact: true })).toHaveCount(0);
});

test('admin change reason validates trimmed bounds and sends trimmed text', async ({ page }) => {
  let requested: string | null = null; let decided = false;
  await mock(page, async (path, _url, body) => {
    if (path === '/api/v1/me/notifications') return emptyNotices;
    if (path === '/api/v1/admin/approval-requests/') return { data: decided ? [] : [row('onboarding', 'Hồ sơ kiểm thử')] };
    if (path === '/api/v1/admin/approval-requests/onboarding') return { data: { id: 'onboarding', kind: 'ONBOARDING', status: 'PENDING', snapshot: JSON.stringify(profile) } };
    if (path.endsWith('/request-changes')) { requested = (body as { reason: string }).reason; decided = true; return { data: { status: 'CHANGES_REQUESTED' } }; }
    return { status: 404 };
  });
  await login(page); await page.getByRole('button', { name: /Hồ sơ kiểm thử/ }).click();
  const reason = page.getByLabel('Lý do cần chỉnh sửa'); const submit = page.getByRole('button', { name: 'Yêu cầu chỉnh sửa', exact: true });
  await reason.fill('   123456789   '); await expect(submit).toBeDisabled();
  await reason.fill('x'.repeat(1001)); await expect(submit).toBeDisabled(); await expect(reason).toHaveAttribute('aria-invalid', 'true');
  await reason.fill('x'.repeat(1000)); await expect(submit).toBeEnabled();
  await reason.fill('  1234567890  '); await expect(submit).toBeEnabled(); await submit.click();
  await expect(page.getByText('Đã lưu quyết định.', { exact: true })).toBeVisible(); expect(requested).toBe('1234567890');
});

test('admin mark read refreshes all loaded notification pages using the fresh cursor chain and purges on denial', async ({ page }) => {
  let read = false; let denied = false; const cursors: Array<string | null> = [];
  await mock(page, async (path, url) => {
    if (path === '/api/v1/admin/approval-requests/') return { data: [] };
    if (path === '/api/v1/me/notifications') {
      const cursor = url.searchParams.get('before'); cursors.push(cursor);
      if (denied && cursor) return { status: 403 };
      const notice = (id: string, title: string) => ({ id, title, body: title, createdAt: '2026-10-09T01:00:00Z', readAt: id === 'older' && read ? '2026-10-09T02:00:00Z' : null });
      return { payload: { data: cursor ? [notice('older', 'Thông báo trang hai')] : [notice('latest', 'Thông báo mới nhất')], unreadCount: read ? 1 : 2, nextCursor: cursor ? null : read ? 'new-cursor' : 'old-cursor' } };
    }
    if (path === '/api/v1/me/notifications/older/read') { read = true; return { data: { id: 'older' } }; }
    return { status: 404 };
  });
  await login(page, '#notifications');
  await page.getByRole('button', { name: 'Xem thêm thông báo', exact: true }).click();
  const older = page.getByRole('listitem').filter({ hasText: 'Thông báo trang hai' });
  await older.getByRole('button', { name: 'Đánh dấu đã đọc', exact: true }).click();
  await expect(older.getByText('Đã đọc', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: /Thông báo.*1 chưa đọc/ })).toBeVisible();
  expect(cursors).toEqual([null, 'old-cursor', null, 'new-cursor']);
  await page.getByRole('button', { name: 'Làm mới thông báo', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Làm mới thông báo', exact: true })).toBeEnabled();
  await expect(older).toBeVisible(); expect(cursors.slice(-2)).toEqual([null, 'new-cursor']);
  denied = true;
  await page.getByRole('button', { name: 'Làm mới thông báo', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'quyền truy cập không còn hợp lệ' })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: 'Thông báo' })).toHaveCount(0);
});

test('admin approval waits for published comparison and submits one explicit final decision', async ({ page }) => {
  let includeCurrent = false; let approved = 0;
  await mock(page, async path => {
    if (path === '/api/v1/me/notifications') return emptyNotices;
    if (path === '/api/v1/admin/approval-requests/') return { data: { items: approved ? [] : [row('revision', 'Cần đối chiếu', 'VENUE_REVISION')], nextCursor: null, totalCount: approved ? 0 : 1, pendingCount: approved ? 0 : 1 } };
    if (path === '/api/v1/admin/approval-requests/revision') return { data: { id: 'revision', kind: 'VENUE_REVISION', status: 'PENDING', snapshot: JSON.stringify(proposed), current: includeCurrent ? current : null } };
    if (path.endsWith('/approve')) { approved++; return { data: { status: 'APPROVED' } }; }
    return { status: 404 };
  });
  await login(page); await page.getByRole('button', { name: /Cần đối chiếu/ }).click();
  await expect(page.getByRole('button', { name: 'Phê duyệt', exact: true })).toBeDisabled();
  expect(approved).toBe(0);
  includeCurrent = true;
  await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click();
  await page.getByRole('button', { name: /Cần đối chiếu/ }).click();
  await page.getByRole('button', { name: 'Phê duyệt', exact: true }).click();
  await expect(page.getByRole('group', { name: 'Xác nhận phê duyệt hồ sơ', exact: true })).toBeVisible();
  expect(approved).toBe(0);
  await page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true }).click();
  await expect(page.getByText('Đã lưu quyết định.', { exact: true })).toBeVisible();
  expect(approved).toBe(1);
  await expect(page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true })).toHaveCount(0);
  await expect(page.getByRole('status').filter({ hasText: '0 hồ sơ chờ xử lý toàn hệ thống' })).toBeVisible();
});
