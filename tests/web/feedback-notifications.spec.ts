import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

const noticeId = '019a1234-1111-7111-8111-000000000001';
const olderId = '019a1234-1111-7111-8111-000000000002';
const createdAt = '2026-10-13T00:00:00Z';

test('Admin badge uses global unread, failed open retains unread and successful approval click navigates safely', async ({ page }) => {
  let active = false; let failed = true; let readAt: string | null = null; let reads = 0;
  const session = { data: { tokenType: 'Bearer', accessToken: 'admin-feedback-fixture', refreshToken: 'fixture-refresh', expiresInSeconds: 3600,
    user: { accountType: 'ADMIN', status: 'ACTIVE' } } };
  await page.route('**/api/v1/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const reply = (body: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (path === '/api/v1/admin-auth/login') { active = true; return reply(session); }
    if (path === '/api/v1/admin-auth/restore') return reply(active ? session : {}, active ? 200 : 401);
    if (path === '/api/v1/admin-auth/me') return reply({ data: { accountType: 'ADMIN', status: 'ACTIVE' } });
    if (path === '/api/v1/admin/approval-requests/') return reply({ data: [] });
    if (path === '/api/v1/me/notifications') return reply({ data: [{ id: noticeId, title: 'Cần kiểm tra hồ sơ', body: 'Hồ sơ mới cần kiểm tra.',
      createdAt, readAt, action: 'ADMIN_APPROVAL' }], unreadCount: readAt ? 69 : 70, nextCursor: null });
    if (path === `/api/v1/me/notifications/${noticeId}/read`) {
      reads++; if (failed) return reply({ code: 'TEMPORARY_FAILURE' }, 503);
      readAt = createdAt; return reply({ data: { id: noticeId, readAt, unreadCount: 69 } });
    }
    return reply({}, 404);
  });
  await page.goto('http://localhost:5175/#notifications');
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Fixture-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.locator('.admin-notice-count')).toHaveText('70');
  await expect(page.getByRole('button', { name: 'Đánh dấu đã đọc' })).toHaveCount(0);
  const open = page.getByRole('button', { name: 'Cần kiểm tra hồ sơ', exact: true });
  await open.focus(); await open.press('Enter');
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.locator('.admin-notice-count')).toHaveText('70');
  await expect(page).toHaveURL(/#notifications$/);
  failed = false; await open.click();
  await expect(page).toHaveURL(/#approvals$/);
  await expect(page.locator('.admin-notice-count')).toHaveText('69');
  expect(reads).toBe(2);
  await page.evaluate(() => { location.hash = '#notifications'; });
  await open.click(); await expect(page).toHaveURL(/#approvals$/); expect(reads).toBe(2);
  await expect(page.getByRole('button', { name: 'Xác nhận đã nhận tiền' })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('Customer older notice click preserves server count on failure, read state on polling and idempotency', async ({ page }) => {
  let failed = true; let readAt: string | null = null; let reads = 0;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const url = new URL(route.request().url()), path = url.pathname;
    const reply = (body: object, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (path.endsWith('/browser-auth/customer/login')) return reply({ data: { accessToken: 'customer-feedback-fixture', ...browserSessionFields(),
      expiresInSeconds: 3600, user: { accountType: 'CUSTOMER', status: 'ACTIVE' } } });
    if (path.endsWith('/me/notifications')) return reply({ data: url.searchParams.has('before') ?
      [{ id: olderId, title: 'Thông báo cũ', body: 'Nội dung chi tiết của thông báo cũ.', createdAt, readAt }] :
      [{ id: noticeId, title: 'Thông báo mới', body: 'Nội dung mới.', createdAt, readAt: null }],
      unreadCount: readAt ? 54 : 55, nextCursor: url.searchParams.has('before') ? null : noticeId });
    if (path.endsWith(`/notifications/${olderId}/read`)) {
      reads++; if (failed) return reply({ code: 'TEMPORARY_FAILURE' }, 503);
      readAt = createdAt; return reply({ data: { id: olderId, readAt, unreadCount: 54 } });
    }
    return reply({}, 404);
  });
  await page.goto('http://localhost:5173/login?returnTo=%2Fme%2Fnotifications');
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Fixture-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByText('55 thông báo chưa đọc', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Xem thêm thông báo', exact: true }).click();
  await expect(page.locator('.customer-notifications > li')).toHaveCount(2);
  const open = page.getByRole('button', { name: 'Thông báo cũ', exact: true });
  await open.click(); await expect(page.getByRole('region', { name: 'Chi tiết: Thông báo cũ' })).toBeVisible();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByText('55 thông báo chưa đọc', { exact: true })).toBeVisible();
  failed = false; await open.focus(); await open.press('Enter');
  await expect(page.getByText('54 thông báo chưa đọc', { exact: true })).toBeVisible();
  const older = page.locator('.customer-notifications > li').filter({ has: open });
  await expect(older).not.toHaveClass('is-unread');
  await open.click(); expect(reads).toBe(2);
  await page.getByRole('button', { name: 'Làm mới thông báo', exact: true }).click();
  await expect(page.locator('.customer-notifications > li')).toHaveCount(2);
  await expect(older).not.toHaveClass('is-unread');
  await expect(page.getByRole('button', { name: 'Đánh dấu đã đọc' })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.reload(); await expect(page.getByText('54 thông báo chưa đọc', { exact: true })).toBeVisible();
});
