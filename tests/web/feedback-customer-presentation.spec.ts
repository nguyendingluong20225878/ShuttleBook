import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

const venueId = '0199f040-0000-7000-8000-000000000001';
const courtId = '0199f040-0000-7000-8000-000000000002';
const bookingId = '0199f050-0000-7000-8000-000000000003';
const photo = '/api/v1/venues/test/photo';
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');

for (const image of ['missing', 'ready', 'failed'] as const) {
  test(`venue shows actual public information and ${image} photo without claiming platform ownership`, async ({ page }) => {
    await page.route('**/api/v1/**', async route => {
      if (await browserSessionRoute(route, page)) return;
      const url = new URL(route.request().url());
      if (url.pathname === photo) return image === 'ready'
        ? route.fulfill({ status: 200, contentType: 'image/png', body: png })
        : route.fulfill({ status: 404 });
      const courts = [1, 2].map(number => ({ id: `${courtId.slice(0, -1)}${number}`, name: String(number),
        bookingBlockMinutes: 60, minimumBookingMinutes: 120, holdMinutes: 20 }));
      const data = url.pathname.endsWith('/availability')
        ? { venueId, date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30, courts: [] }
        : { id: venueId, name: 'Hoàng Cầu · Cơ sở 2', address: '31 ngõ 167 Tây Sơn, Hà Nội', contact: '+84 947 587 664',
          timezone: 'Asia/Ho_Chi_Minh', latitude: 21, longitude: 105, imageUrl: image === 'missing' ? null : photo, courts };
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
    });
    await page.goto(`http://localhost:5173/venues/${venueId}`);
    const hero = page.locator('.venue-hero');
    await expect(hero.getByRole('heading', { name: 'Hoàng Cầu · Cơ sở 2' })).toBeVisible();
    await expect(hero).toContainText('31 ngõ 167 Tây Sơn, Hà Nội');
    await expect(hero).toContainText('2 sân đang hoạt động');
    await expect(hero.getByRole('link', { name: '+84 947 587 664', exact: true })).toHaveAttribute('href', 'tel:+84947587664');
    await expect(hero).not.toContainText('Cơ sở ShuttleBook');
    if (image === 'ready') {
      const actualPhoto = hero.getByRole('img', { name: 'Ảnh Hoàng Cầu · Cơ sở 2' });
      await expect(actualPhoto).toBeVisible();
      await expect.poll(() => actualPhoto.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBeGreaterThan(0);
      await expect(hero.locator('.image-placeholder')).toHaveCount(0);
    } else {
      await expect(hero.locator('.image-placeholder')).toHaveText('Chưa có ảnh cơ sở');
      await expect(hero.getByRole('img')).toHaveCount(0);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test('free-text contact remains readable without becoming an arbitrary link', async ({ page }) => {
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const url = new URL(route.request().url());
    const data = url.pathname.endsWith('/availability')
      ? { venueId, date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30, courts: [] }
      : { id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội', contact: 'Liên hệ tại quầy', latitude: 21, longitude: 105,
        timezone: 'Asia/Ho_Chi_Minh', imageUrl: null, courts: [] };
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
  });
  await page.goto(`http://localhost:5173/venues/${venueId}`);
  await expect(page.locator('.venue-hero')).toContainText('Liên hệ tại quầy');
  await expect(page.locator('.venue-hero a')).toHaveCount(0);
});

test('customer transfer keeps account and QR guidance without displaying legacy bank code', async ({ page }) => {
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const path = new URL(route.request().url()).pathname;
    const reply = (data: object) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
    if (path.endsWith('/browser-auth/customer/login')) return reply({ accessToken: 'presentation-test', ...browserSessionFields(),
      user: { accountType: 'CUSTOMER', status: 'ACTIVE' } });
    if (path === '/api/v1/notifications') return reply({ items: [], unreadCount: 0, nextCursor: null });
    if (path.endsWith('/qr')) return route.fulfill({ status: 200, contentType: 'image/png', body: png });
    if (path === `/api/v1/bookings/${bookingId}`) return reply({ bookingId, bookingNo: 'BK-FEEDBACK', status: 'AWAITING_TRANSFER',
      version: 1, venueName: 'Hoàng Cầu', courtName: '1', date: '2026-10-20', localStart: '18:00', localEnd: '20:00',
      timezone: 'Asia/Ho_Chi_Minh', amount: 400000, amountExact: '400000', slots: [],
      paymentDeadline: new Date(Date.now() + 20 * 60000).toISOString(), createdAt: new Date().toISOString(), expiredAt: null,
      payment: { status: 'AWAITING_TRANSFER', bankCode: 'LEGACY-BANK-SENTINEL', accountName: 'CHỦ CƠ SỞ HOÀNG CẦU',
        maskedAccountNumber: '****7890', qrUrl: `/api/v1/bookings/${bookingId}/qr`, transferContent: 'BK-FEEDBACK' } });
    return route.abort();
  });
  await page.goto(`http://localhost:5173/bookings/${bookingId}`);
  await page.getByLabel('Email', { exact: true }).fill('customer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Mã đơn: BK-FEEDBACK' })).toBeVisible();
  await expect(page.getByText('Chủ tài khoản: CHỦ CƠ SỞ HOÀNG CẦU', { exact: true })).toBeVisible();
  await expect(page.getByText('Số tài khoản: ****7890', { exact: true })).toBeVisible();
  await expect(page.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toBeVisible();
  await expect(page.locator('main')).not.toContainText('LEGACY-BANK-SENTINEL');
  await expect(page.getByText(/Ngân hàng:|Mã ngân hàng/)).toHaveCount(0);
});
