# Partner Lịch & giá — contract triển khai 2026-10-12

> **Cập nhật 2026-10-14:** [Contract mới](Partner-court-profile-followup-2026-10-14.md) bỏ nút xác nhận sân riêng. Lưu giờ/giá hợp lệ của sân nháp trong cơ sở đã công bố sẽ tự công bố sân trong cùng transaction.

Trạng thái: DONE local ngày2026-10-13 trong phạm vi triển khai/kiểm thử bên dưới. Thực thi [prompt](../prompts/Partner-schedule-sync-and-redesign.md); quyết định mới ưu tiên F03 cũ. Không thêm đặt hộ, check-in, thu chi hoặc cổng thanh toán. CI/AWS và kiểm định cá nhân của người dùng được ghi riêng, không tính là đã chạy.

## Phạm vi và giao diện

- Giữ business switcher ở header. Một selector Cơ sở và selector Sân phụ thuộc; số sân sắp theo số. Route `#/schedule?tab=calendar|pricing|maintenance&venueId=...&courtId=...&date=YYYY-MM-DD` hỗ trợ Back/F5, không chứa dữ liệu private trong URL.
- Ba tab thật Lịch sân / Giờ & giá / Bảo trì. Business ACTIVE có cơ sở công bố mặc định Lịch sân; hồ sơ nháp mặc định Giờ & giá. Lịch sân xem mọi sân theo ngày hoặc một sân. Chỉ tab hiện tại được polling.
- Một editor lịch tuần cho INACTIVE và ACTIVE; read/write adapter đúng trạng thái. Nháp đọc trực tiếp cấu hình mới nhất, không lấy rows chung từ sân trước. Bảy ngày mở/tắt; từng ngày một khoảng mở và nhiều giá phủ đầy đủ. Không tự thêm dữ liệu giá khi sân đã có cấu hình. Sân chưa có cấu hình có mẫu rõ ràng để nhập, không tự lưu.
- Nháp chỉ lưu lịch cơ bản; override/policy/preview/bảo trì chỉ ACTIVE theo API hiện hành. Sân INACTIVE trong venue PUBLISHED có CTA Xác nhận dùng version mới; cơ sở nháp vẫn gửi Admin tại trang cơ sở.
- Dirty guard trước mất form khi đổi scope/tab/business/route/reload; đọc có abort/generation; không refresh form đang sửa. Save độc lập mỗi phần kèm version, dữ liệu chưa lưu ở phần khác không bị mất âm thầm.
- Toast đầu viewport 3 giây; lỗi tải/stale persistent có retry/reload. Private state xóa khi mất quyền/terminal401/logout; không giữ lịch phiên cũ.

## API, dữ liệu, quyền và transaction

Tất cả API `/api/v1`, response `{data,traceId}`, lỗi problem code hiện hành. Không thêm bảng/migration trừ index có bằng chứng cần.

### Cấu hình

- GET `/operator/courts/{id}/operations`: ACTIVE giữ guard hiện có, đọc version/hours/basePrices/rules trong RepeatableRead snapshot.
- GET `/partner-onboarding/courts/{id}/schedule`: nháp đúng Owner/pending-active membership và parent editable, trả `id,name,status,version,venueId,timezone,hours,basePrices` (policy/rules nếu có dùng cùng cấu trúc operations). Không mở write ACTIVE qua endpoint nháp.
- PUT nháp `/partner-onboarding/courts/{id}/schedule`: body hours/prices như hiện hành, bắt buộc If-Match, kiểm version sau lock, nguyên tử; trả `id,version,hours,prices` (counts). Chuyển callers và tests legacy đồng thời; thiếu 428, stale412. Nháp GET không gây mutation.
- PUT ACTIVE lịch/override/policy giữ route, guard/version/audit hiện có. Preview chấp nhận duration >= minimum và chia hết 30 phút; block chỉ là bước hiển thị, không ép duration bội block. Pricing engine/snapshot booking giữ nguyên. Preview không tạo hold/quote.

### Calendar read model

GET `/operator/venues/{venueId}/calendar?date=YYYY-MM-DD&courtId=<optional>`.

- Owner tài khoản ACTIVE, business/membership OWNER ACTIVE; không phụ thuộc venue PUBLISHED/court ACTIVE cho việc đọc đơn cũ. Scope khác404, role sai403, query sai400 VALIDATION_FAILED. Query chỉ date/courtId, thiếu date hoặc unknown/duplicate query bị chặn.
- Một RepeatableRead snapshot và generatedAt/server now; `Cache-Control: no-store`. Toàn ngày địa phương, UTC `[dayStart,dayEnd)`, cả events ngoài giờ mở vừa sửa. Read không giải phóng/đổi booking/payment/outbox.
- Response gồm `venueId,venueName,venueStatus,timezone,date,generatedAt,dayStartsAt,dayEndsAt,courts`. Court gồm `id,name,status,canRequestNewBooking,openRanges:[{startsAt,endsAt,localStart,localEnd,isPriced}],events,slots`; event gồm `id,kind,startsAt,endsAt,blocksBooking,isPast` và fields theo loại.
- BOOKING có `occurrenceId,paymentAnchorId,bookingNo,seriesNo,bookingType,bookingStatus,paymentStatus,occurrenceAmount,paymentTotalAmount` (tiền exact-string); fixed mỗi occurrence đúng ngày, tiền occurrence riêng tiền toàn kỳ. Đọc cả lịch sử RELEASED nhưng không block; active occupancy theo RESERVED. Không suy release từ status; anomaly không hiện free giả.
- Slots do backend tính theo khoảng ngày thực tế, ca 30 phút, trả `startsAt,endsAt` nhãn local, `startsAtUtc,endsAtUtc,isPast,occupancy,canRequestNewBooking,pricePerSlotExact`. Tách occupancy khỏi past/open/price. Không hardcode 48 ca nếu timezone có DST. Tối đa 200 sân/5000 events; vượt trả409 CALENDAR_TOO_LARGE, không truncation.
- QUOTE_HOLD dùng QuoteReservations.Active, còn hiệu lực; không tiết lộ customer/quote token hoặc actionable booking link. MAINTENANCE có status/reason và isBlocking theo allocation. Có thể đọc lịch sử đã hủy không block.
- Payload không chứa contact, account, QR/proof/object key. Không Take100 âm thầm; query có giới hạn rõ và lỗi nếu vượt payload an toàn. UI không được coi lỗi/partial data là Trống.
- Lịch sân giữ booking/payment status tách occupancy và thời gian quá khứ; click booking mở detail F06 theo paymentAnchorId hiện có. Không thêm nút duyệt đơn trước khóa ca.

## Acceptance và kiểm thử

PS01–PS12 theo prompt: selection A→B→A/business/Back/F5; draft hydration, dirty và stale; snapshot/version; preview120/150/180 và pricing/customer; lưới booking/quote/maintenance, occurrence/anchor/payment, past/outsidehours; worker-downexpiry/reportedtransfer; quyền/PII; maintenance conflict/race/idempotency; VN midnight; desktop/mobile/keyboard/toast/poll/failure.

- API unit/regression và DB integration transaction/scope dùng PostgreSQL/PostGIS thật, DB tạm riêng. Browser fixture chỉ chứng minh UI; live browser→API/Worker/PostGIS/Mailpit mới chứng minh tích hợp.
- Không mutate DB development để kiểm thử; không commit/push/merge/deploy hoặc tạo AWS. F09/CI/AWS giữ gate riêng.

## Nhiệm vụ ba role sau thay đổi

Customer tiếp tục tìm/quote/đặt/báo chuyển, không có quyền calendar private. Partner chọn cơ sở/sân, xem lịch thực tế, tự cấu hình giờ/giá cho đơn mới, xác nhận sân đủ điều kiện, khóa/hủy bảo trì và vào Đơn đặt sân để đối chiếu. Admin duyệt doanh nghiệp/cơ sở/QR nhạy cảm; không duyệt sân và không sửa giá/đối chiếu tiền thay Owner.

## Bằng chứng

Contract được ghi trước code. Kết quả bản triển khai: typecheck4/build3PASS, backend Release0warning/error; API96/96, focused PostGIS21/21, fixtures80/80 và các lượt recheck mục tiêu; live desktop/mobile8/8PASS, exit0, DB thử đã dọn đúng tên. [Báo cáo lệnh, review và giới hạn](../testing/Partner-schedule-results.md), [hướng dẫn tự kiểm tra](../testing/Partner-schedule-manual.md). CI/AWS của bản code này NOT RUN; không suy PASS local thành F09 production-ready.
