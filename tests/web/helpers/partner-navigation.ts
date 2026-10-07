import { Page } from '@playwright/test';

export async function openPartnerPage(page: Page, name: string) {
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  if (await menu.isVisible() && await menu.getAttribute('aria-expanded') !== 'true') await menu.click();
  await page.getByRole('navigation', { name: 'Quản lý đối tác' }).getByRole('link', { name, exact: true }).click();
}

export async function partnerLogout(page: Page) {
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  if (await menu.isVisible() && await menu.getAttribute('aria-expanded') !== 'true') await menu.click();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
}
