import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

const venueId = '0199f070-0000-7000-8000-000000000001';
const notificationId = '0199f070-0000-7000-8000-000000000002';
const loginData = { tokenType: 'Bearer', accessToken: 'customer-ui-access', ...browserSessionFields(),
  expiresInSeconds: 600, user: { accountType: 'CUSTOMER', status: 'ACTIVE' } };

async function noViewportOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

test('customer account forms support keyboard skip, actionable feedback and responsive controls', async ({ page }) => {
  let registrationRequests = 0;
  await page.route('**/api/v1/auth/register', async route => {
    if (await browserSessionRoute(route, page)) return; registrationRequests++; return route.abort(); });
  await page.goto('http://localhost:5173/');
  // Startup cookie restore temporarily renders a non-interactive loading page.
  // Check keyboard order after the account form has actually mounted.
  await expect(page.getByLabel('Email', { exact: true })).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Chuyển đến nội dung' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#customer-content')).toBeFocused();
  await expect(page.getByLabel('Email', { exact: true })).toHaveAttribute('autocomplete', 'email');
  await expect(page.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('autocomplete', 'new-password');
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Strong-password-2026!');
  await page.getByLabel('Nhập lại mật khẩu').fill('Different-password-2026!');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Mật khẩu nhập lại chưa khớp.');
  await expect(page.getByRole('status')).toBeFocused();
  await expect(page.getByRole('link', { name: 'Chuyển đến nội dung' })).toHaveCSS('opacity', '0');
  await expect(page.getByLabel('Nhập lại mật khẩu')).toHaveAttribute('aria-invalid', 'true');
  expect(registrationRequests).toBe(0);
  for (const width of [375, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await noViewportOverflow(page);
    const chrome = await page.locator('.site-header').boundingBox();
    const content = await page.locator('.customer-workspace').boundingBox();
    expect(chrome!.x).toBe(0);
    expect(chrome!.width).toBe(width);
    expect(chrome!.height).toBeLessThan(180);
    expect(content!.x).toBe(0);
    expect(content!.y).toBeGreaterThanOrEqual(chrome!.y + chrome!.height);
    await expect(page.locator('.customer-nav-caption, .customer-sidebar-account, .customer-toolbar')).toHaveCount(0);
    const tooSmall = await page.locator('.identity-form input, .identity-form button, .identity-tabs button, .site-header nav a')
      .evaluateAll(elements => elements.some(element => element.getBoundingClientRect().height < 44));
    expect(tooSmall).toBe(false);
  }
  await page.setViewportSize({ width: test.info().project.name === 'mobile' ? 375 : 1440, height: 900 });
  await page.screenshot({ path: test.info().outputPath('f07-customer-account.png'), fullPage: true });
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('customer search offers retry and location fallback while long addresses remain readable', async ({ page }) => {
  let failed = true; let nearbyRequests = 0;
  const longName = `Hoàng Cầu ${'Cơ sở có tên dài '.repeat(10)}`;
  const longAddress = `31 ngõ 16 Hoàng Cầu, Hà Nội ${'Địa chỉ chi tiết rất dài '.repeat(10)}`;
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'geolocation', { configurable: true,
      value: { getCurrentPosition: (_success: unknown, error: (value: object) => void) => error({ code: 1 }) } });
  });
  await page.route('https://cdn.maptiler.com/**', route => route.abort());
  await page.route('**/api/v1/venues**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const url = new URL(route.request().url());
    if (url.pathname.includes('/nearby')) nearbyRequests++;
    if (failed) return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ code: 'SERVICE_UNAVAILABLE' }) });
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: { items: [{ id: venueId,
      name: longName, address: longAddress, latitude: 21.02, longitude: 105.82, imageUrl: null }], nextCursor: null } }) });
  });
  await page.goto('http://localhost:5173/venues');
  await expect(page.getByRole('button', { name: 'Thử lại', exact: true })).toBeVisible();
  failed = false;
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByText('Đã tải 1 cơ sở.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Dùng vị trí của tôi', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'Bạn chưa cấp quyền vị trí' })).toBeVisible();
  await page.getByLabel('Tên sân hoặc địa chỉ').fill('Hoàng Cầu');
  await page.getByRole('button', { name: 'Tìm sân', exact: true }).click();
  await expect(page).toHaveURL(/q=Ho/);
  await expect(page.getByRole('link', { name: 'Xem lịch các sân', exact: false })).toBeVisible();
  await expect(page.locator('.site-header nav a[href="/venues"]')).toHaveAttribute('aria-current', 'page');
  for (const width of [375, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await noViewportOverflow(page);
    await expect(page.getByRole('heading', { name: longName.trim(), exact: true })).toBeVisible();
    const card = await page.locator('.venue-card').boundingBox();
    expect(card!.width).toBeLessThanOrEqual(width);
  }
  expect(nearbyRequests).toBe(0);
  await page.setViewportSize({ width: test.info().project.name === 'mobile' ? 375 : 1440, height: 900 });
  await page.screenshot({ path: test.info().outputPath('f07-customer-search.png'), fullPage: true });
});

test('customer inbox displays real unread status, marks read and restores private inbox through browser session', async ({ page }) => {
  let readAt: string | null = null;
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const path = new URL(route.request().url()).pathname;
    const reply = (data: object) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data }) });
    if (path.endsWith('/browser-auth/customer/login')) return reply(loginData);
    if (path.endsWith(`/notifications/${notificationId}/read`)) { readAt = new Date().toISOString(); return reply({ id: notificationId, readAt }); }
    if (path.endsWith('/me/notifications')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({
      data: [{ id: notificationId, title: 'Chủ sân đã xác nhận thanh toán', body: 'Thông tin đặt sân của bạn đã được cập nhật. '.repeat(12),
        readAt, bookingId: venueId, action: 'CUSTOMER_BOOKING', createdAt: '2026-10-07T04:00:00Z' }], unreadCount: readAt ? 0 : 1, nextCursor: null }) });
    return route.abort();
  });
  await page.goto('http://localhost:5173/login?returnTo=%2Fme%2Fnotifications');
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Strong-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByText('1 thông báo chưa đọc', { exact: true })).toBeVisible();
  await expect(page.getByText('Chưa đọc', { exact: true })).toBeVisible();
  await expect(page.locator('.site-header nav a[href="/me/notifications"]')).toHaveAttribute('aria-current', 'page');
  await page.getByRole('button', { name: 'Đánh dấu đã đọc', exact: true }).click();
  await expect(page.getByText('0 thông báo chưa đọc', { exact: true })).toBeVisible();
  await expect(page.getByText('Đã đọc', { exact: true })).toBeVisible();
  for (const width of [375, 768, 1024, 1440]) { await page.setViewportSize({ width, height: 900 }); await noViewportOverflow(page); }
  await page.setViewportSize({ width: test.info().project.name === 'mobile' ? 375 : 1440, height: 900 });
  await page.screenshot({ path: test.info().outputPath('f07-customer-inbox.png'), fullPage: true });
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Thông báo', exact: true })).toBeVisible();
  await expect(page.getByText('0 thông báo chưa đọc', { exact: true })).toBeVisible();
});
