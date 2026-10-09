# ShuttleBook — đánh giá sản phẩm, tham chiếu ALOBO

Ngày thực hiện: **08/10/2026**, múi giờ Asia/Saigon. Prompt thực thi: [ShuttleBook-product-review-alobo](../prompts/ShuttleBook-product-review-alobo.md). Vai trò đánh giá: kỹ sư phần mềm có 10 năm kinh nghiệm; đây là góc nhìn chuyên môn được yêu cầu, không phải tuyên bố trải nghiệm cá nhân sử dụng ALOBO hay vận hành ShuttleBook production.

## 1. Kết luận và quyết định

**ShuttleBook đã đạt một bản MVP local với luồng đặt sân và thanh toán tương đối hoàn chỉnh. Có thể bắt đầu F08 và chuẩn bị pilot; chưa nên mở công khai nhận giao dịch thật ngay.** Điểm mạnh nằm ở tính nhất quán giao dịch: PostgreSQL/PostGIS là nguồn chuẩn, allocation chống trùng, giá/QR snapshot, lịch cố định all-or-none và thanh toán thủ công có đối chiếu.

Người dùng đã xác nhận **nghiệm thu F07**, gồm phần quote giữ tạm và Customer UI mới, trong phiên này. Sau khi hoàn tất đánh giá, **F07 được chốt DONE theo phạm vi local đã duyệt**. Điều này không biến S3 thật, CI hosted, kiểm tải, backup/restore hoặc production thành PASS.

Khoảng trống sản phẩm hiện tập trung vào: nhân viên vận hành, độ bền media, release/khôi phục dữ liệu, trải nghiệm phiên và tra cứu nhiều đơn. Không có dữ liệu để chấm “đã hoàn thành 80%”, so sánh hiệu năng với ALOBO hoặc dự báo doanh thu. **Độ tin cậy cao cho bằng chứng core local; trung bình cho nhận xét UX từ source/screenshot; thấp cho mức phù hợp thị trường.**

Khuyến nghị thứ tự: **sửa các điểm cản trở pilot và kiểm auth race → F08 nhân viên → F09.1 media/CI/backup/giám sát → pilot 1–3 cơ sở → báo cáo/phiên/chia sẻ → tính năng tăng trưởng có nhu cầu thực.** Các sửa và feature tiếp theo là đề xuất; lượt đánh giá này chỉ cập nhật tài liệu, không tự triển khai chúng.

## 2. Phạm vi, nguồn và bằng chứng

Đã đối chiếu AGENTS, process/progress, docs README và 00–05, đặc tả/testcase F01–F07, prompt mới, source ba portal/API/Worker/migration và log/TRX. Nền Git `060fc1c`; F07 và điều chỉnh đang có thay đổi chưa commit. Root tổng hợp, hai reviewer độc lập đọc backend và frontend; không sửa production code trong lượt review này.

| Bằng chứng | Trạng thái và kết luận | Giới hạn |
|---|---|---|
| Build/TypeScript ngày 08/10 trước lượt review | PASS backend 0 warning/error; typecheck workspace và Customer build cuối | Bằng chứng lịch sử cùng phiên ngày; không gọi là build mới của lượt đánh giá |
| API ngày 08/10 | PASS 96/96, 1 phút 14 giây; đã đọc lại `.local/quote-hold-api-tests.log` | Không thay load/security test |
| Customer browser | PASS 72/72 và affected 28/28; đã đọc hai log trong `.local/f07-customer-style/` | Lượt 28 gồm ca lặp và 4 ca mới, không cộng thành 100 ca duy nhất; phần này dùng UI fixture |
| Live browser/API/Worker/PostGIS/Mailpit | PASS 8/8, 52,5 giây; đã đọc `.local/quote-hold-live.log` | Có quote giữ chỗ trước create, proof/report/confirm; MapTiler fixture, media Local |
| PostgreSQL/PostGIS | PASS qua các lượt: 72 testcase duy nhất đều có kết quả Passed trong TRX | Lượt rộng 68: 62 PASS/6 FAIL fixture, sửa rồi kiểm lại; không gọi một lượt 72/72 PASS |
| Người dùng | Xác nhận nghiệm thu F07 trong lượt hiện tại | Xác nhận tổng thể; không tự dựng biên bản PASS từng thao tác tay |
| Review hiện tại | PASS việc đọc/đối chiếu artifact; source findings ở mục 5 | Không dùng static review để khẳng định runtime hoặc chứng nhận bảo mật |
| Health public trong lượt review | Không truy cập được localhost:5080 và 5173–5175 trong probe ngắn | Duyệt luồng mới NOT RUN; không kết luận lỗi ứng dụng khi chưa biết app đang chạy hay không |
| S3 AWS, CI hosted, tải, restore, pilot/production | NOT RUN hoặc chưa có evidence tương ứng | Code adapter/workflow và kế hoạch không đủ để chấm PASS |
| Nguồn ALOBO | Đã truy cập lại ba nguồn chính thức ngày 08/10/2026 | Chưa đăng nhập app ALOBO; không đánh giá backend, độ ổn định hoặc phí từ suy đoán |

Các gate, lệnh và lịch sử lỗi/recheck: [kết quả quote hold](../testing/F07-quote-reservations-results.md), [F07 testcase](../testing/F07-test-cases.md), [progress](../progress.md). Lệnh đối chiếu của lượt review: `git status --short`, `git rev-parse --short HEAD`, `rg` source/spec, đọc log/TRX và tổng hợp testName/outcome bằng PowerShell XML. Không chạy lại regression rộng vì không đổi code.

## 3. Trạng thái feature và mâu thuẫn cần quản lý

| Feature | Trạng thái có căn cứ | Việc còn mở |
|---|---|---|
| F01.1–F01.4 | DONE local; Admin có điều chỉnh idle 30 phút từ thao tác cuối | Không suy ra Customer/Partner có cùng cơ chế restore; xem auth race ở dưới |
| F02 | Spec vẫn IN_PROGRESS do nghiệm thu S3; local onboarding/approval đã đạt | Tách quyết định nghiệm thu local và provider thật; chưa tự đổi trạng thái |
| F03 | IN_PROGRESS trong spec, người dùng đã tạm chấp thuận local ngày 06/10 | Chốt lại phạm vi acceptance phụ thuộc F02; chưa tự đổi DONE |
| F04 | DONE local theo quyết định 07/10 | S3 provider thật hoãn, NOT RUN |
| F05 | IN_PROGRESS trong spec; code/test local và điều chỉnh đã có | Cần ghi quyết định nghiệm thu chính thức riêng, không suy từ F06/F07 |
| F06 | DONE local; report/proof/review/confirm/SLA | Chuyển khoản thủ công; chưa có ngân hàng tự nhận giao dịch |
| F07 | **DONE local sau review này**, người dùng đã nghiệm thu | S3/live production không thuộc mốc; backlog sản phẩm không biến thành feature đã triển khai |
| F08/F09 | Roadmap, chưa triển khai các lát cắt được nghiệm thu | Nhân viên, báo cáo và chuẩn bị vận hành |

Nguồn: [F02](../features/F02-partner-onboarding.md), [F03](../features/F03-court-operations.md), [F05](../features/F05-casual-booking.md), [F06](../features/F06-payment-confirmation.md), [F07](../features/F07-fixed-series.md). Mâu thuẫn trạng thái được giữ rõ; lời nghiệm thu F07 không tự đóng mọi feature phụ thuộc hoặc các provider bị hoãn.

## 4. Đối chiếu ALOBO theo nhiệm vụ thực tế

| Nhiệm vụ | ALOBO công bố/hướng dẫn | ShuttleBook / khoảng trống | Hướng chọn |
|---|---|---|---|
| Quản lý lịch ngày/cố định, trạng thái sân | Có lịch đặt và tình trạng sân. [Trang chính thức](https://www.alobo.vn/) | Đã có bảng court × ca 30 phút, casual/fixed và all-or-none | Giữ luồng đơn giản, ưu tiên đọc lịch và đúng dữ liệu |
| Mở/tắt nhận đơn online, chia sẻ chi nhánh | Có cài đặt chi nhánh và lấy link đặt. [Hướng dẫn, cập nhật 19/05/2026](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/) | Có route chi tiết; chưa thấy nút share và công tắc nhận online hoàn tất | P1: tăng khả năng đưa khách hiện có lên web |
| QR theo số tiền đơn | Hướng dẫn QR tự điền tiền/nội dung, yêu cầu BIN ngân hàng. [Hướng dẫn QR](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/) | Hiện QR owner upload, tiền/nội dung hiển thị riêng | QR động là nâng cấp, không đồng nghĩa tự xác nhận thanh toán |
| Nhân viên và trao đổi với khách | Bản 2.10.3 nêu quyền Social Hub và chat. [Changelog, cập nhật 21/09/2026](https://wiki.alobo.vn/article/alobo-2103-cap-nhat-phien-ban-alobo-quan-ly-alobo-dat-lich/) | Hiện owner scope; F08 chưa có | Làm quyền nhân viên trước; chat sau khi thấy nhu cầu |
| Giao dịch và báo cáo | Bản 2.10.3 có giao dịch tập trung, bán lẻ và xuất báo cáo. [Changelog](https://wiki.alobo.vn/article/alobo-2103-cap-nhat-phien-ban-alobo-quan-ly-alobo-dat-lich/) | Có payment/audit nhưng chưa có báo cáo thu PAID được nghiệm thu | Làm báo cáo tiền sân trước POS/công nợ |

**Suy luận sản phẩm:** ưu tiên đầu tiên là owner/nhân viên đọc lịch và đối chiếu nhanh, khách đặt trên điện thoại ít gián đoạn. Không cần xây toàn bộ social/POS/native của ALOBO để kiểm chứng mô hình đặt sân. Nguồn không xác nhận TTL quote, thuật toán chống trùng hoặc SLA realtime của ALOBO; báo cáo không suy các điều đó từ UI.

## 5. Điểm mạnh và findings

### 5.1. Những nền tảng nên giữ

- **Chống trùng ở DB:** [F03CourtOperations.cs:161](../../backend/src/ShuttleBook.Infrastructure/Data/Migrations/20261005115129_F03CourtOperations.cs#L161) có exclusion allocation. Quote giữ cùng bảng; [QuoteReservations.cs:23](../../backend/src/ShuttleBook.Infrastructure/Bookings/QuoteReservations.cs#L23) chỉ tính hold còn hiệu lực, `:46` thay quote giữ hạn cũ. Không chuyển lớp quyết định slot sang cache.
- **Cả kỳ nguyên tử:** [SeriesEndpoints.cs:81](../../backend/src/ShuttleBook.Api/Bookings/SeriesEndpoints.cs#L81) create, `:163` rollback collision; nhóm có một payment, chuyển chính allocation giữ tạm. Có integration DB thật cho race/rollback/migration.
- **Tiền và đối chiếu:** [PaymentCommands.cs:26](../../backend/src/ShuttleBook.Api/Bookings/PaymentCommands.cs#L26) guard/idempotency/khóa nhóm; [F06ExactPaymentAmount.cs:16](../../backend/src/ShuttleBook.Infrastructure/Data/Migrations/20261006223312_F06ExactPaymentAmount.cs#L16) bảo vệ đúng tổng. Không coi screenshot là tự động PAID.
- **Thông báo có retry:** [OutboxDispatch.cs:20](../../backend/src/ShuttleBook.Infrastructure/Onboarding/OutboxDispatch.cs#L20) transaction/SKIP LOCKED, `:80` retry; reported/review không bị expiry quote giải phóng.
- **Địa lý đúng nguồn:** [PublicVenuesEndpoints.cs:72](../../backend/src/ShuttleBook.Api/Discovery/PublicVenuesEndpoints.cs#L72) ST_Distance, `:76` ST_DWithin; map provider phục vụ hiển thị/geocode.
- **Ba role có UI rõ hơn:** Customer grid/TTL/QR, Partner quyết định toàn kỳ, Admin duyệt có lý do. [SeriesReview.tsx:139](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L139), [BookingDecisions.tsx:103](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L103), [AdminApprovals.tsx:194](../../apps/admin-web/src/AdminApprovals.tsx#L194).

### 5.2. Findings kỹ thuật cần xử lý hoặc xác minh

P1: ưu tiên trước pilot/public release do ảnh hưởng phiên, vận hành hoặc độ tin cậy. P2: ưu tiên sau theo mức tác động và tần suất. Đây là mức ưu tiên xử lý, không khẳng định mọi finding là lỗi nghiêm trọng đã tái hiện runtime. **Không phát hiện lỗi mới đã xác nhận runtime trong allocation/payment toàn kỳ F07 ở lượt đọc này.**

| ID / mức / độ chắc chắn | Kịch bản và bằng chứng file:line | Tác động | Sửa/xác minh và testcase đề xuất |
|---|---|---|---|
| T01 — P1, cấu hình source xác nhận; hosted NOT RUN | [ci.yml:9](../../.github/workflows/ci.yml#L9) web Ubuntu, `:18/20` npm build/test; [package.json:27](../../package.json#L27) gọi powershell.exe | Không có gate web release đáng tin nếu runner không chạy script | Script portable/direct npm hoặc runner Windows phù hợp; chạy workflow hosted thật, lưu log/artifact và failure đúng exit code |
| T02 — P1, SOURCE_RISK; cao về interleaving, runtime NOT RUN | [AuthSessionService.cs:122](../../backend/src/ShuttleBook.Api/Identity/AuthSessionService.cs#L122) refresh khóa token/insert replacement; `:186` Customer/Partner logout kiểm token chưa consumed rồi UPDATE family không cùng khóa. [Program.cs:74](../../backend/src/ShuttleBook.Api/Program.cs#L74) chấp nhận family có bất kỳ refresh row chưa revoked | Logout cạnh tranh refresh có khả năng bỏ sót replacement: refresh giữ old row/insert chưa commit, logout UPDATE lấy snapshot rồi đợi old row; refresh commit khiến replacement không thuộc snapshot UPDATE. Trường hợp logout dùng token đã consumed cũng cần kiểm contract | Focused PostGIS barrier trên nhiều connection trước kết luận bug; yêu cầu logout thắng thì mọi access/refresh family bị từ chối. Thiết kế family lock/revocation authoritative và kiểm fresh status/reuse; không sửa session tùy tiện |
| T03 — P1, source-derived; cao, runtime mới NOT RUN | [VenueMap.tsx:53](../../apps/customer-web/src/features/venues/components/VenueMap.tsx#L53) marker dùng window.location.href; [CustomerSession.tsx:18](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L18) memory state bắt đầu null | Chọn marker gây full navigation/mất phiên, khác click từ list qua SPA | Dùng navigate chung, không cần persist token để sửa; browser login → marker → detail → quote giữ session |
| T04 — P1, source-derived; cao, runtime mới NOT RUN | [PartnerBookings.tsx:53](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L53) polling set first page; `:66` mỗi 5s; `:111` append loadMore | Owner tải thêm >20 đơn rồi poll tiếp bị mất các trang lịch sử vừa tải | Giữ page window/cursor hoặc chế độ lịch sử không bị thay toàn list; test >20 đơn, tải thêm, đợi >6s; scope revoke vẫn xóa private cache |
| T05 — P1 trước public, SOURCE_RISK; cao về giới hạn hiện có | [Program.cs:121](../../backend/src/ShuttleBook.Api/Program.cs#L121) quote limiter theo IP; [QuoteReservations.cs:46](../../backend/src/ShuttleBook.Infrastructure/Bookings/QuoteReservations.cs#L46) một hold/customer/court, không cap tổng sân | Một tài khoản có thể giữ nhiều sân/toàn kỳ đến TTL mà không create; NAT cũng có thể ảnh hưởng khách hợp lệ | Đo active holds/customer và quote→create; chốt quota/cooldown toàn tài khoản sau threat/load test. Test nhiều sân, nhiều API, IP phân tán và NAT; không tự đổi policy |
| T06 — P2, source-derived boundary; cao, browser mới NOT RUN | [BookingEndpoints.cs:71](../../backend/src/ShuttleBook.Api/Bookings/BookingEndpoints.cs#L71) casual quote tiền numeric; [BookingPages.tsx:16](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L16) Quote dùng number. Compute `BookingEndpoints.cs:189` cho phép numeric(18,0) | Trên 2^53−1, JSON Number có thể lệch đồng ở bước casual quote; ví dụ 9007199254740993. Không chứng minh ảnh hưởng giá sân thông thường hoặc series đã dùng exact string | Dùng decimal string/exact BigInt xuyên API/UI hoặc chốt business cap backend. Test casual quote >2^53, slot/total, owner editor roundtrip và booking snapshot đúng từng đồng |
| T07 — P2, source-derived; trung bình, runtime NOT RUN | [VenueSearchPage.tsx:84](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L84) loadMore không generation/abort theo query như initial search | Response page cũ về muộn có thể trộn vào query/mode mới | Generation/abort theo search identity; test cursor cũ về sau query mới không append |
| T08 — P2, hypothesis usability; trung bình | [App.tsx:12](../../apps/customer-web/src/App.tsx#L12) route đổi chưa có focus/scroll policy; [VenueDetailPage.tsx:79](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L79) lịch poll 30s/focus | Chuyển bước từ cuối grid khó nhận biết bằng keyboard; B có thể thấy lịch cũ đến lần refresh dù DB chống trùng | Test heading focus/Back/zoom200%/NVDA; hiển thị thời điểm cập nhật. Đo stale/conflict trước thêm push; không gọi polling là realtime tức thì |

T02 cần xác minh trước public release, không dùng suy luận source để tuyên bố session đã bị khai thác. T03/T04 là backlog discovery/list ngoài thay đổi giữ quote; không tự triển khai trong lượt đánh giá. Các findings này không cấp quyền Customer hủy/đổi hoặc Admin xác nhận thanh toán.

### 5.3. Khoảng trống sản phẩm/vận hành

| Khoảng trống | Bằng chứng hiện tại | Quyết định |
|---|---|---|
| Staff chưa vận hành | [PaymentEndpoints.cs:90](../../backend/src/ShuttleBook.Api/Bookings/PaymentEndpoints.cs#L90) chỉ OWNER; [OutboxDispatch.cs:36](../../backend/src/ShuttleBook.Infrastructure/Onboarding/OutboxDispatch.cs#L36) owner recipient | F08 trước bàn giao cho sân có nhân viên |
| S3 chưa có runtime thật | [MediaEndpoints.cs:177](../../backend/src/ShuttleBook.Api/Onboarding/MediaEndpoints.cs#L177) có complete/verify, `:298` mode; user hoãn provider | IAM/private/CORS/expiry/restore phải kiểm thật trước sử dụng AWS; adapter không đủ |
| Customer/Partner F5 phải login lại | CustomerSession.tsx:18; [partner main.tsx:38](../../apps/partner-web/src/main.tsx#L38); contract F01.2 memory-only | Enhancement cần chốt policy riêng, không lỗi trái nghiệm thu. Không đưa token vào localStorage cho nhanh |
| Khôi phục/giám sát/tải | Thiết kế docs03/04 có, chưa có drill/load/alert evidence | Chốt tải mục tiêu/RPO/RTO rồi thực chạy staging |
| Báo cáo và hiệu quả sử dụng | Payment/audit có; F09 còn roadmap, chưa có pilot metric | Làm report PAID đúng scope và không cộng trùng series; đo funnel trước thêm feature |
| Trợ giúp vận hành | Có reason/review/proof nhưng chưa có quy trình hỗ trợ đã nghiệm thu | Hướng dẫn chuyển sai/quên báo, lịch xử lý và người chịu trách nhiệm; không tự tạo refund hoặc quyền tài chính mới |

## 6. Đánh giá theo chiều và theo role

Thang 0–4: 0 chưa có; 1 thiết kế; 2 có code; 3 runtime local kiểm chứng; 4 vận hành thực đã kiểm chứng trong môi trường mục tiêu. Người dùng nghiệm thu local không tự nâng thành 4. Không cộng thành phần trăm product.

| Chiều | Điểm / tin cậy | Nhận xét |
|---|---|---|
| Booking/payment/quote hold | 3/4, cao cho local | Race/constraint/snapshot/full-series có evidence; F07 local được nghiệm thu |
| Onboarding/search/giá | 3/4, trung bình | Luồng local đầy đủ, provider/deployment cần gate riêng |
| Staff/permission vận hành | 1/4, cao | Mới roadmap/schema thiết kế; endpoint owner không phải staff feature |
| UX ba role | 3/4, trung bình | Style và các task core đã test/nghiệm thu local; T03/T04 cần sửa, usability thật chưa đo |
| Release/phục hồi | 1/4, trung bình | Có thiết kế/workflow, CI compatibility và drill chưa đạt bằng chứng |
| Giá trị thương mại | Chưa chấm | Không có pilot, retention hoặc mức sẵn sàng trả tiền |

| Role | Việc đã làm được | Cản trở thực dụng | Chỉ số cần đo trong pilot |
|---|---|---|---|
| Customer | Tìm/chọn sân, đọc giá, quote giữ tạm, casual/fixed, QR/report và lịch sử | Relogin khi reload/marker, hai deadline cần giải thích, lịch chưa cập nhật tức thì | view→quote→create→report, relogin, bỏ dở và time-to-quote trên mobile |
| Owner/Staff | Owner cấu hình/duyệt, list nhóm/đối chiếu/notification | Chưa staff, tải thêm bị poll reset, thiếu report tiền và phiên thuận tiện | Thời gian xử lý report, đơn quá SLA, số thao tác/ca làm và sai lệch đối soát |
| Admin | Duyệt/revision/yêu cầu sửa, idle restore, cảnh báo SLA | Chưa dashboard sức khỏe Worker/outbox/backup đã vận hành, thiếu quy trình hỗ trợ | Thời gian duyệt, backlog, retry/outbox age và thời gian phục hồi |

## 7. Roadmap theo lát cắt có thể nghiệm thu

S/M/L là ước lượng tương đối, không cam kết ngày: S = một sửa tập trung; M = một luồng liên module; L = nhiều module/provider/diễn tập. Giả định team hiện tại và kiến trúc giữ nguyên; estimate lại sau thiết kế. Policy mới/metrics dưới đây là đề xuất, chưa tự được duyệt.

### P0 cho gate pilot — củng cố trước và cùng F08

**R0 — sửa cản trở và xác minh auth (S–M).** Customer/owner cần thao tác không bị mất phiên hoặc lịch sử. Scope T01–T04/T07 và verify T02; không redesign hay đổi payment policy. Data/API: giữ route/DTO, cải thiện navigation/cursor; nếu auth race xác nhận thì thiết kế shared family lock/revocation trước sửa. Gate: browser marker/pagination/search races, focused PostGIS refresh/logout, hosted CI xanh và quyền/private-cache hồi quy. Metric: relogin do navigation, reset trang lịch sử, workflow failures; chưa đặt phần trăm mục tiêu.

**R1 — F08 mời nhân viên và quyền cơ sở (L).** Actor owner/Admin mời, staff nhận; giá trị là giao ca thực thay owner làm mọi việc. Dependencies F01/F02/F03/F06/F07. Data: invitation token hash, expiry/used/revoked, membership/permission scope business/venue và audit; không cho staff tự join business. API đề xuất nhóm invite/list/revoke/accept, GET quyền hiệu lực; error 401/403, 404 ngoài scope, expired/used/revoked conflict, version/idempotency nơi cần. Notification chỉ gửi người hiện đủ quyền.

Acceptance: lời mời một lần/hết hạn/thu hồi đúng; quyền xem/confirm/config/report tách bạch, scope không vượt venue; queued request sau revoke bị chặn; owner giữ quyền gốc, Admin không tự có payment.confirm. Test PostGIS concurrency accept/revoke, không nâng quyền/cross-scope, outbox đúng recipient, UI desktop/mobile. Gate F08: thiết kế policy quyền được duyệt và test/local manual đạt. Metrics: tỷ lệ accept, thao tác bị từ chối sai và report có người phụ trách. Chưa triển khai permissions chỉ để giống ALOBO.

**R2 — F09.1 media, CI/release và độ bền (L).** Actor owner/customer/Admin cần ảnh/QR/proof tồn tại và đúng quyền; team cần release có thể phục hồi. Dependencies S3 thử nghiệm được phép và F08 cho staff proof scope. Data/API: dùng object key và checksum hiện có, bucket private/IAM, presign/complete/scoped read; errors media unavailable/expired/mismatch không rò key/token. Không public hóa proof để giảm bước.

Acceptance/test: S3 PUT/CORS/HEAD/bytes/checksum thật, hết hạn/đúng-sai role, snapshot QR cũ còn, lifecycle không xóa object đang tham chiếu; CI runner tương thích/build/test thật; migration dữ liệu cũ và release artifact. Backup DB + media, restore sang môi trường riêng, ghi RPO/RTO; failure Worker/DB/outbox có alert và người xử lý. Gate: staging restore/release rehearsal, quyền và media đạt, không tạo bucket trả phí khi chưa được phép. Metrics: tuổi outbox, expiry lag, upload failures, thời gian restore đo được, đối soát allocation/payment.

**R3 — quote chống lạm dụng và tải mục tiêu (M).** Actor khách hợp lệ không bị khóa nhiều sân vô ích. Dependencies quota/abuse policy được chốt. Data/API: instrumentation không PII, limiter theo account/global nếu được duyệt, limit error + Retry-After; vẫn PostgreSQL/exclusion authoritative. Test NAT, nhiều IP/accounts/courts, nhiều API/Worker, expired reads và pool/backpressure. Gate: tải staging theo số sân/khách mục tiêu; chốt p95/error budget sau baseline. Metrics: active hold/account, quote→create, abandon, conflict, latency; không gia hạn quote bằng polling.

### P1 — hiệu quả vận hành và chuyển đổi

**R4 — F09.2 báo cáo thu tiền/đối soát (M).** Owner/staff được quyền report cần biết tiền đã nhận. Dependencies payment PAID/audit/F08; không thêm POS/công nợ. Data/API: query hoặc projection theo venue/timezone, ngày nhận và ngày chơi riêng; export có scope/giới hạn, amount exact; series payment chỉ tính một lần. Error outside-scope404, invalid date400; audit export. Acceptance/test: dữ liệu mẫu casual/fixed/rejected/pending, tổng report = payment PAID không cộng anchor+occurrences, export quyền đúng. Gate chủ sân đối chiếu mẫu đạt. Metric: sai lệch đối soát phải bằng0 trên bộ mẫu; chưa gọi đó là chứng minh mọi giao dịch production.

**R5 — phiên Customer/Partner và luồng quay lại (M).** Khách quay từ app ngân hàng, owner tải lại portal. Cần chốt idle/absolute TTL/thiết bị/two-tabs, không tự áp Admin30 phút sang role khác. Data/API: secure HttpOnly refresh cookie hoặc session server theo thiết kế được duyệt, CSRF/origin/SameSite/rotation/revoke, restore/returnTo nội bộ. Acceptance/test: F5/payment return/reuse/logout/suspend/multi-tab; quote hết hạn phải lấy mới, idempotency intent còn hợp lệ không tạo đơn mới. Gate test auth race và manual mới. Metric relogin và dropoff trước report; không token localStorage.

**R6 — chia sẻ cơ sở, tạm ngừng nhận online và hỗ trợ (M).** Owner đưa khách quen lên web và ngừng nhận đơn mới khi cần. Data/API đề xuất venue onlineBookingEnabled theo version/audit, link công khai; backend chặn quote/create mới khi off, vẫn giữ payment/report/confirm đơn đã tạo theo policy. Hỗ trợ dùng case scoped/reason/proof private trước xác nhận, không tự refund. Test off vs create race, quote đã giữ xử lý theo policy chốt, scope/admin suspension/old bookings. Gate owner/Admin duyệt policy; metric link→booking, yêu cầu hỗ trợ và time-to-resolution.

### P2 — mở rộng sau khi pilot chỉ ra nhu cầu

| Lát cắt / effort | Actor và giá trị | Contract/dependencies | Acceptance/test/metric |
|---|---|---|---|
| QR động (M) | Khách giảm nhập sai tiền/nội dung | BIN/chuẩn QR đã xác minh, owner approval, snapshot/fallback | Đúng exact amount/content trên các app ngân hàng thử; không tự PAID; đo lỗi nhập/chuyển sai |
| Favorite/review (M) | Khách tìm lại sân và đánh giá | Auth/scope, một review CONFIRMED đã qua endsAt; không thêm completed | Duplicate/spam/cross-customer denied; đo quay lại và review hợp lệ |
| Dịch vụ/POS/ưu đãi/hội viên (L) | Owner bán thêm/giữ khách | Giá/ledger/tồn kho/entitlement/discount policy riêng cần duyệt | Không sửa booking snapshot cũ, tiền/scope/race đúng; chọn ưu tiên theo nhu cầu sân |
| Chat/social/ngân hàng tự động/native (L) | Tác vụ được web hiện tại đáp ứng chưa tốt | Provider/webhook/security/retention, ngân hàng idempotent/audit; policy xác nhận phải chốt | Không bẻ owner-confirm, không thêm check-in/no-show; chỉ làm khi dữ liệu pilot xác nhận giá trị |

### Gate pilot và public product

- Pilot đề xuất 1–3 cơ sở tự nguyện, có người hỗ trợ và tải mục tiêu. P0 tương ứng đạt; giao dịch thật/deploy là quyết định riêng cần phép, không được thực hiện chỉ vì báo cáo này.
- Bắt đầu sandbox; đối soát PAID/allocations/reported hằng ngày, diễn tập DB/Worker lỗi, backup restore và migration/release. RPO/RTO được đo, không hứa tùy ý.
- Đo funnel view→select→quote→create→report→confirm, expiry/abandon/failures, thời gian owner phản hồi; không ghi token, biên lai, tài khoản ngân hàng hoặc vị trí khách vào analytics.
- Chốt thời gian/cỡ mẫu pilot trước khi kết luận; go/no-go dựa incidents, đối soát, task completion và phản hồi. Không dùng một screenshot để chứng minh usability.
- Public product cần load/security/recovery/CI evidence, media bền vững/private, quyền staff, terms/quy trình hỗ trợ và người chịu trách nhiệm alert; rollback/runbook có thể thực thi.

## 8. Quyết định giữ, sửa, hoãn và việc cần chốt

| Giữ | Sửa/xác minh kế tiếp | Hoãn |
|---|---|---|
| Modular monolith, ba React app, PostGIS/exclusion, snapshot, outbox, ca30 phút, quote TTL và fixed toàn kỳ | T01–T04/T07, quyền staff, media/release/recovery, phiên thuận tiện theo policy riêng, report tiền đúng | Microservices/Redis khóa, native, social/POS/hội viên và ngân hàng tự động khi chưa có nhu cầu/provider/policy |

Những quyết định business cần chốt khi bắt đầu feature liên quan, không chặn việc bàn giao báo cáo:

1. Staff được xem/sửa/confirm/report ở đâu; ai mời/thu hồi; có cần owner duyệt thao tác nào? Admin vẫn không mặc nhiên có quyền xác nhận tiền.
2. Customer/Partner giữ phiên bao lâu, có nhớ thiết bị/two-tabs hay không? Logout phải thu hồi family thế nào khi refresh đồng thời?
3. Giới hạn giữ quote trên nhiều sân, tải/cỡ cơ sở mục tiêu, RPO/RTO và người trực hỗ trợ pilot là gì?
4. S3/staging được phép dùng khi nào; lịch và người xử lý chuyển sai/quên báo, retention ảnh/audit và điều khoản vận hành ra sao?

Bất biến cho mọi bước: business→venue→court; giá ca30 phút và snapshot; fixed all-or-none; PostgreSQL chuẩn; reported/review không tự release vì owner chậm; outbox retry/dedup; Customer không hủy/đổi; CONFIRMED kết thúc thành công, không check-in/check-out/completed/no-show. Feature mới đi DB→API→UI→test→review. Báo cáo đã thực hiện prompt; không tự code roadmap, commit/push/merge/deploy hay tạo tài nguyên trả phí.
