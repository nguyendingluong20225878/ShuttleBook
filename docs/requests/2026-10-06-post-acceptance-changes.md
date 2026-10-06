# Yêu cầu thay đổi sau nghiệm thu F01.4–F03 — 2026-10-06

Trạng thái: **đang triển khai F01–F03 theo yêu cầu mới của người dùng**, dù nghiệm thu tay F02/F03 cũ chưa hoàn tất. Quyết định triển khai và phần còn lại ở `docs/prompts/F01-F03-post-acceptance-logic.md`. Tài liệu này không thay thế bằng chứng nghiệm thu; cập nhật đặc tả/API/dữ liệu/testcase theo `docs/process.md`.

## 1. Phiên Admin sau F5

- Yêu cầu: trong 30 phút, tải lại trang `admin-web` ở cổng 5175 vẫn ở khu vực Admin; sau 30 phút phải đăng nhập lại.
- Hiện tại: token Admin chỉ ở React memory; F5 luôn xóa phiên phía giao diện. Access token 10 phút và refresh token 30 ngày ở F01.4 không tự tạo chính sách 30 phút cho browser.
- Hướng thiết kế: phiên Admin được khôi phục qua cookie `HttpOnly`, `Secure` trên HTTPS, ràng buộc session DB và kiểm tra quyền/trạng thái hiện hành sau F5. Hạn 30 phút phải được backend cưỡng chế; logout, rotate, suspend và revoke-sessions phải vô hiệu ngay. Bổ sung CSRF/CORS và kiểm thử tab/F5/hết hạn/thu hồi. Không đưa refresh token vào localStorage.
- **Đã chốt 2026-10-06:** 30 phút kể từ thao tác cuối cùng. F5 khôi phục phiên hợp lệ và được tính là thao tác; refresh nền không gia hạn mốc.

## 2. Địa chỉ cơ sở qua Google Maps

- Yêu cầu: owner nhập địa chỉ, chọn gợi ý/vị trí trên Google Map; giao diện tự lấy tọa độ thay vì bắt nhập vĩ độ/kinh độ. Cho owner xem và xác nhận ghim khi kết quả mơ hồ.
- Giữ PostGIS và kiểm tra scope tại backend. Cần thiết kế đường xử lý trường hợp Google không tìm được địa chỉ hoặc trả sai vị trí, đồng thời xác minh dữ liệu trước publish/revision.
- Google Maps Platform yêu cầu project/API key và billing cho dịch vụ liên quan. Xem chính sách Google về Places/Geocoding và giới hạn lưu trữ nội dung; place ID được phép lưu lâu dài, còn nội dung khác cần kiểm tra điều khoản trước khi lưu lâu dài trong PostGIS. Không tạo tài nguyên tính phí trước khi người dùng cho phép.
- Tài liệu chính thức: https://developers.google.com/maps/documentation/javascript/place-autocomplete-new ; https://developers.google.com/maps/documentation/geocoding/policies ; https://developers.google.com/maps/documentation/places/web-service/policies

## 3. Giờ mở, độ dài block và khóa lịch

- Owner cấu hình giờ mở/đóng theo court/venue; F03 đã có giờ tuần theo court, ngày địa phương venue và maintenance.
- Owner muốn UI chia block 30/60/90 phút tùy môn. Đề xuất giữ **đơn vị lưu và chống trùng 30 phút**; block 60/90 là nhóm 2/3 ca liên tiếp ở UI và quote. Cần chốt danh sách độ dài theo môn/court và thay đổi có tác động gì tới booking cũ.
- Ca bảo trì dùng allocation F03. Lịch cố định hàng tuần được tạo theo series F07; toàn bộ kỳ phải được giữ/rollback cùng transaction nếu xung đột. Không tạo trạng thái khóa thủ công giả thay cho booking cố định nếu mục đích là bán lịch cho khách.
- Owner muốn cấu hình thời lượng đặt tối thiểu; hiện vãng lai tối thiểu 30 phút, cố định mỗi buổi tối thiểu 2 giờ và kỳ tối thiểu 1 tháng. Cần xác định giá trị mặc định, phạm vi 30/60/90/... và hiệu lực với lịch đã tạo.
- Owner đề xuất giữ chỗ 20 phút trước khi khách báo chuyển khoản. Hạn giữ phải được chốt trong F05 và áp dụng **chỉ trước khi báo đã chuyển**. Theo nghiệp vụ đã chốt, booking đã báo chuyển không tự giải phóng vì owner xác nhận chậm.

## 4. Giá linh hoạt và đơn hàng

- F03 đã có giá riêng theo court, thứ trong tuần, giờ và khoảng ngày/priority, tính theo ca 30 phút. UI có thể hiển thị giá theo giờ và tự quy đổi/tổng từng ca; giờ mở phải luôn có giá cơ bản hợp lệ.
- Giá cao điểm/ngày thường/cuối tuần có thể biểu diễn bằng rule hiện có. Ngày lễ cần chọn cách quản lý lịch lễ, phạm vi năm và priority so với rule khác; chưa có lịch lễ tự động.
- Giá khách lẻ/khách cố định/hội viên là chính sách giá mới cho F05/F07 hoặc feature hội viên riêng. Cần định nghĩa ai cấp tư cách hội viên, thời hạn, quyền và quy tắc ưu tiên; không lấy nhóm từ body khách để tự giảm giá.
- Chủ sân muốn sửa đơn giá của đơn ngày/tháng. Thiết kế cần giới hạn thời điểm được sửa, quyền owner, lý do/audit, hiển thị và chấp thuận lại giá cho khách khi cần; tính lại tổng và lưu snapshot/version. Không sửa booking `CONFIRMED` như một bước trong luồng đặt.

## 5. Giao diện khách và tính nhất quán

- F04 sẽ hiển thị danh sách/nearby theo vị trí, trang chi tiết sân và các ca trống theo ngày. Nearby không lọc availability; sau khi chọn court mới tải lịch.
- F05/F07 tính quote giá minh bạch cho vãng lai/cố định; F05/F06 xử lý thanh toán và xác nhận. Ô đã giữ bởi booking hoặc maintenance không được chọn. Trạng thái hiển thị có thể cập nhật bằng polling/push, nhưng backend luôn kiểm tra lại bằng transaction và PostgreSQL exclusion constraint khi tạo allocation; UI không phải nguồn chống đặt trùng.
- Các booking đã `CONFIRMED` giữ allocation theo lịch; không thêm check-in/check-out hoặc customer tự hủy/đổi lịch.

## 6. Ảnh trước khi có S3

- Development/Testing mặc định dùng adapter local. Byte ảnh/QR nằm ở `backend/src/ShuttleBook.Api/.media-local/<upload-id>` trên máy chạy API; PostgreSQL lưu `media_uploads` và khóa liên kết `venues.image_upload_id` hoặc `venue_payment_accounts.qr_upload_id`.
- F5 xóa React memory/session và file input chưa gửi, nhưng ảnh đã upload, complete và gắn vào venue/QR không mất. Đăng nhập lại sẽ thấy trạng thái `Đã tải`/`Đã khai báo`; Admin xem ảnh qua endpoint có quyền.
- Thư mục `.media-local` bị Git ignore, không tự đi theo push/deploy hoặc sang máy khác. Xóa thư mục/mất ổ đĩa sẽ khiến metadata còn nhưng byte ảnh không còn. S3 private thật vẫn cần nghiệm thu riêng trước khi F02 DONE.

## Thứ tự thực hiện sau nghiệm thu

1. Khóa kết quả nghiệm thu F02/F03 và cập nhật `docs/progress.md` trung thực, gồm S3 nếu đã kiểm tra.
2. Chốt chính sách phiên Admin 30 phút và thiết kế cookie/session, rồi sửa F01.4 với test an ninh.
3. Chốt provider/billing/licensing bản đồ, rồi cải tiến form địa chỉ F02 và revision.
4. Chốt các tham số giờ/giá tối thiểu trước F04/F05/F07; giữ schema allocation và tính giá 30 phút làm nền.
5. Triển khai F04 → F05/F06 → F07 theo dependency; thêm chính sách hội viên và sửa giá đơn khi actor/state/payment contract đã chốt.
