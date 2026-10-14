import { expect, test, type Page } from '@playwright/test';
import { browserSessionFields, browserSessionRoute } from './helpers/browser-session';
import { emptyPartnerCalendar } from './helpers/partner-calendar';
import { openPartnerPage } from './helpers/partner-navigation';

const deferred = () => { let resolve!: () => void; const promise = new Promise<void>(done => { resolve = done; }); return { promise, resolve }; };
const hour = (opensAt: string) => ({ dayOfWeek: 1, opensAt, closesAt: '22:00' });
const price = (startsAt: string, pricePerSlot: number) => ({ dayOfWeek: 1, startsAt, endsAt: '22:00', pricePerSlot });
const anchorId = '019a1234-1111-7111-8111-000000000001';
const occurrenceId = '019a1234-1111-7111-8111-000000000002';
async function fixture(page: Page, draft = false, sessionTime?: Date) {
  const court = (id: string, name: string, opens: string, amount: number) => ({ id, name, status: draft ? 'INACTIVE' : 'ACTIVE', version: 3,
    hours: [hour(opens)], prices: [price(opens, amount)] });
  const business = { id: 'b1', name: 'Thử lịch sân', legalName: 'Test', contact: 'Test', status: draft ? 'DRAFT' : 'ACTIVE', version: 1,
    venues: [{ id: 'v1', name: 'Cơ sở A', status: draft ? 'DRAFT' : 'PUBLISHED', timezone: 'Asia/Ho_Chi_Minh', version: 1,
      courts: [court('c1', 'Sân 1', '07:00', 100000), court('c10', 'Sân 10', '10:00', 150000), court('c2', 'Sân 2', '09:00', 200000)] },
      { id: 'v2', name: 'Cơ sở B', status: draft ? 'DRAFT' : 'PUBLISHED', timezone: 'Asia/Ho_Chi_Minh', version: 1,
        courts: [court('c3', 'Sân 1', '11:00', 300000)] }] };
  const otherBusiness = { ...business, id: 'b2', name: 'Doanh nghiệp khác', venues: [{ ...business.venues[0], id: 'v3', name: 'Cơ sở khác',
    courts: [court('c4', 'Sân 1', '12:00', 700000)] }] };
  const businesses = [business, otherBusiness];
  const state = { calls: [] as Array<{ path: string; method: string; version?: string; body?: any; query: string }>,
    gate: null as ReturnType<typeof deferred> | null, gateStarted: false, stale: false, calendarFailure: 0, generated: 0, seriesNo: 'SR-ONE' };
  await page.route('**/api/v1/**', async route => {
    if (await browserSessionRoute(route, page)) return;
    const req = route.request(), url = new URL(req.url()), path = url.pathname;
    const headers = { 'Access-Control-Allow-Origin': 'http://localhost:5174', 'Access-Control-Allow-Credentials': 'true',
      'Access-Control-Allow-Headers': 'Authorization,Content-Type,If-Match', 'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS' };
    if (req.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    state.calls.push({ path, query: url.search, method: req.method(), body: req.postData() ? req.postDataJSON() : undefined, version: req.headers()['if-match'] });
    const respond = (data: unknown) => route.fulfill({ headers, contentType: 'application/json', body: JSON.stringify({ data }) });
    const fail = (status: number, code: string) => route.fulfill({ status, headers, contentType: 'application/problem+json', body: JSON.stringify({ code }) });
    if (path.endsWith('/partner/login')) return respond({ accessToken: 'schedule-test', ...browserSessionFields(),
      ...(sessionTime ? { idleExpiresAt: new Date(sessionTime.getTime() + 1800000).toISOString(),
        refreshExpiresAt: new Date(sessionTime.getTime() + 30 * 86400000).toISOString() } : {}),
      user: { accountType: 'VENUE_OPERATOR', status: 'ACTIVE' } });
    if (path === '/api/v1/me/notifications/') return respond([]);
    if (path === '/api/v1/partner-onboarding/businesses') return respond(businesses);
    if (path === '/api/v1/partner-onboarding/businesses/b1') return respond(business);
    if (path === '/api/v1/partner-onboarding/businesses/b2') return respond(otherBusiness);
    if (/\/operator\/venues\/[^/]+\/bookings$/.test(path)) return respond({ items: [], nextCursor: null, counts: { awaitingOwnerConfirmation: 0, needsReview: 0 } });
    if (path.endsWith('/calendar')) {
      if (state.calendarFailure) return fail(state.calendarFailure, state.calendarFailure === 403 ? 'FORBIDDEN' : 'TEMPORARY_UNAVAILABLE');
      state.generated++;
      const venue = businesses.flatMap(business => business.venues).find(venue => path.includes(`/venues/${venue.id}/`))!;
      const data = emptyPartnerCalendar(venue, url.searchParams.get('date')!);
      const selected = url.searchParams.get('courtId'); if (selected) data.courts = data.courts.filter(court => court.id === selected);
      const first = data.courts.find(court => court.id === 'c1');
      if (first) {
        const utc = (time: string) => new Date(`${data.date}T${time}:00+07:00`).toISOString();
        (first.events as any[]) = [
          { id: 'event-recurring', kind: 'BOOKING', occurrenceId, paymentAnchorId: anchorId, bookingNo: 'BK-SECOND', seriesNo: state.seriesNo,
            bookingType: 'RECURRING_OCCURRENCE', bookingStatus: 'CONFIRMED', paymentStatus: 'PAID', occurrenceAmount: '400000', paymentTotalAmount: '1600000',
            startsAt: utc('08:00'), endsAt: utc('10:00'), blocksBooking: true, isPast: true },
          { id: 'event-hold', kind: 'QUOTE_HOLD', startsAt: utc('10:00'), endsAt: utc('11:00'), blocksBooking: true, isPast: false },
          { id: 'event-maintenance', kind: 'MAINTENANCE', reason: 'Sửa lưới', status: 'ACTIVE', startsAt: utc('11:00'), endsAt: utc('12:00'), blocksBooking: true, isPast: false },
        ];
        first.slots.forEach(slot => {
          if (slot.startsAt >= '08:00' && slot.startsAt < '10:00') { slot.occupancy = 'BOOKING'; slot.isPast = true; slot.canRequestNewBooking = false; }
          if (slot.startsAt >= '10:00' && slot.startsAt < '11:00') { slot.occupancy = 'QUOTE_HOLD'; slot.canRequestNewBooking = false; }
          if (slot.startsAt >= '11:00' && slot.startsAt < '12:00') { slot.occupancy = 'MAINTENANCE'; slot.canRequestNewBooking = false; }
        });
      }
      const second = data.courts.find(court => court.id === 'c2');
      if (second) {
        (second.events as any[]) = [{ id: 'event-casual', kind: 'BOOKING', occurrenceId: '019a1234-1111-7111-8111-000000000003',
          paymentAnchorId: '019a1234-1111-7111-8111-000000000003', bookingNo: 'BK-CASUAL', bookingType: 'CASUAL',
          bookingStatus: 'AWAITING_TRANSFER', paymentStatus: 'AWAITING_TRANSFER', occurrenceAmount: '800000', paymentTotalAmount: '800000',
          startsAt: new Date(`${data.date}T12:00:00+07:00`).toISOString(), endsAt: new Date(`${data.date}T14:00:00+07:00`).toISOString(),
          blocksBooking: true, isPast: false }];
        second.slots.forEach(slot => { if (slot.startsAt >= '12:00' && slot.startsAt < '14:00') {
          slot.occupancy = 'BOOKING'; slot.canRequestNewBooking = false;
        } });
      }
      return respond(data);
    }
    const found = businesses.flatMap(business => business.venues.flatMap(venue => venue.courts)).find(court => path.includes(`/courts/${court.id}/`));
    if (found && (path.endsWith('/operations') || path.endsWith('/schedule') && req.method() === 'GET')) {
      const data = { ...structuredClone(found), timezone: 'Asia/Ho_Chi_Minh', bookingBlockMinutes: 60, minimumBookingMinutes: 120,
        holdMinutes: 20, basePrices: structuredClone(found.prices), rules: [] };
      if (state.gate && found.id === 'c1' && !state.gateStarted) { state.gateStarted = true; await state.gate.promise; }
      return respond(data);
    }
    if (found && req.method() === 'PUT') {
      if (state.stale) return fail(412, 'PRECONDITION_FAILED');
      const body = req.postDataJSON();
      if (body.hours) {
        found.hours = body.hours; found.prices = body.prices;
        const venue = businesses.flatMap(item => item.venues).find(item => item.courts.includes(found));
        if (found.status === 'INACTIVE' && venue?.status === 'PUBLISHED') found.status = 'ACTIVE';
      }
      found.version++; return respond({ id: found.id, status: found.status, version: found.version });
    }
    if (path.endsWith('/maintenance')) return respond([]);
    if (path.endsWith('/price-preview')) return respond({ totalPrice: 500000, slots: Array.from({ length: 5 }, () => ({ startsAt: '2026-10-20T01:00:00Z', endsAt: '2026-10-20T01:30:00Z', pricePerSlot: 100000, priority: 0 })) });
    return respond({});
  });
  return { state, business };
}
async function login(page: Page, hash = '#/schedule') {
  await page.goto(`http://localhost:5174/${hash}`);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).first().click();
  await page.getByLabel('Email', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Test-password-2026!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.locator('.operations-page')).toBeVisible();
}
const workspace = (page: Page) => page.locator('.operations-page');
const pricing = (page: Page) => workspace(page).getByRole('tab', { name: 'Giờ & giá', exact: true }).click();

test('draft editor hydrates each court and saves only selected scope/version', async ({ page }) => {
  const { state } = await fixture(page, true); await login(page);
  const scope = workspace(page);
  await expect(page.getByLabel('Mở cửa Thứ hai', { exact: true })).toHaveValue('07:00');
  await scope.getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await expect(page.getByLabel('Mở cửa Thứ hai', { exact: true })).toHaveValue('09:00');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
  await scope.getByRole('combobox', { name: 'Cơ sở', exact: true }).selectOption('v2');
  await expect(page.getByLabel('Mở cửa Thứ hai', { exact: true })).toHaveValue('11:00');
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect.poll(() => state.calls.filter(call => call.method === 'PUT').length).toBe(1);
  expect(state.calls.find(call => call.method === 'PUT')).toMatchObject({ path: '/api/v1/partner-onboarding/courts/c3/schedule', version: '"3"',
    body: { hours: [hour('11:00')], prices: [price('11:00', 300000)] } });
});

test('court options use numeric order and scope survives Back and reload', async ({ page }) => {
  await fixture(page); await login(page); await pricing(page);
  const courts = workspace(page).getByRole('combobox', { name: 'Sân', exact: true });
  expect(await courts.locator('option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value))).toEqual(['c1', 'c2', 'c10']);
  await courts.selectOption('c2'); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
  await courts.selectOption('c10'); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('150000');
  await page.goBack(); await expect(courts).toHaveValue('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
  await page.reload(); await expect(courts).toHaveValue('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
});

test('special price uses one date range for the selected court without weekday or priority inputs', async ({ page }) => {
  const { state } = await fixture(page); await login(page); await pricing(page);
  const special = page.locator('#operator-pricing');
  await page.locator('details:has(#operator-pricing) > summary').click();
  await expect(special.getByRole('combobox', { name: /^Thứ/ })).toHaveCount(0);
  await expect(special.getByLabel('Ưu tiên')).toHaveCount(0);
  await special.getByRole('button', { name: 'Thiết lập giá đặc biệt' }).click();
  await special.getByLabel('Hiệu lực từ').fill('2026-11-02');
  await special.getByLabel('Đến ngày').fill('2026-11-08');
  await special.getByLabel('Từ giờ').fill('08:00');
  await special.getByLabel('Đến giờ').fill('10:00');
  await special.getByLabel('VND / 30 phút').fill('120000');
  await special.getByRole('button', { name: 'Lưu giá đặc biệt' }).click();
  await expect.poll(() => state.calls.filter(call => call.path.endsWith('/special-price')).length).toBe(1);
  expect(state.calls.find(call => call.path.endsWith('/special-price'))).toMatchObject({
    method: 'PUT', version: '"3"', body: { specialPrice: { startsOn: '2026-11-02', endsOn: '2026-11-08',
      startsAt: '08:00', endsAt: '10:00', pricePerSlot: 120000 } },
  });
});

test('owner publishes draft by saving schedule and sees synchronized status', async ({ page }) => {
  const { state, business } = await fixture(page, true);
  business.status = 'ACTIVE'; business.venues[0].status = 'PUBLISHED';
  await login(page, '#/schedule?tab=pricing&venueId=v1&courtId=c1');
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Xác nhận sân', exact: true })).toHaveCount(0);
  await expect(workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).locator('option[value="c1"]')).toContainText('Đang hoạt động');
  expect(state.calls.find(call => call.path.endsWith('/c1/schedule') && call.method === 'PUT')).toMatchObject({ version: '"3"' });
  expect(state.calls.filter(call => call.path.endsWith('/submit'))).toEqual([]);
});

test('suspended business keeps draft configuration read-only even in a published venue', async ({ page }) => {
  const { state, business } = await fixture(page, true);
  business.status = 'SUSPENDED'; business.venues[0].status = 'PUBLISHED';
  await login(page, '#/schedule?tab=pricing&venueId=v1&courtId=c1');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('100000');
  await expect(page.getByRole('button', { name: 'Lưu lịch tuần', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Xác nhận sân', exact: true })).toHaveCount(0);
  expect(state.calls.filter(call => call.method === 'PUT')).toEqual([]);
});

test('unsaved edits require confirmation before changing court or tab', async ({ page }) => {
  await fixture(page); await login(page); await pricing(page);
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000');
  page.once('dialog', dialog => dialog.dismiss());
  await workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await expect(workspace(page).getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c1');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  page.once('dialog', dialog => dialog.dismiss());
  await workspace(page).getByRole('tab', { name: 'Lịch sân', exact: true }).click();
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  page.once('dialog', dialog => dialog.accept());
  await workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
});

test('late A response cannot overwrite newer A after A B A selection', async ({ page }) => {
  const { state, business } = await fixture(page);
  state.gate = deferred();
  await login(page, '#/schedule?tab=pricing&venueId=v1&courtId=c1');
  await expect.poll(() => state.gateStarted).toBe(true);
  await workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
  business.venues[0].courts[0].prices[0].pricePerSlot = 333000;
  business.venues[0].courts[0].version = 9;
  await workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c1');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('333000');
  state.gate.resolve();
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect.poll(() => state.calls.filter(call => call.method === 'PUT').length).toBe(1);
  expect(state.calls.find(call => call.method === 'PUT')).toMatchObject({ version: '"9"', body: { prices: [price('07:00', 333000)] } });
});

test('business switch discards prior private scope only after accepting unsaved guard', async ({ page }) => {
  await fixture(page); await login(page); await pricing(page);
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000');
  const business = page.getByRole('combobox', { name: 'Doanh nghiệp', exact: true });
  page.once('dialog', dialog => dialog.dismiss()); await business.selectOption('b2');
  await expect(business).toHaveValue('b1'); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  page.once('dialog', dialog => dialog.accept()); await business.selectOption('b2');
  await expect(page.locator('.status-banner')).toContainText('Doanh nghiệp khác');
  await pricing(page); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('700000');
  await expect(workspace(page).getByRole('combobox', { name: 'Cơ sở', exact: true })).toHaveValue('v3');
  await expect(workspace(page).getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c4');
});

test('cancelled Back and page exit preserve accepted court scope and unsaved editor', async ({ page }) => {
  await fixture(page); await login(page); await pricing(page);
  await workspace(page).getByRole('combobox', { name: 'Sân', exact: true }).selectOption('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('200000');
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000');
  const acceptedUrl = page.url();
  page.once('dialog', dialog => dialog.dismiss()); await page.goBack();
  await expect(page).toHaveURL(acceptedUrl);
  await expect(workspace(page).getByRole('combobox', { name: 'Sân', exact: true })).toHaveValue('c2');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  page.once('dialog', dialog => dialog.dismiss()); await openPartnerPage(page, 'Đơn đặt sân');
  await expect(page).toHaveURL(acceptedUrl); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  page.once('dialog', dialog => dialog.accept()); await openPartnerPage(page, 'Đơn đặt sân');
  await expect(page).toHaveURL(/#\/bookings$/); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveCount(0);
});

test('stale save preserves editor and blocks retry until deliberate fresh reload', async ({ page }) => {
  const { state, business } = await fixture(page); await login(page); await pricing(page);
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000'); state.stale = true;
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  await expect(page.getByRole('button', { name: 'Lưu lịch tuần', exact: true })).toBeDisabled();
  state.stale = false; business.venues[0].courts[0].version = 8;
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Tải lại cấu hình', exact: true }).click();
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('100000');
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect.poll(() => state.calls.filter(call => call.method === 'PUT').map(call => call.version)).toEqual(['"3"', '"8"']);
});

test('saving policy preserves unsaved weekly prices and uses new version for subsequent save', async ({ page }, info) => {
  const { state } = await fixture(page); await login(page); await pricing(page);
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000');
  await page.locator('details:has(#operator-policy) > summary').click();
  await page.locator('#operator-policy').getByLabel('Giữ chỗ trước khi báo chuyển khoản').fill('25');
  await page.getByRole('button', { name: 'Lưu quy định', exact: true }).click();
  await expect(page.locator('.partner-toast--success')).toContainText('Đã lưu');
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
  await expect(workspace(page)).toContainText('Có thay đổi chưa lưu');
  await page.getByRole('button', { name: 'Lưu lịch tuần', exact: true }).click();
  await expect.poll(() => state.calls.filter(call => call.method === 'PUT').map(call => call.version)).toEqual(['"3"', '"4"']);
  expect(state.calls.filter(call => call.method === 'PUT')[1].body.prices).toEqual([price('07:00', 123000)]);
  await page.screenshot({ path: info.outputPath('partner-pricing.png'), fullPage: true });
});

test('weekly price gap is explained next to the day and cannot send a write until corrected', async ({ page }) => {
  const { state } = await fixture(page); await login(page); await pricing(page);
  const monday = page.locator('.weekly-day').filter({ has: page.getByLabel('Mở Thứ hai', { exact: true }) });
  await monday.getByLabel('Đến Thứ hai', { exact: true }).fill('20:00');
  const save = page.getByRole('button', { name: 'Lưu lịch tuần', exact: true });
  if (await save.isEnabled()) await save.click();
  await expect(monday.getByRole('alert')).toContainText(/phủ|hở|thiếu|liên tục/i);
  await expect(monday.getByLabel('Đến Thứ hai', { exact: true })).toHaveValue('20:00');
  await expect(monday.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('100000');
  expect(state.calls.filter(call => call.method === 'PUT')).toEqual([]);
  await monday.getByLabel('Đến Thứ hai', { exact: true }).fill('22:00');
  await expect(monday.getByRole('alert')).toHaveCount(0);
  await save.click();
  await expect.poll(() => state.calls.filter(call => call.method === 'PUT').length).toBe(1);
  expect(state.calls.find(call => call.method === 'PUT')).toMatchObject({ version: '"3"', body: { prices: [price('07:00', 100000)] } });
});

test('calendar shows fixed occurrence/payment separately and links payment anchor', async ({ page }, info) => {
  const { state } = await fixture(page); state.seriesNo = 'SR26101201a131dc5a67707d9a0c527296dfb7ea';
  await login(page, '#/schedule?tab=calendar&venueId=v1&date=2026-10-20');
  const calendar = page.getByRole('region', { name: 'Lịch sân theo ngày' });
  await expect(calendar).toContainText('Cố định'); await expect(calendar).toContainText('Đã xác nhận');
  await expect(calendar).toContainText('Vãng lai'); await expect(calendar).not.toContainText('AWAITING_TRANSFER');
  await expect(calendar).toContainText('Giữ báo giá'); await expect(calendar).toContainText('Sửa lưới');
  const booking = calendar.getByRole('link', { name: /BK-SECOND/ }).first();
  await expect(booking).toHaveText('Có đơn');
  await expect(page.locator('.calendar-grid').getByText('BK-SECOND')).toHaveCount(0);
  await expect(booking).toHaveAttribute('href', `#/bookings/${anchorId}`);
  await expect(booking).not.toHaveAttribute('href', `#/bookings/${occurrenceId}`);
  const detailLink = calendar.getByRole('link', { name: `Xem đơn ${state.seriesNo}`, exact: true });
  await expect(detailLink).toHaveText('Xem đơn');
  await expect(detailLink).toHaveAttribute('href', `#/bookings/${anchorId}`);
  expect(await detailLink.evaluate(link => {
    const button = link.getBoundingClientRect(), panel = link.closest('.panel')!.getBoundingClientRect();
    return button.left >= panel.left && button.right <= panel.right;
  })).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('partner-calendar.png'), fullPage: true });
});

test('calendar refresh failure retains stale data while denied scope purges private events', async ({ page }) => {
  const { state } = await fixture(page); await login(page);
  await expect(page.getByRole('link', { name: /BK-SECOND/ }).first()).toBeVisible();
  state.calendarFailure = 503;
  await page.getByRole('button', { name: 'Tải lại lịch', exact: true }).click();
  await expect(page.getByRole('link', { name: /BK-SECOND/ }).first()).toBeVisible();
  await expect(page.getByRole('alert').filter({ hasText: /Chưa cập nhật/ })).toBeVisible();
  state.calendarFailure = 403;
  await page.getByRole('button', { name: 'Tải lại lịch', exact: true }).click();
  await expect(page.getByRole('link', { name: /BK-SECOND/ })).toHaveCount(0);
});

test('calendar polls only visible active tab and never overwrites dirty pricing', async ({ page }) => {
  const { state } = await fixture(page); await page.clock.install(); await login(page);
  await expect.poll(() => state.generated).toBeGreaterThan(0);
  const initial = state.generated; await page.clock.fastForward(16000);
  await expect.poll(() => state.generated).toBeGreaterThan(initial);
  await pricing(page); await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('100000');
  await page.getByLabel('Giá Thứ hai', { exact: true }).fill('123000');
  const stopped = state.generated; await page.clock.fastForward(31000);
  expect(state.generated).toBe(stopped);
  await expect(page.getByLabel('Giá Thứ hai', { exact: true })).toHaveValue('123000');
});

test('terminal 401 clears private calendar instead of retaining previous session events', async ({ page }) => {
  await fixture(page); await login(page);
  await expect(page.getByRole('link', { name: /BK-SECOND/ }).first()).toBeVisible();
  await page.route('**/api/v1/browser-auth/partner/restore', route => route.fulfill({ status: 401,
    contentType: 'application/problem+json', body: JSON.stringify({ code: 'INVALID_REFRESH_TOKEN' }) }));
  await page.route('**/calendar?**', route => route.fulfill({ status: 401,
    contentType: 'application/problem+json', body: JSON.stringify({ code: 'UNAUTHORIZED' }) }));
  await page.getByRole('button', { name: 'Tải lại lịch', exact: true }).click();
  await expect(page.getByRole('link', { name: /BK-SECOND/ })).toHaveCount(0);
  await expect(page.locator('.operations-page')).toHaveCount(0);
});

test('default calendar date follows Vietnam midnight rather than UTC date', async ({ page }) => {
  const now = new Date('2026-10-12T18:30:00Z');
  const { state } = await fixture(page, false, now);
  await page.clock.install({ time: now });
  await login(page);
  await expect.poll(() => state.calls.filter(call => call.path.endsWith('/calendar')).map(call => new URLSearchParams(call.query).get('date')))
    .toContain('2026-10-13');
  await expect(page.getByText('Múi giờ: Asia/Ho_Chi_Minh', { exact: false })).toHaveCount(0);
});

test('150 minute preview sends no quote/hold write and clears failed previous result', async ({ page }) => {
  const { state } = await fixture(page); await login(page); await pricing(page);
  const preview = page.locator('#operator-preview');
  await preview.getByLabel('Từ', { exact: true }).fill('08:00'); await preview.getByLabel('Đến', { exact: true }).fill('10:30');
  await preview.getByRole('button', { name: 'Xem giá', exact: true }).click();
  await expect(preview).toContainText('500.000');
  expect(state.calls.filter(call => call.method !== 'GET' && !call.path.includes('browser-auth'))).toEqual([]);
  await page.route('**/price-preview?**', route => route.fulfill({ status: 400, contentType: 'application/problem+json', body: JSON.stringify({ code: 'VALIDATION_FAILED' }) }));
  await preview.getByRole('button', { name: 'Xem giá', exact: true }).click();
  await expect(preview).not.toContainText('500.000');
});
