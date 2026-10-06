import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

const adminContact = process.env.SHUTTLEBOOK_ADMIN_TEST_CONTACT;
const adminPassword = process.env.SHUTTLEBOOK_ADMIN_TEST_PASSWORD;
test.skip(process.env.SHUTTLEBOOK_E2E_REAL !== '1' || !adminContact || !adminPassword,
  'Requires disposable PostgreSQL/PostGIS, API, Mailpit and an Admin test account.');
test.use({ trace: 'off' });

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

test('partner and Admin complete onboarding and court operations through real browser, API and PostGIS', async ({ page, context }) => {
  test.setTimeout(120_000);
  expect((await fetch('http://localhost:5080/health/ready')).status).toBe(200);
  await context.route('**/*', route => {
    const origin = new URL(route.request().url()).origin;
    return ['http://localhost:5173', 'http://localhost:5174', 'http://localhost:5175', 'http://localhost:5080'].includes(origin)
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
  await expect(page.getByRole('heading', { name: `${venueName} · DRAFT` })).toBeVisible();

  const court = page.getByRole('heading', { name: 'Thêm sân' }).locator('..');
  await court.getByLabel('Cơ sở').selectOption({ label: venueName });
  await court.getByLabel('Tên sân').fill('F02 Court');
  await court.getByRole('button', { name: 'Lưu sân' }).click();
  await expect(page.getByText('Sân F02 Court:')).toBeVisible();
  const schedule = page.getByRole('heading', { name: 'Giờ và giá theo sân' }).locator('..');
  await schedule.locator('select').first().selectOption({ label: `${venueName} / F02 Court` });
  await schedule.getByRole('button', { name: 'Lưu giờ/giá' }).click();
  await expect(page.getByText('Sân F02 Court: 1 ngày mở cửa, 1 khung giá')).toBeVisible();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.getByText('Yêu cầu không thành công (INCOMPLETE_PROFILE).')).toBeVisible();

  const image = page.getByRole('heading', { name: 'Ảnh cơ sở' }).locator('..');
  await image.locator('select').selectOption({ label: venueName });
  await image.getByLabel('Ảnh cơ sở').setInputFiles({ name: 'venue.png', mimeType: 'image/png', buffer: png });
  await image.getByRole('button', { name: 'Tải ảnh' }).click();
  await expect(page.getByText('Ảnh: Đã tải')).toBeVisible();
  const payment = page.getByRole('heading', { name: 'Tài khoản nhận tiền' }).locator('..');
  await payment.locator('select').selectOption({ label: venueName });
  await payment.getByLabel('Mã ngân hàng').fill('TEST');
  await payment.getByLabel('Tên tài khoản').fill('F02 CLUB');
  await payment.getByLabel('Số tài khoản').fill('1234567890');
  await payment.getByLabel('Ảnh QR').setInputFiles({ name: 'qr.png', mimeType: 'image/png', buffer: png });
  await payment.getByRole('button', { name: 'Lưu QR và tài khoản' }).click();
  await expect(page.getByText('QR: Đã khai báo')).toBeVisible();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.getByText(`${clubName} · PENDING_APPROVAL`).last()).toBeVisible();

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

  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByText('Admin yêu cầu bổ sung: Please correct the legal name')).toBeVisible();
  const edit = page.getByRole('heading', { name: 'Thông tin doanh nghiệp' }).locator('..');
  await edit.getByLabel('Tên pháp lý').fill('F02 Club Company Updated');
  await edit.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await page.getByRole('button', { name: 'Gửi hồ sơ duyệt' }).click();
  await expect(page.getByText(`${clubName} · PENDING_APPROVAL`).last()).toBeVisible();
  await admin.getByRole('button', { name: 'Tải lại danh sách' }).click();
  await admin.getByRole('button', { name: new RegExp(clubName) }).click();
  await admin.getByRole('button', { name: 'Phê duyệt' }).click();
  await expect(admin.getByRole('button', { name: new RegExp(clubName) })).toHaveCount(0);

  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).last().click();
  await expect(page.getByText(`${clubName} · ACTIVE`).last()).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Vận hành sân' })).toBeVisible();
  await expect(page.getByText('F02 Court · ACTIVE · múi giờ Asia/Ho_Chi_Minh')).toBeVisible();
  await page.getByRole('button', { name: 'Lưu lịch tuần' }).click();
  await expect(page.getByText('phiên bản 2')).toBeVisible();

  const future = new Date();
  future.setUTCDate(future.getUTCDate() + 7 + ((8 - future.getUTCDay()) % 7));
  const nextMonday = future.toISOString().slice(0, 10);
  const pricing = page.getByRole('heading', { name: 'Giá ưu tiên theo khoảng ngày' }).locator('..');
  await pricing.getByRole('button', { name: 'Thêm quy tắc giá' }).click();
  await pricing.getByLabel('Hiệu lực từ').fill(nextMonday);
  await pricing.getByLabel('Đến ngày').fill(nextMonday);
  await pricing.getByLabel('Đến', { exact: true }).fill('09:00');
  await pricing.getByLabel('Giá/30 phút').fill('200000');
  await pricing.getByRole('button', { name: 'Lưu giá theo ngày' }).click();
  await expect(page.getByText('phiên bản 3')).toBeVisible();

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
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});
