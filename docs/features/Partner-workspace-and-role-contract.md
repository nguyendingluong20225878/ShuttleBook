# Partner workspace và quyền ba vai trò — 2026-10-12

> **Cập nhật 2026-10-14:** [Contract mới](Partner-court-profile-followup-2026-10-14.md) thay thao tác Xác nhận sân riêng bên dưới: trong cơ sở đã công bố, lưu lịch/giá hợp lệ sẽ tự công bố sân nháp; Cơ sở & sân có một nút lưu toàn bộ số sân và nút Thêm sân cạnh đó. Mô tả cũ chỉ còn là lịch sử.

> **Feedback hiện hành 2026-10-13 (DONE local):** QR ban đầu nằm trong từng cơ sở nháp ở Cơ sở & sân, dùng chung mọi sân. Thanh toán & QR chỉ đề nghị thay đổi tài khoản/QR của cơ sở công bố để Admin duyệt; không nhập/hiển thị mã ngân hàng. Các sân gom cùng khối cơ sở, nút lưu/xác nhận trong cùng hàng và có link setup/quay lại đúng sân. Ba role click thông báo để mở nội dung/tác vụ và ghi nhận đã đọc; Admin có badge tổng chưa đọc và link duyệt. [Contract](Three-role-feedback-completion.md), [bằng chứng](../testing/Three-role-feedback-results.md).

> **Lịch & giá hiện hành:** ba tab Lịch sân / Giờ & giá / Bảo trì; một scope Cơ sở/Sân và editor nháp/ACTIVE chung. Partner đọc lịch private theo booking/quote/maintenance thật, mở nhóm thanh toán hiện có để đối chiếu; không tạo nghiệp vụ duyệt đơn trước giữ ca. Customer tiếp tục đặt/báo chuyển, Admin không đọc/sửa calendar hoặc xác nhận tiền thay Owner. [Contract triển khai](Partner-schedule-workspace.md), [hướng dẫn tự kiểm tra](../testing/Partner-schedule-manual.md).

> **Quyết định mới nhất:** sân mới trong cơ sở PUBLISHED do Partner bấm **Xác nhận** để công bố khi đủ lịch/giá, không Admin duyệt. Doanh nghiệp ban đầu/cơ sở mới vẫn gửi Admin. Kết quả thao tác Partner dùng toast ở đầu viewport, tự ẩn sau 3 giây. [Contract hiện hành](Partner-toast-and-court-confirmation.md) thay mô tả COURT_ADDITION/gửi duyệt sân ở tài liệu và kết quả lịch sử dưới đây.

> **Quyết định hiện hành:** chỉ ảnh cơ sở tùy chọn; không có ảnh riêng từng sân. Owner thêm/thay/gỡ ảnh cơ sở trực tiếp. Không ảnh vẫn gửi duyệt nếu QR và thông tin sân/lịch/giá đủ. [Contract mới](Venue-only-optional-images.md) thay tất cả mô tả ảnh sân/ảnh cơ sở bắt buộc ở các mốc cũ dưới đây.

> **Lịch sử quyết định bổ sung 2026-10-12:** phạm vi ban đầu có ảnh sân và Admin duyệt sân mới. Hai yêu cầu này đã được thay bởi quyết định hiện hành ở trên: chỉ ảnh cơ sở tùy chọn, sân do Partner tự xác nhận. Partner tự sửa lịch/giá cho đơn mới; booking đã tạo giữ snapshot. Cơ sở mới, địa chỉ/tọa độ, định danh pháp lý và tài khoản/QR nhận tiền vẫn qua Admin; QR không phải ảnh giới thiệu. [Bằng chứng F02 mở rộng](F02-owner-self-service-and-additions.md) và `docs/progress.md` giữ lịch sử từng mốc.

> **Cách gọi sân theo yêu cầu mới:** Chủ quản lý doanh nghiệp có nhiều cơ sở; mỗi cơ sở có các sân thuê riêng, đánh số `Sân 1`, `Sân 2`… Số chỉ duy nhất trong từng cơ sở, được lặp ở cơ sở khác. Tạo/đổi tên dùng Số sân, không đổi tên dữ liệu cũ tự động. [Contract số sân và nút gọn](Partner-court-numbering-and-actions.md) thay quy tắc nhập tên sân tự do trước đó.

## Quyết định sản phẩm

- Cấu trúc dữ liệu vẫn là **Doanh nghiệp → Cơ sở → Sân**. Booking, lịch và giá gắn với sân cụ thể; địa chỉ, ảnh cơ sở tùy chọn và tài khoản nhận tiền gắn với cơ sở. Không có ảnh riêng sân.
- Menu Partner hiển thị **Tổng quan, Hồ sơ doanh nghiệp, Cơ sở & sân, Lịch & giá, Đơn đặt sân, Thanh toán & QR, Thông báo**. Cơ sở & sân gom các form thêm/sửa cơ sở, sân và ảnh. Lịch & giá là trang vận hành riêng vì được dùng thường xuyên sau khi duyệt. Route cũ `#/courts` và `#/media` còn mở được để không làm hỏng liên kết cũ, nhưng không nằm trong menu.
- Ảnh cơ sở có ích cho trang tìm sân của Customer, nhưng tùy chọn; một ảnh đại diện/venue. Partner thêm/thay/gỡ và xem ảnh qua endpoint private đúng quyền; không ảnh vẫn gửi duyệt được. Không có ảnh riêng sân hoặc menu riêng để tải ảnh.
- Giữ timezone ở dữ liệu và API để ngày/giá đúng; form Partner trong phạm vi Việt Nam tự dùng `Asia/Ho_Chi_Minh` khi tạo cơ sở, giữ giá trị hiện có khi sửa. Không hiển thị mã timezone kỹ thuật trên Customer/Partner; Admin vẫn thấy khi đối chiếu thông tin đề nghị thay đổi.
- Với demo CV, tài khoản chỉ cần contact xác minh và mật khẩu để hoạt động. **Đề xuất** thêm tên hiển thị tùy chọn sau đăng ký; số điện thoại chỉ khi có nhu cầu liên lạc và phải xác minh trước khi tin dùng. Không thu năm sinh hoặc giới tính khi chưa có tác vụ cần chúng. Partner đã có tên, tên pháp lý, liên hệ doanh nghiệp và liên hệ từng cơ sở; không nhập lặp vào hồ sơ tài khoản. Chưa đổi schema/API đăng ký trong lượt này.
- Khi business đã ACTIVE, **cơ sở mới phải qua Admin duyệt**. Trong cơ sở PUBLISHED, Owner thêm sân nháp, thiết lập lịch/giá rồi bấm Xác nhận để công bố ngay; không tạo đề nghị Admin. Một business có tối đa một đề nghị Admin đang chờ duyệt theo constraint hiện có; Owner vẫn xác nhận sân hoặc sửa ảnh/vận hành sân cũ trong lúc có revision nhạy cảm khác.

## Luồng Partner trên giao diện

| Giai đoạn | Trang và thao tác | Kết quả / trạng thái |
|---|---|---|
| Chưa có business | Hồ sơ doanh nghiệp: tạo nháp | Có business DRAFT và OWNER/PENDING đúng tài khoản. |
| Business DRAFT | Cơ sở & sân: thêm/sửa cơ sở (địa chỉ MapTiler, liên hệ), thêm/sửa sân, tải ảnh cơ sở nếu có | Ảnh tùy chọn; sân được liệt kê ngay dưới cơ sở. Mỗi cơ sở cần sân/lịch/giá/QR. |
| Business DRAFT | Lịch & giá: chọn sân, giờ mở và giá ca 30 phút | Dữ liệu đủ để xét duyệt; không lấy timezone từ máy khách. |
| Business DRAFT | Cơ sở & sân: khai báo tài khoản/QR ngay trong cơ sở; theo link thiết lập sân rồi quay lại; Hồ sơ doanh nghiệp: gửi hồ sơ duyệt | Thiếu dữ liệu trả INCOMPLETE_PROFILE; gửi thành công khóa chỉnh sửa. |
| PENDING_APPROVAL / CHANGES_REQUESTED | Xem trạng thái, lý do và thông báo; chỉ sửa sau khi Admin yêu cầu | Không lén sửa snapshot đang duyệt. |
| ACTIVE | Lịch & giá, bảo trì, đơn, đối chiếu thanh toán, thông báo; yêu cầu thay đổi thông tin nhạy cảm để Admin duyệt | Chỉ Owner đúng business/venue scope thao tác. Booking đã báo chuyển giữ allocation đến khi quyết định. |
| ACTIVE, thêm cơ sở | Cơ sở & sân: thêm nháp, khai báo QR trong cơ sở; link Lịch & giá: cấu hình sân rồi quay lại; gửi duyệt cơ sở | Venue DRAFT/court INACTIVE; Admin duyệt toàn hồ sơ cơ sở thành PUBLISHED/ACTIVE. |
| ACTIVE, đổi tài khoản/QR | Thanh toán & QR: chọn cơ sở công bố, nhập tài khoản/QR mới, Gửi Admin duyệt | Payment-only revision lấy metadata canonical từ DB; không đổi địa chỉ/liên hệ. Trong lúc chờ dùng QR hiện hành; booking cũ giữ snapshot. |
| ACTIVE, thêm sân trong cơ sở PUBLISHED | Cơ sở & sân: thêm nháp; Lịch & giá: cấu hình rồi bấm Xác nhận sân ngay tại editor (hoặc tại Cơ sở & sân) | Court INACTIVE → ACTIVE trực tiếp, Customer thấy/đặt ngay, không Admin duyệt. |
| ACTIVE, sửa ảnh/số sân | Cơ sở & sân: thêm/thay/gỡ ảnh cơ sở tùy chọn, đổi số sân | Lưu trực tiếp đúng scope/version, audit; không tạo hồ sơ chờ Admin. Không có ảnh riêng sân. |

## Contract: thêm cơ sở/sân sau duyệt

**Scope:** Owner ACTIVE tạo cơ sở DRAFT trong business ACTIVE, hoặc sân INACTIVE trong venue PUBLISHED của mình. Cơ sở mới cần QR/sân/lịch/giá đầy đủ và Admin duyệt; ảnh cơ sở tùy chọn. Sân mới trong cơ sở PUBLISHED kế thừa QR, cần lịch/giá và Owner bấm Xác nhận để công bố. Backend kiểm owner membership, parent scope và trạng thái từ DB; lưu nháp chưa công bố.

**API/error:** dùng route create/update hiện có; cơ sở gửi `POST /partner-onboarding/venues/{id}/submit`, sân xác nhận `POST /partner-onboarding/courts/{id}/confirm` kèm If-Match, không body/query. Thiếu cấu hình trả `409 INCOMPLETE_PROFILE`, sai trạng thái `409 STATE_CONFLICT`, version `412/428`, sai quyền/scope `403/404`. Admin không còn loại Sân mới trong queue; `/courts/{id}/submit` không còn map. [Contract chi tiết](Partner-toast-and-court-confirmation.md).

**Dữ liệu/transaction:** business giữ ACTIVE; venue/court mới chưa PUBLISHED/ACTIVE. Admin approve hồ sơ cơ sở kiểm snapshot, parent và tập sân rồi publish cơ sở cùng đúng các sân được gửi, audit/outbox trong transaction. Owner confirm sân khóa business/venue/court, kiểm version/lịch/giá/QR và chuyển đúng sân ACTIVE, tăng version/audit, không tạo approval. COURT_ADDITION pending cũ chỉ chuyển SUPERSEDED khi Owner xác nhận thành công, giữ snapshot lịch sử. Đổi ảnh cơ sở không làm mất hiệu lực revision nhạy cảm đang chờ duyệt.

**Test cần có:** Owner đúng/sai business; confirm stale/concurrent; đủ lịch/giá/QR và parent PUBLISHED; sân nháp không public/quote trước confirm, sau confirm xuất hiện đúng một lần và không có approval. Admin duyệt/từ chối cơ sở đồng thời và snapshot booking cũ không đổi; migration fresh/upgrade/repeat/legacy và PostGIS thật. Toast thành công/lỗi thấy trong viewport desktop/mobile, ẩn sau3giây, lặp thao tác reset timer; lỗi tải/stale có recovery trong trang.

## Quyền và nhiệm vụ hiện có của ba vai trò

| Tác vụ | Customer | Partner / Owner | Admin |
|---|---|---|---|
| Tài khoản | Đăng ký, xác minh contact, đăng nhập/đăng xuất | Đăng ký, xác minh contact, đăng nhập/đăng xuất | Đăng nhập bằng tài khoản quản trị được tạo riêng; không tự đăng ký public |
| Khám phá | Tìm cơ sở theo danh sách/vị trí/bán kính; xem sân và ca | Xem cơ sở/sân của business mình | Xem hồ sơ cần duyệt |
| Hồ sơ cơ sở | Xem thông tin đã publish | Tạo business, cơ sở, sân nháp; nhập ảnh, QR, lịch và giá; gửi duyệt; xem phản hồi | Duyệt hoặc yêu cầu sửa, đối chiếu snapshot và audit |
| Đặt sân | Xin một quote hold tại một thời điểm, tối đa 10 triệu VND cho toàn booking/kỳ; tạo vãng lai hoặc cố định | Xem đơn thuộc scope, lịch và bảo trì sân | Không đặt hộ Customer trong luồng hiện tại |
| Thanh toán | Xem QR đúng đơn, báo đã chuyển, nộp bằng chứng/bổ sung khi được yêu cầu | Đối chiếu và xác nhận/từ chối theo state machine; cấu hình QR theo cơ sở | Xem cảnh báo và hồ sơ duyệt; không xác nhận tiền thay Owner trong luồng hiện tại |
| Thông báo | Xem thông báo của mình | Xem thông báo đơn, SLA và hồ sơ của mình | Xem thông báo cần xử lý |

Customer không tự hủy/đổi lịch. `CONFIRMED` là kết thúc luồng trong ứng dụng; không có check-in/check-out. F08 nhân viên/phân quyền nhân viên đã hủy. Mọi quyền được backend kiểm theo role/status/membership và business/venue scope; menu ẩn không thay kiểm quyền.

## Vấn đề OTP đang điều tra

Customer local dùng SMTP `localhost:1025`, Mailpit UI `localhost:8025`; `202` là phản hồi chung, **không chứng minh đã gửi mã**, để tránh lộ tài khoản tồn tại. Người dùng báo email thử mới không thấy OTP và sẽ gửi HTTP status/code của `/api/v1/auth/register`. Kiểm tra trực tiếp 2026-10-12: container PostGIS/Mailpit healthy, API `/health/live` và `/health/ready` đều HTTP 200, Mailpit `/api/v1/info` HTTP 200, cổng SMTP 1025 mở. Truy cập cổng từ sandbox mặc định không phản ánh trạng thái host. `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release` dùng DB thử riêng và Mailpit thật: Customer registration/OTP PASS desktop và mobile; toàn luồng 8/8 PASS, DB thử đã drop sau khi xác minh tên. Vì vậy lỗi người dùng thấy chưa tái hiện trên bản thử riêng; cần HTTP/code của request trong phiên đó, kiểm SMTP config của chính API 5080, tab Mailpit và thử gửi lại. Không chia sẻ OTP/password/contact trong log.

## Acceptance cho thay đổi UI hiện tại

- Menu có một đường rõ tới **Cơ sở & sân**, form thêm cơ sở/sân/ảnh nằm ở đó; `Lịch & giá` vẫn riêng.
- Ảnh đã tải được xem bằng request private của Owner; sai quyền không hiện ảnh. Không lộ S3 object key hoặc presigned URL trong HTML.
- Không có ô timezone kỹ thuật; giá trị backend vẫn giữ đúng. ACTIVE thêm nháp/gửi duyệt cơ sở; sân trong cơ sở PUBLISHED dùng Xác nhận. Có quyền tự quản ảnh cơ sở/số sân.
- Typecheck, build ba cổng và browser Partner desktop/mobile cần PASS; nghiệm thu tay U04 vẫn là gate riêng.

**Bằng chứng lịch sử trước mở rộng F02:** typecheck/build 4 workspaces/3 web PASS; `partner-workspace.spec.ts` 10/10 PASS desktop/mobile, tập browser liên quan Customer/Partner sau khi ẩn timezone **82/82 PASS**. Live từng đạt 8/8 PASS rồi có lượt **6 PASS / 2 FAIL** vì Admin login và Customer register desktop quá timeout. Không xóa kết quả lịch sử hoặc kết luận các timeout đó đã được chữa.

**Bằng chứng bản hiện tại 2026-10-12:** PostGIS OnboardingFlowTests **22/22 PASS**, API **96/96 PASS**, browser fixture **56/56 PASS**, live API/Worker/PostGIS/Mailpit **8/8 PASS** desktop/mobile, typecheck/build PASS. Ca live mới kiểm ảnh sân tải trực tiếp không có approval, thêm sân dưới cơ sở hoạt động và Admin duyệt đúng mục, đặt/thanh toán vãng lai/cố định. Chi tiết lệnh, lượt FAIL mobile và recheck ở [báo cáo](../testing/F02-owner-self-service-results.md). Hosted CI đúng code mới và U04 nghiệm thu tay vẫn chưa hoàn tất.
