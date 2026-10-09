import { expect, test } from '@playwright/test';
import { browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => { if (!(await browserSessionRoute(route, page))) await route.fallback(); });
});

const point = { latitude: 21.0278, longitude: 105.8342 };
const venue = { id: '0199f090-0000-7000-8000-000000000001', name: 'Cơ sở Hoàng Cầu', address: '31 ngõ 16 Hoàng Cầu, Hà Nội', ...point, imageUrl: null };

test('customer shows current distance without a map, retains it after text search and fits the full results width', async ({ page, context }, info) => {
  await context.grantPermissions(['geolocation'], { origin: 'http://localhost:5173' });
  await context.setGeolocation(point);
  let nearbyUrl = ''; let mapRequests = 0;
  page.on('request', request => { if (/cdn\.maptiler\.com|api\.maptiler\.com\/maps\//.test(request.url())) mapRequests++; });
  await page.route('**/api/v1/venues**', route => {
    const url = new URL(route.request().url()); const nearby = url.pathname.endsWith('/nearby');
    if (nearby) nearbyUrl = url.href;
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: {
      items: [{ ...venue, ...(nearby ? { distanceMeters: 1200 } : {}) }], nextCursor: null,
    } }) });
  });
  await page.goto('http://localhost:5173/venues');
  await expect(page.locator('.venue-card')).toBeVisible();
  await expect(page.locator('.distance')).toHaveCount(0);
  await expect(page.locator('.venue-map, .discovery-map-panel')).toHaveCount(0);
  await page.getByRole('button', { name: 'Dùng vị trí của tôi', exact: true }).click();
  await expect(page.getByText('Cách vị trí của bạn khoảng 1,2 km', { exact: true })).toBeVisible();
  expect(new URL(nearbyUrl).searchParams.get('radiusMeters')).toBe('5000');
  await page.screenshot({ path: info.outputPath('customer-distance.png'), fullPage: true });
  await page.getByLabel('Tên sân hoặc địa chỉ').fill('Hoàng Cầu');
  await page.getByRole('button', { name: 'Tìm sân', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Danh sách cơ sở', exact: true })).toBeVisible();
  await expect(page.getByText('Cách vị trí của bạn khoảng 0 m', { exact: true })).toBeVisible();
  const results = await page.locator('.results-layout').boundingBox();
  const list = await page.getByRole('region', { name: 'Danh sách cơ sở', exact: true }).boundingBox();
  expect(Math.abs(results!.width - list!.width)).toBeLessThan(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(mapRequests).toBe(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('selected-area distance is not labeled as the current location', async ({ page }) => {
  await page.route('**/api.maptiler.com/geocoding/**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ features: [
    { place_name: 'Hoàng Cầu, Hà Nội', center: [point.longitude, point.latitude] },
  ] }) }));
  await page.route('**/api/v1/venues**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: {
    items: [{ ...venue, distanceMeters: 2000 }], nextCursor: null,
  } }) }));
  await page.goto('http://localhost:5173/venues');
  const area = page.getByLabel('Nhập khu vực tìm kiếm', { exact: true });
  test.skip(await area.count() === 0, 'Area autocomplete requires an existing MapTiler key; no map is rendered.');
  await expect(page.locator('.distance')).toHaveCount(0);
  await area.fill('Hoàng Cầu');
  await page.getByRole('button', { name: 'Hoàng Cầu, Hà Nội', exact: true }).click();
  await expect(page.getByText('Cách khu vực đã chọn khoảng 2,0 km', { exact: true })).toBeVisible();
  await expect(page.locator('.distance')).not.toContainText('vị trí của bạn');
});

test('delayed old load-more cannot mix venues or reset controls after a new text search', async ({ page }) => {
  let release: (() => void) | undefined; let started: (() => void) | undefined;
  const blocked = new Promise<void>(resolve => { release = resolve; });
  const began = new Promise<void>(resolve => { started = resolve; });
  await page.route('**/api/v1/venues**', async route => {
    const url = new URL(route.request().url());
    const fresh = url.searchParams.get('q') === 'Mới';
    const more = url.searchParams.has('cursor');
    if (more) { started?.(); await blocked; }
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data: {
      items: [{ ...venue, id: fresh ? 'new' : more ? 'old-more' : 'old', name: fresh ? 'Cơ sở mới' : more ? 'Cơ sở cũ trang hai' : 'Cơ sở cũ' }],
      nextCursor: fresh || more ? null : 'old-cursor',
    } }) });
  });
  await page.goto('http://localhost:5173/venues');
  await page.getByRole('button', { name: 'Xem thêm cơ sở', exact: true }).click(); await began;
  await page.getByLabel('Tên sân hoặc địa chỉ').fill('Mới');
  await page.getByRole('button', { name: 'Tìm sân', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Cơ sở mới', exact: true })).toBeVisible(); release?.();
  await expect(page.getByRole('heading', { name: 'Cơ sở cũ trang hai', exact: true })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Cơ sở cũ', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Xem thêm cơ sở', exact: true })).toHaveCount(0);
});
