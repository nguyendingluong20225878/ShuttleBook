# Kết quả feedback ba cổng — 2026-10-13

Thực thi [prompt](../prompts/Three-role-feedback-completion.md) theo [contract](../features/Three-role-feedback-completion.md). Trạng thái **DONE local** với bằng chứng của chính mốc feedback; không lấy PASS mốc schedule cũ nghiệm thu mốc này.

## Trước và sau

| Feedback | Thay đổi | Bằng chứng |
|---|---|---|
| Nút sân tách rời, khó setup/gửi duyệt | Nhóm sân cùng cơ sở, form số sân và lưu/xác nhận cùng hàng; link setup đúng court và quay lại venue | Browser grouping/link/draft/tab/F5; desktop alignment |
| Sân nháp mất lựa chọn khi quay lại editor | Danh sách quản lý không lọc theo calendar; nhớ sân editor khi chọn Tất cả sân/quay lại qua menu, hydrate theo hash scoped | Browser round-trip/F5/save/version |
| QR ban đầu ở mục khác | Form QR ngay trong cơ sở nháp; chung venue, không theo court | Browser setup request scoped và PostGIS shared QR |
| Đổi QR có thể gửi lại metadata ẩn cũ | Endpoint payment-only đọc metadata canonical dưới lock; Admin vẫn duyệt | PostGIS deterministic concurrency409/retry, metadata không revert, snapshots cũ |
| Mã ngân hàng gây thừa | Không nhập/hiển thị/read DTO bankCode; legacy chỉ lưu nội bộ | Three UI sources, setup no-code, sanitized admin/booking reads |
| Ô Có đơn có mã dài | Ô chỉ Có đơn, anchor/accessibility giữ đúng | Calendar browser + live |
| Admin thiếu badge/action, nút đọc rời | Badge từ tổng unread toàn scope; click mở/read/action ba role, retry/idempotency/session/pagination | Notifications browser + PostGIS scoped actions |
| Customer nhận diện cơ sở như của ShuttleBook | Metadata cơ sở thật; tel hợp lệ; ảnh tùy chọn có placeholder rõ |5browsercases desktop/mobile và discovery regression |

## Kiểm thử

| Lệnh / nhóm | Kết quả | Log |
|---|---|---|
| `npm.cmd run typecheck` | PASS4workspace | `.local/feedback-typecheck.log` |
| `npm.cmd run build` | PASS3portal | `.local/feedback-web-final-build.log` |
| Build backend Release | PASS0warning/error,10s | `.local/feedback-backend-build.log` |
| API Release `--no-build --no-restore` | PASS96/96, không skip,1m25s | `.local/feedback-api-tests.log` |
| Focused PostGIS | PASS7/7, không skip,1m39s | `.local/feedback-payment-db-final.log` |
| Browser affected specs, desktop/mobile |192PASS/8FAIL,200ca,2,4phút; lỗi fixture/locator được sửa và kiểm lại | `.local/feedback-web-tests.log` |
| Recheck fixture/session/navigation/QR-related Partner detail |26PASS/2FAIL,28ca,44,2s; còn một locator trực tiếp exactname không nhận badge | `.local/feedback-web-recheck.log` |
| Recheck locator responsive Admin cuối | PASS2/2, desktop/mobile,12,2s | `.local/feedback-admin-layout-recheck.log` |
| Live DB thử riêng, desktop/mobile | PASS8/8,1,1phút, exit0; browser/API/Worker/PostGIS/Mailpit | `.local/feedback-live-verified.log` |
| CI code mới/AWS/S3/EC2/SMTP production | NOT RUN | Không nằm trong mốc local này |

### Nguyên nhân các FAIL đã xử lý

- Browser grouping/F5: fixture dùng customRestore nhưng không trả session ở reload, app về đăng nhập. Sửa helper fixture theo cookie/session contract; round-trip/F5 sau đó PASS. Không suy fixture401 thành lỗi xóa court DB.
- Admin fixture mới thiếu tokenType Bearer nên login bị từ chối đúng validation. Sửa fixture.
- Hai testcase navigation/layout dùng exactname Thông báo; badge làm accessible name có thêm số. Cập nhật locator theo tên bắt đầu, kiểm cả desktop/mobile/tablet/keyboard.
- DB lượt đầu4PASS/1FAIL: assertion raw JSON không chấp nhận spaces do JSONB chuẩn hóa. Sửa parse JSON rồi kiểm giá trị; suite7/7 cuối thay bằng chứng lượt đầu.
- Live lượt đầu6PASS/2FAIL: assertion cũ đòi mã ngân hàng đã bỏ khỏi UI. Lượt hai6PASS/2FAIL: assertion dùng thời hạn5giây trong lúc hồ sơ/cấu hình còn đang tải, không có lỗi API trong snapshot; đặt thời hạn chờ hai trạng thái pending và heading editor thành15giây. Lượt cuối8/8PASS trên cùng luồng, không bỏ assertion nghiệp vụ.

Không cộng các lượt recheck trùng thành một lượt toàn bộ lớn hơn.192ca đã PASS và tám ca ban đầu đã PASS sau sửa/recheck; product thay đổi cuối phần Partner không hiện bankCode còn được regression detail/series và live kiểm lại.

Live cuối kiểm đăng ký/OTP Customer qua Mailpit; Admin đăng nhập/đọc thông báo/mở duyệt; hồ sơ ban đầu, cơ sở/sân/QR, phản hồi và duyệt; Owner tự xác nhận sân bổ sung; lịch/giá/preview/bảo trì; booking vãng lai/cố định, báo chuyển và Owner xác nhận; QR thay đổi qua Admin và snapshot cũ; Customer click mở/đọc thông báo. Database thử `shuttlebook_f014_live_eaa73bb1262545c6827701cbe3927f56` đã drop sau kiểm target; runner đã phục hồi build web local bình thường, exit0.

## Lệnh tái chạy

PowerShell tại root repo, PostgreSQL/PostGIS và Mailpit local đang chạy. Browser fixtures là kiểm UI; hai lệnh cuối dùng database thử thật. Filter DB gồm bảy ca của mốc feedback, không phải toàn bộ DB suite.

```powershell
npm.cmd run typecheck
npm.cmd run build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --configuration Release --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --configuration Release --no-build --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Web.ps1 tests/web/partner-schedule.spec.ts tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/ui-partner-history.spec.ts tests/web/feedback-customer-presentation.spec.ts tests/web/feedback-notifications.spec.ts tests/web/f04-discovery.spec.ts tests/web/f06-partner.spec.ts tests/web/f06-customer.spec.ts tests/web/f06-admin.spec.ts tests/web/f07-partner-series.spec.ts tests/web/f07-customer-ui.spec.ts tests/web/f07-admin-ui.spec.ts tests/web/ui-admin-approvals.spec.ts --workers=2 --trace=off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --configuration Release --no-build --filter "FullyQualifiedName~Venue_payment_|FullyQualifiedName~Notification_feedback_|FullyQualifiedName~Initial_profile_without_optional_image_can_submit_and_approve_but_qr_and_schedule_remain_required|FullyQualifiedName~UI_admin_revision_returns_published_current_without_mutating_it_or_exposing_to_customer|FullyQualifiedName~Draft_approval_revision_and_scope_use_disposable_postgis"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release
```

## Review và giới hạn

Review UI độc lập phát hiện P1 metadata ẩn và P2 CSS specificity; đã sửa. Review backend chéo kiểm scope/approval/read count/legacy snapshots; không còn blocker nguồn được báo. PostGIS thực, không mock cho transaction/QR/allocation; browser fixtures chỉ chứng minh UI.

Không có migration mới trong mốc feedback, không thay DB development. QR/phê duyệt/payment state machine và booking snapshot tiếp tục dùng hệ thống cũ; không thêm batch publish hoặc Admin xác nhận tiền. Admin không background-poll vì contract idle/activity hiện có; badge lấy server khi mở/refresh/read. Partner/Customer visible polling vẫn hoạt động.

[Hướng dẫn tự kiểm từng feedback](Three-role-feedback-manual.md). CI và AWS vẫn cần kiểm riêng; không production-ready chỉ từ PASS local.
