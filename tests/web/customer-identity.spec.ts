import { expect, test } from '@playwright/test';

const customerUrl = 'http://localhost:5173';

function tokenPayload(refreshToken: string, expiresInSeconds: number) {
  return {
    data: {
      tokenType: 'Bearer',
      accessToken: `access-${refreshToken}`,
      refreshToken,
      expiresInSeconds,
      user: { id: '00000000-0000-7000-8000-000000000001', accountType: 'CUSTOMER', status: 'ACTIVE' },
    },
    traceId: 'customer-web-test',
  };
}

test('customer can register, verify, log in, refresh and log out without browser storage', async ({ page }) => {
  const requests: { path: string; body: Record<string, string>; authorization: string | undefined }[] = [];
  await page.route('**/api/v1/auth/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const body = request.postDataJSON() as Record<string, string>;
    requests.push({ path, body, authorization: request.headers().authorization });
    if (path.endsWith('/register') || path.endsWith('/verification-resend')) {
      await route.fulfill({ status: 202, contentType: 'application/json', body: JSON.stringify({ data: { verificationRequired: true } }) });
    } else if (path.endsWith('/verify-contact')) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { verified: true } }) });
    } else if (path.endsWith('/login')) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('first-refresh', 1)) });
    } else if (path.endsWith('/refresh')) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('rotated-refresh', 600)) });
    } else if (path.endsWith('/logout')) {
      await route.fulfill({ status: 204 });
    } else await route.abort();
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
  await expect(page.getByRole('heading', { name: 'Xin chào khách hàng' })).toBeVisible();
  await expect.poll(() => requests.filter(request => request.path.endsWith('/refresh')).length).toBe(1);
  expect(requests.find(request => request.path.endsWith('/refresh'))?.body.refreshToken).toBe('first-refresh');

  const persisted = await page.evaluate(() => ({
    local: Object.keys(localStorage),
    session: Object.keys(sessionStorage),
  }));
  expect(persisted).toEqual({ local: [], session: [] });

  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('heading', { name: 'Xin chào khách hàng' })).toBeHidden();
  await expect.poll(() => requests.filter(request => request.path.endsWith('/logout')).length).toBe(1);
  const logout = requests.find(request => request.path.endsWith('/logout'));
  expect(logout?.authorization).toBe('Bearer access-rotated-refresh');
  expect(logout?.body.refreshToken).toBe('rotated-refresh');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
});

test('customer login shows a generic error and keeps the session empty on invalid credentials', async ({ page }) => {
  await page.route('**/api/v1/auth/login', async route => {
    await route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ code: 'INVALID_CREDENTIALS' }) });
  });
  await page.goto(`${customerUrl}/login`);
  await page.getByLabel('Email', { exact: true }).fill('unknown@example.test');
  await page.getByLabel('Mật khẩu').fill('wrong-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Thông tin đăng nhập không hợp lệ.');
  await expect(page.getByRole('heading', { name: 'Xin chào khách hàng' })).toHaveCount(0);
});

test('customer portal rejects an unexpected Admin session response', async ({ page }) => {
  await page.route('**/api/v1/auth/login', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ data: { ...tokenPayload('admin-refresh', 600).data,
      user: { accountType: 'ADMIN', status: 'ACTIVE' } } }),
  }));
  await page.goto(`${customerUrl}/login`);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('status')).toHaveText('Tài khoản này không phải tài khoản khách đang hoạt động.');
  await expect(page.getByRole('heading', { name: 'Xin chào khách hàng' })).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
