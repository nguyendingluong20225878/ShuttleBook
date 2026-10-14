# Kết quả hoàn thiện Lịch & giá Partner

Ngày bàn giao: 2026-10-13. Phạm vi: thực thi [prompt](../prompts/Partner-schedule-sync-and-redesign.md), theo [contract](../features/Partner-schedule-workspace.md). Bằng chứng local dùng DB thử riêng; không sửa DB development.

## Thay đổi và bằng chứng

| Trước | Sau | Bằng chứng |
|---|---|---|
| Editor nháp/ACTIVE và lựa chọn sân riêng, các mục nối thành trang dài | Một lựa chọn Cơ sở/Sân, ba tab Lịch sân / Giờ & giá / Bảo trì; chung editor theo trạng thái | Browser fixtures desktop/mobile, onboarding live |
| Đổi sân có nguy cơ dùng rows/version cũ | Hydrate đúng sân, abort/generation, dirty guard, kiểm If-Match và refresh version sau lưu | UI A→B→A, stale412, navigation; DB snapshot/concurrency |
| Preview ép thời lượng bội block hiển thị | Minimum120/block60 cho phép120/150/180, cùng engine Customer; preview không giữ chỗ | PostGIS so sánh quote/preview; browser preview |
| Chưa có lịch tình trạng riêng cho Owner | Calendar private theo allocation, quote còn hiệu lực, occurrence và payment thật; lịch sử không bị cắt theo giờ mở mới | 9 ca calendar PostGIS, UI grid/deep link, live desktop/mobile |
| Form tuần nhập ngày lặp, lỗi giá khó định vị | Bảy ngày cố định, giá VND/30 phút và tham khảo theo giờ, báo đoạn thiếu giá tại ngày tương ứng | Browser gap validation desktop/mobile |
| Refresh lỗi dễ gây hiểu nhầm tình trạng sân | Giữ snapshot có cảnh báo khi503; xóa dữ liệu khi mất quyền/phiên; retry riêng | Browser fault/authorization cases |
| Tác vụ lưu cũ có thể để toast thành công khi bắt đầu tác vụ mới | Xóa toast cũ khi bắt đầu, thông báo kết quả mới ở đầu viewport3giây | Recheck6/6 desktop/mobile |

## Lệnh và kết quả

Chạy tại repo root bằng PowerShell. Các lệnh dưới đây là bằng chứng của bản thay đổi này, không dùng PASS của CI/mốc cũ thay thế.

| Kiểm tra | Kết quả | Bằng chứng |
|---|---|---|
| `npm.cmd run typecheck` | PASS, bốn workspace | Compiler TypeScript |
| `npm.cmd run build` | PASS, ba portal | Build sau thay đổi toast; runner live còn phục hồi build local khi kết thúc |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx --configuration Release --no-restore` | PASS,0 warning/error | `.local/partner-schedule-build.log` |
| API tests Release `--no-build --no-restore` | PASS96/96, không skip | `.local/partner-schedule-api-tests.log` |
| PostGIS focused suite Release | PASS21/21, không skip,5m34s | `.local/partner-schedule-backend-final.log` |
| Browser fixtures toàn bộ bốn spec bị ảnh hưởng | PASS80/80, desktop/mobile,47s | QA chạy `Test-Web.ps1` |
| Recheck sau validation giá hở | PASS6/6, gồm hai ca mới desktop/mobile | Chọn gap/draft-save/dirty; không gọi đây là một lượt toàn bộ82ca |
| Recheck sau clearToast | PASS6/6,5,1s | Preview/thành công/thất bại, self-confirm và gap desktop/mobile |
| Recheck nút với mã đơn dài | PASS2/2, desktop/mobile,5,7s; build3PASS | `.local/partner-schedule-compact-tests.log`, `.local/partner-schedule-compact-build.log`; bounding box nút nằm trong card, deep link đúng anchor |
| Browser→API→Worker→PostGIS/Mailpit | PASS8/8, desktop/mobile,1,2phút; exit0 | `.local/partner-schedule-live-verified.log`; DB tạm đã drop đúng tên, build thường đã phục hồi |
| CI trên code mới, S3/EC2/SMTP production | NOT RUN | Gate riêng; không commit/push/merge/deploy trong mốc này |

Lệnh PostGIS:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --filter "FullyQualifiedName~Schedule_configuration|FullyQualifiedName~Calendar_|FullyQualifiedName~F03_active_owner|FullyQualifiedName~Draft_approval_revision|FullyQualifiedName~Owner_confirms_court|FullyQualifiedName~Court_confirmation_validates|FullyQualifiedName~Legacy_pending_court|FullyQualifiedName~Unrelated_pending_sensitive|FullyQualifiedName~Active_court_name_and_price" --configuration Release
```

Lệnh fixtures:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Web.ps1 tests/web/partner-schedule.spec.ts tests/web/partner-workspace.spec.ts tests/web/ui-partner-history.spec.ts tests/web/f02-self-service.spec.ts --workers=2 --trace=off
```

Lệnh live:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release
```

## Review, lỗi đã sửa và giới hạn

- Review backend chéo: bổ sung DST ambiguity/booking horizon; GET nháp đọc đủ rules khi trạng thái thay đổi; kiểm lại bằng PostGIS.
- Review UI độc lập: giữ snapshot503, retry bảo trì, version parent sau save, quyền SUSPENDED, Back khi chưa lưu, nhãn payment và giờ hiển thị. Đã sửa và browser recheck.
- Fixture lượt đầu74ca:66PASS/8FAIL. Hai ca phát hiện version parent cũ; sáu lỗi fixture/locator đã cập nhật theo UI mới. Lượt toàn bộ cuối80PASS thay bằng chứng cũ.
- Live hai lượt đầu đều6PASS/2FAIL ở bước lưu/chuyển trang và locator của UI mới; chưa đến đủ đoạn calendar/payment nên không dùng chúng nghiệm thu tích hợp. Sửa chờ HTTP200 + nút lưu bật lại, selector giá theo ngày và định dạng tiền; đồng thời sửa toast cũ gây nhầm kết quả thao tác.
- Live cuối PASS8/8: Customer đăng ký/OTP Mailpit, Admin session, Partner onboarding/duyệt hồ sơ, sân bổ sung tự xác nhận, lịch/giá/preview, tạo/hủy bảo trì, đặt vãng lai/báo chuyển/xác nhận tiền, cố định và lịch buổi thứ hai đúng payment anchor. Calendar kiểm `no-store`, không có trường PII, đúng tiền buổi và toàn kỳ. Không dùng fixture cho các kết quả API/database này.
- Ảnh live cho thấy mã đơn dài khiến nút chi tiết quá rộng trên mobile: rút nhãn thành Xem đơn, giữ mã đầy đủ trong aria-label/title và deep link. Recheck2/2PASS với mã dài và bounding box desktop/mobile; build3PASS. Sau thay đổi nhãn này không chạy lại cả live suite vì không đổi command/API/route/accessible name.
- Calendar cập nhật khoảng15giây khi tab đang hiển thị; không phải stream realtime. Tối đa200sân/5000events với lỗi rõ; không cắt dữ liệu âm thầm.
- MapTiler live dùng fixture để chọn địa chỉ; không nghiệm thu dịch vụ bản đồ thật. Private media local không thay bằng chứng S3 thật.
- Đã xem ảnh browser desktop/mobile tại `.local/partner-schedule-screenshots/`; không tràn ngang cả trang, lưới có vùng cuộn riêng. Bàn phím/tab được kiểm bằng browser; kiểm định cá nhân theo [hướng dẫn U01–U16](Partner-schedule-manual.md) vẫn nên thực hiện.
- Không có migration mới trong mốc schedule. Migration bổ sung cơ sở/sân của các mốc trước vẫn là điều kiện DB local.

## Cách dùng sau bàn giao

Khởi động lại API bằng `npm.cmd run dev:api`, mở Partner/F5 tại `http://localhost:5174/#/schedule`. [Hướng dẫn thao tác và tự nghiệm thu](Partner-schedule-manual.md) giải thích từng tab, xác nhận sân nháp, đối chiếu tiền, bảo trì và xử lý lỗi.
