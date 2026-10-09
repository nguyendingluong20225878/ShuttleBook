# Điều chỉnh F05–F07: giữ chỗ tại bước báo giá — 2026-10-08

## Quyết định và phạm vi
Yêu cầu mới của người dùng thay thế quy tắc cũ quote không giữ chỗ. Customer ACTIVE nhận quote hợp lệ sẽ giữ tạm các ca ngay: vãng lai một khoảng, cố định tất cả buổi trong một transaction. TTL quote hiện tại mặc định120 giây (series120s; casual cấu hình30–600s). Guest vẫn tìm/xem lịch nhưng đăng nhập trước khi báo giá có giữ chỗ. Chưa tạo booking/payment/outbox khi quote.

## Dữ liệu và transaction
Thêm quote_reservations(id=quoteId,customerId,courtId,kind CASUAL/SERIES,createdAt,expiresAt,consumedAt,releasedAt) và court_allocations.quote_reservation_id nullable. Kind allocation QUOTE_HOLD cùng bảng/ràng buộc exclusion với BOOKING/MAINTENANCE. FK scope court phải khớp. Giữ quotes lịch sử; quote cũ không có reservation phải lấy mới, không chuyển quyền bằng UUID.
Quote/create giữ thứ tự business→venue→court→user→reservation; ReadCommitted sau court lock. Maintenance hiện hữu khóa court; Worker cleanup chỉ khóa court và không lấy thêm khóa parent. Cleanup reservation hết hạn ở court trước quote/create/maintenance. Public availability bỏ hold đã hết hạn ngay cả Worker dừng; Worker dọn vật lý. Không dùng cache/mock chống trùng.
Quote mới thay thế quote trước của chính customer trên cùng court; nếu hold cũ còn hiệu lực thì giữ nguyên hạn cũ, không gia hạn bằng reload. Thay thế atomically; failed quote không làm mất hold trước. Tối đa một reservation còn hoạt động/customer/court. Cố định xung đột trả preview200 canCreate=false/quoteId=null, không giữ một phần kỳ.
Create kiểm owner/expiry/consumed/released sau court lock, compute bỏ chính hold hợp lệ, không bỏ hold của người khác; chuyển chính allocation QUOTE_HOLD sang BOOKING và consume reservation trong cùng transaction. Không có khoảng trống giữa giữ quote và booking. Idempotency cùng intent vẫn trả đơn cũ; quote đã consumed với intent khác trả409 QUOTE_CONSUMED. Quote thay thế trả409 QUOTE_EXPIRED. QUOTE_CHANGED yêu cầu quote mới. Deadline thanh toán bắt đầu lúc create theo holdMinutes; đã report/NEEDS_REVIEW không giải phóng bởi quote expiry.

## Contract/API/error
POST /api/v1/availability/quote cần auth CUSTOMER ACTIVE như series quote; request/response hiện có giữ nguyên. expiresAt cũng là hạn giữ tạm. Tạo đơn khác chủ quote trả404 NOT_FOUND, guest401/operator403; quote thiếu reservation lấy mới409 QUOTE_EXPIRED. Conflict casual409 SLOT_UNAVAILABLE; series preview200/latecreate409 SERIES_CONFLICT. Quote hợp lệ hiển thị RESERVED/Đã kín trong lịch public, không lộ customer/quoteId.

## Acceptance và testcase trước code
- R01 casual quote: allocation giữ, không booking/payment; B không quote/create/maintenance chồng; đúng owner create chuyển allocation không duplicate.
- R02 fixed quote: mọi buổi RESERVED, B blocked, all-or-none conflict không hold phần kỳ.
- R03 expiry: đúng boundary trở AVAILABLE, B lấy quote được khi Worker dừng; Worker cleanup tất cả idempotent.
- R04 hai customer quote đồng thời: một giữ được, loser không allocation sót, Postgres exclusion thật.
- R05 create vs expiry/maintenance: một kết quả nguyên tử, không doublebooking; ownhold không tự conflict.
- R06 ownership/role: guest401,operator403,othercustomer404 khi create; UUIDquote không chuyển quyền.
- R07 refresh/requote không gia hạn; thay selection/casual↔series cùngcourt thay hold cũ atomically, invalid quote giữ hold trước; oldquote không create được.
- R08 create replay/cùngquote intent khác: một booking/payment, không dùng lại reservation; quote cũ chưa hold lấy mới.
- R09 price/policy/QR changed: require review; booking snapshot/report/review/expiry/SLA F06 giữ nguyên.
- R10 migration mới/upgrade dữ liệu F07 có allocation cũ và repeat; downgrade không phá booking.
- U01 Customer cùng sidebar/toolbar/palette/cards/spacing với Partner/Admin;375/768/1024/1440,keyboard/zoom,30min grid không chồng chữ.
- U02 countdown nói rõ giữ tạm và hết hạn, conflict/requote actionable; authentication trước quote; private session không đổi.

**DONE local ngày 2026-10-08:** build, integration PostgreSQL/PostGIS và UI/live có gate đạt; người dùng đã xác nhận nghiệm thu, prompt đánh giá sản phẩm đã hoàn tất trước chốt trạng thái. Xem progress/kết quả để phân biệt bằng chứng local và các gate production NOT RUN.
