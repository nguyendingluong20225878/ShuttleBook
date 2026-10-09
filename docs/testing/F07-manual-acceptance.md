# Nghiệm thu F07 — lịch cố định và UI các vai trò

> Chốt 2026-10-08: người dùng đã nghiệm thu F07, bao gồm thay đổi quote giữ tạm/Customer UI; prompt đánh giá sản phẩm đã hoàn tất. **F07 DONE local**. Các câu chờ nghiệm thu hoặc IN_PROGRESS bên dưới thuộc mốc lịch sử; không tự lập biên bản PASS từng ca tay. S3/provider/production vẫn có gate riêng NOT RUN; xem progress và báo cáo sản phẩm.


Thực hiện ở VS Code, terminal **PowerShell**, thư mục `C:\Users\luong\Desktop\CLong`. Checklist này dùng dữ liệu local do bạn tạo. Không kiểm thử bằng việc sửa trực tiếp trạng thái/payment trong DB. S3 thật tiếp tục hoãn; ảnh/QR/biên lai dùng kho private Local.

## 1. Chuẩn bị và khởi động

1. Bật Docker Desktop. Kiểm tra PostgreSQL/PostGIS local đang chạy:

```powershell
Test-NetConnection 127.0.0.1 -Port 54329
```

`TcpTestSucceeded: True` nghĩa là kết nối được cổng DB. Nếu cổng `.env` của bạn khác, dùng cổng đó. Có thể chạy `npm.cmd run db:up` để khởi động các container được dự án cấu hình.

2. Dừng API/Worker cũ bằng Ctrl+C trong đúng terminal. **Bạn chủ động chạy migration F07**:

```powershell
npm.cmd run db:migrate
```

Mục đích: thêm bảng series/quote, cột liên kết nhóm và constraints. Migration giữ booking vãng lai/payment/evidence cũ; không tạo dữ liệu mẫu hoặc xóa lịch cũ. Nếu DB chứa dữ liệu cần giữ, dùng quy trình backup của bạn trước khi nâng cấp. Agent chỉ kiểm tra migration trên database test tạm, không tự nâng DB development.

3. Mỗi tiến trình chạy một terminal riêng. Đặt biến môi trường bằng cú pháp PowerShell:

```powershell
$env:Media__Mode = 'Local'
npm.cmd run dev:api
```

```powershell
$env:Media__Mode = 'Local'
npm.cmd run dev:worker
```

```powershell
npm.cmd run dev:customer
```

```powershell
npm.cmd run dev:partner
```

```powershell
npm.cmd run dev:admin
```

4. Terminal kiểm tra riêng:

```powershell
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

Cả hai phải HTTP200 Healthy. Live200 nhưng ready503 thường là DB/config/schema chưa sẵn sàng. Không kết nối5080: API chưa chạy đúng cổng. Không đặt `Media__Mode=Local` như một lệnh, PowerShell cần `$env:...`.

## 2. Dữ liệu để nghiệm thu

- Owner ACTIVE, có business/venue đã publish và ít nhất một court ACTIVE; customer ACTIVE; Admin có quyền riêng. Dùng ba trình duyệt/tab hoặc cửa sổ riêng theo role.
- Venue timezone `Asia/Ho_Chi_Minh`, ví dụ sân mở05:00–22:00.
- Bảng giá phủ đủ ít nhất một tháng tương lai và thứ được chọn, ví dụ18:00–20:00, 100.000đ/ca30phút. Mỗi buổi120phút có4ca, tiền400.000đ. Nếu có5buổi, tổng2.000.000đ; số buổi thực tế phụ thuộc lịch, không luôn bằng4.
- QR/tài khoản owner READY, đã cấu hình. Có thể dùng ảnh QR test, không cần chuyển tiền thật; nút báo chuyển/xác nhận chỉ là thao tác test local.
- Chọn ngày bắt đầu từ ngày mai, kết thúc bằng ngày bắt đầu cộng **một tháng lịch** và vẫn nằm trong60ngày tính theo timezone venue. Kiểm khoảng này chưa có booking/maintenance từ nghiệm thu trước.
- Nếu muốn thử hết hạn nhanh, đặt holdMinutes của sân thành5 bằng form cấu hình hợp lệ **trước khi lấy quote**. Booking cũ giữ snapshot hold riêng.

## 3. Luồng cố định thành công — Customer5173 → Partner5174

### C01: tìm sân và chọn lịch chung

1. Customer mở `/venues`, tìm cơ sở và vào lịch. Bảng hiện tất cả sân theo hàng, các cột30phút đủ rộng và cuộn trong bảng trên điện thoại.
2. Chọn **Cố định**. Chọn một sân, ít nhất4ca liên tiếp (120phút) hoặc nhiều hơn nếu minimum sân lớn hơn120. Ví dụ18:00–20:00.
3. Tiếp tục. Chưa login phải đăng nhập rồi quay đúng bước đặt; không tự tạo booking.

Đang làm gì: dùng cùng lựa chọn court/khung giờ với vãng lai, sau đó mở form lặp tuần. Nearby vẫn chỉ tìm cơ sở theo vị trí, không quyết định lịch trống toàn kỳ.

### C02: nhập kỳ và xem báo giá

1. Chọn thứ trong tuần, giờ bắt đầu, thời lượng, ngày bắt đầu/ngày kết thúc. Nhấn xem báo giá.
2. Đối chiếu **từng ngày/buổi, giờ, tiền buổi, tổng tiền cả kỳ, số buổi, hạn quote2phút**.
3. Tổng phải bằng tổng các buổi, tiền mỗi buổi bằng tổng các ca30phút. Nếu bảng giá khác theo ngày thì từng buổi có thể khác tiền.
4. Quote hợp lệ đã giữ chỗ tạm: mọi buổi hiển thị RESERVED/Đã kín với khách khác đến expiresAt. Chưa có booking/payment lúc này. Hết120s không tiếp tục thì mọi buổi trở về trống.

Đang làm gì: backend sinh lịch tuần đúng tháng lịch, kiểm mọi buổi/giờ mở/giá/QR; UI hiển thị báo giá và countdown; backend giữ toàn kỳ tạm trong PostgreSQL.

### C03: tạo đơn toàn kỳ

1. Trước hạn quote, nhấn tạo đơn một lần. Chờ kết quả, không tự F5 lúc request đang chạy.
2. Chi tiết phải có **một mã lịch cố định**, tất cả buổi và **một QR/một tổng tiền cả kỳ**. Nội dung chuyển là mã series.
3. Vào Đơn của tôi: series chỉ có **một dòng**, không5đơn thanh toán riêng.
4. Mở lịch public của từng ngày thuộc series: mọi khung18:00–20:00 trên cùng sân đã bị giữ, sân khác/ngày khác không bị khóa.

Đang làm gì: transaction tạo series, các booking/allocation thực và một payment. Một xung đột sẽ rollback cả kỳ, không để vài buổi thành công.

### C04: báo chuyển và owner xác nhận

1. Customer kiểm tổng cả kỳ, gửi ảnh chụp màn hình chuyển khoản/ghi chú nếu muốn, rồi báo đã chuyển. **Không cần mã giao dịch**; ảnh không bắt buộc theo policy đã duyệt.
2. Chờ xác nhận. Partner vào Đơn đặt sân/cơ sở đúng quyền: chỉ một nhóm, có số buổi/kỳ/tổng cả kỳ. Mở chi tiết thấy tất cả ngày và giá snapshot.
3. Xem biên lai nếu có. Số tiền thực nhận mặc định là tổng cả kỳ. Thử nhập tiền một buổi: phải báo không khớp, chưa CONFIRMED.
4. Nhập đúng tổng cả kỳ và xác nhận một lần. Customer/Partner phải hiển thị **CONFIRMED**, payment **PAID**, mọi buổi đã xác nhận, một lịch sử quyết định.
5. Customer nhận thông báo/link về đúng đơn. Không có check-in/check-out/hủy/đổi lịch hoặc bước sau CONFIRMED.

Đang làm gì: một quyết định thanh toán áp dụng toàn kỳ, không thanh toán riêng buổi đầu. QR/giá/account là snapshot khi tạo, đổi cấu hình owner sau này không đổi đơn.

## 4. Validation, conflicts và quote

| ID | Thao tác | Kết quả đúng / mục đích |
|---|---|---|
| V01 | Chọn90phút hoặc thấp hơn minimum sân | Không cho tạo quote/đơn hợp lệ; fixed≥max120/minimum |
| V02 | Giờ18:15 hoặc thời lượng135phút qua request API/DevTools |400 validation; backend không chỉ dựa validation UI |
| V03 | Kỳ dưới một tháng lịch |Reject. Jan31→Feb28 là một tháng khi có tháng2 tương ứng; 4tuần không luôn là một tháng |
| V04 | Ngày quá khứ/kết thúc quá60ngày |Reject theo timezone venue |
| V05 | Xóa một ca giá/đặt giờ ngoài giờ mở rồi quote |Không invent giá0, không tạo quote usable; báo missing price/schedule |
| V06 | QR chưa READY/chưa setup |Không tạo booking, báo payment setup unavailable |
| V07 | Owner tạo maintenance ở một ngày giữa/cuối kỳ trước quote |Preview nêu ngày conflict, không cho tạo; không tự bỏ ngày đó |
| V08 | A lấy quote hợp lệ; B xem lịch/quote trùng một buổi hoặc Owner tạo maintenance ở đó |Tất cả buổi A giữ hiện Đã kín; B không quote/đặt được; maintenance409SLOT_CONFLICT. A vẫn create được trước expiresAt. Đợi quote hết hạn thì mọi buổi AVAILABLE, B đặt được |
| V09 | Đợi hơn2phút sau quote rồi tạo |QUOTE_EXPIRED; lấy quote mới, không tạo từ quote cũ |
| V10 | Lấy quote rồi owner đổi giá/hold/minimum/QR, sau đó tạo |QUOTE_CHANGED; khách phải xem và xác nhận quote mới |
| V11 | Gửi hai lần cùng intent sau lỗi mạng/mất response |Một nhóm/payment, không thêm booking/evidence/decision; retry giữ key/body |

V08 kiểm tra giữ tạm và hết hạn bằng **lịch public của mọi ngày**; tại bước quote danh sách đơn vẫn chưa có đơn. Thử đồng thời hai customer lấy quote: chỉ một bên giữ được, bên còn lại không có hold một phần. UI không hỗ trợ giờ sai18:15 thì V02 thuộc API integration tự động; không sửa mã FE để ép nghiệm thu tay.

## 5. Cần bổ sung, từ chối, hết hạn và SLA

### P01: review → supplement sau hạn cũ → confirm

1. Tạo series mới và báo chuyển. Partner chọn Yêu cầu bổ sung, nhập lý do, gửi.
2. Cả nhóm NEEDS_REVIEW; tất cả allocations vẫn bị giữ. Đợi qua hạn chuyển khoản ban đầu.
3. Customer bổ sung ảnh/ghi chú, trạng thái quay chờ owner. Hạn cũ không ngăn supplement và không giải phóng sân; thời điểm báo đầu tiên/SLA không reset.
4. Owner xác nhận đúng tổng, mọi buổi CONFIRMED.

### P02: từ chối cuối cùng

1. Series mới đã báo chuyển. Owner chọn Từ chối cuối cùng, lý do và checkbox phải ghi rõ **giải phóng toàn bộ số buổi**.
2. Customer PAYMENT_REJECTED; mọi ngày/khung sân đã trống. Tạo lại series/vãng lai ở các ngày đó được.
3. Customer không tự hủy. Owner không được từ chối booking CONFIRMED qua UI hoặc API.

### P03: chưa báo chuyển hết hạn

1. Đặt hold5 trước quote, tạo series, không báo chuyển. Ghi nhận paymentDeadline.
2. Đợi qua deadline và vài chu kỳ Worker. Series EXPIRED, tất cả buổi giải phóng, không còn một ngày bị giữ sót.
3. Tạo series đã báo chuyển trước hạn với cùng hold5 ở khoảng khác. Đợi quá deadline: vẫn chờ xác nhận, không EXPIRED. NEEDS_REVIEW cũng giữ cả kỳ.

### P04: cảnh báo30phút

1. Series đã báo chuyển, owner chưa xác nhận. Ghi firstReportedAt và chờ trên30phút, Worker vẫn chạy.
2. Owner và Admin mỗi người nhận một cảnh báo cho nhóm. Admin chỉ xem cảnh báo, **không** có nút xác nhận tiền hoặc đọc biên lai customer.
3. Bổ sung/làm mới/chạy lại Worker không nhân cảnh báo hoặc reset firstReportedAt. Sân vẫn giữ cả kỳ.

Đang làm gì: kiểm Worker dùng aggregate và transactional outbox. Server có thể có độ trễ vài chu kỳ, không phải cập nhật đúng một mili giây ởdeadline. Không thay đồng hồ máy để thay thế kiểm thời gian thật.

## 6. Quyền, đồng thời và snapshot

- Customer khác không đọc detail/QR/biên lai của nhóm này. Owner business/cơ sở khác không đọc hoặc xác nhận. Admin không có quyền payment của owner.403/404 tùy guard, không lộ dữ liệu riêng tư.
- Đổi business/cơ sở hoặc logout: detail/ảnh cũ phải được xóa khỏi UI. F5 Customer/Partner đăng nhập lại theo memorysession, quay đơn cũ; không tạo thêm series.
- Hai customer đồng thời đặt giao nhau: chỉ một bên thành công, bên thua không giữ bất kỳ phần kỳ nào. Dùng hai cửa sổ/tài khoản và cùng ngày/giờ; trường hợp chính xác race được PostGIS test tự động kiểm thêm.
- Owner sửa giá/tài khoản/QR sau tạo: detail vẫn hiển thị số tiền/recipient đã chốt của đơn, giá public của lượt đặt mới dùng cấu hình mới.
- Trong DevTools Network, command owner có `If-Match` và `Idempotency-Key`; stale412 yêu cầu tải lại và xem trạng thái trước quyết định, không tự gửi lại quyết định khác. Không chụp/share header Authorization hoặc cookie.

## 7. UI và hồi quy

Thực hiện thêm [checklist UI ba role](./F07-ui-manual-acceptance.md): viewport375/768/1024/1440, Tab/skiplink/menuEscape/zoom200%, lỗi/loading/empty, AdminF5 và idle30phút kể từ thao tác cuối. Số liệu dashboard phải dựa API, không là doanh thu demo.

Làm lại ít nhất một đơn vãng lai5ca liên tiếp qua F05–F06: quote → tạo → ảnh/báo chuyển → review/bổ sung → exact confirm. Đơn casual cũ vẫn đọc được, replay không bị đổi scope/hash. Guide F06 vẫn áp dụng cho payment; F07 bổ sung tác động **cả kỳ**.

## 8. Ghi nhận nghiệm thu

Ghi PASS/FAIL từng C01–C04, V01–V11, P01–P04, quyền/snapshot/concurrency/UI/hồi quy; kèm ngày/role/court/seriesNo test và bước lỗi. Không lưu secrets/PII. Đối chiếu [testcase tự động](./F07-test-cases.md) cho migration/constraint/race không dễ kiểm tay.

Chỉ chuyển F07 DONE khi acceptance cần thiết đã đạt, không còn lỗi nghiêm trọng và bạn xác nhận nghiệm thu. Commit/push theo yêu cầu riêng của bạn; S3 thật vẫn theo phạm vi hoãn đã chốt.
