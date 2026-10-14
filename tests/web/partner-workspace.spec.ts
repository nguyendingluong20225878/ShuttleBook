import { expect, Page, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => {
    if (!(await browserSessionRoute(route, page))) await route.fallback();
  });
});
import { openPartnerPage } from './helpers/partner-navigation';
import { emptyPartnerCalendar } from './helpers/partner-calendar';

async function fixture(page: Page, status = 'DRAFT', approval?: { status: string }) {
  const calls: Array<{ path: string; method: string; body: unknown }> = [];
  let noticesFail = false;
  let businessFail = false;
  let read = false;
  const hour = { dayOfWeek: 1, opensAt: '05:00', closesAt: '22:00' };
  const price = { dayOfWeek: 1, startsAt: '05:00', endsAt: '22:00', pricePerSlot: 80000 };
  const businesses = [
    { id: 'b1', name: 'Hoàng Cầu Sports', legalName: 'Công ty Hoàng Cầu', contact: 'Liên hệ test', status, version: 1, approval,
      venues: [{ id: 'v1', name: 'Cơ sở Hoàng Cầu', address: '31 ngõ 16 Hoàng Cầu, Hà Nội', contact: 'Liên hệ cơ sở',
        latitude: 21.0186, longitude: 105.8236, timezone: 'Asia/Ho_Chi_Minh', status, version: 1, imageUploadId: 'image',
        courts: [{ id: 'c1', name: 'Pickleball 1', status, hours: [hour], prices: [price] }, { id: 'c2', name: 'Pickleball 2', status, hours: [], prices: [] }] },
      { id: 'v2', name: 'Cơ sở Đống Đa', address: 'Đống Đa, Hà Nội', contact: 'Liên hệ cơ sở 2', latitude: 21, longitude: 105,
        timezone: 'Asia/Ho_Chi_Minh', status, version: 1, courts: [{ id: 'c3', name: 'Cầu lông 1', status, hours: [], prices: [] }] }] },
    { id: 'b2', name: 'Doanh nghiệp thứ hai', legalName: 'Công ty B', contact: 'Liên hệ B', status, version: 1,
      venues: [{ id: 'v3', name: 'Cơ sở B', address: 'Hà Nội', contact: 'Cơ sở B', latitude: 21, longitude: 105,
        timezone: 'Asia/Ho_Chi_Minh', status, version: 1, courts: [] }] },
  ];
  businesses.forEach(business => business.venues.forEach(venue => {
    if (status === 'ACTIVE') venue.status = 'PUBLISHED';
    venue.courts.forEach(court => { if (status === 'DRAFT') court.status = 'INACTIVE'; });
  }));
  await page.addInitScript(() => {
    (window as unknown as { maptilersdk: unknown }).maptilersdk = {
      config: { apiKey: '' }, Map: class { setCenter() {} setZoom() {} remove() {} resize() {} },
      Marker: class { setLngLat() { return this; } addTo() { return this; } remove() {} },
    };
  });
  await page.route('http://localhost:5080/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const request = route.request(); const path = new URL(request.url()).pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true', 'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match',
      'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    calls.push({ path, method: request.method(), body: request.method() === 'GET' ? null : request.postDataJSON() });
    const respond = (data: unknown) => route.fulfill({ status: 200, contentType: 'application/json', headers, body: JSON.stringify({ data }) });
    if (path === '/api/v1/browser-auth/partner/login') return respond({ accessToken: 'workspace-test', ...browserSessionFields(), user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/partner-onboarding/businesses') return businessFail
      ? route.fulfill({ status: 503, headers, contentType: 'application/problem+json', body: JSON.stringify({ code: 'TEMPORARY_UNAVAILABLE' }) }) : respond(businesses);
    if (path === '/api/v1/me/notifications/') return noticesFail ? route.fulfill({ status: 503, headers })
      : respond([{ id: 'n1', title: 'Phản hồi hồ sơ', body: 'Vui lòng kiểm tra thông tin cơ sở.', readAt: read ? '2026-10-07T00:00:00Z' : null }]);
    if (path === '/api/v1/me/notifications/n1/read') { read = true; return respond({}); }
    if (path === '/api/v1/uploads/image/view') return route.fulfill({ status: 200, contentType: 'image/png', headers,
      body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64') });
    if (path.endsWith('/operations')) return respond({ id: 'c1', name: 'Pickleball 1', status: 'ACTIVE', version: 1,
      timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 30, minimumBookingMinutes: 60, holdMinutes: 20,
      hours: [hour], basePrices: [price], rules: [{ ...price, startsOn: '2026-10-07', endsOn: '2026-10-08', priority: 1 }] });
    if (path.endsWith('/maintenance')) return respond([]);
    if (path.endsWith('/calendar')) return respond(emptyPartnerCalendar(businesses[0].venues.find(venue => path.includes(`/venues/${venue.id}/`))!, new URL(request.url()).searchParams.get('date')!));
    if (/\/partner-onboarding\/courts\/[^/]+\/schedule$/.test(path)) {
      const court = businesses.flatMap(business => business.venues.flatMap(venue => venue.courts)).find(court => path.includes(`/courts/${court.id}/`))!;
      return respond({ ...court, version: 1, timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 30,
        minimumBookingMinutes: 30, holdMinutes: 20, basePrices: court.prices, rules: [] });
    }
    const business = businesses.find(item => path === `/api/v1/partner-onboarding/businesses/${item.id}`);
    return respond(business ?? {});
  });
  return { calls, failNotices: (value: boolean) => { noticesFail = value; }, failBusiness: (value: boolean) => { businessFail = value; } };
}

async function login(page: Page, hash = '') {
  await page.goto(`http://localhost:5174/${hash}`);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.locator('.status-banner')).toContainText('Hoàng Cầu Sports');
}

test('workspace shows real counters, separated forms, retained drafts and cleared business scope', async ({ page }, info) => {
  const api = await fixture(page); await login(page);
  const cards = page.locator('.stat-card');
  await expect(cards.nth(0)).toContainText('2'); await expect(cards.nth(1)).toContainText('3'); await expect(cards.nth(2)).toContainText('1/3');
  await expect(page.locator('.checklist')).toContainText('Cơ sở và sân');
  await page.screenshot({ path: info.outputPath('partner-overview.png'), fullPage: true });
  await openPartnerPage(page, 'Cơ sở & sân');
  await expect(page.getByRole('heading', { name: 'Thêm cơ sở' })).toBeVisible();
  await expect(page.getByRole('img', { name: 'Ảnh cơ sở Cơ sở Hoàng Cầu' })).toBeVisible();
  await expect(page.getByText('Múi giờ', { exact: false })).toHaveCount(0);
  const firstVenue = page.getByRole('region', { name: 'Các sân tại Cơ sở Hoàng Cầu' });
  await expect(firstVenue.getByRole('button', { name: 'Thêm sân tại Cơ sở Hoàng Cầu' })).toBeVisible();
  const payment = page.getByRole('form', { name: 'Thiết lập QR cho Cơ sở Hoàng Cầu' });
  await payment.getByLabel('Số tài khoản').fill('123456');
  await openPartnerPage(page, 'Thanh toán & QR'); await expect(payment).toBeHidden();
  await openPartnerPage(page, 'Cơ sở & sân');
  await expect(payment.getByLabel('Số tài khoản')).toHaveValue('123456');
  await page.getByRole('combobox', { name: 'Doanh nghiệp', exact: true }).selectOption('b2');
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp thứ hai');
  await expect(firstVenue).toHaveCount(0);
  await expect(payment).toHaveCount(0);
  await expect(page.getByRole('form', { name: 'Thiết lập QR cho Cơ sở B' }).getByLabel('Số tài khoản')).toHaveValue('');
  await page.getByRole('region', { name: 'Các sân tại Cơ sở B' }).getByRole('button', { name: 'Thêm sân tại Cơ sở B' }).click();
  await expect.poll(() => api.calls.filter(call => call.method === 'POST' && call.path.endsWith('/courts')).map(call => call.path))
    .toEqual(['/api/v1/partner-onboarding/venues/v3/courts']);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('navigation supports deep links, Back, keyboard menu and unique map labels', async ({ page }) => {
  await fixture(page); await login(page, '#/payments');
  await expect(page.getByRole('heading', { name: 'Thanh toán & QR', exact: true })).toBeVisible();
  await openPartnerPage(page, 'Cơ sở & sân'); await openPartnerPage(page, 'Lịch & giá'); await page.goBack();
  await expect(page.getByRole('heading', { name: 'Cơ sở & sân', exact: true })).toBeVisible();
  await page.goForward(); await expect(page.getByRole('heading', { name: 'Lịch & giá', exact: true })).toBeVisible();
  await openPartnerPage(page, 'Cơ sở & sân');
  const labels = page.getByLabel('Địa chỉ cơ sở trên MapTiler'); await expect(labels).toHaveCount(3);
  const ids = await labels.evaluateAll(elements => elements.map(element => element.id)); expect(new Set(ids).size).toBe(3);
  await page.setViewportSize({ width: 375, height: 812 });
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  await menu.click(); await expect(menu).toHaveAttribute('aria-expanded', 'true');
  await page.getByRole('navigation', { name: 'Quản lý đối tác' }).getByRole('link', { name: 'Cơ sở & sân', exact: true }).focus();
  await page.keyboard.press('Escape'); await expect(menu).toBeFocused(); await expect(menu).toHaveAttribute('aria-expanded', 'false');
  await menu.focus(); await page.keyboard.press('Shift+Tab'); await page.keyboard.press('Shift+Tab');
  await expect(page.getByRole('link', { name: 'Đến nội dung chính' })).toBeFocused(); await page.keyboard.press('Enter');
  await expect(page.locator('#partner-content')).toBeFocused();
});

test('notifications show errors, retry and read state; profile loading can recover', async ({ page }) => {
  const api = await fixture(page); api.failBusiness(true); api.failNotices(true);
  await page.goto('http://localhost:5174'); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test'); await page.getByLabel('Mật khẩu').fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('alert')).toContainText('TEMPORARY_UNAVAILABLE');
  await expect(page.getByRole('heading', { name: 'Tạo doanh nghiệp' })).toHaveCount(0);
  api.failBusiness(false); await page.getByRole('button', { name: 'Thử tải lại hồ sơ' }).click();
  await expect(page.locator('.status-banner')).toContainText('Hoàng Cầu Sports');
  await openPartnerPage(page, 'Thông báo'); await expect(page.getByRole('alert')).toContainText('Không tải được thông báo');
  api.failNotices(false); await page.getByRole('button', { name: 'Tải lại thông báo' }).click();
  await expect(page.locator('.notice-count')).toHaveText('1'); await page.locator('.notice-list .partner-notice-open').click();
  await expect(page.locator('.notice-count')).toHaveCount(0); await expect(page.locator('.notice-list .status-badge')).toHaveText('Đã đọc');
});

test('pending profiles and pending revisions keep mutation forms locked', async ({ page }) => {
  await fixture(page, 'PENDING_APPROVAL', { status: 'PENDING' }); await login(page);
  for (const name of ['Hồ sơ doanh nghiệp', 'Cơ sở & sân', 'Lịch & giá', 'Thanh toán & QR']) {
    await openPartnerPage(page, name);
    // A read-only schedule may remain visible while approval is pending; mutation stays locked.
    for (const save of await page.locator('main form:visible button[type="submit"], main form:visible button:not([type])').all())
      await expect(save).toBeDisabled();
  }
  await expect(page.getByRole('button', { name: 'Gửi hồ sơ duyệt' })).toHaveCount(0);
  await expect(page.getByText(/Hồ sơ đang chờ Admin duyệt/)).toBeVisible();
  await page.unroute('http://localhost:5080/api/v1/**');
  await fixture(page, 'ACTIVE', { status: 'PENDING' }); await page.reload();
  await expect(page.locator('.status-banner')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đề nghị đổi tài khoản hoặc QR' })).toHaveCount(0);
});

test('forms and dashboard reflow at phone, tablet and desktop widths', async ({ page }, info) => {
  await fixture(page, 'ACTIVE');
  await page.goto('http://localhost:5174'); await page.screenshot({ path: info.outputPath('partner-auth.png'), fullPage: true });
  await login(page);
  for (const width of [375, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    for (const name of ['Tổng quan', 'Lịch & giá', 'Thanh toán & QR']) {
      await openPartnerPage(page, name);
      if (name === 'Lịch & giá') {
        await page.getByRole('tab', { name: 'Giờ & giá', exact: true }).click();
        await expect(page.getByRole('heading', { name: 'Giờ mở cửa và giá hàng tuần' })).toBeVisible();
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const visibleFields = page.locator('main input:not([type="checkbox"]):not([type="radio"]):visible, main select:visible');
      for (const element of await visibleFields.all()) {
        const box = await element.boundingBox(); expect(box!.width).toBeGreaterThan(100); expect(box!.height).toBeGreaterThanOrEqual(44);
      }
    }
  }
  await openPartnerPage(page, 'Lịch & giá'); await page.screenshot({ path: info.outputPath('partner-operations-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 375, height: 812 }); await page.screenshot({ path: info.outputPath('partner-operations-mobile.png'), fullPage: true });
});
