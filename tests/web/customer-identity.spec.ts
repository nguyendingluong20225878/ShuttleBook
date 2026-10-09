import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

const customerUrl = 'http://localhost:5173';
function tokenPayload(accessToken: string, expiresInSeconds = 600) {
  return { data: { ...browserSessionFields(), accessToken, expiresInSeconds,
    user: { id: '00000000-0000-7000-8000-000000000001', accountType: 'CUSTOMER', status: 'ACTIVE' } }, traceId: 'customer-web-test' };
}
test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => { if (!(await browserSessionRoute(route, page))) await route.fallback(); });
});

test('customer can register, verify, log in, restore after F5 and log out without token storage', async ({ page }) => {
  const requests: { path: string; body: Record<string, string>; authorization?: string }[] = [];
  let active = false;
  await page.route('**/api/v1/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': customerUrl, 'Access-Control-Allow-Credentials': 'true' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    const body = request.method() === 'POST' ? request.postDataJSON() : {};
    requests.push({ path, body, authorization: request.headers().authorization });
    const reply = (value: object, status = 200) => route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(value) });
    if (path.endsWith('/register') || path.endsWith('/verification-resend')) return reply({ data: { verificationRequired: true } }, 202);
    if (path.endsWith('/verify-contact')) return reply({ data: { verified: true } });
    if (path === '/api/v1/browser-auth/customer/login') { active = true; return reply(tokenPayload('customer-access')); }
    if (path === '/api/v1/browser-auth/customer/restore') return active ? reply(tokenPayload('customer-restored-access')) : reply({ code: 'INVALID_REFRESH_TOKEN' }, 401);
    if (path === '/api/v1/browser-auth/customer/activity') return active ? reply(tokenPayload('customer-restored-access')) : reply({ code: 'INVALID_REFRESH_TOKEN' }, 401);
    if (path === '/api/v1/browser-auth/customer/logout') { active = false; return route.fulfill({ status: 204, headers }); }
    if (path.includes('/venues')) return reply({ data: { items: [], nextCursor: null } });
    if (path.includes('/notifications')) return reply({ data: [], unreadCount: 0 });
    return route.abort();
  });
  await page.goto(customerUrl);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Example-password-2026!');
  await page.getByLabel('Nhập lại mật khẩu').fill('Example-password-2026!');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Xác minh tài khoản khách' })).toBeVisible();
  await page.getByLabel('Mã xác minh 6 chữ số').fill('123456');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Example-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Chọn cơ sở phù hợp với bạn' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Chọn cơ sở phù hợp với bạn' })).toBeVisible();
  expect(requests.filter(item => item.path.endsWith('/login'))).toHaveLength(1);
  expect(requests.filter(item => item.path.endsWith('/restore'))).toHaveLength(2);
  expect(requests.filter(item => item.path.endsWith('/restore')).every(item => JSON.stringify(item.body) === '{}')).toBe(true);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  expect(requests.find(item => item.path.endsWith('/logout'))?.body).toEqual({});
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
});

test('customer login shows a generic error and keeps the session empty on invalid credentials', async ({ page }) => {
  await page.route('**/api/v1/browser-auth/customer/login', route => route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ code: 'INVALID_CREDENTIALS' }) }));
  await page.goto(`${customerUrl}/login`);
  await page.getByLabel('Email', { exact: true }).fill('unknown@example.test');
  await page.getByLabel('Mật khẩu').fill('wrong-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Thông tin đăng nhập không hợp lệ.');
  await expect(page.getByRole('heading', { name: 'Xin chào khách hàng' })).toHaveCount(0);
});

test('customer portal rejects an unexpected Admin session response', async ({ page }) => {
  await page.route('**/api/v1/browser-auth/customer/login', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { ...tokenPayload('admin-access').data, user: { accountType: 'ADMIN', status: 'ACTIVE' } } }) }));
  await page.goto(`${customerUrl}/login`);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Tài khoản này không phải tài khoản khách đang hoạt động.');
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
