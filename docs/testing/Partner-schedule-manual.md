# Tự kiểm tra Lịch & giá Partner

Chạy tại repo `C:\Users\luong\Desktop\CLong`, PowerShell. Dùng tài khoản và dữ liệu thử. Không lấy các bước này làm lý do sửa/xóa đơn thật.

## Mở bản mới

1. Nếu API local đang chạy, tại cửa sổ của API nhấn Ctrl+C rồi chạy `npm.cmd run dev:api`. Khởi động Worker bằng `npm.cmd run dev:worker` nếu chưa chạy.
2. Nếu Partner chưa chạy, mở PowerShell khác tại repo: `npm.cmd run dev:partner`.
3. Mở `http://localhost:5174/#/schedule`, F5 và đăng nhập Partner. Bản này không có migration mới. Các migration của mốc trước vẫn phải được áp dụng theo hướng dẫn mốc đó nếu DB local còn cũ.
4. Chọn doanh nghiệp ở header, sau đó Cơ sở và Sân. Sân xếp Sân 1, Sân 2, Sân 10; số sân riêng từng cơ sở.

## Các luồng cần kiểm tra

| Ca | Thao tác | Kết quả mong đợi |
|---|---|---|
| U01 — đúng scope | Chọn hai cơ sở có Sân 1; chọn từng sân và mở Giờ & giá | Giờ/giá của đúng sân; không mang dữ liệu cơ sở trước sang cơ sở sau |
| U02 — nháp | Thêm sân trong cơ sở đã công bố ở Cơ sở & sân; mở Giờ & giá và chọn sân nháp | Mẫu chưa lưu có thông báo; lưu lịch/giá hợp lệ thì sân tự công bố trong cùng transaction, không có nút Xác nhận riêng và không gửi Admin |
| U03 — đồng bộ | Sau U02 quay lại Cơ sở & sân rồi Customer | Sân ACTIVE, giờ/giá mới, Customer thấy sân; không lỗi version cũ |
| U04 — hồ sơ cha | Thêm sân trong cơ sở nháp/chưa duyệt | Chỉ thiết lập lịch; không tự công bố. Cơ sở/doanh nghiệp vẫn cần Admin |
| U05 — lịch tuần | Bật ngày, chỉnh giờ mở/đóng và các khung giá phủ toàn khoảng; lưu | Giá VND/30 phút rõ; ca hở/chồng bị chặn và dữ liệu nhập được giữ |
| U06 — giá đặc biệt | Mở Giá theo ngày đặc biệt, nhập đúng thứ/ngày/giờ, lưu; xem thử giá | Rule ưu tiên cao thắng. Preview là giá đã lưu, không tạo báo giá giữ chỗ |
| U07 — 150 phút | Cấu hình minimum120/block60; preview08:00–10:30 trong giờ có giá | Hợp lệ, năm ca30phút; khớp quote Customer cùng cấu hình |
| U08 — chưa lưu | Sửa giá rồi đổi sân/cơ sở/tab/business/menu hoặc dùng Back | Có xác nhận bỏ thay đổi. Chọn không thì giữ scope/input; không gửi dữ liệu vào sân khác |
| U09 — hai phiên | Hai tab cùng mở cấu hình; tab1 lưu, tab2 lưu version cũ | Tab2 stale412, giữ input và khóa lưu; chủ động tải lại rồi kiểm tra mới lưu tiếp |
| U10 — lịch thực tế | Customer lấy quote → tạo đơn → báo chuyển; Partner mở Lịch sân đúng ngày | Giữ báo giá → Có đơn/Chờ chuyển → Chờ đối chiếu. Quote hết TTL không còn chặn kể cả Worker dừng |
| U11 — thanh toán | Từ lịch mở đơn, đối chiếu tiền theo luồng hiện có | Đã xác nhận/Đã thanh toán, allocation vẫn giữ. Không có nút check-in/hoàn thành |
| U12 — cố định | Đặt một kỳ nhiều buổi, mở lịch ở buổi thứ hai rồi click đơn | Hiện đúng occurrence; tiền buổi riêng tổng kỳ; chi tiết mở đúng nhóm thanh toán |
| U13 — snapshot | Tạo đơn, sau đó đổi giá/giờ mở cho đơn mới | Tiền/QR/policy đơn cũ không đổi; đơn cũ ngoài giờ mở mới vẫn xuất hiện trên lịch |
| U14 — bảo trì | Tab Bảo trì tạo khoảng không trùng, xem lịch Partner/Customer; hủy | Khóa ca cùng hệ thống, hủy giải phóng ca; khoảng trùng đơn/quote bị409, không xóa đơn để giải quyết |
| U15 — lỗi mạng/quyền | Đã tải lịch rồi tắt mạng và Tải lại; sau đó phục hồi. Đăng xuất | Lỗi mạng giữ snapshot + Chưa cập nhật; mất quyền/phiên xóa dữ liệu private; đăng xuất không còn lịch phiên trước |
| U16 — hiển thị | Desktop/mobile, bàn phím chuyển tab, cuộn trang rồi lưu | Trang không tràn ngang; lưới cuộn trong vùng riêng, xem toàn ngày tùy chọn; toast ở đầu viewport3giây |

Lưới mặc định hiển thị khoảng có giờ mở hoặc events (kể cả lịch sử ngoài giờ mới), có nút Xem toàn ngày. Các khoảng lịch sử đã giải phóng không chặn sân. Ngày đã qua là thông tin thời gian, không che trạng thái đơn đã xác nhận. Lịch cập nhật khoảng15giây khi đang xem; không phải stream realtime tuyệt đối.

## Khi lỗi

- 404 scope/calendar: chọn lại doanh nghiệp/cơ sở đúng quyền; không sửa URL để truy cập cơ sở khác.
- 412: tải cấu hình mới, đối chiếu các thay đổi và lưu lại chủ động; không retry tự động.
- PRICE_UNAVAILABLE: xem lịch/giá đã lưu, thứ của ngày preview và ca hở.
- SLOT_CONFLICT: tải Lịch sân, chọn khoảng khác; không tự giải phóng booking đã báo chuyển.
- CALENDAR_TOO_LARGE: chọn một sân để giảm payload; kết quả lỗi không có nghĩa sân trống.
- 404 route calendar/schedule GET sau cập nhật code: API đang chạy bản cũ; khởi động lại API rồi F5.

CI, AWS/S3/EC2 và SMTP production là các gate riêng; PASS local không thay kết quả của chúng.
