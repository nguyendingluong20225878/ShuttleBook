# Kết quả điều chỉnh F07 — quote giữ chỗ và Customer UI

Ngày: **2026-10-08**. Môi trường Windows/PowerShell, ba web React/Vite, API/Worker .NET 10, PostgreSQL/PostGIS local thật và Mailpit. Các database test là tạm, không nâng cấp DB development hoặc tác động dữ liệu production.

## Kết luận

**Code và các gate local đạt; người dùng xác nhận nghiệm thu F07 ngày 2026-10-08. Sau thực thi prompt đánh giá sản phẩm, F07 DONE theo phạm vi local.** Review backend độc lập không còn finding P0/P1 đã biết trong phạm vi này. Không commit/push/deploy. Source review và UI fixture không thay bằng chứng giao dịch DB thật.

## Gate đã chạy

| Gate | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| Backend solution build cuối | PASS, 0 warning/error, 3,42 giây | Sau các sửa review và testcase cuối; build không chứng minh runtime |
| API suite | PASS 96/96, 1 phút 14 giây | .local/quote-hold-api-tests.log |
| TypeScript | PASS toàn workspace; Customer cuối PASS | Customer Vite build cuối cũng đạt |
| Customer browser focused | PASS 72/72, 35,8 giây | .local/f07-customer-style/browser-final.log; desktop/mobile, UI fixture |
| Browser affected sau QUOTE_CONSUMED | PASS 28/28, 15,5 giây | .local/f07-customer-style/browser-consumed-quote.log; 24 lặp và 4 mới, không cộng thành 100 testcase duy nhất |
| Live browser/API/Worker/DB/Mailpit | PASS 8/8, 52,5 giây | .local/quote-hold-live.log; quote vãng lai/cố định hiện RESERVED trong lịch public trước create, tiếp tục payment/private proof/confirm/F5 |
| PostgreSQL/PostGIS | 72 testcase duy nhất có bằng chứng PASS qua các lượt | 56 baseline + 16 mới; đã đối chiếu testName/outcome trong TRX. Không phải một lượt full 72/72 |
| Review source độc lập | Không còn finding P0/P1 đã biết trong phạm vi thay đổi | Đã sửa fresh actor/ownership trước idempotency hash của casual replay; không thay audit production |
| Git whitespace | PASS git diff --check | Không tự stage/commit; giữ thay đổi có sẵn |

Live dùng MapTiler fixture và media Local; không xác nhận provider MapTiler/AWS thật. Live không bao phủ mọi negative/race; các ca đó được bổ sung bằng integration PostgreSQL/PostGIS thật.

## Nhật ký PostgreSQL/PostGIS: giữ lịch sử FAIL và recheck

Log: .local/quote-hold-qa/. TRX: .local/quote-hold-qa/results/.

| Lượt / TRX | Kết quả | Xử lý |
|---|---|---|
| quote-hold.trx | 12 PASS, 2 phút 59 giây | Quote atomically, expiry boundary, race, quyền, maintenance, exclusion thật, replacement và Worker |
| quote-hold-final-extra.trx | 4 PASS/1 FAIL fixture, 1 phút 20 giây | Migration và fresh replay đạt; ownership fixture dùng EF để sửa alternate key bị từ chối |
| quote-hold-ownership-retry.trx | 1 PASS, 21 giây | Dùng SQL trên DB tạm để mô phỏng ownership đổi; kiểm fresh replay backend |
| quote-hold-regression.trx | 62 PASS/6 FAIL fixture, 16 phút 50 giây | Các kỳ vọng/fixture cũ chưa phù hợp quote giữ allocation và schema mới; không tính lượt này full PASS |
| quote-hold-suspended-create-retry.trx | 1 PASS, 22 giây | Customer bị suspend không tạo booking/payment; quote hold cũ hợp lệ còn đến TTL |
| quote-hold-downgrade-retry.trx | 1 FAIL fixture | Tên table trong SQL kiểm sau downgrade bị sai; không phải lỗi migration |
| quote-hold-downgrade-corrected.trx | 1 PASS, 19 giây | Kiểm đúng table payments bằng SQL khi schema đã downgrade, sau đó upgrade trước đọc current model |
| quote-hold-casual-policy-retry.trx | 3 PASS/1 FAIL fixture, 1 phút 05 giây | Hai theory phân biệt BOOKING và QUOTE_HOLD, expired fixture đạt; DST quote mới vô tình chồng booking trước theo UTC |
| quote-hold-dst-retry.trx | 1 PASS, 18 giây | Dùng giờ New York 03:00–04:00 không chồng khoảng trước; vẫn kiểm DST/quarter-hour và venue suspension |

Sáu lỗi của lượt hồi quy rộng: suspended create còn quote hold hợp lệ; đọc current model sau downgrade; sửa expiresAt vi phạm CHECK createdAt < expiresAt; hai theory đếm allocation mới; DST dùng quote đã consumed trước kiểm suspension. Đã sửa kỳ vọng/fixture và kiểm lại các trường hợp bị ảnh hưởng. Không sửa production để né business rule hoặc bỏ testcase.

## Bao phủ acceptance mới

- R01–R02: giữ đúng ca vãng lai/toàn kỳ; B và maintenance không chen vào; không booking/payment trước create; conflict không giữ phần kỳ.
- R03–R05: expiry tại boundary, lịch public mở lại khi Worker dừng; API cleanup; quote đồng thời, exclusion thật, create đợi khóa vượt TTL, nhiều Worker không giải phóng booking đã consume.
- R06–R08: Guest/role/ownership, fresh replay sau chờ khóa, thay quote không kéo dài hạn, failed replacement giữ hold cũ, quote lịch sử không có reservation phải lấy lại; retry một intent không tạo đơn lặp.
- R09–R10: hồi quy giá/snapshot/payment/report/review/SLA/outbox; migration Down/Up/repeat bảo toàn booking/payment/proof đã tạo.
- U01–U02: sidebar/toolbar/cards/forms chung phong cách Admin/Partner; desktop/mobile, lịch cuộn ngang, countdown và thông báo lỗi/requote. Đã xem screenshot; usability với khách thật chưa đo.

## Lệnh tái lập trong VS Code PowerShell

Chạy tại C:\Users\luong\Desktop\CLong. Docker/PostGIS/Mailpit phải sẵn sàng; port preview5173–5175 và APItest5081 trống. Chỉ test database server local có quyền tạo DB tạm; không cung cấp connection string production.

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run typecheck
npm.cmd run build
npm.cmd run test:api
npm.cmd run test:db -- --filter 'FullyQualifiedName~Quote_hold'
npm.cmd run test:db -- --filter 'FullyQualifiedName~CasualBookingTests|FullyQualifiedName~PaymentConfirmationTests|FullyQualifiedName~FixedSeriesTests'
npm.cmd run test:web -- tests/web/customer-identity.spec.ts tests/web/f04-discovery.spec.ts tests/web/f05-booking.spec.ts tests/web/f06-customer.spec.ts tests/web/f07-customer-ui.spec.ts tests/web/f07-series-customer.spec.ts --workers=2
npm.cmd run test:web -- tests/web/f05-booking.spec.ts tests/web/f07-series-customer.spec.ts --workers=2
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081
git diff --check
~~~

Các dòng trên là lệnh tái lập; kết quả được ghi riêng ở bảng, không hứa một lượt full sẽ có cùng số ca như lượt trước khi bổ sung testcase. Agent subprocess thiếu APPDATA dùng profile tạm riêng .local/build-profile; không sửa profile máy. Một số recheck dùng output directory riêng để tránh ghi đè binary đang chạy. NuGet vulnerability audit online chưa chạy do network.

## Chưa chạy / bước tiếp

- **NOT RUN:** S3 AWS thật; MapTiler provider thật; CI hosted; test tải staging; backup/restore drill và production deployment.
- Người dùng đã xác nhận nghiệm thu tổng thể, chưa cung cấp biên bản từng H01–H06; không tự dựng kết quả từng ca. [Checklist](F07-quote-reservations-manual.md) giữ để tái kiểm khi cần. DONE local không thay bằng chứng provider/production.
- [Prompt đánh giá sản phẩm](../prompts/ShuttleBook-product-review-alobo.md) và [báo cáo/lộ trình](../reviews/ShuttleBook-product-readiness.md) đã cập nhật bằng chứng mới.
