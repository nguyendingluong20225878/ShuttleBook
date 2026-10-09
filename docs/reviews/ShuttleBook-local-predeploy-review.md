# ShuttleBook — review local trước deploy, tham chiếu ALOBO

Ngày review: **2026-10-09 (Asia/Saigon)**. Prompt: [ShuttleBook-local-predeploy-review-alobo](../prompts/ShuttleBook-local-predeploy-review-alobo.md). Phạm vi là source hiện tại, build/test local, ảnh UI có sẵn và nguồn ALOBO công khai; **chưa deploy hoặc thử giao dịch thật**.

## 1. Kết luận

**NO-GO cho deploy/nhận giao dịch thật hiện nay.** Luồng MVP đã có bằng chứng local mạnh, nhưng F09 chưa triển khai, CI hosted chưa được chứng minh, chưa có restore drill cho PostgreSQL + media, và các rủi ro tiền/quota quote còn mở. Đây là quyết định về readiness, không phủ nhận các mốc F01–F07/M03 đã nghiệm thu theo phạm vi local. **Production: NOT VERIFIED.**

Năm việc ưu tiên: (1) sửa/kiểm số tiền casual vượt `Number.MAX_SAFE_INTEGER`; (2) thiết kế quota quote theo tài khoản và test cạnh tranh trên PostGIS; (3) sửa CI Ubuntu gọi `powershell.exe` rồi quan sát workflow chạy thật; (4) backup và restore vào DB/media thử riêng; (5) xác minh sức khỏe Worker/outbox, cảnh báo và kịch bản release/rollback. Bản sửa UI sau audit vẫn cần nghiệm thu tay theo [checklist](../testing/UI-post-audit-improvements-manual.md). **F08 CANCELLED** theo quyết định mới nhất; feature mời/ủy quyền/phân quyền nhân viên không là blocker hoặc hạng mục so sánh ALOBO.

Độ tin cậy: **cao** cho kết quả lệnh và finding source trực tiếp; **trung bình** cho nhận xét hình học/UI từ screenshot fixture và CSS; **thấp/UNKNOWN** cho hiệu năng, usability người dùng thật, bảo mật/độ bền ngoài local và các màn ALOBO phải đăng nhập. Không chấm phần trăm hoàn thành.

## 2. Phạm vi, Git và bằng chứng

- Git ở `main`; trước review đã có thay đổi chưa commit trong tài liệu F08/F09 và progress. Lượt này giữ nguyên chúng, không reset/stage/commit. Source ứng dụng không có diff trước review.
- Đọc AGENTS/process/progress/README/00–05, F09, M03, đặc tả/kết quả UI mới, các review cũ và source liên quan. Review cũ còn các finding F08 hoặc session/queue/UI **đã lỗi thời**; dùng chúng làm lịch sử, không copy vào backlog hiện tại. [Kết quả sửa UI mới](../testing/UI-post-audit-improvements-results.md) đã ghi 206 PASS / 8 SKIP browser và 3 ca focused PostGIS tại thời điểm đó; đây là evidence **lịch sử**, tách khỏi lệnh review này.
- Ảnh đã xem: `.local/ui-improvements/screenshots/customer-distance-mobile.png`, `customer-login-no-sidebar-mobile.png`, `admin-revision-mobile.png`, `artifacts/partner-ui/overview-mobile.png`, `operations-mobile.png`. Ảnh là fixture từ các lượt trước, không phải screenshot mới của phiên review. Chúng hỗ trợ nhận xét bố cục; không chứng minh contrast, thiết bị thật, backend hoặc mọi trang.
- Nguồn ALOBO truy cập trong lượt review: [website](https://www.alobo.vn/), [đặt lịch online, cập nhật 06/04/2026](https://wiki.alobo.vn/article/huong-dan-dat-lich-online-va-duyet-don/), [duyệt đơn, cập nhật 29/05/2026](https://wiki.alobo.vn/article/huong-dan-duyet-don-dat-lich-online/), [bảng giá, cập nhật 22/04/2026](https://wiki.alobo.vn/article/huong-dan-cai-bang-gia/), [mở online và QR, cập nhật 19/05/2026](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/). Đây là tài liệu công khai; không thử app ALOBO bằng account.

### Trạng thái feature theo tài liệu hiện hành

| Feature | Trạng thái ghi trong spec/progress | Điều còn mở liên quan readiness |
|---|---|---|
| F00 | `IN_PROGRESS` trong [spec](../features/F00-project-foundation.md) | Không tự chốt nền tảng DONE chỉ vì build/test local. |
| F01.1–F01.4 | `DONE` local trong các [spec F01](../features/F01.1-customer-registration.md) | Không suy ra production auth đã xác minh. |
| F02 | `IN_PROGRESS` theo điều kiện media S3 trong [spec](../features/F02-partner-onboarding.md); local adapter/PostGIS đã đạt theo lịch sử | S3 thật hoãn/NOT RUN; cần quyết định media durable cho môi trường deploy. |
| F03 | `IN_PROGRESS`, local đã được tạm chấp thuận trong [spec](../features/F03-court-operations.md) | Giữ trạng thái cho đến khi gate còn mở được quyết định. |
| F04 | `DONE` local trong [spec](../features/F04-public-discovery.md) | MapTiler/S3 provider thật chưa thành PASS. |
| F05 | `IN_PROGRESS`, chờ nghiệm thu tay theo [spec](../features/F05-casual-booking.md) | Không đổi trạng thái do F06/F07 đã có code hoặc test. |
| F06, F07, M03 | `DONE` local theo [F06](../features/F06-payment-confirmation.md), [F07](../features/F07-fixed-series.md), [M03](../features/M03-browser-sessions.md) | Giao dịch thật, backup, CI, provider vẫn tách gate. |
| F08, F09 | `CANCELLED` / `NOT STARTED` theo [F09](../features/F09-production-readiness.md) | F08 không là dependency; F09 là workstream readiness. |

### Gate mới trong lượt này

Chạy ở repo root `C:\Users\luong\Desktop\CLong` bằng **PowerShell**. `PASS/FAIL` dưới đây là kết quả runtime mới, `SKIP` không tính PASS.

| Lệnh/gate | Kết quả | Giới hạn/ghi chú |
|---|---|---|
| `npm.cmd run typecheck` | **PASS** 4 workspace | TypeScript local. |
| `npm.cmd run build` | **PASS** 3 Vite app | Build production asset local. |
| `npm.cmd run test:api` | **PASS 96/96**, 0 fail/skip | ASP.NET test host, không thay DB integration. |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore` | **PASS**, 0 warning/error khi chạy lại tuần tự | Lượt đầu **FAIL do khóa file** vì review chạy trùng API test; không phải lỗi source. |
| `npm.cmd run test:web -- --workers=2` | **FAIL: 245 PASS / 1 FAIL / 8 SKIP** | Fixture browser desktop/mobile; ca M03 mobile kiểm activity trailing nhận 1 request trước assert 59 giây. Không là live API/DB. |
| `npm.cmd run test:web -- --workers=1 --project=mobile --grep "real user action inside the throttle window"` | **PASS 1/1** | Chỉ rerun focused, không xóa kết quả suite FAIL. |
| Focused `--repeat-each=5` cùng ca mobile | **PASS 5/5** | Xác nhận ca không fail ở lượt lặp, nhưng full suite vẫn FAIL và race/timing chưa được chứng minh hết. |
| `npm.cmd run test:db` | **BLOCKED — chưa hoàn tất** | Full suite không in summary sau khoảng 9 phút nên review đã ngắt; không kết luận test treo hoặc lỗi DB. DB tests chạy tuần tự và mỗi ca có thể mất lâu. Không tính PASS. |
| `npm.cmd run test:db -- --filter FullyQualifiedName~BaselineMigrationTests` | **PASS 1/1**, 1 phút 28 giây | Migration trên DB PostgreSQL/PostGIS tạm; xác nhận môi trường có thể chạy test, không thay full suite. |
| `docker compose ps` | **BLOCKED** | `docker` không có trong PATH PowerShell phiên này; cổng 54329 mở. Cổng mở không tự chứng minh container healthy. |
| Browser → API → Worker → PostGIS/Mailpit mới; manual UI, NVDA/contrast/zoom 200%, MapTiler/S3 provider, backup restore, hosted CI, load/security/pilot | **NOT RUN** | Không suy PASS từ source, screenshot hoặc evidence lịch sử. |

## 3. Inventory trang và ma trận đồng nhất UI

Inventory từ route/source, chưa đồng nghĩa đã mở và chụp từng trang trong phiên này:

| Cổng | Trang/nhóm màn hình chính | Trạng thái cần quan sát |
|---|---|---|
| Customer/Guest | `/venues`, chi tiết cơ sở/lịch sân, login/register/verify, review vãng lai/cố định, chi tiết/lịch sử đơn, thông báo | Guest/đã đăng nhập, không cấp vị trí, trống/lỗi, quote giữ tạm/hết hạn, awaiting/review/confirmed, F5/Back. [Router](../../apps/customer-web/src/App.tsx), [Shell](../../apps/customer-web/src/components/CustomerShell.tsx). |
| Partner | Tổng quan, hồ sơ, cơ sở, sân, lịch & giá, đơn/list + trang chi tiết, ảnh, thanh toán & QR, thông báo | Nháp/chờ duyệt/active, scope đổi cơ sở, đơn vãng lai/cố định, lỗi/deny, mobile menu. [Navigation](../../apps/partner-web/src/features/workspace/navigation.ts), [Shell](../../apps/partner-web/src/layouts/PartnerShell.tsx). |
| Admin | Tổng quan, duyệt hồ sơ/list + revision, thông báo | Hàng đợi nhiều trang, current/proposed QR, hai bước phê duyệt, conflict/deny, mobile bảng rộng. [Navigation](../../apps/admin-web/src/features/workspace/navigation.ts), [Shell](../../apps/admin-web/src/layouts/AdminShell.tsx). |

| Thuộc tính | Customer | Partner | Admin | Nhận xét |
|---|---|---|---|---|
| Màu, font, nền | Teal `#0f766e`, system font, nền `#f3f6f6` | Cùng teal/nền, Segoe UI ưu tiên | Cùng teal/nền, system font | **Khá đồng nhất về thương hiệu.** Token đặt ba tên khác nhau và nhiều giá trị hard-code: [Customer CSS:1](../../apps/customer-web/src/styles.css#L1), [Partner CSS:1](../../apps/partner-web/src/assets/partner.css#L1), [Admin CSS:1](../../apps/admin-web/src/admin.css#L1). |
| Navigation/shell | Sidebar workspace; màn account dùng header ngang | Sidebar + mobile bar | Sidebar + mobile bar | Cùng tinh thần nhưng brand/icon/nav label/footer và breakpoint tự triển khai ba lần: [CustomerShell:11](../../apps/customer-web/src/components/CustomerShell.tsx#L11), [PartnerShell:9](../../apps/partner-web/src/layouts/PartnerShell.tsx#L9), [AdminShell:8](../../apps/admin-web/src/layouts/AdminShell.tsx#L8). Khác biệt account là chủ ý theo tác vụ. |
| Button, form, card | Chồng CSS theo trang, `primary-action` cho tạo đơn | `button[type=button]` mặc định secondary, `primary-action` riêng | button mặc định primary, `.admin-button-secondary` riêng | **Rủi ro drift của hierarchy:** cùng ngữ nghĩa secondary/primary nhưng rule khác; [Customer CSS:145](../../apps/customer-web/src/styles.css#L145), [Partner CSS:11](../../apps/partner-web/src/assets/partner.css#L11), [Admin CSS:11](../../apps/admin-web/src/admin.css#L11). Chưa thấy lỗi click do CSS. |
| Spacing/radius/content | 14–20px card, booking panel 16px, max content riêng | `--radius:16px`, 248px sidebar, nhiều form xếp dọc | 248px sidebar, panel 16px, bảng có scroll mobile | Có nhịp gần nhau nhưng không có scale dùng chung. Trang Partner Lịch & giá trên ảnh mobile dài và khó định vị thao tác; xem lại bằng usability thật trước khi đổi luồng. |
| Error/loading/empty/status | Có status/alert và quote countdown | Có message/notice, trạng thái hồ sơ/đơn | Có state, review current/proposed và confirm | Nội dung nghiệp vụ tốt nhưng màu/shape và câu chữ chưa có contract hiển thị chung. Cần kiểm trạng thái cùng nghĩa trên cả ba cổng bằng visual regression và manual review. |

`packages/ui` hiện chỉ export `PortalShell` và `browser-session`; chưa export token/component design system cho ba cổng: [package.json](../../packages/ui/package.json), [PortalShell](../../packages/ui/src/PortalShell.tsx). Đây là nguyên nhân bảo trì/drift từ cấu trúc source, **không phải kết luận rằng mọi trang hiện sai phong cách**. Ảnh Customer login/Tìm sân, Partner Tổng quan/Lịch & giá và Admin duyệt revision có cùng màu nền, nhấn màu teal, card sáng. Bộ ảnh chưa bao phủ mọi trang hoặc đo độ tương phản.

## 4. Đối chiếu ALOBO theo nhiệm vụ

**Bỏ qua hoàn toàn feature phân quyền/tài khoản nhân viên của ALOBO.** Nguồn ALOBO là guide/marketing, không xác nhận concurrency, bảo mật, uptime hoặc toàn bộ UI runtime. `UNKNOWN` là thiếu bằng chứng, không là điểm trừ.

| Nhiệm vụ | ALOBO công bố | ShuttleBook có bằng chứng | Kết luận |
|---|---|---|---|
| Tìm sân → chọn lịch → thanh toán | Hướng dẫn [đặt lịch online](https://wiki.alobo.vn/article/huong-dan-dat-lich-online-va-duyet-don/) có tìm sân, chọn ngày/giờ/sân, nhập thông tin, thanh toán QR. | Guest tìm cơ sở và xem grid ca 30 phút; quote giữ chỗ tạm theo TTL; [VenueSearchPage](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx), [BookingReview](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L24), browser fixture PASS theo nhóm. | **PARITY-PARTIAL** ở tác vụ; chưa đo cùng người dùng/thời gian, không kết luận ai nhanh hơn. |
| Chủ sân duyệt và đối chiếu tiền | [Guide duyệt đơn](https://wiki.alobo.vn/article/huong-dan-duyet-don-dat-lich-online/) yêu cầu kiểm bill, tài khoản và tổng tiền trước xác nhận. | Partner có chi tiết đơn, proof và quyết định; F06/M03 có test local lịch sử, [BookingDecisions](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx). | **PARITY-PARTIAL** về quy trình thủ công. Không suy ALOBO hay ShuttleBook tự phát hiện tiền nếu không có tích hợp ngân hàng. |
| QR giúp khách chuyển đúng số tiền | [Guide QR](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/) nói có tùy chọn QR theo số tiền đơn. | QR do Owner upload; số tiền đơn hiển thị và snapshot riêng, [BookingReview](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L65). | **GAP tiện lợi có chứng cứ:** ShuttleBook không tự tạo QR riêng theo số tiền từng đơn như tùy chọn ALOBO. Khách cần đối chiếu số tiền hiển thị; việc app ngân hàng có điền sẵn hay không phụ thuộc QR Owner upload. Đề xuất P2 sau khi chốt provider/contract; không tự chuyển sang ngân hàng xác nhận. |
| Thiết lập giá/lịch | [Guide bảng giá](https://wiki.alobo.vn/article/huong-dan-cai-bang-gia/) mô tả ngày, khung giờ, loại sân. | Owner cấu hình từng court/ca, có xem giá và rule ưu tiên; [PartnerOperations](../../apps/partner-web/src/PartnerOperations.tsx). | **PARITY-PARTIAL.** Nhập nhiều rule trên mobile ShuttleBook có thể tốn cuộn; chưa đo tác vụ hai bên để kết luận nhanh/chậm. |
| Bật/tắt online và chia sẻ link | [Guide mở online](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/) hướng dẫn bật/tắt theo chi nhánh và lấy link đặt sân. | Chưa tìm thấy control tương đương trong navigation/operations đã đọc. | **GAP tùy chọn sản phẩm**, không là lỗi MVP/deploy theo scope hiện tại; xem xét sau pilot nếu Owner cần. |
| Hiệu năng, chống trùng, khả năng khôi phục, Admin nội bộ | Nguồn công khai đã đọc không cung cấp bằng chứng tương đương. | ShuttleBook có allocation/exclusion, idempotency và test local; production còn NOT VERIFIED. | **UNKNOWN khi so hơn/kém ALOBO.** Chỉ ghi đây là điểm mạnh nội bộ của ShuttleBook có evidence. |

## 5. Điểm mạnh và findings ưu tiên

### Điểm mạnh có căn cứ

- Luồng Guest có tìm cơ sở/nearby và lịch sân; Customer thấy từng ca, tổng tiền và countdown quote, không cần đăng nhập chỉ để duyệt lịch. [VenueSearchPage](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx), [BookingPages:65](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L65). Không đủ bằng chứng để nói hơn ALOBO về conversion.
- Booking vãng lai và cả kỳ cố định dùng allocation/quote reservation trong PostGIS; old quote cùng court được thay trong transaction và expiry được cleanup. [QuoteReservations:46](../../backend/src/ShuttleBook.Infrastructure/Bookings/QuoteReservations.cs#L46), [BookingEndpoints:50](../../backend/src/ShuttleBook.Api/Bookings/BookingEndpoints.cs#L50); test DB mới xem bảng gate. Không suy có quota toàn tài khoản.
- Customer/Partner session M03 dùng cookie HttpOnly tách portal, token truy cập trong memory và có guard thao tác thật; browser suite kiểm F5/cross-tab/idle. [browser-session.ts](../../packages/ui/src/browser-session.ts), [M03 spec](../features/M03-browser-sessions.md). Một ca timing flaky ở suite lượt này còn cần xử lý theo finding R04.
- Admin revision có đối chiếu current/proposed và xác nhận hai bước; ảnh fixture mobile không tràn ngang toàn trang, bảng riêng cuộn. [AdminApprovals](../../apps/admin-web/src/AdminApprovals.tsx), [kết quả UI](../testing/UI-post-audit-improvements-results.md).
- Ba cổng đã cùng nền màu, typography cơ bản, card và sidebar; Customer auth có header ngang theo yêu cầu mới. Điểm này được kiểm bằng CSS và các screenshot nêu trên, chưa là visual acceptance toàn bộ màn hình.

### Findings

| ID / mức | Kịch bản và evidence | Ảnh hưởng / độ tin cậy | Cải tiến nhỏ nhất và test chấp nhận |
|---|---|---|---|
| **R01 P0 — tiền casual** | Quote API trả `q.Amount` dạng JSON number [BookingEndpoints:71](../../backend/src/ShuttleBook.Api/Bookings/BookingEndpoints.cs#L71); Customer khai báo `amount:number` và format `money(quote.amount)` [BookingPages:16](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L16), [BookingPages:67](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L67). API đã cho phép tổng đến `999_999_999_999_999_999` [BookingEndpoints:189](../../backend/src/ShuttleBook.Api/Bookings/BookingEndpoints.cs#L189). | Có thể hiển thị sai VND khi quá JS safe integer; **source-confirmed**, chưa tạo booking biên mới. | Thêm chuỗi exact cho quote casual/slot, dùng nó ở UI; giữ field cũ nếu cần tương thích. Test boundary API + browser hiển thị tổng/từng ca, snapshot booking/payment giữ nguyên. |
| **R02 P0 — quote giữ nhiều court** | `ReplaceOwn` chỉ tìm quote cùng `customerId` **và** `courtId` [QuoteReservations:46](../../backend/src/ShuttleBook.Infrastructure/Bookings/QuoteReservations.cs#L46); limiter `booking-quote` theo IP 60/phút [Program:129](../../backend/src/ShuttleBook.Api/Program.cs#L129). | Một tài khoản có thể tạo nhiều hold ở các sân khác nhau, làm giảm lịch trống; **rủi ro từ source**, chưa thử tải/quota. | Chốt policy quota theo tài khoản (số hold đồng thời và hành vi thay thế/từ chối), contract lỗi rõ; PostGIS test nhiều court, hai request cạnh tranh, TTL, scope và không phá flow cố định. |
| **R03 P0 — CI Ubuntu** | Workflow `runs-on: ubuntu-latest` gọi `npm run build/test:web` [ci.yml:7](../../.github/workflows/ci.yml#L7); scripts npm gọi `powershell.exe` [package.json](../../package.json). | Workflow web có nguy cơ lỗi trước khi test; **source-confirmed**, hosted run chưa quan sát. | Dùng script cross-platform hoặc cài PowerShell/đổi lệnh CI rõ ràng; bắt buộc workflow hosted build/test PASS ở commit ứng viên. |
| **R04 P1 — gate session timing** | Suite mới FAIL 1 ca mobile tại assert 59 giây [m03-browser-sessions.spec.ts:120](../../tests/web/m03-browser-sessions.spec.ts#L120); focused rerun 1/1 và repeat 5/5 PASS. | Chưa kết luận idle policy sai; **runtime flaky/finding test**, mức tin cậy trung bình. | Kiểm trace, tách mốc timer khỏi thời gian login/clock advance, assert theo thời điểm event với tolerance; chạy lặp trong suite và DB clock boundary để loại lỗi sản phẩm. |
| **R05 P1 — UI drift** | Màu giống nhau nhưng ba root CSS khai báo token khác tên, button/shell/card định nghĩa riêng; [Customer CSS](../../apps/customer-web/src/styles.css), [Partner CSS](../../apps/partner-web/src/assets/partner.css), [Admin CSS](../../apps/admin-web/src/admin.css), `packages/ui` chưa có token/shared controls. | Dễ lệch hierarchy/spacing khi sửa từng portal; **source-confirmed risk**. | Tạo token + Button/Field/Panel/Status primitives dùng chung, migrate một family mỗi lần. Visual/manual matrix tất cả trang desktop/mobile, không đổi nghiệp vụ/route. |
| **R06 P1 — độ bền local** | F09 yêu cầu backup/restore PostgreSQL + media Local [F09:12](../features/F09-production-readiness.md#L12); tìm trong `scripts/`/`.github/` chưa thấy runbook/script restore drill. | Mất DB hoặc ảnh QR/proof có thể không phục hồi đồng bộ; **readiness gap**, chưa chạy drill. | Viết quy trình backup có retention/vị trí ngoài vùng vận hành; restore vào DB/media thử riêng, đối chiếu booking/payment/series/allocation và quyền ảnh; ghi RPO/RTO mục tiêu trước deploy. |
| **R07 P1 — nghiệm thu UI/a11y** | Bản sửa UI mới ghi cần nghiệm thu tay; screenshot chỉ bao phủ vài trang, không đo NVDA/contrast/zoom 200% [UI results](../testing/UI-post-audit-improvements-results.md). | Chưa thể chứng nhận “mọi trang đồng nhất và dùng tốt”; **evidence gap**, không gán FAIL accessibility. | Duyệt inventory ở mục 3 trên 375/768/1024/1440, zoom 200%, keyboard/focus/target, ảnh trạng thái thường/lỗi/trống; lưu issue và sửa các lỗi tái hiện. |

## 6. Mini design system và thứ tự xử lý

Đề xuất contract UI dùng chung trong `packages/ui` nhưng triển khai từng family nhỏ, giữ CSS riêng cho lịch sân, biểu mẫu vận hành và bảng Admin:

| Thành phần | Quy tắc đề xuất | Acceptance local |
|---|---|---|
| Tokens | `color.brand`, `text`, `muted`, `surface`, `border`, `success/warning/error`; spacing 4/8/12/16/24/32; radius 8/12/16; typography body 16, label 14, h1 26–34; breakpoint theo nội dung. | Ba cổng lấy token chung; không còn ba bản định nghĩa khác tên cho cùng semantic; screenshot regression không làm đổi ý nghĩa trạng thái. |
| Hành động | Primary cho hành động chính của màn, secondary cho xem/tải lại/quay lại, danger cho từ chối có hậu quả; nút ≥44px, loading/disabled/confirmation nhất quán. | Kiểm các trang Customer quote/payment, Partner quyết định đơn, Admin duyệt; tab/Enter/Space/focus visible. |
| Form/card/status | Một Field với label/hint/error, Panel, InlineNotice, StatusBadge; câu chữ trạng thái theo từ điển chung nhưng có mô tả theo role. | Cùng trạng thái booking/payment không đổi màu/nghĩa giữa ba cổng; lỗi cạnh input, không dựa màu đơn độc. |
| Navigation | Dùng cùng brand mark, chiều cao header, spacing/nav active/footer; giữ Customer account header ngang và sidebar workspace theo tác vụ. | Deep link, Back/Forward/F5, skip link, mobile menu và focus kiểm trên ba cổng. |

Thứ tự: **P0** R01 → R02 → R03, song song chuẩn bị F09 backup/monitoring/release (R06) theo contract riêng; **P1** xử lý R04 và nghiệm thu UI/a11y R07, rồi token/primitives R05 theo từng màn; **P2** xem QR điền tiền, copy link nhận online, nhập giá hàng loạt sau khi có feedback Owner. S/M/L tương đối: R01 M, R02 M/L tùy policy, R03 S/M, R04 S, R05 M, R06 M/L, R07 M. Không cam kết ngày khi chưa chốt nguồn lực/policy.

## 7. Checklist local trước khi xin phép deploy

1. P0 tiền/quota/CI có scope, acceptance, API/error contract, data và testcase; sửa xong, API/PostGIS/browser boundary + concurrency PASS; workflow Ubuntu hosted PASS.
2. Build/typecheck/API/DB/web full suite ổn định, không còn FAIL; các `SKIP` live phải có gate riêng bằng API/Worker/PostGIS/Mailpit thật trên database thử. Manual UI mới đạt từng nhóm, không dùng fixture thay nghiệm thu tay.
3. PostgreSQL/PostGIS migration fresh/repeat/upgrade trên bản dữ liệu thử; backup DB và media Local, restore drill và đối chiếu private QR/proof. Không thử restore đè database đang dùng.
4. Health API/Worker/DB, outbox retry/alert, owner chờ quá SLA, log có traceId và không rò secret/PII; thử Worker restart/failure và rollback release. Chốt ngưỡng tải, RPO/RTO và người xử lý cảnh báo.
5. Kiểm HTTPS/CORS/cookie/secrets/release config ở môi trường thử **khi có quyền tạo/triển khai**. S3 thật vẫn hoãn theo quyết định; nếu còn media Local thì volume và backup là gate bắt buộc. Pilot/giao dịch thật/production cần quyền riêng.

Không biến báo cáo này thành feature DONE. F09 và production vẫn **NOT STARTED/NOT VERIFIED** đến khi có bằng chứng của từng lát cắt.
