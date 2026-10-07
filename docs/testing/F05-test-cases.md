# F05 — testcase và bằng chứng

## Điều chỉnh ca vãng lai theo nghiệm thu F06 — 2026-10-07

- Theo yêu cầu mới, duration đạt minimum và các ca30liên tiếp; không yêu cầu tổng duration chia hết bookingBlockMinutes. Minimum120/block60 nhận4/5/6/7ca, chặn3; minimum90/block90 nhận3/4/5/6, không đổi cấu hình/snapshot hoặc F07.
- **PASS7/7 CasualBookingTests** trong lượtDB35/35 (Casual7/Payment27/Identity1) trênPostGIS tạm,10m36. Theory `Casual_half_hour_extensions_after_minimum_quote_create_replay_and_preserve_policy`2cases kiểm quoteprice/từngca/grid/min/opening/overlap/quotechange/create/replay/persistedsnapshot/allocation.
- **PASS browser62/62**25,5s ở suiteF04/F05/F06, có4ca regression chọn4/5/6/7 và5ca handoff qua login đúng17:00–19:30. Lượt đầu có lỗi locator/nearbyfixture ngoàicasualduration đã sửa/retest, không claim thao tác tay usercho từngca. F05status chưa tự đổiDONE.

Kết quả local 2026-10-07: triển khai/kiểm thử tự động đạt; nghiệm thu tay của người dùng chưa chạy. PostgreSQL/PostGIS thật `127.0.0.1:54329` trên DB tạm. Fixture published court/pricing/QR và hai customer ACTIVE; không migrate/drop DB development. Bảng dưới chỉ ghi PASS cho hành vi đã chạy.

| ID | Bước/mục tiêu | Mong đợi | Loại | Kết quả |
|---|---|---|---|---|
| T01 | Quote 4 ca nhiều mức giá; block/min30/60/90 | Tổng từng ca, đúng UTC/policy, không giữ slot | API+DB | PASS — F05 facts 1/3 |
| T02 | Sai grid, past/horizon/giờ mở/DST, thiếu giá/field lạ, tiền vượt giới hạn | 400/409 theo contract; offset Chatham UTC phút15 vẫn tạo được, DST gap/ambiguity bị chặn | API+DB | PASS — facts 1/3/5 |
| T03 | Create rồi retry cùng key, body đổi, quote expired/giá hoặc hold đổi | Một booking/payment/allocation; replay hoặc 409 đúng | API+DB | PASS — facts 1/3/4 |
| T04 | 20 requests overlap qua 2 API host; 8 cùng key song song | Đúng 1 aggregate mỗi ý định; 19 conflict, không đơn nửa vời | PostGIS | PASS — fact 2 |
| T05 | Maintenance giao/chạm biên; booking sân khác cùng giờ | Giao bị chặn, chạm biên/sân khác được | DB+API | PASS — facts 2/3 |
| T06 | Guest create; operator; suspended customer/venue; customer khác đọc detail/QR | 401/403/404, list/quote không rò account/object key | API+DB | PASS — facts 1/5 |
| T07 | Đổi giá/QR sau tạo; revision QR duyệt, F5 đơn cũ | Snapshot bất biến, upload snapshot đúng | API+DB+Live | PASS — fact 1 và live desktop/mobile |
| T08 | 4 expiry processors đồng thời; report đang lock/commit; CONFIRMED | Release đúng một lần; reported/confirmed giữ RESERVED, replay cùng đơn EXPIRED | DB+Worker service | PASS — fact 4, chạy lại sau assertion CONFIRMED |
| T09 | Fresh/repeat/upgrade từ F04 có dữ liệu; SQL sai court/allocation/venue/grid | Migration giữ dữ liệu; FK23503/check23514 từ chối | DB | PASS — baseline regression + fixture F05/facts3/4 |
| T10 | Guest→login→quote→booking→QR/list/F5; expired/changed quote, network retry cùng key | Desktop/mobile dùng được; không cancel/báo chuyển; giữ court/date/time khi rời lịch | Browser mock | PASS — 4 F05 cases + 2 F04 handoff cases |
| T11 | Guest đăng ký Mailpit→login→quote→booking thật→QR→revision QR→F5; notification outbox | Đúng đơn/giá400.000đ/QR snapshot; Worker tạo thông báo cho đúng khách | API+PostGIS+Worker+Browser thật | PASS — 2 F02–F05 live cases desktop/mobile |
| T12 | F01–F04 regression/build/typecheck/diff | PASS | Regression | PASS — lệnh/bằng chứng dưới |
| T13 | S3 provider thật | QR private/signed GET | S3 live | NOT RUN — hoãn theo người dùng |
| T14 | Người dùng chạy checklist F05 bằng tay, gồm chờ hold5 phút thật | Đạt UI/happy path/expiry theo hướng dẫn | Manual | NOT RUN — chờ nghiệm thu; service expiry đã PASS T08 |

## Lệnh và bằng chứng runtime

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore`: **PASS**, 0 warning/error.
- `npm.cmd run test:api -- --no-build`: **78/78 PASS** bản cuối.
- `npm.cmd run test:db -- --no-build`: **30/30 PASS**, 10 phút14 giây ở mốc đầu (4 facts F05). Sau bổ sung DST/offset/FK/overflow: `npm.cmd run test:db -- --no-build --filter FullyQualifiedName~CasualBookingTests`: **5/5 PASS**, 1 phút16 giây. Sau thêm assertion CONFIRMED, build và `--filter FullyQualifiedName~Expiry_multiple_workers`: **1/1 PASS**, 19 giây. Không cộng các lượt này thành một suite 36 ca.
- `npm.cmd run typecheck`: **PASS** toàn bộ workspace. `npm.cmd run build`: **PASS**; live script build lại web trước mỗi lượt với API local.
- `npm.cmd run test:web -- --workers=2`: **46 PASS, 8 SKIP** có chủ đích (live chạy riêng), bản cuối 19,4 giây. Đã xem screenshot booking desktop/mobile; bảng F04 và handoff được regression.
- `npm.cmd run test:identity-live`: **8/8 PASS**, bản cuối 31,3 giây; PostGIS/Mailpit/API/Worker/3 portals thật trên DB tạm. F02–F05 live kiểm tra notification BOOKING_CREATED sau Worker xử lý. Database tạm được script xác minh tên và dọn; không giữ token/password trong báo cáo.
- `git diff --check`: **PASS** (chỉ có thông báo chuyển đổi LF/CRLF theo cấu hình Git).

## Review, sự cố đã sửa và giới hạn

Review tuần tự, chưa có reviewer độc lập. Đã kiểm tra auth/customer scope, lock order business→venue→court, advisory idempotency, half-open exclusion, snapshot không lưu signed URL, expiry lock booking/skip reported, QR private và migration chỉ thêm schema. Không phát hiện lỗi nghiêm trọng còn mở.

- Test browser đầu phát hiện protected-route redirect trước khi App lắng nghe popstate; đổi listener sang layout effect và ngăn redirect StrictMode lặp.
- Live đầu **FAIL 2/8** vì listener F04 xóa courtId khỏi URL review khi chuyển trang. Đã guard listener chỉ xử lý chính route venue, thêm regression giữ court/date/time, chạy lại live **PASS 8/8**.
- Một lượt full web 10 workers **FAIL 4**, Chromium/test worker crash và timeout; bản cuối dùng 2 workers **PASS 46/46**. Build đồng thời với DB tests từng **FAIL** vì DLL bị khóa trên Windows; build tuần tự đã **PASS**.
- Expiry T08 dùng service Worker với thời gian truyền vào trên DB thật, không chờ 20 phút đồng hồ; không coi đó là nghiệm thu tay Worker host elapsed-time. Thông báo BOOKING_CREATED đã kiểm tra qua Worker host thật; UI báo chuyển/xác nhận và notification F06 chưa triển khai. S3 provider thật/production/CI remote **NOT RUN**.
- Hướng dẫn bàn giao: [F05 manual acceptance](F05-manual-acceptance.md). F05 giữ IN_PROGRESS chờ người dùng nghiệm thu tay trước khi yêu cầu chuyển DONE.
