# Setup local — Windows PowerShell

## 1. Môi trường và phiên bản

Chạy các lệnh dưới đây tại **thư mục gốc repo**, ví dụ:

```powershell
Set-Location 'C:\Users\luong\Desktop\CLong'
```

- Git đã cài; repo có `origin/main`. Kiểm tra `git status --short --branch` trước khi thay đổi.
- Node 22.12+ trong dòng 22 hoặc Node 24; dùng `npm.cmd` trên PowerShell. Lockfile npm được lưu trong repo.
- .NET SDK 10.0.401 (`global.json`, cho phép bản vá trong cùng SDK band); script dùng SDK riêng `.tools/dotnet` nếu có.
- React 19.3.0, Vite 8.3.0, TypeScript 5.9.3, Playwright 1.63.0 được pin trong manifest/lockfile.
- Docker Desktop chạy Linux containers và Docker Compose v2 để chạy `postgis/postgis:17-3.5` local.

.NET 10 là LTS; .NET 8 hiện có trên máy không được dùng để build project mới này. Nguồn kiểm tra: [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Vite requirements](https://vite.dev/guide/), [PostGIS image](https://github.com/postgis/docker-postgis). Phiên bản package đã đối chiếu npm/NuGet, không lấy từ ví dụ tài liệu cũ.

## 2. Khởi tạo cấu hình và SDK

```powershell
.\scripts\Initialize-Local.ps1
.\scripts\Install-DotNet.ps1
.\scripts\Doctor.ps1
```

`Initialize-Local.ps1` tạo `.env` có mật khẩu random, không in secret và không ghi đè file đã có. `Install-DotNet.ps1` tải SDK Microsoft, kiểm tra SHA-512, giải nén riêng vào `.tools`; không sửa PATH/máy toàn cục. Lượt triển khai đầu đã làm hai bước này; chạy lại sẽ giữ cấu hình và bỏ qua SDK có sẵn.

`.env` ở gốc repo giữ thông tin PostgreSQL, khóa identity, `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS` và `VITE_API_BASE_URL`. Docker Compose và các script npm bên dưới tự nạp cấu hình này; không cần gõ từng lệnh `$env:...` trong terminal. Chỉ `.env.example` có placeholder được đưa vào Git.

Partner portal dùng MapTiler SDK cho bản đồ và MapTiler Geocoding API cho gợi ý địa chỉ. Tạo một API key MapTiler, đặt thành `VITE_MAPTILER_API_KEY` trong `.env` local và không commit. Vì key được dùng trong trình duyệt nên sẽ thấy được trong DevTools; giới hạn key theo website/domain và quota trong tài khoản MapTiler. Local partner/customer origins là `http://localhost:5174` và `http://localhost:5173`. Khởi động lại Vite của hai portal sau khi sửa `.env`. Khi thiếu key, form partner chặn chọn/lưu địa chỉ mới; venue đã lưu vẫn giữ địa chỉ/toạ độ hiện tại. Customer vẫn tìm theo tên/địa chỉ khi thiếu key hoặc không cấp vị trí. MapTiler ghi rõ gói Cloud Free chỉ dành cho non-commercial và R&D; kiểm tra [pricing](https://www.maptiler.com/cloud/pricing/) và [điều khoản Cloud](https://www.maptiler.com/terms/cloud/) trước khi dùng ShuttleBook thương mại. Xem [MapTiler SDK JS](https://docs.maptiler.com/sdk-js/) và [Geocoding API](https://docs.maptiler.com/cloud/api/geocoding/).

Doctor trả exit code 1 khi còn thiếu công cụ. Nếu PowerShell chặn script, có thể chạy từng lệnh bằng `powershell -ExecutionPolicy Bypass -File .\scripts\Doctor.ps1`; tùy chọn chỉ áp dụng tiến trình đó, không đổi execution policy toàn máy.

## 3. Docker Desktop

Docker Desktop đã được cài trên máy phát triển. Nếu setup máy khác, cài theo [hướng dẫn chính thức Docker cho Windows](https://docs.docker.com/desktop/setup/install/windows-install/), chọn WSL2/Linux containers. Mở Docker Desktop và chờ Engine chạy.

Mở PowerShell mới, kiểm tra:

```powershell
docker version
docker compose version
.\scripts\Doctor.ps1
```

Kết quả cần có cả Client và Server; Compose v2 chạy được. Không cần cài PostgreSQL riêng trên Windows.

## 4. Restore và build

```powershell
npm.cmd ci
npm.cmd run typecheck
npm.cmd run build
.\scripts\dotnet.ps1 restore backend/ShuttleBook.slnx --locked-mode
.\scripts\dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
```

`scripts/dotnet.ps1` chọn SDK local, đặt cache vào thư mục bị ignore và nạp `.env` vào connection string của tiến trình. Lệnh không in secret. Không chạy `dotnet` hệ thống SDK 8 thay cho wrapper nếu chưa cài SDK 10 toàn máy.

Kết quả: ba thư mục `apps/*/dist`, backend build không lỗi. Những artifact này không đưa vào Git.

## 5. Database và migration nền

```powershell
npm.cmd run db:up
npm.cmd run db:migrate
npm.cmd run db:migrate
```

PostgreSQL bind `127.0.0.1:54329`; named volume giữ dữ liệu khi restart. Migrator tạo extensions, EF migration history, bảng identity F01 và schema onboarding F02 gồm business/venue/court, PostGIS location, approval, media và outbox. Lần hai không áp dụng lại migration đã có. Không tự migrate trong API/Worker. Chưa có bảng booking.

Không dùng `docker compose down -v` khi cần giữ dữ liệu. Thay mật khẩu trong `.env` không tự thay mật khẩu của volume đã khởi tạo; cần cập nhật database có chủ đích, không xóa dữ liệu để xử lý cho nhanh.

## 6. Chạy các ứng dụng

Mỗi lệnh chạy lâu cần một terminal PowerShell riêng, cùng cwd gốc repo:

```powershell
# Terminal API (Development chỉ dùng local; nạp .env)
npm.cmd run dev:api
```

```powershell
# Terminal Worker: xử lý transactional outbox và thông báo in-app F02
npm.cmd run dev:worker
```

```powershell
# Một terminal cho mỗi cổng
npm.cmd run dev:customer
npm.cmd run dev:partner
npm.cmd run dev:admin
```

Mở `http://localhost:5173`, `http://localhost:5174`, `http://localhost:5175`. Cổng đối tác có hồ sơ chủ sân F02; cổng Admin có danh sách và quyết định duyệt. Chưa có chức năng booking. CORS Development chỉ chấp nhận chính xác ba origin này; dùng 127.0.0.1 thay localhost là origin khác.

Trong môi trường local, OTP được gửi đến Mailpit tại `http://localhost:8025`, kể cả khi bạn nhập một địa chỉ email thật; Mailpit không chuyển tiếp thư ra ngoài. Hãy tìm thư theo địa chỉ vừa đăng ký. Phản hồi đăng ký `202` là thông báo chung để tránh lộ tài khoản tồn tại: nếu contact đã xác minh hoặc thuộc loại tài khoản khác, hệ thống không gửi mã mới. Dùng contact thử nghiệm mới khi kiểm tra luồng đăng ký.

Chỉ kiểm tra health **sau khi terminal `npm.cmd run dev:api` báo đang lắng nghe ở cổng 5080**. Nếu `/health/live` khỏe nhưng `/health/ready` trả 503, xác nhận đúng database local rồi kiểm tra migration còn pending trước khi chạy Migrator.

```powershell
Invoke-RestMethod http://localhost:5080/health/live
Invoke-RestMethod http://localhost:5080/health/ready
```

Live trả Healthy nếu API còn phục vụ; Ready chỉ Healthy khi DB và baseline sẵn sàng. DB lỗi/thiếu migration thì Ready trả HTTP 503, không làm giả kết nối thành công.

### Khởi tạo và quản lý Admin local

Sau `npm.cmd run db:migrate`, người vận hành có quyền truy cập DB chạy tại **repo root trong PowerShell**:

```powershell
npm.cmd run admin:bootstrap
```

CLI hỏi loại contact, contact và password trong terminal; contact/password được nhập ẩn, không đặt trong lệnh, `.env`, log hoặc tài liệu. Contact phải do người vận hành kiểm soát; bootstrap chỉ tạo một `ADMIN/ACTIVE` đã xác minh và chạy lại không đổi tài khoản. Không chạy bootstrap trên database ngoài môi trường đã được phép. Không có endpoint đăng ký Admin công khai.

Khi cần thay mật khẩu, tạm khóa, mở lại hoặc thu hồi mọi phiên Admin hiện hành:

```powershell
npm.cmd run admin:rotate
npm.cmd run admin:suspend
npm.cmd run admin:activate
npm.cmd run admin:revoke-sessions
```

`rotate` và `suspend` thu hồi mọi phiên; `activate` chỉ cho phép đăng nhập phiên mới. Các lệnh thao tác trên database từ `ConnectionStrings__ShuttleBook` mà script local nạp; hãy xác nhận đúng môi trường trước khi chạy. Đăng nhập Admin ở `http://localhost:5175` sau khi API local hoạt động.

### Kiểm tra luồng F02 bằng UI

1. Đăng ký/xác minh chủ sân tại `http://localhost:5174`, đăng nhập `VENUE_OPERATOR/PENDING_ONBOARDING`. Tạo business, cơ sở có địa chỉ/liên hệ riêng/tọa độ/múi giờ, sân, giờ và giá theo ca 30 phút; tải ảnh cơ sở và QR, khai tài khoản nhận tiền. Thiếu phần bắt buộc thì **Gửi hồ sơ duyệt** trả `INCOMPLETE_PROFILE`.
2. Gửi hồ sơ, đăng nhập Admin tại `http://localhost:5175`, tải danh sách, xem snapshot/ảnh private và chọn **Yêu cầu chỉnh sửa** với lý do hoặc **Phê duyệt**. Owner thấy lý do sau khi tải lại hồ sơ và có thể sửa/gửi lại. Sau duyệt owner đăng nhập lại và thấy trạng thái `ACTIVE`; business `ACTIVE`, venue `PUBLISHED`, court `ACTIVE` trong DB.
3. Giữ `npm.cmd run dev:worker` chạy để thông báo gửi duyệt/quyết định đi từ outbox tới bảng thông báo. Nếu Worker tắt, quyết định vẫn lưu; khi Worker bật lại nó retry. Test tự động DB dùng database tạm và không tác động database phát triển mặc định.

Development dùng adapter file private `.media-local/` để tải ảnh/QR, URL PUT ký HMAC hết hạn sau 5 phút. Không đưa thư mục này vào Git. Khi triển khai S3 private, đặt biến môi trường tiến trình `Media__Mode=S3`, `Media__S3Region`, `Media__S3Bucket` và IAM credentials bằng cơ chế chuẩn của AWS SDK; cấp quyền `s3:PutObject`, `s3:GetObject`, bật CORS chỉ cho origin portal và header `Content-Type`, `x-amz-checksum-sha256`, `If-None-Match`. Không đưa credentials vào `.env.example` hoặc repo. Upload S3 chưa được nghiệm thu trong môi trường local này.

### Kiểm tra F04: khách tìm sân và xem lịch

Sau khi business được Admin duyệt thành `ACTIVE`, venue thành `PUBLISHED` và có ít nhất một court `ACTIVE`, mở `http://localhost:5173/venues` ở browser. Tìm theo tên/địa chỉ, mở **Xem lịch các sân**, chọn ngày tương lai có giờ mở và giá. Mỗi hàng là một sân, mỗi cột là một ca 30 phút. Xanh nhạt là ca có giá và còn trống, đỏ là ca bị giữ, xám là ngoài giờ/chưa có giá, tím là ca đang chọn; mỗi ô cũng có chữ mô tả. Chọn các ca liên tiếp để xem tổng giá tham khảo và điều kiện thời lượng. F04 chưa gửi yêu cầu đặt sân; F05 sẽ kiểm tra lại lịch/giá khi tạo đơn.

Trong portal partner `http://localhost:5174`, tạo bảo trì đúng sân/ngày/giờ đang xem. Quay lại trang khách, nhấn **Làm mới lịch**: ca đó chuyển thành **Đã kín**. Hủy bảo trì rồi làm mới: ca có giá trở thành **Còn trống**. Bảng luôn hiện đủ mỗi sân một hàng, không có dropdown **Xem sân**. Mốc giờ nằm ở ranh giới các ô giá 30 phút; bấm lại một trong hai ô đã chọn chỉ bỏ ô đó. Thay ngày rồi dùng Back/Forward của trình duyệt để kiểm tra URL giữ `date`. Tắt quyền định vị để kiểm tra tìm bằng chữ vẫn hoạt động; khi cho quyền, nút **Dùng vị trí của tôi** gọi nearby bằng PostGIS với bán kính chọn. Kiểm tra màn desktop và mobile hoặc cửa sổ hẹp: bảng có thể cuộn ngang.

Có thể đọc API không cần đăng nhập từ terminal VS Code PowerShell sau khi `/health/ready` trả Healthy:

```powershell
$venues = Invoke-RestMethod 'http://localhost:5080/api/v1/venues?q=F02'
$venues.data.items | Select-Object id,name,address,imageUrl
$venueId = $venues.data.items[0].id
$date = (Get-Date).AddDays(7).ToString('yyyy-MM-dd')
Invoke-RestMethod "http://localhost:5080/api/v1/venues/$venueId/availability?date=$date"
```

Đổi `q` thành tên venue vừa tạo và `$date` thành ngày venue có giờ hoạt động; nếu danh sách rỗng, kiểm tra trạng thái business/venue/court. Kết quả availability trả `courts[].slots[]` với giờ local, UTC, `status` và `pricePerSlot` VND/30 phút. Ảnh venue nằm ở endpoint `/api/v1/venues/{id}/image` khi upload đã `READY`; ảnh local được lưu private dưới `backend/src/ShuttleBook.Api/.media-local` và không mất vì F5. Mất file trên đĩa local hoặc xóa DB sẽ làm ảnh không đọc được; S3 mode lưu object trong bucket riêng.

S3 private dùng lại upload F02: `.env` local có thể đặt `Media__Mode=S3`, `Media__S3Region=<region>`, `Media__S3Bucket=<bucket-test>` khi **đã có bucket thử nghiệm và AWS credentials trên máy**. Không đặt key/secret trong repo hoặc gửi qua chat. Bucket phải chặn public access; IAM cần `s3:PutObject` và `s3:GetObject` cho prefix media của bucket (`HEAD` object dùng quyền đọc object). CORS bucket giới hạn origin partner `http://localhost:5174` cho presigned PUT; nếu dùng fetch trực tiếp signed GET từ customer `http://localhost:5173`, cho GET từ origin đó. Cho các header `Content-Type`, `x-amz-checksum-sha256`, `If-None-Match` theo request upload, không dùng wildcard origin ở môi trường thật. Kiểm tra presigned PUT → complete → public venue image redirect → signed GET, đồng thời kiểm tra object không đọc trực tiếp bằng URL không ký và QR không mở được qua route ảnh venue. Hiện chưa có bucket/credentials thử nghiệm nên các bước provider S3 thật là **NOT RUN**; adapter local và S3 path code không chứng minh S3 runtime.

### Kiểm tra vận hành F03 bằng UI

Thực hiện sau bước duyệt F02: đăng xuất rồi đăng nhập lại cổng đối tác `http://localhost:5174` để nhận trạng thái owner `ACTIVE`. Trong phần **Vận hành sân**, chọn đúng court của venue đã publish. Cấu hình giờ mở theo thứ trong tuần và giá cơ bản cho **mọi ca 30 phút** trong giờ mở; nhấn lưu, kiểm tra phiên bản court tăng. Tạo quy tắc giá theo khoảng ngày và mức ưu tiên, chọn một ngày/khung giờ để xem preview từng ca và tổng VND. Tạo bảo trì cho một khung tương lai theo múi giờ venue, thử tạo trùng để thấy `SLOT_CONFLICT`, sau đó hủy và kiểm tra khung đó dùng lại được. QR/tài khoản sau publish sửa trong phần **Yêu cầu chỉnh sửa** của F02 và chỉ có hiệu lực khi Admin duyệt revision.

Nếu `/health/ready` trả 503 sau khi kéo code F03, kiểm tra migration còn pending và xác nhận đúng database local cần nâng cấp trước khi chạy `npm.cmd run db:migrate`. F03 thêm `F03CourtOperations`; lệnh test tự động dùng database tạm và không thay đổi database development. Với PowerShell, chạy lệnh tại repo root; nếu chạy từ WSL hãy dùng toolchain và checkout Linux riêng như phần WSL bên dưới.

## 7. Kiểm thử

```powershell
npm.cmd run test:api
npm.cmd run test:db
```

API tests xác minh HTTP/error/CORS contract bằng test host; chúng không thay thế DB tests. DB tests cần PostgreSQL/PostGIS thật, tạo database test tên riêng rồi dọn đúng database đó; tài khoản cần quyền tạo database. Test helper chỉ chấp nhận host loopback và database điều khiển `postgres`; không trỏ `SHUTTLEBOOK_TEST_CONNECTION_STRING` tới server production.

```powershell
npm.cmd run setup:browsers
npm.cmd run build
npm.cmd run test:web
```

Script mở ba preview server ẩn và dừng đúng tiến trình do nó tạo sau khi kiểm thử desktop/mobile. Dừng các dev server trên 5173–5175 trước khi chạy; nếu cổng bận script báo lỗi và không dừng tiến trình khác. Browser đã tải trong lượt đầu thì không cần tải lại. Các ca live cần API/Mailpit thật có điều kiện và được ghi riêng trong testcase, không coi các ca mock là bằng chứng end-to-end.

Để chạy riêng browser E2E dùng API/PostgreSQL/Mailpit thật trên **database tạm local** (repo root, Docker đã healthy, các cổng 5080 và 5173–5175 trống):

```powershell
.\scripts\dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:identity-live
```

Script đọc `.env` local, ép PostgreSQL về `127.0.0.1`, kiểm tra tên database tạm qua EF trước khi migrate/dọn, build web với API `http://localhost:5080`, tạo Admin test bằng CLI và chạy ca identity/F02/F03 desktop/mobile. Password test được sinh trong tiến trình, không in ra console. Script dừng API/Worker/preview do nó tạo và xóa đúng database tạm sau khi kết thúc. Nếu phiên terminal bị ngắt đột ngột, có thể còn database với tiền tố `shuttlebook_f014_live_`; kiểm tra tên cụ thể trước khi dọn, không chạy lệnh xóa database theo tên mặc định.

CI `.github/workflows/ci.yml` có web/backend jobs; backend dùng service PostgreSQL/PostGIS thật. Workflow chưa được chạy trên GitHub vì chưa có remote/push; chỉ ghi CI pass sau khi có run thành công.

## 8. Lỗi thường gặp

| Dấu hiệu | Kiểm tra/xử lý |
|---|---|
| SDK 10 không tìm thấy | Chạy Install-DotNet, dùng wrapper; kiểm tra `global.json` |
| Docker không tìm thấy/không có Server | Cài/mở Docker Desktop, mở terminal mới, kiểm tra WSL2/Linux containers |
| Port 54329/5173–5175 đã dùng | Dừng đúng tiến trình của bạn hoặc thay config/cổng tương ứng; không kill process không rõ nguồn |
| Ready 503 | Kiểm tra DB đã healthy, `.env`, rồi chạy Migrator; xem lỗi nội bộ local đã redact |
| Restore bị chặn mạng | Kiểm tra truy cập npmjs.org/api.nuget.org hoặc proxy; không bỏ kiểm tra TLS |
| Playwright không thấy browser | Đặt cùng PLAYWRIGHT_BROWSERS_PATH lúc install và test |
| DB tests thiếu env/server | Dùng Test-Database.ps1 sau compose up; không skip rồi ghi pass |

## WSL

Workspace này đang dùng toolchain Windows. Nếu chuyển sang WSL, dùng checkout và toolchain Linux riêng, cài lại dependencies theo lockfile; không dùng chung node_modules hoặc gọi Windows dotnet.exe với dependency Linux. Không cần chuyển WSL để tiếp tục hướng dẫn này.
