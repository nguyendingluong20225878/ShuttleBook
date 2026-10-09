# Kết quả điều chỉnh sau audit UI — 2026-10-09

Trạng thái: **code và kiểm thử local hoàn tất, chờ nghiệm thu tay các thay đổi**. F07 vẫn DONE local theo nghiệm thu trước. [Phạm vi/contract](../features/UI-post-audit-improvements.md) · [Hướng dẫn nghiệm thu](UI-post-audit-improvements-manual.md).

## Những phần đã sửa

| Vai trò | Thay đổi | Bằng chứng mới |
|---|---|---|
| Customer | Marker chuyển trang trong SPA, giữ memory session hiện tại | Desktop/mobile: không có Document navigation mới, đăng nhập một lần và tiếp tục lấy quote được |
| Customer | Lưu key/body của intent trước POST; đã gửi nhưng chưa rõ kết quả thì được retry cùng intent sau TTL | Browser kiểm key/body không đổi, không tự requote; PostGIS kiểm một booking/payment/idempotency và allocation cũ sau replay |
| Partner | Refresh/poll đơn giữ cửa sổ trang đã tải và đọc lại cursor chain mới; chuyển filter/scope không trộn dữ liệu | 25–26 đơn, trạng thái mới, delayed response, 403/404 purge, desktop/mobile |
| Partner/Admin | Refresh/mark-read thông báo giữ các trang đã tải; số chưa đọc từ server | Partner 55–56 thông báo; Admin kiểm cursor đổi sau đọc, giữ trang cũ và deny purge |
| Partner | Preview Đã tính giá; write Đã lưu; lỗi không mang success style/preview cũ | Desktop/mobile preview, lưu và failure |
| Admin | Tìm tên/loại, keyset pagination, count toàn hệ thống | ASP.NET test host + PostGIS thật: 103 hồ sơ cùng submittedAt, không bỏ sót sau quyết định trên dòng đã đọc; filter/query/cursor/quyền |
| Admin | Đối chiếu current → proposed, tài khoản/QR/version, xác nhận hai bước, lý do trim10–1000 | Browser desktop/mobile; PostGIS xác nhận current thật, không ghi published/snapshot khi GET, customer403 |
| Admin mobile | Wrapper grid có min-width0; bảng cuộn trong vùng riêng, focus bàn phím và hướng dẫn; không che nút duyệt | Browser kiểm trang không tràn ngang, click hai bước/cancel/409; root xem ảnh desktop/mobile |

Không đổi schema, TTL, quyền payment/approval, chính sách fixed series hoặc allocation. Không bổ sung tiện ích mật khẩu, phân quyền nhân viên hay báo cáo. Marker giữ phiên SPA không phải triển khai khôi phục phiên Customer/Partner sau F5. Intent recovery chỉ ở trang review hiện tại, không persist token/intent qua F5.

## Gate cuối

Chạy từ thư mục gốc bằng PowerShell; script nạp cấu hình local và đường dẫn browser. Helper của agent dùng profile tạm trong workspace vì subprocess thiếu APPDATA; không đổi cấu hình toàn máy.

| Gate/lệnh | Kết quả cuối | Giới hạn |
|---|---|---|
| scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore | **PASS**, 0 warning/error, 13,19s | Build local |
| scripts/Build-Web.ps1 | **PASS**, TypeScript + Vite cả ba portal | Build mới trước browser gate cuối |
| scripts/Test-Web.ps1 --workers=2 | **206 PASS / 8 SKIP / 0 FAIL**, 1,6 phút | UI route fixtures; 8 ca live chưa bật harness trong lượt này |
| scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-restore | **96/96 PASS**, 11s | API regression test host |
| Focused PostgreSQL/PostGIS tests (lệnh bên dưới) | **3/3 PASS**, 20s | ASP.NET test host kết nối PostgreSQL/PostGIS thật, DB tạm do harness tạo/dọn |
| git diff --check các file tracked sửa trong lượt này | **PASS** | Cảnh báo CRLF/LF không phải lỗi whitespace |

Lệnh chạy lại các ca PostGIS mới:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --filter 'FullyQualifiedName~UI_admin_|FullyQualifiedName~UI_casual_'
```

48 executions mới trong browser: Customer22, Partner14, Admin12 (desktop + mobile). Có 158 executions hồi quy cũ; không cộng lại các lượt recheck vào tổng cuối. Marker test thực sự chạy với build có key local; SDK giả lập và không gọi MapTiler.

PostGIS mới:

- UI_admin_paging_preserves_unreviewed_rows_after_decisions_and_binds_filters_and_scope.
- UI_admin_revision_returns_published_current_without_mutating_it_or_exposing_to_customer.
- UI_casual_lost_response_replay_after_quote_TTL_returns_original_booking_and_allocation.

Ca replay thật tạo đơn thành công trước, bỏ qua response, tiến clock quá quoteTTL rồi gửi lại key/body cũ. Không giả lập DB bằng fixture; không dựng lỗi truyền tải sau commit trên TCP/browser thật. Gate browser bổ sung kiểm xử lý lỗi mạng phía UI.

## Lỗi đã phát hiện và sửa trong các lượt trước

- Lượt browser đầu: 185PASS/19FAIL/8SKIP; tiếp theo 76PASS/6FAIL; Admin recheck10PASS/2FAIL. Những số này là lịch sử, không phải kết quả gate cuối.
- Lỗi production: thông báo Partner bị mã hóa sai và nhánh Xem giá chưa áp dụng đủ; wrapper grid Admin mở rộng theo bảng min-width, gây tràn/che nút mobile. Đã sửa source; lượt cuối kiểm lại tất cả.
- Lỗi test: luồng approve cũ thiếu bước xác nhận mới; locator select lấy cả option; assert metric Overview khi đang ở tab khác; ngày fixture20/10 khác ngày mặc định09/10; assert refresh trước khi chain xong. Đã sửa fixture/locator/wait, giữ nguyên assert nghiệp vụ; không force-click hoặc bỏ ca mobile.
- DB đầu2PASS/1FAIL do assert AuditEvents rỗng dù login đã tạo audit. Đã lấy baseline sau login và xác nhận GET không thêm audit; recheck3/3 đạt.

## Review và môi trường

Root đã review key/body/TTL, fresh Admin guard, cursor bind/search literal, dữ liệu current, abort/generation, deny purge, blob URL cleanup và thao tác confirm. Các agent triển khai theo file riêng; review độc lập cuối không hoàn tất vì giới hạn tài nguyên của agent, không ghi PASS review độc lập.

Docker Desktop local đã chạy; compose PostgreSQL/PostGIS và Mailpit healthy. Không migrate development DB trong lượt này. Preview do test tạo đã đóng; Docker local giữ chạy để người dùng nghiệm thu. Không commit/push/deploy hoặc tạo S3.

Evidence ignored: `.local/ui-improvements/backend-build.log`, `web-build.log`, `api.log`, `browser.log`, `browser-exit.txt` (0), `db.log`, `db-results/ui-improvements.trx`; ảnh UI dưới `.local/ui-improvements/screenshots/`. Helper/log không phải artifact cần đưa lên Git. Tài liệu/test source được lưu trong repo.

## Còn cần người dùng xác nhận

**NOT RUN mới:** nghiệm thu tay bản sửa, MapTiler provider thật, ảnh QR thực tế của hai phiên bản, browser→API→Worker live mới, AWS S3 thật, thiết bị thật/NVDA/contrast, production/pilot. Các gate live/F07 đạt ở phiên trước vẫn là bằng chứng lịch sử, không coi là lần chạy mới.

Bước tiếp theo: restart API/portal và làm [checklist tay](UI-post-audit-improvements-manual.md). Ghi PASS/FAIL/NOT RUN theo từng nhóm; chỉ chốt nghiệm thu đợt UI này sau phản hồi của người dùng.
