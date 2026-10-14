# Contract feedback ba cổng — 2026-10-13

Trạng thái **DONE local**. [Prompt thực thi và acceptance FB01–FB10](../prompts/Three-role-feedback-completion.md) được viết trước code; áp dụng cùng contract schedule và quyền ba role hiện hành. Hosted CI và AWS là gate riêng.

## Khảo sát

- Court form và nút confirm hiện là hai khối tách rời; cần gom thao tác trong cùng hàng/card ở nhóm cơ sở.
- PartnerOperations dùng selection `courtId=''` cho tất cả sân khi mở calendar; khi đi pricing tự chọn court đầu danh sách. Cần kiểm lựa chọn sân nháp và round-trip bằng browser, không kết luận dữ liệu bị xóa chỉ từ dropdown.
- QR đã gắn VenuePaymentAccount với venueId; form ban đầu hiện nằm tại Thanh toán & QR và bắt nhập BankCode. Nghiệp vụ cần QR/tên/số tài khoản, không sinh QR bằng mã ngân hàng.
- API notifications có unreadCount toàn scope, POST read idempotent. AdminShell chưa nhận unread badge; actions onboarding hiện null. Ba UI vẫn có nút đánh dấu đã đọc riêng.
- Customer VenueDetail có nhãn Cơ sở ShuttleBook cố định, không phải tên chủ sở hữu do backend cung cấp.

## Thiết kế

Phương án, API/error/data/quyền và testcase trong prompt là contract mốc này. Không thêm batch publish, không bypass Admin QR, không QR riêng court, không mở Admin payment confirmation. Metadata nhận tiền mới không dùng bankCode; dữ liệu/snapshot lịch sử giữ nội bộ để tương thích, không xóa.

Review độc lập trước khi nghiệm thu phát hiện QR revision có thể gửi lại metadata cơ sở cũ từ form. Chốt endpoint payment-only `/venues/{id}/payment-account/revisions` chỉ nhận tài khoản/QR; backend đọc metadata canonical dưới lock. Không chuyển metadata ẩn cũ vào proposal. Rule CSS action row cũng cần specificity bằng rule form chung để desktop không bị ép xuống hàng.

## Bằng chứng

Typecheck bốn workspace, build ba portal/backend Release, API 96/96, PostGIS focused 7/7 và live 8/8 đã PASS. Tập browser 200 ca có 192 PASS ở lượt đầu; tám ca fixture/locator lỗi đã sửa và PASS khi kiểm lại. Đã xem ảnh desktop/mobile và kiểm bố cục. [Báo cáo lệnh, lịch sử FAIL và giới hạn](../testing/Three-role-feedback-results.md); [tự kiểm trên local](../testing/Three-role-feedback-manual.md).

## Bổ sung contract đổi tài khoản/QR sau review

- POST `/api/v1/partner-onboarding/venues/{venueId}/payment-account/revisions` chỉ nhận `accountName`, `accountNumber`, `qrUploadId`; `bankCode` legacy là optional và không có trên read DTO. Các trường địa chỉ/liên hệ/tọa độ/timezone không được chấp nhận trong command này (`400 UNSUPPORTED_FIELD`).
- Metadata cơ sở lấy từ PostgreSQL sau lock business và reload venue trong cùng transaction, tránh UI dùng thông tin cũ rồi vô tình đề nghị khôi phục địa chỉ cũ. Command vẫn tạo `VENUE_REVISION`, snapshot baseline, scope Owner ACTIVE và QR READY thuộc cơ sở/chủ; Admin phải duyệt. `403/404` sai quyền/scope, `409 STATE_CONFLICT` sai trạng thái/có đề nghị chờ, `400 VALIDATION_FAILED` tài khoản/QR không hợp lệ.
- Endpoint `/venues/{venueId}/revisions` giữ cho luồng thực sự đổi metadata cơ sở. Không đổi migration, không đổi snapshot đã lưu. Booking mới khi chờ duyệt dùng QR hiện hành; booking cũ không đổi sau duyệt.
- Test bổ sung: payment-only không nhận metadata; snapshot lấy địa chỉ hiện hành đã thay đổi trước command, duyệt QR không khôi phục địa chỉ cũ; hai sân dùng chung QR venue, venue khác độc lập; snapshot booking cũ không thay đổi.
