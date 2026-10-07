import { expect, test } from '@playwright/test';

const venueId = '0199f040-0000-7000-8000-000000000001';
const courtA = '0199f040-0000-7000-8000-000000000002';
const courtB = '0199f040-0000-7000-8000-000000000003';
const courtC = '0199f040-0000-7000-8000-000000000004';

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
          { courtId: courtC, name: 'Sân 3', bookingBlockMinutes: 90, minimumBookingMinutes: 90, holdMinutes: 20,
            slots: [slot('17:00', '17:30', 'AVAILABLE', 90000), slot('17:30', '18:00', 'AVAILABLE', 90000),
              slot('18:00', '18:30', 'AVAILABLE', 90000)] },
        ] });
    }
    if (url.pathname === `/api/v1/venues/${venueId}`) return json({ id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội',
      contact: 'Liên hệ cơ sở', latitude: 21.0278, longitude: 105.8342, timezone: 'Asia/Ho_Chi_Minh',
      imageUrl: null, courts: [{ id: courtA, name: 'Sân 1', bookingBlockMinutes: 30, minimumBookingMinutes: 60,
        holdMinutes: 20 }, { id: courtB, name: 'Sân 2', bookingBlockMinutes: 60, minimumBookingMinutes: 60,
        holdMinutes: 20 }, { id: courtC, name: 'Sân 3', bookingBlockMinutes: 90, minimumBookingMinutes: 90,
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
  await expect(page.getByRole('columnheader', { name: '17:00 đến 17:30, 30 phút' })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 1/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 2/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 3/ })).toBeVisible();
  await expect(page.locator('.schedule-grid tbody tr')).toHaveCount(3);
  await expect(page.getByLabel('Xem sân')).toHaveCount(0);
  const boundaries = await page.locator('.schedule-grid').evaluate(table => {
    const headings = [...table.querySelectorAll<HTMLElement>('.time-heading')];
    const cells = [...table.querySelectorAll<HTMLElement>('tbody tr:first-child .slot-cell')];
    return {
      firstStart: headings[0].querySelector<HTMLElement>('.time-start')!.getBoundingClientRect().left,
      nextStart: headings[1].querySelector<HTMLElement>('.time-start')!.getBoundingClientRect().left,
      firstCellLeft: cells[0].getBoundingClientRect().left,
      firstCellRight: cells[0].getBoundingClientRect().right,
      finalEnd: headings[headings.length - 1].querySelector<HTMLElement>('.time-end')!.getBoundingClientRect().right,
      finalCellRight: cells[cells.length - 1].getBoundingClientRect().right,
    };
  });
  expect(Math.abs(boundaries.firstStart - boundaries.firstCellLeft)).toBeLessThan(12);
  expect(Math.abs(boundaries.nextStart - boundaries.firstCellRight)).toBeLessThan(12);
  expect(Math.abs(boundaries.finalEnd - boundaries.finalCellRight)).toBeLessThan(12);
  await expect(page.locator('.slot-label').filter({ hasText: 'Đã kín' })).toBeVisible();
  await expect(page.locator('.slot-label').filter({ hasText: 'Chưa có giá' })).toBeVisible();
  await page.getByRole('button', { name: /Sân 1, 17:00 đến 17:30/ }).click();
  await page.getByRole('button', { name: /Sân 1, 17:30 đến 18:00/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 250.000đ' })).toBeVisible();
  await page.getByRole('button', { name: /Sân 1, 17:30 đến 18:00/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 100.000đ' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Sân 1, 17:00 đến 17:30/ })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: /Sân 1, 17:30 đến 18:00/ }).click();
  await page.getByRole('button', { name: /Sân 1, 17:00 đến 17:30/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 150.000đ' })).toBeVisible();
  await page.getByRole('button', { name: /Sân 3, 17:00 đến 17:30/ }).click();
  await page.getByRole('button', { name: /Sân 3, 18:00 đến 18:30/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 270.000đ' })).toBeVisible();
  await page.getByRole('button', { name: /Sân 3, 17:30 đến 18:00/ }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Giá tham khảo 90.000đ' })).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('f04-grid.png'), fullPage: true });
  const initialDate = await page.getByLabel('Ngày chơi').inputValue();
  const following = new Date(`${initialDate}T00:00:00Z`);
  following.setUTCDate(following.getUTCDate() + 1);
  const nextDate = following.toISOString().slice(0, 10);
  await page.getByLabel('Ngày chơi').fill(nextDate);
  await expect(page).toHaveURL(new RegExp(`date=${nextDate}`));
  await page.goBack();
  await expect(page.getByLabel('Ngày chơi')).toHaveValue(initialDate);
  await expect(page.getByRole('rowheader', { name: /Sân 1/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 2/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Sân 3/ })).toBeVisible();
  const overflow = await page.locator('.schedule-scroll').evaluate(element => element.scrollWidth > element.clientWidth);
  if ((page.viewportSize()?.width ?? 1280) < 630) expect(overflow).toBeTruthy();
  await page.getByRole('button', { name: /Sân 1, 17:00 đến 17:30/ }).click();
  await page.getByRole('button', { name: /Sân 1, 17:30 đến 18:00/ }).click();
  await page.getByRole('button', { name: 'Tiếp tục đặt vãng lai' }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  const returnTo = new URL(page.url()).searchParams.get('returnTo')!;
  const review = new URL(returnTo, 'http://localhost:5173');
  expect(review.pathname).toBe('/booking-review');
  expect(review.searchParams.get('courtId')).toBe(courtA);
  expect(review.searchParams.get('date')).toBe(initialDate);
  expect(review.searchParams.get('startsAt')).toBe('17:00');
  expect(review.searchParams.get('endsAt')).toBe('18:00');
  expect(bookingRequests).toBe(0);
});

test('grid shows all seven courts without a court filter', async ({ page }) => {
  const courts = Array.from({ length: 7 }, (_, index) => ({
    id: `0199f040-0000-7000-8000-00000000001${index}`,
    name: `Sân ${index + 1}`, bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20,
  }));
  await page.route('**/api/v1/venues/**', route => {
    const url = new URL(route.request().url());
    const data = url.pathname.endsWith('/availability')
      ? { date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30,
        courts: courts.map(court => ({ courtId: court.id, name: court.name,
          bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20,
          slots: [slot('17:00', '17:30', 'AVAILABLE', 80000)] })) }
      : { id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội', contact: 'Liên hệ cơ sở',
        latitude: 21.0278, longitude: 105.8342, timezone: 'Asia/Ho_Chi_Minh', imageUrl: null, courts };
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
  });
  await page.goto(`http://localhost:5173/venues/${venueId}`);
  await expect(page.locator('.schedule-grid tbody tr')).toHaveCount(7);
  await expect(page.getByLabel('Xem sân')).toHaveCount(0);
});

for (const policy of [{ block: 60, minimum: 120 }, { block: 90, minimum: 90 }]) {
  test(`casual selection accepts extra half hours after minimum with block ${policy.block}`, async ({ page }) => {
    const time = (minutes: number) => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
    const slots = Array.from({ length: 7 }, (_, index) => slot(time(1020 + index * 30), time(1050 + index * 30), 'AVAILABLE', 100000));
    let bookingRequests = 0;
    await page.route('**/api/v1/bookings**', route => { bookingRequests++; return route.abort(); });
    await page.route('**/api/v1/venues/**', route => {
      const url = new URL(route.request().url());
      const court = { courtId: courtA, name: 'Sân 1', bookingBlockMinutes: policy.block, minimumBookingMinutes: policy.minimum, holdMinutes: 20, slots };
      const data = url.pathname.endsWith('/availability')
        ? { venueId, date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30, courts: [court] }
        : { id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội', contact: 'Liên hệ cơ sở', latitude: 21.0278,
          longitude: 105.8342, timezone: 'Asia/Ho_Chi_Minh', imageUrl: null, courts: [{ ...court, id: courtA }] };
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
    });
    await page.goto(`http://localhost:5173/venues/${venueId}`);
    const selectSlot = (index: number) => page.getByRole('button', { name: new RegExp(`Sân 1, ${time(1020 + index * 30)} đến ${time(1050 + index * 30)}`) }).click();
    const minimumSlots = policy.minimum / 30;
    await selectSlot(0);
    await selectSlot(minimumSlots - 2);
    const continueBooking = page.getByRole('button', { name: 'Tiếp tục đặt vãng lai' });
    const summary = page.locator('.selection-summary');
    await expect(continueBooking).toBeDisabled();
    await expect(summary).toContainText(`tối thiểu ${policy.minimum} phút`);

    for (let count = minimumSlots; count <= 7; count++) {
      await selectSlot(count - 1);
      await expect(summary).toContainText(`${count * 30} phút`);
      await expect(summary).toContainText(`Giá tham khảo ${new Intl.NumberFormat('vi-VN').format(count * 100000)}đ`);
      await expect(continueBooking).toBeEnabled();
    }

    // Remove the last two slots; five contiguous slots remain valid, and the
    // handoff keeps their full interval instead of rounding to the block.
    await selectSlot(6);
    await selectSlot(5);
    await expect(summary).toContainText('150 phút');
    await expect(continueBooking).toBeEnabled();
    await continueBooking.click();
    await expect(page.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
    const review = new URL(new URL(page.url()).searchParams.get('returnTo')!, 'http://localhost:5173');
    expect(review.pathname).toBe('/booking-review');
    expect(review.searchParams.get('courtId')).toBe(courtA);
    expect(review.searchParams.get('startsAt')).toBe('17:00');
    expect(review.searchParams.get('endsAt')).toBe('19:30');
    expect(bookingRequests).toBe(0);
  });
}

test('full-day schedule keeps half-hour columns readable while scrolling', async ({ page }) => {
  if ((page.viewportSize()?.width ?? 1280) > 630) await page.setViewportSize({ width: 860, height: 900 });
  const time = (minutes: number) => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
  const slots = Array.from({ length: 34 }, (_, index) => slot(time(300 + index * 30), time(330 + index * 30),
    index < 3 ? 'RESERVED' : index === 20 ? 'NO_PRICE' : 'AVAILABLE', index === 20 ? null : index === 10 ? 1000000 : 40000));
  await page.route('**/api/v1/venues/**', route => {
    const url = new URL(route.request().url());
    const court = { courtId: courtA, name: 'Sân 1', bookingBlockMinutes: 60, minimumBookingMinutes: 120, holdMinutes: 20, slots };
    const data = url.pathname.endsWith('/availability')
      ? { venueId, date: url.searchParams.get('date'), timezone: 'Asia/Ho_Chi_Minh', stepMinutes: 30, courts: [court] }
      : { id: venueId, name: 'Hoàng Cầu', address: 'Hà Nội', contact: 'Liên hệ cơ sở', latitude: 21.0278,
        longitude: 105.8342, timezone: 'Asia/Ho_Chi_Minh', imageUrl: null, courts: [{ ...court, id: courtA }] };
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data }) });
  });
  await page.goto(`http://localhost:5173/venues/${venueId}`);
  await expect(page.locator('.slot-cell')).toHaveCount(34);
  const geometry = await page.locator('.schedule-grid').evaluate(table => {
    const cells = [...table.querySelectorAll<HTMLElement>('.slot-cell')];
    const headings = [...table.querySelectorAll<HTMLElement>('.time-heading')];
    return {
      widths: cells.map(cell => cell.getBoundingClientRect().width),
      labelsFit: headings.every(heading => {
        const label = heading.querySelector<HTMLElement>('.time-start')!.getBoundingClientRect();
        const bounds = heading.getBoundingClientRect();
        return label.left >= bounds.left && label.right <= bounds.right;
      }),
      pricesFit: cells.every(cell => {
        const price = cell.querySelector<HTMLElement>('button small');
        return !price || price.scrollWidth <= price.clientWidth;
      }),
    };
  });
  expect(Math.min(...geometry.widths)).toBeGreaterThanOrEqual(104);
  expect(geometry.labelsFit).toBe(true);
  expect(geometry.pricesFit).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath('f04-full-day-start.png'), fullPage: true });
  await page.locator('.schedule-scroll').evaluate(element => { element.scrollLeft = element.scrollWidth; });
  const sticky = await page.locator('.schedule-scroll').evaluate(element => ({
    left: element.getBoundingClientRect().left,
    court: element.querySelector<HTMLElement>('tbody .court-heading')!.getBoundingClientRect().left,
    end: element.querySelector<HTMLElement>('.time-end')!.getBoundingClientRect().right,
    right: element.getBoundingClientRect().right,
  }));
  expect(Math.abs(sticky.court - sticky.left)).toBeLessThan(3);
  expect(sticky.end).toBeLessThanOrEqual(sticky.right);
  expect(sticky.end).toBeGreaterThan(sticky.left);
  await expect(page.locator('.time-end')).toHaveText('22:00');
  await page.screenshot({ path: test.info().outputPath('f04-full-day-end.png'), fullPage: true });
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
  await expect.poll(() => nearbyUrl).toContain('/nearby?');
  const params = new URL(nearbyUrl).searchParams;
  expect(params.get('latitude')).toBe('21.0278');
  expect(params.get('longitude')).toBe('105.8342');
  expect(params.get('radiusMeters')).toBe('10000');
});

function slot(startsAt: string, endsAt: string, status: string, pricePerSlot: number | null) {
  return { startsAt, endsAt, startsAtUtc: '2026-10-12T10:00:00Z', endsAtUtc: '2026-10-12T10:30:00Z',
    status, pricePerSlot };
}
