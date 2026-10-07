# State Diagram và Sequence Diagram

## 1. Trạng thái Booking

Customer không có transition hủy hoặc đổi lịch. `AWAITING_OWNER_CONFIRMATION` hiển thị **Chờ xác nhận**; `CONFIRMED` hiển thị **Đã xác nhận** trên cả cổng khách và cổng chủ sân.

```mermaid
stateDiagram-v2
    [*] --> AwaitingTransfer: tạo booking
    AwaitingTransfer --> AwaitingOwnerConfirmation: khách báo đã chuyển
    AwaitingTransfer --> Expired: quá hạn thanh toán
    AwaitingOwnerConfirmation --> Confirmed: operator xác nhận
    AwaitingOwnerConfirmation --> NeedsReview: cần thêm bằng chứng
    NeedsReview --> AwaitingOwnerConfirmation: khách bổ sung bằng chứng
    AwaitingOwnerConfirmation --> PaymentRejected: operator từ chối
    NeedsReview --> Confirmed: giao dịch hợp lệ
    NeedsReview --> PaymentRejected: đối chiếu thất bại
    Confirmed --> [*]: kết thúc luồng trên hệ thống
    Expired --> [*]
    PaymentRejected --> [*]
```

| Transition | Actor | Điều kiện | Tác động |
|---|---|---|---|
| Tạo → `AwaitingTransfer` | Customer/API | Court trống, quote hợp lệ và venue có QR nhận tiền hợp lệ | Tạo allocation, payment deadline và snapshot QR do chủ sân cung cấp |
| `AwaitingTransfer` → `AwaitingOwnerConfirmation` | Customer | Còn deadline | Lưu evidence, dừng auto-expiry và thông báo operator |
| `AwaitingTransfer` → `Expired` | Worker | Quá deadline, chưa report transfer | Giải phóng allocation |
| Chờ xác nhận → `Confirmed` | Operator | Đúng venue scope và đúng giao dịch | Payment `PAID`, lưu `confirmed_by/at` |
| Chờ xác nhận → `NeedsReview` | Operator | Cần đối chiếu thêm, có lý do | Giữ allocation, payment `NEEDS_REVIEW`, outbox tới customer |
| `NeedsReview` → Chờ xác nhận | Customer | Bổ sung evidence thuộc đơn mình | Payment `TRANSFER_REPORTED`, lưu thêm history, outbox tới owner; không áp dụng deadline cũ |
| Chờ xác nhận → `PaymentRejected` | Operator | Có lý do hợp lệ | Giải phóng allocation khi terminal |

`CONFIRMED` là trạng thái cuối của luồng booking thành công. Khách chỉ cần đến sân chơi theo lịch; mọi vấn đề sau xác nhận thanh toán được giải quyết trực tiếp với nhân viên tại sân. Không có check-in/check-out, trạng thái vắng mặt hoặc hoàn thành. Booking giữ trạng thái `CONFIRMED` sau giờ chơi; allocation vẫn bảo vệ khoảng thời gian đã đặt, không cần thao tác đóng booking.

## 2. Trạng thái Payment

```mermaid
stateDiagram-v2
    [*] --> AwaitingTransfer: booking được tạo
    AwaitingTransfer --> TransferReported: khách báo chuyển, có thể gửi ảnh
    AwaitingTransfer --> Expired: quá deadline
    TransferReported --> Paid: operator xác nhận
    TransferReported --> NeedsReview: thiếu bằng chứng
    TransferReported --> Rejected: không có hoặc sai giao dịch
    NeedsReview --> TransferReported: khách bổ sung bằng chứng
    NeedsReview --> Paid: operator xác nhận
    NeedsReview --> Rejected: đối chiếu thất bại
    Paid --> [*]
    Rejected --> [*]
    Expired --> [*]
```

## 3. Trạng thái Booking Series

```mermaid
stateDiagram-v2
    [*] --> Draft: khách cấu hình lịch
    Draft --> Quoted: kiểm tra toàn bộ occurrence
    Quoted --> AwaitingTransfer: tạo series và allocations
    Quoted --> ConflictRejected: có ngày xung đột
    AwaitingTransfer --> AwaitingOwnerConfirmation: khách báo đã chuyển
    AwaitingOwnerConfirmation --> Active: operator xác nhận thanh toán
    AwaitingTransfer --> Expired: quá payment deadline
    Active --> [*]: lịch đã xác nhận
    ConflictRejected --> [*]
    Expired --> [*]
```

## 4. Trạng thái hồ sơ Cơ sở

```mermaid
stateDiagram-v2
    [*] --> Draft: chủ sân tự khai báo
    Draft --> PendingApproval: chủ sân gửi duyệt
    PendingApproval --> Draft: Admin yêu cầu chỉnh sửa
    PendingApproval --> Published: Admin duyệt
    Published --> Suspended: Admin khóa
    Suspended --> Draft: Admin mở lại để chỉnh sửa
```

Tài khoản chủ sân được tạo ở `PENDING_ONBOARDING` và chỉ chuyển sang trạng thái vận hành đầy đủ khi hồ sơ đầu tiên được duyệt. Chỉ cơ sở `Published` và các sân `Active` mới xuất hiện trên `tenmien.vn`; operator không được tự chuyển cơ sở sang `Published`. Khi sửa địa chỉ, chủ sở hữu hoặc tài khoản nhận tiền của cơ sở đã publish, hệ thống tạo một bản revision chờ duyệt để dữ liệu đang hoạt động không biến mất ngay.

## 5. Trạng thái Sân

```mermaid
stateDiagram-v2
    [*] --> Inactive: được tạo
    Inactive --> Active: Admin/operator publish
    Active --> Maintenance: bắt đầu bảo trì
    Maintenance --> Active: kết thúc bảo trì
    Active --> Inactive: ngừng nhận booking
    Active --> Suspended: Admin khóa
    Inactive --> Suspended: Admin khóa
    Maintenance --> Suspended: Admin khóa
    Suspended --> Inactive: Admin mở khóa
```

## 6. Đặt vãng lai và xác nhận thanh toán

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant UI as Cổng khách tenmien.vn
    participant API as ASP.NET API
    participant Auth as Authorization
    participant DB as PostgreSQL
    participant W as Worker
    participant N as Notification
    actor O as Venue Operator tại cổng đối tác

    C->>UI: Tại trang sân, chọn Vãng lai và các ca 30 phút liên tiếp
    UI->>API: POST /bookings với Idempotency-Key
    API->>Auth: Kiểm tra CUSTOMER
    API->>DB: BEGIN và insert allocation + booking
    DB-->>API: Exclusion constraint thành công
    API->>DB: Ghi payment, policy snapshot, outbox và COMMIT
    API-->>UI: Booking, QR do chủ sân cung cấp, số tiền và deadline
    C->>UI: Chuyển khoản và báo đã chuyển
    UI->>API: POST /bookings/{id}/transfer-evidence
    API->>DB: Chuyển sang AWAITING_OWNER_CONFIRMATION và ghi outbox
    W->>N: Gửi thông báo cho chủ sân kèm liên kết booking
    N-->>O: Khách đã báo chuyển khoản, chờ xác nhận
    O->>API: POST /operator/bookings/{id}/confirm-payment
    API->>Auth: Kiểm tra permission và business/venue membership
    API->>DB: Payment PAID, booking CONFIRMED và outbox
    API-->>O: Booking CONFIRMED - Đã xác nhận
    W->>N: Gửi xác nhận cho customer
    UI->>API: Tải lại trạng thái booking
    API-->>UI: CONFIRMED - Đã xác nhận
```

## 7. Hai khách đặt cùng một khung giờ

```mermaid
sequenceDiagram
    autonumber
    actor A as Customer A
    actor B as Customer B
    participant API as ASP.NET API
    participant DB as PostgreSQL

    par Request A
      A->>API: Đặt court X, khoảng thời gian T
      API->>DB: INSERT active allocation
    and Request B
      B->>API: Đặt court X, khoảng thời gian T
      API->>DB: INSERT active allocation
    end
    DB-->>API: Transaction A commit
    DB-->>API: Transaction B lỗi exclusion 23P01
    API-->>A: 201 AWAITING_TRANSFER
    API-->>B: 409 SLOT_UNAVAILABLE
    Note over API,DB: Cache không bao giờ là khóa cuối cùng
```

## 8. Tìm sân gần nhất

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant GEO as Browser Geolocation
    participant UI as Cổng khách React
    participant API as ASP.NET API
    participant DB as PostgreSQL PostGIS
    participant MAP as MapTiler

    C->>UI: Chọn Tìm sân gần tôi
    UI->>GEO: Xin quyền lấy vị trí
    alt Đồng ý
      GEO-->>UI: Latitude và longitude tạm thời
    else Từ chối
      UI-->>C: Yêu cầu nhập khu vực
      UI->>MAP: Geocode địa chỉ khách nhập
      MAP-->>UI: Latitude và longitude
    end
    UI->>API: GET /venues/nearby với lat, lng và radius
    API->>DB: Lọc venue PUBLISHED bằng ST_DWithin, sắp xếp ST_Distance
    DB-->>API: Các cơ sở gần theo khoảng cách
    API-->>UI: Danh sách cơ sở, vị trí và khoảng cách
    UI->>MAP: Hiển thị marker trên bản đồ
    UI-->>C: Hiển thị danh sách sân gần
    C->>UI: Chọn sân và mở trang chi tiết
    UI->>API: GET /venues/{id}
    API-->>UI: Chi tiết cơ sở và các sân
    C->>UI: Chọn sân cụ thể, Vãng lai hoặc Cố định, ngày giờ và ca cần đặt
    Note over C,API: Tiếp tục luồng đặt sân chung tại mục 6 hoặc 9
```

Nearby chỉ tìm theo vị trí, không nhận ngày/giờ/số ca, không kiểm tra availability hoặc tạo quote. Sau khi chọn sân, người dùng dùng cùng trang chi tiết và luồng đặt sân như mọi cách tìm sân khác; lịch trống và giá được kiểm tra theo lựa chọn đặt sân tại đó.

## 9. Tạo lịch thuê cố định

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant UI as Cổng khách tenmien.vn
    participant API as ASP.NET API
    participant DB as PostgreSQL

    C->>UI: Tại trang sân, chọn Cố định, một thứ, ít nhất 4 ca và kỳ từ 1 tháng
    UI->>API: POST /booking-series/quote
    API->>API: Kiểm tra bước 30 phút, tối thiểu 2 giờ và 1 tháng
    API->>DB: Sinh toàn bộ occurrence và kiểm tra overlap
    alt Có ngày xung đột
      DB-->>API: Danh sách conflict dates
      API-->>UI: 409 và các ngày không khả dụng
    else Tất cả đều trống
      DB-->>API: Giá và danh sách occurrence
      API-->>UI: Quote và danh sách occurrence
      UI->>API: POST /booking-series với Idempotency-Key
      API->>DB: BEGIN tạo series, bookings và khóa mọi occurrence
      DB-->>API: Tất cả constraint thành công
      API->>DB: Ghi payment, outbox và COMMIT
      API-->>UI: Series AWAITING_TRANSFER kèm QR
      Note over UI,DB: Khách khác không thấy các ca giao với occurrence đã giữ
    end
```

## 10. Booking hết hạn

```mermaid
sequenceDiagram
    autonumber
    participant W as Expiry Worker
    participant DB as PostgreSQL
    participant N as Notification

    W->>DB: Chọn AWAITING_TRANSFER quá hạn FOR UPDATE SKIP LOCKED
    DB-->>W: Batch booking IDs
    loop Mỗi booking
      W->>DB: Set EXPIRED, release allocation và ghi outbox
    end
    W->>N: Gửi thông báo hết hạn
```

## 11. Operator từ chối hoặc yêu cầu kiểm tra

```mermaid
sequenceDiagram
    autonumber
    actor O as Venue Operator
    participant UI as Cổng đối tác partner.tenmien.vn
    participant API as ASP.NET API
    participant Auth as Authorization
    participant DB as PostgreSQL
    participant W as Worker
    participant N as Notification

    O->>UI: Chọn booking đang chờ
    UI->>API: POST reject-payment kèm reason và version
    API->>Auth: Kiểm tra business/venue membership
    API->>DB: Lock booking và payment
    alt Cần thêm bằng chứng
      API->>DB: Booking/payment NEEDS_REVIEW, giữ allocation và outbox
    else Từ chối cuối cùng
      API->>DB: PAYMENT_REJECTED, release allocation và outbox
    end
    API->>DB: COMMIT quyết định và outbox
    W->>DB: Đọc outbox sau commit, retry/idempotency
    W->>N: Thông báo customer
```

## 12. Upload QR hoặc biên lai bằng presigned URL

```mermaid
sequenceDiagram
    autonumber
    actor U as Customer hoặc Operator
    participant UI as Cổng khách hoặc cổng đối tác
    participant API as ASP.NET API
    participant DB as PostgreSQL
    participant S3 as AWS S3

    U->>UI: Chọn ảnh
    UI->>API: POST /uploads/presign kèm size, type, checksum
    API->>DB: Tạo upload PENDING với object key do server sinh
    API-->>UI: Presigned PUT URL ngắn hạn
    UI->>S3: PUT file với checksum
    UI->>API: POST /uploads/{id}/complete
    API->>S3: HEAD object và xác minh metadata
    API->>DB: Mark READY và liên kết resource
```
