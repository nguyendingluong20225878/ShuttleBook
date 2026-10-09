# Nghiệm thu điều chỉnh F07 — báo giá giữ chỗ tạm và Customer UI

> Chốt 2026-10-08: người dùng đã nghiệm thu F07, bao gồm thay đổi quote giữ tạm/Customer UI; prompt đánh giá sản phẩm đã hoàn tất. **F07 DONE local**. Các câu chờ nghiệm thu hoặc IN_PROGRESS bên dưới thuộc mốc lịch sử; không tự lập biên bản PASS từng ca tay. S3/provider/production vẫn có gate riêng NOT RUN; xem progress và báo cáo sản phẩm.

Ngày: 2026-10-08. Dùng VS Code terminal PowerShell, dữ liệu local; không cần chuyển khoản thật.

## Khởi động phiên bản mới
Tại C:\Users\luong\Desktop\CLong, dừng API/Worker cũ Ctrl+C; Docker/PostGIS đang chạy. Chạy:
```powershell
npm.cmd run db:migrate
```
Migration thêm quote_reservations và liên kết allocation, giữ booking/payment cũ. Sau đó mỗi lệnh một terminal riêng:
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
Cả health/live và health/ready phải200 Healthy. Customer5173/Partner5174/Admin5175.

## Chuẩn bị
Hai Customer ACTIVE A/B ở hai browser profile/cửa sổ riêng; owner ở Partner. Chọn sân ACTIVE thuộc venue PUBLISHED, QR READY, lịch/giá đầy đủ. Tránh khung đã có booking hoặc maintenance. Mặc định quote120s; không nhầm với thời gian giữ chuyển khoản mặc định20phút.
Guest xem/tìm sân bình thường; phải đăng nhập Customer trước bước lấy báo giá có giữ chỗ. Việc chỉ bấm ô trên bảng mà chưa gửi báo giá không giữ chỗ.

## H01 — cố định giữ tất cả buổi trước tạo đơn
1. A chọn cố định cùng court/thứ/giờ, mỗi buổi≥max120/minimum, kỳ≥1tháng, trong60ngày.
2. A nhấn Xem báo giá toàn kỳ, chưa nhấn Xác nhận tạo lịch cố định.
3. A thấy countdown “Đang giữ chỗ tạm toàn kỳ cho bạn”.
4. B mở lịch cùng court vào từng ngày trong quote, nhấn Làm mới lịch. Mọi ca A chọn hiện Đã kín và không click được.
5. B chọn giờ khác/sân khác vẫn được. Nếu B đã chọn trước khi A quote và dùng lịch cũ để tiếp tục, backend vẫn chặn khi lấy quote, không tạo trùng.
6. Owner thử tạo maintenance chồng một buổi:409SLOT_CONFLICT, không ghi maintenance.
7. Đơn của tôi của A/Partner chưa có booking/payment cho báo giá này.
8. Trước countdown0, A xác nhận tạo kỳ: thành công, một đơn/một QR/tổng cả kỳ. Mọi ca tiếp tục giữ, chuyển deadline sang holdMinutes.
9. Báo chuyển/Owner xác nhận đúng tổng vẫn CONFIRMED/PAID toàn kỳ.

## H02 — A bỏ dở, B đặt sau hết hạn
Dùng kỳ khác còn trống:
1. A lấy quote nhưng không create, ghi expiresAt/countdown.
2. B xác nhận mọi ngày Đã kín.
3. Chờ countdown hết (>120s mặc định), B nhấn Làm mới lịch từng ngày.
4. Mọi ca A chỉ giữ tạm trở thành AVAILABLE/Còn trống; không còn một buổi bị giữ sót.
5. B lấy báo giá và tạo đơn được. A không thể tạo từ quote cũ, phải lấy báo giá mới.
6. Có thể dừng Worker bằng Ctrl+C trước bước3: lịch vẫn bỏ qua quote hết hạn và API B tự cleanup vật lý trước khi giữ mới. Khởi động Worker lại sau test.
Đây là kỳ vọng mới thay cho test cũ “B chen vào sau A nhận quote còn hạn”.

## H03 — vãng lai
Lặp H01/H02 cho một ngày, ví dụ5ca liên tiếp150phút nếu đạt minimum sân. Quote tạm khóa đúng5ca, không khóa sân/ngày khác; create dùng allocation cũ và một payment. Không yêu cầu 150 là bội bookingBlockMinutes.

## H04 — báo giá lại và giá đổi
1. A nhận quote, đợi20–30s, nhấn lấy báo giá mới hoặc F5 rồi đăng nhập lại và lấy quote cùngcourt.
2. Deadline giữ tạm không được tự kéo dài; quote mới kế thừa thời gian còn lại. Quote cũ đã bị thay thế không create được.
3. Thay khung giờ ở form, lấy quote hợp lệ: khung cũ trả trống, khung mới bị giữ, cùng deadline trước.
4. Quote mới không hợp lệ/xung đột phải rollback; khung cũ vẫn giữ đến hạn.
5. Owner đổi giá/policy/QR sau quote: A create nhận QUOTE_CHANGED, phải xem lại báo giá; không tự trả giá cũ.
6. Chỉ một hold chưaconsume/release/customer/court; casual↔fixed cùngcourt thay thế nhau. Chưa giới hạn toàn bộcourt/account; đánh giá abuse ở gateproduct.

## H05 — quyền, retry và sau tạo đơn
- Customer B không dùng quoteId của A để tạo đơn; Guest401/Operator403 tại quote. Không chia sẻ Authorization/token trong ảnh chụp.
- Retry cùng Idempotency-Key/body sau response lỗi trả cùng booking; quote consumed không tạo đơn thứhai bằng keykhác.
- A đãcreate: qua hạn quote2phút booking vẫn giữ đến deadline thanh toán, không bị Workerquote giải phóng.
- A đãreport/NEEDS_REVIEW: qua deadline thanh toán vẫn giữ để Owner xử lý. Không đổi F06.
- Giá/QR snapshot đơn cũ vẫn giữ; ảnh private tồn tại sau F5 và đăng nhập lại.

## H06 — UI Customer cùng phong cách Partner/Admin
- Desktop: sidebar bên trái, toolbar, nền sáng, card và controls dùng palette teal tương ứng hai role.
- Mobile375/768: menu dễ bấm, nội dung/form/card vừa chiều ngang; bảng lịch cuộn ngang bên trong.
- Tab/Enter/focus/skiplink dùng được; đọc rõ trạng thái, countdown, errors/empty/loading; zoom200%.
- Tìm sân, đăng nhập trở lại selection, đặt vãng lai/cố định, ảnhQR/report/list/notification không mất luồng.
- Không số liệu giả/không đổi cơ chế session: Customer/Partner F5 loginlại; Adminidle30phút từ thao tác thật.

## Ghi kết quả
Đánh dấu PASS/FAIL H01–H06, ghi court/ngày/kỳ/quoteexpiry/mãđơn và ảnh lỗi. Concurrency/DBconstraint/migration được bổ sung bằng suitePostGIS thật, không thay bởiUIstub. Người dùng đã xác nhận nghiệm thu, F07 DONE local ngày 2026-10-08; checklist giữ để tái kiểm. Commit/push theo yêu cầu riêng.
