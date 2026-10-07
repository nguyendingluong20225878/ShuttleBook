import { expect, test } from '@playwright/test';

const notices = [
  { id: '0199f060-0000-7000-8000-000000000021', title: 'Đơn quá hạn đối chiếu', body: 'BK-DEMO · Cơ sở A · Sân 1. Khung giờ vẫn được giữ.',
    createdAt: '2026-10-07T01:00:00Z', readAt: null as string | null, bookingId: '0199f060-0000-7000-8000-000000000001', action: 'ADMIN_PAYMENT_ALERT' },
];
const session = { data: { tokenType: 'Bearer', accessToken: 'access-admin-f06', refreshToken: 'refresh-admin-f06',
  expiresInSeconds: 600, user: { accountType: 'ADMIN', status: 'ACTIVE' } } };

for (const retry of [false, true]) {
  test(`admin notification ${retry ? 'error retry and idle timeout' : 'read and safe alert display'}`, async ({ page }) => {
    let active = false; let failed = retry; let read = false;
    await page.clock.install();
    await page.route('**/api/v1/**', async route => {
      const request = route.request(); const path = new URL(request.url()).pathname;
      const reply = (value: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify(value) });
      if (path === '/api/v1/admin-auth/login') { active = true; return reply(session); }
      if (path === '/api/v1/admin-auth/restore') return active ? reply(session) : route.fulfill({ status: 401, body: '{}' });
      if (path === '/api/v1/admin-auth/me') return reply({ data: { accountType: 'ADMIN', status: 'ACTIVE' } });
      if (path === '/api/v1/admin/approval-requests/') return reply({ data: [] });
      if (path === '/api/v1/me/notifications') {
        expect(request.headers().authorization).toBe('Bearer access-admin-f06');
        if (failed) return route.fulfill({ status: 503, body: '{}' });
        return reply({ data: notices.map(item => ({ ...item, readAt: read ? '2026-10-07T01:10:00Z' : null })), unreadCount: read ? 0 : 1, nextCursor: null });
      }
      if (path.endsWith('/read')) { read = true; return reply({ data: { id: notices[0].id, readAt: '2026-10-07T01:10:00Z' } }); }
      return route.fulfill({ status: 404, body: '{}' });
    });
    await page.goto('http://localhost:5175');
    await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    if (retry) {
      await expect(page.getByRole('alert')).toContainText('Không thể tải thông báo');
      failed = false; await page.getByRole('button', { name: 'Làm mới thông báo' }).click();
    }
    await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).toBeVisible();
    await expect(page.getByText('Cổng Admin không xác nhận tiền thay chủ sân.', { exact: false })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Xác nhận đã nhận tiền' })).toHaveCount(0);
    if (retry) {
      await page.clock.fastForward(31 * 60_000);
      await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
      await expect(page.getByText('Đơn quá hạn đối chiếu', { exact: true })).toHaveCount(0);
    } else {
      await page.getByRole('button', { name: 'Đánh dấu đã đọc' }).click();
      await expect(page.getByRole('heading', { name: 'Thông báo 0 chưa đọc' })).toBeVisible();
      await page.reload();
      await expect(page.getByRole('heading', { name: 'Thông báo 0 chưa đọc' })).toBeVisible();
    }
  });
}
