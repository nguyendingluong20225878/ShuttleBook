# Tiến độ ShuttleBook

## Partner chi tiết đơn thành trang riêng; M03 nghiệm thu — 2026-10-09

- Người dùng xác nhận typecheck/build web/build backend --no-restore/test:api/test:db đã PASS và các ca test tay đã thực hiện đều PASS. **M03 DONE local**, F08 tiếp tục DEFERRED theo phạm vi owner. Không tự ghi số ca/log mới từ phía người dùng hoặc biến các gate production còn mở thành PASS.
- Yêu cầu UI mới đã hoàn tất: nút chỉ ghi **Xem đơn**, mở `#/bookings/{id}` với màn hình chi tiết riêng và **Quay lại danh sách đơn**. Không hiển thị danh sách/bộ lọc bên trên chi tiết. Giữ filter/pagination memory khi quay lại cùng business, hỗ trợ Back/Forward, F5 M03, notification đổi business; link cũ `?bookingId=...` chuẩn hóa bằng replaceState. Scope/contract trước code: [Partner-booking-detail-route](features/Partner-booking-detail-route.md).
- **PASS agent lượt này:** `npm.cmd run typecheck`, `npm.cmd run build` cho ba portal; browser affected **74/74**, 50,5s desktop/mobile với F06 Partner/F07 series/history/M03. Không FAIL/SKIP. Root review tuần tự và xem ảnh chi tiết casual desktop/fixed mobile; không ghi review độc lập. Evidence ignored `.local/partner-booking-route/{typecheck,build,browser}.log`, ảnh `test-results`. Test fixtures cho UI không thay bằng chứng PostGIS. API/DB/live không chạy mới vì không sửa backend/schema/contract; các gate agent trước và xác nhận người dùng ghi riêng.
- Đã cập nhật live locators theo nhãn mới và route mới; live mới NOT RUN trong lượt UI này. Không migration, reset development, commit/push hoặc deploy. Cần xem lại UI mới bằng cách mở đơn, kiểm URL/F5/Back/Quay lại danh sách trước commit.
- Bước tiếp theo: chốt bản UI mới và lưu baseline Git khi người dùng yêu cầu; xử lý các risk còn mở mốc2 (casual exact money/quota quote toàn tài khoản/CI Ubuntu gọi powershell.exe), sau đó F09 vận hành/backup/monitoring và môi trường thử nghiệm. F08 không chặn bản owner. S3 thật vẫn hoãn theo yêu cầu trước; không suy product production100% từ nghiệm thu local.

## M03 — phiên Customer/Partner, F08 hoãn theo scope owner — 2026-10-09 (code/test local hoàn tất)

- Người dùng hoãn F08, chỉ owner vận hành và duyệt policy Customer/Partner idle30phút, absolute30ngày từ login. Process/guide đã cập nhật DEFERRED, không gọi F08 DONE hoặc chặn đợt owner. Contract trước code: features/M03-browser-sessions.md.
- Code API cookieHttpOnly theo portal/Origin/JSON, accessmemory, startuprestore/retry, activitytừinputthật không polling/layoutscroll, logout/revocation/cross-tab/epochguard đã triển khai. Dùng LastActivityAt sẵn có, không thêm migration/devDB reset. Legacyrefresh/logout serialize family bằng advisorylock và logoutconsumedtoken không bỏ replacement.
- PASS cuối: backendRelease0warning/error26,57s, API96/96; focusedPostGIS16/16. FullDB105PASS/1connecttimeout trước Fixture.Create logic; sau sửa testlocalhost→127.0.0.1/Timeout15 và rebuild, recheckM035/5PASS17s. TRX xác nhận106ca duy nhất cóPASS qua các lượt, không gọi một lượtfull106/106PASS. Browserfull244PASS8SKIP (M0332/32); affected44/44 cuối23s sau sửa message/transition. Livecuối8/8PASS58,9s vớiPartnerF5/CustomerF5/privateproof/casual/fixed/report/confirm và cleanupDBtạm/normalbuildrestore. [Bằng chứng/lỗi và sửa](testing/M03-browser-sessions-results.md).
- Review nguồn độc lập cuối đóng, không còn P0/P1 M03 đã biết; request đã xác thực trướclogout có thể hoàn tất transaction, không cam kết rollback mọi inflightwrite. Root xem ảnhmobile thật củaCustomer/Partner F5. Các risk quota/casualamountboundary/CIhosted vẫn riêng. API5080 người dùng vẫn binarycũ, testRelease5081/DBtạm; cần restart API khi nghiệm thu tay. Hướng dẫn testing/M03-browser-sessions-manual.md. Chờ biên bản tay mới; không commit/push.

## Hướng dẫn nghiệm thu mốc 2–5 — 2026-10-09

- Đã đối chiếu code/session, workflow và testcase hiện có để viết [hướng dẫn chi tiết](testing/M02-M05-acceptance-guide.md): chuẩn bị PowerShell, dữ liệu thử, test tay/race cần PostGIS, checklist phiên và staff sau triển khai, luồng F01–F07 và gate MVP.
- Mốc2 chỉ test được một phần; search race đã sửa, auth logout/refresh race chưa có bằng chứng focused runtime, boundary casual amount/quota cần quyết định và test, workflow Ubuntu/powershell.exe cần sửa. Mốc3 restore Customer/Partner và mốc4 F08 chưa triển khai; không hướng dẫn như tính năng sẵn có. Admin idle30phút không tự áp hai role còn lại.
- Đã cảnh báo F05 manual lịch sử về policy quote không giữ chỗ; bản hiện hành quote giữ120giây, F06 report giữ sau deadline, cố định atomic toàn kỳ. Không chạy suite mới trong lượt viết hướng dẫn, không ghi PASS mới/đổi DONE, không sửa code hoặc dữ liệu/commit/push.

## Customer tài khoản không sidebar, tìm sân không bản đồ — 2026-10-09

- Người dùng xác nhận **PASS** các nhóm còn lại của mốc1: recovery vãng lai, lịch sử đơn/thông báo khi refresh/read, preview giá và Admin. Yêu cầu mới thay marker acceptance: bỏ map Customer, chỉ hiển thị khoảng cách; login không có sidebar trái. Chưa suy nghiệm thu tay hai thay đổi mới từ nghiệm thu các nhóm cũ.
- Đã triển khai [scope/acceptance trước code](features/Customer-distance-auth-layout.md): CustomerIdentity dùng header ngang cho login/register/verify; danh sách Customer toàn chiều rộng không render/load SDK map. Chọn cơ sở qua tên/Xem lịch giữ navigation/session SPA. Partner địa chỉ/bản đồ không nằm trong thay đổi này.
- Nearby vẫn lấy khoảng cách/filter từ PostGIS. Vị trí chỉ được xin theo thao tác Dùng vị trí của tôi và giữ memory; tìm tên sau đó hiển thị ước lượng đường thẳng từ tọa độ venue. Label phân biệt vị trí của bạn/khu vực tự chọn, chưa có vị trí không hiện số giả. Query/load-more cũ bị abort/ignore, dedup theo venueId; không thay API/schema.
- **PASS mới:** TypeScript/Vite3portal; affected browser60/60 desktop/mobile,0skip,30,1s: customer-distance, F07CustomerUI, F04discovery, UIrecovery, customeridentity, F05booking. Sáu executions mới về khoảng cách/source label/query race. Đã cập nhật test marker sang list→detail→quote và test geometry tài khoản theo yêu cầu mới, không bỏ business assertions.
- Root đã xem ảnh login desktop và distance mobile; chụp login desktop/mobile trên dev server với API abort, browser gates dùng API fixtures. Không tính là geolocation/device/provider thật. Backend/DB mới không chạy vì không sửa contract/schema; S3/SMTP/provider/liveproduction vẫn ngoài gate này.
- Evidence ignored: .local/ui-improvements/distance-build.log, distance-browser.log, distance-browser-exit.txt(0), screenshots/customer-login-no-sidebar-desktop.png và mobile.png; ảnh distance đã lưu cùng thư mục. PreviewIPv4 riêng đã đóng, các dev serverIPv6/API của người dùng giữ chạy.
- Bàn giao [mục2 nghiệm thu mới](testing/UI-post-audit-improvements-manual.md). F07 giữ DONE local; bốn nhóm mốc1 đã PASS theo người dùng, hai thay đổi mới chờ xem lại. Không commit/push/deploy; không migrate DB trong lượt UI này.

## Readiness local503 →200 — 2026-10-09

- Người dùng báo GET /health/ready trả503. Kiểm tra trực tiếp: live200, ready503; Docker PostGIS/Mailpit healthy, TCP54329 mở, postgis/btree_gist đủ. DB local shuttlebook thiếu migration 20261008103840_F07QuoteReservations; các migration đến F07FixedSeries đã có.
- Đã đọc migration Up: bổ sung quote_reservations/FK/index/constraint và cột allocation, không xóa hoặc cập nhật booking/payment. Sao lưu DB local trước thao tác vào .local/readiness-backups/shuttlebook_before_f07_quote_20261009_065037.dump (Git ignored); pg_restore --list PASS, chưa thực restore drill.
- Áp dụng Migrator bằng scripts/dotnet.ps1 run --project backend/src/ShuttleBook.Migrator --no-restore với target kiểm tra cố định127.0.0.1:54329/shuttlebook: PASS. Không sửa source hoặc reset/xóa volume DB.
- Kiểm chứng sau migration: GET http://localhost:5080/health/ready **HTTP200 Healthy**; migration history có F07QuoteReservations; bảng quote_reservations tồn tại. Số booking9/payment5 trước và sau giữ nguyên. API đang chạy tiếp, không cần restart để readiness phản ánh schema mới.
- Các dòng không migrate development ở mốc UI trước là bằng chứng lịch sử; lượt khắc phục readiness này đã áp dụng migration còn thiếu trên development local. Bước tiếp theo: người dùng chạy lại health rồi tiếp tục nghiệm thu UI/F07. Không commit/push/deploy.

## Điều chỉnh Customer/Partner/Admin sau audit — 2026-10-09 (code/test local hoàn tất)

- Hoàn tất năm nhóm đã chốt: marker điều hướng SPA giữ phiên; retry vãng lai cùng key/body sau lỗi mạng/TTL; refresh/poll/read giữ cửa sổ trang đơn/thông báo; Xem giá báo Đã tính giá và lỗi đúng style; Admin tìm/filter/keyset pagination, published→proposed bank/account/QR/version và confirm approve hai bước, lý do trim10–1000.
- Đặc tả/contract trước code: [UI post audit](features/UI-post-audit-improvements.md). Không thêm migration, đổi TTL/allocation/payment/fixed-series policy hoặc triển khai tiện ích mật khẩu, nhân viên, báo cáo. Không bổ sung persistence phiên Customer/Partner sau F5 trong đợt này.
- **PASS cuối:** backend build0warning/error (13,19s); TypeScript/Vite3portal; browser206PASS/8SKIP/0FAIL (1,6phút); API96/96 (11s); ba ca ASP.NET test host + PostGIS thật3/3 (20s), gồm103 hồ sơ/cursor/filter/foreignrole/current và replay đơn đã commit sauTTL, không tạo booking/payment/allocation thứ hai. Browser có48 executions mới desktop/mobile; không cộng các lần recheck trùng vào số cuối.
- Các lượt đầu phát hiện lỗi mã hóa Partner, nhánh thông báo preview và wrapper grid Admin tràn/che nút mobile; đã sửa, xem ảnh cuối desktop/mobile và chạy hồi quy. Đã sửa fixture/locator/wait tương ứng hợp đồng confirm mới/ngày/cursor. Root review đã hoàn tất; review độc lập cuối không hoàn tất do giới hạn tài nguyên, không ghi PASS độc lập. Chi tiết: [kết quả và giới hạn](testing/UI-post-audit-improvements-results.md).
- Docker local/PostGIS/Mailpit healthy; các test DB dùng database tạm, không migrate development. Preview do test tạo đã đóng. Không commit/push/merge/deploy hoặc tạo S3. Bằng chứng ignored ở .local/ui-improvements; code/test/tài liệu sẵn để review.
- **Chờ nghiệm thu tay bản sửa:** [hướng dẫn từng nhóm](testing/UI-post-audit-improvements-manual.md). MapTiler/QR provider thật, browser→API→Worker live mới, S3 AWS, thiết bị thật/NVDA/contrast/production/pilot NOT RUN trong lượt này. **F07 giữ DONE local** theo nghiệm thu trước; đợt chỉnh UI nghiệm thu riêng.

## Audit UI từng control và đối chiếu ALOBO — 2026-10-09 (hoàn tất đánh giá)

- Đã viết và thực thi [prompt](prompts/ShuttleBook-ui-button-audit-alobo.md), giao ba reviewer Customer/Partner/Admin theo quyền AGENTS; root đối chiếu source/file:line, hướng dẫn ALOBO chính thức, public browser và ảnh fixture. Bàn giao [báo cáo](reviews/ShuttleBook-ui-button-audit-alobo.md) và [checklist agent/tay từng control](testing/ShuttleBook-ui-button-acceptance.md).
- Inventory **220 record logic**: Customer83, Partner112, Admin25; **218 control/family có source và2 dòng SDK chưa quan sát** (C40/P60). Không gọi tổng này là220 nút đã bấm: có input/select/family; controls nội bộ SDK không bịa nhãn. Mỗi record có action/state/good/gap/priority/source/evidence và testcase.
- **PASS build mới** ba portal (TypeScript/Vite); **158 PASS / 8 SKIP browser** desktop/mobile,1,2 phút, `scripts/Test-Web.ps1 --workers=2`. Tám live executions SKIP vì không bật API/DB harness; không dùng UI fixture thay bằng chứng PostgreSQL. Wrapper lượt đầu vấp PowerShell stderr warning; chỉnh helper local và chạy lại suite đạt, không sửa source production để làm pass.
- Root **tái hiện C53 trên UI fixture**: create POST bị abort →clock+121s →nút Xác nhận tạo đơn disabled, chỉ1 POST, link Đơn của tôi còn. Commit DB/mất response sau commit thật **NOT RUN**; finding recovery không được gọi mất đơn/đặt trùng đã chứng minh. Marker full navigation/memory session, query pagination muộn, polling làm mất trang cũ, auth refresh/logout race và numeric casual boundary ghi riêng source/risk; focused API/PostGIS gates chưa chạy mới.
- ALOBO chỉ truy cập nguồn/hình công khai: onboarding, Guest chrome sau bỏ qua, login chủ sân desktop/mobile và guide đặt lịch/duyệt/giá/khóa/nhân viên/report. Authenticated Admin/owner flow, backend/storage/TTL race **UNKNOWN**; Guest còn spinner khi chụp nên không chấm hiệu năng. Không đăng ký/account/OTP/booking/transaction trên ALOBO.
- **Giữ F07 DONE local** theo nghiệm thu trước; không tự triển khai backlog/F08/F09 hoặc đổi các feature/provider khác. Ưu tiên xác minh recovery/auth/query/polling trước pilot, sau đó tiện ích auth/mobile/giá/đối chiếu, F08 nhân viên và F09 vận hành/báo cáo. S3/MapTiler/provider thật, screen-reader/contrast/device thật, production/load/restore/pilot vẫn NOT RUN trong lượt này.
- Evidence ignored tại `.local/ui-button-audit/`: inventory, log, probe, screenshot fresh/manifest. Không migrate development, tạo tài nguyên trả phí, commit/push/merge/deploy; helper preview do audit tạo đã đóng. Chỉ thay tài liệu của lượt audit, giữ các thay đổi code có sẵn của người dùng.



## F07 — nghiệm thu và hoàn tất đánh giá sản phẩm — 2026-10-08 (DONE local)

- Người dùng xác nhận **đã nghiệm thu F07**, gồm lịch cố định, giữ chỗ từ báo giá và Customer UI mới; yêu cầu chạy prompt đánh giá sản phẩm trước khi chốt DONE. Đã thực thi `docs/prompts/ShuttleBook-product-review-alobo.md` và hoàn tất [báo cáo](reviews/ShuttleBook-product-readiness.md), với hai review độc lập backend/frontend, kiểm lại source/file:line, log/TRX và nguồn ALOBO chính thức.
- **F07 chuyển DONE theo phạm vi local đã duyệt.** Căn cứ: acceptance người dùng + gate trước đó (API96, browser72 và affected28, live8,72 testcase DB duy nhất đạt qua các lượt) + review. Lượt đánh giá không sửa production, không chạy lại regression rộng; đã đối chiếu artifact chứ không rebrand thành test mới. Không dựng biên bản từng testcase tay mà người dùng chưa gửi.
- Báo cáo đưa rõ các finding/backlog trước pilot: CI web Ubuntu gọi powershell.exe; rủi ro logout/refresh Customer/Partner cần focused PostGIS xác minh; marker map full navigation làm mất memory session; polling Partner có thể reset các trang tải thêm; quote hold đa sân cần abuse policy; boundary số tiền casual trên JS safe integer. Các source-risk/UX này không được coi là lỗi runtime mới đã xác nhận trong allocation/payment toàn kỳ F07 và không được tự sửa trong lượt đánh giá.
- Source/artifact review PASS; health probe5080/5173–5175 không truy cập được nên duyệt flow mới NOT RUN. S3 AWS thật, hosted CI, load/security production, backup/restore drill và pilot vẫn NOT RUN/chưa có evidence. Không chuyển các gate này PASS vì F07 DONE.
- Khuyến nghị: xử lý cản trở pilot/xác minh auth → F08 nhân viên → F09 media/CI/backup/monitoring/load → pilot1–3cơ sở → report/phiên/chia sẻ → tăng trưởng theo dữ liệu. Chưa triển khai F08/F09; không tự đổi F02/F03/F05 DONE, không migrate development, commit/push/merge/deploy hay tạo tài nguyên trả phí.

## F07 — giữ chỗ từ báo giá và Customer UI: code/test local hoàn tất — 2026-10-08 (IN_PROGRESS)

- Yêu cầu mới thay chính sách quote không giữ chỗ: Customer ACTIVE nhận báo giá hợp lệ được giữ ngay các ca, mặc định 120 giây; cố định giữ toàn kỳ atomically. Hết hạn chưa tạo đơn trả trống; tạo đơn chuyển chính allocation QUOTE_HOLD sang BOOKING và bắt đầu hạn thanh toán theo holdMinutes. Đơn đã báo chuyển/NEEDS_REVIEW tiếp tục giữ theo F06. Guest vẫn tìm/xem lịch; cần đăng nhập trước báo giá có giữ chỗ.
- Contract trước code: [F07 quote reservations](features/F07-quote-reservations.md). Migration 20261008103840_F07QuoteReservations bổ sung reservation, TTL và FK scope allocation/court, dùng chung exclusion chống đặt trùng. Quote thay thế cùng sân giữ hạn cũ; thất bại rollback giữ quote trước. Public bỏ qua expired ngay khi Worker dừng; API/Worker dọn allocation vật lý. Không migrate DB development; các gate migration chỉ dùng DB tạm.
- **PASS backend build cuối:** solution 0 warning/error, 3,42 giây, sau tất cả sửa review. **PASS API 96/96** (1 phút 14 giây). **PASS typecheck toàn workspace**; Customer typecheck/Vite build cuối đạt.
- **PASS UI:** 72/72 focused browser (35,8 giây), desktop/mobile; sau bổ sung xử lý QUOTE_CONSUMED chạy lại 28/28 affected (15,5 giây, gồm 24 lặp và 4 trường hợp mới). Đã xem screenshot desktop/mobile. Customer sidebar/toolbar/palette/cards/forms đồng bộ Partner/Admin; countdown ghi rõ giữ tạm. UI fixture không thay bằng chứng transaction.
- **PASS PostgreSQL/PostGIS qua các lượt:** 72 testcase duy nhất có bằng chứng PASS (56 baseline + 16 mới), đã đối chiếu tên testcase trong TRX. Lượt rộng 68 ca: 62 PASS/6 FAIL fixture; cập nhật fixture theo policy/schema mới rồi kiểm lại các ca liên quan đều PASS. Lượt đầu 12/12 giữ quote đạt; các lượt bổ sung bao phủ migration, fresh replay và quyền sở hữu. Không gọi một lượt full 72/72 PASS. Chi tiết lỗi, sửa và lệnh: [kết quả](testing/F07-quote-reservations-results.md).
- **PASS live 8/8** (52,5 giây): browser → API/PostGIS/Worker/Mailpit thật; kiểm lịch public thấy RESERVED ngay sau quote vãng lai/cố định và trước tạo đơn, tiếp tục private proof/report/owner confirm/list/F5 desktop/mobile. DB tạm đã dọn đúng tên; normal web build được phục hồi. Live không thay các ca fresh replay/race/migration được test riêng trên PostGIS.
- Review backend độc lập đã sửa casual replay kiểm fresh status/type/ownership trước hash; không còn finding P0/P1 source đã biết trong phạm vi thay đổi. NuGet audit online, S3 AWS thật, MapTiler provider thật, CI hosted, load/backup drill và nghiệm thu tay phiên bản mới: **NOT RUN**; không tính PASS production.
- Đã viết [prompt đánh giá sản phẩm](prompts/ShuttleBook-product-review-alobo.md) và [báo cáo nhận xét/lộ trình](reviews/ShuttleBook-product-readiness.md), đối chiếu nguồn ALOBO chính thức. Khuyến nghị: nghiệm thu điều chỉnh F07 → F08 nhân viên → F09 media/CI/backup/giám sát → pilot ít cơ sở → báo cáo và mở rộng theo dữ liệu.
- Bàn giao [nghiệm thu thay đổi](testing/F07-quote-reservations-manual.md): người dùng chạy npm.cmd run db:migrate rồi restart API/Worker/ba portal. F07 giữ IN_PROGRESS chờ xác nhận nghiệm thu logic mới. Không commit/push/merge/deploy tự động.

## F07 — hoàn tất code và kiểm thử local, chờ nghiệm thu tay — 2026-10-08 (IN_PROGRESS)

- Đã thực hiện prompt F07 theo chính sách được duyệt: cùng sân/thứ/khung giờ hàng tuần, mỗi buổi >=max120/minimum sân, kỳ >=một tháng lịch, cửa sổ60ngày/max12buổi. Quote120s; create giữ toàn kỳ atomically; một payment/QR thu100%, owner xác nhận một lần. Group detail/list/filter/count/QR/privateproof và report/review/supplement/confirm/reject/expiry/SLA/outbox dùng canonical anchor; occurrence endpoint không xử lý riêng một buổi.
- UI customer có lựa chọn cố định ở lịch chung, form kỳ/preview từng buổi/conflict/tổng/TTL và QR cả kỳ; partner một dòng nhóm/bảng từng buổi/tổng/whole-period quyết định. Admin/customer/partner dùng layout theo feature, giữ session/idle/quyền. Review độc lập sửa privacy pagination403/404 và fresh-actor replay sau khi chờ khóa; không còn lỗi production nghiêm trọng đã biết.
- **PASS build cuối:** solution backend0warning/error (5,65s), typecheck toànworkspace, build3web. API96/96PASS (1m17s); recurrence11 nằm trong96, không cộng trùng.
- **PASS browser:** full150PASS/8SKIP (1,1phút) và affectedpartner26/26PASS (21s) sau privacyfix.8SKIP là live riêng. Đã kiểm screenshots customer/partner/Admin desktop/mobile, layout375/768/1024/1440 và keyboard/session/privatecache.
- **PASS live8/8** (1,1phút): browser→API/PostGIS/Worker/Mailpit thật, F01→F07 includingfixedquote/create/alloccurrences/privateproof/report/wholeconfirm/listgroup/F5 desktop/mobile. Lượt đầu6PASS/2FAIL duplicate header/main link đã sửa locator, chạy lại cả8PASS. DBtạm dọn đúng tên và normalwebbuildrestore. Đây là gate happyflow; fresh-replay authfix sau đó được kiểm riêng bằng PostGISrecheck dưới đây.
- **PASS PostgreSQL/PostGIS22caF07 qua các lượt:** lần đầu18PASS/2FAIL20ca (5,1662phút; fixturefuturenbf và expectedstatus sai), sau sửa chạy6ca liên quan/bổ sung6/6PASS (1,5232phút):2repair+2newdowngrade/freshauth+2repeat. Không gọi một lượt full22PASS. Các ca bao phủ transactional rollback/exclusion23P01 thật/freshcontext, lateconflict/concurrency/idempotency/priceoverflow/snapshot, groupcommands/privateproof/rights, queuedreportqua deadline, Workerexpiry/SLAparallel+skiplocked, F06backfill/repeat và downgradepreserved.
- **PASS hồi quy F05/F0634ca qua các lượt:** combined33PASS/1FAIL34 (8,2503phút), fixture chạy API mới trên schema đã downgrade đã sửa đúng cách tạo history trước downgrade; recheck1/1PASS (26,2052s). Không gọi một lượt full34PASS. Report/QR/proof/history/replay/constraints/casual4/5/6/7ca và outboxretry/dedup/commitrollback/SLA vẫn đạt.
- Restore/build trong runner dùng profile tạm riêng workspace để xử lý thiếu APPDATA của subprocess; không đổi cấu hình máy/repo, không thêm dependency. NuGet audit vulnerability online NOT RUN vì network; S3 thật/MapTilerprovider thật và nghiệm thu tay người dùng NOT RUN theo phạm vi đã nêu.
- Bàn giao: docs/prompts/F07-fixed-series-ui.md, docs/features/F07-fixed-series.md, docs/testing/F07-test-cases.md và **docs/testing/F07-manual-acceptance.md**. Người dùng chủ động npm.cmd run db:migrate rồi restartAPI/Worker/3portal để nghiệm thu local. F07 giữ IN_PROGRESS chỉ chờ xác nhận nghiệm thu; không migrate development, commit/push/merge/deploy tự động.


## F07 — tiếp tục hoàn tất và kiểm chứng — 2026-10-08 (IN_PROGRESS)

- Tiếp tục từ code/migration/API/UI đã viết, giữ ba chính sách được duyệt. PostgreSQL54329/Mailpit8025 đã kết nối; các cổng preview5173–5175/APItest5081 trống. Root tích hợp live customer→owner cả kỳ; phân công review backend, QA PostGIS và hoàn thiện customer với phạm vi file riêng.
- **PASS frontend 2026-10-08:** build cả ba portal; `scripts/Test-Web.ps1 --workers=2` **150PASS/8SKIP**,1,1phút. SKIP là8ca live được chạy riêng, không coi skip là pass. Gồm20ca fixed customer,4ca partner series và UI/identity/F02/F04/F05/F06 hồi quy desktop/mobile. Đã xem ảnh quote conflict desktop và giá lớn mobile. Review customer sửa private quote/intent khi403/404, quote double-submit và end24:00; fixture clock đồng bộ để TTL test không phụ thuộc ngày máy. PostGIS/API/live mới chưa hoàn tất ở mốc này.
- Các kết quả ngày07 bên dưới là bằng chứng lịch sử. Gate mới đang chạy; chưa ghi PASS hoặc DONE khi chưa có runtime. Không migrate DB development, không commit/push và không triển khai S3 thật.

- **PASS gates tiếp theo:** API96/96 (1m17s); partner affected26/26 (21s) sau sửa pagination403/404 xóa cache private, được review độc lập lại. Live scripts/Test-Identity-Live.ps1 -ApiPort5081 **8/8PASS**,1,1phút: browser/API/PostGIS/Worker/Mailpit thật, F02→F07 casual +fixedquote/create/all-occurrences/proof/report/wholeconfirm/listgroup/F5 desktop/mobile. Lượt đầu6PASS/2FAIL do link Đơn của tôi trùng header/main; giới hạn locator header rồi chạy lại cả8PASS. DBtạm dọn đúng tên, normal webbuild restore; S3/MapTilerprovider thật không kiểm trong gate này.
- **Review cuối đang xác minh:** sửa GETseries scope, rollback23P01 contextmới, lỗi giá/lịch kèm ngày và Worker bỏ qua nhóm khóa. PostGIS lượt20ca:18PASS/2FAIL fixture (JWTfuture nbf khi re-login sau clockadvance, expectation closedschedule400 so với contract409). Fixture đã sửa, đang recheck6ca gồm2ca mới downgrade/freshauth. Replayhash409 trước freshactor khi chờlock đã sửa. Negativeauth và hồi quy34F05/F06 đang chạy, không ghiPASS trước runtime.

## F07 — chính sách đã duyệt, triển khai luồng toàn kỳ — 2026-10-07 (IN_PROGRESS)

- Người dùng trả lời **“đồng ý cả 3”**: FULL_SERIES100% cả kỳ/một QR/một xác nhận, horizon60ngày/max12buổi, quote120s/holdMinutes sân. Chưa báo chuyển hết hạn giải phóng toàn kỳ; reported/NEEDS_REVIEW giữ mọi buổi chờ owner. Đã đồng bộ đặc tả/prompt/designer notes/README/nghiệp vụ/API/sequence trước code phụ thuộc.
- Contract: quote conflict preview200 không giữ chỗ và không có quoteId usable; create conflict409 SERIES_CONFLICT rollback toàn kỳ. Một payment anchor; normalize occurrence ID trước lock/hash; list một nhóm, filter match bất kỳ buổi; tiền detail là tổng kỳ, occurrence giữ giá riêng. Phân công backend, customer UI và reviewer độc lập; root tích hợp partner UI/tài liệu/QA.
- Các PASS của mốc chuẩn bị bên dưới giữ nguyên lịch sử. Schema/API/payment/Worker/fixed UI và integration mới đang triển khai, chưa tính PASS và chưa DONE F07. Không tự commit/push/migrate DB development; S3 thật tiếp tục hoãn.
- Partner đã có một dòng series/kỳ/số buổi/tổng cả kỳ, bảng giá snapshot từng buổi, hướng dẫn quyết định toàn nhóm và checkbox final reject ghi rõ giải phóng mọi buổi. Typecheck/build partner PASS. Browser `npm.cmd run test:web -- tests/web/f07-partner-series.spec.ts tests/web/f06-partner.spec.ts --workers=2` bản cuối **22/22PASS**,15,2s (4series+18F06 desktop/mobile); đã xem ảnh hai viewport. Lượt đầu20PASS/2FAIL vì fixture dùng exact text label textarea chứa giá trị sau rerender; sửa locator theo textbox accessible name, không đổi production logic để né test. Stub chỉ chứng minh UI/contract, PostGIS mới chưa chạy tại mốc này.

## F07 — prompt, recurrence và UI các vai trò — 2026-10-07 (IN_PROGRESS)

- Người dùng yêu cầu viết prompt và thực hiện F07 cùng cải thiện UI các role dựa trên GitHub UI UX Pro Max. Git ban đầu sạch, nền commit `060fc1c` F06. Đã đọc roadmap/UC/state/data/API và code F05/F06, viết prompt `docs/prompts/F07-fixed-series-ui.md`, đặc tả `docs/features/F07-fixed-series.md`, thiết kế kỹ thuật `F07-designer-notes.md`, testcase và checklist UI tay.
- **Chính sách còn chờ trả lời:** FULL_SERIES100% toàn kỳ/một QR/một xác nhận; horizon60ngày/max12buổi; quote120s/holdMinutes sân/expiry toàn kỳ chưa báo chuyển. README/process/00 còn mở payment plan; không suy luận đồng ý từ thời gian chờ hoặc câu đồng ý F06 cũ. Chưa triển khai migration/API quote-create/payment group/Worker group/UI fixed khi quyết định tài chính chưa chốt. F07 không DONE.
- Designer đề xuất series + từng occurrence booking/allocation, một payment anchor; PaymentScopeId non-null CHECK/composite FK/unique để giữ payment đúng nhóm, preserve casual dữ liệu cũ. Normalize occurrence ID trước lock/hash; series→bookings theo thứ tự→scope/payment→allocations, xử lý toàn kỳ. Quote conflict preview200 là ADR đề xuất cần đồng bộ docs02 sau khi chốt, không âm thầm thay contract cũ.
- Đã triển khai độc lập `WeeklyRecurrence`: tháng lịch inclusive, thứ/ngày, >=max120/minimum sân, lưới30, count do caller truyền, invalid/ambiguous DST và UTC-offset lẻ. Không quyết định horizon/payment, không gọi DB. **PASS11/11**,46ms: `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --no-restore --filter FullyQualifiedName~WeeklyRecurrenceTests` (agent lượt đầu39ms). Solution build PASS0warning/error; typecheck và build3web PASS.
- UI customer: shell/nav/skip, identity/search/notification cards/forms/feedback, booking facts/tổng/chi tiết ca collapsible; giữ session memory/route/API/schedule columns30phút. Partner giữ shell, rõ list/filter/selected detail/tổng, responsive; business logic payment không đổi. Admin có sidebar/overview/approvals/notifications, counters từ API (approval số trong list tải tối đa100), giữ draft/hash history; một nguồn notifications, không fake analytics/quyền payment.
- Review độc lập phát hiện và đã sửa: Admin skip đổi hash; facts nền lấy nhầm token chữ; token-refresh abort detail làm stuck disabled; shared restore Response bị parse hai lần trong StrictMode; cacheprivate401/403; focus notification autoGET gia hạn server idle. Giữ idle30phút từ thao tác thật; notifications Admin chỉ reload chủ động/mount, không polling/focus. **PASS runtime Vite DEVELOPMENT StrictMode5185** bằng browser fixture: một restore, deep link được mở, không page error/storage; không dùng fixture này làm bằng chứng cookie server.
- **Browser:** lượt đầu96PASS/4FAIL100 ca,54,7s (skip bản dist cũ +locator nút menu đổi tên). Rebuild bản sửa chạy lại104 ca: **102PASS/2FAIL**,50,5s; hai FAIL là testgetByText exact weekday nằm trong listitem gồm cả giờ. Sửa chỉ locator testcase theo nội dung listitem; **PASS recheck2/2**,3,4s `npm.cmd run test:web -- tests/web/f07-admin-ui.spec.ts --grep "admin deep link" --workers=2`. Không gọi đây là một lượt full104PASS. Các identity/F02/F04/F05/F06customer-partner-admin/newUI/cache/refresh/idle còn lại PASS trong lượt104. Đã xem screenshotcustomer/Admin/partner desktop/mobile, widths375/768/1024/1440; không lỗi bố cục nghiêm trọng.
- Live F01→F06 đang kiểm tra DB tạm riêng/API5081/Worker/Mailpit; lượt đầu6PASS/2FAIL8 ca,58,4s vì emailinput mới type=email native validation chặn request, testcase cũ chờ lỗi API. Sửa test kiểm typeMismatch/focus/0request, rồi weak password phải gọi API1lần và báo lỗi; đang chạy lại, chưa ghi PASS khi pending. DB tạm lượt đầu đã dọn đúng tên, normal webbuild restore. Không migrate DB development, commit/push/deploy; S3/MapTilerprovider live không chạy.
- **Gate live cuối PASS8/8**,47,7s: `npm.cmd run test:identity-live -- -ApiPort 5081`. Lượt giữa6PASS/2FAIL vì cold request đăng ký vẫn đang gửi quá5s; testcase chờ và assert HTTP202 trong20s trước kiểm màn verify, không thay API hoặc giả thành công. Bản cuối browser→API/PostGIS/Worker/Mailpit thật đạt đăng ký/xác minh/login/Admin và F02→F06 ảnh local/scope/QR/quote/report/review/supplement/confirm/outbox/F5. DB tạm mỗi lượt dọn đúng tên; webbuild normal đã restore. Typecheck/build/solution/diffcheck đạt, unit11PASS, browser104ca đã đạt102trong lượt kết hợp +2ca sau sửa locator. UI mốc hoàn tất, toàn F07 vẫn IN_PROGRESS vì payment plan chưa nhận đáp án; các DB/API/payment/Worker/flowfixed testcase F07 vẫn NOT RUN.

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
