# Nghiệm thu các điều chỉnh sau audit UI

Ngày: 2026-10-09. Phạm vi: [đặc tả](../features/UI-post-audit-improvements.md). Đây là đợt chỉnh UI/recovery riêng; F07 đã DONE local theo nghiệm thu trước.

## 1. Chạy phiên bản mới trong terminal VS Code

Dùng **PowerShell**, thư mục `C:\Users\luong\Desktop\CLong`. Mỗi tiến trình dev cần một terminal riêng. Nếu đang chạy bản cũ, nhấn Ctrl+C trong đúng terminal rồi chạy lại:

```powershell
npm.cmd run db:up
npm.cmd run dev:api
```

Trong các terminal khác:

```powershell
npm.cmd run dev:customer
```

```powershell
npm.cmd run dev:partner
```

```powershell
npm.cmd run dev:admin
```

Kiểm tra API từ terminal rảnh:

```powershell
curl.exe -i http://localhost:5080/health/ready
```

Mong đợi HTTP 200, Healthy. Lượt sửa này không thêm migration. Nếu DB chưa áp dụng F07, làm bước migration trong [hướng dẫn F07](F07-quote-reservations-manual.md) trước. Worker cần chạy nếu muốn thử thông báo/hết hạn qua toàn bộ luồng: `npm.cmd run dev:worker`.

Địa chỉ: Customer http://localhost:5173; Partner http://localhost:5174; Admin http://localhost:5175. Dùng tài khoản local đã nghiệm thu; không gửi mật khẩu/token qua chat. Nếu cổng bận, dừng đúng tiến trình dev cũ của bạn; không mở thêm bản thứ hai.

## 2. Customer — màn hình tài khoản và khoảng cách (thay bài test marker)

Cập nhật2026-10-09: người dùng yêu cầu bỏ sidebar ở login/register/verify và bỏ bản đồ Customer. Các mục3–6 bên dưới đã được người dùng xác nhận PASS; chỉ nghiệm thu thay đổi mới trong mục này.

1. Mở http://localhost:5173/login, F5: header ngang, không còn menu dọc trái. Kiểm tra Đăng ký và bước xác minh có cùng bố cục; form và điều hướng vẫn dùng được.
2. Mở Tìm sân: chỉ có danh sách cơ sở, không có bản đồ hoặc vùng trắng dành cho bản đồ. Bấm tên cơ sở/Xem lịch các sân để vào lịch.
3. Bấm Dùng vị trí của tôi, chọn Allow/Cho phép. Kiểm tra cơ sở trong bán kính có dòng Cách vị trí của bạn khoảng …m/km.
4. Sau đó tìm theo tên/địa chỉ: vị trí đã cấp trong trang vẫn được dùng để ước lượng khoảng cách đến cơ sở. Khoảng cách là đường thẳng, không phải quãng đường đi xe.
5. Chưa cấp hoặc từ chối vị trí: không hiện khoảng cách giả; thông báo hướng dẫn và tìm theo tên vẫn dùng được.
6. Nếu chọn một khu vực thay vì cấp vị trí, nhãn là Cách khu vực đã chọn; không gọi đó là vị trí của bạn. Khi đã biết vị trí hiện tại, khoảng cách trên card vẫn tham chiếu vị trí của bạn.
7. Thử trên điện thoại: không tràn ngang. Đăng nhập rồi đi từ danh sách→lịch→báo giá vẫn giữ phiên trong SPA.

Khoảng cách nearby lấy từ PostGIS; khoảng cách trong tìm tên là ước lượng từ tọa độ cơ sở trả về. Vị trí hiện tại chỉ giữ trong memory của trang; chưa cấp quyền thì hệ thống không biết vị trí của bạn. Quy trình địa chỉ của Partner dùng riêng.

## 3. Customer — lỗi mạng khi tạo đơn và báo giá hết hạn

### Chưa từng gửi yêu cầu

1. Lấy báo giá, không bấm Xác nhận tạo đơn.
2. Chờ countdown hết hạn.
3. Nút Xác nhận tạo đơn phải bị khóa; Lấy báo giá mới còn dùng được.

### Đã gửi nhưng chưa biết kết quả

1. Lấy báo giá; giữ nguyên trang này.
2. DevTools → Network → Offline, bấm Xác nhận tạo đơn để gây lỗi mạng.
3. Nút Lấy báo giá mới bị khóa và có hướng dẫn kiểm tra Đơn của tôi/gửi lại yêu cầu cũ.
4. Chờ hết TTL, chuyển Network về No throttling; bấm Xác nhận tạo đơn lần nữa.
5. Nếu lần đầu không tới API, máy chủ trả báo giá hết hạn; UI bỏ báo giá cũ và cho lấy báo giá mới. Đây là kết quả đúng, không phải lỗi tạo đơn.
6. Nếu có đơn đã commit nhưng response bị mất, máy chủ trả lại chính đơn đó, không tạo đơn thứ hai. Tình huống này đã được agent kiểm trên PostGIS thật; Offline trước khi gửi không chứng minh trường hợp commit rồi mất response.

Trong Network, hai POST `/api/v1/bookings` của cùng lần thử phải có **cùng Idempotency-Key và cùng body**. Không refresh/đổi trang giữa các lần thử: intent của trang hiện tại giữ trong memory. Không tự gửi request mới/key mới để thử tình huống mất response. Không copy token vào tài liệu/chat.

**Đạt:** chưa gửi thì hết hạn bị khóa; đã thử gửi thì có đường kiểm lại kết quả cũ sau TTL; lỗi xác định như QUOTE_EXPIRED/CHANGED/CONSUMED bỏ quote cũ, không tự lấy quote khác.

## 4. Partner — lịch sử đơn và thông báo

### Đơn đặt sân

1. Chọn cơ sở có hơn 20 đơn, bấm Xem thêm đơn.
2. Ghi nhận một đơn nằm ở trang tải thêm; chờ ít nhất 6 giây và đổi tab rồi quay lại.
3. Bấm Tải lại đơn. Đơn ở trang tải thêm vẫn hiện nếu còn phù hợp bộ lọc; trạng thái phải lấy mới từ server.
4. Dùng Customer/Partner khác tạo hoặc xử lý một đơn. Làm mới và kiểm tra trạng thái/count cập nhật, không trùng dòng.
5. Đổi trạng thái/ngày lọc hoặc cơ sở: danh sách phải chuyển sang phạm vi mới; không mang dòng cũ sang.

### Thông báo

1. Với hơn 50 thông báo, bấm Thông báo trước đó.
2. Chờ polling, hoặc bấm Tải lại thông báo: các trang đã tải còn hiện.
3. Đánh dấu Đã đọc một thông báo ở trang cũ; kiểm tra badge và số chưa đọc cập nhật, không mất trang cũ.
4. Thông báo mới phát sinh phải xuất hiện khi làm mới; không lặp thông báo.

Thiếu đủ dữ liệu thì ghi **NOT RUN**, không tạo hàng loạt đơn thật chỉ để đủ 20/50. Agent đã kiểm cửa sổ 25–26 đơn và 55–56 thông báo bằng fixture desktop/mobile. Các ca scope/response đến chậm/403–404 purge cũng đã kiểm tự động; không tự sửa DB tài khoản thật để thử.

## 5. Partner — Xem giá và Lưu

1. Vào Lịch & giá/Vận hành sân, chọn sân/ngày/khung giờ có giá.
2. Bấm Xem giá: hiện tổng/từng ca và thông báo xanh **Đã tính giá.**
3. Lưu một cấu hình hợp lệ: thông báo **Đã lưu.**
4. Thử khung giờ không có giá hoặc lỗi request: hiện lỗi, không báo thành công và không giữ kết quả preview cũ để gây hiểu nhầm.
5. Chuyển sân: preview cũ được xóa; lỗi tải cấu hình phải mang kiểu lỗi.

## 6. Admin — tìm hồ sơ, đối chiếu và xác nhận

### Danh sách

1. Tìm tên doanh nghiệp; chọn loại Hồ sơ mới hoặc Thay đổi cơ sở; bấm Tìm hồ sơ.
2. Kiểm tra dòng hiển thị phù hợp. Tổng chờ xử lý toàn hệ thống không bị đổi thành số kết quả lọc.
3. Xem thêm hồ sơ, Tải lại danh sách: giữ các trang đã tải, cập nhật hồ sơ còn chờ; Xóa bộ lọc trở lại danh sách đầy đủ.
4. Nếu có hơn 100 hồ sơ pending, xác nhận vẫn xem được hồ sơ ngoài 100. Agent đã kiểm API bằng 103 hồ sơ trong DB tạm; nếu local không đủ thì ghi NOT RUN tay cho ca này.

### Hồ sơ thay đổi cơ sở

1. Partner gửi revision có địa chỉ/ngân hàng/QR khác bản đang công bố.
2. Admin mở hồ sơ: bảng phải hiển thị Đang công bố và Đề nghị thay đổi; ô khác biệt được đánh dấu. Trên mobile, vuốt ngang bảng để xem cột đề nghị.
3. Bấm Xem QR đang công bố và Xem QR mới: xem đúng hai ảnh tương ứng, không chỉ ảnh đề nghị. Agent dùng ảnh fixture; bạn cần xác nhận nội dung QR local thực tế.
4. Bấm Phê duyệt: chỉ mở vùng xác nhận, chưa gửi quyết định.
5. Bấm Hủy phê duyệt: đóng xác nhận, hồ sơ vẫn pending.
6. Mở lại và bấm Xác nhận phê duyệt: gửi một quyết định; hồ sơ khỏi danh sách chờ, thông tin công bố cập nhật theo luồng cũ.
7. Trường hợp không tải được current: nút Phê duyệt bị khóa, có hướng dẫn tải lại.
8. Hai Admin xử lý cùng hồ sơ: người thứ hai nhận conflict và hướng dẫn đọc lại; không được báo Đã lưu quyết định.

### Yêu cầu chỉnh sửa

- Chỉ khoảng trắng hoặc 9 ký tự sau trim: không gửi được.
- 10 ký tự và 1.000 ký tự: gửi được; khoảng trắng đầu/cuối được bỏ trước khi gửi.
- 1.001 ký tự: bị khóa, có cảnh báo. Có thể thử validation rồi đóng hồ sơ; chỉ gửi quyết định khi bạn muốn thay trạng thái local.

Lặp các thao tác chính ở màn hình hẹp khoảng 390px: nút không bị ảnh/bảng che; trang không tràn ngang, riêng bảng được cuộn ngang.

## 7. Ghi nhận nghiệm thu

Ghi PASS/FAIL/NOT RUN theo từng mục, kèm bước gây lỗi và response code nếu có; che thông tin riêng tư. [Kết quả tự động](UI-post-audit-improvements-results.md) phân biệt UI fixture và PostGIS thật. Tiện ích mật khẩu, phân quyền nhân viên và báo cáo không thuộc đợt sửa này.
