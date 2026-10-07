# Tiến độ ShuttleBook

## F06 — nghiệm thu và điều chỉnh hoàn tất — 2026-10-07 (DONE)

- Người dùng xác nhận đã nghiệm thu F06 đạt, cho phép chuyển DONE sau xử lý phản hồi này. Sửa booking vãng lai: đạt minimum sân rồi được thêm từng ca 30 phút, không yêu cầu tổng là bội bookingBlockMinutes (minimum120 nhận 4/5/6/7... ca); vẫn liên tiếp, đúng sân, giờ mở, giá, chống trùng, giữ snapshot và policy F07 riêng.
- Giản lược payment UI: bỏ mã giao dịch customer/owner; API nhận mã tùy chọn để giữ client/historical evidence và idempotency cũ, evidence mới nullable reference bằng migration tăng dần. Ảnh chụp màn hình/ghi chú vẫn tùy chọn theo “có thể cung cấp”; giữ owner đối chiếu đúng tiền, review/reject, private READY proof, SLA/outbox.
- Kiểm tra log OperationCanceledException trong xác thực: request do client hủy phải dừng và không ghi lỗi JWT; chỉ xử lý khi RequestAborted đã hủy, không nuốt lỗi DB/timeout thực và không bỏ kiểm tra quyền/session.
- Acceptance trước code: UI+API quote/create minimum120 chấp nhận 4/5/6/7 ca, từ chối <minimum/non-grid/slot unavailable; report/supplement/confirm không reference thành công với/không ảnh, validation và replay cũ giữ nguyên; dữ liệu evidence cũ/migration/constraints bảo toàn; canceled auth không thành công và không log JWT error, request thật vẫn kiểm active session/Admin idle.
- **PASS build:** solution 0 warning/error; typecheck và build cả ba web. Migration tăng dần `20261007114512_F06OptionalBankReference` bảo toàn lịch sử/reference/hash/snapshot cũ, không tạo mã ngân hàng giả; Down chặn dữ liệu không tương thích thay vì ghi đè null. DB development chưa migrate.
- **PASS PostgreSQL/PostGIS 35/35**, 10m36: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build --filter "FullyQualifiedName~CasualBookingTests|FullyQualifiedName~PaymentConfirmationTests|FullyQualifiedName~IdentityFlowTests"` trên DB tạm: Casual7 +Payment27 +Identity1. Gồm 4/5/6/7ca, minimum/grid/price/snapshot/overlap/replay, report có/không ảnh không reference, supplement sau deadline/normalized replay, exact amount, migration old F06 → new/repeat giữ lịch sử/hash/client cũ, paid CHECK/FKs/READY/privateproof, concurrency/scope/SLA/outbox.
- **PASS auth cancellation 7/7**, 166ms: API suite lượt đầu84 PASS/1 FAIL/85 vì test kỳ vọng handler trả failure khi non-client OCE, thực tế handler log rồi ném exception (đúng failclosed). Chỉ sửa fixture assertion sang ThrowsAsync +log; rebuild API.Tests PASS0warning/error, `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --no-restore --filter FullyQualifiedName~JwtSessionCancellationTests`7/7PASS. Không gọi lượt đầu là full85PASS. Các cancellation request do client hủy trả NoResult và không JWTerror; nonclient lỗi vẫn propagate/log.
- **PASS browser 62/62**,25,5s: `npm.cmd run test:web -- tests/web/f04-discovery.spec.ts tests/web/f05-booking.spec.ts tests/web/f06-customer.spec.ts tests/web/f06-partner.spec.ts tests/web/f06-admin.spec.ts --workers=2`. Lượt đầu57PASS/5FAIL do implicit label textarea bao gồm draft text trong locator và testnearby đọc URL trước request; tách label htmlFor/useId, test chờ quan sát actualnearbyrequest; rebuild rồi toàn62PASS. Screenshot-only/report{}/no-reference confirm/18digit exact/retry/412/F5/privatecache/Adminidle desktop/mobile đều đạt.
- **PASS live8/8**,47,9s: `npm.cmd run test:identity-live -- -ApiPort 5081`; browser→API/Worker/PostGIS/Mailpit thực tế, ảnh localprivate-only report không mã → review → supplement không mã giữ firstReportedAt/SLA → ownerconfirm không mã400.000đ → PAID/CONFIRMED/notification/F5. DB test đã dọn đúng tên, web build normal phục hồi; MapTiler provider stubbed/S3 live vẫnNOT RUN theo phạm vi hoãn.
- Review độc lập casual/auth bởi agent UI: không vấn đề nghiêm trọng; root kiểm tra optional reference/migration/hash/constraints/UI, sửa các lỗi fixture/label và chạy lại ca bị ảnh hưởng. `git diff --check`PASS (chỉ line-endingwarning). Xác nhận nghiệm thu của người dùng +gate bản sửa đạt → F06DONE; không tuyên bố người dùng đã thao tác tay từng ca bản sửa. Hướng dẫn tay cập nhật `docs/testing/F06-manual-acceptance.md`; chạy `npm.cmd run db:migrate` và restartAPI/Worker/web để dùng bản mới. Không tự commit/push/merge/deploy hoặc chuyểnDONE F05.

## F06 — hoàn tất triển khai/kiểm thử local, chờ nghiệm thu tay — 2026-10-07 (IN_PROGRESS)

- Người dùng đồng ý cả ba đề xuất: reference bắt buộc/proof optional; xác nhận đúng expectedAmount, lỗi thiếu/thừa không mutation; SLA 30 phút từ báo chuyển đầu tiên, owner + Admin cảnh báo một lần kể cả NEEDS_REVIEW, bổ sung không reset và không tự giải phóng sân.
- Tiếp tục từ nền tảng đã PASS: triển khai report/supplement, confirm/review/final reject theo transaction/version/idempotency, SLA Worker và các form customer/partner; QA bổ sung và chạy integration trên DB tạm PostgreSQL/PostGIS thật. Chưa có bằng chứng mutation ở mốc ghi này, không coi policy đã duyệt là test PASS.

### Hoàn tất luồng và đang chạy gate cuối

- Đã triển khai ba command report/supplement, confirm và quyết định review/final reject: lock/recheck quyền DB, If-Match, idempotency replay trước stale/deadline/state, evidence/decision/audit/outbox cùng transaction, final reject release allocation. Worker SLA 30 phút dedup bền vững, không đổi booking version/state hoặc release. Migration F06ExactPaymentAmount enforce PAID đúng expectedAmount. UI customer/partner có form, upload private local, history/notifications, retry cùng intent và explicit reload khi stale; DTO exact strings + BigInt giữ nguyên VND tới 18 chữ số.
- Review độc lập đã sửa thêm: clear private detail/proof khi quyền bị thu hồi, chặn GET cũ ghi đè POST mới, và retry report cùng intent sau mất response dù deadline cũ đã qua. SLA UI dựa đồng hồ, không khẳng định alert đã gửi khi Worker chưa dispatch. Không còn lỗi nghiêm trọng được tìm thấy trong source review; runtime gates còn ghi riêng.
- **PASS** FULL solution latest build 0 warning/error; API `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --no-restore`: **78/78**, 1m14s; typecheck toàn workspace PASS.
- **PASS live** `npm.cmd run test:identity-live -- -ApiPort 5081`: **8/8**, 55,5s trên desktop/mobile, browser → API/PostGIS/Mailpit/Worker thật. F02→F05 regression và F06 private proof/report → owner notification/deep link/private view → NEEDS_REVIEW → supplement giữ firstReportedAt/SLA → PAID/CONFIRMED → customer notification/F5 cùng đơn. Lượt trước 6 PASS/2 FAIL vì test chọn wrapped select bằng getByLabel exact; đổi getByRole combobox rồi chạy lại toàn live PASS. DB tạm đã dọn, build web normal đã restore; MapTiler provider stubbed, S3 thật không chạy.
- Full PostGIS và browser regression đang chạy ở mốc này; chưa tính pending thành PASS. DB từng từ chối TCP54329 lúc Docker tắt, đã dừng lượt bị chặn và người dùng bật Docker để chạy lại. Không migrate development/commit/push/deploy.
- **PASS browser hồi quy bản cuối:** `npm.cmd run test:web -- --workers=2`: **102 PASS / 8 SKIP** live có chủ đích, 1,1 phút. F06 có customer22/partner18/Admin4 desktop-mobile; bao gồm clear private cache khi revoke, GET cũ, retry lost response sau deadline và tiền vượt safe integer. F05 assertion cũ “không có Đã chuyển khoản” đã sửa đúng F06, vẫn kiểm không Hủy/Đổi lịch. Đã xem ảnh partner detail/reject và customer confirmed desktop/mobile; không lỗi layout nghiêm trọng. Full PostGIS còn pending tại mốc ghi này.

### Kết quả gate cuối và bàn giao

- **Full PostgreSQL/PostGIS thật:** `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build`: **53 PASS / 2 FAIL / 0 SKIP**, 55 ca, 16m57s. Hai FAIL là fixture F06 đăng nhập Admin qua endpoint Customer; các ca production payment/constraint/concurrency/migration còn lại đạt. Sửa fixture dùng `/admin-auth/login`, duy trì đúng idle policy bằng request thật ở phút20 trước đọc cảnh báo phút31; không sửa authentication production hoặc cập nhật thủ công session DB.
- **PASS kiểm tra lại 2/2**, 37s: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build --filter "FullyQualifiedName~PaymentConfirmationTests.Scope_headers|FullyQualifiedName~PaymentConfirmationTests.Thirty_minute_SLA"`. Test build bản sửa PASS 0 warning/error. Không gọi đây là một lượt full55 PASS; 24 ca F06 gồm 22 đã đạt trong lượt full và 2 đã đạt sau sửa fixture. Không còn lỗi nghiêm trọng đã biết hoặc gate F06 chưa xử lý.
- Phạm vi đạt gồm INITIAL deadline/expiry/race, replay/new intent, review/supplement sau deadline, confirm đúng tiền/PAID metadata/exact18digits, final reject+rebook, owner scope/revoke locks, private READY proof/FKs, outbox commit rollback/multiple dispatchers/retry và SLA30/dedup/no-release. Mapping T01–T20 và bằng chứng ở `docs/testing/F06-test-cases.md`; review độc lập và ảnh UI đã hoàn tất.
- **Bàn giao nghiệm thu tay:** `docs/testing/F06-manual-acceptance.md`. Để dùng local, người dùng chủ động `npm.cmd run db:migrate` rồi restart API + Worker và các portal. DB development chưa được migrate tự động. Nghiệm thu tay của người dùng và S3 provider thật **NOT RUN**; S3 tiếp tục hoãn theo yêu cầu. F06 giữ IN_PROGRESS chờ nghiệm thu theo luồng F05/F04 hiện có; chưa tự đổi DONE F05 hoặc commit/push/deploy.

## F06 — nền tảng và UI đọc, chờ chốt policy — 2026-10-07 (IN_PROGRESS)

- Người dùng yêu cầu thực thi prompt. Đã lập spec/testcase trước code ở `docs/features/F06-payment-confirmation.md`, `docs/testing/F06-test-cases.md`; đã hỏi gộp evidence/amount/SLA nhưng chưa có trả lời ở mốc này. Không coi các đề xuất của prompt là đã duyệt. Quote/hold/horizon và session F01–F05 giữ nguyên.
- Đã triển khai hai migration tăng dần F06PaymentConfirmation/F06OutboxDiagnostics: evidence/quyết định/metadata, scoped FK + READY proof trigger, ActorUserId idempotency bảo toàn key/hash F05, PAID metadata CHECK và outbox failure diagnostics. Operator list/detail/payment scope, DTO history, proof presign/complete/view local, notifications paging/unread/read/link và dispatcher dùng chung Worker/PostGIS tests. Booking/payment DTO đọc cùng query; detail transaction snapshot; chưa triển khai mutation report/confirm/review/reject hoặc SLA scan.
- UI customer mapping 6 trạng thái/history/private proof/notifications/F5 returnTo; partner Đơn đặt sân scoped list/filter/count/detail/history, deep link chọn đúng business, notifications visible/focus polling và scope abort/reset; Admin có notification alert display/read/retry, không quyền xác nhận thanh toán. Các form xử lý tiền còn chờ policy, không có happy flow thanh toán F06 thực tế.
- **PASS** build solution 0 warning/error, typecheck/build web, API **78/78**; toàn browser production preview IPv4 **76 PASS / 8 SKIP** live có chủ đích trong 36,5s. Lượt trước **74 PASS / 2 FAIL / 8 SKIP** do test ép thứ tự hai startup read độc lập; sửa assertion vẫn kiểm mỗi request đúng một lần và chạy lại PASS. UI riêng customer6/partner8/Admin4 + workspace/regression đều trong suite cuối; đã xem ảnh customer/partner desktop/mobile.
- **PASS PostgreSQL/PostGIS thật:** 6/6 neutral F06 (1m19s) và 16/16 regression F02/F03/F05 + proof/scope/concurrent complete (3m56s), không gộp thành full suite F06. Commit failure rollback notification/processed marker, 8 dispatcher dedup, retry/no-recipient/unknown event/backoff diagnostics, upgrade F05/replay key cũ và 8 complete đồng thời chỉ audit một lần đều có runtime evidence. DB tạm được dọn, không migrate development. Sáu Fact F06 future handlers còn active nhưng **NOT RUN** ở filter đã chạy; full DB suite chưa chạy và sẽ chưa đạt khi handler thiếu.
- Review độc lập customer agent/QA phát hiện proof presign lộ ObjectKey, READY audit trùng khi complete đồng thời, notification read revoke race và owner notifications thiếu active/type guard; root đã sửa. Scope/media retest trong regression PASS; API và browser FAIL đã được sửa/ghi rõ. Fixture F05 cần migrate latest trước seed model mới, F03 seed purpose sai PAYMENT_QR đổi đúng QR, hai Worker ctor test cập nhật DI.
- **PASS** outbox retest sau bổ sung masked customer/report time/truncate body và owner notification DB active/type guard: **2/2** PostGIS, 30s; build solution cuối PASS 0 warning/error. `git diff --check` chỉ cảnh báo line ending, không lỗi whitespace.
- **Còn mở:** chốt reference/proof policy, amount mismatch và SLA/recipient; tiếp tục handlers/state transactions, report/supplement và owner action forms, SLA Worker, live integrated flow và manual acceptance đầy đủ. S3 live/MapTiler provider thật không được chứng minh bằng UI mocks; S3 tiếp tục hoãn. Chưa DONE F06, đổi DONE F05, commit/push hoặc deploy.

## Chuẩn bị F06 — prompt 2026-10-07

- Đã đối chiếu UC-07/08/11/17/18, state machine/API/data, code booking/expiry/idempotency/outbox/media F05 và UI partner hiện tại. Prompt triển khai ở `docs/prompts/F06-payment-confirmation.md`: customer báo chuyển và bổ sung, owner đọc/xác nhận/review/từ chối đúng scope, evidence private local, notification outbox/in-app, SLA, migration/concurrency và hướng dẫn nghiệm thu.
- Prompt yêu cầu đồng bộ transition booking NEEDS_REVIEW → AWAITING_OWNER_CONFIRMATION, sequence reject qua Worker/outbox và thay client `proofObjectKey` bằng upload ID READY/scoped. Nêu rõ dispatcher hiện mặc định TargetUserId null gửi Admin, guard upload hiện chỉ operator và idempotency còn tên CustomerId; F06 cần mở rộng có kiểm soát, không giả định các khả năng này đã tồn tại.
- Chưa chốt chính sách evidence bắt buộc, số tiền đối chiếu và SLA/recipient; prompt ghi đề xuất để xác nhận trước code, không xem là quyết định đã duyệt. Quote TTL/horizon/hold F05 giữ chính sách đã chốt. S3 provider thật tiếp tục hoãn theo yêu cầu người dùng; không triển khai F07/F08 hoặc thay auth persistence.
- **Mốc tài liệu:** `git diff --check` PASS (chỉ cảnh báo line ending các file có sẵn); chưa triển khai/test runtime F06, chưa đổi trạng thái DONE của feature, migrate DB development hoặc commit/push. Bước tiếp theo là thực thi prompt khi người dùng yêu cầu, lập spec/testcase và chốt các policy còn mở.

## Cải thiện UI partner trước F06 — 2026-10-07

- Đọc UI UX Pro Max từ repository người dùng chỉ định. Chạy design-system generator với query thể thao rồi thu hẹp sang SaaS operations; kết quả vẫn thiên về marketing nên ghi rõ quyết định dashboard riêng trong `docs/prompts/partner-ui-refresh.md`. Prompt có scope, acceptance, dữ liệu/API/error contract và testcase trước code; không cài skill toàn cục.
- Partner có sidebar/menu mobile, hash navigation/Back/Forward/deep link, Tổng quan với counters/checklist từ API, nhóm Hồ sơ/Cơ sở/Sân/Lịch & giá/Ảnh/Thanh toán & QR/Thông báo và màn auth mới. CSS tokens teal/nền sáng, SVG, keyboard/focus/skip link/reduced-motion. Giữ form khi đổi mục, reset lựa chọn và input khi đổi business; MapTiler label ID duy nhất và resize khi trang hiện lại. Giữ API/DTO/If-Match/refresh/token memory và các guard duyệt hiện có.
- **PASS:** typecheck/build; toàn browser suite **58 PASS / 8 SKIP** live có chủ đích, gồm 10 ca UI mới desktop/mobile, reflow 375/768/1024/1440, business scope/input, pending lock, notifications error/retry/read và F01–F05 regression. Đã xem ảnh Tổng quan/Vận hành/Auth desktop/mobile; ảnh local trong `artifacts/partner-ui/` (ignored). Lượt đầu phát hiện đọc startup lặp do StrictMode và helper chưa mở menu mobile trước logout; đã sửa. Fixture chọn business bằng label từng bị strict-match với nav label; đã đổi sang combobox exact, chạy lại pass.
- **PASS** `npm.cmd run test:identity-live -- -ApiPort 5081`: **8/8** trên PostgreSQL/PostGIS/Mailpit/Worker thật, gồm partner → Admin trả sửa/duyệt → F03 policy block/min/hold (DTO và If-Match), giá/preview/bảo trì → F04 availability → F05 booking/QR snapshot và outbox. Lượt cuối sau bổ sung kiểm tra policy pass 8/8 trong 37,4s. DB tạm đã drop sau xác thực tên/host, web build đã phục hồi về cấu hình local. MapTiler trong live được stub; media dùng local private adapter.
- Live ban đầu trên 5080 **BLOCKED** vì API người dùng đang chạy. Hai lượt 5081 **FAIL** (2 PASS/6 FAIL) do Chromium mở frontend dev IPv6 của người dùng và gọi sai origin API, không phải lỗi backend/luồng onboarding. Đã sửa preview/readiness sang IPv4 và resolver Chromium giữ origin localhost, thêm guard origin trong live tests và tham số `-ApiPort`; lượt sau PASS. Không dừng dev server/API của người dùng. Browser suite đầu có thể đã chạy vào dev IPv6; toàn suite đã chạy lại trên production preview IPv4 cho bản cuối: **58 PASS / 8 SKIP** trong 28,6s (gồm feedback khi gửi và touch target 44px). Build/typecheck/diff cuối PASS; artifact cả ba app không còn URL test API 5081.
- Checklist tay ở `docs/testing/partner-ui-refresh.md`. Review tuần tự bởi implementer, chưa có reviewer độc lập. Không thay API/schema/DB development, commit/push, trạng thái DONE F05 hoặc triển khai F06. S3/MapTiler provider thật không phải bằng chứng của suite UI mock.

## Sửa bảng lịch F04/F05 theo phản hồi giao diện — 2026-10-07

- Đã tái hiện trường hợp lịch05:00–22:00 với34ca bị ép còn18,23px/cột desktop và chồng giờ/giá. Sửa bằng colgroup/chiều rộng theo số ca, ô30phút120px desktop/112px mobile; cuộn ngang trong bảng, tên sân sticky, mốc giờ ở biên, giá ở giữa, bỏ nhãn30phút/Còn trống lặp trong từng ô. Giữ đủ hàng sân, toggle/min/block/tổng giá/handoff F05.
- **PASS:** build/typecheck/diff; browser F04+F05 **14/14** desktop/mobile, gồm lịch cả ngày, cuộn cuối22:00, 3/7 sân, selection và login/quote/QR/F5. Đã review ảnh UI. Test hồi quy quote từng fail vì fixture chuyển trạng thái theo request bị abort; đã đổi fixture theo thao tác lấy quote mới và chạy lại pass.
- Tài liệu/testcase bổ sung F04-T18. Không thay API/schema, không migrate DB, commit/push hoặc triển khai F06. F05 tiếp tục chờ người dùng nghiệm thu tay; F04 DONE là mốc đã nghiệm thu trước bản điều chỉnh này. F06 theo roadmap là báo chuyển → notification outbox tới owner → đối chiếu/xác nhận/cần bổ sung/từ chối; CONFIRMED kết thúc luồng.

## F05 — quote và booking vãng lai — 2026-10-07 (IN_PROGRESS)

- Người dùng đã chốt báo giá 2 phút, đặt trước tối đa 60 ngày theo timezone cơ sở, giữ chỗ theo `holdMinutes` từng sân và yêu cầu xác nhận báo giá mới khi giá/cấu hình đổi. Đặc tả và testcase được lập trước code trong `docs/features/F05-casual-booking.md`, `docs/testing/F05-test-cases.md`.
- Đã triển khai migration quote/booking/payment/idempotency, API quote/create/list/detail/QR private, giá/QR/policy snapshot, allocation/exclusion chống trùng và Worker expiry/outbox. Customer nối lịch F04 → đăng nhập/đăng ký → review → tạo đơn → QR/Đơn của tôi; session dùng chung trong memory, F5 đăng nhập lại rồi đọc đúng đơn, không tự tạo đơn mới.
- **Hoàn tất triển khai/kiểm thử local, chờ nghiệm thu tay:** API 78/78 PASS; full PostGIS 30/30 PASS ở mốc đầu, F05 mở rộng 5/5 PASS (DST/offset lẻ/FK/overflow), expiry assertion CONFIRMED chạy lại 1/1 PASS; web 46 PASS/8 SKIP live có chủ đích; live 8/8 PASS qua API/PostGIS/Mailpit/Worker thật, gồm F02→F05 và notification BOOKING_CREATED. Build backend 0 warning/error, typecheck/build web và diff check PASS. Đã xem ảnh UI desktop/mobile. Review tuần tự, chưa có reviewer độc lập; chưa có lỗi nghiêm trọng còn mở. Không cộng các lượt DB khác nhau thành một full suite mới.
- Đã sửa lỗi handoff listener F04 xóa courtId khỏi URL review và lỗi startup protected redirect/StrictMode. Regression giữ đúng court/date/time, retry cùng key, F5 cùng đơn và QR cũ sau revision đã PASS. Lượt browser 10 workers từng crash; chạy lại 2 workers PASS. Chi tiết lệnh/FAIL đã xử lý/giới hạn bằng chứng ở testcase F05.
- Hướng dẫn nghiệm thu tay: `docs/testing/F05-manual-acceptance.md`. S3 live tiếp tục NOT RUN theo yêu cầu hoãn; báo chuyển/xác nhận thuộc F06, series thuộc F07. Chưa migrate DB phát triển, commit/push/merge/deploy.

## F04 — public discovery, lịch sân và ảnh private local — 2026-10-07 (DONE)

- **Quyết định nghiệm thu 2026-10-07:** Người dùng xác nhận F04 đạt và yêu cầu chuyển `DONE`. Phạm vi DONE là tìm/list/nearby/detail/lịch, giá tham khảo và ảnh private local đã kiểm thử; S3 live được người dùng hoãn sang mốc tích hợp media riêng. T10/phần S3 T09 giữ **NOT RUN**, không coi F04 DONE là S3 PASS. Kết nối đăng nhập khách với bước đặt sân thuộc F05. Chưa commit/push theo yêu cầu lượt này.

### Chuẩn bị F05 — prompt 2026-10-07

- Đã đối chiếu roadmap và code F01–F04, ghi prompt triển khai tại `docs/prompts/F05-casual-booking.md`: quote có hạn, booking vãng lai theo policy F03, auth handoff, giá/QR snapshot, allocation + idempotency + expiry Worker, DB/API/UI/test trên PostGIS thật. Đây mới là prompt; **F05 chưa triển khai**.
- Trước code F05 cần lập đặc tả/testcase và chốt quote TTL, booking horizon, chính sách giá đổi giữa quote/create; prompt ghi đề xuất cấu hình, không giả vờ các chính sách đã được người dùng duyệt. Báo chuyển/xác nhận thuộc F06, series thuộc F07, S3 live thuộc mốc media sau.

- Đã chốt phạm vi/API/error/data/testcase trước code trong `docs/features/F04-public-discovery.md`, `docs/testing/F04-test-cases.md`; prompt thực thi ở `docs/prompts/F04-public-discovery-s3.md`. F04 chỉ đọc list/nearby/detail/lịch/giá tham khảo; F05 mới tạo quote/booking và kiểm tra lại trạng thái trong giao dịch.
- API public `GET /api/v1/venues`, `/nearby`, `/{id}`, `/{id}/availability`, `/{id}/image` đã nối PostgreSQL/PostGIS và media F02. Nearby dùng `ST_DWithin`/`ST_Distance`; availability dựng ca 30 phút theo giờ địa phương, rule giá F03 và allocations RESERVED; ảnh chỉ từ upload READY/VENUE_IMAGE đúng venue published. Adapter local trả ảnh private, S3 path phát signed GET ngắn hạn. F04 không thêm migration/schema.
- Customer web tách `App.tsx`, `features/auth`, `features/venues` và CSS dùng chung. Trang tìm sân có text/geolocation/MapTiler/map fallback; detail có bảng court × 30 phút theo hình tham chiếu, màu và nhãn trạng thái, tổng giá tham khảo, ngày trong URL, reload/focus/poll. Bảng luôn hiện mọi sân; mốc giờ ở biên ô giá; bấm lại ô chọn chỉ bỏ ô đó. Có thể dùng desktop/mobile; auth F01 được giữ và regression đã chạy.
- **PASS:** backend build 0 warning/error, `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run test:api` 78/78, `npm.cmd run test:web` 40 PASS/8 SKIP live có chủ đích, `npm.cmd run test:identity-live` 8/8 trên PostGIS tạm (gồm F04 owner bảo trì → khách thấy kín → hủy → còn trống), full `npm.cmd run test:db -- --no-build` 26/26 trên PostGIS thật và hai test F04 mở rộng 2/2. Đã xem ảnh chụp UI desktop/mobile trong Playwright; bảng cuộn ngang ở mobile. Hai kỳ vọng F02 cũ về route `/api/v1/venues` đã đổi thành kiểm tra danh sách rỗng trước publish và có venue sau publish.
- **NOT RUN:** S3 provider thật/IAM/CORS/private object/signed GET vì người dùng chưa có bucket thử nghiệm và credentials local. MapTiler provider thật chưa có bằng chứng F04 riêng. Các mục này được theo dõi riêng, không gộp kết quả mock với S3/PostGIS live.
- Đã viết checklist nghiệm thu tay đầy đủ trong `docs/testing/F04-manual-acceptance.md`: PowerShell VS Code, dữ liệu mẫu, T01–T14, lỗi 400/404, F5/ảnh local, màn desktop/mobile và cách ghi bằng chứng. Tiếp theo: người dùng chạy checklist; khi có bucket non-production và cấu hình local, chạy kịch bản S3 F02/F04, rồi cập nhật trạng thái từng testcase. Một lượt DB trước đó bị chặn do Docker chưa mở; sau khi người dùng mở lại, suite đầy đủ đã PASS.

### Điều chỉnh bảng lịch theo nghiệm thu 2026-10-07

- Đã bỏ dropdown lọc sân trên customer UI; số hàng luôn bằng số court trong availability. Trục giờ hiển thị mốc bắt đầu ở ranh giới trái mỗi ô 30 phút và mốc kết thúc tại mép phải ô cuối, kèm nhãn 30 phút. Chọn lại ô trong dải chỉ bỏ ô đó; nếu bấm ô giữa của dải dài hơn hai ca thì giữ phần liên tiếp dài hơn (hòa giữ phần trước). URL customer chỉ giữ `date`; API `courtId` tùy chọn không đổi.
- **PASS:** `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run test:web -- tests/web/f04-discovery.spec.ts` 8/8 (desktop/mobile, 3 và 7 sân, vị trí giờ, toggle, giá, Back), toàn bộ `npm.cmd run test:web` 42 PASS/8 SKIP live có chủ đích. Đã xem ảnh UI desktop/mobile. Bản Playwright đầu dùng `dist` cũ và fail nhãn mới; build lại rồi suite pass.
- **Nghiệm thu:** Người dùng xác nhận đã nghiệm thu F04 và yêu cầu chốt DONE ngày 2026-10-07; không có biên bản thao tác tay chi tiết từng testcase trên venue thật. S3 live vẫn NOT RUN; login kết nối đặt sân chuyển sang F05. Chưa commit/push ở mốc điều chỉnh này.

## F04 — phân tích phạm vi và prompt tích hợp S3 — 2026-10-06 (mốc trước triển khai)

- Đối chiếu roadmap `docs/process.md`, UC-02/UC-03 và thiết kế API/data với F02–F03 hiện có. F04 gồm tìm/list/nearby, chi tiết venue/court active và lịch ca; nearby chỉ dùng PostGIS theo vị trí/bán kính, còn availability/giá tham khảo tải sau khi chọn court/ngày.
- Ghi prompt thực thi chi tiết tại `docs/prompts/F04-public-discovery-s3.md`. Prompt chốt F04 không tạo quote có expiry/booking/hold (thuộc F05), đọc giờ/giá/policy/allocation từ F03, chỉ xuất ảnh venue đã publish và không lộ QR/tài khoản.
- F02 đã có adapter S3 trong `MediaEndpoints.cs` nhưng lượt kiểm thử S3 thật vẫn **NOT RUN**; prompt yêu cầu tái dùng luồng đó, đọc ảnh bằng signed GET ngắn hạn trên bucket private và chạy thử bucket non-production. Chưa triển khai code F04, chưa thay trạng thái F02/F03; S3 chỉ được PASS sau kiểm thử provider runtime.

## Nghiệm thu tay và chuẩn bị push F02–F03 — 2026-10-06

- Người dùng xác nhận đã nghiệm thu thủ công và tạm thời chấp thuận luồng F02–F03; yêu cầu push từng feature. Đây là nghiệm thu của người dùng, không phải bằng chứng độc lập cho từng testcase. MapTiler key đã được người dùng thêm; kết quả/tọa độ provider thật không được ghi lại trong hội thoại.
- Thay đổi session Admin idle 30 phút kể từ thao tác cuối và chính sách block/min/hold vẫn nằm trong phạm vi thay đổi F01–F03 đã chốt ở `docs/prompts/F01-F03-post-acceptance-logic.md`; yêu cầu gốc ở `docs/requests/2026-10-06-post-acceptance-changes.md`.
- Đã thêm session Admin trượt trong DB, cookie `HttpOnly` cho khôi phục F5 và kiểm tra Origin; F02 hiện dùng MapTiler SDK + Geocoding picker ở ba form, timezone trim; court policy block/min/hold, API và UI F03. Migration tăng dần `20261006075958_F01F03RequestedPolicies` đặt mặc định 30/30/20 và check constraint. Không tạo tài khoản/key MapTiler, không migrate database development của người dùng.
- Bằng chứng hiện tại: `dotnet build backend/ShuttleBook.slnx --no-restore` PASS 0 warning/error; `npm.cmd run typecheck`, `npm.cmd run build` PASS; `npm.cmd run test:api` PASS 78/78; `npm.cmd run test:web` PASS 34, SKIP 8 live chủ đích; Google picker/no-location-submit + Admin F5 desktop/mobile mục tiêu PASS 4/4; DB mục tiêu sau migration/service changes PASS: Admin idle + hai restore song song 1/1, F03 block/min/hold/preview/scope/check constraint 1/1; trước đó hai migration upgrade PASS 2/2. `git diff --check` PASS (chỉ cảnh báo line ending). **Lượt `npm.cmd run test:db` đầy đủ cuối cùng không chạy được 21 ca** vì Postgres tại `127.0.0.1:54329` từ chối kết nối; 3 ca không cần server vẫn PASS. Không có Docker CLI/process trong shell hiện tại. Các DB ca mới đã PASS khi server đang mở trước đó.
- Còn mở: chạy lại full `npm.cmd run test:db` sau khi sửa kỳ vọng baseline; review hai tab Admin restore vẫn cần browser thật/cookie; nghiệm thu S3 private thật chưa chạy nên F02 chưa DONE. Booking/availability, member entitlement và sửa giá trong đơn chưa thuộc F03; policy block/min/hold chỉ là cấu hình để feature booking áp dụng sau. Không đánh dấu F02/F03 DONE chỉ từ nghiệm thu tay tạm thời.

## F02 — Chuyển provider bản đồ sang Goong — lịch sử, đã thay thế

> Lịch sử: Goong đã được tích hợp rồi bị thay thế theo yêu cầu người dùng ở lượt sau. Không còn là provider hiện hành.

- Người dùng yêu cầu dùng Goong để thử địa chỉ `31 ngõ 16 Hoàng Cầu - Hà Nội`. Thay `GooglePlacePicker` bằng `GoongPlacePicker`: debounce autocomplete v2, lấy địa chỉ/toạ độ qua Place Detail v2, hiển thị map marker Goong và bắt buộc xác nhận trước khi lưu. Contract API/backend/PostGIS không đổi.
- Cấu hình local mới: `VITE_GOONG_API_KEY`, `VITE_GOONG_MAPTILES_KEY`; cập nhật `.env.example`, `scripts/Use-LocalEnvironment.ps1`, `docs/setup.md`, F02 spec và Playwright fixtures. Không ghi key vào repo.
- **PASS:** `npm.cmd run typecheck`; `npm.cmd run build` với test placeholder keys; `npm.cmd run test:web -- tests/web/f02-onboarding.spec.ts` **6/6** (desktop/mobile), gồm assert payload mẫu giữ nguyên formatted address + latitude/longitude sau khi chọn và xác nhận; `git diff --check` không lỗi nội dung (chỉ cảnh báo CRLF/LF của các file đang sửa khác trong workspace).
- Goong đã bị thay bằng MapTiler theo yêu cầu mới; không còn là provider hiện hành.

## F02 — Chuyển provider bản đồ sang MapTiler — 2026-10-06 (IN_PROGRESS)

- Người dùng đổi provider hiện hành sang MapTiler. Thay Goong picker bằng `apps/partner-web/src/MapTilerPlacePicker.tsx`: MapTiler SDK render bản đồ, Geocoding API autocomplete trả GeoJSON/address/coordinates, marker preview và nút xác nhận; không đổi contract API hay dữ liệu PostGIS.
- Cấu hình local là một key `VITE_MAPTILER_API_KEY`; `.env.example`, `scripts/Use-LocalEnvironment.ps1`, setup/F02 docs và Playwright fixtures đã cập nhật. API key browser lộ trong DevTools nên cần giới hạn domain/quota. MapTiler Cloud Free chỉ phi thương mại và R&D theo điều khoản hiện tại; xem setup trước khi dùng cho sản phẩm thương mại.
- **PASS mock/build:** `npm.cmd run typecheck`; `npm.cmd run build` với test placeholder key; `npm.cmd run test:web -- tests/web/f02-onboarding.spec.ts` **6/6** (desktop/mobile), gồm assertion chuỗi truy vấn địa chỉ, payload address/lat/lng và xác nhận marker; `git diff --check` không lỗi whitespace (chỉ cảnh báo line endings của các file workspace khác).
- Người dùng báo đã thêm key và nghiệm thu thủ công F02–F03. Ghi nhận acceptance theo lời người dùng; suggestion/toạ độ provider live không được lưu lại. Test mock vẫn chỉ kiểm tra request/query GeoJSON → tọa độ marker → xác nhận/payload.

## F03 — Vận hành giờ, giá, QR và bảo trì — 2026-10-05 (người dùng tạm thời chấp thuận)

- Ghi chú lịch sử trước khi người dùng đổi thứ tự ngày 2026-10-06: ban đầu dự định chờ nghiệm thu F02/F03 mới sửa; yêu cầu sau đó đổi thành triển khai ngay. Trạng thái triển khai hiện hành ở mục trên.
- **F03 IN_PROGRESS, local checks PASS; người dùng xác nhận nghiệm thu tay và tạm thời chấp thuận ngày 2026-10-06.** Prompt triển khai: `docs/prompts/F03-operations-completion.md`; contract/acceptance: `docs/features/F03-court-operations.md`; testcase và bằng chứng: `docs/testing/F03-test-cases.md`. Triển khai theo Database → API → partner UI: nâng cấp giá F02 với ngày hiệu lực/priority và version court; `court_allocations`/`court_maintenance` có GiST exclusion chống trùng; API owner scope, lịch tuần/giá ưu tiên/preview/bảo trì; UI hoạt động sau khi Admin publish. QR/tài khoản sau publish vẫn qua revision F02 và Admin duyệt, không có đường ghi trực tiếp mới.
- **PASS local:** `dotnet build backend/ShuttleBook.slnx --no-restore` 0 warning/error; `npm.cmd run test:api` 77/77; `npm.cmd run typecheck`; `npm.cmd run build`; `npm.cmd run test:web` 32/32 với 8 live skip chủ đích; `npm.cmd run test:identity-live` 8/8 qua browser desktop/mobile, API, PostGIS tạm và Mailpit, gồm F03 và revision QR. F03 DB 2/2 trên PostGIS tạm; migration F02→F03 có dữ liệu QR/giá/lịch, rule/allocation exclusion, owner scope, concurrent PUT/POST, UTC, cancel idempotent. F02 revision DB có assert QR trước/sau duyệt và PASS 1/1.
- **Bộ DB đầy đủ:** lượt đầu 22/23 PASS; bài test nền F00 cũ thử gỡ `btree_gist` sau khi F03 đã tạo constraint phụ thuộc nên FAIL `2BP01`. Đã sửa bài test để xác nhận PostgreSQL từ chối gỡ extension đang dùng; chạy lại riêng baseline 1/1 PASS và F03 2/2 PASS. Chưa chạy lại nguyên bộ 23 ca sau sửa duy nhất ở test baseline. Không migrate/drop database development.
- **Còn mở:** F02 vẫn IN_PROGRESS do S3 private thật chưa nghiệm thu; full DB suite chưa chạy lại nguyên bộ sau khi sửa test baseline. Review F03 do agent thực hiện tuần tự, không có reviewer độc lập. Khi chạy API local, phải migrate đúng DB đã xác nhận trước; `/health/ready` sẽ 503 khi còn migration F02/F03 pending.

## F02 — Chủ sân khai báo và Admin duyệt — 2026-10-05 (đang thực hiện)

- **F02 IN_PROGRESS — chức năng và luồng local đã đạt.** Đặc tả/testcase được chốt trước code ở `docs/features/F02-partner-onboarding.md` và `docs/testing/F02-test-cases.md`. Migration tăng dần tạo business → venue/PostGIS/contact → court/giờ/giá nháp → ảnh/QR private → approval/revision → transactional outbox/notification. API kiểm tra user hiện hành, owner scope và Admin decision; partner/admin portal thực hiện draft, gửi duyệt, trả sửa, duyệt và revision. Giá/QR tối thiểu nằm trong F02 để không duyệt hồ sơ thiếu dữ liệu; F03 sẽ mở thêm cấu hình vận hành và hiệu lực giá theo ngày.
- **PASS local:** `dotnet build backend/ShuttleBook.slnx --no-restore` (0 warning/error), `npm.cmd run test:api` (70/70), `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run test:web` (32 PASS, 8 SKIP live chủ đích), `npm.cmd run test:identity-live` (8/8 desktop/mobile qua API/PostGIS/Mailpit/media file private; database tạm dọn đúng tên). `npm.cmd run test:db -- --logger "console;verbosity=normal"` 20/20 trên PostgreSQL/PostGIS thật; bộ F02 chạy lại sau bổ sung testcase Worker retry là 6/6. Các ca migration F01→F02, race submit/quyết định, scope, QR/ảnh, outbox retry/replay đều chạy runtime. `git diff --check` không có lỗi whitespace.
- **NOT RUN:** S3 private thật chưa có endpoint/bucket/IAM để kiểm tra presigned PUT, HEAD, checksum, quyền private và CORS. Development/Testing dùng adapter local kiểm tra byte/size/type/checksum/scope; kết quả đó không chứng minh provider S3. Theo prompt bàn giao, giữ F02 **IN_PROGRESS** tới khi có nghiệm thu object store tương thích. CI/production chưa chạy; không commit/push/deploy hoặc đụng database development.
- Bước tiếp theo: cấu hình bucket thử nghiệm S3 tương thích riêng, chạy upload/complete/view và kiểm tra quyền/CORS, ghi bằng chứng vào testcase rồi mới xét F02 DONE. Khi chạy local: `npm.cmd run db:up`, xác nhận đúng DB thử nghiệm trước `npm.cmd run db:migrate`, sau đó `npm.cmd run dev:api`, `dev:worker`, `dev:partner`, `dev:admin` theo `docs/setup.md`.
- Nghiệm thu thủ công 2026-10-05: DB local ở `127.0.0.1:54329` đang mở, cổng API 5080 ban đầu chưa có server. API chạy thử cho `/health/live` 200 và `/health/ready` 503; EF read-only `migrations list` thấy 4 migration F02 còn `Pending` trên database local mặc định. **Chưa chạy migrate trên database này**; tiến trình API chẩn đoán đã dừng. Script `dev:api` được chỉnh dùng gói đã restore (`--no-restore`) để không đọc NuGet.Config ngoài workspace trong phiên agent. Người vận hành xác nhận đúng database local và dữ liệu cần giữ trước khi áp migration để tiếp tục nghiệm thu UI.

## Mốc tích hợp F01.1/F01.4 — 2026-10-02 (lịch sử)

- **F01.1: DONE** (18/18 testcase PASS) và **F01.4: DONE** (25/25 testcase PASS). **F01.2/F01.3 giữ DONE** theo nghiệm thu trước đó; do đó phạm vi identity F01 đã đạt nghiệm thu local. F01.4 gồm migration một Admin, CLI bootstrap/rotate/suspend/activate/revoke, API login/me và admin-web; review độc lập Auth/CLI không còn lỗi nghiêm trọng. Các đoạn dưới là mốc lịch sử.
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
