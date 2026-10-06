# F04 — testcase và bằng chứng

Trạng thái: **IN_PROGRESS** (2026-10-06). Dùng PostgreSQL/PostGIS thật trên database tạm, không migrate/drop DB development. Chưa có bucket/credentials S3 thử nghiệm nên phần provider thật **NOT RUN**; không ghi key/token/URL ký vào log.

| ID | Mục tiêu / dữ liệu / bước | Kết quả mong đợi | Loại | Kết quả |
|---|---|---|---|---|
| F04-T01 | Seed business ACTIVE và các venue PUBLISHED/DRAFT/PENDING/SUSPENDED, court ACTIVE/INACTIVE; guest gọi list/detail | Chỉ venue có business ACTIVE, published, court ACTIVE được trả; draft/inactive 404 detail | API+DB | PASS published/draft/business suspended/court inactive; NOT RUN pending riêng |
| F04-T02 | Seed 3 venue ở khoảng cách khác nhau và ngoài radius; gọi nearby, phân trang | `ST_DWithin` lọc theo mét, `ST_Distance` tăng dần, tie-break ổn định, không lấy ngoài radius | PostGIS+API | PASS thứ tự/2 trang/ngoài radius; NOT RUN tie bằng khoảng cách |
| F04-T03 | Tìm q theo tên/địa chỉ, nhiều trang; truyền lat/lng sai, radius, limit, cursor, query lạ/trùng | Search đúng; giá trị sai trả 400; không lưu vị trí khách | API+DB | PASS tên/lat thiếu-sai/limit/cursor/query lạ; NOT RUN đủ ma trận validation |
| F04-T04 | Detail/availability với venue unpublished, court khác venue, court inactive, không auth | 404; không rò bank/QR/business contact/owner data trong JSON | API+DB | PASS draft detail và private contact; NOT RUN court scope đầy đủ |
| F04-T05 | Hai court giờ mở khác nhau, chọn hôm nay/ngày tương lai theo Asia/Ho_Chi_Minh; ca sát giờ đóng | Sinh ca 30 phút đúng `[open,close)`, UTC đúng; ô ngoài giờ chỉ do UI tô | API+DB | PASS ngày tương lai/hai sân/UTC/quá khứ 400; NOT RUN hôm nay riêng |
| F04-T06 | Giá base và override khác ngày/khung giờ/priority; thiếu giá một ca | Giá VND per slot đúng rule cao nhất; thiếu giá NO_PRICE, không chọn | API+DB | PASS |
| F04-T07 | RESERVED MAINTENANCE/BOOKING và RELEASED; khoảng chạm biên `[start,end)`; cancel maintenance | Mọi RESERVED giao ca thành RESERVED; RELEASED/biên chạm không khóa; sau cancel đọc lại AVAILABLE | API+DB | PASS maintenance/booking allocation giả lập trong DB/RELEASED/biên/cancel; F05 booking flow riêng chưa có |
| F04-T08 | Policy block 30/60/90 và min; chọn ca liên tiếp/không liên tiếp, hai sân | UI chỉ cho selection một sân, thời lượng hợp lệ, tổng giá tham khảo đúng | Browser | PASS block 30/60 và tổng; NOT RUN block 90 |
| F04-T09 | Public image với READY VENUE_IMAGE, PENDING, QR, ảnh venue draft; GET trực tiếp object không ký | Chỉ ảnh venue published/READY hiển thị; QR/draft 404; bucket direct public deny | API+S3 | PASS local READY/QR; NOT RUN S3 direct/PENDING |
| F04-T10 | Presigned PUT F02 trên bucket test: đúng/sai checksum, size/type, complete lặp, owner khác, signed GET expiry, CORS từ partner/customer | READY chỉ với byte hợp lệ và đúng scope; bucket private; signed GET hoạt động/expire | S3 live | NOT RUN |
| F04-T11 | Customer desktop/mobile: geolocation cho/từ chối, text fallback, MapTiler map lỗi, list/detail/back, ngày, grid, empty/loading/retry/poll | Luồng dùng được, map lỗi không chặn list; status có nhãn, responsive, không có request booking | Browser mock + live DB | PASS text/grant/deny/grid/back/desktop/mobile/live; NOT RUN MapTiler provider live |
| F04-T12 | Xem lịch rồi owner tạo/cancel bảo trì; đọc lại sau focus/poll | Grid đổi theo DB, không hứa giữ chỗ; không có side effect ghi từ GET | Browser+PostGIS | PASS nút refresh + DB thật; NOT RUN chờ poll/focus có thay đổi |
| F04-T13 | Migration/schema kiểm tra fresh/repeat/upgrade từ F03 có dữ liệu nếu có migration F04 | Không mất dữ liệu F02/F03, constraint còn nguyên | DB | Không áp dụng: F04 không thêm migration |
| F04-T14 | Chạy lại identity customer và F02/F03 regression liên quan | Register/login/refresh và partner/admin flow không hồi quy | API+browser+DB | PASS API/web/live/full DB 26/26 sau cập nhật assertion F02 |
| F04-T15 | Venue có 2–7 sân; mở lịch desktop/mobile | Mỗi court ACTIVE là một hàng, không có dropdown Xem sân; số hàng khớp response | Browser | NOT RUN — thay đổi 2026-10-07 |
| F04-T16 | Nhìn trục giờ 17:00–18:00 và ô giá 30 phút, gồm mốc kết thúc cuối cùng | 17:00 và 17:30 ở hai biên ô đầu, 17:30 và 18:00 ở hai biên ô sau; giá nằm trong ô, cuộn mobile vẫn đọc được | Browser + visual review | NOT RUN — thay đổi 2026-10-07 |
| F04-T17 | Chọn hai ô liên tiếp, bấm lại từng ô; chọn ba ô rồi bấm ô giữa | Chỉ ô vừa bấm bị bỏ ở biên, ô còn lại/tổng giá được giữ; giữa ba ô giữ một dải liên tiếp, không tạo hai dải rời | Browser | NOT RUN — thay đổi 2026-10-07 |

Sau mỗi lượt ghi ngày, môi trường, lệnh và PASS/FAIL/NOT RUN/BLOCKED cùng lý do. Mock UI không chứng minh PostGIS hoặc S3 provider; code review không chứng minh runtime.

## Bằng chứng 2026-10-06

- **PASS:** `dotnet build backend/ShuttleBook.slnx --no-restore` (0 warning/error); `npm.cmd run typecheck`; `npm.cmd run build`; `npm.cmd run test:api` 78/78; `npm.cmd run test:web` **40 PASS, 8 SKIP** live có chủ đích; `npm.cmd run test:web -- tests/web/f04-discovery.spec.ts` 6/6 PASS. `npm.cmd run test:identity-live` 8/8 trên database PostGIS tạm, gồm customer F04 desktop/mobile sau owner bảo trì và hủy bảo trì. Script live xóa đúng database tạm sau khi chạy.
- **PASS PostGIS:** `npm.cmd run test:db -- --no-build` **26/26** sau khi sửa hai kỳ vọng F02 cũ về route công khai, và `npm.cmd run test:db -- --filter FullyQualifiedName~PublicVenuesTests` **2/2** sau khi bổ sung kiểm tra trạng thái public/radius/validation/BOOKING allocation. Mỗi ca tạo/xóa database tạm; không migrate/drop database development.
- **Lịch sử đã xử lý:** DB cổng 54329 từng tắt nên một lượt full suite không nối được; người dùng đã mở Docker và lượt sau PASS. Một lượt build chồng gây lỗi reference assembly; build tuần tự sau đó PASS. Kỳ vọng F02 cũ coi `/api/v1/venues` là 404 trước F04 đã được thay bằng kiểm tra rỗng/trả venue published.
- **NOT RUN:** bucket S3/IAM/CORS/signed GET và provider MapTiler thật vì chưa có bucket thử nghiệm; kiểm thử MapTiler ở browser mock chỉ là UI. Các nhánh ghi `NOT RUN` trong bảng là phần chưa có bằng chứng tương ứng.
