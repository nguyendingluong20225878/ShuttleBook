# Prompt hoàn thiện ShuttleBook theo ba vai trò và mức duyệt

> **Đính chính hiện hành 2026-10-12:** người dùng đã bỏ ảnh riêng từng sân; chỉ ảnh cơ sở tùy chọn, Owner thêm/thay/gỡ trực tiếp, hồ sơ không có ảnh vẫn được duyệt nếu đủ thông tin khác. [Contract hiện hành](../features/Venue-only-optional-images.md) thay mọi yêu cầu ảnh sân/ảnh cơ sở bắt buộc trong prompt lịch sử này. Số sân theo [contract đánh số](../features/Partner-court-numbering-and-actions.md).

> Quyết định của chủ dự án ngày 2026-10-12: Admin chỉ duyệt thay đổi có tác động lớn. Partner tự quản ảnh cơ sở, ảnh riêng của từng sân, lịch và giá áp dụng cho đơn mới. Cơ sở/sân mới trong doanh nghiệp đang hoạt động vẫn cần Admin duyệt trước khi xuất hiện cho khách. Prompt này là yêu cầu triển khai cho lượt sau, không phải bằng chứng các tính năng đã hoàn thành.

---

Bạn là kỹ sư full stack kiêm reviewer sản phẩm có 10 năm kinh nghiệm. Hãy **hoàn thiện những phần còn thiếu của ShuttleBook theo đặc tả hiện hành**, đặc biệt là quyền Customer, Partner/Owner, Admin và giao diện Partner dễ dùng. Làm việc trực tiếp trong repo, báo cáo bằng tiếng Việt. Tiếp tục từ code và bằng chứng hiện có, không xây lại phần đã đạt.

## 1. Nguồn thông tin và cách làm

1. Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`, `docs/features/Partner-workspace-and-role-contract.md`, đặc tả F01–F07/F09 liên quan và testcase. Kiểm tra `git status`, migration, API, ba web portal, CI và kết quả kiểm thử trước khi sửa. Không ghi đè các thay đổi đang có của người dùng.
2. Yêu cầu mới trong prompt này ưu tiên hơn mô tả cũ nếu mâu thuẫn. Ghi quyết định và mâu thuẫn vào đặc tả trước khi code. Ví dụ: tài liệu hiện chỉ cho đổi ảnh cơ sở khi hồ sơ `DRAFT`; hãy mở quyền Owner đổi ảnh của cơ sở `PUBLISHED` mà không tạo yêu cầu Admin duyệt. Giữ luồng duyệt cho tài khoản/QR nhận tiền và thay đổi địa chỉ/tọa độ.
3. Chia thành các lát cắt nhỏ theo `Database → API → UI → test → review`. Trước mỗi lát cắt, viết phạm vi, acceptance criteria, bảng quyền, API/request/response/error contract, dữ liệu/migration và testcase. Chỉ hỏi chủ dự án nếu gặp quyết định thật sự chặn bước hiện tại; tiếp tục phần độc lập khi chờ. Không tự thêm nghiệp vụ hoặc duyệt đại trà.

## 2. Ma trận quyền và duyệt đã chốt

| Tác vụ | Người thực hiện | Admin duyệt? | Quy tắc |
|---|---|---|---|
| Đăng ký/xác minh Customer, tìm sân, xin báo giá, đặt sân, xem QR của đơn, báo chuyển tiền, xem thông báo | Customer | Không | Chỉ tài khoản/booking của mình; tối đa một quote hold còn hiệu lực; tổng tiền tối đa 10.000.000 VND cho một đơn vãng lai hoặc **toàn kỳ** cố định. Vượt trần bị chặn từ bước quote. Customer không tự hủy/đổi lịch. |
| Tạo hồ sơ doanh nghiệp ban đầu, cơ sở và sân nháp; gửi hồ sơ hoạt động | Partner/Owner | **Có, duyệt lần đầu** | Hồ sơ chưa duyệt không xuất hiện public hoặc nhận booking. |
| Thêm cơ sở mới vào business `ACTIVE`, thêm sân mới vào cơ sở `PUBLISHED` | Partner tạo nháp/gửi duyệt | **Có, trước khi khách thấy/đặt** | Chỉ mục mới chờ duyệt; business, cơ sở, sân và booking đã hoạt động vẫn bình thường. |
| Đổi pháp lý/định danh doanh nghiệp, địa chỉ hoặc tọa độ cơ sở, tài khoản ngân hàng hoặc **QR nhận tiền** đã publish | Partner gửi revision | **Có** | Bản published cũ giữ hiệu lực đến lúc duyệt. Không coi QR nhận tiền là “ảnh thông thường”. Không thay snapshot QR/tài khoản của booking cũ. |
| Thêm, thay, gỡ **ảnh giới thiệu cơ sở** và **ảnh riêng từng sân** đã hoạt động | Partner/Owner đúng scope | **Không** | Ảnh là nội dung giới thiệu, không phải QR nhận tiền. Chỉ publish ảnh đã upload và xác minh thành công; ảnh cũ vẫn hiển thị nếu thay ảnh thất bại. Ghi audit, kiểm MIME/size/checksum và quyền truy cập. |
| Sửa tên hiển thị/mô tả sân, thông tin giới thiệu không thuộc định danh pháp lý/địa chỉ/nhận tiền | Partner/Owner đúng scope | **Không** | Validate, ghi audit; không sửa dữ liệu lịch sử của booking. Nếu field hiện bị gộp với trường nhạy cảm, tách DTO/endpoint để tránh đường vòng duyệt. |
| Sửa giờ mở cửa, lịch ca, bảng giá 30 phút, booking policy và bảo trì sân đang hoạt động | Partner/Owner đúng scope | **Không** | Giá/lịch mới áp dụng cho quote/booking mới; quote đang giữ chỗ tuân theo contract `QUOTE_CHANGED` hiện có, booking đã tạo giữ snapshot tiền/lịch/QR. Dùng version/`If-Match`, kiểm xung đột allocation và ghi audit. |
| Xem đơn, đối chiếu báo chuyển, xác nhận hoặc từ chối thanh toán theo state machine | Partner/Owner đúng scope | **Không** | Đây là trách nhiệm vận hành của Owner, không chuyển cho Admin duyệt từng đơn. Idempotency và outbox phải giữ nguyên. |
| Duyệt/yêu cầu sửa hồ sơ ban đầu, mục mới và revision nhạy cảm; xem cảnh báo/audit | Admin | Admin tự quyết định | Bắt buộc lý do khi yêu cầu sửa; một request chỉ có một quyết định có hiệu lực. Admin không phải duyệt ảnh giới thiệu, lịch/giá thường ngày hay từng payment confirmation. |

Không tự mở luồng xóa/ngừng hoạt động cơ sở hoặc sân đang có booking. Nếu cần tác vụ đó, thiết kế riêng cách bảo vệ booking hiện hữu và hỏi chủ dự án trước khi triển khai. Không khôi phục F08 phân quyền nhân viên: chỉ Owner vận hành đúng business/venue scope.

## 3. Các phần phải hoàn thiện

### Partner

- Giữ cấu trúc dữ liệu `business → venue → court`, booking luôn gắn sân cụ thể. Menu **Hồ sơ doanh nghiệp**, **Cơ sở & sân**, **Lịch & giá**, **Đơn đặt sân**, **Thanh toán & QR**, **Thông báo** phải có đường đi và trạng thái rõ. Form thêm cơ sở/sân nằm ở **Cơ sở & sân**, cả khi doanh nghiệp đã `ACTIVE`; tạo nháp, bổ sung dữ liệu, gửi duyệt, xem lý do yêu cầu sửa. Không dùng nút “Lưu” giả khi API chưa có.
- Thêm **một ảnh đại diện riêng cho mỗi sân trong MVP** ngoài ảnh đại diện cơ sở hiện có. Có thể mở rộng album sau, nhưng không bắt buộc ở lát cắt này. Owner upload/đổi/gỡ ảnh cơ sở hoặc sân đã hoạt động trực tiếp, không qua Admin. Ảnh sân mới chưa duyệt chỉ hiện private cho Owner/Admin trong hồ sơ duyệt; public chỉ thấy sau khi sân được duyệt. Hiển thị ảnh thật trong trang Partner và trang chi tiết/tìm kiếm phù hợp của Customer; có placeholder rõ nếu chưa có ảnh.
- Tách thao tác media giới thiệu khỏi QR nhận tiền ở API, quyền, purpose, lưu trữ và UI. Chặn gắn upload sai `venueId`/`courtId`/owner/purpose hoặc chưa `READY`. Không trả object key, URL ký tạm hay ảnh QR private qua API công khai. Nếu cho gỡ ảnh cơ sở, giữ điều kiện hồ sơ tối thiểu và trải nghiệm public nhất quán.
- Owner sửa giá/lịch cho đơn tương lai bằng UI hiện có; hiển thị preview và lỗi rõ. Không buộc duyệt Admin, không hồi tố booking đã tạo.

### Customer

- Xem ảnh cơ sở và ảnh từng sân đúng dữ liệu đã publish, rõ sân nào đang được chọn. Luồng tìm kiếm → sân → quote → booking vãng lai/cố định → QR → báo chuyển → chờ Owner xác nhận không đổi bản chất.
- Khi quote vượt 10 triệu hoặc đã có quote hold, UI giải thích lỗi backend. Không tạo nút hủy/đổi lịch, check-in/check-out hoặc quyền Customer xem dữ liệu sân đang chờ duyệt.
- Đối chiếu lỗi Customer đăng ký email mới nhưng không thấy OTP trong Mailpit: tái hiện bằng browser → API thật → Mailpit → PostgreSQL với dữ liệu thử, phân biệt HTTP `202` chung với giao hàng thành công, rà config/port/rate limit và ghi bằng chứng an toàn. Chỉ sửa khi tìm được nguyên nhân; không in OTP, mật khẩu hay contact thật.

### Admin

- Danh sách và chi tiết yêu cầu duyệt phân biệt **hồ sơ ban đầu**, **cơ sở mới**, **sân mới**, **revision nhạy cảm**; nêu trường thay đổi, bản hiện tại/bản đề xuất, ảnh nháp nếu cần, lý do và trạng thái. Duyệt/yêu cầu sửa đúng scope và transaction.
- Không đưa ảnh giới thiệu, đổi giá/lịch, bảo trì hay xác nhận từng đơn vào hàng chờ duyệt. Giữ công cụ xem audit/cảnh báo trong phạm vi hiện có, không tự tạo quyền sửa dữ liệu của Owner.

## 4. Contract kỹ thuật và kiểm thử bắt buộc

- Thiết kế migration tăng dần cho ảnh sân và approval của mục mới; cập nhật public/owner/admin DTO. Chốt rõ `court.image_upload_id` hoặc bảng tương đương, FK/check/index, loại upload, ảnh private cho draft và route đọc public chỉ khi `business ACTIVE` + `venue PUBLISHED` + `court ACTIVE`. Không viết route chỉ ẩn bằng UI; backend kiểm role, account status, Owner membership và parent scope từ DB.
- Giữ envelope `{data,traceId}` và `application/problem+json` có `status/code/traceId`. Với version dùng `428 PRECONDITION_REQUIRED`/`412 PRECONDITION_FAILED`; sai quyền/scope dùng `401/403/404` phù hợp; pending/submit lặp dùng `409 STATE_CONFLICT`; dữ liệu thiếu dùng lỗi validation rõ. Tránh thay đổi contract F01–F07 đã dùng nếu không cần.
- Approval của cơ sở/sân mới phải snapshot đúng mục mới, kiểm lại parent và dữ liệu trước publish; lock và transaction chống approve/request-changes đua nhau hoặc retry nhân đôi. Mục mới không xuất hiện ở search/nearby/availability, không nhận quote/booking cho đến khi duyệt. Các mục cũ không bị ẩn hoặc khóa.
- Test PostgreSQL/PostGIS thật cho migration fresh/upgrade/repeat, FK/scope, status gate, approval race/idempotency, ảnh sai purpose/owner/court/READY, thay ảnh lỗi giữ ảnh cũ, giá mới và snapshot booking cũ, quote limit/race. Test API cho Customer/Owner khác/Admin và browser ba cổng ở desktop/mobile cho loading/empty/error/success. Chạy typecheck, build, các suite liên quan và CI khi đã có quyền cần thiết; báo **PASS/FAIL/NOT RUN/BLOCKED** kèm lệnh, môi trường và giới hạn. Không coi mock UI là bằng chứng backend/S3 thật.
- Cập nhật đặc tả, testcase, `docs/progress.md` sau mỗi mốc; đối chiếu acceptance và review lại diff cuối. Tách rõ phần đã triển khai, chưa triển khai, blocker và việc người dùng tự kiểm định. Giữ nhận diện xanh và thành phần UI chung, không hiện chuỗi kỹ thuật `Múi giờ: Asia/Ho_Chi_Minh` trên Customer/Partner.

## 5. Giới hạn và bàn giao

- Không tự commit, push, merge, deploy production, tạo tài nguyên AWS trả phí hoặc sửa dữ liệu thật nếu chưa có yêu cầu/ủy quyền tương ứng. Không ghi secrets vào repo/log/hội thoại. S3/EC2 production, SMTP/OTP thật, backup ngoài EC2, HTTPS/domain, ngân sách và vận hành là gate F09 riêng; không gọi tính năng “sẵn sàng production” chỉ vì local pass.
- Kết thúc bằng bảng **vai trò → tác vụ đã chạy được → tác vụ còn thiếu → quyền duyệt**; liệt kê file chính, API/migration, bằng chứng test, rủi ro còn mở và hướng dẫn người dùng tự thử từng luồng. Chỉ đánh dấu DONE khi acceptance và test bắt buộc đạt, không còn lỗi nghiêm trọng.
