# F04 — nghiệm thu bằng tay trong VS Code PowerShell và trình duyệt

Thực hiện trên môi trường **local development**. Checklist này kiểm tra F04 công khai; F04 chỉ hiển thị giá và trạng thái tại lúc đọc, chưa tạo booking/hold/quote. Ghi PASS/FAIL/NOT RUN cho từng mục vào cuối tài liệu. Không sửa trực tiếp dữ liệu development chỉ để tạo trạng thái hiếm; dùng database tạm của test tích hợp cho các nhánh đó.

## 1. Chuẩn bị dịch vụ

Mở Terminal → New Terminal trong VS Code, chọn **PowerShell**, tại repo root:

```powershell
Set-Location 'C:\Users\luong\Desktop\CLong'
git status --short
docker compose ps
```

Nếu PostgreSQL/Mailpit chưa chạy, mở Docker Desktop rồi dùng `npm.cmd run db:up`. Không chạy `docker compose down -v`: lệnh đó xóa volume dữ liệu. Mỗi lệnh sau chiếm một terminal riêng (bỏ qua terminal của dịch vụ đã chạy):

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

Terminal thứ sáu kiểm tra:

```powershell
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

Cả hai phải trả `200`, `ready` có `{"status":"Healthy"}`. `live=200, ready=503` nghĩa là API chạy nhưng DB/baseline chưa sẵn sàng: kiểm tra Docker, `.env` local và migration trên **đúng database local** theo `docs/setup.md` trước khi chạy `npm.cmd run db:migrate`. Không chạy Migrator chỉ vì thấy 503 khi chưa xác định DB.

## 2. Dữ liệu mẫu và biến dùng lại

Nếu đã có business `ACTIVE`, venue `PUBLISHED`, ảnh `READY` và ít nhất hai court `ACTIVE` từ F02/F03, dùng chúng. Nếu chưa có: ở `http://localhost:5174` đăng ký/xác minh owner, tạo business, venue (địa chỉ chọn và xác nhận trên MapTiler), hai court, lịch tuần và giá, ảnh cơ sở, QR/tài khoản; gửi duyệt. Ở `http://localhost:5175` Admin phê duyệt. Owner đăng xuất và đăng nhập lại để thấy **Vận hành sân**. Chi tiết luồng tạo hồ sơ nằm trong `docs/setup.md`.

Để dễ tính tay, chọn một **thứ Hai tương lai** và cấu hình một sân như sau trong Vận hành sân:

| Cấu hình | Giá trị mẫu |
|---|---|
| Giờ thứ Hai | 17:00–20:00 |
| Giá cơ bản | 100.000 VND mỗi ca 30 phút, phủ toàn bộ 17:00–20:00 |
| Giá ưu tiên đúng ngày thứ Hai đã chọn | 18:00–19:00, 150.000 VND mỗi ca, ưu tiên 1 |
| Policy | Block 60 phút, tối thiểu 60 phút, hold 20 phút |
| Sân thứ hai | Giờ 18:00–20:00, giá 80.000 VND/ca, policy có thể là block 90/tối thiểu 90 |

Trong PowerShell, tính thứ Hai kế tiếp; dùng đúng ngày này trong form **Hiệu lực từ/Đến ngày** và khi xem lịch:

```powershell
$today = (Get-Date).Date
$daysUntilMonday = (8 - [int]$today.DayOfWeek) % 7
if ($daysUntilMonday -eq 0) { $daysUntilMonday = 7 }
$date = $today.AddDays($daysUntilMonday).ToString('yyyy-MM-dd')
$date
```

Đổi `Tên venue của bạn` bên dưới thành tên thật. Không lấy phần tử `[0]` nếu có nhiều kết quả trùng tên:

```powershell
$base = 'http://localhost:5080/api/v1/venues'
$venueName = 'Tên venue của bạn'
$q = [uri]::EscapeDataString($venueName)
$found = Invoke-RestMethod "${base}?q=$q"
$found.data.items | Select-Object id,name,address,imageUrl
$venueId = ($found.data.items | Where-Object name -eq $venueName | Select-Object -First 1).id
if (-not $venueId) { throw 'Chưa tìm thấy venue đã publish; kiểm tra trạng thái F02.' }
$detail = (Invoke-RestMethod "${base}/${venueId}").data
$detail.courts | Select-Object id,name,bookingBlockMinutes,minimumBookingMinutes,holdMinutes
```

Lưu `$venueId`, `$date` trong terminal này. Nếu mở terminal khác thì chạy lại phần khai báo biến. Các lệnh `GET` công khai không cần bearer token.

## 3. Checklist giao diện và API công khai

### T01 — chỉ hiện hồ sơ đã publish

1. Trước khi Admin duyệt một hồ sơ F02 mới, mở `http://localhost:5173/venues`, tìm tên venue nháp/đang chờ. Kết quả phải rỗng; API `GET /venues?q=...` trả `200` với `items: []`.
2. Sau khi Admin duyệt, tải lại trang khách. Venue xuất hiện. Mở chi tiết được và thấy các court đang hoạt động.
3. Một business bị suspend hoặc venue chỉ có court inactive cũng phải ẩn và detail trả `404`. Hiện chưa có thao tác UI để đưa các đối tượng này vào trạng thái đó; xem bằng chứng DB tạm `PublicVenuesTests`, ghi **NOT RUN manual** nếu không có fixture riêng.

### T02–T03 — tìm tên, vị trí, bán kính, phân trang và validation

1. Tìm tên và một phần địa chỉ của venue; danh sách phải đúng. Tìm chuỗi vô nghĩa dài vừa phải để thấy thông báo **Chưa có cơ sở phù hợp**. Không có khoảng cách giả trong danh sách tìm chữ.
2. Cho phép định vị trong browser, chọn bán kính 3/5/10/20 km rồi nhấn **Dùng vị trí của tôi**. Tiêu đề đổi thành **Sân gần khu vực đã chọn**, card có khoảng cách. Nếu key MapTiler hoạt động, nhập khu vực, chọn đúng một gợi ý, xem marker và kết quả nearby. MapTiler chỉ chọn tọa độ; PostGIS quyết định bán kính/thứ tự. Kết quả phụ thuộc tọa độ thật của các venue và vị trí đã chọn.
3. Từ chối quyền vị trí hoặc chặn MapTiler trong DevTools Network. Trang vẫn tìm theo tên/địa chỉ được. Nếu gợi ý MapTiler không trả về, ghi lỗi provider cụ thể; đừng coi test mock là provider live PASS.
4. Có ít nhất hai venue published: kiểm tra thứ tự khoảng cách, venue ngoài bán kính không xuất hiện; tăng bán kính thì có thể xuất hiện. Kiểm tra cursor bằng PowerShell:

```powershell
$first = Invoke-RestMethod "${base}?limit=1"
$first.data.items | Select-Object id,name
$cursor = $first.data.nextCursor
if ($cursor) {
    $second = Invoke-RestMethod "${base}?limit=1&cursor=$([uri]::EscapeDataString($cursor))"
    $second.data.items | Select-Object id,name
}
```

Hai trang không được lặp venue; trang cuối có `nextCursor = null`. Nếu chỉ có một venue, ghi **NOT RUN** phân trang. Với hai venue cùng tọa độ, thứ tự tie-break phải ổn định theo ID; nếu không có fixture này, ghi **NOT RUN** tie-break thủ công.

Để đối chiếu nearby ngay tại tọa độ venue mẫu, dùng format số bất biến (dấu chấm) và cần ít nhất hai venue trong bán kính:

```powershell
$lat = $detail.latitude.ToString([cultureinfo]::InvariantCulture)
$lon = $detail.longitude.ToString([cultureinfo]::InvariantCulture)
$near1 = Invoke-RestMethod "${base}/nearby?latitude=$lat&longitude=$lon&radiusMeters=20000&limit=1"
$near1.data.items | Select-Object id,name,distanceMeters
if ($near1.data.nextCursor) {
    $near2 = Invoke-RestMethod "${base}/nearby?latitude=$lat&longitude=$lon&radiusMeters=20000&limit=1&cursor=$([uri]::EscapeDataString($near1.data.nextCursor))"
    $near2.data.items | Select-Object id,name,distanceMeters
}
```

Khoảng cách phải tăng dần, không có ID trùng; venue mẫu gần chính tọa độ của nó sẽ có khoảng cách xấp xỉ 0 m.

Các lệnh âm dưới đây phải trả `400 Bad Request`, `Content-Type: application/problem+json`, `code: VALIDATION_FAILED`:

```powershell
curl.exe -i "${base}?limit=0"
curl.exe -i "${base}?limit=101"
curl.exe -i "${base}?cursor=invalid"
curl.exe -i "${base}?q=a&q=b"
curl.exe -i "${base}?unexpected=1"
curl.exe -i "${base}/nearby?latitude=91&longitude=105"
curl.exe -i "${base}/nearby?latitude=21"
curl.exe -i "${base}/nearby?latitude=21&longitude=105&radiusMeters=0"
curl.exe -i "${base}/nearby?latitude=21&longitude=105&radiusMeters=50001"
curl.exe -i "${base}/nearby?latitude=21&longitude=105&date=$date"
```

Không nhập vị trí hoặc thông tin cá nhân thật khi lưu ảnh màn hình/log nghiệm thu.

### T04 — chi tiết, quyền và dữ liệu không được lộ

Mở chi tiết ở browser không đăng nhập, hoặc dùng:

```powershell
$detail = (Invoke-RestMethod "${base}/${venueId}").data
$detail | ConvertTo-Json -Depth 10
$unknownVenue = [guid]::NewGuid().ToString()
$unknownCourt = [guid]::NewGuid().ToString()
curl.exe -i "${base}/${unknownVenue}"
curl.exe -i "${base}/${venueId}/availability?date=$date&courtId=$unknownCourt"
```

Chi tiết chỉ có tên/địa chỉ/liên hệ **cơ sở**, tọa độ, timezone, ảnh và court/policy. Không được có business contact riêng, số tài khoản, QR, `objectKey`, owner/member/approval ID. Hai request ID không tồn tại trả `404 NOT_FOUND`; venue nháp cũng `404`. Nếu có court thực thuộc venue khác hoặc inactive, dùng ID đó trong `courtId` để xác nhận `404`. DevTools Network cho thấy các request GET F04 không cần `Authorization` header.

### T05–T06 — lưới 30 phút, múi giờ và giá

Ở `http://localhost:5173/venues/<venueId>`, chọn `$date`. Mỗi hàng là một court, cột cách nhau 30 phút. Với sân mẫu 17:00–20:00 phải có 17:00, 17:30, …, 19:30; **không có ca bắt đầu 20:00**. Sân thứ hai mở 18:00 thì ô 17:00/17:30 xám ngoài giờ. Giá ưu tiên 18:00 và 18:30 là 150.000đ/ca; 17:30 và 19:00 là 100.000đ/ca. Chọn 18:00–19:00 để thấy 60 phút, **300.000đ** tham khảo. So với **Xem thử giá** ở portal partner.

Đọc API để xác nhận local/UTC và tất cả sân:

```powershell
$schedule = (Invoke-RestMethod "${base}/${venueId}/availability?date=$date").data
$schedule | Select-Object venueId,date,timezone,stepMinutes,generatedAt
$schedule.courts | ForEach-Object {
    "Sân: $($_.name)"
    $_.slots | Select-Object startsAt,endsAt,startsAtUtc,status,pricePerSlot
}
$yesterday = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')
curl.exe -i "${base}/${venueId}/availability?date=$yesterday"
curl.exe -i "${base}/${venueId}/availability?date=not-a-date"
```

`stepMinutes=30`, `timezone=Asia/Ho_Chi_Minh` nếu venue dùng múi giờ Việt Nam; 18:00 local tương ứng 11:00 UTC trong ngày đó. Ngày quá khứ và chuỗi sai trả `400 VALIDATION_FAILED`. Nếu xem ngày hôm nay, ca đã bắt đầu là `PAST` và không chọn được. Ngày không có giờ hoạt động có thông báo không có giờ/sân, không sinh ca giả.

F03 yêu cầu giá cơ bản phủ toàn bộ giờ mở, nên `NO_PRICE` không tạo được qua form bình thường. Nhánh này được kiểm chứng bằng `PublicVenuesTests` trên database tạm; ghi **NOT RUN manual** nếu không có fixture chuyên biệt. `NO_PRICE` không phải giá 0 và không được chọn.

### T07 và T12 — bảo trì, biên thời gian và cập nhật

1. Trong portal partner, chọn **đúng court**, ngày `$date`, từ 18:00 đến 19:00, nhập lý do từ 5 ký tự, nhấn **Khóa ca bảo trì**. Tạo lại đúng khoảng phải trả `409 SLOT_CONFLICT` ở Network.
2. Trong portal customer, nhấn **Làm mới lịch**. Chỉ ca 18:00–18:30 và 18:30–19:00 trên đúng sân chuyển **Đã kín**; 17:30 và 19:00 còn trống. Ô kín không bấm chọn được; lý do bảo trì không được lộ trên API công khai.
3. Giữ trang khách mở, hủy ca ở partner. Quay lại tab khách để trigger focus hoặc chờ tối đa khoảng 30 giây khi trang hiện; lịch trở lại **Còn trống**. Có thể nhấn Làm mới để xác nhận ngay. Mọi giá/slot chỉ là snapshot khi đọc, không phải giữ chỗ. Trường hợp `BOOKING` allocation trước F05 chỉ kiểm chứng bằng DB test, không có luồng tạo booking UI ở F04.

### T08 — policy và chọn ca

Đặt policy sân 1 block/tối thiểu 60 phút. Chọn một ca 30 phút: tổng giá vẫn hiện nhưng thông báo chưa đủ thời lượng. Chọn thêm ca 30 phút **liền kề, cùng sân**: hiện hợp lệ. Bấm lại ô đã chọn để bỏ chọn. Chọn qua một ô **Đã kín** không tạo được một khoảng liên tục. Đổi sang sân 2: selection cũ phải mất; mỗi lần chỉ chọn một sân. Thử đổi policy sân 2 sang block/tối thiểu 90 phút, tải lại và xác nhận 60 phút chưa hợp lệ, 90 phút liên tiếp hợp lệ. F04 chỉ báo giá tham khảo, **không có nút đặt/giữ chỗ/thanh toán**.

### T09 — ảnh local, QR và F5

Ảnh venue đã tải ở F02 phải hiện ở danh sách và chi tiết công khai. Nhấn F5 và khởi động lại riêng API: ảnh vẫn hiện vì adapter Development lưu private dưới `backend/src/ShuttleBook.Api/.media-local`; F5 không xóa file. Không xóa thư mục hoặc volume khi kiểm tra. Nếu venue không có ảnh thì hiện placeholder, không làm hỏng lịch. QR/tài khoản chỉ có ở giao diện có quyền của F02, không có trong JSON F04 hay route ảnh venue. Có thể xem HTTP status ảnh mà không in byte ảnh:

```powershell
curl.exe -sS -o NUL -w 'HTTP %{http_code}, type %{content_type}\n' "${base}/${venueId}/image"
```

Ảnh hợp lệ ở mode local trả `200 image/*`; venue không công khai/không ảnh hợp lệ trả `404`. Không dùng `curl.exe -i` cho ảnh vì sẽ in dữ liệu nhị phân ra terminal.

### T11 — UX desktop/mobile, lỗi và điều hướng

Mở trang desktop rồi DevTools → Toggle device toolbar để thử chiều rộng điện thoại. Bảng lịch cuộn ngang; header giờ/tên sân và chữ trạng thái vẫn đọc được. Chọn **Xem sân** để chỉ còn một hàng. Đổi ngày và sân, kiểm tra URL có `date`/`courtId`; Back/Forward và F5 khôi phục bộ lọc. Trường hợp list rỗng, trang lỗi mạng và ảnh lỗi phải có thông báo/placeholder; dùng DevTools Network → Offline rồi tải lại, sau đó Online và **Thử lại/Làm mới lịch**. Nếu key MapTiler hoặc tile lỗi, list text vẫn xem được. Kiểm tra bàn phím Tab và nhãn trạng thái; màu không phải tín hiệu duy nhất.

### T13–T14 — schema và hồi quy

F04 không thêm migration nên T13 **không áp dụng**. Kiểm tra nhanh các luồng F01/F02/F03 liên quan: đăng ký/đăng nhập customer, partner và Admin; Admin F5 dưới 30 phút vẫn ở trang Admin; owner còn xem/chỉnh giờ, giá, policy, bảo trì; venue sau duyệt hiển thị công khai và trước duyệt thì không. Bộ test tự động trên PostgreSQL/PostGIS là bằng chứng riêng cho constraint, quyền, migration, race/outbox; thao tác tay tuần tự không chứng minh chống double-booking.

## 4. S3 live — chỉ làm khi có bucket test riêng

Hiện chưa có bucket/credentials nên **T10 và phần S3 của T09 = NOT RUN**. Không đánh dấu F04 DONE chỉ dựa vào ảnh local. Khi đã có bucket non-production private và credentials trên máy (không gửi secret qua chat), cấu hình `Media__Mode=S3`, region/bucket theo `docs/setup.md`, restart API. Tải ảnh mới qua partner: kiểm tra presigned PUT, `complete` thành `READY`, ảnh venue public redirect sang signed GET hạn tối đa 5 phút và hiển thị được; URL object không ký bị từ chối, QR không đọc qua route venue image. Thử sai checksum/type/size, `complete` lặp, owner khác, hết hạn, CORS từ origin partner/customer; ghi status/code và không lưu signed URL/token vào báo cáo. Bucket public hoặc lỗi CORS là **FAIL**, không phải lý do bỏ qua.

## 5. Chạy bộ bằng chứng tự động bổ trợ và chốt nghiệm thu

Các ca khó tạo bằng UI (business suspended, court inactive, NO_PRICE, booking allocation, constraint, migration) phải được hỗ trợ bằng test trên **database tạm**. Sau thao tác tay, nhấn Ctrl+C trong các terminal API/Worker/customer/partner/admin do bạn đã mở; giữ Docker/Mailpit chạy. `test:web` cần cổng 5173–5175 trống, `test:identity-live` còn cần cổng 5080 trống. Script không dừng tiến trình không thuộc nó.

```powershell
npm.cmd run typecheck
npm.cmd run build
.\scripts\dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:api
npm.cmd run test:db
npm.cmd run test:web
npm.cmd run test:identity-live
```

`test:web` có các ca live được SKIP có chủ đích; `test:identity-live` chạy riêng qua API/PostGIS thật trên database tạm, cần Docker/Mailpit và cổng trống. Không dùng kết quả UI mock để thay kết quả DB hoặc S3. Ghi rõ **PASS/FAIL/NOT RUN/BLOCKED**, ngày, browser, lệnh hoặc URL/HTTP status, venue/court test ID (không ghi token/password/QR/signed URL). F04 chỉ được chốt DONE khi acceptance trong `docs/features/F04-public-discovery.md` đạt, bao gồm S3 live nếu vẫn là yêu cầu tích hợp.

| Nhóm | Kết quả tay | Bằng chứng / lỗi / lý do NOT RUN |
|---|---|---|
| T01–T04 công khai, tìm và quyền |  |  |
| T05–T06 lịch, múi giờ, giá |  |  |
| T07–T08 bảo trì và policy |  |  |
| T09 ảnh local/QR |  |  |
| T10 S3 live | NOT RUN khi chưa có bucket |  |
| T11–T12 UX và cập nhật |  |  |
| T13–T14 schema/hồi quy |  |  |

Đối chiếu toàn bộ ID và bằng chứng hiện có ở `docs/testing/F04-test-cases.md`.
