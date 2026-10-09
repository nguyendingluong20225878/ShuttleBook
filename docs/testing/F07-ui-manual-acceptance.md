# Nghiệm thu tay UI F07

> Chốt 2026-10-08: người dùng đã nghiệm thu F07, bao gồm thay đổi quote giữ tạm/Customer UI; prompt đánh giá sản phẩm đã hoàn tất. **F07 DONE local**. Các câu chờ nghiệm thu hoặc IN_PROGRESS bên dưới thuộc mốc lịch sử; không tự lập biên bản PASS từng ca tay. S3/provider/production vẫn có gate riêng NOT RUN; xem progress và báo cáo sản phẩm.


Đây là checklist **UI và hồi quy F01–F06**. Luồng cố định/thanh toán cả kỳ F07 kiểm thêm theo `F07-manual-acceptance.md`; UI đạt riêng chưa đủ để đánh dấu toàn F07 DONE. Prompt/contract ở `docs/prompts/F07-fixed-series-ui.md`, `docs/features/F07-fixed-series.md`.

## 1. Khởi động ở terminal VS Code / PowerShell

Mở workspace `C:\Users\luong\Desktop\CLong`. Chạy mỗi tiến trình lâu dài trong terminal riêng:

```powershell
npm.cmd run dev:api
```

```powershell
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

Docker/PostgreSQL phải đang chạy. Người dùng chủ động `npm.cmd run db:migrate` để nâng schema F07 trước nghiệm thu, rồi restart API/Worker. Khi cần kho ảnh local, đặt `$env:Media__Mode = 'Local'` trong **terminal API và Worker trước khi chạy**, không gõ cú pháp Bash `Media__Mode=Local`. Không gửi key/mật khẩu/token trong chat hoặc ảnh chụp.

Ở terminal thứ sáu:

```powershell
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

Mục đích: xác nhận API đang chạy và kết nối DB/schema đúng. Cả hai mong đợi200 Healthy. Ready503: kiểm Docker/migration/config trước nghiệm thu nghiệp vụ; lỗi không kết nối: terminal API chưa chạy hoặc sai cổng.

## 2. Customer — localhost:5173

1. Mở `/venues` chưa đăng nhập. Header có thương hiệu/Tìm sân/Đăng nhập; search card có nhãn tên/địa chỉ, tìm vị trí/khu vực/bán kính. Tab bàn phím → skip link → Enter: focus tới nội dung, trang không đổi.
2. Tìm cơ sở đã publish. Kết quả có tên/địa chỉ và link lịch; tên/địa chỉ dài xuống dòng, không kéo toàn trang cuộn ngang. Nếu từ chối vị trí, vẫn tìm bằng tên/địa chỉ; không bị chặn bởi map lỗi. Mục đích: kiểm fallback và đọc dữ liệu thật, không chỉ card demo.
3. Mở cơ sở. Bảng vẫn có tất cả sân theo hàng và cột30phút đủ rộng; mobile cuộn **bên trong bảng**, không co nhãn chồng nhau. Chọn 4/5/6ca liên tiếp tùy minimum sân; click bỏ một ô vẫn theo logic đã nghiệm thu F04/F06.
4. Tiếp tục vãng lai khi chưa login phải về đăng nhập, đăng nhập thành công quay đúng bước cũ. Form có nhãn/feedback dễ đọc; đăng nhập sai không mở trang riêng tư. Đổi sang Cố định hằng tuần phải mở form kỳ/preview trước tạo; kiểm đầy đủ theo checklist nghiệp vụ F07.
5. Xem báo giá: tổng và TTL; mở “Chi tiết giá từng ca30phút” để đối chiếu từng mức giá. Tạo đơn, xem facts cơ sở/sân/ngày/giờ, tổng nổi bật, QR và hạn. Mục đích: bảo đảm cách trình bày mới không đổi số tiền hoặc snapshot.
6. Báo chuyển với/không ảnh; không cần mã giao dịch. Chờ xác nhận vẫn giữ sân, owner xác nhận thì hiện thành công; lịch sử/biên lai chỉ đúng tài khoản. Nghiệp vụ biên tiếp tục theo `F06-manual-acceptance.md`.
7. Vào Đơn của tôi và Thông báo. Dòng đọc/chưa đọc có chữ và màu; đánh dấu đọc cập nhật số thật. F5 customer vẫn về login theo session memory hiện tại; login lại mở đúng đơn/thông báo, không tạo đơn lần nữa.
8. Đổi viewport375/768/1024/1440 qua DevTools responsive; Tab/ShiftTab được tất cả nút/form, focus thấy rõ, không bị che. Button và input dễ bấm; Zoom200% chữ vẫn đọc được. Screenshot không kèm secrets/PII thật.

## 3. Partner — localhost:5174

1. Login owner đã ACTIVE; vào Đơn đặt sân. Có phần “Danh sách đơn & đối chiếu”, chọn cơ sở/filter rõ. Bộ đếm là response thật; loading/error không tự biến thành số0 giả.
2. Lọc trạng thái/ngày. Đến ngày trước từ ngày phải báo lỗi, không submit filter sai. List giá/tên/status xuống dòng trên điện thoại; xem đơn mở chi tiết đúngscope.
3. Đối chiếu số tiền/recipient/transferContent snapshot trước quyết định. Retry mất mạng giữ intent; stale412 yêu cầu tải lại. Confirm đúng tổng; review giữ sân; final reject có checkbox và lý do. Các nghiệp vụ này không thay đổi bởi layout mới.
4. Biên lai chỉ xem khi có quyền; đổi doanh nghiệp/cơ sở/logout không giữ nội dung riêng tư cũ. Không có nút owner thao tác thay Admin hoặc ngược lại.

## 4. Admin — localhost:5175

1. Login Admin. Desktop có sidebar Tổng quan/Duyệt hồ sơ/Thông báo; mobile có menu44px. Tổng quan dùng số hồ sơ **trong danh sách đã tải** (API hiện trả tối đa100) và số thông báo chưa đọc thật, không là thống kê doanh thu.
2. Mở Duyệt hồ sơ → chọn hồ sơ → nhập lý do chưa gửi → chuyển Thông báo → Back/Forward: bản nháp cùng phiên vẫn còn. Skip link khi đang Duyệt hồ sơ phải chỉ focus nội dung, không chuyển Tổng quan hoặc đổi hash.
3. Kiểm địa chỉ/ảnh/QR/giờ/giá trước approve/request-changes. Lý do ít nhất10ký tự; submit đang chạy disable để tránh double click. Kết quả cập nhật danh sách; network lỗi có tải lại, không báo thành công giả. Ảnh QR ở đây là ảnh hồ sơ được quyền duyệt, không là biên lai payment của khách.
4. Thông báo có cảnh báo đối chiếu; Admin không có nút xác nhận tiền hoặc link ảnh biên lai. Đánh dấu đã đọc cập nhật số; tải lại chủ động đọc dữ liệu mới.
5. Mobile mở menu, Tab đến link, Escape: menu đóng và focus về nút mở. Không có overflow toàn trang/nhãn bị cắt ở375px. Khi menu đóng không Tab vào phần sidebar ẩn.
6. F5 dưới30phút từ thao tác thật: vẫn workspace; `dev:admin` chạy React StrictMode nên kiểm **cả lúc mới mở trang đã có cookie hợp lệ**. Mục đích: bắt lỗi đọc cùng response restore hai lần trong môi trường development.
7. Test idle thật: ghi giờ thao tác cuối; không click/gõ trong Admin, đợi trên30phút. Tab focus tự động, hash do script hoặc notification background không được kéo dài phiên. Sau đó F5 phải login. Khi còn trước30phút, thao tác click/gõ thực phải bắt đầu lại thời hạn. Không đổi Date.now/đồng hồ trình duyệt để chứng minh server cookie hết hạn.
8. Khi access token xoay trong lúc tải hồ sơ, list không được bị disable mãi. Khi bị từ chối quyền, không còn hiển thị hồ sơ/ảnh cached. Mục đích: kiểm race và quyền, không chỉ màu sắc.

## 5. Ghi nhận

Ghi từng mục PASS/FAIL, ngày, role/viewport và các bước tái hiện. UI PASS chỉ nghiệm thu mốc UI. Đối chiếu thêm `F07-manual-acceptance.md` và kết quả runtime trong `F07-test-cases.md`; không thay bằng chứng transaction/constraint thật bằng việc chỉ xem layout.
