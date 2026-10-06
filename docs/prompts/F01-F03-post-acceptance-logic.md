# Prompt tiếp tục thay đổi logic F01–F03 theo yêu cầu ngày 2026-10-06

Bạn là agent tiếp tục ShuttleBook tại `C:\Users\luong\Desktop\CLong`. Người dùng đã chốt phiên Admin hết hạn **30 phút kể từ thao tác cuối cùng** và yêu cầu sửa code F01–F03, chuẩn bị hợp đồng cho F04–F07. Không commit/push/deploy hoặc tạo tài nguyên Google/S3 trả phí. Không xóa thay đổi chưa commit. Trả lời và hướng dẫn bằng tiếng Việt.

## Bắt đầu

1. Đọc `AGENTS.md`, `docs/process.md`, phần đầu `docs/progress.md`, `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`, đặc tả/testcase F01.4, F02, F03 và `docs/requests/2026-10-06-post-acceptance-changes.md`.
2. Kiểm tra `git status --short`. Workspace đang có nhiều thay đổi F02/F03 chưa commit của phiên trước; giữ nguyên chúng. Đối chiếu code trước khi viết tiếp, không làm lại phần đã đạt.
3. Trước mỗi sửa đổi, chốt scope, acceptance, API/error contract, migration và testcase. Làm Database → API → UI → test → review. Ghi PASS/FAIL/NOT RUN/BLOCKED với bằng chứng runtime, nhất là PostgreSQL/PostGIS thật.

## Quy tắc nền không được phá

- `business → venue → court`; owner scope/backend authorization. PostGIS là nguồn vị trí; `court_allocations` và GiST exclusion là nguồn chống trùng.
- Giá tính theo ca 30 phút và snapshot khi tạo booking. Block hiển thị 60/90 phút là nhóm 2/3 ca liên tiếp.
- Cố định hàng tuần, mỗi buổi ít nhất 2 giờ, kỳ ít nhất 1 tháng; xung đột rollback cả kỳ. Bảo trì giữ allocation F03.
- Hạn giữ chỗ chỉ áp dụng trước khi khách báo chuyển khoản. Sau báo chuyển không tự giải phóng do owner chậm xác nhận. `CONFIRMED` là kết thúc thành công; khách không tự hủy/đổi lịch.
- Outbox cho thông báo. Không lưu secret/token vào repo, browser localStorage hay log.

## A. F01.4 — phiên Admin 30 phút trượt

**Acceptance:** Sau login Admin trên `5175`, F5 ở phút 1–29 giữ trang Admin; từ 30 phút không thao tác, F5 hoặc request Admin trả về đăng nhập. Thao tác có chủ ý (nhấn phím/click) gia hạn mốc; refresh nền không gia hạn. Logout, CLI rotate/suspend/revoke làm mất hiệu lực ngay. Customer/partner bearer không vào Admin. Token vẫn ở memory; cookie refresh `HttpOnly`, `SameSite=Strict`, `Secure` khi HTTPS. CORS chỉ cho origin được cấu hình; cookie restore kiểm tra `Origin` chống CSRF. Đảm bảo hai tab/refresh đồng thời không tự revoke oan family.

**Code hiện có để tiếp tục kiểm tra:** `IdentityModels.RefreshSession.LastActivityAt`, migration `20261006075958_F01F03RequestedPolicies`, `AuthSessionService` kiểm tra idle ở refresh, `Program.cs` kiểm tra ở bearer, `AdminBrowserSession.cs`, `AdminAuthEndpoints` `/restore`, `admin-web/src/main.tsx` khôi phục và theo dõi thao tác. Đặc biệt xem race refresh/cookie giữa nhiều tab và `AdminApprovals` tự tải lại khi access token đổi. Không để refresh nền tính là thao tác.

**API:** `POST /api/v1/admin-auth/login` giữ contract cũ và set cookie cho Admin origin; `POST /api/v1/admin-auth/restore` đọc cookie, trả `AuthTokens` mới hoặc `401 INVALID_REFRESH_TOKEN`; `auth/refresh`, `auth/logout` đồng bộ cookie. `GET /admin-auth/me` yêu cầu bearer và Admin hiện hành. Request sai Origin của restore trả `403 FORBIDDEN`; thiếu/hết hạn cookie `401`. Mọi response auth `Cache-Control: no-store`.

**Test:** API cookie/Origin/CSRF, DB idle 29m/31m, suspend/revoke/logout, rotation/reuse, hai tab và F5; browser login → F5 → Admin, hết 30m → login, logout → F5 → login. Không dùng đồng hồ thật chờ 30m trong test; dùng `TimeProvider` hoặc set thời gian session trong DB thử nghiệm.

## B. F02 — chọn địa chỉ bằng MapTiler

**Acceptance:** Form thêm/sửa cơ sở và đề nghị revision chỉ yêu cầu owner nhập/chọn địa chỉ Google, hiển thị bản đồ và nút xác nhận vị trí; không hiện ô vĩ/kinh độ. Chưa xác nhận vị trí thì chặn submit. Vẫn lưu tọa độ hợp lệ vào PostGIS và giữ scope/version/approval. Múi giờ IANA bỏ khoảng trắng đầu/cuối; `Asia/Ho_Chi_Minh ` phải được chuẩn hóa. Không gắn địa chỉ tìm thấy vào venue khác.

**Code hiện có:** `apps/partner-web/src/MapTilerPlacePicker.tsx`, ba form trong `PartnerOnboarding.tsx`; API `OnboardingEndpoints.cs` nhận `address`, `latitude`, `longitude` và lưu tọa độ PostGIS. `VITE_MAPTILER_API_KEY` được nạp từ local `.env` qua `scripts/Use-LocalEnvironment.ps1`. MapTiler Geocoding API trả GeoJSON suggestions gồm formatted place name và coordinates; chọn gợi ý sẽ preview marker, sau đó chủ sân phải xác nhận trước khi lưu. Khi thiếu key, không lưu vị trí mới. Giới hạn API key theo website, quota và chạy smoke test địa chỉ Việt Nam bằng key thật; dữ liệu mock chỉ xác nhận luồng UI, không xác nhận độ chính xác provider. Không coi lat/lng client gửi là bằng chứng server đã xác minh provider. MapTiler Cloud Free chỉ dành cho phi thương mại/R&D theo điều khoản hiện tại.

**Test:** browser dùng MapTiler GeoJSON response giả lập có kiểm soát cho gợi ý/chọn/xác nhận và payload; kiểm tra trường hợp chưa chọn; API validation/scope; live provider dùng MapTiler key để truy vấn địa chỉ thật. S3 private thật là tiêu chí F02 riêng; adapter `.media-local` giữ ảnh qua F5 nhưng không bền qua máy/deploy.

## C. F03 — giờ, block, giữ chỗ, giá

**Acceptance:** Owner đã publish đặt giờ mở/đóng theo sân/thứ, giá cơ bản phủ kín giờ mở, giá ưu tiên theo ngày/thứ/giờ, khóa/hủy bảo trì. Thêm `bookingBlockMinutes` 30/60/90, `minimumBookingMinutes` là bội số block (tối đa 480), `holdMinutes` 5–60 mặc định 20. Owner scope, `If-Match` version, audit, DB check constraint. Price preview từ 30 phút cộng từng ca, trả `400 VALIDATION_FAILED` khi thời lượng không hợp lệ và `409 PRICE_UNAVAILABLE` khi thiếu giá. Giá theo giờ trên UI phải quy đổi chính xác (`giá/giờ = 2 × giá/30 phút`). Ngày lễ cụ thể có thể đặt override theo khoảng một ngày; chưa có nguồn lịch lễ tự động.

**Code hiện có:** `Court` thêm ba trường policy, migration `20261006075958_F01F03RequestedPolicies`, `CourtOperationsEndpoints` GET operations/PUT booking-policy/price-preview, `PartnerOperations.tsx` form policy và quy đổi giá. Kiểm tra migration từ DB F03 cũ có data, defaults 30/30/20 và check constraint; đảm bảo migration chỉ thêm, không sửa migration cũ. Tạo testcase PostgreSQL thật cho scope, version, giá giờ cao điểm/cuối tuần/ngày lễ, block 60/90, min, hold, maintenance collision. Mỗi lần test DB dùng database tạm theo guard script; không migrate/drop DB development của người dùng.

**Giới hạn rõ của F01–F03:** Chưa có bảng booking/payment/series/member nên chưa thể thực thi giữ chỗ 20 phút, giảm giá theo tư cách hội viên/cố định, sửa đơn giá trực tiếp trong đơn, lịch ô trống của khách hoặc push realtime. F03 chỉ lưu cấu hình/giá và khóa bảo trì. Không dựng booking giả để đánh dấu yêu cầu đó DONE.

## D. Contract tiếp theo F04–F07

- F04 đọc court policy/giờ mở/maintenance/allocation để trình bày ô trống theo ngày/sân; nearby chỉ theo vị trí/bán kính. Polling hoặc push cập nhật UI, nhưng tạo booking luôn recheck trong transaction Postgres.
- F05 quote vãng lai theo 30 phút, block/min F03, giá theo ngày/thứ/giờ; snapshot giá và QR. Giữ chỗ theo `holdMinutes` từ F03, hết hạn chỉ khi **chưa** báo chuyển. Nếu đã báo chuyển, chờ F06.
- F06 nhận báo chuyển, outbox thông báo, owner xác nhận → payment `PAID`, booking `CONFIRMED`; không tự release do owner chậm.
- F07 series cố định tối thiểu 2 giờ/buổi và 1 tháng; atomic allocation toàn kỳ. Giá riêng `FIXED` cần quyền và quy tắc ưu tiên. Hội viên cần mô hình cấp/hết hạn/kiểm tra entitlement, tuyệt đối không tin segment client gửi. Chủ sân sửa giá đơn ngày/tháng chỉ trong trạng thái được phép, có lý do/audit, tính lại tổng và snapshot, quy tắc chấp thuận lại của khách nếu giá đổi. Không sửa booking `CONFIRMED` trong luồng này.

## Kết thúc

Chạy `dotnet build backend/ShuttleBook.slnx --no-restore`, `npm.cmd run typecheck`, `npm.cmd run build`, API tests, PostgreSQL/PostGIS integration tests và browser tests phù hợp. Sửa test cũ chỉ khi contract mới cố ý thay đổi; không biến test thành mock để tuyên bố provider Google/S3 đã PASS. Cập nhật đặc tả/testcase và `docs/progress.md` với trạng thái thực tế, lệnh/kết quả, giới hạn Maps/S3 và hướng dẫn nghiệm thu bằng PowerShell terminal VS Code. Không tự đánh dấu F02/F03 DONE nếu còn provider hoặc nghiệm thu tay chưa đạt.
