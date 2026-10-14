import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

for (const portal of ['customer', 'partner'] as const) {
  test(`${portal} avatar opens an editable personal profile and hides booking code in notifications`, async ({ page }) => {
    let displayName: string | null = null;
    let loggedIn = portal === 'customer';
    const code = `BK261014${'a'.repeat(32)}`;
    await page.route('**/api/v1/**', async route => {
      const req = route.request(), path = new URL(req.url()).pathname;
      const reply = (data: unknown) => route.fulfill({ contentType: 'application/json',
        headers: { 'Access-Control-Allow-Origin': `http://localhost:${portal === 'customer' ? 5173 : 5174}`,
          'Access-Control-Allow-Credentials': 'true' }, body: JSON.stringify({ data }) });
      const fixtureSession = { tokenType: 'Bearer', accessToken: `${portal}-profile-fixture`, ...browserSessionFields(),
        user: { accountType: portal === 'customer' ? 'CUSTOMER' : 'VENUE_OPERATOR', status: 'ACTIVE' } };
      if (loggedIn && (path.endsWith(`/browser-auth/${portal}/restore`) || path.endsWith(`/browser-auth/${portal}/activity`)))
        return reply(fixtureSession);
      if (await browserSessionRoute(route, page)) return;
      if (path.endsWith(`/browser-auth/${portal}/login`) || portal === 'partner' && path.endsWith('/partner/login')) {
        loggedIn = true; return reply(fixtureSession);
      }
      if (path === '/api/v1/auth/profile') {
        if (req.method() === 'PUT') displayName = req.postDataJSON().displayName;
        return reply({ id: 'profile-test', displayName, contactType: 'email', contact: `${portal}@example.test` });
      }
      if (path === '/api/v1/venues') return reply({ items: [], nextCursor: null });
      if (path === '/api/v1/me/notifications' || path === '/api/v1/me/notifications/')
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: [{ id: '019a1234-1111-7111-8111-000000000001',
          title: 'Cập nhật đơn', body: `${code} · Cơ sở A · 08:00`, readAt: null, createdAt: '2026-10-14T00:00:00Z' }], unreadCount: 1, nextCursor: null }) });
      if (path === '/api/v1/partner-onboarding/businesses') return reply([]);
      return reply({});
    });
    if (portal === 'customer') {
      await page.goto('http://localhost:5173/venues');
      await expect(page.getByText('Đã đăng nhập')).toBeVisible();
      await (test.info().project.name === 'mobile' ? page.getByRole('link', { name: 'Hồ sơ', exact: true })
        : page.getByRole('link', { name: 'Mở thông tin cá nhân' })).dispatchEvent('click');
      await expect(page).toHaveURL(/\/me\/profile/);
    } else {
      await page.goto('http://localhost:5174/#/overview');
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
      await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
      await page.getByLabel('Mật khẩu').fill('Fixture-password-2026!');
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
      if (test.info().project.name === 'mobile') await page.getByRole('button', { name: 'Menu' }).click();
      await page.getByRole('button', { name: 'Mở thông tin cá nhân' }).click();
    }
    await expect(page.locator('.account-profile').getByRole('heading', { name: 'Thông tin cá nhân' })).toBeVisible();
    await expect(page.getByLabel('Email đăng nhập')).toHaveValue(`${portal}@example.test`);
    await expect(page.getByLabel('Email đăng nhập')).toHaveAttribute('readonly');
    await page.locator('.account-profile').getByLabel('Tên hiển thị').fill('Tên thử nghiệm');
    await page.getByRole('button', { name: 'Lưu thông tin' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Đã lưu thông tin cá nhân.' })).toBeVisible();
    if (portal === 'customer' && test.info().project.name !== 'mobile')
      await expect(page.getByRole('link', { name: 'Mở thông tin cá nhân' })).toContainText('Tên thử nghiệm');
    await page.reload();
    await expect(page.locator('.account-profile').getByLabel('Tên hiển thị')).toHaveValue('Tên thử nghiệm');
    if (portal === 'customer' && test.info().project.name !== 'mobile')
      await expect(page.getByRole('link', { name: 'Mở thông tin cá nhân' })).toContainText('Tên thử nghiệm');
    if (portal === 'partner' && test.info().project.name === 'mobile')
      await page.getByRole('button', { name: 'Menu' }).click();
    if (portal === 'customer') {
      await page.getByRole('link', { name: 'Thông báo' }).click();
      await expect(page.getByText(code)).toHaveCount(0);
      await expect(page.getByText('Đơn đặt sân · Cơ sở A · 08:00')).toBeVisible();
    } else {
      await page.getByRole('link', { name: 'Thông báo' }).click();
      await expect(page.getByText(code)).toHaveCount(0);
      await expect(page.getByText('Đơn đặt sân · Cơ sở A · 08:00')).toBeVisible();
    }
  });
}
