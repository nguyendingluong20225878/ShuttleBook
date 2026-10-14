# Prompt hoàn thiện Lịch & giá Partner theo hệ thống hiện hành

Ngày: 2026-10-12. Repo: `C:\Users\luong\Desktop\CLong`. Trang: `http://localhost:5174/#/schedule`.

Đây là prompt để chạy ở lượt triển khai tiếp theo, **chưa phải báo cáo đã sửa trang**. Đã đối chiếu ảnh người dùng, source và đặc tả; browser tool không có browser khả dụng nên chưa đọc phiên đăng nhập local trực tiếp. Các phát hiện từ source bên dưới cần kiểm chứng runtime khi thực thi. Những PASS trước đây trong progress là bằng chứng của mốc trước, không nghiệm thu thiết kế mới này.

---

## Vai trò và mục tiêu

Bạn là reviewer web có 10 năm kinh nghiệm, đồng thời điều phối Product Designer, Technical Designer, Implementer, Independent Reviewer và QA. Hoàn thiện trang Partner **Lịch & giá** theo dữ liệu/nghiệp vụ thực tế của ShuttleBook: dễ hiểu, đúng quyền, đồng bộ với Cơ sở & sân, Customer, Đơn đặt sân, thanh toán và thông báo.

Luồng ALOBO người dùng cung cấp là tham khảo về cách chủ sân bao quát sân và nhận biết khách lẻ/cố định. Chọn cách phù hợp nhất với ứng dụng hiện có; không tự mở thêm nghiệp vụ quản lý dịch vụ tại sân hoặc biến toàn ứng dụng thành ALOBO.

Khi người dùng yêu cầu chạy prompt này, thực hiện phần cải tiến đã đủ điều kiện đến kiểm thử/review local. Những lựa chọn mở rộng nghiệp vụ cần hỏi trước; tiếp tục phần độc lập trong lúc chờ. Không hỏi lại các quyết định đã chốt, không dừng chỉ vì còn nhiều việc.

## 1. Đọc và đối chiếu trước khi sửa

Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`; đặc tả F02/F03/F05/F06/F07 và các quyết định mới nhất:

- `docs/features/Partner-workspace-and-role-contract.md`
- `docs/features/Partner-toast-and-court-confirmation.md`
- `docs/features/Partner-court-numbering-and-actions.md`
- `docs/features/Venue-only-optional-images.md`
- `docs/features/F03-court-operations.md`
- `docs/features/F05-casual-booking.md`
- `docs/features/F06-payment-confirmation.md`
- `docs/features/F07-fixed-series.md`, `F07-designer-notes.md`, `F07-quote-reservations.md`.

Kiểm tra Git và giữ toàn bộ thay đổi có sẵn. Kiểm tra phiên bản API/migration mà local đang chạy trước khi kết luận lỗi UI. Nếu đọc được trang bằng công cụ browser, quan sát phiên người dùng bằng thao tác đọc; không lưu cấu hình, xác nhận tiền hoặc tạo đơn trong dữ liệu của họ chỉ để khảo sát. Nếu chưa đăng nhập, dùng luồng thử với DB/tài khoản riêng hoặc ghi giới hạn; không hỏi người dùng gửi mật khẩu/OTP.

File chính cần kiểm tra:

- UI: `apps/partner-web/src/PartnerOnboarding.tsx`, `PartnerOperations.tsx`, `features/workspace/PartnerOverview.tsx`, `navigation.ts`, `features/bookings/PartnerBookings.tsx`, `BookingDecisions.tsx`, `SeriesSchedule.tsx`, `components/PartnerToast.tsx`, `assets/partner.css`.
- Backend: `backend/src/ShuttleBook.Api/Operations/CourtOperationsEndpoints.cs`, `Discovery/PublicVenuesEndpoints.cs`, `Bookings/BookingEndpoints.cs`, `PaymentEndpoints.cs`, `PaymentCommands.cs`, `BookingReadModel.cs`, `Onboarding/OnboardingEndpoints.cs`, `OnboardingAdditions.cs`.
- Dữ liệu: `backend/src/ShuttleBook.Infrastructure/Bookings/BookingGroup.cs`, `QuoteReservations.cs`, model giá/allocation/payment và exclusion constraint hiện hành.

## 2. Thực trạng đã thấy từ source

1. Cùng route schedule có form nháp **Giờ và giá theo sân** trong PartnerOnboarding và form ACTIVE **Vận hành sân** trong PartnerOperations. Hai dropdown, hai state/form và hai API khác nhau; người dùng khó biết đang cấu hình sân nào.
2. Các nút Quy định đặt/Lịch tuần/Giá theo ngày/Xem thử giá/Bảo trì hiện là anchor cuộn tới một trang dài; tất cả form vẫn xếp nối nhau. Đây chưa phải giao diện theo tác vụ.
3. Form nháp dùng `scheduleRows` chung; đổi `scheduleCourt` hiện chỉ đổi ID, không hydrate dữ liệu sân đang chọn. Phải kiểm chứng nguy cơ mang form của sân A sang sân B.
4. PartnerOperations khởi tạo `courtId` từ choices một lần. Parent keyed theo business đã có bảo vệ khi đổi doanh nghiệp, nhưng choices thay đổi trong cùng business chưa có cơ chế reconcile selection; thêm/xác nhận sân mới cần kiểm tra đồng bộ.
5. Đọc cấu hình có guard theo courtId, nhưng chưa đủ cho request A→B→A hoặc reload cùng ID. Cần generation/abort và chống response cũ ghi đè; bảo vệ form đang sửa trước khi đổi scope.
6. `today()` dùng UTC ISO trong PartnerOperations. Ngày mặc định phải theo timezone của cơ sở, đặc biệt gần 00:00 Việt Nam; không hiển thị dòng `Múi giờ: Asia/Ho_Chi_Minh`.
7. Trong source hiện tại, PricePreview yêu cầu duration chia hết BookingBlockMinutes; Customer đã cho thêm từng ca 30 phút sau minimum. Ví dụ minimum120/block60: 150 phút hợp lệ ở Customer nhưng Partner preview bị chặn. Xác minh và sửa theo contract Customer hiện hành, không sửa Customer về quy tắc cũ để làm hai bên giống nhau.
8. GET operations đọc court/version, hours, prices bằng các query rời chưa có snapshot transaction; cần kiểm tra nguy cơ version/cấu hình không cùng một phiên đọc.
9. API lịch nháp tăng version nhưng chưa kiểm If-Match/không trả version. Chuẩn hóa concurrency cho nháp nếu cần, ghi contract và cập nhật client/test tương thích trước code.
10. Tổng quan hiện là thống kê cấu hình và checklist, chưa phải dashboard tình trạng đặt sân. Schedule chưa có lưới booking/hold/bảo trì theo ngày/toàn cơ sở.

Phân biệt lỗi chắc chắn từ đọc code, rủi ro cần tái hiện và đề xuất UX. Không gọi toàn backend hỏng hoặc không đồng bộ chỉ từ ảnh dropdown. Tên legacy như `1` được giữ trong DB; nhãn có thể trình bày rõ hơn nhưng không tự đánh lại số/đổi snapshot booking cũ.

## 3. Quyết định nghiệp vụ bắt buộc giữ

- Một Owner có thể quản lý business với nhiều cơ sở; mỗi cơ sở có nhiều sân. Sân đánh số trong từng cơ sở; sân khác cơ sở được trùng số. Booking giữ courtId cụ thể.
- Sân nháp trong cơ sở PUBLISHED được Owner bấm **Xác nhận** khi đủ lịch/giá/QR để thành ACTIVE, không Admin duyệt. Sân trong business/cơ sở chưa duyệt vẫn theo hồ sơ cha. Doanh nghiệp/cơ sở mới và thay đổi tài khoản/QR nhận tiền vẫn qua Admin.
- Chỉ ảnh cơ sở tùy chọn, không ảnh riêng sân. Giữ nhận diện ShuttleBook và thành phần chung ba cổng.
- Partner tự sửa lịch/giá cho đơn mới. Giá tính từng ca30phút, rule ưu tiên cao nhất theo ngày/giờ; booking đã tạo giữ snapshot giá/policy/QR.
- Vãng lai dùng ca liên tiếp, đủ minimum rồi tăng từng30phút. Cố định giữ contract F07: hàng tuần, mỗi buổi ít nhất2giờ, kỳ ít nhất1tháng và conflict rollback toàn kỳ. Không thêm chế độ linh hoạt nhiều ngày tùy ý bằng cách gọi nó là cố định.
- Mỗi Customer tối đa một báo giá đang giữ, vượt10triệuVND bị chặn từ lấy báo giá; trần áp tổng đơn/toàn kỳ, không phải ngân sách AWS.
- QR Owner cung cấp; Customer báo chuyển → Owner đối chiếu → payment PAID/booking CONFIRMED. Allocation đã khóa từ luồng quote/booking, không đợi Owner “duyệt đơn” mới khóa sân.
- CONFIRMED kết thúc thành công trong app. Không tự thêm đang chơi/check-in/check-out/hoàn thành/vắng mặt, khách tự hủy/đổi lịch, nhân viên F08, bán dịch vụ/thu chi/cổng thanh toán.
- Đã báo chuyển hoặc NEEDS_REVIEW không tự mất chỗ vì Owner chậm. Database/PostGIS và allocation exclusion là nguồn chuẩn, không thay bằng cache/mock.
- Toast kết quả thao tác ở đầu viewport3giây; lỗi tải/stale giữ recovery. Lệnh đối chiếu tiền giữ If-Match + Idempotency-Key; không tự gửi lại khi stale.

## 4. Phương án giao diện khuyến nghị

Giữ menu **Lịch & giá**; tổ chức thành ba tab thật, không thêm một menu quản lý lịch khác trùng chức năng:

| Tab | Mục đích | Nội dung chính |
|---|---|---|
| Lịch sân | Bao quát theo ngày tại một cơ sở | Lưới sân × giờ, booking/hold/bảo trì, nhãn Vãng lai/Cố định, mở đơn hiện có |
| Giờ & giá | Thiết lập một sân cụ thể | Lịch tuần + giá cơ bản; giá theo khoảng ngày; xem thử giá; quy định đặt ở phần mở rộng |
| Bảo trì | Khóa khoảng sân thực sự không nhận khách | Danh sách và form tạo/hủy, kiểm conflict với đơn/hold/bảo trì |

ACTIVE mặc định vào Lịch sân sau khi tab này có dữ liệu thật. Business nháp mặc định vào Giờ & giá; không hiển thị lưới đặt được cho sân chưa công bố. Giữ route `#/schedule` và liên kết cũ; state tab/cơ sở/sân/ngày phải hỗ trợ Back/F5 và kiểm lại scope từ server.

### 4.1. Chọn đúng cơ sở và sân

- Dùng business switcher sẵn có ở header, không tạo dropdown business thứ hai trong nội dung.
- Một selector Cơ sở; danh sách Sân phụ thuộc cơ sở. Lịch sân có lựa chọn Tất cả sân; editor bắt buộc chọn một sân.
- Nhãn cơ sở/sân/trạng thái rõ, sân xếp1,2,10 theo số. Sân nháp có badge và hướng dẫn cấu hình→Xác nhận; không nằm trong nhóm khách đặt được.
- Một bộ editor dùng state theo businessId/venueId/courtId; adapter chọn API nháp hoặc ACTIVE đúng trạng thái. Không gửi một payload của sân cũ sang sân mới.
- Nếu nguồn server làm selection mất hiệu lực, chọn lại hợp lệ hoặc báo rõ; không để combobox trống nhưng vẫn thao tác bằng ID cũ. Không tự đổi selection khi người dùng đang sửa mà chưa xử lý unsaved changes.
- Sau xác nhận sân, tải lại danh sách/config/lịch của đúng scope. Có CTA Xác nhận sân tại phần cấu hình nháp trong venue PUBLISHED; dùng cùng API hiện có và phản hồi trạng thái với Cơ sở & sân.

### 4.2. Lịch sân

- Ngày mặc định theo cơ sở; Hôm nay, ngày trước/sau, chọn ngày và Tải lại. Bản đầu ưu tiên lịch ngày, chưa xây lịch tháng phức tạp.
- Hàng là sân, cột là giờ; đọc theo ca30phút, có thể nhóm cột để dễ nhìn nhưng không đổi đơn vị allocation/giá.
- Phân biệt Trống, Đang giữ báo giá, Có đơn, Bảo trì, Ngoài giờ mở/Không nhận đặt. Trạng thái thời gian Đã qua tách riêng, không che mất đơn đã xác nhận hoặc bảo trì trong quá khứ.
- Booking badge Vãng lai/Cố định và nhãn nghiệp vụ: Chờ chuyển khoản, Chờ đối chiếu, Cần bổ sung, Đã xác nhận. Payment status lấy từ backend, không suy từ màu hoặc chỉ thấy booking CONFIRMED.
- Cố định hiển thị từng occurrence đúng ngày/giờ, click mở payment anchor/group hiện có; không đặt buổi đầu vào mọi ngày hoặc nhân tổng tiền toàn kỳ cho từng buổi.
- Click booking mở chi tiết đơn hiện có; việc đối chiếu tiền tiếp tục ở Đơn đặt sân. Không có nút “Duyệt đơn” tạo state machine thứ hai. Quote hold không có booking thì không hiện nút xử lý tiền.
- Ghi thời điểm cập nhật, polling nhẹ khoảng15giây khi đang chọn tab Lịch sân và document visible, refresh khi focus và sau mutation liên quan. Không gọi là realtime tuyệt đối; không tăng request theo từng ô sân/ca và không refresh ghi đè form đang sửa.
- Khi refresh lỗi giữ dữ liệu cũ kèm cảnh báo “Chưa cập nhật”, không hiện toàn bộ là Trống. 403/404 do mất quyền, 401 sau khi cơ chế refresh session thất bại, logout hoặc đổi phiên phải bỏ dữ liệu private và khóa thao tác. Không tạo toast lỗi lặp cho 401 tạm thời đã được cơ chế refresh xử lý thành công.
- Desktop có header/cột sân dễ theo dõi; mobile cuộn ngang trong vùng lưới hoặc dùng danh sách theo sân, không làm cả trang tràn ngang. Trạng thái có text/legend, không chỉ màu; hỗ trợ bàn phím và focus khi mở/đóng chi tiết.

### 4.3. Giờ & giá

- Bảy ngày cố định trong tuần, bật/tắt ngày mở; cấu hình giờ và giá cùng một nơi, không lặp dropdown “Ngày” cho mọi khung nếu đã chia theo ngày.
- Không tự đổi khả năng mô hình lịch hiện có: nếu DB chỉ một khoảng mở/ngày, không hứa giờ nghỉ nhiều đoạn chưa được hỗ trợ. Giá phải phủ toàn bộ giờ mở, không có đoạn hở/chồng.
- Giá hiển thị rõ VND/30phút và quy đổi VND/giờ để tham khảo, không âm thầm đổi đơn vị payload. Không dùng float hoặc làm tròn sai tiền.
- Giá ngày đặc biệt đặt trong mục riêng, có khoảng hiệu lực/thứ/giờ/ưu tiên. Giải thích ngắn quy tắc nào thắng; ghi lỗi sát row và giữ dữ liệu nhập khi save thất bại.
- Quy định đặt sân nằm trong phần mở rộng: bước hiển thị30/60/90, thời lượng tối thiểu và thời gian giữ trước báo chuyển. Bước hiển thị không có nghĩa mọi booking phải dài bội số block.
- Xem thử giá cùng khu vực, cho biết tổng và breakdown từng ca/rule; không tạo quote/booking/hold, không chiếm quota báo giá Customer. Giá tính được không bảo đảm khoảng đó còn trống; tình trạng xem ở Lịch sân.
- GET cấu hình/version nhất quán. Save lịch tuần+giá cơ bản nguyên tử theo API hiện có; save override/policy có version mới đúng. Không tự save tất cả form hoặc gửi nhiều PUT cùng một version cũ.
- Có unsaved changes và Hủy chỉnh sửa/Tải lại có chủ đích; trước đổi sân/tab/business cảnh báo nếu sẽ mất dữ liệu. Tách trạng thái editor khỏi lịch refresh. Chỉ refresh cấu hình vào form khi an toàn.
- Bản đầu không mở bulk edit nhiều sân. Nếu đề xuất sao chép lịch/giá hàng loạt, trình bày preview, quyền/version, cách atomic/partial recovery và hỏi trước khi làm.

### 4.4. Bảo trì và Tổng quan

- Bảo trì có ngày/giờ/sân/lý do rõ, tạo/hủy qua API hiện hành; conflict409 không được giải quyết bằng xóa booking/hold. Sau thành công lưới Partner và Customer cùng phản ánh allocation.
- Giữ lịch sử, cancellation/idempotency và constraint chống trùng. Backend kiểm thời gian quá khứ, UTC và scope.
- Tổng quan dẫn tới Lịch sân, nhãn thống kê cấu hình không bị gọi là tình trạng sân thực tế. Nếu thêm số liệu vận hành, định nghĩa theo ngày/cơ sở và dữ liệu thật; không tự gọi tổng tiền booking là doanh thu đã thu.

## 5. Contract backend cần chốt trước code

Ưu tiên tái dùng write APIs F02/F03/F06/F07. Thống nhất editor không đồng nghĩa mở endpoint ACTIVE cho sân nháp hoặc bypass approval.

Lưới Partner cần read model riêng. Public availability hiện gộp BOOKING/QUOTE_HOLD/MAINTENANCE thành RESERVED, ưu tiên PAST và chỉ tạo ô theo giờ mở hiện tại; không đủ để làm lịch Owner. Owner list là danh sách payment anchor/nhóm thanh toán, có pagination và summary chưa trả payment.status; không dùng trang đầu max100 để kết luận các ô còn lại Trống.

Đề xuất route đọc, phải ghi request/response/error trong đặc tả trước triển khai:

`GET /api/v1/operator/venues/{venueId}/calendar?date=YYYY-MM-DD&courtId=<optional>`

- Owner/business ACTIVE đúng membership, kiểm venue/court scope từ DB. Cần vẫn xem và xử lý đơn cũ khi sân/cơ sở ngừng nhận đơn mới; không copy guard chỉ-venue-PUBLISHED cho mọi read nếu nó che đơn cũ.
- Snapshot nhất quán, một server now/generatedAt cho toàn response, no-store. Ngày/query strict và giới hạn truy vấn/payload rõ; dùng cancellation, query có index và không N+1.
- Lấy events theo giao toàn ngày địa phương, không cắt ở giờ mở mới. Booking đã chốt ngoài giờ mở vừa sửa vẫn phải hiện, kèm thông tin giờ mở hiện tại.
- Trả metadata cơ sở/sân, open ranges và events BOOKING/QUOTE_HOLD/MAINTENANCE; tách occupancy khỏi isPast/canRequestNewBooking. Đừng tự coi đoạn không có giá là đặt được.
- Booking event có occurrenceId, paymentAnchorId, bookingNo/seriesNo, bookingType, ngày/giờ thực tế, bookingStatus/paymentStatus; tiền occurrence và tổng kỳ đặt tên riêng/exact-string nếu cần, không chia tổng kỳ tùy ý.
- Chỉ trả dữ liệu cần cho lưới. Không lộ contact/account/QR/private proof/object key; click đơn dùng detail có scope hiện có.
- Dùng predicate Active của QuoteReservations để không hiển thị hold đã hết hạn dù Worker dừng. Booking đã quá deadline nhưng allocation còn RESERVED không tự biến Trống; booking đã báo chuyển/NEEDS_REVIEW vẫn giữ chỗ.
- Occupancy chặn sân theo allocation RESERVED và predicate hold còn hiệu lực. Lịch sử booking EXPIRED/PAYMENT_REJECTED hoặc bảo trì CANCELLED có allocation RELEASED chỉ là sự kiện không chặn sân; không suy rằng đã giải phóng chỉ từ status khi allocation vẫn RESERVED. Nếu phát hiện status/allocation không nhất quán, báo và kiểm tra lại dữ liệu thay vì hiển thị trống giả.
- GET không giải phóng allocation, không đổi payment/booking, không phát outbox. Nếu giới hạn kết quả, phải có continuation/lỗi rõ, không Take100 âm thầm tạo “trống giả”.
- Có thể không cần migration nếu tái dùng bảng hiện có. Chỉ thêm index/schema khi có bằng chứng cần, không tự dựng hệ thống mới.

Sửa sự lệch price-preview và snapshot GET operations trong cùng lát cắt phù hợp; dùng chung chính sách tính giá/validation khi có ích, tránh refactor toàn engine. Với draft concurrency, chốt If-Match/version response và cập nhật caller/test trước khi enforce, không phá onboarding cũ lặng lẽ.

## 6. Ranh giới tham khảo ALOBO

| Luồng tham khảo | Quyết định cho ShuttleBook |
|---|---|
| Lưới sân trống/đã đặt, nhãn khách lẻ/cố định | Áp dụng qua read model và dữ liệu thật hiện có |
| Chủ sân duyệt đơn rồi mới khóa ca | Giữ luồng quote/booking đã khóa ca; Owner chỉ đối chiếu tiền theo F06 |
| Chủ sân tự tạo lịch ngày/linh hoạt/tháng cho khách | Chưa có nghiệp vụ/API Owner tạo hộ; chỉ đề xuất riêng và hỏi trước |
| Khách đang chơi, thanh toán lúc ra về | Không thêm vào mốc này; trái kết thúc CONFIRMED đang chốt |
| Thêm dịch vụ, thu chi, cổng tự động | Đề xuất giai đoạn sau, cần đặc tả/ý kiến người dùng trước triển khai |
| Điều phối nhân viên | F08 đã hủy, không mở lại |

## 7. Thứ tự thực hiện khi chạy prompt

1. Khảo sát, tái hiện và ghi `docs/features/Partner-schedule-workspace.md`: hiện trạng, phương án đã chọn, state/quyền, API/error/data, acceptance/testcase. Chỉ hỏi các vấn đề thật sự chưa chốt; giữ phạm vi trên cho phần chắc chắn.
2. Lát cắt S01: sửa đồng bộ selection/draft hydration/version/read consistency và preview120/150/180; regression cần thiết trước khi xây thêm UI.
3. Lát cắt S02: editor hợp nhất và tab Giờ & giá/Bảo trì, dirty state và toast, giữ onboarding/xác nhận sân đúng status. Có thể bàn giao mốc này riêng nhưng không gọi calendar đã xong.
4. Lát cắt S03: read API calendar với PostGIS thật, sau đó tab Lịch sân và deep link tới đơn, polling/invalidation/scope. Không dùng fixture thay bằng chứng backend.
5. Lát cắt S04: tích hợp ba cổng, review độc lập, sửa findings, chạy lại phần ảnh hưởng, cập nhật progress/báo cáo và hướng dẫn tự nghiệm thu.

Có thể chia agent theo contract thống nhất: backend calendar/concurrency, frontend workspace và reviewer/QA read-only. Mỗi agent sở hữu file rõ ràng; root tích hợp, không để nhiều agent sửa cùng PartnerOnboarding hoặc tests.

## 8. Acceptance và testcase tối thiểu

- PS01: một selector scope và editor; draft/ACTIVE/pending đúng quyền; chỉ sân trong venue PUBLISHED được Owner xác nhận. Không còn chữ sân mới gửi Admin ở schedule.
- PS02: đổi A→B→A, đổi business, Back/F5, request trả chậm, thêm/confirm sân, mất quyền; dữ liệu/version của đúng sân, không sót private data.
- PS03: hydrate draft đúng hours/prices; dirty guard; API fail giữ form; stale412 khóa lưu và reload có chủ đích; đọc cấu hình version/hours/prices nhất quán trong race.
- PS04: minimum120/block60 preview120/150/180 đều theo F05; các trường hợp sai lưới/minimum vẫn bị chặn. Preview tổng từng30phút/rule khớp Customer quote cùng điều kiện giá; preview không tạo hold.
- PS05: cover giờ mở/giá hở/chồng, ngoại lệ priority conflict, If-Match cạnh tranh; save không làm thay giá/QR/policy snapshot booking cũ.
- PS06: lưới phân biệt booking/hold/maintenance, Vãng lai/Cố định và payment thực; occurrence ngày khác anchor hiện đúng, đơn đã xác nhận ngoài giờ mở mới không biến mất, xem ngày quá khứ đúng dữ liệu.
- PS07: hold expiry Worker dừng được phản ánh; booking đang chờ chuyển nhưng chưa release và đã báo chuyển/NEEDS_REVIEW không hiện trống giả; lịch sử có allocation RELEASED không chặn sân; cùng now giữa các events.
- PS08: scope Owner khác/Customer/pending/suspended, court không thuộc venue; payload không lộ PII/tài khoản/private media. Logout/đổi phiên/401 cuối cùng xóa dữ liệu private. Không đủ trang dữ liệu không được coi là đầy đủ lịch.
- PS09: bảo trì conflict booking/quote/maintenance, race, cancel lặp; allocation/exclusion thật, Customer thấy thay đổi sau refresh.
- PS10: confirm/review/reject tiền đúng anchor/toàn kỳ, If-Match/Idempotency giữ nguyên; lịch cập nhật trạng thái sau command, không thêm transition mới.
- PS11: mặc định ngày quanh00:00 Việt Nam; date/time hiển thị24giờ dễ hiểu khi cần, internaltimezone đúng và không hiện IANA UI.
- PS12: desktop/mobile/tablet, bàn phím/zoom/overflow, toast3giây khi cuộn; refresh15giây không mất dữ liệu form, không toast spam, offline/deny không vẽ lưới trống giả.

Kiểm thử theo rủi ro: typecheck/build, API, các class DB bị ảnh hưởng bằng PostgreSQL/PostGIS thật; browser fixtures cho UI/late responses và live browser→API→Worker→PostGIS/Mailpit bằng DB thử riêng. Regression F02 xác nhận sân, F03/F04, F05/F06/F07; chọn suite cần thiết, không chạy rộng chỉ để tăng số PASS. Nếu migration mới có, cần fresh/upgrade/repeat/rollback bảo toàn dữ liệu.

Lệnh chuẩn tại repo root PowerShell: `npm.cmd run typecheck`, `npm.cmd run build`, `scripts/dotnet.ps1`, `scripts/Test-Database.ps1`, `scripts/Test-Web.ps1`, `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release`. Chọn đúng filter/spec sau khi biết diff; không chạy live và build/preview khác đồng thời trên cùng cổng. Không in secrets vào log; giữ DB thử tách biệt.

## 9. Bàn giao và giới hạn

- Bảng trước/sau: vấn đề thực tế → thay đổi → file → bằng chứng/giới hạn. Ghi đúng PASS/FAIL/NOT RUN/BLOCKED và lệnh, không lấy PASS mốc trước thay code mới.
- Viết đặc tả thao tác Partner sau cùng và cập nhật nhiệm vụ ba role tại Partner-workspace-and-role-contract. Customer tiếp tục booking; Partner cấu hình/xem lịch/đối chiếu; Admin duyệt doanh nghiệp/cơ sở/QR theo scope.
- Cập nhật `docs/progress.md` sau mỗi mốc, ghi mâu thuẫn đã xử lý và việc còn mở. Có checklist tự thử dễ hiểu: thêm sân→cấu hình→confirm→Customer thấy; đặt/chuyển tiền→Partner đối chiếu→lịch đổi; sửa giá→đơn cũ giữ nguyên; bảo trì→ca bị khóa.
- Không tự commit/push/merge/deploy, tạo tài nguyên AWS trả phí hoặc đổi dữ liệu thật. Cho phép chuẩn bị code/docs và test DB tạm; nếu cần chạy CI đúng code mới, kiểm tra authorization commit/push hiện hành trước bước đó.
- Chưa chạy S3/EC2/SMTP production không ghi production-ready. F09 và AWS demo CV là gate riêng. Không tuyên bố hoàn thiện toàn hệ thống chỉ vì trang schedule đẹp hơn.

Bắt đầu bằng báo cáo khảo sát ngắn, phương án và contract; sau đó thực thi phần đã đủ điều kiện, không chỉ đưa khuyến nghị rồi dừng.
