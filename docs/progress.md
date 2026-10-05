# Tiến độ ShuttleBook

## Mốc tích hợp F01.1/F01.4 — 2026-10-02 (mới nhất)

- **F01.1: DONE** (18/18 testcase PASS) và **F01.4: DONE** (25/25 testcase PASS). **F01.2/F01.3 giữ DONE** theo nghiệm thu trước đó; do đó phạm vi identity F01 đã đạt nghiệm thu local. F01.4 gồm migration một Admin, CLI bootstrap/rotate/suspend/activate/revoke, API login/me và admin-web; review độc lập Auth/CLI không còn lỗi nghiêm trọng. Đây là trạng thái mới nhất; các đoạn dưới là mốc lịch sử.
- Bằng chứng runtime cuối: `npm.cmd run test:api` **58/58 PASS**; `npm.cmd run test:db` **15/15 PASS** trên PostgreSQL/PostGIS thật, thêm hai test Admin sau assertion cuối **2/2 PASS** và migration F01.1 riêng **1/1 PASS**; `npm.cmd run test:web` **26 PASS, 6 SKIP có chủ đích** vì các ca live chạy riêng; `npm.cmd run test:identity-live` **6/6 PASS** desktop/mobile qua API/PostgreSQL/Mailpit thật trên database tạm, dọn đúng tên sau chạy. `npm.cmd run typecheck`, `npm.cmd run build`, build solution `--no-restore` (**0 warning, 0 error**) và `git diff --check`: **PASS**. Lệnh, testcase và giới hạn bằng chứng ghi ở `docs/testing/F01.1-test-cases.md` và `docs/testing/F01.4-test-cases.md`.
- Review script E2E đã sửa theo hai phát hiện: kết nối database lấy riêng từ `.env` local, ép `127.0.0.1`, EF phải báo đúng tên DB tạm và data source trước khi migrate, dry-run xác nhận tên trước khi drop; Vite build ép API `http://localhost:5080`, Playwright chặn browser request ra origin khác. Test DB còn có guard Npgsql chặn override host từ xa và database điều khiển khác `postgres`.
- **Sự cố dữ liệu local:** bản đầu của `scripts/Test-Identity-Live.ps1` dùng sai `DbConnectionStringBuilder` trong PowerShell; lệnh cleanup EF đã xóa database development `shuttlebook` thay vì database tạm. Người dùng xác nhận **không có bản sao lưu**. Đã chạy Migrator thành công để tạo lại **schema** local, nhưng các bản ghi cũ trong database đó không thể khôi phục từ migration. Không có bằng chứng ảnh hưởng môi trường production. Bản script lỗi đã được thay, các lần sau chỉ dọn đúng DB tạm; một DB tạm `shuttlebook_f014_live_*` còn sau lần ngắt đã được xác minh tên và dọn riêng. Không chạy lại lệnh EF drop trên tên mặc định.
- Điểm còn mở: bản ghi cũ trong database development `shuttlebook` không có nguồn để khôi phục; schema đã tạo lại. CI và production **NOT RUN** vì chưa push/deploy; không commit/push/merge/deploy. F00 giữ trạng thái riêng, không đánh dấu DONE theo F01.

## Nghiệm thu F01.2 và F01.3 — 2026-10-02

- Người dùng xác nhận đã chạy và đạt toàn bộ các trường hợp kiểm thử còn lại trên Postman/UI/DB local, bao gồm các nhánh âm và bảo mật đã được hướng dẫn. Không lưu password, OTP, access/refresh token, contact thật hoặc ảnh response chứa bí mật vào repository.
- **F01.2 — Đăng nhập và phiên làm việc: DONE.** Đã nghiệm thu login theo trạng thái, JWT, refresh rotation/reuse, refresh đồng thời, family isolation, logout đúng/sai/lặp, suspend sau login, rate limit, audit/redaction, cấu hình và UI thực tế. F012-T13 kiểm tra quyền trên endpoint F02/F06 được chuyển sang feature phụ thuộc vì endpoint chưa thuộc phạm vi F01.2; quyết định này thay thế yêu cầu cũ coi T13 là blocker cho F01.2.
- **F01.3 — Đăng ký chủ sân: DONE.** Đã nghiệm thu register email/phone, allowlist/validation, duplicate/cross-role, OTP happy/negative/resend/attempt/expiry/concurrency, rate limit/audit/redaction, login `PENDING_ONBOARDING`, logout và partner UI nối API/Mailpit local.
- Bằng chứng tự động nền đã ghi trước đó vẫn giữ nguyên: `npm.cmd run test:api` 25/25 PASS, `npm.cmd run test:db` 2/2 PASS trên PostgreSQL/PostGIS thật, `npm.cmd run test:web` 14/14 PASS, build/typecheck PASS. Xác nhận nghiệm thu thủ công bổ sung do người dùng cung cấp; không dựng transcript hoặc số liệu chi tiết chưa được lưu.
- Bước tiếp theo trong F01: hoàn tất/đối chiếu F01.1 nếu còn tiêu chí chưa đóng, sau đó lập đặc tả và triển khai **F01.4 — Admin bootstrap/hardening** theo prompt `docs/prompts/F01-next-steps.md`.

## Sửa lỗi đăng ký và OTP theo phản hồi người dùng — 2026-10-02

- Đã thêm ô xác nhận mật khẩu cho customer; partner đã có sẵn ô này. Cả hai portal đều chặn submit khi mật khẩu nhập lại không khớp.
- Customer portal giữ contact/type trong `history.state`, khôi phục sau F5/back-forward. Màn `/verify` chỉ có ô OTP, nút xác minh/gửi lại và link Mailpit; nếu mở URL trực tiếp mà không có state, OTP bị khóa và có đường quay lại đăng ký. Không lưu OTP/password/token vào browser storage.
- UI customer/partner phân biệt `429 RATE_LIMITED` và `503 IDENTITY_DELIVERY_UNAVAILABLE`, hiển thị thời gian Retry-After hoặc hướng dẫn kiểm tra dịch vụ gửi.
- Điều tra 429: hai cổng dùng chung giới hạn IP 5 lần đăng ký/resend và 10 lần verify/login/refresh trong 15 phút; ngoài ra register/resend cùng contact bị giới hạn 3 lần. Development giờ có cấu hình riêng (50/100 theo IP, 20 theo contact); mặc định môi trường khác vẫn 5/10 và 3. Giới hạn vẫn hoạt động.
- Mailpit/API local trả HTTP 200. Thực hiện browser E2E customer qua API mới ở cổng 5081: mismatch bị chặn, đăng ký chuyển qua verify, contact còn sau F5, không báo sai validation và OTP xuất hiện trong Mailpit với format 6 chữ số. Partner browser E2E: mismatch bị chặn, OTP Mailpit, verify và login onboarding pass; HTTP lần lượt `202`, `200`, `200`. Dữ liệu dùng email tổng hợp `example.test`; hai account test local được tạo và giữ nguyên.
- `npm.cmd run build`: PASS; `npm.cmd run typecheck`: PASS. API build vào output tách riêng: PASS, 0 warning/0 error. Build solution cùng output mặc định bị BLOCKED do API đang mở trên máy giữ khóa `ShuttleBook.Api.exe`; không dừng tiến trình người dùng. Docker CLI không có trong phiên shell, nhưng API health và Mailpit endpoint local đã phản hồi HTTP 200.
- Cần người dùng khởi động lại API hiện tại ở `localhost:5080` để nạp code/config mới. Không đánh dấu F01.1/F01.3 DONE; race, rate-limit security cases và toàn bộ testcase vẫn cần nghiệm thu riêng.

### Điều chỉnh giao diện theo phản hồi tiếp theo

- Màn customer `/verify` chỉ còn ô OTP, nút xác minh/gửi lại và liên kết Mailpit local; bỏ phương thức liên hệ/email/số điện thoại khỏi màn này. Contact vẫn nằm trong `history.state` để verify/resend sau F5. Nếu không có state, OTP actions bị khóa và UI đưa người dùng quay lại đăng ký.
- Khi customer login thành công, URL được đặt rõ thành `/login`; do session chỉ ở React memory, F5 quay lại form đăng nhập.
- Customer/partner tiếp tục dùng chung API `POST /api/v1/auth/login`, refresh/logout. Đây là contract có chủ ý; backend tự tra account type/status, còn endpoint đăng ký customer và partner được tách riêng.
- Playwright UI smoke sau điều chỉnh (API mock): `/verify` chỉ có OTP, reload giữ bước/contact nội bộ, verify gửi đúng contact, login đặt URL `/login`, F5 quay lại form login: **PASS**. `npm.cmd run build` và `npm.cmd run typecheck`: **PASS**. API Postman đã được người dùng xác nhận hoạt động.

## Điều chỉnh cách kiểm thử theo yêu cầu người dùng — 2026-10-02

- Người dùng muốn kiểm tra F01 trên FE/API client thay vì chạy kịch bản bằng Windows PowerShell.
- Đã thay `docs/testing/F01-manual-verification.md` bằng hướng dẫn thao tác UI customer/partner, Mailpit và Postman; tạo `docs/testing/F01-Identity.postman_collection.json` cho các endpoint register/verify/resend/login/refresh/logout và nhánh lỗi cơ bản.
- Chạy app local vẫn cần terminal để khởi động Docker/API/Vite, nhưng các ca kiểm thử và quan sát kết quả thực hiện trong browser/Postman. DBeaver/pgAdmin là tùy chọn cho truy vấn chỉ đọc.
- Giới hạn ghi rõ: Postman tuần tự không chứng minh refresh race; audit/rate-limit/migration vẫn phải dựa trên integration/API/DB test hoặc DB client phù hợp. Không nâng testcase thành PASS bởi chỉ có hướng dẫn mới.
- Bằng chứng kiểm tra collection: JSON parse thành công; hướng dẫn UI/Postman đã được cập nhật sau đó bằng browser/API E2E local.

Cập nhật: 2026-10-02. Phạm vi tiếp tục theo mốc mới nhất: **F01.2 — Đăng nhập và phiên làm việc** và **F01.3 — Đăng ký chủ sân**, cả hai vẫn **IN_PROGRESS**. Người dùng xác nhận nghiệm thu các bước quy trình 1–3 (Planner, Designer, Implementer); xác nhận này chỉ ghi nhận mốc quy trình, không nghiệm thu F00, F01.1, F01.2 hoặc F01.3.

## Mốc quy trình 2026-10-02

- Planner: **đã được người dùng nghiệm thu**.
- Designer: **đã được người dùng nghiệm thu**.
- Implementer: **đã được người dùng nghiệm thu**.
- Reviewer + QA: đã rà soát tuần tự trong phiên này; không có sub-agent reviewer độc lập. Build/typecheck/API pass; người dùng cung cấp transcript DB 2/2 pass và browser 14/14 pass. Chi tiết testcase và phạm vi mock ở `docs/testing/F01.2-test-cases.md` và `docs/testing/F01.3-test-cases.md`.
- Sửa và xác minh: không phát hiện lỗi tái hiện được trong phần chạy được; không sửa mã khi thiếu bằng chứng PostgreSQL/browser để chẩn đoán các khoảng trống. Cần chạy lại suite bị chặn trong môi trường phù hợp.
- Hướng dẫn test tay ban đầu dùng PowerShell đã được thay bằng browser/Postman theo yêu cầu; xem trạng thái sửa lỗi 2026-10-02 ở đầu file.
- Bàn giao: tài liệu và trạng thái được cập nhật; không commit/push/merge/deploy.

### Kiểm tra phiên này (2026-10-02, Windows PowerShell, repo root)

- `npm.cmd run build`: **PASS**, cả ba web portal build thành công.
- `npm.cmd run typecheck`: **PASS**, bốn workspace typecheck thành công.
- `npm.cmd run test:api`: **PASS**, 25/25, 0 failed, 0 skipped.
- `npm.cmd run test:db`: **PASS**, người dùng cung cấp transcript chạy thành công ngày 2026-10-02: 2/2 integration tests, 0 failed, 0 skipped, gồm `BaselineMigrationTests` và `IdentityFlowTests` trên PostgreSQL/PostGIS local dùng database tạm. Lần thử trước trong phiên agent không hoàn tất; được thay thế bởi bằng chứng chạy thành công này.
- `npm.cmd run test:web`: ban đầu **BLOCKED** do cổng 5174 bận trong lần agent; sau đó người dùng cung cấp transcript chạy thành công ngày 2026-10-02: Playwright 14/14 pass trong 13.1 giây. Đây là browser suite hiện tại; các test identity mock API và không chứng minh browser → API thật → Mailpit → DB.
- `git diff --check`: **PASS** sau cập nhật tài liệu (không có whitespace error). `.env` được `git check-ignore` xác nhận đang bị ignore.
- Review mã: đọc các test identity hiện có và đối chiếu danh sách khoảng trống. Phạm vi kiểm tra lần này không độc lập với implementer và không thay thế testcase DB/browser còn chặn.

### Điểm còn mở và bước tiếp theo

- F01.2: DB integration pass 2/2 cung cấp một phần bằng chứng cho refresh rotation/reuse, logout, partner login và migration lặp. Browser suite 14/14 pass nhưng identity dùng API mock. Refresh race, suspend, logout family isolation đầy đủ, rate limit/audit, cấu hình bí mật và UI nối API thật chưa được kiểm chứng.
- F01.3: DB integration pass chứng minh partner verify/login happy path và một nhánh reject cross-flow. Browser suite 14/14 pass nhưng partner identity dùng API mock, không Mailpit thật. Duplicate/cross-role, OTP resend/attempts/concurrency, rate limit/audit và browser nối API/Mailpit thật chưa được kiểm chứng đầy đủ.
- Bước tiếp theo: khởi động lại API `localhost:5080` để nạp code/config mới; dùng UI/Postman cho ca còn lại và cập nhật testcase theo bằng chứng.
- F00 và F01.1 giữ nguyên trạng thái trước đó; nghiệm thu ba bước quy trình không làm thay đổi acceptance của các feature.
- Trạng thái feature tổng: F01.2/F01.3 **IN_PROGRESS**; bước tiếp theo là có môi trường PostgreSQL/PostGIS và cổng browser rảnh, chạy lại/bổ sung testcase, sửa lỗi nếu tái hiện, rồi review bản cuối.

Cập nhật: 2026-09-24. Feature hiện tại: [F00 — Project foundation](./features/F00-project-foundation.md), **IN_PROGRESS**.

## Đã hoàn thành

- Đọc prompt đính kèm và 8 tài liệu hiện có; trước lượt này workspace chỉ có docs, chưa có source, Git hay kết quả test.
- Khởi tạo Git local nhánh `main`; không tạo remote hoặc commit.
- Tạo `AGENTS.md` (bản chính), `AGENT.md` (liên kết), `docs/process.md` (quy trình/roadmap), đặc tả F00 và file tiến độ này.
- Planner/reviewer độc lập kiểm tra tài liệu, ghi các mâu thuẫn cần giải quyết ở feature sau vào process.md.
- Tạo `.env` local bằng script: mật khẩu PostgreSQL được sinh ngẫu nhiên, không in ra console và bị Git ignore.
- `npm.cmd run build`: PASS cho ba portal web.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\dotnet.ps1 build backend\ShuttleBook.slnx --no-restore`: PASS, 0 warning và 0 error.
- Khôi phục `dotnet-ef` theo manifest và tạo migration `20260924084114_BaselinePostgresExtensions`; migration chỉ khai báo extension `postgis` và `btree_gist`, chưa tạo schema nghiệp vụ.
- Migrator chạy hai lần với PostgreSQL/PostGIS local: PASS; lần chạy lại an toàn.
- `scripts/Test-Database.ps1`: PASS; 1/1 integration test thành công trong 45.2 giây. Test tạo database tạm, kiểm tra baseline migration, readiness, `postgis`, `btree_gist`, lịch sử migration và tự dọn database tạm.
- API contract tests: PASS, 15/15. Đã sửa test host để không ghi lỗi test vào Windows Event Log vốn đòi quyền hệ điều hành; production logging không bị thay đổi.

## Đang thực hiện

- Audit môi trường, chọn/pin toolchain và dựng khung F00.
- Chuẩn bị testcase, setup, kiểm thử/build và review nền tảng.

## Môi trường quan sát

| Công cụ | Kết quả |
|---|---|
| Git Windows | 2.47.0.windows.1 |
| Node Windows | 22.14.0 |
| npm Windows | 10.9.2, dùng `npm.cmd` trong PowerShell |
| .NET Windows | SDK hệ thống 8.0.425; SDK 10.0.401 đã có riêng trong `.tools/dotnet` |
| Docker/PostgreSQL Windows | Không tìm thấy trong PATH hoặc Docker Desktop ở đường dẫn mặc định |
| WSL | Ubuntu-24.04 WSL2 có sẵn; kiểm tra không thấy docker/psql/dotnet/node trong PATH Linux |

## Quyết định

- Giữ toàn bộ nghiệp vụ đã chốt trong AGENTS.md; không bắt đầu lại thiết kế.
- Dùng Windows PowerShell làm môi trường local chính cho workspace hiện tại.
- Không tạo trước bảng nghiệp vụ. Chính sách còn mở không chặn F00.
- Tách quy trình (`process.md`) và trạng thái (`progress.md`); giữ file `AGENT.md` theo tên người dùng yêu cầu nhưng không sao chép quy tắc.

## Bằng chứng và bước tiếp theo

- `git init -b main`: PASS.
- Doctor: Git/Node/npm/.NET/.env PASS; Docker Desktop/Compose: BLOCKED vì chưa cài hoặc chưa chạy.
- Web Playwright: PASS — 6/6 desktop/mobile smoke test pass trong 6.9 giây.
- Database migration và integration test PostgreSQL/PostGIS: PASS. Runtime health endpoint với API thực vẫn cần nghiệm thu.
- CI: NOT RUN vì repository chưa có remote/GitHub workflow run.
- Bước tiếp theo: cài/bật Docker Desktop, chạy database/migration/test thật, sửa vấn đề Playwright runner, sau đó cập nhật testcase và review. Chưa được đánh dấu F00 DONE hoặc triển khai F01 trước khi nghiệm thu phần nền.
# F01.1 — nhật ký mốc hiện tại (cập nhật 2026-09-24)

- Feature hiện tại: [F01.1 — Đăng ký và xác minh tài khoản khách](./features/F01.1-customer-registration.md), **IN_PROGRESS**.
- Đã bổ sung migration `20260924092459_CustomerRegistration`: `users`, `contact_verification_challenges`, `audit_events`, unique index contact chuẩn hóa và constraint một contact chính.
- API public: `POST /api/v1/auth/register`, `POST /api/v1/auth/verify-contact`, `POST /api/v1/auth/verification-resend`. Server luôn gán `CUSTOMER`; trạng thái đổi `PENDING_VERIFICATION` → `ACTIVE` khi OTP hợp lệ. Chưa có JWT (F01.2).
- OTP sinh bằng CSPRNG, database chỉ lưu HMAC-SHA256 có pepper; password dùng `PasswordHasher<User>`. API/log không trả OTP; local delivery dùng Mailpit trong `compose.yaml`.
- Đã bổ sung form customer web cho đăng ký, xác minh và gửi lại mã.
- Backend build: PASS, 0 warning/0 error. Web build: PASS ba portal. API regression: PASS, 15/15.
- Database regression: BLOCKED môi trường trong phiên agent hiện tại: `127.0.0.1:54329` từ chối kết nối. Khi PostGIS đang chạy, cần chạy lại `scripts/Test-Database.ps1`, migrator, smoke API và Playwright trước khi đánh dấu F01.1 DONE.

## Kiểm tra môi trường ngày 2026-10-01

- Docker Desktop đã khởi động lại thành công sau lỗi Secrets Engine; `docker info` trả về Docker Engine 29.8.0. Không đổi tên hoặc xóa thư mục socket.
- `docker compose up -d --wait`: PASS; PostgreSQL/PostGIS và Mailpit đều `healthy`.
- `scripts/Test-Database.ps1`: PASS, 2/2 integration tests trên PostgreSQL/PostGIS thật, 0 failed, 0 skipped. Dòng BLOCKED database ở mốc 2026-09-24 phía trên chỉ là tình trạng lịch sử.
- `docker compose ps`: PASS; hai container tiếp tục `healthy` sau kiểm thử.

## Rà soát F01.2/F01.3 ngày 2026-10-01

- Trạng thái cả F01.2 và F01.3: **IN_PROGRESS**, chưa đủ bằng chứng để đánh dấu DONE. Mã API, migration identity/session, UI customer/partner và test cơ bản đã có; F01.4 chưa bắt đầu.
- `scripts/dotnet.ps1 run --project backend/src/ShuttleBook.Migrator` chạy hai lần: PASS; migration trên database local áp dụng/lặp lại an toàn.
- API thực `GET /health/live` và `GET /health/ready` tại `http://localhost:5080`: PASS, đều HTTP 200 `Healthy` sau migration. API tạm dùng để smoke test đã dừng.
- `scripts/Test-Database.ps1`: PASS 2/2 trên PostgreSQL/PostGIS thật. `IdentityFlowTests` kiểm tra luồng customer/partner email, login, rotation/reuse/logout cơ bản và protected `/me`; chưa phủ toàn bộ testcase cạnh tranh, phone, suspend, validation/rate limit/audit.
- Bằng chứng phiên kiểm thử trước trong cùng ngày: backend/web build PASS, API tests 25/25 PASS, Playwright 14/14 PASS sau khi đặt `PLAYWRIGHT_BROWSERS_PATH=.tools/playwright`. Playwright identity đang dùng route mock, chưa chứng minh browser → API thật → Mailpit → DB.
- Các bảng testcase `docs/testing/F01.1-test-cases.md`, `F01.2-test-cases.md`, `F01.3-test-cases.md` vẫn là checklist chưa nghiệm thu từng dòng; không diễn giải `NOT RUN` ở đó thành PASS chỉ vì regression tổng pass.
- Bước tiếp theo: bổ sung test PostgreSQL/API cho refresh đồng thời, suspend, family isolation, partner duplicate/cross-role/OTP resend và rate limit; chạy một luồng browser với API/Mailpit thật; cập nhật từng testcase bằng lệnh/kết quả rồi review độc lập. Sau F01.2/F01.3 mới chuyển F01.4 Admin seed/hardening.

## Git và lệnh chạy local ngày 2026-10-01

- Git đã được khởi tạo trước đó ở `main`; chưa có commit/remote. Đã sửa ownership riêng thư mục `.git` về tài khoản người dùng hiện tại, nên `git status` chạy bình thường, không cần cấu hình `safe.directory` toàn máy. Không commit/push.
- `.env` có sẵn, được Git ignore; đã bổ sung `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS`, `VITE_API_BASE_URL` mà không in/ghi đè secret. `.env.example` chỉ chứa placeholder.
- Thêm lệnh npm ngắn cho database, migration, API, Worker, ba web portal và kiểm thử; script tự nạp `.env` cùng đường dẫn Playwright local. Xem `README.md` và `docs/setup.md`.
- Xác minh: `npm.cmd run db:up` PASS (PostgreSQL/Mailpit healthy); `npm.cmd run db:migrate` PASS; `npm.cmd run dev:api` + `/health/ready` PASS (HTTP 200); `npm.cmd run dev:partner` PASS (HTTP 200); `npm.cmd run dev:worker` PASS trong Development; `npm.cmd run build` PASS ba portal; `npm.cmd run test:api` PASS 25/25; `npm.cmd run test:web` PASS 14/14 sau build mới. Các tiến trình dev dùng để kiểm tra đã dừng sau đó.

## Kiểm tra OTP local ngày 2026-10-01

- API đang chạy: `/health/live` và `/health/ready` đều HTTP 200 `Healthy`; Mailpit HTTP 200 và SMTP 1025 chấp nhận kết nối. Lỗi `Unhealthy` người dùng thấy trước đó không tái hiện ở thời điểm kiểm tra.
- Gửi một đăng ký partner với contact thử nghiệm mới qua API thật: HTTP 202; số thư trong Mailpit tăng từ 2 lên 3. Không đọc hoặc in OTP. Vì vậy đường gửi local hoạt động; cấu hình hiện chỉ gửi vào Mailpit, không tới hộp thư ngoài.
- Giao diện customer/partner trong chế độ Development đã hiển thị liên kết Mailpit ở bước nhập OTP, đồng thời giữ thông báo 202 chung để tránh dò tài khoản.
- `npm.cmd run build`: PASS cho ba portal sau thay đổi giao diện. `npm.cmd run test:web`: BLOCKED vì cổng 5174 đang do dev server hiện có sử dụng; Playwright yêu cầu cổng preview trống. Không dừng tiến trình người dùng để chạy test. Kết quả Playwright 14/14 trước thay đổi vẫn là bằng chứng lịch sử, không tính là lần kiểm tra mới.
- Smoke trực tiếp trên partner dev server đang chạy: đăng ký contact thử nghiệm qua UI thật đến màn hình nhập OTP, liên kết Mailpit hiển thị và trỏ đúng `http://localhost:8025`; Mailpit tăng lên 4 thư. `/health/ready` vẫn HTTP 200 `Healthy`. Đây là bằng chứng cho đường UI → API → Mailpit trong môi trường local, chưa thay thế toàn bộ testcase F01.3.
