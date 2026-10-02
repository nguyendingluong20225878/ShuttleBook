# Hướng dẫn triển khai theo mô hình đã chốt

## 1. Mô hình tài khoản

Dùng một cột `users.account_type`; frontend không được phép truyền hoặc thay đổi giá trị này:

- `CUSTOMER`: được tạo qua `POST /auth/register`.
- `VENUE_OPERATOR`: chủ sân được tạo qua `POST /partner-auth/register` ở trạng thái `PENDING_ONBOARDING`; nhân viên được tạo/kích hoạt qua invitation.
- `ADMIN`: được seed hoặc tạo bằng quy trình nội bộ.

Mỗi API của operator phải kiểm tra đủ bốn điều kiện:

1. Access token hợp lệ.
2. `account_type = VENUE_OPERATOR`.
3. Có `business_membership` active bao phủ toàn doanh nghiệp hoặc `venue_membership` active với cơ sở của resource.
4. Có permission cần thiết như `payment.confirm` hoặc `court.manage`.

## 2. Cách chủ sân tự đăng ký sân

### Chủ sân khai báo

1. Mở `https://partner.tenmien.vn/register`, nhập email/phone, mật khẩu và mã xác minh.
2. Backend tự tạo `VENUE_OPERATOR/PENDING_ONBOARDING`; request không có trường `accountType`.
3. Chủ sân tạo một business `DRAFT`; backend đồng thời tạo `business_membership` vai trò `OWNER`, trạng thái `PENDING` cho đúng user đang đăng nhập.
4. Chủ sân khai báo venue, court, vị trí, giờ mở cửa, bảng giá, ảnh và QR nhận tiền.
5. Khi đủ thông tin, chủ sân gửi duyệt; backend tạo `approval_request` có snapshot và chuyển business/venue sang `PENDING_APPROVAL`.

### Admin duyệt

1. Admin mở danh sách hồ sơ `PENDING_APPROVAL` tại `admin.tenmien.vn`.
2. Nếu thiếu hoặc sai thông tin, Admin chọn **Yêu cầu chỉnh sửa**, nhập lý do và đưa hồ sơ về `DRAFT`.
3. Nếu hợp lệ, Admin chọn **Phê duyệt**; user, business và owner membership thành `ACTIVE`, venue thành `PUBLISHED`, court hợp lệ thành `ACTIVE` và owner được vận hành đầy đủ.
4. Worker gửi kết quả duyệt cho chủ sân qua in-app notification và email/SMS theo cấu hình.

### Mời nhân viên

Sau khi business được duyệt, owner/Admin có thể mời nhân viên bằng token một lần trong `operator_invitations`, chọn permission và scope toàn doanh nghiệp hoặc từng cơ sở. Nhân viên không được tự khai báo mình thuộc business có sẵn.

Admin có thể suspend business/venue hoặc revoke membership bất cứ lúc nào. API vẫn phải kiểm tra membership ở mỗi thao tác nhạy cảm, không chỉ tin vào claim cũ trong JWT.

## 3. Luồng khách đặt sân

### Tìm và chọn sân

1. Khách cho phép lấy vị trí hoặc nhập khu vực.
2. Trình duyệt lấy vị trí hoặc Google Maps geocode địa chỉ; React gọi `/venues/nearby` chỉ với tọa độ và bán kính.
3. PostGIS trả các venue đã publish theo khoảng cách cùng thông tin cơ bản và vị trí để hiển thị danh sách/bản đồ. Nearby không kiểm tra availability hoặc tính quote.
4. Khách chọn sân để mở trang chi tiết và dùng chung luồng đặt sân: chọn sân cụ thể, **Vãng lai**/**Cố định**, ngày giờ và các ca 30 phút cần đặt. Trang chi tiết tải lịch trống và tính giá theo lựa chọn của khách.

### Đặt vãng lai

Chủ sân cấu hình `pricing_rules` riêng cho từng court của mình theo ngày và khung giờ, với giá mỗi ca 30 phút. Backend tính tổng giá các ca khách chọn, kể cả khi đi qua nhiều mức giá; lịch cố định tính từng buổi theo ngày tương ứng. Không tạo quote/booking cho ca thiếu giá. Khi tạo booking, lưu snapshot chi tiết giá và tổng tiền; thay đổi bảng giá chỉ áp dụng cho booking mới. API sửa giá phải kiểm tra owner có business membership active bao phủ court.

- Chọn tự do một ngày tương lai và một hoặc nhiều ca 30 phút liên tiếp.
- Thời lượng tối thiểu 30 phút; start/end chỉ được ở phút `00` hoặc `30`.
- Backend tạo một booking `CASUAL` và một allocation bao phủ toàn bộ các ca.
- PostgreSQL exclusion constraint quyết định toàn bộ khoảng còn trống hay không.

### Đặt cố định

- MVP hỗ trợ một khung giờ lặp hàng tuần trên cùng court, ví dụ thứ Ba 18:00-20:00 từ 01/10 đến 30/11.
- Mỗi buổi tối thiểu 2 giờ, tức ít nhất 4 ca liên tiếp; kỳ thuê tối thiểu 1 tháng.
- Backend sinh local date theo timezone venue rồi chuyển từng occurrence sang UTC.
- Quote API trả giá và danh sách ngày xung đột.
- Create API tạo `booking_series`, bookings và toàn bộ allocations trong một transaction; các ca của mọi occurrence được giữ ngay lập tức.
- Nếu một occurrence xung đột thì rollback toàn bộ; không âm thầm bỏ qua.

## 4. Chuyển khoản và xác nhận

1. API trả QR do chủ sân cung cấp cho venue, tài khoản nhận tiền, số tiền, nội dung chuyển khoản và deadline từ snapshot của booking. Chặn tạo booking nếu cơ sở chưa có QR nhận tiền hợp lệ.
2. Khách chuyển khoản rồi bấm **Đã chuyển khoản**, nhập mã giao dịch và có thể tải biên lai.
3. Backend chuyển payment sang `TRANSFER_REPORTED`, booking sang `AWAITING_OWNER_CONFIRMATION` (hiển thị **Chờ xác nhận**) và ghi outbox trong cùng transaction.
4. Worker gửi notification trong ứng dụng đến chủ sân của booking, kèm liên kết mở chi tiết để xác nhận; nhân viên nhận thêm theo phân quyền.
5. Operator đối chiếu ngân hàng rồi chọn:
   - **Xác nhận:** payment `PAID`, booking `CONFIRMED`, hiển thị **Đã xác nhận** trên cả cổng khách và cổng chủ sân.
   - **Cần kiểm tra:** payment `NEEDS_REVIEW`.
   - **Từ chối:** bắt buộc có lý do; terminal rejection giải phóng allocation.
6. Customer nhận thông báo và xem trạng thái mới.

Luồng booking kết thúc tại **Đã xác nhận** (`CONFIRMED`). Khách chỉ cần đến sân chơi; mọi vấn đề sau xác nhận thanh toán được giải quyết trực tiếp với nhân viên tại sân. Không triển khai check-in/check-out, hoàn thành hoặc vắng mặt. Customer không có API/nút hủy hoặc đổi lịch.

## 5. Notification cho chủ sân

Không gửi notification trực tiếp trong transaction booking:

1. Transaction ghi thêm `outbox_message`.
2. Background Worker lấy message bằng `FOR UPDATE SKIP LOCKED`.
3. Gửi in-app notification đến chủ sân; kênh ngoài ứng dụng như email/SMS/Zalo được gửi thêm theo cấu hình.
4. Lưu số lần thử, thời điểm thử lại và trạng thái hoàn thành.
5. Cảnh báo khi event gửi thất bại hoặc booking chờ operator quá SLA.

Operator portal có thể dùng SignalR để cập nhật realtime; PostgreSQL vẫn là nguồn dữ liệu chuẩn.

## 6. Cấu trúc module đề xuất

ASP.NET Core modular monolith:

```text
Modules/
  Identity/
  Businesses/
  Venues/
  Availability/
  Bookings/
  Payments/
  Notifications/
  Administration/
```

Frontend monorepo:

```text
apps/
  customer-web/       # tenmien.vn
  partner-web/        # partner.tenmien.vn
  admin-web/          # admin.tenmien.vn
packages/
  ui/
  api-client/
  contracts/
  config/
```

Ba ứng dụng dùng chung package nhưng build và deploy độc lập:

- `customer-web`: bản đồ, tìm sân, booking, QR và lịch sử của khách.
- `partner-web`: doanh nghiệp, cơ sở, sân, lịch, xác nhận chuyển khoản và báo cáo.
- `admin-web`: tiếp nhận đối tác, duyệt cơ sở, membership, dispute và giám sát.

Không gộp lại thành các route `/operator` và `/admin` trong một bundle vì ba cổng có đối tượng, quyền truy cập và chu kỳ phát hành khác nhau.

## 7. Triển khai AWS và tên miền

1. Route 53 quản lý `tenmien.vn`, `partner.tenmien.vn`, `admin.tenmien.vn` và `api.tenmien.vn`.
2. Ba ứng dụng React được build riêng, tải lên ba S3 bucket private và phân phối qua ba CloudFront distribution dùng Origin Access Control.
3. ACM cấp chứng chỉ HTTPS cho domain gốc và các subdomain.
4. EC2 chạy Docker Compose gồm Nginx, ASP.NET Core API và Background Worker; Nginx phục vụ `api.tenmien.vn`.
5. RDS PostgreSQL + PostGIS lưu dữ liệu giao dịch; MongoDB chỉ lưu application log nếu bắt buộc.
6. S3 media private lưu ảnh sân, QR và biên lai; client upload bằng presigned URL.
7. CloudWatch thu metrics/alarm; Secrets Manager hoặc SSM lưu secret; tuyệt đối không đặt access key trong source.

## 8. Thứ tự nên xây

1. Customer/partner identity, account type, contact verification và staff invitation.
2. Partner onboarding, business owner membership, venue approval, court, operating hours, pricing và PostGIS location.
3. Map search, ngày tương lai, availability và booking vãng lai.
4. QR transfer, evidence, outbox notification và operator confirmation.
5. Booking series và kiểm tra xung đột lịch cố định.
6. Đánh giá tùy chọn cho booking `CONFIRMED` đã qua `ends_at`, báo cáo, tranh chấp thanh toán chưa được xác nhận và production hardening.

## 9. Test bắt buộc

- Hai customer đặt cùng court/giờ: chính xác một booking thành công.
- Operator cơ sở A không đọc hoặc xác nhận booking cơ sở B khi ngoài scope.
- Business owner đọc được các cơ sở cùng doanh nghiệp nhưng không đọc được doanh nghiệp khác.
- Venue `DRAFT` hoặc `PENDING_APPROVAL` không xuất hiện trên cổng khách.
- Customer endpoint không thể tạo operator; partner endpoint chỉ tạo `PENDING_ONBOARDING` và không nhận `accountType` từ client.
- Pending operator không thể tự gán vào business có sẵn hoặc xác nhận booking trước khi được duyệt.
- Invitation hết hạn hoặc đã dùng không thể kích hoạt lần hai.
- Map search sắp xếp đúng khoảng cách và có fallback khi khách từ chối location.
- Vãng lai từ chối giờ 18:10, thời lượng 45 phút hoặc các ca không liên tiếp.
- Vãng lai 18:00-20:00 được tính thành 4 ca và chặn mọi booking giao ít nhất một ca.
- Series có một ngày bị trùng phải rollback toàn bộ và trả đúng conflict dates.
- Series dưới 2 giờ hoặc ngắn hơn 1 tháng bị từ chối; series hợp lệ khóa toàn bộ occurrence trong kỳ.
- Customer submit transfer hai lần không tạo hai notification.
- Operator confirm hai lần không tạo hai payment confirmation.
