import { expect, test } from '@playwright/test';

const venueId = '0199f040-0000-7000-8000-000000000001';
const courtA = '0199f040-0000-7000-8000-000000000002';
const courtB = '0199f040-0000-7000-8000-000000000003';

test('guest searches and reads the court-by-time grid without creating a booking', async ({ page }) => {
  let bookingRequests = 0;
  await page.route('**/api/v1/bookings**', route => { bookingRequests++; return route.abort(); });
  await page.route('https://cdn.maptiler.com/**', route => route.abort());
  await page.route('**/api/v1/venues**', route => {
    const url = new URL(route.request().url());
    const json = (data: object) => route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ data, traceId: 'f04-ui-test' }) });
    if (url.pathname.endsWith('/availability')) {
      const date = url.searchParams.get('date');
      return json({ venueId, date, timezone: 'Asia/Ho_Chi_Minh', generatedAt: new Date().toISOString(), stepMinutes: 30,
        courts: [
          { courtId: courtA, name: 'Sân 1', bookingBlockMinutes: 30, minimumBookingMinutes: 60, holdMinutes: 20,
            slots: [
              slot('17:00', '17:30', 'AVAILABLE', 100000), slot('17:30', '18:00', 'AVAILABLE', 150000),
              slot('18:00', '18:30', 'RESERVED', 150000), slot('18:30', '19:00', 'AVAILABLE', 100000),
            ] },
          { courtId: courtB, name: 'Sân 2', bookingBlockMinutes: 60, minimumBookingMinutes: 60, holdMinutes: 20,
            slots: [slot('18:00', '18:30', 'AVAILABLE', 80000), slot('18:30', '19:00', 'AVAILABLE', 80000),
              slot('19:00', '19:30', 'NO_PRICE', null)] },
        ] });
    }
    if (url.pathname === `/api/v1/venues/${venueId}`) return json({ id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội',
      contact: 'Liên hệ cơ sở', latitude: 21.0278, longitude: 105.8342, timezone: 'Asia/Ho_Chi_Minh',
      imageUrl: null, courts: [{ id: courtA, name: 'Sân 1', bookingBlockMinutes: 30, minimumBookingMinutes: 60,
        holdMinutes: 20 }, { id: courtB, name: 'Sân 2', bookingBlockMinutes: 60, minimumBookingMinutes: 60,
        holdMinutes: 20 }] });
    if (url.pathname === '/api/v1/venues') return json({ items: [{ id: venueId, name: 'Hoàng Cầu',
      address: 'Hà Nội', latitude: 21.0278, longitude: 105.8342, imageUrl: null }], nextCursor: null });
    return route.abort();
  });

  await page.goto('http://localhost:5173/venues');
  await expect(page.getByRole('heading', { name: 'Chọn cơ sở phù hợp với bạn' })).toBeVisible();
  await page.getByLabel('Tên sân hoặc địa chỉ').fill('Hoàng Cầu');
  await page.getByRole('button', { name: 'Tìm sân' }).click();
  await expect(page.getByRole('link', { name: 'Xem lịch các sân' })).toBeVisible();
  await page.getByRole('link', { name: 'Xem lịch các sân' }).click();
  await expect(page.getByRole('heading', { name: 'Lịch theo sân và giờ' })).toBeVisible();
  await expect(page.getByRole('columnheader', { name: '17:00' })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 1/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 2/ })).toBeVisible();
  await expect(page.locator('.slot-label').filter({ hasText: 'Đã kín' })).toBeVisible();
  await expect(page.locator('.slot-label').filter({ hasText: 'Chưa có giá' })).toBeVisible();
  await page.getByRole('button', { name: /Sân 1, 17:00 đến 17:30/ }).click();
  await page.getByRole('button', { name: /Sân 1, 17:30 đến 18:00/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 250.000đ' })).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('f04-grid.png'), fullPage: true });
  await page.getByLabel('Xem sân').selectOption(courtB);
  await expect(page.getByRole('rowheader', { name: /Sân 1/ })).toHaveCount(0);
  await expect(page.getByRole('rowheader', { name: /Sân 2/ })).toBeVisible();
  await page.goBack();
  await expect(page.getByRole('rowheader', { name: /Sân 1/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 2/ })).toBeVisible();
  const overflow = await page.locator('.schedule-scroll').evaluate(element => element.scrollWidth > element.clientWidth);
  if ((page.viewportSize()?.width ?? 1280) < 630) expect(overflow).toBeTruthy();
  expect(bookingRequests).toBe(0);
});

test('guest can still search by name after location permission is denied', async ({ page }) => {
  await page.addInitScript(() => Object.defineProperty(navigator, 'geolocation', { value: {
    getCurrentPosition: (_success: unknown, failure: (reason: { code: number }) => void) => failure({ code: 1 }),
  } }));
  await page.route('**/api/v1/venues**', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ data: { items: [{ id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội',
      latitude: 21.0278, longitude: 105.8342, imageUrl: null }], nextCursor: null }, traceId: 'f04-fallback' }) }));
  await page.goto('http://localhost:5173/venues');
  await page.getByRole('button', { name: 'Dùng vị trí của tôi' }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'chưa cấp quyền vị trí' })).toBeVisible();
  await page.getByLabel('Tên sân hoặc địa chỉ').fill('Hoàng Cầu');
  await page.getByRole('button', { name: 'Tìm sân' }).click();
  await expect(page.getByRole('heading', { name: 'Hoàng Cầu' })).toBeVisible();
});

test('guest location searches the selected PostGIS radius', async ({ page, context }) => {
  await context.grantPermissions(['geolocation'], { origin: 'http://localhost:5173' });
  await context.setGeolocation({ latitude: 21.0278, longitude: 105.8342 });
  let nearbyUrl = '';
  await page.route('**/api/v1/venues**', route => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/nearby')) nearbyUrl = url.href;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      data: { items: [{ id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội',
        latitude: 21.0278, longitude: 105.8342, distanceMeters: 0, imageUrl: null }], nextCursor: null },
      traceId: 'f04-nearby-ui',
    }) });
  });
  await page.goto('http://localhost:5173/venues');
  await page.getByLabel('Bán kính').selectOption('10000');
  await page.getByRole('button', { name: 'Dùng vị trí của tôi' }).click();
  await expect(page.getByRole('heading', { name: 'Sân gần khu vực đã chọn' })).toBeVisible();
  await expect(page.getByText('Cách khoảng 0.0 km')).toBeVisible();
  const params = new URL(nearbyUrl).searchParams;
  expect(params.get('latitude')).toBe('21.0278');
  expect(params.get('longitude')).toBe('105.8342');
  expect(params.get('radiusMeters')).toBe('10000');
});

function slot(startsAt: string, endsAt: string, status: string, pricePerSlot: number | null) {
  return { startsAt, endsAt, startsAtUtc: '2026-10-12T10:00:00Z', endsAtUtc: '2026-10-12T10:30:00Z',
    status, pricePerSlot };
}
