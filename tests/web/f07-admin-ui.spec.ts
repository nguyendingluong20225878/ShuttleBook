import { expect, Page, test } from '@playwright/test';

const id = '0199f070-0000-7000-8000-000000000081';
const noticeId = '0199f070-0000-7000-8000-000000000082';
const businessName = 'ShuttleBook Hoàng Cầu';
const profile = { name: businessName, legalName: 'Công ty thể thao Hoàng Cầu', contact: 'owner@example.test', venues: [{
  name: 'Cơ sở Hoàng Cầu', address: '31 ngõ 16 Hoàng Cầu, Hà Nội', contact: 'owner@example.test',
  latitude: 21.0186, longitude: 105.8236, timezone: 'Asia/Ho_Chi_Minh', imageUploadId: 'venue-image',
  bankCode: 'DEMO', accountName: 'DOI TAC THU NGHIEM', accountNumber: '00000000', qrUploadId: 'venue-qr',
  courts: [{ name: 'Sân cầu lông 1', hours: [{ dayOfWeek: 1, opensAt: '05:00:00', closesAt: '22:00:00' }],
    prices: [{ dayOfWeek: 1, startsAt: '05:00:00', endsAt: '22:00:00', pricePerSlot: 40000 }] }],
}] };

type Controls = { listFailure: boolean; noticeFailure: boolean; listDenied: boolean; noticeDenied: boolean; decided: boolean; read: boolean;
  requestedReason: string | null; noticesCalls: number; meCalls: number };
async function mockApi(page: Page): Promise<Controls> {
  const state: Controls = { listFailure: false, noticeFailure: false, listDenied: false, noticeDenied: false, decided: false, read: false,
    requestedReason: null, noticesCalls: 0, meCalls: 0 };
  let active = false;
  const session = { data: { tokenType: 'Bearer', accessToken: 'admin-ui-demo', refreshToken: 'admin-ui-refresh-demo',
    expiresInSeconds: 3600, user: { accountType: 'ADMIN', status: 'ACTIVE' } } };
  await page.route('**/api/v1/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (body: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) });
    if (path === '/api/v1/admin-auth/login') { active = true; return reply(session); }
    if (path === '/api/v1/admin-auth/restore') return active ? reply(session) : route.fulfill({ status: 401, body: '{}' });
    if (path === '/api/v1/admin-auth/me') { state.meCalls++; return reply({ data: { accountType: 'ADMIN', status: 'ACTIVE' } }); }
    if (path === '/api/v1/auth/logout') { active = false; return route.fulfill({ status: 204 }); }
    expect(request.headers().authorization).toBe('Bearer admin-ui-demo');
    if (path === '/api/v1/admin/approval-requests/') return state.listDenied ? route.fulfill({ status: 403, body: '{}' }) : state.listFailure ? route.fulfill({ status: 503, body: '{}' }) :
      reply({ data: state.decided ? [] : [{ id, businessId: 'business-demo', businessName, kind: 'ONBOARDING', status: 'PENDING', submittedAt: '2026-10-07T01:00:00Z' }] });
    if (path === `/api/v1/admin/approval-requests/${id}`) return reply({ data: { id, kind: 'ONBOARDING', status: 'PENDING', snapshot: JSON.stringify(profile) } });
    if (path === `/api/v1/admin/approval-requests/${id}/request-changes`) {
      state.requestedReason = (request.postDataJSON() as { reason: string }).reason;
      state.decided = true; return reply({ data: { status: 'CHANGES_REQUESTED' } });
    }
    if (path === '/api/v1/me/notifications') {
      state.noticesCalls++;
      return state.noticeDenied ? route.fulfill({ status: 403, body: '{}' }) : state.noticeFailure ? route.fulfill({ status: 503, body: '{}' }) : reply({ data: [{ id: noticeId,
        title: 'Đơn quá hạn đối chiếu', body: 'BK-DEMO · Hoàng Cầu. Khung giờ vẫn được giữ.', createdAt: '2026-10-07T01:00:00Z',
        readAt: state.read ? '2026-10-07T01:05:00Z' : null, action: 'ADMIN_PAYMENT_ALERT' }], unreadCount: state.read ? 0 : 1, nextCursor: null });
    }
    if (path === `/api/v1/me/notifications/${noticeId}/read`) { state.read = true; return reply({ data: { id: noticeId } }); }
    if (path === '/api/v1/uploads/venue-image/view') return route.fulfill({ contentType: 'image/png',
      body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=', 'base64') });
    return route.fulfill({ status: 404, body: '{}' });
  });
  return state;
}
async function login(page: Page, hash = '') {
  await page.goto(`http://localhost:5175/${hash}`);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Ui-demo-password!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
}
async function navigate(page: Page, name: string) {
  const opener = page.getByRole('button', { name: 'Mở menu quản trị', exact: true });
  if (await opener.isVisible()) await opener.click();
  await page.getByRole('navigation', { name: 'Điều hướng quản trị' }).getByRole('link', { name, exact: true }).click();
}

test('admin overview has response counters, one notification source and safe alerts', async ({ page }, info) => {
  const state = await mockApi(page); await login(page);
  await expect(page.getByRole('heading', { name: 'Tổng quan', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: /Hồ sơ chờ xử lý 1/ })).toBeVisible();
  await expect(page.getByRole('link', { name: /Thông báo chưa đọc 1/ })).toBeVisible();
  await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).toHaveCount(1);
  expect(state.noticesCalls).toBe(1);
  await expect(page.getByRole('button', { name: 'Xác nhận đã nhận tiền' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Đánh dấu đã đọc' }).click();
  await expect(page.getByRole('link', { name: /Thông báo chưa đọc 0/ })).toBeVisible();
  await page.screenshot({ path: info.outputPath(`admin-overview-${info.project.name}.png`), fullPage: true });
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('admin deep link, navigation history and approval draft retain exact decision contract', async ({ page }, info) => {
  const state = await mockApi(page); await login(page, '#approvals');
  await expect(page.getByRole('heading', { name: 'Duyệt hồ sơ', exact: true })).toBeVisible();
  const skip = page.getByRole('link', { name: 'Đến nội dung chính', exact: true });
  await skip.focus(); await skip.press('Enter');
  await expect(page.locator('#admin-main')).toBeFocused();
  await expect(page).toHaveURL(/#approvals$/);
  await expect(page.getByRole('heading', { name: 'Duyệt hồ sơ', exact: true })).toBeVisible();
  await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).not.toBeVisible();
  await page.getByRole('button', { name: new RegExp(businessName) }).click();
  await expect(page.getByRole('heading', { name: businessName, exact: true })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: 'Thứ 2' }).first()).toBeVisible();
  await expect(page.getByText('05:00–22:00', { exact: true })).toBeVisible();
  const reason = 'Vui lòng kiểm tra lại tên pháp lý của doanh nghiệp.';
  await page.getByLabel('Lý do cần chỉnh sửa').fill(reason);
  await navigate(page, 'Thông báo');
  await expect(page.getByRole('heading', { level: 1, name: 'Thông báo', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.getByRole('heading', { name: 'Duyệt hồ sơ', exact: true })).toBeVisible();
  await expect(page.getByLabel('Lý do cần chỉnh sửa')).toHaveValue(reason);
  await page.goForward();
  await navigate(page, 'Duyệt hồ sơ');
  await page.locator('#admin-main').focus();
  await page.screenshot({ path: info.outputPath(`admin-approval-${info.project.name}.png`), fullPage: true });
  await page.getByRole('button', { name: 'Yêu cầu chỉnh sửa', exact: true }).click();
  await expect(page.getByText('Đã lưu quyết định.', { exact: true })).toBeVisible();
  expect(state.requestedReason).toBe(reason);
  await expect(page.getByText('Chưa có hồ sơ chờ duyệt.', { exact: true })).toBeVisible();
  await navigate(page, 'Tổng quan');
  await expect(page.getByRole('link', { name: /Hồ sơ chờ xử lý 0/ })).toBeVisible();
});

test('admin load failures have retries and never masquerade as empty counts', async ({ page }) => {
  const state = await mockApi(page); state.listFailure = true; state.noticeFailure = true;
  await login(page);
  await expect(page.getByRole('alert').filter({ hasText: 'Không thể tải hồ sơ' })).toBeVisible();
  await expect(page.getByRole('alert').filter({ hasText: 'Không thể tải thông báo' })).toBeVisible();
  await expect(page.getByRole('link', { name: /Hồ sơ chờ xử lý Chưa tải được/ })).toBeVisible();
  await expect(page.getByRole('link', { name: /Thông báo chưa đọc Chưa tải được/ })).toBeVisible();
  await expect(page.getByText('Chưa có hồ sơ chờ duyệt.', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Chưa có thông báo.', { exact: true })).toHaveCount(0);
  state.listFailure = false; state.noticeFailure = false;
  await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click();
  await page.getByRole('button', { name: 'Làm mới thông báo', exact: true }).click();
  await expect(page.getByRole('link', { name: /Hồ sơ chờ xử lý 1/ })).toBeVisible();
  await expect(page.getByRole('link', { name: /Thông báo chưa đọc 1/ })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('admin responsive layout keeps controls readable and menu keyboard accessible', async ({ page }) => {
  await mockApi(page); await login(page);
  for (const width of [375, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    const controls = await page.locator('button:visible, input:visible, select:visible, textarea:visible').evaluateAll(elements =>
      elements.map(element => ({ text: element.textContent, height: element.getBoundingClientRect().height })));
    expect(controls.filter(control => control.height < 44)).toEqual([]);
    if (width <= 800) {
      const opener = page.getByRole('button', { name: 'Mở menu quản trị', exact: true });
      await opener.click();
      await expect(page.getByRole('button', { name: 'Đóng menu quản trị', exact: true })).toHaveAttribute('aria-expanded', 'true');
      await page.getByRole('navigation', { name: 'Điều hướng quản trị' }).getByRole('link', { name: 'Thông báo', exact: true }).focus();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('button', { name: 'Mở menu quản trị', exact: true })).toBeFocused();
      await expect(page.getByRole('navigation', { name: 'Điều hướng quản trị' })).not.toBeVisible();
    } else await expect(page.getByRole('navigation', { name: 'Điều hướng quản trị' })).toBeVisible();
  }
});

test('admin remains active after real input but background navigation never renews idle session', async ({ page }) => {
  const state = await mockApi(page); await page.clock.install(); await login(page);
  await page.clock.fastForward(29 * 60_000);
  await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click();
  await page.clock.fastForward(2 * 60_000);
  await expect(page.getByRole('heading', { name: 'Tổng quan', exact: true })).toBeVisible();
  const meCalls = state.meCalls;
  const noticeCalls = state.noticesCalls;
  await page.evaluate(() => { window.location.hash = '#notifications'; window.dispatchEvent(new FocusEvent('focus')); });
  await page.clock.fastForward(29 * 60_000);
  await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  expect(state.meCalls).toBe(meCalls);
  expect(state.noticesCalls).toBe(noticeCalls);
  await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).toHaveCount(0);
});

test('admin denied responses clear the already loaded private profile, images and notifications', async ({ page }) => {
  const state = await mockApi(page); await login(page);
  await page.getByRole('button', { name: new RegExp(businessName) }).click();
  await page.getByRole('button', { name: 'Xem ảnh cơ sở', exact: true }).click();
  await expect(page.getByRole('img', { name: 'Ảnh Cơ sở Hoàng Cầu' })).toHaveAttribute('src', /^blob:/);
  state.listDenied = true;
  await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'quyền truy cập không còn hợp lệ' })).toBeVisible();
  await expect(page.getByRole('heading', { name: businessName, exact: true })).toHaveCount(0);
  await expect(page.getByRole('img', { name: 'Ảnh Cơ sở Hoàng Cầu' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: new RegExp(businessName) })).toHaveCount(0);
  state.noticeDenied = true;
  await page.getByRole('button', { name: 'Làm mới thông báo', exact: true }).click();
  await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /Thông báo chưa đọc Chưa tải được/ })).toBeVisible();
});

test('admin refresh during a pending detail request restores enabled queue controls', async ({ page }) => {
  await page.clock.install();
  let loggedIn = false; let refreshed = false; let detailCalls = 0;
  let detailStarted: (() => void) | undefined; let releaseDetail: (() => void) | undefined;
  const started = new Promise<void>(resolve => { detailStarted = resolve; });
  const delayed = new Promise<void>(resolve => { releaseDetail = resolve; });
  const session = (token: string, seconds: number) => ({ data: { tokenType: 'Bearer', accessToken: token,
    refreshToken: 'admin-ui-refresh-demo', expiresInSeconds: seconds, user: { accountType: 'ADMIN', status: 'ACTIVE' } } });
  await page.route('**/api/v1/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify(value) });
    if (path === '/api/v1/admin-auth/login') { loggedIn = true; return reply(session('admin-before-refresh', 1)); }
    if (path === '/api/v1/admin-auth/restore') {
      if (!loggedIn) return route.fulfill({ status: 401, body: '{}' });
      refreshed = true; return reply(session('admin-after-refresh', 3600));
    }
    if (path === '/api/v1/admin-auth/me') return reply({ data: { accountType: 'ADMIN', status: 'ACTIVE' } });
    if (path === '/api/v1/admin/approval-requests/') return reply({ data: [{ id, businessName, kind: 'ONBOARDING', submittedAt: '2026-10-07T01:00:00Z' }] });
    if (path === '/api/v1/me/notifications') return reply({ data: [], unreadCount: 0 });
    if (path === `/api/v1/admin/approval-requests/${id}`) {
      detailCalls++;
      if (detailCalls === 1) { detailStarted?.(); await delayed; }
      return reply({ data: { id, kind: 'ONBOARDING', status: 'PENDING', snapshot: JSON.stringify(profile) } });
    }
    return route.fulfill({ status: 404, body: '{}' });
  });
  await login(page);
  await page.getByRole('button', { name: new RegExp(businessName) }).click();
  await started;
  await expect(page.getByRole('button', { name: new RegExp(businessName) })).toBeDisabled();
  await page.clock.fastForward(1500);
  await expect.poll(() => refreshed).toBe(true);
  await expect(page.getByRole('button', { name: new RegExp(businessName) })).toBeEnabled();
  releaseDetail?.();
  await page.getByRole('button', { name: new RegExp(businessName) }).click();
  await expect(page.getByRole('heading', { name: businessName, exact: true })).toBeVisible();
});
