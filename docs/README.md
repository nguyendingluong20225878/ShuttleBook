# ShuttleBook - Thiết kế hệ thống đặt sân cầu lông

Đây là bộ tài liệu phân tích và thiết kế cho nền tảng thuê sân cầu lông theo giờ. Hệ thống tách ba cổng giao diện theo đúng nhóm người dùng nhưng dùng chung một backend và một nguồn dữ liệu giao dịch.

| Cổng | Tên miền chính thức | Đối tượng |
|---|---|---|
| Đặt sân | `tenmien.vn` | Khách xem, tìm và đặt sân |
| Đối tác | `partner.tenmien.vn` | Chủ sân và nhân sự vận hành |
| Quản trị | `admin.tenmien.vn` | Nhân sự quản trị nền tảng |
| API | `api.tenmien.vn` | Backend dùng chung cho cả ba cổng |

Nếu sử dụng `datlich.tenmien.vn` cho chiến dịch hoặc tên miền cũ, địa chỉ này chỉ chuyển hướng về `tenmien.vn`. Tên miền thực tế sẽ thay cho `tenmien.vn` khi mua domain.

## Mô hình nghiệp vụ đã thống nhất

- `CUSTOMER`: khách tự đăng ký để tìm và thuê sân.
- `VENUE_OPERATOR`: chủ sân tự đăng ký tại cổng đối tác; nhân viên được chủ sân hoặc Admin mời vào doanh nghiệp/cơ sở phù hợp.
- `ADMIN`: duyệt doanh nghiệp/cơ sở do chủ sân khai báo, quản trị tài khoản vận hành, khiếu nại và cấu hình hệ thống.
- Mỗi tài khoản chỉ có một `account_type`; quyền của operator còn bị giới hạn bởi membership và permission.
- Cấu trúc quản lý là `Doanh nghiệp → Cơ sở/Chi nhánh → Sân`. Booking luôn gắn với một sân cụ thể.
- Từ trang chi tiết sân, khách chọn đặt vãng lai hoặc cố định trên cùng một lịch được chia thành các ca 30 phút.
- Tìm sân gần chỉ trả danh sách cơ sở theo vị trí và bán kính. Khách chọn sân rồi vào trang chi tiết để thực hiện cùng luồng đặt vãng lai/cố định, chọn ngày giờ và ca cần đặt.
- Đặt vãng lai cho phép chọn tự do một hoặc nhiều ca liên tiếp; đặt cố định lặp cùng sân và khung giờ hàng tuần, mỗi buổi tối thiểu 2 giờ và thời hạn tối thiểu 1 tháng.
- Sau khi tạo booking, hệ thống giữ khung giờ và hiển thị QR nhận tiền do chủ sân cung cấp. Khách chuyển khoản 100% rồi bấm **Đã chuyển khoản**; booking hiển thị **Chờ xác nhận** và hệ thống gửi thông báo đến chủ sân. Sau khi chủ sân đối chiếu và xác nhận, booking hiển thị **Đã xác nhận**.
- Luồng booking thành công kết thúc ở **Đã xác nhận**. Khách chỉ cần đến sân chơi, không cần check-in/check-out; mọi vấn đề sau xác nhận thanh toán được giải quyết trực tiếp với nhân viên tại sân.
- Khách không có chức năng hủy hoặc đổi lịch trên hệ thống.

## Trạng thái chính

| Trạng thái | Ý nghĩa hiển thị |
|---|---|
| `AWAITING_TRANSFER` | Đang chờ khách chuyển khoản |
| `AWAITING_OWNER_CONFIRMATION` | Chờ xác nhận |
| `NEEDS_REVIEW` | Cần bổ sung hoặc đối chiếu bằng chứng |
| `CONFIRMED` + payment `PAID` | Đã xác nhận |
| `EXPIRED` | Quá hạn chuyển khoản, khung giờ được giải phóng |
| `PAYMENT_REJECTED` | Sân không xác nhận được giao dịch |

## Kiến trúc đã chốt

- Frontend: monorepo React + TypeScript + Vite gồm ba ứng dụng độc lập: `customer-web`, `partner-web`, `admin-web`.
- Backend: C# ASP.NET Core Web API theo modular monolith.
- PostgreSQL: nguồn dữ liệu chuẩn cho doanh nghiệp, cơ sở, sân, lịch, booking, thanh toán và audit.
- PostGIS: tìm sân theo khoảng cách và bán kính.
- MongoDB: chỉ lưu application log đã loại bỏ dữ liệu nhạy cảm nếu dự án bắt buộc dùng MongoDB.
- MapTiler: hiển thị bản đồ và hỗ trợ nhập/geocode địa chỉ; danh sách sân gần nhất vẫn do PostGIS truy vấn.
- AWS S3 + CloudFront + ACM: triển khai riêng ba frontend bằng HTTPS, không public trực tiếp S3 website endpoint.
- AWS S3 media: ảnh sân, QR và bằng chứng chuyển khoản ở bucket private, chỉ truy cập qua presigned URL.
- AWS EC2 + Docker Compose + Nginx: chạy API và Worker cho MVP; PostgreSQL chạy trên RDS.
- Transactional outbox + Background Worker để gửi thông báo và xử lý hết hạn.

## Quy tắc quan trọng

- Booking và maintenance không được chồng thời gian trên cùng một court.
- PostgreSQL exclusion constraint là lớp bảo vệ cuối cùng chống double booking.
- Đặt cố định tạo `booking_series` và nhiều occurrence; mỗi occurrence vẫn có allocation riêng.
- Mọi thời điểm đặt phải thẳng hàng theo bước 30 phút. Khi series được tạo, tất cả occurrence trong kỳ được giữ nên khách khác không thể chọn ca giao nhau.
- Nếu một ngày trong series bị trùng, MVP rollback toàn bộ và trả danh sách ngày xung đột.
- Thời điểm thực lưu UTC; ngày/giờ hiển thị và quy tắc giá dùng múi giờ của venue.
- Giá tiền dùng `numeric`, không dùng `float`.
- Chủ sân tự cấu hình giá cho từng court thuộc doanh nghiệp của mình, theo ngày và khung giờ. Mỗi sân có bảng giá riêng; tiền booking là tổng giá các ca đã chọn theo bảng giá đó và được snapshot khi tạo booking.
- Chủ sân chỉ được xác nhận booking thuộc doanh nghiệp/cơ sở đang có membership active.
- `POST /auth/register` luôn tạo `CUSTOMER`; `POST /partner-auth/register` luôn tạo `VENUE_OPERATOR` ở trạng thái `PENDING_ONBOARDING`. Client không được tự gửi `account_type`.
- Chủ sân tự khai báo doanh nghiệp/cơ sở/sân tại cổng đối tác. Chỉ dữ liệu đã được Admin duyệt mới xuất hiện trên cổng khách thuê.
- Nhân viên vận hành không tự gán vào sân; họ tham gia bằng lời mời từ chủ doanh nghiệp hoặc Admin.

## Phạm vi MVP

| Bắt buộc | Nên có | Có thể bổ sung | Chưa làm |
|---|---|---|---|
| Ba cổng web, Customer auth, chủ sân tự onboarding, Admin duyệt sân, mời nhân viên, quản lý doanh nghiệp/cơ sở/sân, MapTiler + PostGIS, ngày tương lai, đặt vãng lai, đặt cố định hàng tuần, QR chuyển khoản, operator xác nhận, chống trùng lịch, S3, audit, backup | Notification đa kênh, review, dịch vụ đi kèm, báo cáo doanh thu, dispute | Promotion, favorite, export báo cáo | Customer hủy/đổi lịch, native app, loyalty, tournament, AI pricing, multi-currency |

## Tài liệu

1. [Luồng nghiệp vụ toàn hệ thống](./00-luong-nghiep-vu.md)
2. [Actor, Use Case và phân rã Use Case](./01-use-cases.md)
3. [State Diagram và Sequence Diagram](./02-states-and-sequences.md)
4. [Database và kiến trúc triển khai](./03-data-and-architecture.md)
5. [API, bảo mật và kế hoạch triển khai](./04-api-security-delivery.md)
6. [Hướng dẫn triển khai theo từng bước](./05-implementation-guide.md)

## Chính sách đã chốt

- F05: quote có hiệu lực 2 phút; đặt trước tối đa 60 ngày theo múi giờ venue; hạn chuyển khoản theo holdMinutes snapshot từng sân (mặc định 20 phút).
- F06 (nghiệm thu 2026-10-07): bỏ ô mã giao dịch ở customer/owner; API vẫn nhận mã tùy chọn để tương thích lịch sử cũ. Khách có thể gửi ảnh chụp màn hình chuyển khoản/ghi chú; chỉ xác nhận đúng tổng tiền. Sau 30 phút từ báo chuyển đầu tiên, owner + Admin nhận một cảnh báo; NEEDS_REVIEW vẫn giữ sân, bổ sung không reset SLA.
- In-app là kênh bắt buộc qua transactional outbox; email/SMS/Zalo bổ sung sau.

## Các quyết định business còn cần chốt

- Lịch cố định thanh toán từng buổi, theo tháng hay toàn bộ series?
- Series dài tối đa bao lâu và bao nhiêu occurrence?

## Trạng thái thiết kế

Phương án kiến trúc nền tảng đã được chốt: ba cổng web triển khai độc lập, một ASP.NET Core modular monolith, PostgreSQL/PostGIS là nguồn dữ liệu chuẩn, frontend trên S3 + CloudFront và API/Worker trên EC2. Các mục còn mở phía trên là tham số chính sách kinh doanh cần cấu hình trước khi lập trình, không làm thay đổi kiến trúc tổng thể.
