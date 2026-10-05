import { expect, test } from '@playwright/test';

test('partner can register, verify, log in and log out without persisting tokens in the browser', async ({ page }) => {
  const requests: { path: string; body: unknown }[] = [];
  await page.route('http://localhost:5080/api/v1/**', async route => {
    const request = route.request();
    const corsHeaders = { 'Access-Control-Allow-Origin': 'http://localhost:5174',
      'Access-Control-Allow-Methods': 'POST, OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type, Authorization' };
    if (request.method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: corsHeaders, body: '' });
      return;
    }
    const path = new URL(request.url()).pathname;
    const body = request.postDataJSON();
    requests.push({ path, body });
    if (path === '/api/v1/partner-auth/register' || path === '/api/v1/partner-auth/verification-resend') {
      await route.fulfill({ status: 202, contentType: 'application/json', headers: corsHeaders,
        body: JSON.stringify({ data: { verificationRequired: true } }) });
    } else if (path === '/api/v1/partner-auth/verify') {
      await route.fulfill({ status: 200, contentType: 'application/json', headers: corsHeaders,
        body: JSON.stringify({ data: { verified: true } }) });
    } else if (path === '/api/v1/auth/login') {
      await route.fulfill({ status: 200, contentType: 'application/json', headers: corsHeaders, body: JSON.stringify({
        data: { accessToken: 'test-access-token', refreshToken: 'test-refresh-token',
          user: { accountType: 'VENUE_OPERATOR', status: 'PENDING_ONBOARDING' } }
      }) });
    } else if (path === '/api/v1/auth/logout') {
      expect(request.headers().authorization).toBe('Bearer test-access-token');
      expect(body).toEqual({ refreshToken: 'test-refresh-token' });
      await route.fulfill({ status: 204, headers: corsHeaders, body: '' });
    } else {
      await route.fulfill({ status: 404, body: '' });
    }
  });

  await page.goto('http://localhost:5174');
  await page.getByRole('textbox', { name: 'Email' }).fill('partner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Example-password-2026!');
  await page.getByLabel('Nhập lại mật khẩu').fill('Example-password-2026!');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Xác minh liên hệ' })).toBeVisible();
  await page.getByLabel('Mã xác minh 6 chữ số').fill('123456');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập chủ sân' })).toBeVisible();
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Example-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Hồ sơ chủ sân' })).toBeVisible();
  await expect(page.getByText('Phần khai báo sân sẽ có trong F02.')).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập chủ sân' })).toBeVisible();
  expect(requests.map(request => request.path)).toEqual([
    '/api/v1/partner-auth/register', '/api/v1/partner-auth/verify',
    '/api/v1/auth/login', '/api/v1/auth/logout'
  ]);
});

test('partner registration catches mismatched passwords before making an API request', async ({ page }) => {
  let requested = false;
  await page.route('http://localhost:5080/api/v1/**', async route => {
    requested = true;
    await route.fulfill({ status: 500, body: '' });
  });
  await page.goto('http://localhost:5174');
  await page.getByRole('textbox', { name: 'Email' }).fill('partner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Example-password-2026!');
  await page.getByLabel('Nhập lại mật khẩu').fill('Different-password-2026!');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Mật khẩu nhập lại chưa khớp.');
  expect(requested).toBe(false);
});

test('partner portal rejects an unexpected Admin session response', async ({ page }) => {
  await page.route('http://localhost:5080/api/v1/auth/login', async route => {
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174',
      'Access-Control-Allow-Methods': 'POST, OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type' };
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers });
    } else {
      await route.fulfill({ status: 200, contentType: 'application/json', headers,
        body: JSON.stringify({ data: { accessToken: 'admin-access', refreshToken: 'admin-refresh',
          user: { accountType: 'ADMIN', status: 'ACTIVE' } } }) });
    }
  });
  await page.goto('http://localhost:5174');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByRole('textbox', { name: 'Email' }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Tài khoản này không thuộc cổng chủ sân.');
  await expect(page.getByRole('heading', { name: 'Hồ sơ chủ sân' })).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
