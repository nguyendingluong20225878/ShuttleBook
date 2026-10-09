# Nghiệm thu F05 trong terminal VS Code và trình duyệt

> Lưu ý phiên bản hiện tại (2026-10-09): hướng dẫn dưới đây ghi luồng F05 tại thời điểm trước F06/F07. **Báo giá hiện đã giữ chỗ 120 giây**; khách B không thể chen vào ca A đang có quote hợp lệ. Các bước cũ nói quote không giữ chỗ hoặc chưa có báo chuyển không áp dụng cho bản hiện tại. Dùng [nghiệm thu quote reservations](F07-quote-reservations-manual.md), [thanh toán F06](F06-manual-acceptance.md) và [mốc 2–5](M02-M05-acceptance-guide.md) cho test tích hợp mới.

Chạy PowerShell tại `C:\Users\luong\Desktop\CLong`. Dữ liệu thử riêng; không dùng thông tin ngân hàng/khách thật. F05 tạo đơn và giữ sân, hiển thị QR; báo chuyển/xác nhận triển khai F06.

## 1. Khởi động

Mở Docker Desktop/PostGIS trước. Sau khi dừng các API/Worker cũ bằng Ctrl+C trong terminal của chúng, chạy lần lượt:

```powershell
Set-Location C:\Users\luong\Desktop\CLong
npm.cmd run db:migrate
npm.cmd run build
```

Migration thêm quote, booking, payment, idempotency và FK, giữ dữ liệu F01–F04. Chỉ chạy lệnh migrate khi bạn muốn áp dụng F05 vào DB local của mình; agent chỉ migrate DB test tạm. Không chạy down -v/reset.

Mở các terminal riêng:

```powershell
npm.cmd run dev:api
```

```powershell
npm.cmd run dev:worker
```

```powershell
npm.cmd run dev:customer
```

```powershell
npm.cmd run dev:partner
```

Admin cần khi thử revision QR, mở terminal khác `npm.cmd run dev:admin`. Kiểm tra API:

```powershell
Invoke-RestMethod http://localhost:5080/health/ready
```

Mong đợi Healthy. Unable to connect: API chưa chạy; Unhealthy: kiểm tra PostgreSQL/migration ở terminal API. Worker phải chạy để hết hạn giải phóng sân và xử lý outbox.

## 2. Tiền điều kiện và happy path

1. Dùng hồ sơ F02 đã duyệt: business ACTIVE, venue PUBLISHED, court ACTIVE, có giờ/giá F03 và QR READY. Owner vào 5174 để xem policy block/min/hold; chọn ngày tương lai <=60 ngày có giờ mở, hai hoặc bốn ca liên tiếp hợp policy.
2. Mở cửa sổ ẩn danh `http://localhost:5173/venues`. Tìm venue, mở lịch. Guest vẫn thấy đủ sân, ca, giá. Chọn dải hợp lệ, nhấn **Tiếp tục đặt vãng lai**: sang login, URL returnTo giữ court/date/start/end.
3. Đăng nhập customer ACTIVE; nếu chưa có tài khoản thì đăng ký/xác minh Mailpit rồi đăng nhập. Sang **Xác nhận đặt vãng lai**, đúng venue/court/ngày/ca cũ. Không còn màn chỉ thông báo phiên đang hoạt động.
4. Kiểm tra mỗi ca 30 phút có giá riêng, tổng bằng phép cộng, timezone đúng. Quote 2 phút chỉ báo giá; mở cửa sổ khác vẫn thấy ca trống vì chưa tạo đơn.
5. Nhấn **Xác nhận tạo đơn**. Mong đợi trang chi tiết có mã duy nhất, AWAITING_TRANSFER/Đang chờ chuyển khoản, QR, bank/tên/số tài khoản che bớt, tổng giá, nội dung chuyển khoản=mã đơn và countdown holdMinutes. Không có nút Đã chuyển khoản/hủy/đổi lịch ở F05.
6. Cửa sổ guest khác làm mới lịch: đúng ca của đúng sân chuyển Đã kín. Ca chạm giờ kết thúc và sân khác không bị khóa.
7. Chọn **Đơn của tôi** rồi mở mã đơn: đọc cùng dữ liệu. Nhấn F5: customer cần đăng nhập lại theo F01 hiện hành; đăng nhập trở lại đúng đơn, mã/giá/QR không đổi, không tạo đơn mới. Token không nằm localStorage/sessionStorage/URL.

## 3. Các lỗi và biên cần thử

- Quote expiry: đợi quá 2 phút trên review. Nút tạo bị khóa, lấy báo giá mới để tiếp tục; không tạo hold khi chỉ xem quote.
- Giá/policy thay đổi: mở quote, owner sửa rule giá hoặc hold/min/block ở 5174, sau đó customer tạo trước expiry. Mong đợi QUOTE_CHANGED (hoặc lỗi policy hợp lệ nếu dải không còn đủ thời lượng); UI yêu cầu lấy quote mới và khách xác nhận lại, không tự tạo đơn với tiền khác.
- Conflict: hai customer có quote cùng court/time; khách A tạo trước, B tạo sau. A có đơn, B SLOT_UNAVAILABLE và không có đơn/payment nửa vời. Race 20 request được chứng minh bằng test DB, thao tác tay tuần tự không thay thế.
- Policy: minimum60, chỉ chọn30 thì nút tiếp tục disabled; chọn60 hoặc90 thì hợp lệ dù block60. Với minimum120, chọn3ca bị chặn, chọn4/5/6/7ca liên tiếp được tiếp tục dù block60. Với minimum90/block90, chọn90/120/150 đều được. Giá từng ca vẫn30 phút; tổng duration không cần là bội block theo nghiệm thu 2026-10-07.
- Ngày: hôm nay chỉ ca chưa bắt đầu; >60 ngày hoặc past bị API từ chối. Đừng dùng ngày cố định đã qua. Ca ngoài giờ/không giá/đã kín không cho chọn.
- Auth scope: sao chép URL đơn sang customer B. Sau B login trả NOT_FOUND, không đọc giá/QR/tài khoản đơn A. Guest tạo/đọc private route bị401; operator/Admin không đóng vai customer. Venue suspended hoặc court inactive không nhận quote/booking mới.
- Idempotency: ở DevTools Network request POST `/bookings`, gửi lại cùng body + Idempotency-Key trong lúc còn login. Trả cùng booking ID, không tăng số đơn. Cùng key đổi court/interval/quote trả409 IDEMPOTENCY_KEY_REUSED. Nếu mất mạng sau click, thử lại cùng intent trên review hoặc tìm ở Đơn của tôi. Không lưu bearer/token vào báo cáo.
- Snapshot: tạo đơn rồi owner sửa giá; đơn cũ giữ giá/từng ca cũ. Gửi revision QR/tài khoản và Admin duyệt; đơn cũ vẫn đọc QR/tên tài khoản snapshot cũ, đơn mới dùng bản đã duyệt mới. Public venue image không cho đọc QR.
- Mobile/keyboard: bảng mọi sân và biên giờ vẫn đúng, review/QR cuộn được, Tab tới button, thông báo lỗi bằng chữ. Back/F5 review lấy quote mới.

## 4. Expiry

Trên sân test, có thể đặt holdMinutes=5 để thử nhanh (ghi lại giá trị cũ và khôi phục sau thử). Tạo đơn AWAITING_TRANSFER, giữ Worker chạy và đợi quá deadline. Sau một chu kỳ Worker 5 giây và polling trang 5 giây trong điều kiện local bình thường, detail EXPIRED và lịch trở lại Available khi làm mới. Reload hay chạy lại Worker không tạo expiry/outbox lặp. Đơn EXPIRED giữ lịch sử giá/mã nhưng ẩn QR hướng dẫn chuyển tiền. Không chuyển khoản thật trong nghiệm thu F05.

Booking đã báo chuyển/confirmed không tự hết hạn: F05 chưa có UI báo chuyển, nhánh này được kiểm thử bằng DB transaction thật; sẽ nghiệm thu UI ở F06.

## 5. Test tự động bổ trợ

Dừng dev servers trong terminal của chúng để giải phóng 5080 và 5173–5175. Chạy tuần tự, giữ PostGIS/Mailpit:

```powershell
npm.cmd run typecheck
npm.cmd run build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:api
npm.cmd run test:db -- --no-build
npm.cmd run test:web -- --workers=2
npm.cmd run test:identity-live
```

DB/live scripts tạo/dọn database tạm, không reset DB local. Full web có live cases SKIP có chủ đích; identity-live chạy riêng qua API/PostGIS/Mailpit/Worker thật, gồm F05. Không build backend đồng thời với test chạy vì Windows khóa DLL. Không chạy nhiều browser workers khi máy thiếu RAM.

Ghi PASS/FAIL/NOT RUN cùng date/browser/court test/status code, không token/password/QR/signed URL. S3 live NOT RUN theo quyết định hoãn. Mặc định ở `backend/src/ShuttleBook.Api/appsettings.json`; tùy chỉnh local bằng `Booking__QuoteSeconds`/`Booking__MaxAdvanceDays` trong .env rồi restart API. Đặc tả F05 ghi toàn bộ contract.
