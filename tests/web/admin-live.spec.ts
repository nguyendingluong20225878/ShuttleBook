import { expect, test } from '@playwright/test';

const contact = process.env.SHUTTLEBOOK_ADMIN_TEST_CONTACT;
const password = process.env.SHUTTLEBOOK_ADMIN_TEST_PASSWORD;
test.skip(process.env.SHUTTLEBOOK_ADMIN_E2E_REAL !== '1' || !contact || !password,
  'Requires a disposable migrated PostgreSQL database, local API and test Admin credentials.');
test.use({ trace: 'off' });

test('admin portal uses the real API and clears its session on logout and reload', async ({ page }) => {
  const ready = await fetch('http://localhost:5080/health/ready');
  expect(ready.status).toBe(200);
  await page.route('**/*', route => {
    const origin = new URL(route.request().url()).origin;
    return origin === 'http://localhost:5175' || origin === 'http://localhost:5080'
      ? route.continue() : route.abort();
  });
  const meResponses: number[] = [];
  page.on('response', response => {
    if (response.url().endsWith('/api/v1/admin-auth/me')) meResponses.push(response.status());
  });

  await page.goto('http://localhost:5175/');
  await page.getByLabel('Email', { exact: true }).fill(contact!);
  await page.getByLabel('Mật khẩu').fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Chưa có module quản trị.')).toBeVisible();
  expect(meResponses).toContain(200);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);

  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await page.goto('http://localhost:5175/session-check');
  await page.goBack();
  await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await page.goForward();
  await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await expect(page.getByText('Chưa có module quản trị.')).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
