# Tự kiểm feedback ba cổng

Ngày2026-10-13. Chạy PowerShell tại `C:\Users\luong\Desktop\CLong`; dùng dữ liệu/tài khoản thử.

## Mở code mới

1. Cửa sổ API hiện tại: Ctrl+C, `npm.cmd run dev:api`. Worker: `npm.cmd run dev:worker` nếu chưa chạy. Mốc này không thêm migration.
2. Ba portal dev: `npm.cmd run dev:customer`, `npm.cmd run dev:partner`, `npm.cmd run dev:admin`, mỗi lệnh ở cửa sổ riêng nếu chưa chạy. F5 các trang đang mở.
3. Partner `localhost:5174`, Customer `localhost:5173`, Admin `localhost:5175`. Chọn đúng doanh nghiệp ở Partner.

## Partner: setup cơ sở → sân → gửi duyệt

1. Mở **Cơ sở & sân**, lưu cơ sở nháp. Ngay trong khối cơ sở có **QR nhận tiền của cơ sở**: tên tài khoản, số tài khoản và ảnh QR. Không nhập mã ngân hàng. Lưu QR; ảnh cơ sở là tùy chọn.
2. Thêm các sân đánh số trong cơ sở. Mỗi sân nằm trong nhóm **Sân trong cơ sở**, có một **Lưu số sân** và, khi cơ sở đã công bố, một **Xác nhận**. Lưu số và xác nhận là hai thao tác riêng; chưa thiết lập giờ/giá thì backend từ chối xác nhận.
3. Bấm **Thiết lập giờ & giá** tại đúng sân. Selector đã chọn cơ sở/sân đó. Lưu lịch tuần đầy đủ giá, rồi **Quay lại cơ sở & sân**.
4. Doanh nghiệp/cơ sở nháp: hoàn thiện rồi gửi Admin. Doanh nghiệp đang hoạt động và cơ sở mới: **Gửi duyệt cơ sở** ở cuối khối cơ sở. Sân mới trong cơ sở đã công bố: **Xác nhận**, không gửi Admin.
5. Kiểm sân nháp: chọn sân đó tại Giờ & giá → Lịch sân → Tất cả sân → Giờ & giá; sân vẫn được chọn. F5 vẫn còn sân và lịch đã lưu. Lịch không làm công bố sân nháp.
6. Một cơ sở dùng một QR cho mọi sân; hai cơ sở có thể có tài khoản/QR khác nhau. QR thuộc cơ sở, giá/lịch thuộc sân.

## Partner: đề nghị đổi QR

1. Mở **Thanh toán & QR** → chọn cơ sở công bố → tên/số tài khoản mới và QR mới.
2. Bấm **Gửi Admin duyệt**. Không nhập lại địa chỉ hoặc mã ngân hàng. Chờ kết quả tại hồ sơ/thông báo; có đề nghị đang chờ thì chưa gửi thêm đề nghị khác.
3. Khi chưa duyệt, tài khoản/QR đang hoạt động tiếp tục dùng cho đơn mới. Admin duyệt xong, đơn mới dùng bản mới; đơn đã tạo giữ snapshot cũ. Đổi QR không đổi địa chỉ/liên hệ cơ sở.

## Calendar và Customer

- Lịch sân: ô đã đặt chỉ ghi **Có đơn**, không hiện mã dài; click vẫn mở đúng đơn/nhóm thanh toán.
- Customer chi tiết cơ sở: tên thật, địa chỉ, liên hệ và số sân hoạt động; không ghi “Cơ sở ShuttleBook”. Số điện thoại hợp lệ có thể bấm gọi. Không có ảnh/ảnh lỗi thì ghi **Chưa có ảnh cơ sở**, không giả ảnh cơ sở.
- Desktop: số sân và các nút cùng hàng khi đủ rộng; mobile wrap trong khối, không tràn ngang trang.

## Thông báo ba role

1. Có hai thông báo chưa đọc: badge là2. Click tiêu đề/thẻ một thông báo bằng chuột hoặc Enter.
2. Nội dung được mở; sau POST đọc thành công, thẻ không sáng nữa và badge giảm còn1. Click lại không giảm thêm. F5 lấy trạng thái từ server; không có nút đánh dấu đã đọc riêng.
3. Admin “Có hồ sơ chờ duyệt” dẫn tới **Duyệt hồ sơ**. Cảnh báo tiền chỉ mở nội dung/hướng dẫn liên hệ Owner; không cấp Admin quyền xác nhận tiền.
4. Partner thông báo đơn dẫn đến chi tiết đơn; phản hồi hồ sơ dẫn tới hồ sơ đúng doanh nghiệp. Customer mở nội dung và dùng link Xem đơn đặt sân khi có.
5. Tắt mạng rồi click thông báo chưa đọc: báo lỗi và giữ badge/highlight. Bật mạng rồi thử lại; không báo đã đọc giả. Kiểm cả các thông báo ở trang trước/Xem thêm.

## Lỗi thường gặp

- API404 route payment-account/revisions: API đang chạy binary cũ, khởi động lại API.
- INCOMPLETE_PROFILE: kiểm QR cơ sở và lịch/giá sân; QR không khai báo riêng từng sân.
- STATE_CONFLICT: có hồ sơ chờ duyệt hoặc trạng thái thay đổi; tải lại và xem phản hồi, không retry tự động.
- 412/428: version không đúng/thiếu; tải lại hồ sơ/cấu hình có chủ đích.
- Chưa đọc không giảm khi lỗi mạng là đúng: server chưa ghi nhận việc đọc.

CI/AWS, S3/EC2 và SMTP production vẫn là các gate riêng. Chưa commit/merge/deploy trong mốc này.
