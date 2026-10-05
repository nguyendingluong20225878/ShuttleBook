import { expect, test } from '@playwright/test';

const adminUrl = 'http://localhost:5175';
const tokenPayload = (refreshToken: string, expiresInSeconds = 600, accountType = 'ADMIN') => ({
  data: { tokenType: 'Bearer', accessToken: `access-${refreshToken}`, refreshToken, expiresInSeconds,
    user: { accountType, status: 'ACTIVE' } }, traceId: 'admin-web-test',
});

test('admin login checks me, shows only the pending module, and clears memory on logout and reload', async ({ page }) => {
  const paths: string[] = [];
  await page.route('**/api/v1/**', async route => {
    const path = new URL(route.request().url()).pathname;
    paths.push(path);
    if (path === '/api/v1/admin-auth/login')
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('first')) });
    else if (path === '/api/v1/admin-auth/me')
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { accountType: 'ADMIN', status: 'ACTIVE' } }) });
    else if (path === '/api/v1/auth/logout') await route.fulfill({ status: 204 });
    else await route.abort();
  });
  await page.goto(adminUrl);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByText('Chưa có module quản trị.')).toBeVisible();
  expect(paths).toContain('/api/v1/admin-auth/me');
  expect(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })))
    .toEqual({ local: [], session: [] });
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  expect(paths).toContain('/api/v1/auth/logout');
});

test('admin login keeps non-admin response out and shows generic and rate limit errors', async ({ page }) => {
  let reply = 401;
  await page.route('**/api/v1/admin-auth/login', async route => {
    if (reply === 401) await route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ code: 'INVALID_CREDENTIALS' }) });
    else if (reply === 429) await route.fulfill({ status: 429,
      headers: { 'Retry-After': '120', 'Access-Control-Expose-Headers': 'Retry-After' }, body: '{}' });
    else await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('wrong-role', 600, 'CUSTOMER')) });
  });
  await page.goto(adminUrl);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByRole('status')).toHaveText('Thông tin đăng nhập không hợp lệ.');
  reply = 429;
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByRole('status')).toContainText('2 phút');
  reply = 200;
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  await expect(page.getByText('Chưa có module quản trị.')).toHaveCount(0);
});

test('admin login shows loading and a safe network error', async ({ page }) => {
  let release: (() => void) | undefined;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/v1/admin-auth/login', async route => {
    await pending;
    await route.abort();
  });
  await page.goto(adminUrl);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đang đăng nhập…' })).toBeDisabled();
  release?.();
  await expect(page.getByRole('status')).toHaveText('Không thể kết nối. Vui lòng thử lại.');
  await expect(page.getByText('Chưa có module quản trị.')).toHaveCount(0);
});

test('late refresh response cannot restore an Admin session after logout', async ({ page }) => {
  let releaseRefresh: (() => void) | undefined;
  let refreshStarted: (() => void) | undefined;
  const started = new Promise<void>(resolve => { refreshStarted = resolve; });
  let refreshFinished: (() => void) | undefined;
  const finished = new Promise<void>(resolve => { refreshFinished = resolve; });
  await page.route('**/api/v1/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/v1/admin-auth/login')
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('first', 1)) });
    else if (path === '/api/v1/admin-auth/me')
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { accountType: 'ADMIN', status: 'ACTIVE' } }) });
    else if (path === '/api/v1/auth/refresh') {
      refreshStarted?.();
      await new Promise<void>(resolve => { releaseRefresh = resolve; });
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tokenPayload('second')) });
      refreshFinished?.();
    } else if (path === '/api/v1/auth/logout') await route.fulfill({ status: 204 });
    else await route.abort();
  });
  await page.goto(adminUrl);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByText('Chưa có module quản trị.')).toBeVisible();
  await started;
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
  releaseRefresh?.();
  await finished;
  await expect(page.getByText('Chưa có module quản trị.')).toHaveCount(0);
});
