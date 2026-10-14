import { expect, test } from '@playwright/test';

const contact = process.env.SHUTTLEBOOK_ADMIN_TEST_CONTACT;
const password = process.env.SHUTTLEBOOK_ADMIN_TEST_PASSWORD;
const api = process.env.SHUTTLEBOOK_TEST_API_URL ?? 'http://localhost:5080';
test.skip(process.env.SHUTTLEBOOK_ADMIN_E2E_REAL !== '1' || !contact || !password,
  'Requires a disposable migrated PostgreSQL database, local API and test Admin credentials.');
test.use({ trace: 'off' });

test('admin portal uses the real API and clears its session on logout and reload', async ({ page }) => {
  const ready = await fetch(`${api}/health/ready`);
  expect(ready.status).toBe(200);
  await page.route('**/*', route => {
    const origin = new URL(route.request().url()).origin;
    return origin === 'http://localhost:5175' || origin === new URL(api).origin
      ? route.continue() : route.abort();
  });
  const meResponses: number[] = [];
  page.on('response', response => {
    if (response.url().endsWith('/api/v1/admin-auth/me')) meResponses.push(response.status());
  });

  await page.goto('http://localhost:5175/');
  await page.getByLabel('Email', { exact: true }).fill(contact!);
  await page.getByLabel('Mật khẩu').fill(password!);
  // Preserve the original five-second login-to-render budget while identifying
  // whether a failure happened before the HTTP response or while rendering.
  const loginStarted = Date.now();
  const loginTimestamp = new Date(loginStarted).toISOString();
  try {
    const [response] = await Promise.all([
      page.waitForResponse(result => result.url() === `${api}/api/v1/admin-auth/login` &&
        result.request().method() === 'POST', { timeout: 5_000 }),
      page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(),
    ]);
    const problem = response.ok() ? null : await response.json().catch(() => null) as { code?: unknown } | null;
    const code = typeof problem?.code === 'string' && /^[A-Z0-9_]{1,64}$/.test(problem.code) ? problem.code : null;
    console.info(JSON.stringify({ operation: 'admin-login-http', timestamp: loginTimestamp,
      elapsedMs: Date.now() - loginStarted, status: response.status(), code }));
    expect(response.status()).toBe(200);
  } catch (failure) {
    console.info(JSON.stringify({ operation: 'admin-login-http-failed', timestamp: loginTimestamp,
      elapsedMs: Date.now() - loginStarted }));
    throw failure;
  }
  try {
    await expect(page.getByRole('heading', { name: 'Hồ sơ chờ duyệt' })).toBeVisible({
      timeout: Math.max(1, 5_000 - (Date.now() - loginStarted)),
    });
    console.info(JSON.stringify({ operation: 'admin-login-render', timestamp: loginTimestamp,
      elapsedMs: Date.now() - loginStarted, rendered: true }));
  } catch (failure) {
    console.info(JSON.stringify({ operation: 'admin-login-render', timestamp: loginTimestamp,
      elapsedMs: Date.now() - loginStarted, rendered: false }));
    throw failure;
  }
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
  await expect(page.getByRole('heading', { name: 'Hồ sơ chờ duyệt' })).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
