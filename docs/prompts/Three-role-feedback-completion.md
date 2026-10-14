# Prompt thực thi feedback ba cổng — 2026-10-13

## Vai trò và mục tiêu

Đóng vai Codex có kinh nghiệm thiết kế, triển khai và review ứng dụng đặt sân. Đọc AGENTS.md, process/progress, F02/F03/F05/F06/F07 và các contract Partner hiện hành. Kiểm tra working tree và giữ thay đổi cũ. Triển khai feedback dưới đây đến kiểm thử local, không chỉ đưa đề xuất.

## Phương án đã chọn

1. **Cơ sở & sân:** mỗi cơ sở là một khối, danh sách sân bên trong là các hàng/card liền mạch. Mỗi sân có một nút Lưu số sân và tối đa một nút Xác nhận khi đủ quyền; hai nút cùng hàng thao tác trên desktop, wrap ở mobile. Không tạo batch command nhiều sân chưa được đặc tả. Tách rõ lưu số với công bố sân; không xác nhận ngầm khi lưu. Link Thiết lập giờ & giá chọn chính xác venue/court; có đường Quay lại cơ sở & sân để hoàn thiện hồ sơ/gửi duyệt.
2. **Đồng bộ:** tái hiện thêm sân nháp → F5 → chuyển Lịch sân/Giờ & giá → quay lại. Giữ danh sách sân quản lý từ onboarding riêng với các sân calendar trả về, không làm mất sân nháp hoặc đổi ID ngầm khi đổi tab. Giá/lịch/draft GET scoped; chống response cũ, stale412, dirty guard đã có phải tiếp tục hoạt động. Calendar chỉ đọc không công bố sân.
3. **QR theo cơ sở:** xác minh dữ liệu và booking snapshot dùng một VenuePaymentAccount/venue, không QR riêng court. Setup ban đầu đặt ngay trong từng cơ sở nháp: tên tài khoản, số tài khoản, ảnh QR; lưu riêng rồi thiết lập sân và gửi duyệt hồ sơ cha. Thanh toán & QR dành cho cơ sở công bố và đề nghị đổi tài khoản/QR gửi Admin; QR cũ tiếp tục dùng đến khi duyệt, booking cũ giữ snapshot.
4. **Bỏ mã ngân hàng:** không có input/nhãn/mã trên ba UI; request mới không cần bankCode, các read DTO mới không trả trường này. Không thay bằng mã giả. Cột/snapshot lịch sử chỉ giữ nội bộ để tương thích và không phá booking cũ; không xóa dữ liệu lịch sử. Không thêm chọn tên ngân hàng khi người dùng chưa cần; QR/tên/số tài khoản đủ cho chuyển thủ công hiện hành.
5. **Calendar:** ô đặt sân chỉ hiện Có đơn; không render bookingNo dài trong ô. Link vẫn mở đúng payment anchor, có accessible name phân biệt sân/giờ.
6. **Thông báo ba role:** bỏ các nút Đánh dấu đã đọc/Đã đọc. Click tiêu đề/thẻ thông báo bằng chuột hoặc bàn phím mở nội dung chi tiết và gọi POST read đúng ID; highlight/badge chỉ giảm sau server xác nhận, idempotent và không giảm hai lần. Thông báo có tác vụ dẫn đến đúng đơn/hồ sơ; ADMIN hồ sơ dẫn đến Duyệt hồ sơ, ADMIN payment alert chỉ hướng xử lý/liên hệ Owner, không mở quyền xác nhận tiền. Badge Admin phải dùng unreadCount toàn bộ từ API và đồng bộ sau read, tải trang, polling; không đếm riêng50rows.
7. **Customer chi tiết cơ sở:** bỏ nhãn Cơ sở ShuttleBook gây hiểu nhầm sở hữu. Hiện tên thực, địa chỉ, liên hệ có thể gọi, số sân hoạt động; ảnh có thì hiện, không thì placeholder rõ. Không đưa QR/account/private data vào public venue. Không bịa tiện ích, giờ/giá tổng hợp nếu API chưa cung cấp.
8. **Responsive:** action row/grid phù hợp desktop, compact buttons, mobile wrap và hit target đủ; giữ nhận diện xanh chung. Không thay đổi phong cách toàn hệ thống.

## Contract trước code

- Tiếp tục business→venue→court; trường paymentAccount thuộc venue. Không đổi allocation/giá/QR booking snapshot, max10triệu/toàn kỳ, một quote active, CONFIRMED terminal, Customer không hủy/đổi.
- Payment setup PUT `/partner-onboarding/venues/{venueId}/payment-account`: accountName/accountNumber/qrUploadId, owner đúng scope và venue nháp. BankCode legacy optional nếu cần tương thích; không bắt buộc và không expose lại.
- Payment-only revision POST `/partner-onboarding/venues/{venueId}/payment-account/revisions`: chỉ accountName/accountNumber/qrUploadId, không nhận địa chỉ/liên hệ từ UI. Metadata cơ sở được lấy từ DB dưới lock, Admin approval/baseline/snapshot giữ nguyên; tránh đổi QR làm ghi đè địa chỉ đã được duyệt trong lúc form mở. Route `/venues/{venueId}/revisions` vẫn dành cho thay đổi metadata có chủ đích. Thiếu cấu hình409, quyền403/404, lỗi validation400, stale412/428 theo command đang có.
- GET notifications: giữ no-store/scoped/read idempotent; thêm action ADMIN_APPROVAL hoặc PARTNER_PROFILE cho events onboarding dựa trên eventType/recipient role, không suy từ title; cung cấp target ID hợp lệ nếu cần. Không trả URL tùy ý để tránh điều hướng ngoài app. Read lỗi giữ unread/error và cho retry, không báo đã đọc giả. Abort/session guard để không ghi phiên cũ.
- Không thêm schema/migration nếu field legacy lưu rỗng đáp ứng constraint; nếu cần migration phải chốt riêng và test fresh/upgrade. Không chạy sửa DB development.

## Acceptance và testcase

FB01 sân cùng khối, nút lưu/xác nhận không nhân đôi; số sân scoped/đúng version; confirm chỉ sân trong cơ sở đã công bố.
FB02 sân nháp còn trong dropdown qua tab/F5/A→B→A và cấu hình đúng ID; link setup/quay lại đúng scope, dirty guard còn hoạt động.
FB03 setup QR tại cơ sở nháp rồi gửi duyệt; QR dùng chung khi đặt hai sân cùng venue; cơ sở khác tách riêng.
FB04 đổi QR cơ sở công bố chờ Admin, QR cũ vẫn nhận booking; approve xong đơn mới dùng QR mới, snapshot cũ không đổi; sai Owner bị chặn.
FB05 request không bankCode hợp lệ; ba UI và read DTO không expose bankCode; không hạ kiểm tra tài khoản/QR.
FB06 calendar chỉ Có đơn trong ô, không mã dài; mở anchor đúng, mobile không overflow.
FB07 Admin badge2 → click notice chưa đọc →1, click lại không giảm; hồ sơ mở approvals; read fail giữ badge và có retry; reload đúng server.
FB08 Partner/Customer click mở chi tiết, link đúng booking/profile; pagination không mất read state, scope/session/late response không lộ dữ liệu.
FB09 Customer tên/địa chỉ/contact/courts đúng, không nhận sở hữu ShuttleBook, không giả tiện ích/ảnh; no-image vẫn dùng được.
FB10 desktop action rows và mobile layout/keyboard hợp lệ, toast3s và lỗi tải persistent.

## Triển khai và bàn giao

Chia file độc lập khi cần: backend payment contract + PostGIS; notifications ba role/API; Customer public presentation; root Partner scope/setup/actions và tích hợp. Thống nhất contract trước khi sửa, review độc lập và sửa findings. Typecheck/build, API/DB tests theo vùng rủi ro, browser fixtures cho interaction, live browser→API→Worker→PostGIS/Mailpit trên DB tạm cho QR/onboarding/notification. Không chạy build/preview/live đồng thời trùng cổng. Báo đúng PASS/FAIL/NOT RUN và lệnh; không lấy source/fixture thay PostGIS/live.

Viết hướng dẫn tự kiểm, cập nhật progress/báo cáo và đặc tả quyền ba role. Không tự commit/push/merge/deploy/AWS hoặc thay dữ liệu thật; CI/AWS là gate riêng. Chỉ hỏi vấn đề thực sự chặn hiện tại, yêu cầu mới ưu tiên mô tả cũ.
