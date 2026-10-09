# Prompt — đánh giá ShuttleBook để tiến tới sản phẩm, tham chiếu ALOBO

> Đã thực thi ngày 2026-10-08: [báo cáo sản phẩm](../reviews/ShuttleBook-product-readiness.md) hoàn tất sau source/evidence review và đối chiếu nguồn ALOBO. Người dùng đã nghiệm thu F07; F07 chốt DONE local sau báo cáo. Khi chạy lại prompt cần kiểm source/evidence hiện hành.


Ngày tạo: **2026-10-08**. Prompt này thực hiện **sau khi hoàn tất và kiểm chứng thay đổi quote giữ tạm + Customer UI đồng bộ Partner/Admin**. Nếu mốc đó chưa đủ evidence, vẫn đọc và đánh giá phần độc lập nhưng ghi chính xác phần mới PENDING/NOT RUN, không giả định đã hoàn tất.

## Vai trò và mục tiêu

Bạn làm việc với góc nhìn **lập trình viên có 10 năm kinh nghiệm**, có trách nhiệm về kiến trúc, giao dịch dữ liệu, bảo mật, frontend, kiểm thử và vận hành sản phẩm. Đây là vai trò chuyên môn để đặt tiêu chuẩn đánh giá; không tuyên bố có trải nghiệm cá nhân dùng ALOBO hoặc thành tích nghề nghiệp không có bằng chứng.

Đánh giá ShuttleBook hiện tại bằng mã nguồn, tài liệu, bằng chứng runtime và trải nghiệm có thể quan sát. Sử dụng **ứng dụng ALOBO làm chuẩn tham chiếu nghiệp vụ và UX** từ nguồn chính thức, rồi viết nhận xét cụ thể và lộ trình từ bản local tới pilot và sản phẩm. Không chỉ liệt kê mọi tính năng ALOBO rồi yêu cầu sao chép.

Mục tiêu đầu ra:

1. Xác định ứng dụng hiện đang giải quyết tốt vấn đề nào cho Customer, Owner/Staff, Admin; vấn đề nào còn thiếu hoặc gây cản trở.
2. Phân biệt có thiết kế, có code, local đã test, người dùng đã nghiệm thu, và production đã kiểm chứng.
3. Có đánh giá rõ ràng từng chiều, chỉ ra bằng chứng và độ tin cậy, không khen chung hoặc chấm phần trăm tùy ý.
4. Lộ trình P0/P1/P2 có dependencies, acceptance, test và release/gate pilot; đề xuất feature tiếp theo bám roadmap F08/F09.
5. Nêu quyết định nên giữ, nên sửa, nên hoãn và lý do gắn với nhiệm vụ của người dùng thật.

## 1. Đọc dự án trước khi kết luận

Đọc AGENTS.md, docs/process.md, docs/progress.md, docs/README.md và toàn bộ docs 00–05. Đọc đặc tả/testcase F01–F07, mốc nghiệm thu mới nhất và prompt triển khai quote hold/Customer UI. Kiểm Git status/diff, không reset hoặc sửa mất thay đổi chưa commit.

Kiểm source theo tác vụ, tối thiểu:

- Identity/JWT/refresh/session/cookie/Admin idle và luồng đăng nhập trở lại intent.
- Onboarding/business→venue→court/approval/revision/ngân hàng/QR.
- Public list/nearby/PostGIS/map/ảnh/lịch 30 phút.
- Quote vãng lai/cố định, allocation/constraint/TTL/cleanup/convert/idempotency/price fingerprint/ownership/giới hạn tần suất.
- Booking/payment anchor/proof private/report/supplement/confirm/reject/expiry/SLA/outbox/Worker.
- Shell ba cổng, navigation, mobile/keyboard, loading/empty/error states, sự nhất quán của phong cách.
- Migration/nâng cấp dữ liệu, test trên DB thật, CI/cấu hình release, mã media S3, setup/scripts/observability/tài liệu backup.

Ưu tiên rg/rg --files. Đọc file liên quan trước khi nêu lỗi. Mỗi claim quan trọng dẫn **file:line hiện tại**; số dòng phải kiểm sau thay đổi root, không copy line cũ từ báo cáo này. Search không tìm thấy chỉ cho phép nói “chưa tìm thấy trong phạm vi đã kiểm”, chưa đủ chứng minh toàn bộ hệ thống không có tính năng.

Tóm tắt trạng thái F01–F07 theo mốc mới nhất. Nếu spec/progress/lời nghiệm thu khác nhau, đưa bảng mâu thuẫn và phạm vi đã duyệt; không tự đổi tất cả DONE. S3 live bị hoãn là NOT RUN cho provider, không làm mất bằng chứng local đã đạt và cũng không biến provider thành PASS.

## 2. Xác minh chuẩn tham chiếu ALOBO

Truy cập nguồn **chính thức**, ghi ngày truy cập/ngày bài cập nhật và URL hỗ trợ từng claim:

- https://www.alobo.vn/
- https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/
- https://wiki.alobo.vn/article/alobo-2103-cap-nhat-phien-ban-alobo-quan-ly-alobo-dat-lich/
- Chỉ mục hướng dẫn chính thức nếu cần thêm nhiệm vụ cụ thể.

Các mốc đã tham khảo khi tạo prompt: hướng dẫn online cập nhật 19/05/2026, changelog 2.10.3 cập nhật 21/09/2026. Khi thực thi lại cần kiểm nguồn hiện hành, không coi đây là latest vĩnh viễn.

Dùng bảng: nhiệm vụ → ALOBO công bố/hướng dẫn gì → source → ShuttleBook có gì/evidence → gap → cần làm/hoãn. Phân tích các nhóm lịch ngày/tháng, branch/phạm vi quyền nhân viên, QR/đối soát, chia sẻ link/cài đặt nhận online, report, hỗ trợ khách. Bán lẻ/social/chat/membership chỉ tham khảo khi phù hợp.

Không khẳng định ALOBO giữ quote bao lâu, realtime thế nào, chống đặt trùng bằng gì, mức phí/quota/thị phần/hiệu năng nếu nguồn không xác nhận. Không suy từ ảnh UI ra transaction/backend. Không copy thương hiệu/tài sản/UI từng pixel. Đọc docs ALOBO không thay nghiệm thu thực app ALOBO. Paraphrase ngắn, mỗi source web tổng dẫn xuất trong câu trả lời không quá limit của tool; không trích nguyên bài.

## 3. Các quy tắc ShuttleBook phải giữ

- Ba app React/TypeScript/Vite độc lập, ASP.NET Core modular monolith API + Worker, PostgreSQL/PostGIS authoritative.
- Business→venue→court; booking giữ court cụ thể; tiền VND chính xác; thời gian UTC lưu, múi giờ địa phương của venue tính giá/lịch.
- Slot 30 phút, vãng lai liên tiếp từ minimum và thêm từng 30 phút; không ép total là bội bookingBlockMinutes.
- Cố định cùng sân/thứ/giờ bắt đầu/thời lượng; mỗi buổi ≥ max(120, minimum sân), kỳ ≥ một tháng lịch, 60 ngày/max 12 buổi, all-or-none.
- Yêu cầu mới: **báo giá của A giữ tạm các ca trong TTL, B thấy không khả dụng; chưa xác nhận hết TTL phải mở lại**. Kiểm scope/ownership/replay/expiry/race của policy này bằng gate mới. Chọn ô trên browser chưa gọi API không được coi là giữ chỗ theo nguồn dữ liệu chuẩn. UI Guest không vô tình khóa sân chỉ bằng xem/list hoặc polling.
- Sau tạo booking hạn chuyển khoản theo holdMinutes, có snapshot riêng. Reported/NEEDS_REVIEW không tự hết hạn vì Owner chậm; bổ sung không reset SLA 30 phút; outbox transactional/retry/dedup.
- FULL_SERIES 100% một QR/một payment/một xác nhận; cùng nhóm mọi read/command không thay một buổi riêng.
- QR do Owner cung cấp, Customer báo đã chuyển, Owner đối chiếu đúng tổng rồi PAID/CONFIRMED. Ảnh tùy chọn không là bằng chứng tự động chắc chắn trả tiền; mã giao dịch ngân hàng không bắt buộc.
- CONFIRMED kết thúc thành công; Customer không hủy/đổi; không check-in/check-out/completed/no-show.
- Quyền frontend không thay guard backend; Admin không tự được owner xác nhận hoặc proof private. Không thêm ngân hàng tự xác nhận/trạng thái tài chính mới chưa chốt.

## 4. Cách đánh giá và kiểm chứng

### Luồng thực tế

Nếu môi trường cho phép, duyệt ba app với account test và DB test riêng. Theo task:

1. Guest tìm sân/nearby khi từ chối location → xem sân/lịch.
2. Customer quay lại bước trước sau đăng nhập → chọn vãng lai/fixed → quote hold/countdown → create → QR/report → bổ sung → confirm.
3. B cạnh tranh với A: B thấy kín khi A lấy quote; A bỏ dở hết TTL B đặt; race quote/create/cleanup/maintenance và toàn kỳ rollback.
4. Owner onboarding/config/hồ sơ → list có scope → total/evidence → decision/review/reject.
5. Admin approval/SLA/idle/F5; không thêm quyền thanh toán.
6. Mobile 375/768, desktop 1024/1440, keyboard Tab/Escape, zoom 200%; rõ nhãn/trạng thái/chú giải màu, bảng 30 phút không chồng text.

Dùng screenshots hợp lý, không chứa secrets/PII. Đo tỷ lệ hoàn thành tác vụ/thời gian tới bước báo giá nếu thực chạy; ghi cỡ mẫu, thiết bị và điều kiện. Nếu không chạy, ghi NOT RUN; không dùng source hoặc UI stub để khẳng định runtime DB.

### Tiêu chí kỹ thuật

- Correctness: quote hold và booking cuối dùng allocation chung/exclusion, tối đa một khách thắng trên nhiều API, tính giá từng ca, DST/horizon, idempotent retry.
- Authorization: quyền sở hữu của Customer, business active/venue/membership/perms, suspend/revoke trong request chờ khóa, xóa dữ liệu riêng và bỏ GET cũ.
- Financial: đúng tổng tiền, snapshot bất biến giá/QR, toàn kỳ transitions, report/review deadline, audit/ledger boundaries.
- Reliability: Worker restart/failure/retry/SKIP LOCKED, quote expiry Worker ngừng chạy, timeouts/backpressure/correlation, migration dữ liệu cũ.
- Security/media: validation/giới hạn tần suấts, cookie/CSRF/CORS/headers, upload private/type/size/checksum/TTL, S3 live và secret hygiene.
- Delivery: CI thực tế với runner và script tương thích, fresh/repeat/upgrade migration, backup/restore/release rollback và health/alert.
- UX/product: Customer có chung style Partner/Admin mà nhiệm vụ đặt không bị tăng bước; không chỉ số giả; trạng thái giữ tạm khác confirmed có giải thích; tư vấn hỗ trợ khi chuyển nhầm/quên báo.

Có thể dùng thang 0–4 (thiết kế → code → local đã test → đã nghiệm thu vận hành) kèm confidence. Không cộng thành product% hoặc lợi ích doanh thu không có dữ liệu. Mọi finding gồm severity, scenario, evidence, impact, fix, test, confidence. Phân biệt lỗi đã xác nhận với rủi ro suy ra từ source/hypothesis.

## 5. Lộ trình phải có

### P0 trước gate tương ứng

- Hoàn tất/review/retest/nghiệm thu tay quote hold mới và Customer UI; bảo toàn F05/F06/F07.
- F08: lời mời dùng một lần, business/permission theo venue, revoke, staff notification đúng scope và quyền thao tác.
- F09 chuẩn bị pilot: CI tương thích môi trường/run thực, S3 private live khi có phép, secrets/HTTPS/CORS, migration rehearsal, backup DB + media/restore, monitoring/alert/runbook và load test theo tải chốt.
- Reconcile trạng thái docs/acceptance và danh sách NOT RUN; không đổi DONE do có file code.

### P1 tăng hiệu quả vận hành

- Report đối soát thu tiền PAID, phân biệt ngày chơi/ngày nhận, series không cộng trùng, export có scope.
- Phiên Customer/Partner và quay lại bước trước sau đăng nhập thuận tiện theo policy đã duyệt; cookie an toàn/CSRF/rotation, không token trong localStorage tùy tiện.
- Link chia sẻ/cài đặt nhận online không thay đơn đã tạo; reminder ngoài app theo scope/provider được phép.
- Hỗ trợ/dispute **trước xác nhận** có case/audit/proof private, không tạo quyền tài chính mới tự động.

### P2 tăng trưởng sau pilot

QR động đã kiểm định; favorite/review hợp lệ; dịch vụ/POS; membership/promotion; chat/social; đối soát ngân hàng tự động; PWA/native. Chọn theo vấn đề người dùng đã quan sát và funnel, mỗi feature tài chính phải thiết kế policy/ledger/dependencies riêng. Nêu những tính năng ALOBO nên hoãn và chi phí độ phức tạp của chúng.

Với mỗi lát cắt: actor/problem/value, scope/non-goals, data/API/error/auth, dependencies, acceptance/test, đề xuất metric, effort S/M/L có giả định và gate. Không cam kết ngày release khi chưa estimate và nguồn lực chốt.

### Gate pilot và product

Đề xuất pilot 1–3 cơ sở có người chịu trách nhiệm và hỗ trợ; chốt dữ liệu test/giao dịch thật, tải dự kiến, RPO/RTO, consent/retention và công tắc ngừng nhận đơn. Trước giao dịch thật cần permission riêng và P0 đạt. Đo funnel view→select→quote→create→report→confirm, thời gian owner xác nhận, bỏ dở/lỗi quote, sự cố quyền/scope, đối soát và restore. Phân biệt cỡ mẫu/KPI với giả định.

Gate public: acceptance chính thức, security/load/recovery/release evidence, media durable/private, phạm vi quyền nhân viên, chính sách hỗ trợ/terms và trách nhiệm Owner. Không tự triển khai khi chỉ được yêu cầu đánh giá.

## 6. Đầu ra bắt buộc

Viết tiếng Việt, concrete, tránh câu khen chung. Tạo/cập nhật **docs/reviews/ShuttleBook-product-readiness.md** theo cấu trúc:

1. Kết luận maturity/go-no-go và mức tự tin.
2. Scope, nguồn, môi trường, lệnh/evidence + PASS/FAIL/NOT RUN/BLOCKED.
3. Bảng trạng thái F01–F07 và yêu cầu mới.
4. Đối chiếu ALOBO có URL gần claim.
5. Điểm mạnh và findings có file:line.
6. Điểm từng chiều có phương pháp và hạn chế.
7. P0/P1/P2 bản đồ feature/dependencies/acceptance/metrics/gates.
8. Bước tiếp theo đề xuất rõ; decisions giữ/sửa/hoãn; câu hỏi business thật sự cần chốt.

Final tóm tắt mức hiện tại,3–5 gaps ưu tiên và thứ tự làm, link report và prompt. Không làm thêm schema/feature/deploy chỉ vì phát hiện roadmap; sửa bug production trong yêu cầu root thuộc workflow riêng.

## 7. Giới hạn thực thi

Không secrets/log token/PII; không tự commit/push/merge, migrate DB thật, deploy production hoặc tạo tài nguyên trả phí. Không ghi PASS nếu chưa có runtime. Bằng chứng lịch sử giữ ngày/môi trường, không rebrand thành lần test mới. Đọc progress ở cuối phiên vì có thể root đã hoàn tất hold/UI và gate sau lúc bắt đầu đánh giá; cập nhật status bằng bằng chứng đó.
