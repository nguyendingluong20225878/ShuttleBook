# Prompt thực hiện F07 và cải thiện giao diện các vai trò

> Cập nhật2026-10-08: quote hợp lệ giữ chỗ tạm đến expiresAt; create chuyển allocation tạm sang BOOKING nguyên tử. Xem docs/features/F07-quote-reservations.md; chính sách mới thay mô tả quote không giữ chỗ trước đó.


## Mục tiêu và nguồn yêu cầu

Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, tài liệu `docs/00`–`05`, F03–F06 và code đã commit trước khi sửa. Tiếp tục từ F06 đã nghiệm thu. Triển khai lịch cố định hàng tuần trên cùng sân; cải thiện customer/admin và các màn booking partner. Không tự commit/push, không migrate DB development, không triển khai S3 thật trong phiên này.

Tham khảo [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill), đọc SKILL upstream và dùng script đã có trong `.local/ui-ux-pro-max`. Query `booking operations dashboard --design-system` trả Minimalism/Swiss phù hợp; phần hero marketing, màu amber và font monospace không phù hợp với giao diện vận hành hiện tại. Chọn dashboard nền sáng, giữ thương hiệu teal và system font có tiếng Việt. Query UX `error summary validation` bổ sung error summary nhận focus và thông báo lỗi qua role alert; React query hướng dẫn form có state. Không áp dụng nguyên xi đề xuất sai ngữ cảnh.

## Chính sách đã được người dùng duyệt

Ngày 2026-10-07 người dùng trả lời “đồng ý cả 3”. Triển khai:

1. Thu 100% toàn kỳ bằng một QR; owner xác nhận một lần cho tất cả buổi.
2. Giữ horizon 60 ngày của F05; ít nhất một tháng lịch, tối đa 12 occurrence.
3. Quote 2 phút, giữ toàn kỳ theo holdMinutes sân; một conflict rollback toàn bộ. Chưa báo chuyển quá hạn thì giải phóng cả kỳ; đã báo chuyển/NEEDS_REVIEW không tự giải phóng vì owner chậm.

Đặc tả và thiết kế đã ghi nhận phê duyệt; tiếp tục database/API/UI/kiểm thử theo contract đã chốt.

## Thiết kế và thứ tự triển khai

1. Chốt đặc tả/acceptance/API/error/schema/testcase ở `docs/features/F07-fixed-series.md`, `docs/testing/F07-test-cases.md` trước code nghiệp vụ.
2. Migration tăng dần: series aggregate, quote recurrence/snapshot, từng booking có SeriesId và từng allocation cụ thể. Một payment cả kỳ, không tạo giả thanh toán riêng từng buổi. Bảo toàn bookings/payments/evidence/idempotency F05/F06 cũ.
3. Quote sinh ngày theo thứ trong tuần và timezone cơ sở; thời lượng >= max(120, minimum sân), chia hết 30; kỳ >= startsOn.AddMonths(1), không thay bằng 4 tuần. Kiểm tra DST, quá khứ, horizon, số buổi, giờ mở, bảng giá từng ca và tổng chính xác. Không tự mặc định ca thiếu giá thành 0.
4. Create transaction giữ tất cả buổi; recheck quyền, publish/scope, policy, giá, QR; quote đổi phải xem báo giá mới. GiST exclusion của Postgres quyết định cuối cùng; cạnh tranh với casual/maintenance không để lại một phần.
5. Idempotency actor/operation/key + canonical hash; replay sau kiểm quyền hiện tại, không nhân booking/payment/audit/outbox. Khóa aggregate trước các occurrence theo thứ tự thống nhất trong create/payment/expiry/SLA để tránh deadlock. Không cho endpoint từng booking xử lý riêng một occurrence thuộc series.
6. Reuse báo chuyển/bổ sung/owner đối chiếu đúng tổng/review/final reject F06 cho aggregate. Optional screenshot/note, không bắt mã giao dịch. Report giữ cả kỳ; confirm PAID/CONFIRMED cả kỳ; reject terminal giải phóng cả kỳ. State/version/audit/evidence/outbox/idempotency cùng transaction. Supplement không reset firstReportedAt/SLA.
7. Worker expiry/SLA/outbox hỗ trợ series: expiry trước report toàn kỳ, SLA 30 phút owner+Admin một lần, không tự release đã báo chuyển. Notification deep link đúng role/series; tối thiểu dữ liệu, retry/dedup. Private proof/QR phải kiểm owner/customer/scope như F06.

## API và UI

- Giữ ví dụ docs: `POST /api/v1/booking-series/quote` nhận courtId/dayOfWeek/localStartTime/durationMinutes/startsOn/endsOn; `POST /api/v1/booking-series` nhận quoteId và Idempotency-Key.
- GET series chi tiết/danh sách customer và operator, QR private; payment commands yêu cầu If-Match và Idempotency-Key. Contract chi tiết được chốt trong feature spec trước khi code, giữ envelope và error format hiện có.
- Customer chọn cơ sở/sân qua lịch chung, chuyển Vãng lai/Cố định, nhập thứ/kỳ; preview toàn bộ buổi và giá/tổng/TTL/conflict trước create. List có mỗi series một dòng, detail liệt kê tất cả buổi và một thanh toán. Guest phải login để tạo; F5 dùng guard memory hiện tại.
- Partner giữ shell đang có, thêm loại đơn/cả kỳ/giá từng buổi và tổng để không đối chiếu nhầm một buổi; review/confirm/reject nói rõ tác động cả kỳ.
- Admin dashboard rõ duyệt hồ sơ/cảnh báo, không thêm quyền owner hoặc quyền ảnh biên lai. Giữ F5 restore HttpOnly và idle 30 phút từ thao tác thực; polling không gia hạn.
- Customer/admin dùng header/nav/cards/form grids/empty/loading/error thống nhất. SVG icon, text tương phản 4.5:1, body16px, line-height1.5, target44px, focus rõ, giảm chuyển động, label thật, kiểm 375/768/1024/1440. Không tạo số liệu giả. Bảng sân vẫn cuộn ngang bên trong, không co nhỏ cột 30 phút.
- Tách component/hook theo feature hiện có; không đưa state/UI vào API layer, không thêm router/state library chỉ để đổi bố cục.

## Kiểm chứng bắt buộc

- API validation: tháng lịch/biên cuối tháng/leap, thứ/ngày, duration/minimum/grid, horizon/maxbuổi/timezone DST, thiếu giá/QR, strict fields và auth.
- PostgreSQL/PostGIS thật: create all-or-none, conflict cuối kỳ không partial, casual/series/maintenance concurrency, concurrent same-key one group, changed/expired quote, migration upgrade+repeat, FKs/CHECK/unique payment/scope.
- Report/expiry race, whole-series reject/rebook, review/supplement sau deadline giữ tất cả, confirm exact total, replay/stale version, scope revoke, proof READY/private, SLA và outbox retry/dedup.
- Browser customer/partner/admin desktop/mobile: new fixed flow + existing casual regression, stable retries/errors/privatecache, nav Back/F5/idle, form/keyboard/longtext/touch/overflow. Xem ảnh chụp thực tế.
- Build solution/typecheck/build, test API, các DB/browser/live test liên quan. Ghi PASS/FAIL/NOT RUN với lệnh và giới hạn; không dùng stub làm bằng chứng provider/PostGIS.

## Bàn giao

Viết checklist nghiệm thu tay PowerShell VS Code, giải thích hành động và trạng thái mong đợi cả customer/partner/admin. Cập nhật progress mỗi mốc và bằng chứng cuối. F07 chỉ DONE khi đạt acceptance cần thiết và người dùng nghiệm thu; báo chính xác nếu còn policy hoặc runtime chưa đạt. S3 live vẫn theo dõi NOT RUN trong phạm vi hoãn đã chốt.
