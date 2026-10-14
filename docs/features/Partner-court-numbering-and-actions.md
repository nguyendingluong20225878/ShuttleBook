# Số sân trong từng cơ sở và nút Partner

Ngày 2026-10-12. Phạm vi theo yêu cầu người dùng: xác minh một Chủ quản lý nhiều cơ sở, mỗi cơ sở có nhiều sân thuê; sân đặt theo số 1, 2, 3… và rút gọn nút.

Quyết định tiếp theo: [ảnh chỉ ở cơ sở và tùy chọn](Venue-only-optional-images.md); nút Gỡ ảnh áp cho cơ sở, không có ảnh riêng sân. Số sân/phân cấp giữ nguyên.

## Contract trước code

- Giữ quan hệ Owner membership → business → venue → court, booking giữ courtId. Business là hồ sơ doanh nghiệp/nhận diện của chủ; không gộp venue và court.
- Giữ API `{name}` và schema hiện có; khi tạo/đổi tên sân, nhận số nguyên dương ASCII hoặc `Sân N` (không phân biệt hoa/thường), chuẩn hóa thành `Sân N`. Giới hạn kiểu số int32 (1–2147483647), không nhận số thập phân/âm/0/chữ hoặc số quá lớn.
- Số sân riêng trong từng venue: hai venue được có Sân 1, cùng venue không trùng kể cả dạng `1`, `01`, `Sân 01`. Create và rename kiểm dưới business lock; exclude chính court khi rename; dữ liệu cũ được so theo số nếu nhận diện được. `400 VALIDATION_FAILED` cho tên/số sai, `409 COURT_NUMBER_EXISTS` khi trùng, `401/403/404` theo scope, `409 STATE_CONFLICT` cho mục không editable, version theo contract hiện có. Không tự đổi tên sân đã lưu hoặc snapshot booking; sân tên cũ vẫn đọc/vận hành được, đổi tên lần tiếp theo phải theo số.
- Không thêm migration hoặc tự đánh lại số dữ liệu thật. Sân được sắp theo số trong Partner detail, Customer detail và availability (1,2,…10); tên cũ không theo số nằm sau nhóm có số và dùng thứ tự tên/ID ổn định.
- UI tạo/đổi tên dùng input “Số sân” type number/min1/step1/max int32; tạo sân gợi ý số nhỏ nhất còn trống khi chọn cơ sở, có thể tự sửa. Sau tạo gợi ý lại cho đúng cơ sở; chuyển business reset form. Ghi rõ “Sân N” tại cơ sở được chọn; sân legacy ghi tên hiện tại và yêu cầu nhập số nếu muốn đổi.
- Form cơ sở vẫn giữ tên cơ sở tự do (ví dụ “Sân A · Cơ sở 1”). Nút không chứa tên cơ sở/sân dài trong phần nhìn thấy. Dùng nhãn ngắn trong card đúng mục, aria-label đầy đủ cho tác vụ có nhiều nút giống nhau. Nút thao tác trong workspace onboarding có chiều rộng theo nội dung kể cả mobile, tối thiểu cao44px; không thu nhỏ ô nhập hoặc nút kết quả tìm địa chỉ MapTiler.

## Acceptance/testcase

- NUM01: cùng Owner tạo nhiều venue và nhiều court dưới mỗi venue, scope không lẫn; mỗi venue có Sân 1 hợp lệ.
- NUM02: tạo/rename dạng hợp lệ được chuẩn hóa; input invalid không lưu; rename/create duplicate kể cả alias trả409 không500; đồng thời cùng số chỉ một thành công.
- NUM03: rename giữ courtId/venueId và booking; Owner khác bị chặn; ACTIVE cần If-Match, pending không sửa.
- NUM04: Partner chọn cơ sở gợi ý đúng số, override, chuyển cơ sở/business không mang số/scope cũ; browser desktop/mobile nhãn/nút không tràn hoặc kéo dài theo tên.
- NUM05: danh sách/availability public sắp Sân1,2,10 đúng, chỉ ACTIVE; dữ liệu legacy đọc được.
- Regression F02 onboarding/additions/media và luồng live liên quan; PostgreSQL/PostGIS thật cho uniqueness/race/scope, fixtures chỉ chứng minh UI. Kết quả ban đầu NOT RUN; cập nhật progress sau chạy.

## Kết quả 2026-10-12

| Phạm vi/lệnh tại repo root PowerShell | Kết quả | Mục đích |
|---|---|---|
| `npm.cmd run typecheck` | PASS4workspaces | Kiểm TypeScript ba app/UI |
| `npm.cmd run build` | PASS3portal | Bundle mới cho browser; `.local/court-numbering-web-build.log` |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx -c Release --no-restore` | PASS0warning/error | Backend/test assembly; `.local/court-numbering-build.log` |
| `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --configuration Release` | PASS96/96 | Regression API; `.local/court-numbering-api.log` |
| `scripts/Test-Database.ps1 --no-build -c Release --filter FullyQualifiedName~OnboardingFlowTests` | PASS26/26,0skip,5m57s | PostGIS thật: NUM01–03/05 và22regression, gồm actual booking snapshot sau rename; `.local/court-numbering-db.log` |
| `scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/f02-onboarding.spec.ts tests/web/f04-discovery.spec.ts --workers=2 --reporter=line` | PASS46/46,31,7s | NUM04 và UI regression desktop/mobile; `.local/court-numbering-web-final.log` |
| `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release` | PASS8/8,exit0 | Luồng thật F02–F07 với Sân1/Sân2, Admin, Customer OTP/đặt/thanh toán; `.local/court-numbering-live.log`, DB thử riêng đã drop |

Hai executions NUM04 lượt đầu FAIL vì locator exact label của select; dùng role combobox để chọn đúng điều khiển, focused recheck2/2 PASS và full46/46 PASS. Root xem ảnh form số sân desktop/mobile, nút Lưu sân dưới180px/caoít nhất44px trong test, không tràn ngang. Ảnh lưu `.local/court-numbering-screenshots/desktop.png`, `mobile.png`.

Reviewer độc lập đọc source không thấy blocker về quan hệ/scope/race/legacy; root đã sửa nhãn accessibility của các nút Lưu số sân theo góp ý. Không chạy lại toàn bộ các class DB khác, hosted CI hoặc AWS trong mốc này. Không thêm migration, không tự đổi số sân cũ/DB development, không commit/push/merge/deploy. Các kết quả F02 mở rộng trước đó là lịch sử trước thay đổi số sân này.
