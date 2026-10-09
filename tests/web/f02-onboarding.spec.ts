import { expect, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});

test('partner onboarding shares one refresh across simultaneous protected reads', async ({ page }) => {
  let refreshCalls = 0;
  const successfulReads = new Set<string>();
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page, true)) return;
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Authorization,Content-Type',
      'Access-Control-Allow-Methods': 'GET,POST,OPTIONS' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    if (path === '/api/v1/browser-auth/partner/login') return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: { accessToken: 'expired-access', ...browserSessionFields(),
        user: { accountType: 'VENUE_OPERATOR', status: 'PENDING_ONBOARDING' } } }) });
    if (path === '/api/v1/browser-auth/partner/restore') {
      refreshCalls++;
      await new Promise(resolve => setTimeout(resolve, 50));
      return route.fulfill({ status: 200, headers, contentType: 'application/json',
        body: JSON.stringify({ data: { accessToken: 'new-access', ...browserSessionFields(), user: { accountType: 'VENUE_OPERATOR', status: 'PENDING_ONBOARDING' } } }) });
    }
    if (path === '/api/v1/partner-onboarding/businesses' || path === '/api/v1/me/notifications/') {
      if (request.headers().authorization !== 'Bearer new-access')
        return route.fulfill({ status: 401, headers, contentType: 'application/problem+json',
          body: JSON.stringify({ code: 'UNAUTHORIZED' }) });
      successfulReads.add(path);
      return route.fulfill({ status: 200, headers, contentType: 'application/json',
        body: JSON.stringify({ data: [] }) });
    }
    return route.fulfill({ status: 404, headers, body: '{}' });
  });
  await page.goto('http://localhost:5174');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByRole('textbox', { name: 'Email' }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Tạo doanh nghiệp' })).toBeVisible();
  await expect.poll(() => successfulReads.size).toBe(2);
  expect(refreshCalls).toBe(1);
  await expect(page.getByText('Phiên đăng nhập đã hết hạn.')).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('partner draft forms call the scoped onboarding API', async ({ page }) => {
  await page.addInitScript(() => {
    (window as unknown as { maptilersdk: unknown }).maptilersdk = {
      config: { apiKey: '' }, Map: class { setCenter() {} setZoom() {} remove() {} },
      Marker: class { setLngLat() { return this; } addTo() { return this; } remove() {} },
    };
  });
  const searchedAddresses: string[] = [];
  await page.route('https://api.maptiler.com/geocoding/**', route => {
    const url = new URL(route.request().url());
    searchedAddresses.push(decodeURIComponent(url.pathname.split('/').pop()!.replace(/\.json$/, '')));
    return route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ type: 'FeatureCollection', features: [{ type: 'Feature',
      place_name: '31 ngõ 16 Hoàng Cầu, Hà Nội',
      geometry: { type: 'Point', coordinates: [105.8236, 21.0186] }, center: [105.8236, 21.0186] }] }),
    });
  });
  const calls: string[] = [];
  let savedLocation: { address: string; latitude: number; longitude: number } | null = null;
  const businessId = '0199fc70-0000-7000-8000-000000000001';
  const venueId = '0199fc70-0000-7000-8000-000000000002';
  let business: { id: string; name: string; legalName: string; contact: string; status: string; version: number;
    venues: Array<{ id: string; name: string; address: string; contact: string; status: string; version: number;
      latitude: number; longitude: number; timezone: string; courts: Array<{ id: string; name: string;
        status: string; hours: object[]; prices: object[] }> }> } | null = null;
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page, true)) return;
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match',
      'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    calls.push(`${request.method()} ${path}`);
    if (path === '/api/v1/browser-auth/partner/login') return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: { accessToken: 'partner-access', ...browserSessionFields(),
        user: { accountType: 'VENUE_OPERATOR', status: 'PENDING_ONBOARDING' } } }) });
    if (path === '/api/v1/partner-onboarding/businesses' && request.method() === 'GET')
      return route.fulfill({ status: 200, headers, contentType: 'application/json',
        body: JSON.stringify({ data: business ? [{ id: business.id, name: business.name, status: business.status }] : [] }) });
    if (path === '/api/v1/partner-onboarding/businesses' && request.method() === 'POST') {
      const input = request.postDataJSON();
      business = { id: businessId, name: input.name, legalName: input.legalName, contact: input.contact,
        status: 'DRAFT', version: 1, venues: [] };
      return route.fulfill({ status: 201, headers, contentType: 'application/json', body: JSON.stringify({ data: business }) });
    }
    if (path === `/api/v1/partner-onboarding/businesses/${businessId}` && business)
      return route.fulfill({ status: 200, headers, contentType: 'application/json', body: JSON.stringify({ data: business }) });
    if (path === `/api/v1/partner-onboarding/businesses/${businessId}/venues` && request.method() === 'POST' && business) {
      const input = request.postDataJSON();
      savedLocation = { address: input.address, latitude: input.latitude, longitude: input.longitude };
      business.venues.push({ id: venueId, name: input.name, address: input.address,
        contact: input.contact, status: 'DRAFT', version: 1,
        latitude: input.latitude, longitude: input.longitude, timezone: input.timezone, courts: [] });
      return route.fulfill({ status: 201, headers, contentType: 'application/json', body: JSON.stringify({ data: { id: venueId } }) });
    }
    return route.fulfill({ status: 404, headers, body: '{}' });
  });
  await page.goto('http://localhost:5174');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByRole('textbox', { name: 'Email' }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  const create = page.getByRole('heading', { name: 'Tạo doanh nghiệp' }).locator('..');
  await create.getByLabel('Tên hiển thị').fill('Sân Cầu Lông A');
  await create.getByLabel('Tên pháp lý').fill('Công ty A');
  await create.getByLabel('Liên hệ').fill('owner@example.test');
  await create.getByRole('button', { name: 'Tạo hồ sơ nháp' }).click();
  await expect(page.getByRole('heading', { name: 'Thêm cơ sở' })).toBeVisible();
  const venue = page.getByRole('heading', { name: 'Thêm cơ sở' }).locator('..');
  await venue.getByLabel('Tên', { exact: true }).fill('Cơ sở Quận 1');
  await venue.getByLabel('Liên hệ cơ sở').fill('Test venue contact');
  await venue.getByRole('button', { name: 'Lưu cơ sở' }).click();
  await expect(page.getByText('Chọn và xác nhận địa chỉ trên MapTiler.')).toBeVisible();
  await venue.getByPlaceholder('Nhập địa chỉ, ví dụ: 31 ngõ 16 Hoàng Cầu - Hà Nội').fill('31 ngõ 16 Hoàng Cầu - Hà Nội');
  const suggestion = page.getByRole('button', { name: '31 ngõ 16 Hoàng Cầu, Hà Nội' });
  await expect(suggestion).toBeVisible();
  await suggestion.click();
  await venue.getByRole('button', { name: 'Xác nhận vị trí này' }).click();
  await venue.getByRole('button', { name: 'Lưu cơ sở' }).click();
  await expect(page.getByRole('heading', { name: 'Cơ sở Quận 1 · Hồ sơ nháp' })).toBeVisible();
  expect(calls).toContain('POST /api/v1/partner-onboarding/businesses');
  expect(calls).toContain(`POST /api/v1/partner-onboarding/businesses/${businessId}/venues`);
  expect(searchedAddresses).toContain('31 ngõ 16 Hoàng Cầu - Hà Nội');
  expect(savedLocation).toEqual({ address: '31 ngõ 16 Hoàng Cầu, Hà Nội', latitude: 21.0186, longitude: 105.8236 });
});

test('Admin can review a submitted profile and send an approval decision', async ({ page }) => {
  const id = '0199fc70-0000-7000-8000-000000000003';
  let decided = false;
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page, true)) return;
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5175',
      'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Authorization,Content-Type',
      'Access-Control-Allow-Methods': 'GET,POST,OPTIONS' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    if (path === '/api/v1/admin-auth/login') return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: { tokenType: 'Bearer', accessToken: 'admin-access', refreshToken: 'admin-refresh',
        expiresInSeconds: 600, user: { accountType: 'ADMIN', status: 'ACTIVE' } } }) });
    if (path === '/api/v1/admin-auth/me') return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: { accountType: 'ADMIN', status: 'ACTIVE' } }) });
    if (path === '/api/v1/admin/approval-requests/') return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: decided ? [] : [{ id, businessName: 'Sân A', kind: 'ONBOARDING', submittedAt: '2026-10-05T00:00:00Z' }] }) });
    if (path === `/api/v1/admin/approval-requests/${id}`) return route.fulfill({ status: 200, headers, contentType: 'application/json',
      body: JSON.stringify({ data: { id, kind: 'ONBOARDING', status: 'PENDING', snapshot: JSON.stringify({
        name: 'Sân A', legalName: 'Công ty A', contact: 'owner@example.test', venues: [] }) } }) });
    if (path === `/api/v1/admin/approval-requests/${id}/approve`) {
      expect(request.headers().authorization).toBe('Bearer admin-access');
      decided = true;
      return route.fulfill({ status: 200, headers, contentType: 'application/json', body: JSON.stringify({ data: { status: 'APPROVED' } }) });
    }
    return route.fulfill({ status: 404, headers, body: '{}' });
  });
  await page.goto('http://localhost:5175');
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Mật khẩu').fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await page.getByRole('button', { name: /Sân A/ }).click();
  await expect(page.getByRole('heading', { name: 'Sân A' })).toBeVisible();
  await page.getByRole('button', { name: 'Phê duyệt', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true }).click();
  await expect(page.getByText('Chưa có hồ sơ chờ duyệt.')).toBeVisible();
  expect(decided).toBe(true);
});
