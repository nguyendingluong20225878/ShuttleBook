# Feedback Partner, giá và hồ sơ — 2026-10-14

## Phạm vi và quyết định

- Một cơ sở có nhiều sân. Owner nhập số cho nhiều sân rồi **Lưu tất cả số sân** một lần. Backend xác nhận tập số duy nhất, quyền sở hữu và version trước khi cập nhật nguyên tử; đổi chỗ số sân được phép. Nút **Xác nhận** từng sân vẫn riêng vì công bố từng sân là hành động khác với đổi tên.
- Desktop xếp hai sân trên một hàng trong cùng card cơ sở; mobile một cột. Nút gửi duyệt doanh nghiệp đặt sau phần setup cơ sở, QR, sân và liên kết giờ/giá.
- Giá tuần theo từng sân, từng thứ và ca 30 phút. Một sân có tối đa một khoảng giá đặc biệt đang cấu hình; form gồm từ ngày, đến ngày, giờ bắt đầu/kết thúc và giá/30 phút. Áp dụng mọi ngày **có mở cửa và có khung giờ đó** trong khoảng ngày. Không có thứ/ưu tiên trên UI. Nếu giờ đặc biệt vượt giờ mở của một ngày được áp dụng, trả validation, không lưu một phần. Giá tuần là fallback. Booking đã tạo giữ snapshot.
- Giá Customer phải đọc giá mới sau khi Partner lưu thành công và Customer làm mới đúng ngày/sân. Kiểm qua response API, không suy giá của Thứ hai sang Thứ tư hoặc từ sân 1 sang sân 2. Không tự nhân bản bảng giá sân 1 sang các sân khác.
- Cơ sở nháp sửa tên, địa chỉ, liên hệ trực tiếp. Cơ sở đã công bố cho Owner sửa liên hệ cơ sở trực tiếp (có version và audit); đổi tên/địa chỉ vẫn qua revision cần Admin duyệt. Đổi tài khoản/QR nhận tiền vẫn cần Admin duyệt; ảnh sửa trực tiếp.
- Avatar Customer dẫn đến hồ sơ cá nhân, cho chỉnh tên hiển thị; contact đã xác minh chỉ đọc đến khi có luồng xác minh thay đổi riêng. Avatar Partner dẫn đến hồ sơ tài khoản tương tự; hồ sơ doanh nghiệp giữ riêng. Không thu năm sinh/giới tính cho demo khi chưa có tác vụ dùng.
- Thông báo chỉ ẩn mã đơn dài trong phần hiển thị; bookingId/action được giữ để điều hướng, dữ liệu booking vẫn giữ mã cho đối soát.

## Contract và lỗi

- `PUT /api/v1/partner-onboarding/venues/{venueId}/court-numbers` nhận `{courts:[{id,version,number}]}`; chỉ Owner đúng scope, business/venue/court ở trạng thái cho phép. `400 VALIDATION_FAILED`, `409 COURT_NUMBER_EXISTS/STATE_CONFLICT`, `412 PRECONDITION_FAILED` cho version bất kỳ sai, `403/404` cho quyền/scope. Response danh sách id/name/version sau commit. Một transaction và audit từng sân đổi.
- `PUT /api/v1/operator/courts/{courtId}/special-price` nhận `{specialPrice:{startsOn,endsOn,startsAt,endsAt,pricePerSlot}}` hoặc `{specialPrice:null}` để xóa. Owner ACTIVE; If-Match bắt buộc. Mỗi thứ mở cửa trong khoảng ngày được lưu một rule priority 1, date range và time/price giống nhau; GET operations trả `specialPrice` nếu rules cùng cấu hình hoặc `legacySpecialPrices=true` nếu dữ liệu cũ không thể biểu diễn một lịch. Trường hợp legacy cần Owner chủ động thay thế, không tự bỏ rule.
- `GET/PUT /api/v1/auth/profile` chỉ tài khoản Customer/Partner đang hợp lệ; body update `{displayName}`; `400 VALIDATION_FAILED`, `401/403` theo session/role. Contact trả dạng email hoặc phone đã xác minh, không đổi qua form này. Không cache response. Trường tên mới có migration additive, không làm thay đổi khóa đăng nhập.
- `PUT /api/v1/partner-onboarding/venues/{venueId}/contact` nhận `{contact}`, If-Match version bắt buộc, chỉ Owner đúng scope; `412` khi version cũ, `409` khi đang có hồ sơ duyệt cho cơ sở. Liên hệ thay đổi hiển thị ở trang Customer sau khi lưu; tên/địa chỉ vẫn theo revision metadata cần Admin duyệt. API payment-only không dùng để sửa metadata.

## Testcase chấp nhận

1. Hai sân trong một cơ sở đổi số đồng thời và hoán vị: một lần lưu, đúng tên sau F5; số trùng/stale/sai owner rollback cả tập. Không tác động cơ sở khác.
2. Giá tuần khác nhau cho Thứ hai/Thứ tư và cho sân 1/sân 2: public availability cùng ngày cho giá đúng sau save, preview/quote bằng giá public; booking cũ giữ snapshot.
3. Giá đặc biệt 7 ngày áp dụng từng ngày mở trong khoảng ngày và đúng giờ; giá tuần ngoài khoảng/ngoài giờ; ngày đóng vẫn đóng; invalid range/giờ không lưu; thay thế/xóa một lịch.
4. Avatar mở profile đúng role, lưu tên, F5 còn dữ liệu, contact không đổi; sai session/role bị chặn.
5. Notification ba role không hiện mã đơn dài, click vẫn mở đúng tác vụ và giảm unread đúng.
6. Browser desktop/mobile: hai cột sân và nút lưu duy nhất; mobile không tràn; gửi duyệt ở cuối, chỉnh cơ sở và linked setup hoạt động.
