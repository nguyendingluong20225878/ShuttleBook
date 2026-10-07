# Nghiệm thu F06 bằng terminal VS Code và trình duyệt

F06 nối luồng **tạo đơn F05 → khách báo chuyển → chủ sân đối chiếu → xác nhận / yêu cầu bổ sung / từ chối cuối cùng**. Trang hướng dẫn là checklist để bạn tự nghiệm thu; kết quả tự động đã chạy nằm trong `docs/testing/F06-test-cases.md`. Chỉ ghi PASS sau khi đã thực hiện và thấy kết quả mong đợi.

Chính sách theo nghiệm thu 2026-10-07: bỏ ô mã giao dịch ở customer/owner; khách có thể gửi ảnh chụp màn hình chuyển khoản và ghi chú, cả hai không bắt buộc. API vẫn nhận mã tùy chọn cho client/lịch sử cũ. Chủ sân chỉ xác nhận số tiền đúng bằng tổng snapshot của đơn. Sau 30 phút từ lần báo chuyển đầu tiên, Worker nhắc owner và Admin một lần, kể cả khi đang cần bổ sung. Bổ sung không reset mốc đó; đã báo chuyển thì không hết hạn theo hạn giữ chỗ cũ. Đặt vãng lai được thêm từng ca 30 phút sau minimum sân: minimum120 nhận4/5/6/7... ca liên tiếp.

Sử dụng hồ sơ, tài khoản và ảnh thử riêng trên DB local. Không cần chuyển tiền thật: dùng ảnh ghi rõ “F06 TEST” và chủ sân thử nghiệm đối chiếu dữ liệu thử. Hệ thống không tự kiểm tra tài khoản ngân hàng; owner phải đối chiếu trước khi xác nhận trong sử dụng thực tế. S3 thật tiếp tục hoãn; ảnh dùng private local adapter.

## 1. Chuẩn bị trong PowerShell của VS Code

Trong terminal tại root repository:

```powershell
Set-Location C:\Users\luong\Desktop\CLong
npm.cmd run db:up
```

Lệnh trên khởi động PostgreSQL/PostGIS và Mailpit bằng Docker. Nếu báo không kết nối được Docker daemon, mở Docker Desktop rồi chạy lại. Không dùng `down -v` hay reset dữ liệu.

Kiểm tra `.env` local có chế độ ảnh local:

```dotenv
Media__Mode=Local
```

Không in toàn bộ `.env` ra terminal/chụp màn hình vì file có secrets. Lệnh migrate dưới đây là bước **bạn chủ động áp dụng F06 vào DB local**, thêm evidence/decision/metadata/FK, ràng buộc PAID đúng expectedAmount và đổi tên actor idempotency, giữ dữ liệu F05. Agent đã dùng DB test tạm cho kiểm thử; DB local không tự được nâng cấp chỉ vì code đã sửa.

Dừng API/Worker đang chạy bằng Ctrl+C trong terminal của chính chúng, sau đó:

```powershell
npm.cmd run db:migrate
npm.cmd run build
```

Mở các terminal riêng; mỗi terminal đều đứng tại root dự án:

| Terminal | Lệnh | Đang làm gì |
|---|---|---|
| API | `npm.cmd run dev:api` | Chạy endpoint booking, proof, quyết định và notification trên 5080 |
| Worker | `npm.cmd run dev:worker` | Expiry đơn chưa báo chuyển, dispatch outbox, cảnh báo SLA |
| Customer | `npm.cmd run dev:customer` | Giao diện khách trên 5173 |
| Partner | `npm.cmd run dev:partner` | Giao diện chủ sân trên 5174 |
| Admin | `npm.cmd run dev:admin` | Giao diện quản trị trên 5175, xem cảnh báo SLA |

Sau thay đổi code hoặc migration, phải chạy lại API **và Worker**. Chạy API cũ sẽ không có endpoint mới; Worker cũ sẽ chưa xử lý event/SLA mới.

Terminal kiểm tra riêng:

```powershell
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

Mong đợi hai endpoint HTTP 200, `Healthy`. Live chỉ xác nhận API đang chạy; ready kiểm tra DB/migration. `Unable to connect` là API chưa mở/khác cổng; ready 503 là API chạy nhưng DB chưa sẵn sàng. Xem lỗi ở terminal API, không tiếp tục đánh giá F06 nếu ready chưa Healthy.

## 2. Dữ liệu và cửa sổ thử

Chuẩn bị:

- Owner A ACTIVE, business A ACTIVE, cơ sở PUBLISHED, sân ACTIVE, có giờ mở/giá/QR READY từ F02–F03.
- Customer A và B ACTIVE. Owner B thuộc business khác để thử scope. Admin đã bootstrap/đăng nhập được.
- Ngày tương lai trong 60 ngày, ca liên tiếp đúng minimum/block của sân. Chia các đơn thử vào giờ hoặc sân khác nhau để không bị giữ trùng.
- Một ảnh PNG/JPEG/WebP thử ≤5 MB; không dùng biên lai thật. Có thể lấy ảnh mẫu bạn tự tạo. File PDF/SVG/HEIC không được chấp nhận.

Dùng browser profile/cửa sổ ẩn danh riêng cho customer A, customer B và owner B. Owner A trên 5174, Admin trên 5175 có origin khác customer nên có thể mở cùng profile; không đăng nhập A/B cùng origin trong cùng session nếu cần so sánh quyền.

Ghi mã và URL cho mỗi đơn: A xác nhận, B bổ sung, C từ chối, D hết hạn, E SLA. Không dùng lại đơn terminal cho một luồng mới.

## 3. T01 — happy path: ảnh local → báo chuyển → xác nhận

1. Customer A mở `http://localhost:5173/venues`, tìm cơ sở A và chọn sân/ngày/ca liên tiếp.
2. Nhấn **Tiếp tục đặt vãng lai**, đăng nhập nếu cần. Review đúng sân, ngày, từng ca 30 phút, tổng tiền và quote 2 phút.
3. Nhấn **Xác nhận tạo đơn**. Ghi mã đơn A. Detail có QR snapshot, thông tin nhận tiền che số tài khoản và countdown hold của sân. Lịch công khai lúc này đã kín đúng ca đó.
4. Nhấn **Đã chuyển khoản**. Form chỉ có **Ảnh chụp màn hình chuyển khoản (không bắt buộc)** và ghi chú tùy chọn; không có ô mã giao dịch. Chọn ảnh hợp lệ và có thể bỏ ghi chú để thử luồng chỉ gửi ảnh.
5. Nhấn **Gửi báo chuyển khoản** một lần. Trong DevTools Network có thứ tự: POST `proof-uploads/presign` → PUT URL upload được ký → POST `uploads/{id}/complete` → POST `transfer-evidence`. Report gửi `proofUploadId`, không gửi raw object key hay URL ảnh.
6. Mong đợi **Chờ xác nhận**, lịch sử có ảnh/ghi chú nếu đã gửi và thời điểm báo; không tự sinh mã giao dịch. Countdown giữ chỗ và QR hướng dẫn chuyển tiền được ẩn. Dòng “Sân vẫn được giữ…” hiển thị. Khách chưa phải Đã xác nhận/PAID.
7. Owner A mở **Thông báo**, nhấn **Tải lại thông báo** nếu cần, thấy báo chuyển cho đúng cơ sở/đơn. Nhấn **Xem đơn đặt sân** hoặc vào **Đơn đặt sân**, chọn **Cơ sở xem đơn**, mở **Xem đơn {mã}**.
8. Detail owner có mã, cơ sở/sân, giờ/múi giờ, tiền snapshot, khách che contact và **Lịch sử đối chiếu**. Nhấn **Xem biên lai**: ảnh đọc private bằng quyền owner; **Ẩn biên lai** đóng ảnh.
9. Ở **Quyết định thanh toán**, chọn **Xác nhận thanh toán**. Kiểm tra **Số tiền thực nhận (đ)** đúng tổng đơn; không có ô mã giao dịch đối chiếu. Ghi chú tùy chọn. Nhấn **Xác nhận đã nhận đủ tiền**.
10. Mong đợi owner và customer đều **Đã xác nhận** sau làm mới/polling. Customer có thông báo xác nhận và lịch sử owner quyết định. DB booking `CONFIRMED`, payment `PAID`, allocation `RESERVED`; người/thời gian/số tiền xác nhận được lưu.

Đây là kết thúc thành công. Không có hủy/đổi lịch/check-in/check-out/completed. Lịch công khai vẫn kín ca đã xác nhận. Polling khoảng 5 giây khi tab hiện, có thể focus/làm mới để kiểm tra ngay; không phải push realtime tuyệt đối.

## 4. T02/T12/T13 — validation, ảnh private và F5

- Bỏ ảnh và ghi chú rồi gửi đơn mới: vẫn báo chuyển thành công với body `{}`, không có request upload. Owner có thể yêu cầu khách bổ sung ảnh nếu cần đối chiếu.
- Ghi chú tối đa 1.000 ký tự. API cũ có thể gửi mã giao dịch tùy chọn tối đa100; bỏ/null/trắng đều thành null, số/field lạ/chuỗi quá dài vẫn400. Backend luôn kiểm tra nếu client bị sửa.
- Chọn SVG/PDF/ảnh >5 MB: UI báo lỗi và khóa submit. Nhấn **Bỏ ảnh biên lai** để gửi không ảnh, hoặc chọn lại PNG/JPEG/WebP hợp lệ. Đơn chưa thay đổi khi form lỗi.
- Ảnh đúng đuôi nhưng nội dung/MIME/checksum sai: backend từ chối upload/complete; không report thành công bằng tệp chưa READY. Ca checksum/magic/FK cần test tự động ở mục 13 để có bằng chứng DB thật.
- Sau report, customer nhấn F5: cần đăng nhập lại theo session F01 hiện hành. Đăng nhập xong trở lại cùng đơn, có lịch sử/ảnh cũ; không tạo đơn/report tự động. Token/reference/ảnh không lưu localStorage/sessionStorage hay URL.
- API restart không làm mất ảnh đã lưu local. File nằm dưới `.media-local` của API content root (khi chạy bằng script dự án: `backend/src/ShuttleBook.Api/.media-local`), metadata/upload ID/scope/checksum nằm PostgreSQL. Xóa thư mục/file local sẽ làm ảnh không tải được; F5 không xóa file này. PostGIS không chứa binary ảnh.
- Owner đúng scope chỉ xem proof sau khi proof đã gắn evidence; ảnh upload nhưng chưa gửi báo chuyển không mở quyền cho owner. Customer B, owner B, guest và Admin không được đọc proof của A. Admin nhận cảnh báo SLA không được cấp quyền xem biên lai.
- Copy URL `/api/v1/uploads/{id}/view` vào tab guest: 401; URL chỉ là endpoint cần bearer, không phải ảnh public. Không chia sẻ bearer/signed PUT URL trong báo cáo.

## 5. T07 — yêu cầu bổ sung sau hạn giữ chỗ cũ

1. Tạo đơn B và customer báo chuyển hợp lệ trước deadline.
2. Owner mở B, chọn **Quyết định → Yêu cầu bổ sung**, **Lý do đối chiếu → Cần bổ sung bằng chứng**, nhập **Nội dung gửi khách** như “Bổ sung ảnh chụp màn hình chuyển khoản rõ ràng”, nhấn **Gửi yêu cầu bổ sung**.
3. Customer thấy **Cần bổ sung bằng chứng**, lý do owner và lịch sử lần báo đầu. Sân vẫn kín; Worker không đổi đơn sang EXPIRED.
4. Đợi qua deadline giữ chỗ ban đầu. Có thể dùng sân thử với hold=5 phút, ghi lại policy cũ để khôi phục. Không sửa timestamp trực tiếp trong DB local để giả lập nghiệp vụ.
5. Customer nhấn **Bổ sung bằng chứng**, chọn ảnh chụp màn hình hoặc ghi chú tùy chọn, nhấn **Gửi bổ sung bằng chứng**; không nhập mã giao dịch.
6. Mong đợi trở lại **Chờ xác nhận**, lịch sử giữ cả INITIAL và SUPPLEMENT. Không yêu cầu tạo đơn mới/chuyển lại toàn bộ tiền; first reportedAt/SLA gốc không reset. Owner nhận thông báo bổ sung.
7. Owner đối chiếu và xác nhận như T01. Lịch sử giữ cả quyết định yêu cầu bổ sung và quyết định xác nhận cuối.

## 6. T08/T09 — tiền lệch và từ chối cuối cùng

**Tiền lệch:** trên đơn đang chờ, nhập tiền ít hơn/thừa hơn tổng 1đ. UI báo chưa khớp, không xác nhận. Nếu gửi API trực tiếp, 409 `PAYMENT_AMOUNT_MISMATCH`; status/version/evidence/decision/allocation/outbox không đổi. Owner chuyển sang **Yêu cầu bổ sung** nếu đơn đang chờ xác nhận; đơn đã NEEDS_REVIEW tiếp tục chờ khách bổ sung hoặc được đối chiếu trực tiếp.

**Từ chối:** tạo đơn C, báo chuyển; owner chọn **Từ chối cuối cùng**, chọn lý do và nhập nội dung. Chưa chọn checkbox giải phóng sân thì submit bị chặn. Đọc và chọn checkbox xác nhận quyết định cuối, nhấn **Xác nhận từ chối cuối cùng**.

Mong đợi customer thấy **Không xác nhận giao dịch** và lý do, owner history có quyết định. DB `PAYMENT_REJECTED/REJECTED/RELEASED` trong một transaction. Làm mới lịch: ca cũ Available nếu không có khóa/đơn khác. Customer B có thể tạo booking mới cùng ca. Booking C cũ không thể confirm lại hoặc tự hồi sinh. F06 không tự hoàn tiền hay tạo giao dịch hoàn tiền.

## 7. T03/T04 — chưa báo chuyển thì mới hết hạn

Tạo đơn D, không nhấn báo chuyển. Khi hết deadline và Worker đã chạy một chu kỳ, detail chuyển **Đã hết hạn**, QR/form bị ẩn, lịch được giải phóng. Nếu deadline đã qua nhưng Worker chưa chạy, nút báo chuyển vẫn bị khóa; API báo lần đầu tại/sau deadline trả 409 `PAYMENT_DEADLINE_EXPIRED`, không hồi sinh đơn.

So sánh với B/E đã báo: qua deadline cũ vẫn giữ sân. Không coi owner chậm trả lời là khách chưa thanh toán. Exact deadline và report/expiry race được chứng minh bằng TimeProvider/row lock test DB, thao tác tay tuần tự không thay thế kiểm thử concurrency.

## 8. T15 — SLA 30 phút, thông báo và Worker restart

Tạo đơn E và báo chuyển trước deadline; ghi thời điểm lần báo đầu. Không confirm/reject trong 30 phút. Có thể yêu cầu bổ sung để kiểm tra nhánh NEEDS_REVIEW. Giữ Worker chạy.

- Tại `firstReportedAt + 30 phút` và sau một chu kỳ Worker, owner thấy **Quá hạn đối chiếu**/thông báo nhắc, Admin trên 5175 có mục **Thông báo** và nhắc đối chiếu. Nếu chưa thấy, dùng **Làm mới thông báo** của Admin hoặc focus/làm mới owner.
- Customer có thể thấy **Đơn đang chờ đối chiếu quá 30 phút** ngay khi API tính thấy quá hạn, kể cả Worker đang dừng. Dòng này mô tả thời gian chờ, chưa chứng minh thông báo đã được gửi. Chỉ kiểm PASS cảnh báo khi notification đã xuất hiện và outbox được xử lý.
- Customer/owner vẫn pending hoặc review, allocation vẫn RESERVED; không tự PAID, EXPIRED hoặc RELEASED.
- Reload nhiều lần hoặc Ctrl+C rồi chạy lại Worker: chỉ một cảnh báo cho mỗi recipient. Customer bổ sung không reset lần báo đầu hoặc phát thêm mốc 30 phút mới.
- Admin chỉ liên hệ owner, không có quyền xác nhận/từ chối/đọc proof thay owner. Đơn terminal trước mốc không có cảnh báo mới.

Không muốn đợi 30 phút: chạy ca DB thật với đồng hồ kiểm soát trên **database test tạm**:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build --filter "FullyQualifiedName~PaymentConfirmationTests.Thirty_minute_SLA|FullyQualifiedName~PaymentConfirmationTests.SLA_scan"
```

Đó là kiểm thử tự động bổ trợ, không ghi là bạn đã chờ/nghiệm thu tay SLA 30 phút. Build backend trước nếu binary chưa có/bản cũ; không chạy build khi test/backend đang khóa DLL.

## 9. T11/T18/T19 — scope, F5, polling và giao diện

- Customer B login rồi mở URL đơn A: NOT_FOUND, không thấy giá/QR/proof của A.
- Owner B login mở `#/bookings?bookingId={A}`: không tìm thấy trong quyền. Operator pending onboarding không vận hành đơn; Admin không đóng vai owner ở endpoint quyết định.
- Owner A đổi business/cơ sở/filter trong lúc tải: dữ liệu/detail/form cũ được bỏ, không gửi quyết định cho đơn ở scope trước. URL deep link qua login/F5 về đúng đơn nếu còn quyền.
- Owner thử decision ở hai tab trên cùng đơn: sau một tab xử lý, tab kia lệnh mới với version cũ nhận 412. UI khóa action, yêu cầu **Tải lại chi tiết**, không tự gửi lại với version mới.
- Customer form đang nhập ghi chú/chọn ảnh, focus hoặc chờ polling: draft không mất. Lỗi 412/409 yêu cầu **Tải lại để kiểm tra đơn**, chỉ GET; khách phải tự nhấn submit lần nữa nếu state còn cho phép.
- Report mất response do mạng: khi chưa sửa payload, nhấn lại submit dùng đúng key/body/version của lần gửi trước. Cùng ý định đã gửi được retry sau deadline cũ để kiểm tra lần gửi có commit hay chưa; sửa mã/ảnh/ghi chú sau hạn thì bị khóa. UI không tự báo chuyển khi polling hay hết countdown.
- Sau báo chuyển thành công, response GET với version cũ không được hiển thị lại form/QR hoặc xóa lịch sử mới. Ca này có browser test kiểm soát response; thao tác tay chỉ kiểm không thấy đơn nhảy lùi trạng thái khi focus/làm mới.
- Sau report, mở **Thông báo** ở customer: unread count, **Đánh dấu đã đọc**, **Xem đơn đặt sân** đúng đơn. F5/login trở lại `/me/notifications`. Thông báo hồ sơ F02 của partner vẫn hiển thị cùng các thông báo đặt sân.
- Thu hồi membership/suspend thuộc kiểm thử DB tự động; không cập nhật DB local bằng SQL để chạy ca này. API luôn kiểm quyền DB hiện tại, không tin accountType hay nút ẩn.
- Nếu đọc lại đơn trả 401/403/404, chi tiết/lịch sử/ảnh private đã tải phải được bỏ khỏi màn hình. Lỗi kết nối tạm thời có thể giữ chi tiết cũ để bạn thử lại; lỗi mất quyền không giữ dữ liệu đó. Browser test và DB scope test kiểm riêng hai trường hợp.
- Kiểm tra 375px/mobile và desktop: Tab đến form/button, label đọc được, lỗi bằng chữ; ảnh vừa màn hình, không tràn ngang toàn trang. Tab ẩn không poll mạng liên tục; focus tải lại server state. Không tự POST khi mount/back/F5.

## 10. Kiểm tra API/retry bằng PowerShell, không in token

Phần này dành cho các ca HTTP mà form đã chặn. Tất cả lệnh vẫn ở root, API local 5080. Dùng tài khoản thử đang ACTIVE. Login tạo một session API riêng với session trình duyệt; token chỉ nằm trong biến terminal.

```powershell
$api = 'http://localhost:5080/api/v1'
$customerContact = Read-Host 'Email customer thử'
$customerPassword = Read-Host 'Mật khẩu customer thử' -AsSecureString
$credential = [Net.NetworkCredential]::new('', $customerPassword)
$customerAuth = Invoke-RestMethod -Method Post -Uri "$api/auth/login" -ContentType 'application/json' -Body (@{
    contactType = 'email'; contact = $customerContact; password = $credential.Password
} | ConvertTo-Json -Compress)
Remove-Variable credential, customerPassword
$customerHeaders = @{ Authorization = "Bearer $($customerAuth.data.accessToken)" }
$demoBookingId = Read-Host 'Booking ID của đơn thử mới (UUID trong URL)'
$detail = (Invoke-RestMethod -Uri "$api/bookings/$demoBookingId" -Headers $customerHeaders).data
$detail | Select-Object bookingId, bookingNo, status, version, amount, paymentDeadline
```

Nếu dùng tài khoản phone, thay contactType bằng `phone` và nhập số E.164. Không gõ `$customerAuth`/`$customerHeaders` để in chúng, không đưa password vào lệnh literal/chat. Hết access token thì refresh bằng refreshToken hiện tại hoặc login lại; tránh thử retry với bearer hết hạn rồi nhầm 401 là lỗi idempotency.

Helper đọc status/problem body cho PowerShell 5.1:

```powershell
function Invoke-F06Json {
    param([string]$Uri, [hashtable]$Headers, [string]$Body)
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri $Uri -Headers $Headers -ContentType 'application/json' -Body $Body -ErrorAction Stop
        [pscustomobject]@{ Status = [int]$response.StatusCode; Body = ($response.Content | ConvertFrom-Json) }
    } catch {
        if (-not $_.Exception.Response) { throw }
        $response = $_.Exception.Response
        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        [pscustomobject]@{ Status = [int]$response.StatusCode; Body = ($text | ConvertFrom-Json) }
    }
}
```

Đơn phải còn AWAITING_TRANSFER/còn hạn cho report lần đầu. Gửi và replay:

```powershell
$reportKey = [guid]::NewGuid().ToString()
$reportHeaders = @{ Authorization = $customerHeaders.Authorization; 'Idempotency-Key' = $reportKey; 'If-Match' = ('"{0}"' -f $detail.version) }
$reportBody = @{} | ConvertTo-Json -Compress
$first = Invoke-F06Json -Uri "$api/bookings/$demoBookingId/transfer-evidence" -Headers $reportHeaders -Body $reportBody
$first.Status
$first.Body.data | Select-Object bookingId, status, version
$again = Invoke-F06Json -Uri "$api/bookings/$demoBookingId/transfer-evidence" -Headers $reportHeaders -Body $reportBody
$again.Status
$again.Body.data | Select-Object bookingId, status, version
```

Mong đợi 200 hai lần, cùng booking ID, chỉ một evidence/transition. Replay dùng lại If-Match cũ vẫn được vì lookup idempotency xảy ra trước stale check. Sau owner xử lý, replay có thể trả trạng thái hiện tại của cùng đơn; không tạo evidence mới.

Các biến thể:

- Cùng key, sửa note/proof/booking ID: 409 `IDEMPOTENCY_KEY_REUSED` khi vẫn đúng resource scope. Client cũ gửi bankReference khác cũng bị chặn như trước.
- Key mới nhưng If-Match cũ: 412 `PRECONDITION_FAILED`; key mới/version hiện tại trên đơn đã report: 409 `STATE_CONFLICT`.
- Thiếu If-Match: 428 `PRECONDITION_REQUIRED`; version không có dấu ngoặc kép hoặc field lạ/raw object key: 400.
- Confirm trước khi customer report: 409 `STATE_CONFLICT`. Owner confirm lệch 1đ: 409 `PAYMENT_AMOUNT_MISMATCH`, không mutation. Dùng owner login vào biến/session riêng và endpoint `/operator/bookings/{id}/confirm-payment`, payload `{confirmedAmount,note?}`, If-Match version hiện tại. Mã cũ có thể gửi bankReference tùy chọn.
- Không tự đổi version để retry một quyết định owner chưa xem lại. Khi 412, GET lại detail, xem lịch sử/state rồi mới tạo key/ý định mới.

Không cần tự viết script gửi đồng thời nhiều lệnh tài chính vào DB development. Ca same-key song song/report-expiry/confirm-reject đã có runner DB tạm ở mục 13.

## 11. Đối chiếu DB chỉ đọc

Các lệnh dưới chỉ SELECT; không sửa timestamp/status/allocation. Đọc DB/user từ container, không cần in password:

```powershell
$dbName = (docker compose exec -T postgres printenv POSTGRES_DB).Trim()
$dbUser = (docker compose exec -T postgres printenv POSTGRES_USER).Trim()
$demoBookingId = Read-Host 'UUID booking thử cần kiểm tra'
if ($demoBookingId -notmatch '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$') { throw 'Booking ID không hợp lệ' }
$sql = @"
SELECT b.booking_no, b.status AS booking_status, b.version,
       p.status AS payment_status, p.expected_amount, p.confirmed_amount,
       p.first_reported_at, p.last_reported_at, p.confirmed_at,
       p.confirmed_by IS NOT NULL AS has_confirmer,
       p.confirmation_alerted_at, a.status AS allocation_status
FROM bookings b JOIN payments p ON p.booking_id=b.id
JOIN court_allocations a ON a.id=b.allocation_id
WHERE b.id='$demoBookingId'::uuid;
SELECT kind, reported_at, proof_upload_id IS NOT NULL AS has_proof
FROM payment_evidence WHERE booking_id='$demoBookingId'::uuid ORDER BY reported_at,id;
SELECT resolution, reason_code, confirmed_amount, decided_at
FROM payment_decisions WHERE booking_id='$demoBookingId'::uuid ORDER BY decided_at,id;
SELECT event_type, count(*) AS events, count(processed_at) AS processed
FROM outbox_messages WHERE entity_id='$demoBookingId'::uuid GROUP BY event_type ORDER BY event_type;
SELECT m.event_type, count(n.id) AS notifications, count(DISTINCT n.user_id) AS recipients
FROM outbox_messages m LEFT JOIN notifications n ON n.outbox_message_id=m.id
WHERE m.entity_id='$demoBookingId'::uuid GROUP BY m.event_type ORDER BY m.event_type;
"@
docker compose exec -T postgres psql -U $dbUser -d $dbName -c $sql
```

Mong đợi:

| Luồng | Booking | Payment | Allocation |
|---|---|---|---|
| Chưa báo, còn hạn | AWAITING_TRANSFER | AWAITING_TRANSFER | RESERVED |
| Đã báo/bổ sung | AWAITING_OWNER_CONFIRMATION | TRANSFER_REPORTED | RESERVED |
| Owner cần bổ sung | NEEDS_REVIEW | NEEDS_REVIEW | RESERVED |
| Owner xác nhận | CONFIRMED | PAID | RESERVED |
| Từ chối cuối cùng | PAYMENT_REJECTED | REJECTED | RELEASED |
| Chưa báo và đã hết hạn | EXPIRED | EXPIRED | RELEASED |

Version tăng đúng một cho mỗi transition thật; cảnh báo SLA không đổi version. Replay không tăng evidence/decision/outbox. Notification cần thời gian Worker dispatch; trước processed_at thì chưa nhận được notification là bình thường. Một event có nhiều owner/Admin đủ quyền có nhiều recipient; mỗi `(outbox message,user)` chỉ một notification.

## 12. Lỗi thường gặp

| Hiện tượng | Kiểm tra và cách xử lý |
|---|---|
| 404 endpoint mới | API cũ chưa restart hoặc URL thiếu `/api/v1`; lỗi business scope cũng trả404 nên phân biệt response code/route |
| Lỗi relation/column F06 chưa tồn tại | Chưa migrate đúng DB local; Ctrl+C API/Worker, migrate rồi restart |
| Báo chuyển thành công nhưng owner chưa nhận | Worker mới phải chạy, đúng DB với API; kiểm tra outbox processed_at/attempts. Focus/làm mới portal |
| 401/SESSION_REQUIRED | Phiên hết hạn/đăng nhập lại; customer F5 về login là policy hiện tại |
| 403 | Sai accountType hoặc chưa ACTIVE; customer không confirm, Admin không đọc proof/confirm |
| 409 deadline/state | Đã qua hạn hoặc đơn đã xử lý; tải lại, không tạo report/decision tự động |
| 412 | Version cũ; tải lại và xem quyết định mới trước submit thủ công |
| 503 MEDIA_UNAVAILABLE | Kiểm tra Media__Mode=Local cho local; restart API. Không bật S3 khi chưa có bucket/credentials |
| Ảnh không lên sau report | File local thiếu/bị xóa hoặc endpoint private lỗi; dùng Tải lại ảnh/customer, Xem biên lai/owner; không đổi URL thành public |
| Browser test bị chặn cổng5173–5175 | Dừng dev web bằng Ctrl+C trong terminal của chúng trước test; không chạy hai runner browser cùng lúc |
| DLL bị khóa khi build backend | Dừng API/Worker/test của chính bạn trước build; không kill process tùy tiện |

## 13. Test tự động bổ trợ và ghi biên bản

Trong root PowerShell, chạy tuần tự, Docker đang mở; dừng dev web trước browser suite. API port5081 dùng riêng nếu 5080 của bạn đang chạy; runner sẽ kiểm tra cổng và tạo/dọn DB test tạm. Đọc script trước khi đổi tham số.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:api
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --no-build --filter "FullyQualifiedName~PaymentConfirmationTests"
npm.cmd run typecheck
npm.cmd run build
npm.cmd run test:web -- tests/web/f06-customer.spec.ts tests/web/f06-partner.spec.ts --workers=2
npm.cmd run test:identity-live -- -ApiPort 5081
```

UI mock chứng minh form/error/F5/polling, không chứng minh transaction PostgreSQL. DB suite dùng PostGIS thật và đồng hồ/lock kiểm soát. Live suite có customer/partner/API/Worker thật; xem testcase/runtime evidence để biết chính xác luồng đã chạy. S3 provider thật vẫn NOT RUN, không gộp với ảnh local PASS.

API trả thêm chuỗi `amountExact`, `expectedAmountExact`, `confirmedAmountExact` cho số VND lớn; màn chi tiết/lịch sử sử dụng giá trị chính xác, không làm tròn theo giới hạn số JavaScript. Ca 18 chữ số dùng dữ liệu browser/DB test tạm, không cần đổi bảng giá của cơ sở thật để nghiệm thu.

Biên bản tay: ghi ngày, browser, mã đơn/cơ sở thử, ca T01–T20 đã thực hiện, HTTP/status DB, PASS/FAIL/NOT RUN và lỗi còn mở. Không chụp token/password/ảnh/signed URL thật. Ghi riêng các ca tự động bổ trợ với lệnh và kết quả; không đánh dấu concurrency hoặc SLA tay PASS chỉ bằng đọc source. Chỉ chốt F06 DONE khi acceptance cần thiết đạt và không còn lỗi nghiêm trọng.
