# Nghiệm thu mốc 2–5 — ShuttleBook

Ngày đối chiếu code: 2026-10-09. Dùng VS Code terminal **PowerShell** tại `C:\Users\luong\Desktop\CLong`.

Đây là kế hoạch nghiệm thu, **không phải kết quả một lượt test mới**. Mốc 2–5 là các mốc lộ trình sản phẩm, không phải feature F02–F05.

## 1. Trạng thái trước khi test

| Mốc | Phạm vi | Test được ngay? |
|---|---|---|
| 2 | Rủi ro kỹ thuật: auth race, tiền chính xác, quote abuse, quyền, search race, CI | Một phần; search race đã sửa và có browser test. Các rủi ro còn lại cần test/thiết kế bổ sung |
| 3 | Khôi phục phiên Customer/Partner sau F5 và quay lại từ ứng dụng ngân hàng | Đã triển khai theo policy30phút idle/30ngày absolute; xem [nghiệm thu M03](M03-browser-sessions-manual.md) |
| 4 | F08 mời nhân viên và quyền theo cơ sở | DEFERRED theo người dùng09/10; ngoài phạm vi owner MVP đợt này |
| 5 | Nghiệm thu tích hợp MVP owner | Luồng F01–F07 + phiên mới; F08 hoãn không chặn đợt này, mốc2 còn gate riêng |

Customer/Partner mới khôi phục phiên bằng cookieHttpOnly; access giữ memory. Policy người dùng duyệt: idle30phút, absolute30ngày từ login. Admin giữ flow riêng. Cần restart API cũ trước test mới; hướng dẫn memory-only trước ngày09/10 là lịch sử.

**Quy tắc hiện hành:** chọn ô chưa giữ chỗ; lấy báo giá hợp lệ khi đã đăng nhập giữ chỗ 120 giây. Tạo đơn chuyển allocation đó sang booking, hạn thanh toán theo `holdMinutes`. Đã báo chuyển hoặc NEEDS_REVIEW không tự giải phóng theo hạn thanh toán cũ. Mọi buổi cố định được giữ hoặc giải phóng cùng kỳ.

## 2. Chuẩn bị chung

### 2.1. Chạy dịch vụ

Terminal kiểm tra:

```powershell
Set-Location C:\Users\luong\Desktop\CLong
npm.cmd run db:up
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

`db:up` khởi động PostGIS/Mailpit. Health phải HTTP 200 và Healthy. Nếu API chưa chạy, mở terminal API theo bảng dưới. Local DB đã được áp dụng F07QuoteReservations ngày 09/10; không cần reset DB. Nếu checkout khác thiếu migration, dừng API/Worker và chạy `npm.cmd run db:migrate`, rồi khởi động lại.

Mỗi dịch vụ dùng một terminal riêng, tại root dự án:

| Dịch vụ | Lệnh | Chức năng |
|---|---|---|
| API | `$env:Media__Mode = 'Local'` rồi `npm.cmd run dev:api` | Endpoint, xác thực, transaction, upload local; cổng 5080 |
| Worker | `$env:Media__Mode = 'Local'` rồi `npm.cmd run dev:worker` | Expiry, outbox, nhắc SLA |
| Customer | `npm.cmd run dev:customer` | Cổng 5173 |
| Partner | `npm.cmd run dev:partner` | Cổng 5174 |
| Admin | `npm.cmd run dev:admin` | Cổng 5175 |

Nếu đã chạy đủ và đúng phiên bản, giữ nguyên. Không chạy dịch vụ trùng cổng; Ctrl+C trong terminal tương ứng khi cần dừng. `Media__Mode=Local` chỉ là cú pháp file `.env`; gán trong PowerShell phải dùng `$env:Media__Mode = 'Local'`.

### 2.2. Dữ liệu thử

- Customer A và B ACTIVE; hai browser profile riêng. Nhiều tab ẩn danh có thể dùng chung cookie, không mặc định coi là hai tài khoản độc lập.
- Owner A có business ACTIVE, venue PUBLISHED, court ACTIVE, giờ/giá đầy đủ, QR READY. Owner B thuộc business khác. Admin riêng.
- Một sân thử minimum 120 phút, giá đơn giản 80.000đ/giờ; có khung cao điểm 150.000đ/giờ để đối chiếu.
- Ngày tương lai trong 60 ngày; mỗi ca 30 phút. Dùng các giờ/ngày khác nhau cho từng test.
- Ảnh PNG/JPEG/WebP ≤5 MB, ghi “TEST — KHÔNG PHẢI BIÊN LAI THẬT”. Không cần chuyển tiền thật.
- Ghi mã đơn, sân, ngày, giờ, tổng, trạng thái, deadline. Mở F12 → Network → Fetch/XHR, Preserve log. Không chụp/copy Authorization, token hoặc toàn bộ `.env`.

## 3. Mốc 2 — rủi ro kỹ thuật

### M2-01. Kết quả tìm kiếm cũ không trộn vào tìm kiếm mới — test ngay

1. Customer vào Tìm sân, chọn bộ lọc có đủ kết quả để có Tải thêm.
2. F12 → Network → chọn Slow 3G để làm chậm response.
3. Nhấn Tải thêm của truy vấn A; ngay lập tức đổi tên/khu vực/bộ lọc sang truy vấn B và tìm lại.
4. Đợi response A và B kết thúc. Danh sách chỉ có kết quả B, không nhân đôi venue, không append trang A.
5. Lặp khi chuyển giữa tìm tên và nearby; trả Network về No throttling.

Nếu không có Tải thêm hoặc không tái hiện được response về muộn, ghi NOT RUN cho race. Test tự động có delay xác định:

```powershell
npm.cmd run build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Web.ps1 tests/web/customer-distance.spec.ts --workers=2
```

Runner dùng preview riêng. Nếu báo port in use, dừng ba terminal web bằng Ctrl+C rồi chạy; sau đó khởi động lại web. Đây là browser test với API fixtures, không phải geolocation/PostGIS/provider thật. Đợt trước đã PASS 6 executions desktop/mobile, không tính là PASS mới nếu chưa chạy lại.

### M2-02. Đăng xuất cơ bản và logout/refresh đồng thời

**Tay — kiểm hành vi cơ bản:**

1. Đăng nhập A, mở Đơn của tôi và một đơn thuộc A.
2. Đăng xuất, nhấn Back, mở lại URL đơn riêng, thử thao tác cần đăng nhập.
3. Không được nhận thêm dữ liệu riêng hoặc ghi thay đổi bằng phiên vừa logout. Một trang công khai vẫn truy cập được; HTTP 200 của HTML SPA không chứng minh API private cho phép.
4. Lặp với Owner A ở Partner. Đăng nhập mới vẫn hoạt động.

**Tự động — rotation/reuse hiện có:**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --filter FullyQualifiedName~IdentityFlowTests
```

Test này dùng PostGIS thật, DB tạm; không đủ riêng để chứng minh race logout/refresh. Đợt M03 đã bổ sung BrowserSessionTests với family-lock barrier, thử hai thứ tự refresh/logout và token đã consumed; xem kết quả M03. Click nhanh bằng tay không thay test này.

### M2-03. Giá/tổng đúng từng ca

1. Đặt sân 80.000đ/giờ: 4 ca = 160.000đ; 5 ca = 200.000đ; 6 ca = 240.000đ; 7 ca = 280.000đ.
2. Lấy báo giá qua ranh giới thường/cao điểm: nếu hai ca ở 80.000đ/giờ và hai ca ở 150.000đ/giờ, tổng = 230.000đ. Chỉ dùng ví dụ này nếu bảng giá của sân đúng như vậy.
3. Kiểm tổng ở báo giá → tạo đơn → Customer detail → Partner detail → số tiền xác nhận.
4. Sau tạo đơn, Owner sửa giá: đơn cũ giữ snapshot, báo giá mới dùng giá mới. Nếu sửa giá trước create, quote cũ phải yêu cầu xem báo giá mới, không lặng lẽ tạo với giá khác.

Giá thông thường PASS không chứng minh boundary > `Number.MAX_SAFE_INTEGER` (9.007.199.254.740.991). Casual quote còn rủi ro Number; cần chốt cap giá backend hoặc dùng amount string chính xác, rồi test trong DB tạm. Không nhập số cực lớn vào cơ sở đang dùng để nghiệm thu bình thường. Boundary này chưa được chốt PASS.

### M2-04. Giữ chỗ, hết hạn và lạm dụng nhiều sân

1. A chỉ chọn ô, chưa lấy quote; B làm mới lịch: vẫn trống.
2. A lấy quote 5 ca; chưa tạo đơn. B làm mới cùng ngày/sân: đúng 5 ca đã kín; sân/giờ khác vẫn trống.
3. Đợi quá deadline quote 120 giây. B làm mới: các ca A giữ tạm trở lại trống, B lấy quote được.
4. Lặp với cố định, kiểm từng ngày: toàn kỳ giữ và toàn kỳ trả trống.
5. Quote lại cùng sân sau 20–30 giây: deadline gốc không kéo dài; đổi selection hợp lệ trả khung cũ, giữ khung mới theo deadline còn lại.
6. Sau tạo booking, hết quote TTL không giải phóng booking; trước báo chuyển, deadline áp dụng theo sân.

Hiện chỉ giới hạn một hold/customer/court, chưa cap toàn tài khoản. Thử A lấy quote ở vài sân để ghi hành vi/TTL, nhưng không gọi việc được giữ nhiều sân là FAIL khi chưa có quota được duyệt. Test quota/cooldown/NAT/nhiều API chỉ có tiêu chí PASS sau khi thiết kế chính sách.

### M2-05. Quyền dữ liệu và quyền sau khi bị thu hồi

1. Ghi URL đơn của Customer A. Customer B mở URL đó trong profile B: không được đọc chi tiết/biên lai hay báo chuyển thay A.
2. Owner B mở URL đơn của business A: không được xem/confirm/reject đơn đó.
3. Guest mở lại URL private: phải đăng nhập, không nhận dữ liệu đơn.
4. Nếu xem biên lai bằng URL ký tạm, dùng endpoint đọc private của đơn để thử quyền; URL ký hết hạn và quyền mới là hai cơ chế khác nhau, không mặc định URL đã cấp bị thu hồi tức thì.
5. Khi bị từ chối, UI không tiếp tục hiển thị cache dữ liệu của scope cũ; ảnh đã tải sẵn/screenshot không thể bị máy chủ thu hồi.

Thu hồi membership/suspend trong lúc request đang chờ khóa cần test PostGIS do agent thực hiện, không tự đổi SQL tài khoản đang dùng. Suite FixedSeriesTests và QuoteReservationTests đã có một số ca fresh authorization; không đồng nghĩa mọi endpoint/race đã được kiểm đủ.

### M2-06. CI trên GitHub

Sau khi có bản sửa workflow và bạn push theo kế hoạch: GitHub → Actions → workflow Verify foundation → mở job web/backend. Cả hai phải thực sự chạy build/test và xanh; kiểm test count, log, artifact lỗi. Local PASS không thay hosted PASS. Workflow hiện dùng ubuntu-latest trong khi npm scripts gọi powershell.exe; đây là việc cần sửa trước nghiệm thu CI, không phải chỉ nhấn rerun để chốt PASS.

**Gate mốc 2:** các rủi ro auth/tiền/quota đã có quyết định, code và test tương ứng; CI hosted chạy được; không còn lỗi nghiêm trọng chưa xử lý. Hiện chưa đủ để chốt cả mốc.

## 4. Mốc 3 — phiên Customer/Partner

Đã triển khai theo policy người dùng duyệt09/10. Dùng [hướng dẫn M03 mới](M03-browser-sessions-manual.md) để chạy tay; bảng dưới là acceptance. Idle30phút, absolute30ngày; polling/restore không gia hạn idle.

| ID | Thao tác nghiệm thu sau triển khai | Kết quả cần có |
|---|---|---|
| M3-01 | Đăng nhập, mở list/detail private, F5 và mở URL trực tiếp khi còn hạn | Restore phiên trước gọi API private; không hiện login sai rồi nhảy lại, không vòng lặp |
| M3-02 | Lấy quote, F5 khi quote còn hạn; quay lại selection | Không gia hạn giữ chỗ, không sinh đơn thứ hai; nếu form chưa có recovery thì thông báo lấy lại quote theo contract |
| M3-03 | Tạo đơn, ghi mã/tiền, chuyển sang app ngân hàng rồi quay lại | Mở đúng đơn/QR/deadline; báo chuyển một lần, không create lại |
| M3-04 | Không thao tác đến quá idle timeout được duyệt, rồi gọi thao tác private | Yêu cầu login; sau login quay lại URL nội bộ phù hợp |
| M3-05 | Có thao tác đều nhưng đã quá absolute lifetime | Vẫn hết phiên theo policy; thao tác không kéo dài vô hạn |
| M3-06 | Mở hai tab cùng profile; logout một tab, tab kia gọi API private | Backend từ chối phiên cũ; UI xóa dữ liệu riêng theo cơ chế đồng bộ đã thiết kế |
| M3-07 | Suspend/revoke trong lúc còn session; thử refresh và thao tác ghi | Không phục hồi quyền bằng refresh/replay |
| M3-08 | Sau logout, Back/F5/đóng mở lại browser | Không restore phiên đã logout; đóng browser có giữ phiên hay không theo policy được duyệt |
| M3-09 | Network Offline khi restore, rồi Online/thử lại | Hiển thị lỗi kết nối phù hợp, không tạo booking trùng hoặc vòng refresh |
| M3-10 | returnTo trỏ URL nội bộ và URL ngoài hệ thống | Cho URL nội bộ hợp lệ; từ chối redirect ngoài |

Lặp cho Customer và Partner; kiểm desktop/mobile. Boundary idle/absolute/reuse/race kiểm clock/PostGIS tự động, không đổi đồng hồ Windows. Kết quả mới xem M03-browser-sessions-results.md; không suy PASS tay từ suite.

## 5. Mốc 4 — F08 tạm hoãn; checklist lưu cho sau

Chưa có UI/API invitation và quyền staff. Tên button/endpoint/error cụ thể phải lấy từ contract F08 sau khi được duyệt. Chuẩn bị Owner A hai venue A1/A2, Owner B venue B1, ba nhân viên thử với quyền khác nhau.

Ma trận kỳ vọng cần chốt theo thiết kế:

| Actor/quyền thử | Xem đơn A1 | Xem proof A1 | Xác nhận A1 | Đổi giá/QR A1 | A2/B1 |
|---|---|---|---|---|---|
| Staff chỉ xem đơn A1 | Có | Theo quyền proof riêng | Không | Không | Không |
| Staff xác nhận A1 | Có | Theo quyền đã cấp | Có | Không nếu chưa cấp | Không |
| Staff cấu hình A1 | Theo quyền đã cấp | Theo quyền đã cấp | Không nếu chưa cấp | Có trong phạm vi được cấp | Không |
| Owner A | Theo quyền owner | Theo quyền owner | Có | Có | A2 có, B1 không |
| Admin | Theo quyền quản trị đã thiết kế | Theo quyền đã thiết kế | Không tự có payment.confirm | Theo contract | Theo contract |

Các ca bắt buộc:

1. Owner mời nhân viên đúng venue/quyền; chỉ nhận lời mời hợp lệ mới có membership. Không tự join business bằng ID.
2. Nhân viên đúng tài khoản nhận; lời mời đã dùng không tạo membership lần hai. Hai accept đồng thời chỉ có một hiệu lực.
3. Invite expired/revoked bị từ chối; accept cạnh tranh revoke có một kết quả nhất quán.
4. Staff chỉ xem không thấy nút xác nhận/đổi giá. Gọi API trực tiếp vẫn bị chặn; ẩn nút không đủ để PASS authorization.
5. Staff có confirm xác nhận đúng số tiền snapshot; không có quyền configure thì không sửa giá/QR.
6. Đổi venueId/bookingId sang A2/B1 không vượt scope. Chuyển scope không còn dữ liệu private cũ.
7. Owner thu hồi quyền khi staff đang mở đơn. Yêu cầu mới bị chặn; request chờ khóa phải kiểm quyền fresh trước commit. Agent test concurrency trên PostGIS.
8. Notification chỉ đến recipient có quyền phù hợp ở thời điểm xử lý theo contract; revoke không cho đọc proof từ API nữa.
9. Audit ghi actor/action/scope/thời điểm; không mất quyền owner gốc và không cho staff tự nâng quyền.
10. Mobile/keyboard: mời, nhận, chọn scope, trạng thái lỗi/rỗng và quyết định đọc được, thao tác được.

Người dùng quyết định **DEFERRED** ngày09/10, ngoài phạm vi nghiệm thu owner đợt này. Không coi F08 DONE hoặc yêu cầu tìm nút chưa tồn tại.

## 6. Mốc 5 — tích hợp F01–F07 và gate MVP

Các luồng dưới đây chạy được ngay trên local. Dùng đơn khác nhau cho confirm/review/reject/expiry.

### M5-01. Guest → đăng nhập → vãng lai → xác nhận

1. Guest tìm venue, xem khoảng cách nếu đã cấp vị trí; không map Customer. Login không sidebar trái.
2. Chọn ngày và 5 ca liên tiếp 150 phút trên sân minimum120. Guest chưa giữ chỗ; đăng nhập để lấy quote.
3. Kiểm sân/ngày/ca/tổng, quote countdown; B làm mới lịch thấy ca giữ tạm.
4. Create trước deadline. Ghi mã đơn, QR/giá snapshot, hạn thanh toán.
5. Báo chuyển với ảnh thử, không cần mã giao dịch; trạng thái chờ xác nhận.
6. Partner nhận notification, đọc proof private, xác nhận đúng tổng. Customer thấy đã xác nhận; payment PAID/booking CONFIRMED qua test backend. Lịch vẫn kín, một đơn/một payment.
7. Không có nút Customer tự hủy/đổi lịch hoặc check-in/check-out. 4/5/6/7 ca đều hợp lệ nếu liên tiếp và đủ minimum.

### M5-02. Cố định thành công

1. Chọn cùng sân/thứ/giờ hàng tuần, mỗi buổi ít nhất max(120 phút, minimum sân), kỳ ít nhất một tháng lịch; tất cả trong horizon60ngày/max12buổi.
2. Quote: xem từng buổi/tiền/tổng; B kiểm từng ngày thấy đã kín.
3. Create: một nhóm đơn, một QR/payment, tổng bằng tổng các buổi; không tạo payment riêng từng occurrence.
4. Báo chuyển/Owner confirm một lần cho cả kỳ. Mọi buổi vẫn giữ, không cần xác nhận từng ngày.

### M5-03. Cố định xung đột và atomic rollback

1. **Trước khi A lấy quote**, Owner tạo maintenance hoặc B tạo booking ở một buổi giữa kỳ.
2. A quote kỳ có buổi đó: báo xung đột, không nhận hold hợp lệ toàn kỳ.
3. B xem những ngày còn lại: không bị A giữ sót; danh sách A/Owner không có booking tạo dở.
4. Khi A đã có quote còn hạn giữ kỳ, B/maintenance không được chen vào ca đã giữ; test hai quote đồng thời và constraint cần suite PostGIS.

### M5-04. Hai loại hết hạn

1. A quote, không create; chờ quá120s: toàn bộ hold quote trả trống.
2. A create đơn mới, không report; chờ hạn thanh toán theo sân: Worker/API giải phóng đúng ca/cả kỳ, không còn đơn giữ sót.
3. A create đơn khác, report trước hạn; chờ qua hạn: vẫn giữ/chờ Owner, không expire. Không cần gửi tiền thật.
4. Có thể dừng Worker để test quote-expired reads; khởi động lại ngay sau ca. Đơn chưa report và outbox cần kiểm Worker hoạt động.

### M5-05. Bổ sung, từ chối, notification

1. Đơn đã report → Owner yêu cầu bổ sung có lý do → Customer thấy yêu cầu và gửi ảnh/note bổ sung → Owner confirm. Hold giữ suốt thời gian NEEDS_REVIEW.
2. Đơn khác → Owner từ chối cuối cùng có lý do → Customer thấy kết quả; toàn kỳ cố định trả trống.
3. Một đơn report chưa được xử lý quá30phút: Owner/Admin có nhắc SLA một lần. Supplement không reset mốc đầu; Admin không tự confirm payment.
4. Nhấn Làm mới/thao tác retry không tạo thông báo logic/decision/payment lần hai. Outbox rollback/retry cần suite DB, UI không đủ chứng minh exactly-once effect.

### M5-06. Snapshot, maintenance, retry, UI

1. Owner sửa giá/QR sau create: đơn cũ giữ giá/QR snapshot; đơn mới dùng cấu hình hợp lệ hiện hành.
2. Maintenance ca trống thành công; overlap quote/booking bị chặn. Gỡ maintenance thử trả lịch đúng, không mở ca có booking.
3. Mất response create sau server commit phải retry cùng intent/key/body trả cùng đơn; không tự dựng request key mới. Dùng test UiCasualRecoveryTests cho lỗi mạng được điều khiển; chuyển Offline trước request không chứng minh server đã commit.
4. Customer B/Owner B không xem/ghi đơn hoặc proof ngoài scope; không chỉ kiểm nút ẩn.
5. Bảng mỗi sân một hàng, giờ ở ranh giới ca30phút, bỏ chọn một ca không xóa toàn selection; cuộn ngang bảng không làm tràn toàn trang.
6. List lịch sử/thông báo nhiều trang: Tải thêm → đợi polling/Làm mới/đánh dấu đọc vẫn giữ cửa sổ trang, không duplicate; đổi scope xóa dữ liệu cũ.
7. Desktop/mobile375px/zoom200%/Tab/Enter, loading/error/empty, không bị nút che khuất. Customer/Partner F5 còn hạn restore đúng URL/dữ liệu; hết idle/absolute phải login.

### 6.1. Gate tự động cuối

Chạy từng lệnh, kiểm kết quả trước lệnh kế tiếp; không tiếp tục ghi PASS khi command lỗi. DB cần Docker đang chạy và role local có quyền tạo DB tạm.

```powershell
npm.cmd run typecheck
npm.cmd run build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:api
npm.cmd run test:db
```

Web/live: dừng ba web dev bằng Ctrl+C để dành cổng preview, giữ API5080 nếu muốn; live dùng5081:

```powershell
npm.cmd run test:web
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081
```

`test:web` chủ yếu dùng fixtures và có ca live SKIP theo cấu hình; phải chạy live riêng. Live runner tạo API/Worker/DB tạm và cleanup, phục hồi build web; không reset development DB. Đọc PASS/FAIL/SKIP thực tế, không đòi count cố định vì code/test có thể thay đổi. “No test matches” là NOT RUN. Thiếu dependencies/browser phải setup theo docs/setup.md, không tắt/skip ca lỗi để có kết quả xanh.

### 6.2. Khi nào được chốt

- **Core local F01–F07:** luồng hiện có và gate tương ứng đạt, lưu bằng chứng. F07 đã nghiệm thu DONE local, không buộc làm lại tất cả nếu không thay phần đó.
- **MVP owner theo scope09/10:** F08 hoãn không chặn đợt này; coreF01–F07 vàM03 phải đạt, các risk/gate mốc2 còn mở phải ghi rõ. F08 là việc tương lai, không gọi DONE.
- **Production:** S3 thật/CI hosted/backup restore/load/security/monitoring/pilot có gate riêng. Manual local không chứng minh production đạt100%.

## 7. Biên bản test

| ID | Trạng thái | Dữ liệu/lệnh | Kết quả thực tế | Bằng chứng/việc tiếp theo |
|---|---|---|---|---|
| M2-01 | NOT RUN | Truy vấn A/B hoặc command | | |
| M2-02 race | NOT RUN | Cần test barrier PostGIS | | |
| M3 | PASS theo xác nhận người dùng 2026-10-09; DONE local | Có restore/policy mới; build/typecheck/API/DB người dùng báo PASS | Không cung cấp log/count từng ca nên không tự dựng biên bản | UI Partner tách trang chi tiết sau đó nghiệm thu riêng |
| M4 | DEFERRED | F08 hoãn09/10 | | Ngoài scope đợt owner |
| M5-01…06 | NOT RUN trong hướng dẫn này | Mã đơn/sân/ngày | | |

PASS chỉ sau khi làm và thấy đúng; FAIL ghi steps; NOT RUN là chưa chạy; BLOCKED ghi điều kiện chặn cụ thể. Gửi ảnh UI/HTTP status/error code và mã đơn thử, không gửi token/secret. Đây không sửa trạng thái feature hay tự tạo commit/push.
