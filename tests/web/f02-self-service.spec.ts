import { expect, Page, test } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';
import { openPartnerPage } from './helpers/partner-navigation';
import { emptyPartnerCalendar } from './helpers/partner-calendar';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');
const hour = { dayOfWeek: 1, opensAt: '07:00', closesAt: '22:00' };
const price = { dayOfWeek: 1, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 80000 };
test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/browser-auth/**', async route => { if (!(await browserSessionRoute(route, page))) await route.fallback(); });
});

async function partnerFixture(page: Page, pending = false) {
  const calls: Array<{ path: string; method: string; body: any; version?: string }> = [];
  const business = { id: 'b1', name: 'Doanh nghiệp A', legalName: 'Công ty A', contact: 'Liên hệ A', status: 'ACTIVE', version: 4,
    approvals: pending ? [{ id: 'a1', kind: 'BUSINESS_REVISION', status: 'PENDING' }] : [],
    venues: [{ id: 'v1', name: 'Cơ sở A', status: 'PUBLISHED', version: 3, address: 'Hà Nội', contact: 'Liên hệ cơ sở',
      latitude: 21, longitude: 105, timezone: 'Asia/Ho_Chi_Minh', imageUploadId: 'old-venue', paymentAccount: { maskedAccountNumber: '***1234' },
      courts: [{ id: 'c1', name: 'Sân đang hoạt động', status: 'ACTIVE', version: 7, imageUploadId: 'old-court', hours: [hour], prices: [price] },
        { id: 'c2', name: 'Sân mới', status: 'INACTIVE', version: 1, imageUploadId: null, hours: [], prices: [] }] },
      { id: 'v2', name: 'Cơ sở mới', status: 'DRAFT', version: 1, address: 'Đống Đa', contact: 'Liên hệ B',
        latitude: 21, longitude: 105, timezone: 'Asia/Ho_Chi_Minh', imageUploadId: null, paymentAccount: null, courts: [] }] };
  await page.addInitScript(() => {
    (window as any).maptilersdk = { config: { apiKey: '' }, Map: class { setCenter() {} setZoom() {} remove() {} resize() {} },
      Marker: class { setLngLat() { return this; } addTo() { return this; } remove() {} } };
  });
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const req = route.request(); const path = new URL(req.url()).pathname;
    const reply = (data: unknown) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data }) });
    calls.push({ path, method: req.method(), body: req.postData() ? req.postDataJSON() : null, version: req.headers()['if-match'] });
    if (path.endsWith('/partner/login')) return reply({ accessToken: 'owner-self-service', ...browserSessionFields(), user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/me/notifications/') return reply([]);
    if (path === '/api/v1/partner-onboarding/businesses') return reply([business]);
    if (path === '/api/v1/partner-onboarding/businesses/b1' && req.method() === 'GET') return reply(business);
    if (path.endsWith('/court-numbers') && req.method() === 'PUT') {
      const venue = business.venues.find(item => path.includes(`/venues/${item.id}/`))!;
      for (const change of req.postDataJSON().courts) {
        const court = venue.courts.find(item => item.id === change.id)!;
        court.name = `Sân ${change.number}`; court.version++;
      }
      return reply({ courts: venue.courts });
    }
    if (path.endsWith('/contact') && req.method() === 'PUT') {
      const venue = business.venues.find(item => path.includes(`/venues/${item.id}/`))!;
      venue.contact = req.postDataJSON().contact; venue.version++;
      return reply({ id: venue.id, contact: venue.contact, version: venue.version });
    }
    if (path.endsWith('/courts/c2/schedule')) {
      const court = business.venues[0].courts[1];
      if (req.method() === 'GET') return reply({ ...court, venueId: 'v1', timezone: 'Asia/Ho_Chi_Minh',
        bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20, basePrices: court.prices, rules: [] });
      court.hours = [hour]; court.prices = [price]; court.status = 'ACTIVE'; court.version++;
      business.approvals = business.approvals.filter(item => item.kind !== 'COURT_ADDITION');
      return reply({ id: court.id, status: court.status, version: court.version });
    }
    if (/\/partner-onboarding\/venues\/[^/]+\/courts$/.test(path) && req.method() === 'POST') {
      const venue = business.venues.find(item => path.includes(`/venues/${item.id}/`))!;
      const court = { id: `new-${venue.id}-${venue.courts.length}`, name: req.postDataJSON().name, status: 'INACTIVE',
        version: 1, imageUploadId: null, hours: [], prices: [] };
      venue.courts.push(court as any);
      return reply(court);
    }
    if (path.endsWith('/operations')) return reply({ id: 'c1', name: 'Sân đang hoạt động', status: 'ACTIVE', version: 7,
      timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 30, minimumBookingMinutes: 60, holdMinutes: 20, hours: [hour], basePrices: [price], rules: [] });
    if (path.endsWith('/maintenance')) return reply([]);
    if (path.endsWith('/calendar')) return reply(emptyPartnerCalendar(business.venues[0], new URL(req.url()).searchParams.get('date')!));
    if (path.endsWith('/view')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path === '/api/v1/uploads/presign') return reply({ id: 'new-image', uploadUrl: 'http://localhost:5080/test-media', uploadHeaders: {} });
    if (path.endsWith('/payment-account') && req.method() === 'PUT') {
      const venue = business.venues.find(item => path.includes(`/venues/${item.id}/`))!;
      venue.paymentAccount = { accountName: req.postDataJSON().accountName, maskedAccountNumber: '***7890' } as any;
      venue.version++; return reply({ venueId: venue.id, ...venue.paymentAccount });
    }
    if (path.endsWith('/payment-account/revisions') && req.method() === 'POST') {
      business.approvals = [{ id: 'payment-change', kind: 'VENUE_REVISION', status: 'PENDING' }]; return reply({});
    }
    if (path === '/api/v1/operator/venues/v1/image') {
      business.venues[0].imageUploadId = req.method() === 'DELETE' ? null as any : req.postDataJSON().uploadId;
      business.venues[0].version++; return reply({});
    }
    if (path === '/api/v1/partner-onboarding/businesses/b1/revisions') {
      business.approvals = [{ id: 'a3', kind: 'BUSINESS_REVISION', status: 'PENDING' }]; return reply({});
    }
    return reply({});
  });
  await page.route('http://localhost:5080/test-media', route => route.fulfill({ status: 200 }));
  await page.goto('http://localhost:5174/#/venues');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp A');
  return { business, calls };
}

test('Partner numbers courts within each venue and keeps action buttons compact', async ({ page }, info) => {
  const api = await partnerFixture(page);
  const longName = 'Cơ sở cầu lông Hoàng Cầu khu thể thao dành cho cộng đồng';
  api.business.venues[0].name = longName;
  api.business.venues[0].courts[0].name = 'Sân 1';
  api.business.venues[0].courts[1].name = 'Sân 3';
  await page.getByRole('button', { name: 'Tải lại hồ sơ', exact: true }).click();
  const first = page.getByRole('region', { name: `Các sân tại ${longName}` });
  const second = page.getByRole('region', { name: 'Các sân tại Cơ sở mới' });
  await first.getByRole('button', { name: `Thêm sân tại ${longName}` }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/v1/courts') && call.method === 'POST').length).toBe(1);
  expect(api.calls.find(call => call.path.endsWith('/v1/courts') && call.method === 'POST')!.body).toEqual({ name: 'Sân 4' });
  await second.getByRole('button', { name: 'Thêm sân tại Cơ sở mới' }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/v2/courts') && call.method === 'POST').length).toBe(1);
  expect(api.calls.find(call => call.path.endsWith('/v2/courts') && call.method === 'POST')!.body).toEqual({ name: 'Sân 1' });
  await expect(first.getByText('Sân 4:', { exact: false })).toBeVisible();
  await expect(second.getByText('Sân 1:', { exact: false })).toBeVisible();
  await page.reload();
  await expect(first.getByText('Sân 4:', { exact: false })).toBeVisible();
  await expect(second.getByText('Sân 1:', { exact: false })).toBeVisible();
  await expect(first.getByRole('button', { name: `Thêm sân tại ${longName}`, exact: true })).toHaveText('Thêm sân');
  await expect(page.getByRole('button', { name: `Gỡ ảnh cơ sở ${longName}`, exact: true })).toHaveText('Gỡ ảnh');
  const actions = first.locator('.court-batch-actions');
  const addBox = (await actions.getByRole('button', { name: `Thêm sân tại ${longName}` }).boundingBox())!;
  await expect(actions.getByRole('button')).toHaveCount(1);
  expect(addBox.width).toBeLessThan(180);
  expect(addBox.height).toBeGreaterThanOrEqual(44);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await first.screenshot({ path: info.outputPath('partner-court-number-form.png') });
});

test('active Partner replaces and removes optional venue photo while a legal revision is pending', async ({ page }) => {
  const api = await partnerFixture(page, true);
  await expect(page.getByRole('heading', { name: 'Ảnh riêng từng sân' })).toHaveCount(0);
  await expect(page.getByRole('img', { name: /^Ảnh sân/ })).toHaveCount(0);
  const venue = page.getByRole('heading', { name: 'Ảnh cơ sở', exact: true }).locator('..');
  await venue.getByRole('combobox', { name: 'Cơ sở', exact: true }).selectOption('v1');
  await venue.getByLabel('Ảnh cơ sở', { exact: true }).setInputFiles({ name: 'venue.png', mimeType: 'image/png', buffer: png });
  await venue.getByRole('button', { name: 'Tải ảnh', exact: true }).click();
  await expect.poll(() => api.calls.filter(call => call.path === '/api/v1/operator/venues/v1/image').length).toBe(1);
  expect(api.calls.find(call => call.path === '/api/v1/operator/venues/v1/image')?.version).toBe('"3"');
  expect(api.calls.find(call => call.path === '/api/v1/uploads/presign')?.body).toMatchObject({ venueId: 'v1', purpose: 'VENUE_IMAGE' });
  await expect(page.getByRole('img', { name: 'Ảnh cơ sở Cơ sở A' })).toBeVisible();
  await page.getByRole('button', { name: 'Gỡ ảnh cơ sở Cơ sở A', exact: true }).click();
  await expect(page.getByRole('img', { name: 'Ảnh cơ sở Cơ sở A' })).toHaveCount(0);
  expect(api.calls.find(call => call.method === 'DELETE')?.version).toBe('"4"');
  expect(api.calls.filter(call => call.path.includes('/operator/courts/') || call.body?.purpose === 'COURT_IMAGE')).toEqual([]);
  expect(api.calls.filter(call => call.path.endsWith('/submit') || call.path.endsWith('/revisions'))).toEqual([]);
});

test('active Partner publishes court by saving schedule and keeps new venue approval separate', async ({ page }) => {
  const api = await partnerFixture(page);
  await expect(page.getByRole('heading', { name: 'Thêm cơ sở', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Gửi duyệt cơ sở Cơ sở mới' })).toBeVisible();
  await openPartnerPage(page, 'Lịch & giá');
  const draftSchedule = page.locator('.operations-page');
  await draftSchedule.getByRole('tab', { name: 'Giờ & giá', exact: true }).click();
  await expect(draftSchedule.getByRole('combobox', { name: 'Sân', exact: true }).locator('option')).toHaveCount(2);
  await draftSchedule.getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await draftSchedule.getByRole('button', { name: 'Lưu lịch tuần' }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/courts/c2/schedule') && call.method === 'PUT').length).toBe(1);
  await openPartnerPage(page, 'Cơ sở & sân');
  await expect(page.getByRole('form', { name: 'Thiết lập QR cho Cơ sở mới' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Xác nhận Sân mới tại Cơ sở A' })).toHaveCount(0);
  await expect(page.getByText('Sân mới: 1 ngày mở cửa, 1 khung giá · Đang hoạt động', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Gửi duyệt cơ sở Cơ sở mới' })).toBeEnabled();
  expect(api.calls.find(call => call.path.endsWith('/courts/c2/schedule') && call.method === 'PUT')?.version).toBe('"1"');
  expect(api.calls.filter(call => call.path.endsWith('/submit'))).toEqual([]);
  expect(api.business.approvals).toEqual([]);
  await expect(page.getByRole('button', { name: 'Gỡ ảnh cơ sở Cơ sở A' })).toBeEnabled();
  expect(api.business.status).toBe('ACTIVE');
  await page.screenshot({ path: test.info().outputPath('partner-additions.png'), fullPage: true });
});

test('venue setup groups court actions and links exact draft through pricing tabs, reload and back', async ({ page }, info) => {
  const api = await partnerFixture(page);
  const group = page.getByRole('region', { name: 'Các sân tại Cơ sở A' });
  const row = group.locator('.court-item').filter({ hasText: 'Sân mới:' });
  await expect(group.getByRole('button', { name: 'Lưu tất cả số sân' })).toHaveCount(0);
  await expect(row.getByRole('button', { name: /^Xác nhận/ })).toHaveCount(0);
  await expect(group.getByRole('button', { name: 'Thêm sân tại Cơ sở A' })).toBeVisible();
  await row.getByRole('link', { name: /Thiết lập giờ & giá/ }).click();
  const workspace = page.locator('.operations-page');
  await expect(workspace.getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c2');
  await workspace.getByRole('button', { name: 'Lưu lịch tuần' }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/courts/c2/schedule') && call.method === 'PUT').length).toBe(1);
  await expect(workspace.getByRole('button', { name: 'Lưu lịch tuần' })).toBeEnabled();
  await workspace.getByRole('tab', { name: 'Lịch sân', exact: true }).click();
  await expect(workspace.getByRole('combobox', { name: 'Sân', exact: true }).locator('option')).toContainText(['Tất cả sân', 'Sân đang hoạt động', 'Sân mới']);
  await workspace.getByRole('combobox', { name: 'Sân', exact: true }).selectOption('');
  await workspace.getByRole('tab', { name: 'Giờ & giá', exact: true }).click();
  await expect(workspace.getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c2');
  await page.reload();
  await expect(workspace.getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('80000');
  await workspace.getByRole('link', { name: 'Quay lại cơ sở & sân' }).click();
  await expect(group).toBeVisible();
  await group.screenshot({ path: info.outputPath('grouped-court-actions.png') });
  if (info.project.name === 'desktop') {
    const [first, second] = await Promise.all([
      group.locator('.court-item').nth(0).boundingBox(), group.locator('.court-item').nth(1).boundingBox(),
    ]);
    expect(Math.abs(first!.y - second!.y)).toBeLessThan(4);
    expect(second!.x).toBeGreaterThan(first!.x + first!.width - 5);
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('Partner court numbers are read-only and adding a court does not rename existing courts', async ({ page }) => {
  const api = await partnerFixture(page);
  api.business.venues[0].courts[0].name = 'Sân 1';
  api.business.venues[0].courts[1].name = 'Sân 2';
  await page.getByRole('button', { name: 'Tải lại hồ sơ', exact: true }).click();
  const group = page.getByRole('region', { name: 'Các sân tại Cơ sở A' });
  await expect(group.getByRole('spinbutton')).toHaveCount(0);
  await expect(group.getByRole('button', { name: /Lưu tất cả số sân|Xóa sân/ })).toHaveCount(0);
  await group.getByRole('button', { name: 'Thêm sân tại Cơ sở A' }).click();
  expect(api.calls.filter(call => call.path.endsWith('/v1/court-numbers'))).toEqual([]);
  expect(api.calls.filter(call => call.method === 'DELETE' && call.path.includes('/courts/'))).toEqual([]);
  expect(api.calls.find(call => call.path.endsWith('/v1/courts') && call.method === 'POST')?.body).toEqual({ name: 'Sân 3' });
  await page.reload();
  await expect(group.getByText('Sân 1:', { exact: false })).toBeVisible();
  await expect(group.getByText('Sân 2:', { exact: false })).toBeVisible();
  await expect(group.getByText('Sân 3:', { exact: false })).toBeVisible();
});

test('initial venue QR is set inside venue setup without bank code and revision sends payment fields only', async ({ page }) => {
  const api = await partnerFixture(page);
  const setup = page.getByRole('form', { name: 'Thiết lập QR cho Cơ sở mới' });
  await expect(page.getByLabel('Mã ngân hàng')).toHaveCount(0);
  await setup.getByLabel('Tên tài khoản').fill('NEW VENUE OWNER');
  await setup.getByLabel('Số tài khoản').fill('1234567890');
  await setup.getByLabel('Ảnh QR').setInputFiles({ name: 'qr.png', mimeType: 'image/png', buffer: png });
  await setup.getByRole('button', { name: 'Lưu QR và tài khoản' }).click();
  await expect(setup).toContainText('Đã lưu: NEW VENUE OWNER');
  expect(api.calls.find(call => call.path.endsWith('/v2/payment-account'))?.body).toEqual({
    accountName: 'NEW VENUE OWNER', accountNumber: '1234567890', qrUploadId: 'new-image' });
  expect(api.calls.find(call => call.path.endsWith('/presign'))?.body.venueId).toBe('v2');
  await expect(setup.getByRole('button', { name: 'Lưu QR và tài khoản' })).toBeEnabled();
  await openPartnerPage(page, 'Thanh toán & QR');
  await expect(page.getByRole('form', { name: 'Thiết lập QR cho Cơ sở mới' })).toBeHidden();
  const revision = page.locator('.payment-revision-form');
  await revision.getByLabel('Cơ sở').selectOption('v1');
  api.business.venues[0].address = 'Địa chỉ mới đã duyệt';
  await page.getByRole('button', { name: 'Tải lại hồ sơ', exact: true }).click();
  await revision.getByLabel('Tên tài khoản').fill('UPDATED OWNER');
  await revision.getByLabel('Số tài khoản').fill('9876543210');
  await revision.getByLabel('QR mới').setInputFiles({ name: 'new-qr.png', mimeType: 'image/png', buffer: png });
  await revision.getByRole('button', { name: 'Gửi Admin duyệt' }).click();
  await expect(revision).toHaveCount(0);
  expect(api.calls.find(call => call.path.endsWith('/v1/payment-account/revisions'))?.body).toEqual({
    accountName: 'UPDATED OWNER', accountNumber: '9876543210', qrUploadId: 'new-image' });
  await expect(page.getByLabel('Mã ngân hàng')).toHaveCount(0);
});

test('Partner saves a legacy pending court during an unrelated revision', async ({ page }) => {
  const api = await partnerFixture(page, true);
  api.business.venues[0].courts[1].status = 'PENDING_APPROVAL';
  api.business.approvals = [{ id: 'a2', kind: 'COURT_ADDITION', status: 'PENDING', venueId: 'v1', courtId: 'c2' } as any];
  await page.getByRole('button', { name: 'Tải lại hồ sơ', exact: true }).click();
  await page.getByRole('region', { name: 'Các sân tại Cơ sở A' }).getByRole('link', { name: /Thiết lập giờ & giá/ }).last().click();
  const workspace = page.locator('.operations-page');
  await workspace.getByRole('button', { name: 'Lưu lịch tuần' }).click();
  await expect.poll(() => api.business.venues[0].courts[1].status).toBe('ACTIVE');
  await expect(page.getByRole('button', { name: 'Xác nhận sân' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Gửi duyệt Sân/ })).toHaveCount(0);
});

test('Partner toast stays at the viewport top, expires and resets on repeated errors', async ({ page }, info) => {
  await partnerFixture(page);
  await page.clock.install();
  const add = page.getByRole('button', { name: 'Thêm sân tại Cơ sở A', exact: true });
  await page.route('**/api/v1/partner-onboarding/venues/v1/courts', route => route.fulfill({
    status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: 'INCOMPLETE_PROFILE' }),
  }));
  await add.scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(200);
  await add.click();
  const toast = page.locator('.partner-toast--error');
  await expect(toast).toContainText('Chưa đủ thông tin');
  const box = (await toast.boundingBox())!;
  expect(box.y).toBeGreaterThanOrEqual(0); expect(box.y).toBeLessThan(40);
  expect(box.y + box.height).toBeLessThan(page.viewportSize()!.height);
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  expect(await toast.evaluate(element => getComputedStyle(element).position)).toBe('fixed');
  await page.screenshot({ path: info.outputPath('partner-toast-scrolled.png') });
  await page.clock.fastForward(2000);
  await add.click();
  await expect(toast).toBeVisible();
  await page.clock.fastForward(2000);
  await expect(toast).toBeVisible();
  await page.clock.fastForward(1100);
  await expect(toast).toHaveCount(0);
  await add.click();
  await expect(toast).toBeVisible();
  await toast.getByRole('button', { name: 'Đóng thông báo' }).click();
  await expect(toast).toHaveCount(0);
  await page.unroute('**/api/v1/partner-onboarding/venues/v1/courts');
  await add.click();
  const success = page.locator('.partner-toast--success');
  await expect(success).toContainText('Đã thêm sân.');
  expect((await success.boundingBox())!.y).toBeLessThan(40);
  await page.clock.fastForward(3100);
  await expect(success).toHaveCount(0);
});

test('Partner stale court addition retains recovery after toast expires', async ({ page }) => {
  const api = await partnerFixture(page);
  await page.clock.install();
  await page.route('**/api/v1/partner-onboarding/venues/v1/courts', route => route.fulfill({
    status: 412, contentType: 'application/problem+json', body: JSON.stringify({ code: 'PRECONDITION_FAILED' }),
  }));
  const add = page.getByRole('button', { name: 'Thêm sân tại Cơ sở A', exact: true });
  await add.click();
  await expect(page.locator('.partner-toast--error')).toBeVisible();
  await page.clock.fastForward(3100);
  await expect(page.locator('.partner-toast')).toHaveCount(0);
  await expect(page.getByText('Thông tin đã thay đổi. Tải lại hồ sơ trước khi tiếp tục.', { exact: false })).toBeVisible();
  await expect(add).toBeDisabled();
  api.business.venues[0].courts[1].version = 2;
  await page.unroute('**/api/v1/partner-onboarding/venues/v1/courts');
  await page.getByRole('button', { name: 'Tải lại để tiếp tục', exact: true }).click();
  await expect(add).toBeEnabled();
  await add.click();
  await expect(page.locator('.partner-toast--success')).toContainText('Đã thêm sân.');
  expect(api.calls.find(call => call.path.endsWith('/v1/courts') && call.method === 'POST')?.body).toEqual({ name: 'Sân 1' });
});

test('Partner stale schedule stays blocked until it reloads a fresh version', async ({ page }) => {
  await partnerFixture(page);
  await openPartnerPage(page, 'Lịch & giá');
  await page.getByRole('tab', { name: 'Giờ & giá', exact: true }).click();
  await page.locator('details:has(#operator-policy) > summary').click();
  const save = page.locator('#operator-policy').getByRole('button', { name: 'Lưu quy định', exact: true });
  await expect(save).toBeEnabled();
  await page.clock.install();
  const versions: string[] = [];
  await page.route('**/api/v1/operator/courts/c1/booking-policy', route => {
    versions.push(route.request().headers()['if-match']);
    return route.fulfill({ status: versions.length === 1 ? 412 : 200, contentType: 'application/json',
      body: JSON.stringify(versions.length === 1 ? { code: 'PRECONDITION_FAILED' } : { data: {} }) });
  });
  await page.route('**/api/v1/operator/courts/c1/operations', route => route.fulfill({ contentType: 'application/json',
    body: JSON.stringify({ data: { id: 'c1', name: 'Sân đang hoạt động', status: 'ACTIVE', version: 8,
      timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 30, minimumBookingMinutes: 60, holdMinutes: 20,
      hours: [hour], basePrices: [price], rules: [] } }) }));
  await save.click();
  await expect(page.locator('.partner-toast--error')).toBeVisible();
  await page.clock.fastForward(3100);
  await expect(page.locator('.partner-toast')).toHaveCount(0);
  await expect(page.locator('.operations-page').getByRole('alert').filter({ hasText: 'Cấu hình hoặc trạng thái sân đã thay đổi' })).toBeVisible();
  await expect(save).toBeDisabled();
  await page.getByRole('button', { name: 'Tải lại cấu hình', exact: true }).click();
  await expect(page.locator('details:has(#operator-policy) > summary')).toBeVisible();
  await page.locator('details:has(#operator-policy) > summary').click();
  await expect(save).toBeEnabled();
  await save.click();
  await expect(page.locator('.partner-toast--success')).toContainText('Đã lưu.');
  expect(versions).toEqual(['"7"', '"8"']);
});

test('active Partner submits legal business revision with current version and keeps published values', async ({ page }) => {
  const api = await partnerFixture(page);
  await openPartnerPage(page, 'Hồ sơ doanh nghiệp');
  const revision = page.getByRole('heading', { name: 'Đề nghị thay đổi thông tin doanh nghiệp', exact: true }).locator('..');
  await revision.getByLabel('Tên pháp lý', { exact: true }).fill('Công ty đổi tên');
  await revision.getByRole('button', { name: 'Gửi duyệt' }).click();
  await expect.poll(() => api.calls.filter(call => call.path.endsWith('/businesses/b1/revisions')).length).toBe(1);
  const sent = api.calls.find(call => call.path.endsWith('/businesses/b1/revisions'))!;
  expect(sent.version).toBe('"4"'); expect(sent.body.legalName).toBe('Công ty đổi tên');
  await expect(page.getByRole('heading', { name: 'Đề nghị thay đổi thông tin doanh nghiệp', exact: true })).toHaveCount(0);
  await expect(page.getByText('Tên pháp lý: Công ty A', { exact: true })).toBeVisible();
});

for (const hasPhoto of [false, true]) test(`Customer shows only optional venue photo (photo=${hasPhoto})`, async ({ page }) => {
  await page.route('**/api/v1/venues/**', route => {
    const url = new URL(route.request().url());
    const data = url.pathname.endsWith('/availability') ? { venueId: 'v1', date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', courts: [], stepMinutes: 30 }
      : { id: 'v1', name: 'Cơ sở A', address: 'Hà Nội', contact: 'Liên hệ', latitude: 21, longitude: 105, timezone: 'Asia/Ho_Chi_Minh', imageUrl: hasPhoto ? '/api/v1/venues/v1/image' : null,
        courts: [{ id: 'c1', name: 'Sân ảnh', imageUrl: '/api/v1/courts/c1/image', bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20 },
          { id: 'c2', name: 'Sân trống', imageUrl: null, bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20 }] };
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ data }) });
  });
  const courtImageRequests: string[] = [];
  page.on('request', request => { if (/\/courts\/.*\/image/.test(request.url())) courtImageRequests.push(request.url()); });
  await page.route('**/api/v1/venues/v1/image', route => route.fulfill({ contentType: 'image/png', body: png }));
  await page.goto('http://localhost:5173/venues/v1');
  if (hasPhoto) await expect(page.getByRole('img', { name: 'Ảnh Cơ sở A', exact: true })).toBeVisible();
  else await expect(page.locator('.venue-hero .image-placeholder')).toBeVisible();
  await expect(page.getByRole('img', { name: /^Ảnh sân/ })).toHaveCount(0);
  expect(courtImageRequests).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath(`customer-venue-photo-${hasPhoto}.png`), fullPage: true });
});

for (const kind of ['VENUE_ADDITION', 'BUSINESS_REVISION']) {
  test(`Admin reviews the precise scope of ${kind}`, async ({ page }) => {
    let approved = false;
    const businessCurrent = { name: 'Tên cũ', legalName: 'Công ty cũ', contact: 'Liên hệ cũ', version: 3 };
    const proposal = kind === 'BUSINESS_REVISION' ? { ...businessCurrent, name: 'Tên mới', legalName: 'Công ty mới' }
      : { name: 'Doanh nghiệp A', legalName: 'Công ty A', contact: 'Liên hệ A', venues: [{ name: 'Cơ sở mới A', address: 'Hà Nội', contact: 'Liên hệ',
        timezone: 'Asia/Ho_Chi_Minh', latitude: 21, longitude: 105, imageUploadId: kind === 'VENUE_ADDITION' ? null : 'venue-photo', qrUploadId: 'qr', bankCode: 'TEST', accountName: 'OWNER', accountNumber: '1234',
        courts: [{ name: 'Sân mới A', hours: [hour], prices: [price], imageUploadId: 'court-photo' }] }] };
    await page.route('**/api/v1/**', async route => {
      const path = new URL(route.request().url()).pathname;
      const reply = (data: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data }) });
      if (path.endsWith('/admin-auth/restore')) return reply({}, 401);
      if (path.endsWith('/admin-auth/login')) return reply({ tokenType: 'Bearer', accessToken: 'admin-self-service', refreshToken: 'admin-refresh', expiresInSeconds: 600, user: { accountType: 'ADMIN', status: 'ACTIVE' } });
      if (path.endsWith('/admin-auth/me')) return reply({ accountType: 'ADMIN', status: 'ACTIVE' });
      if (path.includes('/uploads/')) return route.fulfill({ contentType: 'image/png', body: png });
      if (path === '/api/v1/admin/approval-requests/') return reply(approved ? [] : [{ id: 'a1', businessName: 'Doanh nghiệp A', kind, status: 'PENDING', submittedAt: '2026-10-12T00:00:00Z' }]);
      if (path === '/api/v1/admin/approval-requests/a1') return reply({ id: 'a1', kind, status: 'PENDING', snapshot: JSON.stringify(proposal), current: kind === 'BUSINESS_REVISION' ? businessCurrent : null });
      if (path.endsWith('/a1/approve')) { approved = true; return reply({}); }
      return reply([]);
    });
    await page.goto('http://localhost:5175/#approvals');
    await page.getByLabel('Email', { exact: true }).fill('admin@example.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password!');
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await page.getByRole('button', { name: /Doanh nghiệp A/ }).click();
    await expect(page.getByRole('button', { name: 'Xem ảnh sân', exact: true })).toHaveCount(0);
    if (kind === 'VENUE_ADDITION') await expect(page.getByText('Cơ sở chưa có ảnh (tùy chọn).', { exact: true })).toBeVisible();
    if (kind === 'BUSINESS_REVISION') {
      const diff = page.getByRole('region', { name: 'Đối chiếu thông tin doanh nghiệp' });
      await expect(diff.getByRole('cell', { name: 'Tên cũ', exact: true })).toBeVisible();
      await expect(diff.getByRole('cell', { name: 'Tên mới', exact: true })).toBeVisible();
    }
    await page.getByRole('button', { name: 'Phê duyệt', exact: true }).click();
    const confirmation = page.getByRole('group', { name: 'Xác nhận phê duyệt hồ sơ' });
    await expect(confirmation).toContainText(kind === 'VENUE_ADDITION' ? 'Công bố cơ sở mới' : 'Áp dụng thông tin doanh nghiệp');
    await page.screenshot({ path: test.info().outputPath(`admin-${kind}.png`), fullPage: true });
    await page.getByRole('button', { name: 'Xác nhận phê duyệt', exact: true }).click();
    await expect(page.getByText('Chưa có hồ sơ chờ duyệt.')).toBeVisible();
    expect(approved).toBe(true);
  });
}
