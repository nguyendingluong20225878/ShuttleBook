import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { openPartnerPage, partnerLogout } from './helpers/partner-navigation';

const adminContact = process.env.SHUTTLEBOOK_ADMIN_TEST_CONTACT;
const adminPassword = process.env.SHUTTLEBOOK_ADMIN_TEST_PASSWORD;
const api = process.env.SHUTTLEBOOK_TEST_API_URL ?? 'http://localhost:5080';
test.skip(process.env.SHUTTLEBOOK_E2E_REAL !== '1' || !adminContact || !adminPassword,
  'Requires disposable PostgreSQL/PostGIS, API, Mailpit and an Admin test account.');
test.use({ trace: 'off', actionTimeout: 20_000 });

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==', 'base64');

async function verificationCode(contact: string): Promise<string | null> {
  const response = await fetch('http://127.0.0.1:8025/api/v1/messages');
  if (!response.ok) throw new Error('Mailpit unavailable');
  const list = await response.json() as { messages: Array<{ ID: string; To: Array<{ Address: string }> }> };
  const entry = list.messages.find(message => message.To.some(to => to.Address === contact));
  if (!entry) return null;
  const detail = await fetch(`http://127.0.0.1:8025/api/v1/message/${entry.ID}`);
  const message = await detail.json() as { Text: string };
  return message.Text.match(/\b\d{6}\b/)?.[0] ?? null;
}

test('F02–F06 onboarding, court operations and payment reconciliation through real browser, API, Worker and PostGIS', async ({ page, context }) => {
  test.setTimeout(240_000);
  expect((await fetch(`${api}/health/ready`)).status).toBe(200);
  await context.route('**/*', route => {
    const url = new URL(route.request().url()); const origin = url.origin;
    if (url.pathname.startsWith('/api/v1/') && origin !== new URL(api).origin)
      throw new Error(`Browser API origin ${origin} differs from test API ${new URL(api).origin}.`);
    return ['http://localhost:5173', 'http://localhost:5174', 'http://localhost:5175', new URL(api).origin].includes(origin)
      ? route.continue() : route.abort();
  });
  // This live flow exercises browser → API → PostGIS. MapTiler responses are stubbed
  // here; the provider itself is smoke-tested separately with a real restricted key.
  await page.addInitScript(() => {
    (window as unknown as { maptilersdk: unknown }).maptilersdk = {
      config: { apiKey: '' }, Map: class { setCenter() {} setZoom() {} remove() {} },
      Marker: class { setLngLat() { return this; } addTo() { return this; } remove() {} },
    };
  });
  await page.route('https://api.maptiler.com/geocoding/**', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ type: 'FeatureCollection', features: [{ type: 'Feature',
      place_name: '31 ngõ 16 Hoàng Cầu, Hà Nội',
      geometry: { type: 'Point', coordinates: [105.8236, 21.0186] }, center: [105.8236, 21.0186] }] }),
  }));
  const contact = `f02-live-${randomUUID()}@example.test`;
  const password = `F02-${randomUUID()}!Aa1`;
  const clubName = `F02 Club ${randomUUID().slice(0, 8)}`;
  const venueName = `F02 Venue ${randomUUID().slice(0, 8)}`;

  await page.goto('http://localhost:5174');
  await page.getByLabel('Email', { exact: true }).fill(contact);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByLabel('Nhập lại mật khẩu').fill(password);
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Xác minh liên hệ' })).toBeVisible();
  await expect.poll(() => verificationCode(contact)).not.toBeNull();
  await page.getByLabel('Mã xác minh 6 chữ số').fill((await verificationCode(contact))!);
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập chủ sân' })).toBeVisible();
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Tạo doanh nghiệp' })).toBeVisible();

  const business = page.getByRole('heading', { name: 'Tạo doanh nghiệp' }).locator('..');
  await business.getByLabel('Tên hiển thị').fill(clubName);
  await business.getByLabel('Tên pháp lý').fill('F02 Club Company');
  await business.getByLabel('Liên hệ').fill('Test contact');
  await business.getByRole('button', { name: 'Tạo hồ sơ nháp' }).click();
  await expect(page.getByRole('heading', { name: 'Thêm cơ sở' })).toBeVisible();

  const venue = page.getByRole('heading', { name: 'Thêm cơ sở' }).locator('..');
  await venue.getByLabel('Tên', { exact: true }).fill(venueName);
  await venue.getByPlaceholder('Nhập địa chỉ, ví dụ: 31 ngõ 16 Hoàng Cầu - Hà Nội').fill('31 ngõ 16 Hoàng Cầu - Hà Nội');
  await page.getByRole('button', { name: '31 ngõ 16 Hoàng Cầu, Hà Nội' }).click();
  await venue.getByRole('button', { name: 'Xác nhận vị trí này' }).click();
  await venue.getByLabel('Liên hệ cơ sở').fill('Test venue contact');
  await venue.getByRole('button', { name: 'Lưu cơ sở' }).click();
  await expect(page.getByRole('heading', { name: `${venueName} · Hồ sơ nháp` })).toBeVisible();
  await openPartnerPage(page, 'Sân');

  const court = page.getByRole('heading', { name: 'Thêm sân' }).locator('..');
  await court.getByLabel('Cơ sở').selectOption({ label: venueName });
  await court.getByLabel('Tên sân').fill('F02 Court');
  await court.getByRole('button', { name: 'Lưu sân' }).click();
  await expect(page.getByText('Sân F02 Court:')).toBeVisible();
  await openPartnerPage(page, 'Lịch & giá');
  const schedule = page.getByRole('heading', { name: 'Giờ và giá theo sân' }).locator('..');
  await schedule.locator('select').first().selectOption({ label: `${venueName} / F02 Court` });
  await schedule.getByRole('button', { name: 'Lưu giờ/giá' }).click();
  await openPartnerPage(page, 'Sân');
  await expect(page.getByText('Sân F02 Court: 1 ngày mở cửa, 1 khung giá')).toBeVisible();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.getByText('Yêu cầu không thành công (INCOMPLETE_PROFILE).')).toBeVisible();
  await openPartnerPage(page, 'Ảnh cơ sở');

  const image = page.getByRole('heading', { name: 'Ảnh cơ sở' }).locator('..');
  await image.locator('select').selectOption({ label: venueName });
  await image.getByLabel('Ảnh cơ sở').setInputFiles({ name: 'venue.png', mimeType: 'image/png', buffer: png });
  await image.getByRole('button', { name: 'Tải ảnh' }).click();
  await expect(page.getByText('Ảnh: Đã tải')).toBeVisible();
  await openPartnerPage(page, 'Thanh toán & QR');
  const payment = page.getByRole('heading', { name: 'Tài khoản nhận tiền' }).locator('..');
  await payment.locator('select').selectOption({ label: venueName });
  await payment.getByLabel('Mã ngân hàng').fill('TEST');
  await payment.getByLabel('Tên tài khoản').fill('F02 CLUB');
  await payment.getByLabel('Số tài khoản').fill('1234567890');
  await payment.getByLabel('Ảnh QR').setInputFiles({ name: 'qr.png', mimeType: 'image/png', buffer: png });
  await payment.getByRole('button', { name: 'Lưu QR và tài khoản' }).click();
  await expect(page.getByText('QR: Đã khai báo')).toBeVisible();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.locator('.status-banner').getByText('Đang chờ duyệt', { exact: true })).toBeVisible();

  const admin = await context.newPage();
  await admin.goto('http://localhost:5175');
  await admin.getByLabel('Email', { exact: true }).fill(adminContact!);
  await admin.getByLabel('Mật khẩu').fill(adminPassword!);
  await admin.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await admin.getByRole('button', { name: new RegExp(clubName) }).click();
  await expect(admin.getByRole('heading', { name: clubName })).toBeVisible();
  await admin.getByLabel('Lý do cần chỉnh sửa').fill('Please correct the legal name');
  await admin.getByRole('button', { name: 'Yêu cầu chỉnh sửa' }).click();
  await expect(admin.getByRole('button', { name: new RegExp(clubName) })).toHaveCount(0);

  await partnerLogout(page);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByText('Admin yêu cầu bổ sung: Please correct the legal name')).toBeVisible();
  await openPartnerPage(page, 'Hồ sơ doanh nghiệp');
  const edit = page.getByRole('heading', { name: 'Thông tin doanh nghiệp' }).locator('..');
  await edit.getByLabel('Tên pháp lý').fill('F02 Club Company Updated');
  await edit.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.locator('.status-banner').getByText('Đang chờ duyệt', { exact: true })).toBeVisible();
  await admin.getByRole('button', { name: 'Tải lại danh sách' }).click();
  await admin.getByRole('button', { name: new RegExp(clubName) }).click();
  await admin.getByRole('button', { name: 'Phê duyệt' }).click();
  await expect(admin.getByRole('button', { name: new RegExp(clubName) })).toHaveCount(0);

  await partnerLogout(page);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.locator('.status-banner').getByText('Đang hoạt động', { exact: true })).toBeVisible();
  await openPartnerPage(page, 'Lịch & giá');
  await expect(page.getByRole('heading', { name: 'Vận hành sân' })).toBeVisible();
  await expect(page.getByText('F02 Court · Đang hoạt động · múi giờ Asia/Ho_Chi_Minh')).toBeVisible();
  const updatedSchedule = page.waitForResponse(response => response.url().endsWith('/operations') && response.request().method() === 'GET');
  await page.getByRole('button', { name: 'Lưu lịch tuần' }).click();
  expect((await (await updatedSchedule).json()).data.version).toBe(2);
  await expect(page.locator('.operations-page').getByRole('status')).toHaveText('Đã lưu.');

  const future = new Date();
  future.setUTCDate(future.getUTCDate() + 7 + ((8 - future.getUTCDay()) % 7));
  const nextMonday = future.toISOString().slice(0, 10);
  const pricing = page.getByRole('heading', { name: 'Giá ưu tiên theo khoảng ngày' }).locator('..');
  await pricing.getByRole('button', { name: 'Thêm quy tắc giá' }).click();
  await pricing.getByLabel('Hiệu lực từ').fill(nextMonday);
  await pricing.getByLabel('Đến ngày').fill(nextMonday);
  await pricing.getByLabel('Đến', { exact: true }).fill('09:00');
  await pricing.getByLabel('Giá/30 phút').fill('200000');
  const updatedPricing = page.waitForResponse(response => response.url().endsWith('/operations') && response.request().method() === 'GET');
  await pricing.getByRole('button', { name: 'Lưu giá theo ngày' }).click();
  expect((await (await updatedPricing).json()).data.version).toBe(3);
  await expect(page.locator('.operations-page').getByRole('status')).toHaveText('Đã lưu.');

  const policy = page.getByRole('heading', { name: 'Quy định đặt sân' }).locator('..');
  await policy.getByLabel('Block hiển thị').selectOption('60');
  await policy.getByLabel('Thời lượng đặt tối thiểu').fill('60');
  await policy.getByLabel('Giữ chỗ trước khi báo chuyển khoản').fill('20');
  const savedPolicy = page.waitForResponse(response => response.url().endsWith('/booking-policy') && response.request().method() === 'PUT');
  await policy.getByRole('button', { name: 'Lưu quy định' }).click();
  const policyReply = await savedPolicy;
  expect(policyReply.status()).toBe(200);
  expect(policyReply.request().headers()['if-match']).toBe('"3"');
  expect(policyReply.request().postDataJSON()).toEqual({ bookingBlockMinutes: 60, minimumBookingMinutes: 60, holdMinutes: 20 });
  await expect(page.locator('.operations-page').getByRole('status')).toHaveText('Đã lưu.');
  await expect(policy.getByLabel('Block hiển thị')).toHaveValue('60');

  const preview = page.getByRole('heading', { name: 'Xem thử giá' }).locator('..');
  await preview.getByLabel('Ngày').fill(nextMonday);
  await preview.getByLabel('Từ').fill('08:00');
  await preview.getByLabel('Đến').fill('10:00');
  await preview.getByRole('button', { name: 'Xem giá' }).click();
  await expect(page.getByText('Tổng giá: 600.000 VND · 4 ca')).toBeVisible();

  const customer = await context.newPage();
  await customer.goto('http://localhost:5173/venues');
  await customer.getByLabel('Tên sân hoặc địa chỉ').fill(venueName);
  await customer.getByRole('button', { name: 'Tìm sân' }).click();
  const resultCard = customer.locator('.venue-card').filter({ has: customer.getByRole('heading', { name: venueName }) });
  await expect(resultCard).toBeVisible();
  await resultCard.getByRole('link', { name: 'Xem lịch các sân' }).click();
  await expect(customer.getByRole('heading', { name: venueName })).toBeVisible();
  await customer.getByLabel('Ngày chơi').fill(nextMonday);
  await expect(customer.getByRole('rowheader', { name: /F02 Court/ })).toBeVisible();
  await expect(customer.getByRole('button', { name: /F02 Court, 08:00 đến 08:30, Còn trống, 200.000đ/ })).toBeVisible();

  await expect(customer.getByRole('button', { name: /F02 Court, 09:00 đến 09:30, Còn trống, 100.000đ/ })).toBeVisible();
  await expect(customer.getByRole('img', { name: `Ảnh ${venueName}` })).toBeVisible();

  const maintenance = page.getByRole('heading', { name: 'Bảo trì sân' }).locator('..');
  await maintenance.getByLabel('Ngày').fill(nextMonday);
  await maintenance.getByLabel('Lý do').fill('F03 live maintenance');
  await maintenance.getByRole('button', { name: 'Khóa ca bảo trì' }).click();
  await expect(page.getByText('F03 live maintenance')).toBeVisible();
  await customer.getByRole('button', { name: 'Làm mới lịch' }).click();
  await expect(customer.locator('.slot-label').filter({ hasText: 'Đã kín' }).first()).toBeVisible();
  await expect(customer.getByRole('button', { name: /F02 Court, 08:00 đến 08:30/ })).toHaveCount(0);
  await page.getByRole('button', { name: 'Hủy bảo trì' }).click();
  await expect(page.getByText('Chưa có ca bảo trì.')).toBeVisible();
  await customer.getByRole('button', { name: 'Làm mới lịch' }).click();
  await expect(customer.getByRole('button', { name: /F02 Court, 08:00 đến 08:30, Còn trống, 200.000đ/ })).toBeVisible();

  // F05: guest preserves its court/time through customer registration and login.
  await customer.getByRole('button', { name: /F02 Court, 08:00 đến 08:30/ }).click();
  await customer.getByRole('button', { name: /F02 Court, 08:30 đến 09:00/ }).click();
  await customer.getByRole('button', { name: 'Tiếp tục đặt vãng lai' }).click();
  await expect(customer.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await customer.getByRole('button', { name: 'Đăng ký', exact: true }).click();
  const customerContact = `f05-live-${randomUUID()}@example.test`;
  const customerPassword = `F05-${randomUUID()}!Aa1`;
  await customer.getByLabel('Email', { exact: true }).fill(customerContact);
  await customer.getByLabel('Mật khẩu', { exact: true }).fill(customerPassword);
  await customer.getByLabel('Nhập lại mật khẩu').fill(customerPassword);
  await customer.getByRole('button', { name: 'Đăng ký', exact: true }).last().click();
  await expect(customer.getByRole('heading', { name: 'Xác minh tài khoản khách' })).toBeVisible();
  await expect.poll(() => verificationCode(customerContact)).not.toBeNull();
  await customer.getByLabel('Mã xác minh 6 chữ số').fill((await verificationCode(customerContact))!);
  await customer.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await customer.getByLabel('Mật khẩu', { exact: true }).fill(customerPassword);
  const loginReply = customer.waitForResponse(response => response.url().endsWith('/api/v1/auth/login') && response.request().method() === 'POST');
  await customer.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  const customerAccess = (await (await loginReply).json()).data.accessToken as string;
  await expect(customer.getByRole('heading', { name: 'Xác nhận đặt vãng lai' })).toBeVisible();
  await expect(customer.getByText('Tổng tiền: 400.000đ')).toBeVisible();
  await customer.getByRole('button', { name: 'Xác nhận tạo đơn' }).click();
  await expect(customer.getByRole('heading', { name: 'Chi tiết đơn đặt sân' })).toBeVisible();
  await expect(customer.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toBeVisible();
  await expect(customer.getByText('Ngân hàng: TEST · Chủ tài khoản: F02 CLUB', { exact: true })).toBeVisible();
  expect(await customer.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const bookingUrl = customer.url();
  await expect.poll(async () => {
    const response = await fetch(`${api}/api/v1/me/notifications/`, { headers: { Authorization: `Bearer ${customerAccess}` } });
    return ((await response.json()).data as Array<{ title: string }>).map(item => item.title);
  }, { timeout: 15_000 }).toContain('Đơn đặt sân đang giữ chỗ');

  await openPartnerPage(page, 'Thanh toán & QR');
  const revision = page.getByRole('heading', { name: 'Đề nghị thay đổi thông tin quan trọng' }).locator('..');
  await revision.locator('select').selectOption({ label: venueName });
  await revision.getByLabel('Mã ngân hàng').fill('TEST');
  await revision.getByLabel('Tên tài khoản').fill('F02 CLUB UPDATED');
  await revision.getByLabel('Số tài khoản').fill('9876543210');
  await revision.getByLabel('QR mới').setInputFiles({ name: 'qr-new.png', mimeType: 'image/png', buffer: png });
  await revision.getByRole('button', { name: 'Gửi thay đổi để duyệt' }).click();
  await expect(revision).toHaveCount(0);
  await admin.getByRole('button', { name: 'Tải lại danh sách' }).click();
  await admin.getByRole('button', { name: new RegExp(clubName) }).click();
  await admin.getByRole('button', { name: 'Phê duyệt' }).click();
  await expect(admin.getByRole('button', { name: new RegExp(clubName) })).toHaveCount(0);
  await customer.reload();
  await expect(customer.getByRole('heading', { name: 'Đăng nhập khách hàng' })).toBeVisible();
  await customer.getByLabel('Email', { exact: true }).fill(customerContact);
  await customer.getByLabel('Mật khẩu', { exact: true }).fill(customerPassword);
  await customer.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(customer).toHaveURL(bookingUrl);
  await expect(customer.getByText('Ngân hàng: TEST · Chủ tài khoản: F02 CLUB', { exact: true })).toBeVisible();
  await expect(customer.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);

  // F06: proof is uploaded privately, customer reporting does not mark payment PAID.
  await customer.getByRole('button', { name: 'Đã chuyển khoản', exact: true }).click();
  await expect(customer.getByLabel('Mã giao dịch ngân hàng', { exact: true })).toHaveCount(0);
  await customer.getByLabel('Ảnh chụp màn hình chuyển khoản (không bắt buộc)').setInputFiles({ name: 'proof.png', mimeType: 'image/png', buffer: png });
  const reportReply = customer.waitForResponse(response => response.url().endsWith('/transfer-evidence') && response.request().method() === 'POST');
  await customer.getByRole('button', { name: 'Gửi báo chuyển khoản', exact: true }).click();
  const reported = (await (await reportReply).json()).data;
  expect(reported.status).toBe('AWAITING_OWNER_CONFIRMATION');
  expect(reported.payment.status).toBe('TRANSFER_REPORTED');
  expect(reported.evidence[0].proofUrl).toMatch(/^\/api\/v1\/uploads\/[0-9a-f-]+\/view$/);
  expect(reported.evidence[0].bankReference).toBeNull();
  expect(reported.version).toBe(2);
  await expect(customer.locator('.booking-status')).toHaveText('Chờ xác nhận');
  await expect(customer.getByRole('img', { name: 'QR nhận tiền của cơ sở cho đơn đặt sân' })).toHaveCount(0);
  await expect.poll(async () => {
    const response = await fetch(`${api}/api/v1/me/notifications/`, { headers: { Authorization: `Bearer ${customerAccess}` } });
    return ((await response.json()).data as Array<{ title: string }>).map(item => item.title);
  }, { timeout: 15_000 }).toContain('Đơn đặt sân đang giữ chỗ');

  // Worker delivers the owner notice; the link opens the scoped booking detail.
  await openPartnerPage(page, 'Thông báo');
  const ownerNotice = page.locator('.notice-list li').filter({ hasText: 'Khách đã báo chuyển khoản' }).filter({ hasText: reported.bookingNo });
  await expect(ownerNotice).toBeVisible({ timeout: 15_000 });
  await ownerNotice.getByRole('button', { name: 'Xem đơn đặt sân' }).click();
  await expect(page).toHaveURL(new RegExp(`bookingId=${reported.bookingId}`));
  await openPartnerPage(page, 'Đơn đặt sân');
  await expect(page.getByRole('button', { name: `Xem đơn ${reported.bookingNo}` })).toBeVisible();
  await page.getByRole('button', { name: `Xem đơn ${reported.bookingNo}` }).click();
  const detail = page.getByRole('region', { name: 'Chi tiết đơn đặt sân', exact: true });
  await expect(detail.locator('.status-badge')).toHaveText('Chờ xác nhận');
  await detail.getByRole('button', { name: 'Xem biên lai' }).click();
  await expect(detail.getByRole('img', { name: 'Biên lai chuyển khoản khách cung cấp' })).toBeVisible();
  const decision = detail.getByRole('form', { name: 'Quyết định thanh toán' });
  await decision.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('NEEDS_REVIEW');
  await decision.getByLabel('Nội dung gửi khách').fill('F06 live: cần bổ sung thông tin để đối chiếu');
  const reviewReply = page.waitForResponse(response => response.url().endsWith('/reject-payment') && response.request().method() === 'POST');
  await decision.getByRole('button', { name: 'Gửi yêu cầu bổ sung' }).click();
  const review = (await (await reviewReply).json()).data;
  expect(review.status).toBe('NEEDS_REVIEW');
  expect(review.version).toBe(3);
  await expect(customer.locator('.booking-status')).toHaveText('Cần bổ sung bằng chứng', { timeout: 15_000 });
  await customer.getByRole('button', { name: 'Bổ sung bằng chứng', exact: true }).click();
  await customer.getByLabel('Ghi chú (không bắt buộc)', { exact: true }).fill('F06 live: đã kiểm tra lại thông tin chuyển khoản');
  const supplementReply = customer.waitForResponse(response => response.url().endsWith('/transfer-evidence') && response.request().method() === 'POST');
  await customer.getByRole('button', { name: 'Gửi bổ sung bằng chứng', exact: true }).click();
  const supplemented = (await (await supplementReply).json()).data;
  expect(supplemented.version).toBe(4);
  expect(supplemented.evidence.map((item: { kind: string }) => item.kind)).toEqual(['INITIAL', 'SUPPLEMENT']);
  expect(supplemented.evidence[1].bankReference).toBeNull();
  expect(supplemented.payment.firstReportedAt).toBe(reported.payment.firstReportedAt);
  expect(supplemented.confirmationDueAt).toBe(reported.confirmationDueAt);
  await expect(detail.locator('.status-badge')).toHaveText('Chờ xác nhận', { timeout: 15_000 });
  await decision.getByRole('combobox', { name: 'Quyết định', exact: true }).selectOption('CONFIRMED');
  await expect(decision.getByLabel('Mã giao dịch đối chiếu')).toHaveCount(0);
  const confirmReply = page.waitForResponse(response => response.url().endsWith('/confirm-payment') && response.request().method() === 'POST');
  await decision.getByRole('button', { name: 'Xác nhận đã nhận đủ tiền' }).click();
  const confirmed = (await (await confirmReply).json()).data;
  expect(confirmed.status).toBe('CONFIRMED'); expect(confirmed.payment.status).toBe('PAID');
  expect(confirmed.payment.confirmedAmount).toBe(400000); expect(confirmed.version).toBe(5);
  expect(confirmed.decisions.at(-1).bankReference).toBeNull();
  await expect(detail.locator('.status-badge')).toHaveText('Đã xác nhận');
  await expect(detail.getByRole('form', { name: 'Quyết định thanh toán' })).toHaveCount(0);
  await expect(customer.locator('.booking-status')).toHaveText('Đã xác nhận', { timeout: 15_000 });
  await expect.poll(async () => {
    const response = await fetch(`${api}/api/v1/me/notifications/`, { headers: { Authorization: `Bearer ${customerAccess}` } });
    return ((await response.json()).data as Array<{ title: string }>).map(item => item.title);
  }, { timeout: 15_000 }).toContain('Đơn đặt sân đã xác nhận');
  await customer.reload();
  await customer.getByLabel('Email', { exact: true }).fill(customerContact);
  await customer.getByLabel('Mật khẩu', { exact: true }).fill(customerPassword);
  await customer.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(customer).toHaveURL(bookingUrl);
  await expect(customer.locator('.booking-status')).toHaveText('Đã xác nhận');
  await expect(customer.getByRole('button', { name: 'Đã chuyển khoản', exact: true })).toHaveCount(0);
});
