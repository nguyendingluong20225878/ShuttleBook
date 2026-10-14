# F02 mở rộng — bằng chứng kiểm thử và bàn giao

Ngày: 2026-10-12. Windows PowerShell, PostgreSQL/PostGIS thật ở local; database thử riêng do runner tạo và dọn. Code nằm trong working tree của nhánh `codex/f09-local-readiness`, chưa commit/push cho mốc này.

**Lưu ý hiện hành:** người dùng đã bỏ ảnh riêng sân, chỉ ảnh cơ sở tùy chọn. [Contract và kết quả mới](../features/Venue-only-optional-images.md) thay phạm vi ảnh của báo cáo lịch sử này; không dùng các ca ảnh sân PASS bên dưới để yêu cầu tính năng đã bị bỏ.

Mốc tiếp theo cùng ngày đã đổi tạo/đổi tên sân sang số trong từng cơ sở và thu gọn nút; bằng chứng mới (26DB/96API/46browser) ở [contract số sân](../features/Partner-court-numbering-and-actions.md). Các kết quả bên dưới giữ lịch sử của mốc mở rộng F02 trước thay đổi số sân.

## Phần đã triển khai

| Vai trò | Tác vụ | Mức duyệt |
|---|---|---|
| Customer | Xem ảnh riêng sân đã công bố, placeholder khi chưa có ảnh; sân nháp/pending không hiện public và không lấy được quote/availability | Không; chỉ đọc dữ liệu được công bố |
| Partner/Owner | Đổi ảnh cơ sở, thêm/thay/gỡ ảnh sân, đổi tên sân; lịch/giá cho đơn mới giữ F03 | Trực tiếp, backend kiểm scope/version và ghi audit |
| Partner/Owner | Tạo cơ sở/sân mới dưới business ACTIVE, cấu hình nháp, gửi duyệt, sửa/gửi lại khi được yêu cầu | Admin duyệt đúng mục mới; mục cũ tiếp tục hoạt động |
| Partner/Owner | Gửi revision pháp lý business; địa chỉ/tọa độ/tài khoản/QR venue giữ revision nhạy cảm | Giữ bản đang hoạt động đến khi Admin duyệt |
| Admin | Queue phân biệt ONBOARDING, VENUE_ADDITION, COURT_ADDITION, BUSINESS_REVISION, VENUE_REVISION; xem snapshot và duyệt/yêu cầu sửa | Một quyết định hiệu lực/request; không duyệt ảnh/giá thường ngày |

MVP có một ảnh đại diện/sân. Ảnh cơ sở bắt buộc được thay bằng ảnh khác, không gỡ. Một business có tối đa một đề nghị pending theo constraint hiện có. Không thêm mô tả sân khi schema chưa có field; không thêm dữ liệu tài khoản năm sinh/giới tính hoặc thay luồng đăng ký.

Migration `20261012095502_F02OwnerSelfService` thêm ảnh/scope sân và loại approval; upgrade backfill baseline của revision cũ còn hợp lệ để đổi ảnh không phá duyệt nhạy cảm, không hồi sinh snapshot stale. Lock business → venue/court và kiểm version bảo vệ thao tác tên/ảnh khi F03 sửa lịch/giá đồng thời.

## Kết quả tự động

Các lệnh chạy tại repo root. Không cộng các lần recheck trùng vào số testcase.

| Lệnh / phạm vi | Kết quả | Mục đích và bằng chứng |
|---|---|---|
| `npm.cmd run typecheck` | **PASS**, 4 workspaces | Kiểm TypeScript cả ba app và UI chung |
| `npm.cmd run build` | **PASS**, 3 portal | Build bundle; `.local/f02-self-service-web-build.log` |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx --configuration Release` | **PASS**, 0 warning/error | API/Worker/Migrator/CLI/tests; `.local/f02-self-service-backend-build.log` |
| `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --configuration Release` | **PASS 96/96** | API regression; `.local/f02-self-service-api.log` |
| `scripts/Test-Database.ps1 --no-build --configuration Release --filter FullyQualifiedName~OnboardingFlowTests` | **PASS 22/22** | PostGIS thật: media scope/purpose/READY/version, role/membership, concurrency, child approval, request-changes/resubmit, migration fresh/upgrade/repeat, revision cũ hợp lệ/stale, F02/F03 regression; `.local/f02-self-service-db.log` |
| Cùng runner, filter `FullyQualifiedName~Active_additions_remain_private_and_admin_only_publishes_submitted_children` sau bổ sung assertion | **PASS 1/1**, thuộc nhóm trên | Quote và availability trả 404 cho sân INACTIVE/PENDING; approve đúng mục; `.local/f02-self-service-quote-gate.log` |
| `scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/f02-onboarding.spec.ts tests/web/f04-discovery.spec.ts --workers=2 --reporter=line` | **PASS 44/44** | Browser desktop/mobile với API fixtures: form, ảnh, quyền UI, child pending, lỗi/version và discovery; `.local/f02-self-service-web.log` |
| `scripts/Test-Web.ps1 tests/web/ui-admin-approvals.spec.ts --workers=2 --reporter=line` | **PASS 12/12** | Admin review regression; `.local/f02-self-service-admin-web.log` |
| `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release` | **PASS 8/8**, exit 0 | Browser → API → PostGIS/Worker/Mailpit thật, desktop/mobile; `.local/f02-self-service-live-recheck.log` |

Browser fixture không chứng minh backend/SMTP/S3. Live giữ stub MapTiler để kiểm luồng ứng dụng; chưa kiểm provider bằng khóa thật ở mốc này. API tests không thay PostGIS integration. Full database suite tất cả class và hosted CI đúng working tree **NOT RUN** trong mốc này.

Visual recheck 4 executions thuộc tập browser trên **PASS**; root đã xem ảnh Partner mobile và Customer desktop/mobile: form ảnh, gallery ảnh/placeholder và bố cục không tràn trong các ảnh đã xem. Ảnh test là PNG một pixel màu đỏ để xác minh tải ảnh, không phải asset production. Bằng chứng ignored `.local/f02-visual.log` và `.local/f02-visual-results/`; không thay nghiệm thu thiết bị thật/U04.

## Live và lỗi đã phát hiện

- Lượt đầu mở rộng: **7 PASS / 1 FAIL**. OTP Customer, Admin và F02–F07 desktop PASS. Mobile dừng vì assertion màn cơ sở trong 5 giây khi UI vẫn đang xử lý request, chưa có bằng chứng HTTP lỗi. Không kết luận sản phẩm lỗi hoặc local chậm chỉ từ timeout này.
- Test được đổi để chờ riêng POST tạo venue tối đa 20 giây, kiểm HTTP 201 rồi kiểm render; thêm log chỉ operation/status/elapsed, không contact/password/OTP/token. Không tăng timeout chung của suite hoặc sửa nghiệp vụ để né test.
- Lượt chạy lại **PASS 8/8 trong 53,4 giây**, exit 0; POST venue trả 201 sau 104 ms desktop/57 ms mobile. F02–F07 live kiểm ảnh cơ sở DRAFT/PUBLISHED, đổi ảnh sân không tạo approval, thêm sân dưới venue hoạt động, sân mới ẩn đến Admin duyệt, rồi sửa giá/bảo trì và đặt/thanh toán vãng lai/cố định. DB thử đã drop sau xác minh đúng tên. Lượt này không đủ để kết luận mọi timeout lịch sử đã được chữa hoặc OTP trên phiên người dùng đã được sửa.
- Lỗi OTP người dùng báo **chưa tái hiện trong DB thử**: Mailpit thực sự có thư và mã xác minh được dùng trong luồng browser. Kết quả local không chứng nhận SMTP/OTP production hoặc cấu hình của API development đang mở trước đó.

## Review và các gate còn mở

Review độc lập đã phát hiện race đổi tên sân/F03 và vấn đề revision cũ bị ảnh trực tiếp làm stale. Root đã sửa lock/version và migration baseline, thêm ca PostGIS xác minh. Review độc lập toàn diff cuối chưa hoàn tất do giới hạn tài nguyên; root kiểm diff và kiểm thử phần bị ảnh hưởng. Không ghi PASS review độc lập cuối.

- **NOT RUN:** nghiệm thu tay U04 đầy đủ ba cổng, thiết bị thật, hosted CI cho code mới, S3/EC2/SMTP production.
- **Chưa áp vào DB development:** migration chỉ được chạy trên DB thử. Trước tự kiểm định, backup local nếu cần, áp migration và khởi động lại API theo [hướng dẫn](F02-owner-self-service-manual.md).
- **Chưa merge/deploy:** tiếp tục trên nhánh kiểm thử; nghiệm thu code mới và CI trước khi merge. F09 production vẫn **NO-GO** vì các gate vận hành/AWS riêng chưa đóng.
- AWS demo CV dùng dữ liệu giả theo [phương án](../runbooks/F09-cv-demo-aws-plan.md); mốc này không tạo tài nguyên hoặc xác minh Free Tier/chi phí.

[Đặc tả API/dữ liệu/quyền](../features/F02-owner-self-service-and-additions.md) · [Nhiệm vụ ba vai trò](../features/Partner-workspace-and-role-contract.md) · [Tự thử từng bước M01–M11](F02-owner-self-service-manual.md).
