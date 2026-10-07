# Prompt triển khai F06 — báo chuyển khoản và chủ sân xác nhận

> Prompt chuẩn bị ngày 2026-10-07, cập nhật theo nghiệm thu: bỏ mã giao dịch khỏi form customer/owner, ảnh chụp màn hình/ghi chú tùy chọn; API nhận bankReference tùy chọn để giữ dữ liệu/client cũ. Confirm đúng expectedAmount, SLA 30 phút từ báo chuyển đầu tiên tới owner + Admin một lần, không reset/giải phóng sân do chậm xác nhận. Đặt vãng lai được thêm từng ca30 sau minimum, không cần tổng là bội block. Contract hiện hành và kết quả thực thi ở `docs/features/F06-payment-confirmation.md`, `docs/testing/F06-test-cases.md`, `docs/progress.md`; tiếp tục từ mã nguồn/bằng chứng mới nhất, không hỏi chốt lại chính sách đã duyệt.

## 1. Nhiệm vụ

Bạn là agent phát triển ShuttleBook trong workspace hiện tại. Hãy hoàn thành **F06 từ Database → API → UI → Worker → kiểm thử → review**, nối trực tiếp vào booking vãng lai F05:

**Khách tạo booking và xem QR → chuyển tiền bên ứng dụng ngân hàng → báo đã chuyển → chủ sân nhận thông báo → đối chiếu → xác nhận, yêu cầu bổ sung hoặc từ chối cuối cùng.**

Khi chủ sân xác nhận, payment là `PAID`, booking là `CONFIRMED`; cả hai cổng hiển thị **Đã xác nhận**. Đây là kết thúc luồng giao dịch thành công. Không dừng ở API hoặc UI mock nếu còn thiếu phần đã đủ điều kiện triển khai và kiểm thử.

Đọc trước khi sửa:

- `AGENTS.md`, `docs/process.md`, `docs/progress.md`, `docs/setup.md`, `docs/README.md` và toàn bộ `docs/00-*.md` đến `docs/05-*.md`.
- UC-06/07/08/11/14/17/18 trong `docs/01-use-cases.md`; booking/payment state machine và sequence report/expiry/reject trong `docs/02-states-and-sequences.md`.
- `docs/features/F02-partner-onboarding.md`, `F03-court-operations.md`, `F04-public-discovery.md`, `F05-casual-booking.md`; testcase tương ứng, đặc biệt `docs/testing/F05-test-cases.md`, `F05-manual-acceptance.md`.
- `docs/prompts/partner-ui-refresh.md`, `docs/testing/partner-ui-refresh.md` để kế thừa giao diện partner đã được cải thiện.

Kiểm tra Git/diff, schema/migration, mã nguồn và bằng chứng kiểm thử. Tiếp tục từ phần còn thiếu; giữ tất cả thay đổi chưa commit, không tự commit/push/merge/deploy. Không tự migrate/drop DB development của người dùng, tạo tài nguyên trả phí, đổi cấu hình máy toàn cục hoặc đưa secrets/token/signed URL/biên lai thật vào repo và output.

## 2. Baseline phải dùng lại

### Backend và dữ liệu

- `backend/src/ShuttleBook.Api/Bookings/BookingEndpoints.cs` đã có quote, tạo booking CASUAL, `GET /api/v1/me/bookings`, `GET /api/v1/bookings/{id}`, QR private theo chủ booking. GET booking hiện chỉ cho CUSTOMER, chưa cho operator.
- `backend/src/ShuttleBook.Infrastructure/Bookings/BookingModels.cs` và `BookingModelConfiguration.cs`: `Booking`, `BookingPayment`, `BookingQuote`, `BookingIdempotency`; F05 lưu từng ca bằng JSON snapshot, chưa có bảng `booking_items`. Không dựng lại aggregate/schema F05 từ mô hình minh họa cũ.
- Migration `20261006181227_F05CasualBooking.cs` đã cho phép các status F06 trong check constraint. Trạng thái được dành sẵn **không có nghĩa các command F06 đã tồn tại**. Payment hiện thiếu evidence, reported/confirmed metadata, số tiền thực nhận và lịch sử quyết định.
- `BookingIdempotency.CustomerId` hiện FK đến `User`, unique `(CustomerId,Operation,Key)` và phục vụ `CASUAL_CREATE`. Khi hỗ trợ actor operator, cần làm rõ tên/semantics bằng migration tăng dần hoặc giải pháp tương đương, bảo toàn record F05 và retry của booking cũ.
- `backend/src/ShuttleBook.Infrastructure/Bookings/BookingExpiry.cs` và `ShuttleBook.Worker/BookingExpiryWorker.cs` expire **chỉ** booking/payment `AWAITING_TRANSFER`. Worker khóa booking bằng `FOR UPDATE SKIP LOCKED`, cập nhật payment/allocation/audit/outbox trong transaction. F06 phải tương thích thứ tự khóa và race deadline này.
- `CourtAllocation` trong `Onboarding/CourtOperationModels.cs`, `CourtOperationsEndpoints.cs` và GiST exclusion `ex_f03_allocation_no_overlap` là nguồn chống trùng. `CONFIRMED` vẫn giữ allocation `BOOKING/RESERVED`; chỉ expiry trước báo chuyển hoặc từ chối cuối cùng mới release.
- `backend/src/ShuttleBook.Worker/ApprovalOutboxWorker.cs` hiện xử lý approval, `BOOKING_CREATED`, `BOOKING_EXPIRED`; có backoff và unique notification theo outbox/user. **Message không có TargetUserId hiện mặc định gửi Admin**. Không dùng mặc định này để gửi báo chuyển cho chủ sân.
- `Onboarding/NotificationEndpoints.cs` đã có `GET /api/v1/me/notifications` và `POST /api/v1/me/notifications/{id}/read`, chỉ đọc/đánh dấu thông báo thuộc user. DTO hiện chưa có liên kết booking.
- `Onboarding/MediaEndpoints.cs` đã có presign/content/complete/view, local private adapter và adapter S3; purpose hiện chỉ `QR`/`VENUE_IMAGE`, guard upload/complete dành cho operator. Không mở guard này cho mọi customer để upload biên lai.
- `Onboarding/OnboardingModels.cs` hiện có **business membership OWNER**, chưa có đầy đủ venue membership/permission/invitation F08. F06 dùng owner ACTIVE đúng business scope; thiết kế điểm mở rộng `booking.read`/`payment.confirm`, không tạo luôn F08 hoặc coi mọi VENUE_OPERATOR có quyền xác nhận.
- Dùng envelope `{data,traceId}`, `application/problem+json`, `TimeProvider`, rate limit/CORS/auth hiện có. Kiểm tra trạng thái user/membership trong DB, không chỉ dựa trên claim token hoặc ẩn nút UI.

### Frontend

- Customer: `apps/customer-web/src/features/bookings/BookingPages.tsx`, `features/auth/CustomerSession.tsx`, `App.tsx` và `routes/`. Đã có review/create/detail/list, polling detail, auth returnTo, private QR blob và retry create. Đọc đúng phiên bản hiện hành; prompt F05 cũ có mô tả trước khi session được trích ra.
- Partner: `src/layouts/PartnerShell.tsx`, `components/PartnerIcon.tsx`, `features/workspace/navigation.ts`, `features/workspace/PartnerOverview.tsx`, `assets/partner.css`, `PartnerOnboarding.tsx`, `PartnerOperations.tsx`.
- Giữ sidebar/mobile menu, hash navigation/Back/Forward/deep link, CSS tokens teal, focus/keyboard, responsive và trạng thái form đã có. Mục **Thanh toán & QR** là cấu hình nhận tiền; thêm mục **Đơn đặt sân** để xử lý đơn, không thay mục QR bằng danh sách giao dịch.
- Customer hiện có nhãn list chưa bao phủ đầy đủ status; F06 phải sửa mapping trạng thái ở list/detail/partner để không hiển thị mọi đơn chưa EXPIRED thành “Đang chờ chuyển khoản”.

## 3. Phạm vi và bất biến

### F06 phải có

1. Customer báo chuyển trước hạn; có thể gửi ảnh chụp màn hình chuyển khoản và ghi chú, không nhập mã giao dịch.
2. Lưu evidence và lịch sử; booking chuyển **Chờ xác nhận**; outbox gửi notification trong ứng dụng cho owner đúng business/venue.
3. Partner có danh sách đơn theo cơ sở, bộ lọc trạng thái/ngày, phân trang, chi tiết và bằng chứng private.
4. Owner xác nhận, yêu cầu bổ sung hoặc từ chối cuối; ghi người/thời gian/số tiền/lý do, version, audit và outbox.
5. Customer nhận kết quả và bổ sung bằng chứng từ `NEEDS_REVIEW`, rồi đơn trở lại **Chờ xác nhận**.
6. Cập nhật hai cổng bằng polling hoặc SignalR; dùng polling hiện có nếu đủ, không bắt buộc thêm SignalR.
7. Cảnh báo chờ xác nhận quá SLA được chốt, không tự giải phóng sân; retry/outbox lỗi được quan sát và cảnh báo.
8. Test transaction, authorization/scope, idempotency, deadline/concurrency và migration trên PostgreSQL/PostGIS thật.

### Những quy tắc phải giữ

- Booking gắn một court của một venue/business. Giá, từng ca, policy, QR và thông tin nhận tiền của F05 là snapshot, không tính lại hoặc lấy QR hiện hành khi xác nhận đơn cũ.
- Báo đã chuyển **chưa phải PAID**. Không xác nhận chỉ vì customer upload ảnh hoặc bấm nút; owner đối chiếu ngân hàng thủ công.
- Đơn đã báo chuyển, gồm `AWAITING_OWNER_CONFIRMATION` và `NEEDS_REVIEW`, không auto-expire theo payment deadline cũ, kể cả owner phản hồi chậm hoặc đã qua giờ chơi.
- Customer không hủy/đổi lịch. `CONFIRMED` là terminal thành công và không có check-in/check-out/completed/no-show; không tự release sau xác nhận.
- Không thêm thanh toán online, webhook ngân hàng, tự đọc giao dịch, hoàn tiền tự động, sửa giá thủ công trong đơn, doanh thu hoàn chỉnh, series F07, staff invitation F08 hoặc dispute Admin đầy đủ.
- S3 provider thật vẫn được người dùng hoãn. Hoàn thành/test biên lai bằng local private adapter; duy trì khả năng dùng adapter S3 hiện có. Ghi S3 live **NOT RUN** khi chưa có bucket/credentials, không tạo tài nguyên AWS và không coi local/mock là S3 PASS.

## 4. Chốt chính sách trước code nghiệp vụ

Tạo trước `docs/features/F06-payment-confirmation.md` và `docs/testing/F06-test-cases.md`: scope/non-goals, actor, acceptance, API/error contract, dữ liệu, sequence/state machine, lock order, UI states, policy và testcase có ID.

Các chính sách dưới đây đã được chốt và cập nhật theo nghiệm thu; không hỏi lại:

| Điểm còn mở | Đề xuất | Cách xử lý |
|---|---|---|
| Evidence | Bỏ ô mã giao dịch; ảnh chụp màn hình/ghi chú tùy chọn; bankReference API nullable | Giữ reference/lịch sử/hash client cũ; không tự sinh mã ngân hàng giả |
| Số tiền đối chiếu | MVP chỉ confirm khi `confirmedAmount == expectedAmount`; thiếu/thừa tiền chuyển sang NEEDS_REVIEW để xử lý trực tiếp, không sửa snapshot giá | Chốt xử lý thiếu/thừa tiền; không tự coi trả một phần là đã trả 100% |
| SLA | Đề xuất 30 phút từ lần báo chuyển đầu tiên; cảnh báo owner và Admin một lần khi còn chờ, không gia hạn/reset chỉ do upload bổ sung | Chốt thời lượng, người nhận và cách tính cho NEEDS_REVIEW; đây không phải idle timeout đăng nhập Admin |

Hỏi gọn các chính sách thực sự chặn code khi bắt đầu thực thi prompt, vẫn hoàn thiện thiết kế/testcase độc lập trong lúc chờ. Không coi người dùng chưa trả lời là đã duyệt. Quote TTL **120 giây**, horizon **60 ngày**, giữ chỗ theo **holdMinutes snapshot court, mặc định 20 phút** đã được chốt ở F05, không hỏi lại hoặc thay đổi.

Giới hạn kỹ thuật: reference nullable/trim ≤100 ký tự, note ≤1.000, reasonCode enum + reason trim 1–1.000; ảnh PNG/JPEG/WebP ≤5 MiB, kiểm tra checksum/type/magic giống media hiện có. Không nhận SVG/HTML, URL ảnh ngoài hoặc path/object key tự chọn từ client. Validation nhất quán UI/API/test.

Đồng bộ tài liệu cũ trước code:

- Booking diagram thiếu `NEEDS_REVIEW → AWAITING_OWNER_CONFIRMATION` khi customer bổ sung; payment diagram đã có `NEEDS_REVIEW → TRANSFER_REPORTED`.
- Reject sequence còn `API → Notification` trực tiếp; sửa thành ghi outbox trong transaction và Worker dispatch sau commit.
- Contract evidence cũ nhận `proofObjectKey`; thay bằng `proofUploadId` đã READY/đúng scope. Object key chỉ server tra từ upload, không tin key do client gửi.

## 5. State machine bắt buộc

| Lệnh | Trạng thái nguồn booking/payment | Trạng thái đích booking/payment | Allocation |
|---|---|---|---|
| Báo chuyển lần đầu, còn hạn | AWAITING_TRANSFER / AWAITING_TRANSFER | AWAITING_OWNER_CONFIRMATION / TRANSFER_REPORTED | Giữ RESERVED |
| Owner yêu cầu bổ sung | AWAITING_OWNER_CONFIRMATION / TRANSFER_REPORTED | NEEDS_REVIEW / NEEDS_REVIEW | Giữ RESERVED |
| Customer bổ sung | NEEDS_REVIEW / NEEDS_REVIEW | AWAITING_OWNER_CONFIRMATION / TRANSFER_REPORTED | Giữ RESERVED |
| Owner xác nhận | AWAITING_OWNER_CONFIRMATION / TRANSFER_REPORTED hoặc NEEDS_REVIEW / NEEDS_REVIEW | CONFIRMED / PAID | Giữ RESERVED |
| Owner từ chối cuối | Hai cặp trạng thái chờ nói trên | PAYMENT_REJECTED / REJECTED | Release trong cùng transaction |
| Worker expiry trước báo chuyển | AWAITING_TRANSFER / AWAITING_TRANSFER và now ≥ deadline | EXPIRED / EXPIRED | Release trong cùng transaction |

Bổ sung evidence từ NEEDS_REVIEW không bị chặn bởi payment deadline ban đầu. Không cho báo lần đầu khi đã hết hạn dù Worker chưa chạy, không revive EXPIRED/REJECTED, không ghi đè evidence cũ, không confirm đơn chưa báo chuyển hoặc đã terminal. Retry đúng idempotency key của lệnh đã commit trả kết quả hợp lệ mà không thực hiện transition lần hai.

Mỗi transition thực sự làm tăng `Booking.Version` đúng một lần; trạng thái booking/payment phải nhất quán. Đọc DTO bằng query/transaction phù hợp để không ghép status cũ của booking với status mới của payment khi có request đồng thời.

## 6. API/error contract

Base `/api/v1`, camelCase, bearer auth, response `{data,traceId}`, lỗi Problem Details. Giữ catalog hiện có và ghi endpoint bổ sung vào `docs/04-api-security-delivery.md`.

### Customer báo chuyển/bổ sung

`POST /bookings/{id}/transfer-evidence` — CUSTOMER ACTIVE sở hữu booking.

```http
Idempotency-Key: <key-của-ý-định-báo-chuyển>
If-Match: "<booking-version>"
```

```json
{
  "proofUploadId": null,
  "note": "Đã chuyển theo nội dung mã đặt sân"
}
```

- Một endpoint có thể xử lý báo lần đầu và bổ sung từ NEEDS_REVIEW; server xác định transition từ trạng thái đã khóa. Document rõ hai trường hợp và validation supplement có nội dung theo yêu cầu đối chiếu.
- Lưu evidence append-only, actor/time và loại INITIAL/SUPPLEMENT; response booking/detail mới, payment status, version, evidence metadata và reportedAt. Không gửi URL ký/key nội bộ trong outbox hoặc audit.
- Bộ đếm hạn chỉ áp dụng báo lần đầu. Kiểm tra server time sau khi đã lấy khóa; `now < paymentDeadline` mới được báo lần đầu, tại đúng deadline là hết hạn.

### Upload/read biên lai private

- Thiết kế endpoint gắn booking, ví dụ `POST /bookings/{id}/proof-uploads/presign`, nhận `contentType,sizeBytes,sha256Base64`. Venue/customer/booking/purpose `PAYMENT_PROOF` do server suy ra từ booking đã scope.
- Dùng lại storage/signing/verification của media; có thể trích service chung vừa đủ. Complete/read phải kiểm tra customer-owner hoặc operator đúng quyền/scope **cho purpose biên lai**, giữ nguyên guard QR/ảnh venue.
- `proofUploadId` phải READY, đúng customer, booking, venue và purpose; không dùng ảnh cơ sở/QR, file của đơn khác hoặc customer khác làm biên lai.
- Xác định read route private, ví dụ `GET /bookings/{id}/transfer-evidence/{evidenceId}/proof` cho customer; operator có route scoped tương ứng. GET không public, không cache qua proxy; signed GET ngắn hạn chỉ tạo sau auth. Không yêu cầu owner sở hữu upload mới được xem biên lai của customer thuộc đơn mình có quyền xử lý.
- Upload xong sau deadline không làm hồi sinh đơn và không tự báo chuyển. Nếu người dùng bỏ ảnh, mất mạng hoặc upload chưa READY thì có hướng retry rõ ràng.

### Operator đọc và xử lý

- `GET /operator/venues/{venueId}/bookings`: danh sách scoped, filter `status,dateFrom,dateTo`, phân trang cursor/limit có giới hạn. Ngày filter theo timezone venue, boundary UTC chuẩn; mặc định dễ mở nhóm Chờ xác nhận/Cần bổ sung. Không trả tất cả booking rồi lọc tại frontend.
- `GET /operator/bookings/{id}`: chi tiết scoped, customer display tối thiểu cần đối chiếu, court/venue/date/time, snapshot giá/QR, deadline, version, lịch sử evidence/quyết định và action được phép. Không mở guard CUSTOMER của GET F05 một cách thiếu scope để operator đọc mọi đơn.
- `GET /payments/{id}` theo catalog nếu triển khai: chỉ customer sở hữu hoặc operator đúng scope; dùng cùng payment DTO/service, không tạo một đường đọc không kiểm quyền.
- `POST /operator/bookings/{id}/confirm-payment`: `Idempotency-Key`, `If-Match`; body `{confirmedAmount,note?,bankReference?}`. UI không yêu cầu mã giao dịch; API giữ reference optional cho client cũ, không sửa lịch sử customer. Confirm có số tiền lệch trả lỗi không mutation; owner chọn Yêu cầu bổ sung/đối chiếu bằng lệnh riêng, không âm thầm chuyển state trong response lỗi.
- `POST /operator/bookings/{id}/reject-payment`: `Idempotency-Key`, `If-Match`; body `{resolution,reasonCode,reason}` với resolution `NEEDS_REVIEW` hoặc `FINAL_REJECTION`. Cả hai có lý do rõ cho customer; chỉ FINAL_REJECTION release. Dùng một enum/contract thống nhất, không để frontend suy ra “từ chối” có release hay không.
- Các lệnh owner kiểm tra OWNER ACTIVE, user ACTIVE, business scope, permission tương đương `payment.confirm`. Ghi chính sách xử lý đơn cũ khi venue/court tạm ngừng: không tự expire/chuyển trạng thái; quyền đọc/đối chiếu phải được thiết kế rõ, không sao chép guard chỉ phục vụ tạo booking mới rồi làm mất đường xử lý tiền đã báo.

### Notification

Mở rộng endpoint notifications hiện có với metadata loại entity, bookingId và đích nội bộ có allowlist; giữ field cũ để approval UI không hỏng. Có unread count/phân trang phù hợp nếu cần. Customer/operator mở đúng trang chi tiết; notification không cấp quyền truy cập. `POST .../read` vẫn idempotent và chỉ thuộc user; không bắt buộc booking version cho thao tác đọc thông báo.

### Lỗi phải chốt và test

| HTTP/code | Khi nào |
|---|---|
| 400 VALIDATION_FAILED / UNSUPPORTED_FIELD | Input/amount/enum/length/header sai; JSON field lạ/trùng theo chuẩn hiện tại |
| 401 UNAUTHORIZED | Chưa đăng nhập, hết phiên hoặc user không còn hợp lệ |
| 403 FORBIDDEN | Sai account type/quyền |
| 404 NOT_FOUND | Booking/payment/evidence/upload/notification không tồn tại hoặc ngoài resource scope |
| 409 PAYMENT_DEADLINE_EXPIRED | Báo lần đầu tại/sau deadline; không thay đổi đơn |
| 409 STATE_CONFLICT | Transition không hợp lệ, terminal, command mới trên đơn đã xử lý |
| 409 IDEMPOTENCY_KEY_REUSED | Cùng actor/operation/key nhưng booking hoặc body khác |
| 409 PAYMENT_AMOUNT_MISMATCH | Nếu chính sách chốt chỉ cho xác nhận đủ đúng số tiền |
| 409 UPLOAD_NOT_READY / UPLOAD_MISMATCH | Upload chưa READY hoặc metadata/checksum sai |
| 412 PRECONDITION_FAILED | Version stale của một ý định mới |
| 428 PRECONDITION_REQUIRED | Thiếu If-Match, nếu chọn contract này; đồng bộ với cách xử lý header các endpoint hiện hành |
| 429 / 503 MEDIA_UNAVAILABLE | Rate limit hoặc adapter media không khả dụng |

Chốt response success/status code và missing-header behavior trong spec. Không lộ SQL, PII, số tài khoản đầy đủ, raw object key hoặc signed URL qua errors/list/public endpoints. Frontend hiển thị lỗi có hướng xử lý, version conflict thì reload để owner xem lại trước quyết định.

## 7. Dữ liệu và transaction

Migration F06 tăng dần, bảo toàn dữ liệu/snapshot/constraint F05:

- Thêm `payment_evidence`: liên kết payment/booking, actor customer, reference, note, upload nullable, INITIAL/SUPPLEMENT, timestamp và sequence/version phù hợp. Lịch sử không update/delete đè. Object key/type/size/checksum lấy từ upload đã xác minh; signed URL không lưu DB.
- Mở rộng payment với first/last reportedAt, confirmedAmount, confirmedBy/At, reference/note owner đã đối chiếu, review/reject metadata cần thiết. Check constraint PAID bắt buộc người/thời gian/số tiền phù hợp chính sách. Review/reject bắt buộc lý do; dùng lịch sử quyết định để không mất lần yêu cầu bổ sung trước.
- Evidence/upload có FK và ràng buộc đủ để không liên kết sai booking/customer/venue. Nếu MediaUpload thêm BookingId nullable cho PAYMENT_PROOF, giữ QR/VENUE_IMAGE cũ hợp lệ; kiểm tra upgrade và purpose invariants. Không chỉ dựa trên một if tại UI.
- Có timeline read model/event append-only cần cho UI; không public toàn bộ audit nội bộ hoặc dữ liệu nhạy cảm. Dùng audit hiện có cho actor/action/entity/correlation/time; chỉ lưu metadata được phép.
- Generalize idempotency cho customer/operator với unique `(actor,operation,key)`, canonical hash gồm actor, operation, bookingId và body chuẩn hóa; bảo toàn `CASUAL_CREATE` cũ. Không dùng một key chung giữa confirm/reject/report. Không xóa record khiến retry tạo side effect trùng khi đơn còn cần tra cứu.
- Index list theo venue/status/date, evidence theo payment/time, pending SLA và outbox; FK delete restrict phù hợp lịch sử tài chính. Tiền `numeric(18,0)`/long có giới hạn/overflow, thời điểm UTC, hiển thị theo timezone snapshot.

Với mỗi command mutation:

1. Validate payload/header; xác thực actor và resource scope từ DB.
2. BEGIN; chống đua idempotency bằng khóa/unique trong DB. Cùng key/body đã commit thì trả kết quả cùng booking **trước** kiểm tra stale If-Match, deadline và trạng thái hiện tại; vẫn kiểm tra quyền hiện tại. Không insert evidence/outbox/audit lại.
3. Khóa booking rồi payment/allocation theo thứ tự thống nhất với expiry; đọc lại state/version và auth cần thiết trong transaction. Nếu phải khóa user/membership hoặc business/venue/court, ghi toàn bộ lock order và kiểm tra không có vòng khóa với F03/F05/Worker; không tự thêm khóa ngược chiều.
4. Với lệnh mới: kiểm tra If-Match, transition, deadline hoặc amount policy; xác minh evidence upload từ server. Đừng kiểm tra deadline chỉ trước khi đợi khóa.
5. Ghi state/version/evidence hoặc quyết định/allocation nếu cần, audit, outbox và idempotency trong **cùng transaction** rồi commit.
6. Không gửi notification/email/HTTP provider trong transaction booking. Upload HEAD/file IO nên hoàn tất ở bước READY trước report; không giữ booking lock trong lúc upload ảnh.

Races bắt buộc xử lý:

- Report commit hợp lệ trước expiry → Worker đọc lại không expire. Expiry thắng hoặc report lấy khóa sau deadline → report 409, không revive.
- Hai owner confirm/reject đồng thời → đúng một transition thắng; không PAID đồng thời allocation RELEASED.
- Owner xử lý trong lúc customer bổ sung → version/state đảm bảo kết quả nhất quán; request thua reload, không mất evidence đã commit.
- Confirm/reject timeout sau commit → retry cùng key trả kết quả, không notification/audit lặp.
- Sau terminal reject, F04 thấy slot còn trống và F05 đặt mới được; booking cũ không thể confirm lại. Sau CONFIRMED, slot vẫn bị exclusion bảo vệ.

## 8. Outbox, in-app và SLA

- Mở rộng dispatcher hiện có hoặc trích service hợp lý; giữ nguyên approval/F05 events. Event mới tối thiểu: transfer reported/supplemented, needs review, confirmed, final rejected, confirmation overdue. Chốt tên event trong spec.
- Event báo chuyển phải đến owner ACTIVE đúng business sở hữu venue; staff chỉ nhận khi permission/scope đã thực sự tồn tại. Không broadcast toàn bộ operator/Admin do TargetUserId null.
- Chốt recipient resolution: snapshot recipient IDs trong transaction hoặc explicit event/target kind và query scoped khi dispatch. Khi quyền bị thu hồi trước dispatch, không để thông báo/preview/evidence lộ dữ liệu ngoài quyền hiện tại; tất cả link/read vẫn reauthorize.
- Owner notification có booking code, khách hiển thị tối thiểu, venue/court, ngày/giờ, amount, reportedAt và liên kết chi tiết. Không kèm ảnh/signed URL/số tài khoản đầy đủ. Chặn payload quá dài theo giới hạn notification hiện có.
- Quyết định owner ghi outbox cho đúng customer. NEEDS_REVIEW có lý do và link bổ sung; CONFIRMED hiển thị Đã xác nhận; FINAL_REJECTION hiển thị lý do và slot đã release.
- Worker `FOR UPDATE SKIP LOCKED`, atomic notification insert + processed mark, unique `(OutboxMessageId,UserId)` và retry/backoff. Hai worker/restart/crash không tạo thông báo trùng. Không nuốt event không biết hoặc no-recipient rồi đánh dấu thành công âm thầm.
- Chốt ngưỡng retry cảnh báo/configuration, lưu attempts/last failure metadata không nhạy cảm, có cảnh báo và cách retry vận hành. Không để poison event chặn cả hàng đợi; giữ message để xử lý lại, không log proof/token/payload chứa PII.
- SLA Worker dùng thời điểm báo chuyển đã lưu, chính sách được chốt và status pending; cảnh báo bền vững/dedup theo booking và mốc SLA. Confirm đồng thời scan không được phát cảnh báo mới cho đơn terminal sau khi kiểm tra dưới khóa. Quá SLA **không release**, không chuyển EXPIRED/REJECTED, không đánh dấu PAID.
- In-app là bắt buộc. Email/SMS/Zalo tùy chọn và không được coi là đã triển khai nếu chưa có adapter/config/test; không gửi tin ra ngoài khi chưa có authorization/config phù hợp.

## 9. UI tích hợp

### Customer

- Detail đơn đang AWAITING_TRANSFER có **Đã chuyển khoản**, form ảnh chụp màn hình/ghi chú tùy chọn, trạng thái upload và gửi; chỉ gửi được khi hợp lệ/còn hạn, không có ô mã giao dịch. UI không giả lập PAID.
- Sau report: hiển thị **Chờ xác nhận**, thời điểm báo, evidence/timeline, số tiền và thông tin booking snapshot. Bỏ countdown có ý nghĩa tự hủy sau khi đã báo; thông báo rõ sân vẫn giữ trong lúc đối chiếu.
- NEEDS_REVIEW: hiện lý do và **Bổ sung bằng chứng**, giữ lịch sử cũ. Không yêu cầu tạo lại booking hoặc chuyển lại toàn bộ tiền; supplement vẫn làm được sau deadline ban đầu.
- CONFIRMED: Đã xác nhận, đúng sân/ngày/giờ; không hiện action cancel/reschedule/check-in. REJECTED/EXPIRED có nhãn/lý do phù hợp, không còn nút report/confirm.
- Đơn của tôi/detail/notification thống nhất mapping cả 6 status, trạng thái bằng chữ và màu. Có thông báo chưa đọc, mark read và link booking chính mình.
- Retry cùng một ý định giữ Idempotency-Key/body; thay đổi nội dung cho ý định mới tạo key mới. Double-click, abort, mất mạng sau commit và F5 phải đọc lại server state, không tự POST lại theo mount effect.
- Kế thừa session refresh single-flight/generation guard và returnTo nội bộ. Không thay persistence/login policy F01 trong F06. Với F5 yêu cầu đăng nhập lại của customer hiện có, đăng nhập xong mở đúng đơn; không lưu auth token/reference/ảnh vào URL/localStorage.

### Partner

- Thêm **Đơn đặt sân** vào sidebar/menu mobile, deep link tới đơn cụ thể. Giữ business/venue selection scoped; đổi scope hủy request cũ, xóa detail/input của đơn trước, không cập nhật kết quả từ response cũ vào cơ sở mới.
- Danh sách lọc theo cơ sở/status/ngày, paging, loading/empty/error/retry, badge Chờ xác nhận/Cần bổ sung và thời gian chờ/SLA. Counters phải dựa trên server count phù hợp, không coi 20 dòng trang đầu là tổng mọi đơn.
- Detail/card/drawer responsive có mã đơn, khách, cơ sở/sân, ngày/giờ/timezone, từng ca/tổng snapshot, mã giao dịch, ảnh private và lịch sử. Không đưa metadata kỹ thuật/raw enum/traceId vào flow bình thường; traceId chỉ phục vụ chi tiết lỗi khi cần.
- Tách rõ **Xác nhận đã nhận tiền**, **Yêu cầu bổ sung**, **Từ chối cuối cùng**. Confirm nhập số tiền owner đối chiếu, ghi chú tùy chọn, không nhập mã giao dịch; review/reject có reason. Trước final reject có màn xác nhận rõ sẽ giải phóng ca đã giữ.
- Action chỉ hiện đúng quyền/status nhưng backend vẫn enforce. Pending onboarding không có quyền vận hành đơn. 412/409 reload rồi cho owner xem lại; không tự retry quyết định tài chính với version mới.
- Notifications hiện có được nâng cấp polling/unread/link; vẫn hiển thị đúng thông báo hồ sơ F02. Deep link qua đăng nhập quay về đơn hợp lệ, không auto-submit action.

Tách module vừa đủ vào `features/bookings`/`features/payments`/`features/notifications`, API/types/hooks theo nghiệp vụ khi hữu ích. Dùng CSS/components/layout hiện có, không đưa thêm router/store/UI library chỉ để giống mẫu cây thư mục. Keyboard/focus/labels, dialog focus, text lỗi, target cảm ứng và 375/768/1024/1440px phải được kiểm tra thực tế.

Polling lấy server state định kỳ khoảng 5 giây khi trang visible và khi focus, cleanup timer/request/blob URL khi unmount/logout/đổi đơn, tránh chồng request và backoff khi lỗi. Không làm mất nội dung form đang nhập hoặc spam thông báo. Không quảng cáo realtime tuyệt đối nếu thực tế là polling; DB transaction/exclusion mới bảo vệ chống trùng.

## 10. Acceptance và testcase cần có trước code

Mỗi testcase ghi ID, mục tiêu, tiền điều kiện, dữ liệu thử, bước, kết quả mong đợi, loại test và kết quả thực tế. Tối thiểu:

| ID | Trường hợp | Bằng chứng cần có |
|---|---|---|
| F06-T01 | F04 chọn ca → F05 tạo đơn/QR → báo chuyển → owner nhận in-app → confirm → customer thấy Đã xác nhận | Browser + API/Worker/PostGIS thật; hai cổng và payment/allocation đúng |
| F06-T02 | Reference/proof/note hợp lệ và invalid; upload thiếu READY, checksum/MIME/magic/size sai, field lạ/trùng | API/media local runtime; không state/outbox khi request lỗi |
| F06-T03 | Báo lần đầu trước/tại/sau deadline, Worker chưa kịp expiry | Đồng hồ kiểm soát, DB thật; tại/sau deadline bị từ chối |
| F06-T04 | Report ↔ expiry race, nhiều Worker | Khóa/barrier transaction PostGIS, không sleep đoán; đúng một kết quả, không PAID/RELEASED sai |
| F06-T05 | Same key/body song song + timeout sau commit + retry khi version/state đã đổi | Một evidence/transition/audit/outbox; replay đúng booking, không 412 giả |
| F06-T06 | Cùng key khác body/booking; key khác sau report/terminal | 409 theo contract, không side effect lặp |
| F06-T07 | NEEDS_REVIEW → supplement sau deadline cũ → chờ xác nhận → confirm | Evidence/history append-only, Worker không release trong toàn luồng |
| F06-T08 | Owner final reject từ chờ/review, lý do bắt buộc | REJECTED/PAYMENT_REJECTED, release atomic, customer nhận lý do; F04/F05 đặt mới được |
| F06-T09 | Confirm đủ tiền/amount mismatch/overflow theo policy; không confirm trước report | Metadata người/time/amount hợp lệ, snapshot không đổi; PAID/CONFIRMED đồng thời |
| F06-T10 | Hai owner confirm↔reject; owner confirm↔customer supplement | Một transition hợp lệ, version tăng đúng; không mất evidence hoặc trả tiền trạng thái lệch |
| F06-T11 | Guest/customer khác/operator khác business/pending/revoked/suspended/Admin sai quyền | API list/detail/payment/action/proof đều kiểm scope; notification không vượt quyền |
| F06-T12 | Proof upload customer/booking/venue khác, QR/VENUE_IMAGE, object key/path tự chọn; anonymous đọc proof | Không đọc/attach file ngoài scope; private adapter và lỗi đúng |
| F06-T13 | Owner đúng scope xem customer upload; list/public/log/error không lộ ảnh/key/account/token | Kiểm DTO/log/header và private URL lifetime; S3 live tách NOT RUN |
| F06-T14 | Outbox crash trước/sau insert, restart/retry/backoff, hai dispatcher, no recipient/unknown event | Không trùng notification, không mất message, có cảnh báo lỗi; approval/F05 vẫn đúng |
| F06-T15 | Quá SLA ở chờ/review; scan lặp; confirm đồng thời; owner quyền thay đổi | Dedup/recipient đúng, không expire/release; terminal không bị cảnh báo mới |
| F06-T16 | Đổi giá/QR/hold policy sau khi booking tạo hoặc đang review | Amount/từng ca/QR/deadline snapshot cũ không đổi; confirm theo đơn cũ |
| F06-T17 | Fresh migration/repeat/upgrade DB có booking, media QR/ảnh venue F05 và idempotency cũ | FK/check/index/exclusion giữ đúng, retry CASUAL_CREATE cũ vẫn đúng |
| F06-T18 | Customer F5/login returnTo/back, owner detail deep link, double-click/retry/412/409 | Browser desktop/mobile, không tự POST/mất đơn/mất draft do polling |
| F06-T19 | Partner đổi business/venue trong lúc request/polling, unread/read/link/error/retry | Không lẫn dữ liệu, link reauthorize, không hỏng notifications approval |
| F06-T20 | Slot đã CONFIRMED, sau endsAt, maintenance/booking khác giao nhau, các ca liền kề | Exclusion vẫn giữ khoảng cũ, CONFIRMED không chuyển completed hoặc release |

Integration transaction/constraint/idempotency/concurrency phải dùng PostgreSQL/PostGIS thật; không dùng EF InMemory/SQLite hoặc đọc source làm bằng chứng. Browser mock chỉ chứng minh UI/error states; bổ sung luồng customer và partner chạy API/Worker/DB thật.

## 11. Thực thi và kiểm chứng

1. Lập spec/testcase, đối chiếu các mâu thuẫn và chốt policy.
2. Thiết kế migration/authorization/transaction/outbox trước khi chỉnh code; giao subtask độc lập có contract/file ownership rõ nếu dùng các vai trò được AGENTS.md cho phép.
3. Implement lát cắt DB/API → customer report → owner detail/actions → Worker notifications/SLA → UI cập nhật; chạy test theo từng rủi ro và sửa lỗi còn mở.
4. Review quyền/scope, transition, deadline, lock order, upload private, retries/snapshot và migration. Reviewer độc lập khi có thể; nếu review tuần tự thì ghi rõ giới hạn.
5. Chạy regression bị ảnh hưởng F01–F05 và partner UI, xem screenshot desktop/mobile, rồi viết hướng dẫn nghiệm thu.

Lệnh nền trong **PowerShell terminal VS Code, tại root repository**:

```powershell
npm.cmd run db:up
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:api
npm.cmd run test:db
npm.cmd run typecheck
npm.cmd run build
npm.cmd run test:web
npm.cmd run test:identity-live -- -ApiPort 5081
git diff --check
```

Đọc scripts trước chạy và cập nhật command chính xác cho test F06 mới. `test:identity-live` hiện chưa chứa F06; mở rộng hoặc thêm runner live riêng, không gọi suite F01–F05 PASS là F06 PASS. Test DB dùng database tạm tên/host xác minh; test live truyền API URL cho browser rõ ràng để không chạm nhầm API 5080/DB development. Không dừng dev server của người dùng để lấy cổng; dùng cổng trống nếu có xung đột. Preview IPv4/resolver trong Playwright phải giữ origin localhost theo CORS.

Nếu Docker/DB chưa mở, ghi BLOCKED đúng bước, tiếp tục phần độc lập; khi DB có lại chạy phần còn thiếu. Không thay đổi ExecutionPolicy toàn máy; dùng wrapper PowerShell đã có. Không migrate DB development để chạy test hoặc drop database chỉ dựa vào biến tên chưa xác minh.

## 12. Bàn giao và trạng thái

Cập nhật:

- `docs/features/F06-payment-confirmation.md` — contract, state machine, policy đã chốt, data/lock/security/acceptance.
- `docs/testing/F06-test-cases.md` — lệnh, ngày/môi trường, PASS/FAIL/NOT RUN/BLOCKED và bằng chứng thực tế.
- `docs/testing/F06-manual-acceptance.md` — hướng dẫn tiếng Việt chi tiết cho terminal VS Code và browser: chuẩn bị 2 customer/2 owner khác business, tạo đơn thật, report/confirm/review/supplement/reject/expiry/SLA, retry/version/scope, kiểm database read-only và kết quả mong đợi.
- `docs/02-states-and-sequences.md`, `03-data-and-architecture.md`, `04-api-security-delivery.md`, `docs/process.md`, `docs/setup.md` khi contract/decision thay đổi; giữ lịch sử milestone trong `docs/progress.md`.

Hướng dẫn tay phải giải thích mỗi thao tác đang kiểm gì, chạy ở terminal/thư mục nào, kết quả HTTP/UI/DB nào đạt, các lỗi thường gặp và cách khởi động API/Worker/web. Không bắt người dùng chờ holdMinutes thật cho mọi ca tự động; expiry/SLA test dùng TimeProvider/test DB tạm phù hợp, nghiệm thu tay có phương án an toàn và rõ ràng.

Báo ngắn phần đã làm, file chính, test đã chạy, lỗi đã sửa và điểm còn mở. Không đổi F05 sang DONE chỉ vì thực hiện F06; giữ đúng trạng thái nghiệm thu đã ghi. Chỉ F06 DONE khi acceptance và kiểm thử bắt buộc đạt, không còn lỗi nghiêm trọng. Nếu nghiệm thu tay còn chờ thì ghi rõ mốc code/test local và bước nghiệm thu tiếp theo theo quy trình hiện hành. S3 live hoãn là mốc riêng được người dùng cho phép; không đánh dấu nó PASS hoặc tuyên bố AWS đã nghiệm thu. Không tự push Git.
