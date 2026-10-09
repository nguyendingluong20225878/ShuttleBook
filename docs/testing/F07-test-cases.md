# F07 — testcase và bằng chứng

> Chốt 2026-10-08: người dùng đã nghiệm thu F07, bao gồm thay đổi quote giữ tạm/Customer UI; prompt đánh giá sản phẩm đã hoàn tất. **F07 DONE local**. Các câu chờ nghiệm thu hoặc IN_PROGRESS bên dưới thuộc mốc lịch sử; không tự lập biên bản PASS từng ca tay. S3/provider/production vẫn có gate riêng NOT RUN; xem progress và báo cáo sản phẩm.


Cập nhật2026-10-08: chính sách giữ chỗ từ quote theo [contract mới](../features/F07-quote-reservations.md). Các bằng chứng phía dưới trước thay đổi là lịch sử; kiểm thử mới ghi trong progress.

Ngày 2026-10-07. F07 IN_PROGRESS; người dùng đã duyệt FULL_SERIES/60 ngày/12 buổi/quote120s/holdMinutes. Bảng dưới đã cập nhật theo gate cuối; lịch sử các lượt FAIL/recheck giữ phía sau. Không dùng unit/UI stub thay bằng chứng booking PostgreSQL.

| ID | Trường hợp / cách kiểm | Mong đợi | Loại / trạng thái |
|---|---|---|---|
| R01 | 01/04–01/05 thứ Ba; 01/10–01/11 thứ Năm | 4/5 buổi inclusive, đúng thứ/120phút | Unit PASS |
| R02 | 31/01–28/02; dưới một tháng lịch | Accept month-clamp; reject dưới tháng | Unit PASS |
| R03 | 90/135 phút, min180 nhưng chọn120; 150 phút | Reject short/non-grid; accept150 theo lưới | Unit PASS |
| R04 | Giờ18:15, ca qua ngày, count quá tham số | Reject, không partial occurrence | Unit PASS |
| R05 | NewYork spring/fall DST, Kathmandu offset45phút | Reject invalid/ambiguous; giữ local grid/UTC đúng | Unit PASS |
| D01 | Quote mọi buổi giá khác nhau; thiếu một ca/QR; expired/changed | Giá chính xác, giữ chỗ tạm toàn kỳ khi quote hợp lệ; block missing setup | DB/API PASS qua các lượt |
| D02 | Fourth-week conflict; simultaneous series/casual/maintenance | Rollback toàn kỳ, một winner, conflictDates | PostGIS PASS qua các lượt |
| D03 | Concurrent same-key replay/keychanged; scope suspend/revoke | One group/payment/event; không bypass auth | PostGIS PASS qua các lượt |
| D04 | Upgrade F06 dữ liệu evidence/idempotency/QR; repeat migration | Bảo toàn, checks/FKs/uniques; không xóa dữ liệu | PostGIS PASS qua các lượt |
| P01 | Report chờ khóa qua deadline, expiry nhiều Worker; report{} với/không READY proof | Whole-group single transition; private scoped proof | PostGIS PASS qua các lượt |
| P02 | Review→supplement sau deadline; exact-total confirm/reject | Giữ cả kỳ; CONFIRMED/PAID hoặc release toàn kỳ | PostGIS PASS qua các lượt |
| P03 | Occurrence endpoint bypass; stale/retry/race scope | Không mutate riêng một buổi, valid replay/version | PostGIS PASS qua các lượt |
| P04 | SLA30min/owner+Admin/outbox retry | Một alert, no release/reset, role deep link | PostGIS PASS qua các lượt |
| U01 | Customer search/identity/detail/bookings/notifications desktop/mobile | Semantic label/focus/touch; không viewport overflow; lưới cuộn nội bộ | Browser PASS; live luồng cũ PASS |
| U02 | Admin overview→approvals→notifications, Back/F5/menuEscape | API counters thật, giữ draft, restore/idle30min, scope không đổi | Browser PASS; development StrictMode PASS |
| U03 | Partner list/filter/scoped detail/payment retry/review/confirm | Đọc rõ tổng/tiền, stable intent/version/proof quyền cũ | Browser PASS; live F02→F06 PASS |
| U04 | Fixed form/quote/conflict/create/QR/list/detail toàn kỳ | End-to-end đúng chính sách | Browser/live PASS |

## Gate cuối — 2026-10-08

- Build backend solution: PASS0warning/error,5,65s; typecheck toànworkspace/build3web PASS. API96/96PASS,1m17s (bao gồm recurrence11, không cộng trùng).
- Browser full150PASS/8SKIP,1,1phút. Sau privacyfix, affectedpartner26/26PASS,21s.8SKIP là live riêng. Screenshots customer/partner/Admin desktop/mobile đã xem; viewport375/768/1024/1440.
- Live8/8PASS,1,1phút: F01→F07 trên browser/API/PostGIS/Worker/Mailpit thật và database tạm. Fixedquote/create/allbuổi/privateproof/report/wholeconfirm/list/F5 đạt desktop/mobile. Lượt đầu6PASS/2FAIL duplicate link đã sửa locator, không sửa production để né test. Fresh-replay authfix sau live được kiểm bằng PostGISrecheck6ca bản cuối.
- F07 PostGIS22case unique đạt qua18PASS cũ +6PASSrecheck (2repeat+4repair/new), không là một lượt full22PASS. Hồi quy34F05/F06 đạt33PASS cũ +1PASSrecheck, không là một lượt full34PASS.
- PASS git diff --check (chỉ lineendingwarning). Không dùng stub thay PostGIS; không migrate DB development.
- NOT RUN: nghiệm thu tay người dùng, S3provider thật/MapTilerprovider thật trong gate này; NuGetvulnerabilityaudit online vì network. S3 tiếp tục hoãn theo yêu cầu.

### Lệnh PostgreSQL và kết quả

Chạy từ repo root trong PowerShell; runner của agent dùng profile tạm trong workspace cho APPDATA/LOCALAPPDATA/ProgramData do môi trường subprocess thiếu các biến này. Không sửa global config hoặc in connection string.

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --filter 'FullyQualifiedName~FixedSeriesTests' --logger 'console;verbosity=normal'`
   - 18PASS/2FAIL20case,5,1662phút. Hai lỗi fixture: re-login sau virtualclockadvance tạo JWTfuture nbf; closedinterval expectation400 khác contract409PRICE_UNAVAILABLE. Fixture đã sửa.
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --output '.local/f07-qa-recheck/bin' --filter 'FullyQualifiedName~Occurrence_ids_normalize|FullyQualifiedName~Quote_checks_roles|FullyQualifiedName~Downgrade_refuses|FullyQualifiedName~Replay_with_changed_intent|FullyQualifiedName~Replay_rechecks_active|FullyQualifiedName~Concurrent_same_key_create' --logger 'console;verbosity=normal'`
   - 6/6PASS,1,5232phút. Bao gồm freshactor suspended sau chờ advisorylock, downgrade không xóa series và2fixture sửa; output tách để không ghi đè DLL đang chạy.
3. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build --filter 'FullyQualifiedName~CasualBookingTests|FullyQualifiedName~PaymentConfirmationTests' --logger 'console;verbosity=normal'`
   - 33PASS/1FAIL34case,8,2503phút. Lỗi fixture downgrade schema rồi gọi CURRENTF07API; đã tạo historicaldata trước downgrade, kiểm old NOTNULL bằng SQL và repeatupgrade.
4. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --output '.local/f07-qa-migration/bin' --filter 'FullyQualifiedName~Optional_reference_migration_preserves_legacy_history_snapshots_and_idempotency' --logger 'console;verbosity=normal'`
   - 1/1PASS,26,2052giây. Historicalreference/evidence/QR/hash và replay bảo toàn.

### Mapping bằng chứng

- D01: Prices_are_snapshotted_per_occurrence, Missing_late_price_or_QR, Expired_or_changed_quote, Quote_checks_roles.
- D02: Fourth_week_conflict, Concurrent_series_and_casual_or_maintenance (2), Competing_different_customer_series, Exclusion_constraint_race (Postgres23P01 thực, rollback/freshcontext).
- D03: Concurrent_same_key_create, Replay_rechecks_active, Creating_series_rechecks_suspended_customer, Replay_with_changed_intent.
- D04: Upgrade_from_final_F06, Downgrade_refuses và scopedconstraintSQL trong Replay_rechecks_active.
- P01: Occurrence_ids_normalize (READYprivateproof/report), Report_waiting_for_series_lock (request chờ khóa, qua deadline trả409, Worker giải phóng mọi buổi), Expiry_releases_unreported (4Worker/retention). Không tuyên bố đã chạy một race report/Worker độc lập khác.
- P02: Occurrence_ids_normalize (review/supplement sau deadline/exactwholeconfirm), Final_rejection_releases_whole.
- P03: Occurrence_ids_normalize, Concurrent_same_key_create_and_cross_occurrence_report, Final_rejection confirm/rejectrace và Series_read_has customer/operator/otherroles scope.
- P04: Expiry_releases_unreported SLAowner+Adminonce/noreset/norelease; Locked_earliest_series (2) skiplockedexpiry/SLA; F06runtime Unknown_or_no_recipient, Outbox_commit_failure, Thirty_minute_SLA kiểm retry/dedup/rollback.
- U04: f07-series-customer20case+f07-partner-series8case desktop/mobile (4case privacy bổ sung ở retest) và livef02F07 hai viewport.


## Lịch sử các mốc và giới hạn

### Mốc triển khai sau khi duyệt

- API96/96PASS (1m17s); partner sau privacyfix26/26PASS (21s). Live8/8PASS (1,1phút) trên DBtạm/Worker/Mailpit thật, casual và fullfixedcreate/proof/report/owner-confirm/list/F5 desktop/mobile. Lượt đầu6PASS/2FAIL duplicate link đã sửa locator rồi cả8PASS; normalbuildrestore/tempDBcleanup đạt.
- PostGIS focused đầu20ca:18PASS/2FAIL fixture, sửa2fixture đang recheck6ca (2repair+2newdowngrade/authbarrier+2replays). Hồi quy34F05/F06 đang chạy. Không gọi18PASS hoặc sourcefix là fullF07PASS.


- 2026-10-08: build3web **PASS**, full browser `scripts/Test-Web.ps1 --workers=2` **150PASS/8SKIP**,1,1phút;20case fixed customer+4partner series, UI3role/casual/session/proof regression.8SKIP là live riêng. Ảnh quoteconflict/largeexacttotal mobile-desktop đã xem. Đây là UI fixtures, không thay transaction PostGIS.

- Partner series + F06 regression: `npm.cmd run test:web -- tests/web/f07-partner-series.spec.ts tests/web/f06-partner.spec.ts --workers=2` **PASS22/22**,15,2s; typecheck/build riêng partner PASS. Kiểm một dòng nhóm/5buổi, tổng2triệu không nhận400nghìn một buổi, command anchor/If-Match/idempotency, review giữ cả kỳ và final reject whole-period, desktop/mobile/overflow. Đã xem ảnh. Lượt đầu20PASS/2FAIL exact textarea label, sửa locator textbox rồi chạy lại cả22PASS; fixture không chứng minh server group transaction.

### Lịch sử mốc chuẩn bị

- Unit: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --no-restore --filter FullyQualifiedName~WeeklyRecurrenceTests` **PASS11/11**,39ms. Unit không kiểm tra DB/price/payment/horizon.
- Solution build `scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore` PASS0warning/error; `npm.cmd run typecheck`, `npm.cmd run build`, `git diff --check` PASS (Git chỉ báo line endings).
- Browser: `npm.cmd run test:web -- tests/web/f07-customer-ui.spec.ts tests/web/f07-admin-ui.spec.ts tests/web/customer-identity.spec.ts tests/web/admin-identity.spec.ts tests/web/f02-onboarding.spec.ts tests/web/f04-discovery.spec.ts tests/web/f05-booking.spec.ts tests/web/f06-customer.spec.ts tests/web/f06-partner.spec.ts tests/web/f06-admin.spec.ts --workers=2` lượt cuối102PASS/2FAIL104ca,50,5s. Hai lỗi fixture weekday exact text đã sửa theo listitem; recheck `npm.cmd run test:web -- tests/web/f07-admin-ui.spec.ts --grep "admin deep link" --workers=2` PASS2/2,3,4s. Không gọi là một lượt full104PASS. UI cases20 và hồi quy84 đã đạt qua các lượt; đã xem screenshots desktop/mobile. Lượt đầu96/100 sửa skiplink oldbuild/menu locator đã xử lý.
- Admin Vite development5185 StrictMode fixture: PASS một restore, protected deep link/no page error/no storage. Fixture không chứng minh server cookie thật. Browser idle dùng đồng hồ giả để kiểm request/state; test tay server-cookie30min vẫn theo checklist, không tuyên bố đã ngồi chờ30phút tay.
- Live cuối `npm.cmd run test:identity-live -- -ApiPort 5081` PASS8/8,47,7s trên DB tạmPostGIS/API/Worker/Mailpit/browser thật, F01→F06. Lượt đầu6/8 vì native emailvalidation; cập nhật kiểm focus/typeMismatch/noHTTP, weakpassword1request400. Lượt giữa6/8 vì requestđăngkýcold>5s; thêm chờHTTP20220s trước kiểmverify và bảncuối8PASS. DB tạm dọn đúng tên, normalwebbuild restore; không đụng DBdevelopment.
- Stub browser chỉ kiểm UI/contract, không chứng minh PostgreSQL/provider thật. Live ở đây kiểm luồng cũ, **không** là kiểm chứng F07 series/payment/concurrency khi chưa code.
- Không migrate DB development, không tự commit/push/deploy. S3/MapTiler provider live không chạy trong UI refresh này.
