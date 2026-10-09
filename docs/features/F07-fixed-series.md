# F07 — lịch cố định hàng tuần và cải thiện UI

> **Thay đổi theo yêu cầu 2026-10-08:** quote hợp lệ của Customer ACTIVE giữ chỗ tạm đến expiresAt (mặc định120s), vãng lai một khoảng/cố định toàn kỳ. Khi create, chuyển allocation tạm sang booking nguyên tử; hết hạn tự giải phóng. Quy tắc này thay thế mọi mô tả quote không giữ chỗ trước đó. Contract và testcase mới: [F07-quote-reservations](./F07-quote-reservations.md).


Trạng thái: **DONE — người dùng nghiệm thu ngày 2026-10-08; đã hoàn tất prompt đánh giá sản phẩm trước khi chốt trạng thái**. Phạm vi local, bao gồm quote giữ tạm và Customer UI mới; S3 thật/production vẫn NOT RUN. Đặc tả gốc ngày 2026-10-07. Prompt: `docs/prompts/F07-fixed-series-ui.md`. Contract/data/lock: `F07-designer-notes.md`.

## Phạm vi đã xác định

- Cùng sân, một thứ và khung giờ hàng tuần; tối thiểu max(120, minimum sân) phút, chia hết 30. Kỳ ít nhất một tháng lịch, không phải mặc định 4 tuần.
- Một booking/allocation thật cho mỗi buổi; quote và snapshot giá riêng từng ngày. Một xung đột rollback cả kỳ, không bỏ buổi.
- Giữ các quy tắc F06: private proof, exact amount, If-Match, idempotency, active scope, outbox, SLA; báo chuyển/NEEDS_REVIEW không tự release vì owner chậm.
- Customer/admin UI dùng layout và component theo feature, partner cải thiện phần danh sách/đối chiếu; giữ auth/session và luồng cũ.

## Quyết định đã duyệt ngày 2026-10-07

Người dùng trả lời **“đồng ý cả 3”**: FULL_SERIES (100% toàn kỳ, một QR/một xác nhận), cửa sổ trong 60 ngày địa phương/tối đa 12 buổi, quote 120 giây/holdMinutes sân. Chưa báo chuyển quá hạn giải phóng cả kỳ; đã báo chuyển/NEEDS_REVIEW giữ cả kỳ chờ owner.

Thiết kế/API/error/schema/lock trong `F07-designer-notes.md` đã chốt làm contract triển khai. Quote có xung đột trả HTTP 200 với preview/conflicts, canCreate=false và quoteId=null; create xung đột trả 409 SERIES_CONFLICT và rollback toàn kỳ. Một payment anchor cho cả nhóm; mọi endpoint occurrence phải chuẩn hóa về anchor. Không mở luồng cố định cho người dùng khi backend chưa có.

## Acceptance criteria

1. Weekly recurrence đúng tháng lịch, weekday, duration/court minimum, half-hour và timezone; không chấp nhận DST invalid/ambiguous.
2. Quote kiểm publication/active/auth/horizon/giờ mở/giá/QR, trả mọi buổi và conflict date; quote không xung đột giữ toàn kỳ đến expiresAt. Tổng chính xác numeric18/BigInt, không gán giá0 cho ca thiếu.
3. Create atomic series + tất cả bookings + chuyển allocations giữ tạm sang BOOKING + payment + snapshot/audit/outbox/idempotency; recheck giá/policy/QR/quote expiry. Constraint PostgreSQL bảo vệ overlap; concurrent retry không tạo thêm nhóm.
4. List/filter/count/details customer/owner phản ánh nhóm thanh toán và từng buổi rõ ràng; ngày lọc match bất kỳ buổi. Không lộ QR/account/proof ngoài quyền.
5. Theo chính sách được duyệt: báo chuyển/review/supplement/confirm/reject/expiry nhất quán toàn nhóm; không có bypass endpoint occurrence; correct locks/version/replay/scope; supplement giữ SLA, owner+Admin một alert.
6. Migration upgrade/repeat giữ dữ liệu casual/evidence/idempotency cũ; checks/FKs/unique payment đảm bảo scope. Down không xóa dữ liệu fixed để ép casual.
7. UI có lựa chọn cùng lịch sân, form weekly/period, preview buổi/giá/conflict, retry/auth/QR/report/ownerdecision; không thêm customer cancel/change hoặc trạng thái sau CONFIRMED.
8. Cải thiện UI ba role đạt keyboard/label/focus44px/contrast/responsive; không số liệu giả, không polling gia hạn Admin idle, không token storage. Existing casual/onboarding/approval/notification regression PASS.
9. Build/typecheck + API + PostGIS integration + browser/live phù hợp đạt; review bugs được sửa, manual acceptance được bàn giao. S3 live vẫn hoãn theo yêu cầu người dùng.

## Triển khai hiện tại — 2026-10-08

- Migration tăng dần F07FixedSeries, series/quote, bookings từng buổi và một payment scope; giữ casual/evidence/QR/idempotency cũ. Không migrate development tự động.
- Quote/create authenticated, GET series scoped; group-aware customer/operator list/filter/count/detail/QR/proof, payment commands/expiry/SLA/outbox áp dụng cả kỳ và chuẩn hóa occurrence ID về anchor.
- Customer fixed mode dùng lịch chung, form tuần/kỳ và preview tất cả buổi/conflict/giá/TTL; detail/list một nhóm và reuse QR/report F06. Partner bảng từng buổi/tổng kỳ/whole-period quyết định; Admin/customer/partner UI đã cải thiện.
- Build3web/full browser150PASS+8SKIP live riêng; sau review privacy partner, affected26PASS. API96PASS. PostGIS22case F07 và34case F05/F06 đạt qua các lượt/recheck, live8PASS và buildsolution cuối0warning/error. Review độc lập không còn blocker. Mốc này là bằng chứng trước lần đổi quote hold. Người dùng đã nghiệm thu bản mới, F07 DONE local ngày 2026-10-08; xem kết quả quote reservations và progress cho gate mới, lịch sử FAIL/recheck và giới hạn.

## Chốt nghiệm thu — 2026-10-08

Người dùng xác nhận nghiệm thu F07. Gate bản giữ chỗ mới và UI: backend build/typecheck đạt, API96/96, browser72/72 và affected28/28, live8/8, PostGIS72 testcase duy nhất có PASS qua các lượt. Báo cáo sản phẩm đã thực hiện theo yêu cầu: [ShuttleBook-product-readiness](../reviews/ShuttleBook-product-readiness.md). S3 thật/CI hosted/backup/load là các gate riêng chưa chạy; backlog được ghi rõ, không tự triển khai trong mốc này.
