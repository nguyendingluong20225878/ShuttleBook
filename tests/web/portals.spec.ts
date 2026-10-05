import { expect, test } from '@playwright/test';

const portals = [
  { port: 5173, title: 'ShuttleBook — Khách đặt sân', heading: 'ShuttleBook' },
  { port: 5174, title: 'ShuttleBook — Đối tác', heading: 'Chào chủ sân.' },
  { port: 5175, title: 'ShuttleBook — Quản trị', heading: 'Quản trị ShuttleBook.' },
];

for (const portal of portals) {
  test(`portal ${portal.port} renders its independent production build`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const response = await page.goto(`http://localhost:${portal.port}`);
    expect(response?.ok()).toBeTruthy();
    await expect(page).toHaveTitle(portal.title);
    await expect(page.getByRole('heading', { level: 1, name: portal.heading })).toBeVisible();
    if (portal.port === 5175) {
      await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible();
    } else {
      await expect(page.getByRole('button', { name: 'Đăng ký' }).first()).toBeVisible();
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
    expect(errors).toEqual([]);
  });
}
