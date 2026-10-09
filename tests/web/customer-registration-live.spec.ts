import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

// This suite uses the local API, PostgreSQL and Mailpit. Keep it opt-in so the
// ordinary portal UI suite remains usable without identity infrastructure.
test.skip(process.env.SHUTTLEBOOK_E2E_REAL !== '1', 'Set SHUTTLEBOOK_E2E_REAL=1 with local API, PostgreSQL and Mailpit running.');
test.use({ trace: 'off' });

const mailpit = 'http://127.0.0.1:8025';
const api = process.env.SHUTTLEBOOK_TEST_API_URL ?? 'http://localhost:5080';

type MessageList = { messages: { ID: string; To: { Address: string }[] }[] };
type Message = { Text: string };

async function newestCode(contact: string, exceptId?: string): Promise<{ id: string; code: string } | null> {
  const response = await fetch(`${mailpit}/api/v1/messages`);
  if (!response.ok) throw new Error('Mailpit unavailable');
  const list = await response.json() as MessageList;
  const entry = list.messages.find(message => message.ID !== exceptId && message.To.some(to => to.Address === contact));
  if (!entry) return null;
  const detailResponse = await fetch(`${mailpit}/api/v1/message/${entry.ID}`);
  if (!detailResponse.ok) throw new Error('Mailpit message unavailable');
  const detail = await detailResponse.json() as Message;
  const code = detail.Text.match(/\b\d{6}\b/)?.[0];
  if (!code) throw new Error('Verification email lacks a six-digit code');
  return { id: entry.ID, code };
}

test('customer registration works through browser, API, PostgreSQL and Mailpit', async ({ page }) => {
  test.setTimeout(120_000);
  const ready = await fetch(`${api}/health/ready`);
  expect(ready.status).toBe(200);
  await page.route('**/*', route => {
    const url = new URL(route.request().url()); const origin = url.origin;
    if (url.pathname.startsWith('/api/v1/') && origin !== new URL(api).origin)
      throw new Error(`Browser API origin ${origin} differs from test API ${new URL(api).origin}.`);
    return origin === 'http://localhost:5173' || origin === new URL(api).origin
      ? route.continue() : route.abort();
  });
  const contact = `f011-live-${randomUUID()}@example.test`;
  const password = `F011-${randomUUID()}!Aa1`;
  const registerRequests: string[] = [];
  page.on('request', request => {
    if (request.url().endsWith('/api/v1/auth/register')) registerRequests.push(request.method());
  });

  await page.goto('http://localhost:5173/register');
  await page.getByLabel('Email', { exact: true }).fill(contact);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByLabel('Nhập lại mật khẩu').fill(`${password}different`);
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('status')).toContainText('chưa khớp');
  expect(registerRequests).toHaveLength(0);

  await page.getByLabel('Nhập lại mật khẩu').fill(password);
  const [registered] = await Promise.all([
    page.waitForResponse(response => response.url().endsWith('/api/v1/auth/register') && response.request().method() === 'POST', { timeout: 20_000 }),
    page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click(),
  ]);
  expect(registered.status()).toBe(202);
  await expect(page.getByRole('heading', { name: 'Xác minh tài khoản khách' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Mailpit' })).toHaveCount(0);
  expect(registerRequests).toHaveLength(1);
  await expect.poll(async () => Boolean(await newestCode(contact))).toBe(true);
  const first = await newestCode(contact);
  expect(first).not.toBeNull();

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Xác minh tài khoản khách' })).toBeVisible();
  await page.getByLabel('Mã xác minh 6 chữ số').fill(first!.code === '000000' ? '000001' : '000000');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('không hợp lệ');

  const [resent] = await Promise.all([
    page.waitForResponse(response => response.url() === `${api}/api/v1/auth/verification-resend` && response.request().method() === 'POST', { timeout: 20_000 }),
    page.getByRole('button', { name: 'Gửi lại mã' }).click(),
  ]);
  expect(resent.status()).toBe(202);
  await expect(page.getByRole('status')).toContainText('mã mới đã được gửi');
  await expect.poll(async () => Boolean(await newestCode(contact, first!.id))).toBe(true);
  const second = await newestCode(contact, first!.id);
  expect(second).not.toBeNull();

  await page.getByLabel('Mã xác minh 6 chữ số').fill(first!.code);
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('không hợp lệ');
  await page.getByLabel('Mã xác minh 6 chữ số').fill(second!.code);
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('Xác minh thành công');

  await page.goBack();
  await expect(page.getByRole('heading', { name: 'Xác minh tài khoản khách' })).toBeVisible();
  await page.goForward();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Chọn cơ sở phù hợp với bạn' })).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('customer registration reports invalid contact and weak password', async ({ page }) => {
  const registerRequests: string[] = [];
  page.on('request', request => {
    if (request.url().endsWith('/api/v1/auth/register')) registerRequests.push(request.method());
  });
  await page.route('**/*', route => {
    const url = new URL(route.request().url()); const origin = url.origin;
    if (url.pathname.startsWith('/api/v1/') && origin !== new URL(api).origin)
      throw new Error(`Browser API origin ${origin} differs from test API ${new URL(api).origin}.`);
    return origin === 'http://localhost:5173' || origin === new URL(api).origin
      ? route.continue() : route.abort();
  });
  const contact = `f011-invalid-${randomUUID()}@example.test`;
  await page.goto('http://localhost:5173/register');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  expect(registerRequests).toHaveLength(0);
  await page.getByLabel('Email', { exact: true }).fill('not-an-email');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('F011-Valid-password-2026!');
  await page.getByLabel('Nhập lại mật khẩu').fill('F011-Valid-password-2026!');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByLabel('Email', { exact: true })).toBeFocused();
  expect(await page.getByLabel('Email', { exact: true }).evaluate(element => (element as HTMLInputElement).validity.typeMismatch)).toBe(true);
  expect(registerRequests).toHaveLength(0);
  await page.getByLabel('Email', { exact: true }).fill(contact);
  await page.getByLabel('Mật khẩu', { exact: true }).fill('abcdefghijkl');
  await page.getByLabel('Nhập lại mật khẩu').fill('abcdefghijkl');
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(page.getByRole('status')).toContainText('Thông tin chưa hợp lệ');
  expect(registerRequests).toHaveLength(1);
  await expect(page.getByRole('heading', { name: 'Tạo tài khoản khách' })).toBeVisible();
});
