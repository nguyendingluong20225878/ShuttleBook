# F04 — tìm sân công khai, lịch theo cơ sở và ảnh S3 private

Trạng thái: **DONE** (người dùng chấp nhận nghiệm thu ngày 2026-10-07). Phạm vi được chốt lại là tìm sân, lịch và ảnh private bằng adapter local; S3 provider thật được dời sang mốc tích hợp media riêng, vẫn **NOT RUN**. Không diễn giải DONE thành bằng chứng S3 live đã đạt.

## Phạm vi và quyết định

- Guest/Customer xem danh sách, tìm theo chữ, tìm gần theo tọa độ/bán kính, xem venue và lịch cho mọi court ACTIVE của cùng venue. Bảng giống ảnh tham chiếu của người dùng: hàng là court, mỗi ô giá nằm giữa hai mốc giờ cách nhau 30 phút.
- Nearby chỉ lọc theo geography của venue và bán kính, không nhận ngày/giờ hoặc truy vấn allocation. Sau khi chọn venue mới tải availability và giá. Giá F04 là giá hiện hành tham khảo; quote có expiry và booking thuộc F05 dù UC-03 trong tài liệu nghiệp vụ có nhắc quote.
- F04 không tạo booking/hold/payment/series, không tính giá nhóm khách hoặc sửa giá thủ công. Không tạo schema theo ca. Chưa triển khai album nhiều ảnh; dùng `venues.image_upload_id` F02.
- MapTiler hỗ trợ nhập khu vực và hiển thị bản đồ; PostGIS lọc/sắp xếp gần. Nếu khách từ chối geolocation hoặc MapTiler lỗi, tìm tên/địa chỉ trên danh sách vẫn dùng được.
- Frontend tách theo `features/auth` và `features/venues`, có `App.tsx`, trang và dịch vụ thực sự cần. Số route ít nên dùng History API hiện có; chưa cần Context/store hay dependency định tuyến mới.

## Dữ liệu và quyền

- Chỉ `businesses.status=ACTIVE`, `venues.status=PUBLISHED`, court `ACTIVE` được công khai. Venue không có court active bị ẩn khỏi search. Detail/availability/ảnh của venue không công khai trả `404 NOT_FOUND`.
- Dùng `venues.location geography(Point,4326)` với GiST index hiện có. Gần theo `ST_DWithin` mét và thứ tự `ST_Distance`, tie-break ID. Không ghi tọa độ khách vào DB/log/audit.
- Availability sinh từ `court_operating_hours` theo local date/time của `venues.timezone`, biến từng ca thành khoảng UTC `[start,end)`, loại ca có `court_allocations.status=RESERVED` giao khoảng (mọi kind). `RELEASED` không khóa. Đầu giờ mở tính gồm, cuối giờ đóng không gồm; ngày local hôm nay trở đi. Ca đã bắt đầu được đánh `PAST`.
- Với mỗi ca, lấy `pricing_rules` của court có `starts_on <= date <= ends_on`, đúng `day_of_week`, khung giờ phủ trọn ca; rule priority cao nhất thắng. `price_per_slot` là VND/30 phút. Thiếu giá thành `NO_PRICE`, không được chọn. Trả bookingBlockMinutes, minimumBookingMinutes, holdMinutes từ F03; UI chỉ chọn các ca liên tiếp cùng court, tổng thời lượng đạt minimum và chia hết block. F05 xác thực lại.
- Public DTO chỉ gồm tên/địa chỉ/contact cơ sở, tọa độ venue, timezone, tên court, policy và giá/trạng thái ca. Không trả business contact, số tài khoản/QR, owner/membership/approval, media object key. Không công khai lý do maintenance hoặc thông tin booking của khách.
- Ảnh đại diện chỉ đọc khi upload gắn với `image_upload_id` của venue, cùng venue, `purpose=VENUE_IMAGE`, `status=READY`. S3 bucket private; API kiểm tra publish rồi cấp signed GET tối đa 5 phút. Local adapter đọc file private. QR `purpose=QR` không bao giờ qua public image route.

## API contract

Prefix `/api/v1`, ẩn danh; thành công `{data,traceId}`; lỗi `application/problem+json` với `code/status/traceId`. Danh sách mặc định limit 20, tối đa 100, cursor opaque; giới hạn bán kính nearby mặc định 5000 m, tối đa 50000 m. Query field lạ, giá trị lặp, cursor sai → `400 VALIDATION_FAILED`.

| Route | Query | `data` |
|---|---|---|
| `GET /venues` | `q?`, `limit?`, `cursor?` | `{items:[{id,name,address,latitude,longitude,imageUrl}],nextCursor}`; q tìm name/address, không tính distance |
| `GET /venues/nearby` | `latitude`, `longitude`, `radiusMeters?`, `limit?`, `cursor?` | Items như trên cộng `distanceMeters`, sắp tăng theo khoảng cách + id |
| `GET /venues/{id}` | không | `{id,name,address,contact,latitude,longitude,timezone,imageUrl,courts:[{id,name,bookingBlockMinutes,minimumBookingMinutes,holdMinutes}]}` |
| `GET /venues/{id}/availability` | `date=YYYY-MM-DD`, `courtId?` | `{venueId,date,timezone,generatedAt,stepMinutes:30,courts:[{courtId,name,bookingBlockMinutes,minimumBookingMinutes,holdMinutes,slots:[{startsAt,endsAt,startsAtUtc,endsAtUtc,status,pricePerSlot}]}]}` |
| `GET /venues/{id}/image` | không | 302 sang signed S3 GET ≤5 phút hoặc bytes từ local private adapter; 404 nếu ảnh không công khai |

`status` là `AVAILABLE`, `RESERVED`, `NO_PRICE`, `PAST`; UI tự tô các ô ngoài giờ/đóng cửa màu trung tính khi dựng trục giờ chung. Với ca RESERVED có thể trả giá hiện hành nhưng không cho chọn; không trả kind/lý do allocation. Slot `startsAt/endsAt` dùng `HH:mm` local, UTC dùng ISO-8601 `Z`; `pricePerSlot` là số VND nguyên hoặc null. `generatedAt` là UTC ISO-8601. Nếu ngày local sai/quá khứ, tọa độ ngoài [-90,90]/[-180,180], radius ≤0/>50000, limit ngoài 1–100, query thiếu/không hợp lệ: `400 VALIDATION_FAILED`. Court không thuộc venue/không active, venue chưa published: `404 NOT_FOUND`. S3 lỗi: `503 MEDIA_UNAVAILABLE`. Danh sách rỗng trả 200 với `items=[]`.

## UI và ảnh tham chiếu

- `/venues` có tìm chữ, dùng vị trí hiện tại, gõ/chọn khu vực MapTiler, bán kính, danh sách và marker. `/venues/{id}` có thông tin venue, ảnh, ngày và bảng tất cả court. Link đăng ký/đăng nhập F01 vẫn hoạt động.
- Bảng: cột 30 phút đồng bộ; mốc giờ đặt ở đường biên của ô giá, có cả mốc kết thúc cuối cùng. Tất cả court ACTIVE là các hàng, không có dropdown lọc sân; header giờ và nhãn court sticky, cuộn ngang trên màn nhỏ. `AVAILABLE` nền sáng; `RESERVED` nền san hô; `NO_PRICE`/ngoài giờ nền xám; `PAST` màu trung tính; `SELECTED` tím. Mỗi ô có nhãn trạng thái/giá cho screen reader, không chỉ dựa màu. Chú giải hiển thị cạnh lịch. Chọn các ca liên tiếp trên một court để tính tổng tham khảo; bấm lại ô đầu/cuối đã chọn chỉ bỏ ô đó và giữ các ô còn lại. Nếu bấm ô ở giữa, giữ đoạn liên tiếp dài hơn để không tạo hai dải rời. Không có nút đặt ở F04.
- Tải lại khi đổi ngày/focus tab và tối đa 30 giây khi trang hiện; stale request phải bị bỏ. Không tuyên bố slot được giữ. Có loading/error/empty/retry, fallback ảnh, map lỗi vẫn xem danh sách được; desktop/mobile.

## Acceptance

1. Public filtering, nearby geospatial và search text đúng; guest không cần đăng nhập.
2. Lịch mọi sân một cơ sở đúng giờ local, giá và trạng thái từ PostgreSQL/PostGIS; thay đổi maintenance phản ánh ở lần đọc tiếp theo; chọn một court đúng policy.
3. Public response và ảnh local private không lộ QR/payment/owner data. S3 bucket private và signed GET thuộc mốc tích hợp media sau khi có bucket thử nghiệm.
4. Customer UI desktop/mobile theo bảng tham chiếu, MapTiler/geolocation có fallback, auth F01 không hồi quy.
5. Kiểm thử DB/API/browser trong phạm vi local đạt và người dùng chấp nhận nghiệm thu tay. S3 bucket test thật vẫn `NOT RUN` theo quyết định hoãn của người dùng; phải kiểm thử riêng trước khi dùng S3 thật.

## Điều chỉnh nghiệm thu UI 2026-10-07

- Theo yêu cầu mới của người dùng, bỏ bộ lọc sân trên customer UI và luôn dựng một hàng cho mỗi court ACTIVE trong response. `courtId` của API vẫn là query tùy chọn cho client khác; không đổi dữ liệu hay migration.
- Mốc 17:00 và 17:30 phải nằm ở hai ranh giới ô giá 17:00–17:30, thay vì giờ nằm giữa ô. Mốc cuối giờ đóng cũng phải hiển thị. Trên mobile vẫn cuộn ngang được.
- Bấm lại một trong hai ô đang chọn chỉ bỏ ô vừa bấm; ô kia và tổng giá tương ứng còn lại. Lựa chọn luôn là một dải liên tiếp trong một sân. Đây là điều chỉnh UI, không đổi contract API/error hay chính sách F03.
