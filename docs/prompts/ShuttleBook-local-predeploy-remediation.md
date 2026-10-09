# Prompt — hoàn thiện ShuttleBook ở local trước deploy

Bạn là kỹ sư full-stack kiêm reviewer/QA senior. **Thực hiện công việc**, không dừng ở danh sách đề xuất: sửa các rủi ro đã có bằng chứng, chuẩn hóa UI, chạy kiểm thử và chuẩn bị đầy đủ gate local để ShuttleBook có thể được xem xét deploy. Làm theo từng lát cắt nhỏ, có contract và testcase trước code. Báo cáo trung thực `PASS/FAIL/NOT RUN/BLOCKED` bằng tiếng Việt.

## 1. Bối cảnh và quyền đã được người dùng xác nhận

- Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, tài liệu nghiệp vụ `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`, đặc tả/testcase liên quan, [F09](../features/F09-production-readiness.md), [báo cáo review trước deploy](../reviews/ShuttleBook-local-predeploy-review.md) và Git diff/status **hiện tại**. Quyết định mới của người dùng ưu tiên tài liệu cũ. Không ghi đè thay đổi chưa commit của người dùng.
- **Đã chốt:** trần tiền **10.000.000 VND**; mỗi tài khoản Customer chỉ được có **tối đa một báo giá còn hiệu lực đang giữ chỗ** trên toàn hệ thống, áp dụng cả quote vãng lai và cố định. Tính bằng tài khoản, không theo IP/court. PostgreSQL/PostGIS là nguồn chuẩn; chống race bằng transaction/constraint phù hợp, không dựa vào state frontend hoặc cache.
- **Đã cho phép chạy workflow trên repository** để xác nhận CI thật sau khi sửa. Không hỏi lại quyền chạy workflow. Tuy nhiên, việc commit/push/merge vẫn theo `AGENTS.md`: chỉ thực hiện khi người dùng yêu cầu/cho phép rõ; nếu chạy workflow cần đưa code lên remote mà chưa được phép commit/push, chuẩn bị diff và hỏi đúng thao tác Git còn thiếu. Không yêu cầu quyền deploy production từ việc cho phép chạy CI.
- **F08 CANCELLED:** không làm tài khoản nhân viên, mời/ủy quyền/phân quyền nhân viên; vẫn giữ guard Customer/Admin, owner membership và business/venue scope backend. Không thêm check-in/check-out/no-show, Customer tự hủy/đổi, ngân hàng tự xác nhận hoặc trạng thái sau `CONFIRMED`.
- S3 live và báo cáo doanh thu còn hoãn/tùy chọn. Không deploy production, tạo tài nguyên trả phí, thay đổi dữ liệu thật, hoặc dùng tiền/ngân hàng thật trong nhiệm vụ này.

## 2. Hỏi ý kiến trước khi thực hiện phần phụ thuộc quyết định

Ngay sau khi kiểm tài liệu/source, gom các câu hỏi **thật sự chặn một lát cắt** thành một lần hỏi ngắn, đưa phương án cụ thể và tác động. Tiếp tục phần độc lập trong khi chờ; **không tự chọn mặc định nếu câu trả lời làm đổi nghiệp vụ/chi phí/rủi ro**. Tối thiểu xác nhận:

1. **Trần 10 triệu áp vào đâu?** Đề xuất: tổng số tiền phải chuyển cho **một booking vãng lai**, hoặc **toàn bộ kỳ cố định trên một payment anchor**. Không chia nhỏ kỳ để né trần. Hỏi nếu người dùng muốn hiểu khác. Hỏi thêm cách xử lý khi tổng vượt trần: từ chối quote với mã lỗi rõ, hay chặn từ bước chọn lịch (UI vẫn phải giải thích cùng lỗi backend). Có thể chuẩn bị contract/test trước khi có câu trả lời; chưa đổi policy runtime.
2. **Khi tài khoản đang giữ một quote và yêu cầu quote mới:** đề xuất hai lựa chọn: (A) trả lỗi, giữ quote cũ; (B) thay quote cũ bằng quote mới **nguyên tử**, chỉ giải phóng quote cũ nếu quote mới tạo thành công. Phải hỏi trước khi triển khai hành vi này. Cả hai cách đều không cho hai hold active đồng thời ở casual/fixed hoặc qua nhiều tab/API.
3. **Backup vận hành:** hỏi nơi giữ bản sao ngoài vùng chạy app, thời gian lưu, RPO/RTO mục tiêu và ai nhận cảnh báo. Trước câu trả lời có thể viết script/runbook và restore drill vào DB/media test riêng trong workspace, nhưng không tự ghi backup thật ra đường dẫn hoặc cloud chưa được duyệt.
4. **UI:** giữ palette/brand ShuttleBook hiện tại là baseline kỹ thuật. Trước khi áp dụng thay đổi thị giác lớn trên cả ba cổng, trình bày token/component mẫu hoặc screenshot để người dùng chọn/duyệt. Những lỗi rõ về overflow, focus, label, trạng thái sai có thể sửa ngay theo acceptance hiện có.
5. **Pilot/giao dịch thật/provider:** nếu cần dữ liệu, account, thiết bị hoặc dịch vụ bên ngoài, hỏi đúng điều kiện trước bước phụ thuộc. Người dùng tự đối chiếu tiền ngân hàng thật và nghiệm thu cảm nhận trên thiết bị thực; agent chuẩn bị checklist và hỗ trợ sửa lỗi được báo.

Không hỏi lại quyết định đã chốt ở mục 1. Nếu người dùng chưa trả lời câu hỏi của một lát cắt, ghi `BLOCKED — chờ policy` cho lát cắt đó, hoàn thành phần độc lập khác. Không dùng thời gian chờ làm sự đồng ý.

## 3. Thứ tự triển khai

### A. Contract và test trước code

Viết phạm vi, non-goals, acceptance, API/error contract, dữ liệu/migration/transaction và testcase cho từng lát cắt F09; cập nhật tài liệu mới, không viết lại toàn bộ schema. Đối chiếu trạng thái F00–F07/M03 mới nhất; không đổi DONE từ việc có code. Review spec, chỉ hỏi phần policy chặn.

### B. Trần tiền 10 triệu và tính VND chính xác

- Kiểm mọi đường tạo quote/booking casual/fixed, snapshot booking/payment, response và cách UI format. Dùng số nguyên/decimal chính xác ở server; trả `amountExact` và `pricePerSlotExact` dạng chuỗi khi cần cho browser. Không để JS `number` làm tròn trước khi Customer xem hoặc xác nhận. Giữ tương thích response cũ nếu cần.
- Thực thi trần ở **backend** trên tổng theo phạm vi người dùng chốt, trước khi giữ chỗ/tạo đơn theo contract; frontend báo lỗi dễ hiểu và không tạo hold/đơn sai. Quy định rõ biên `10.000.000` được phép, `10.000.001` bị từ chối; ca 30 phút và series nguyên kỳ giữ giá/snapshot như cũ.
- Test biên tiền trên API, PostgreSQL/PostGIS và browser, bao gồm nhiều ca cộng tổng, số gần giới hạn JS, quote replay, giá đổi, fixed total và không tạo allocation/payment khi bị từ chối. Không đổi tiền của booking cũ.

### C. Một quote hold active cho mỗi Customer

- Thiết kế guard database/transaction cho casual + fixed **toàn tài khoản**. Giữ bất biến quote TTL, replacement policy người dùng chọn, create nguyên tử, hết hạn giải phóng, idempotency replay và fixed all-or-none. Không tính booking đã tạo là quote active; booking/report payment theo policy cũ.
- Test PostGIS thật với hai court/hai venue, casual↔fixed, hai tab và hai request song song; có một kết quả hợp lệ theo policy, không tạo hai hold active. Test fail quote mới không làm mất quote cũ nếu chọn thay nguyên tử; test hết TTL/create/replay/Worker restart. Kiểm Customer A không giải phóng hold Customer B.

### D. CI và gate tự động

- Sửa workflow Ubuntu đang gọi npm scripts dùng `powershell.exe`; làm script cross-platform hoặc lệnh CI phù hợp, giữ trải nghiệm PowerShell local. Chạy typecheck, ba web build, backend build, API, browser và DB tests theo môi trường tương ứng. Tách lịch chạy để tránh build/test .NET khóa cùng output.
- Điều tra ca M03 mobile timing [trong báo cáo](../reviews/ShuttleBook-local-predeploy-review.md): full suite từng 245 PASS / 1 FAIL / 8 SKIP, focused 6/6 PASS. Dùng trace và thời điểm clock chính xác; sửa test nếu assert phụ thuộc thời gian login/clock, sửa sản phẩm nếu tái hiện policy sai. Không chỉ tăng timeout hoặc bỏ test.
- Full DB suite trước đó bị ngắt sau khoảng 9 phút, ca migration focused 1/1 PASS. Chạy lại với log tiến độ/timeout hợp lý và DB tạm; ghi số ca cuối, lỗi thật và thời lượng. Browser fixture không thay live browser→API→Worker→PostGIS/Mailpit.
- Sau khi code sẵn sàng, **chạy workflow trên repository theo quyền đã cấp** nếu có đường chạy không cần Git mutation ngoài quyền hiện có. Nếu cần commit/push trước khi trigger, chuẩn bị diff/test/report rồi hỏi riêng quyền commit/push; sau khi được phép, trigger và ghi link run/status. Không gọi YAML đọc bằng mắt hoặc local build là hosted CI PASS.

### E. Backup, restore, giám sát và release local

- Chuẩn bị script/hướng dẫn backup PostgreSQL/PostGIS + media Local và manifest/checksum; restore vào **DB và thư mục thử riêng**, không ghi đè development/production. Đối chiếu booking, payment, series, allocation, QR và proof private đúng quyền. Hỏi destination/retention/RPO/RTO trước khi cấu hình backup vận hành thật.
- Kiểm API/Worker/DB health; outbox retry/idempotency, cảnh báo retry và owner chờ quá SLA; log traceId không chứa secret/OTP/PII. Diễn tập Worker restart/lỗi mạng, kiểm booking đã báo chuyển không tự giải phóng.
- Viết runbook local: cấu hình môi trường, migration fresh/repeat/upgrade có dữ liệu, release/rollback ứng dụng, phục hồi DB/media, checklist bảo mật HTTPS/CORS/cookie/secrets. Chưa deploy môi trường ngoài local trong prompt này.

### F. Đồng nhất UI ba cổng

- Dựa trên ma trận UI trong báo cáo, tạo token semantic chung và các primitive thật sự dùng lại được (button, field, panel, status/notice), nhưng giữ bố cục khác nhau khi nhiệm vụ khác nhau: Customer account header ngang, Customer/Partner/Admin workspace. Không ép cả ba trang giống từng pixel.
- Migrate từng family, tránh refactor tràn lan; ưu tiên trang quote/payment Customer, đơn/giá Partner, duyệt Admin và các trạng thái loading/error/empty. Kiểm màu, typography, spacing, hierarchy của nút, focus, thông báo, mobile 375/768/1024/1440, zoom 200%, keyboard, không overflow. Lưu screenshot before/after bằng dữ liệu thử; đánh dấu NVDA/thiết bị thật `NOT RUN` nếu chưa kiểm.
- Trình mẫu để người dùng nghiệm thu các thay đổi thị giác lớn theo mục 2, rồi mới áp dụng rộng. Kiểm F5/Back/Forward, route, quyền và luồng nghiệp vụ không bị đổi.

## 4. Báo cáo và gate hoàn thành

Sau **mỗi lát cắt**, cập nhật `docs/progress.md`: phần đã làm, bằng chứng lệnh/log/ngày/môi trường, `PASS/FAIL/NOT RUN/BLOCKED`, phần còn mở và bước tiếp theo. Tạo tài liệu kết quả F09 rõ source changes, contract, migration, test DB/API/browser/live, review, ảnh UI và workflow URL. Không in secret/token/OTP/connection string/PII.

Chỉ kết luận **READY FOR DEPLOY REVIEW** khi: tiền/quota đúng policy và test race PostGIS PASS; full suites ổn định; hosted CI PASS; restore drill DB+media PASS; health/outbox/rollback có bằng chứng; UI ba cổng đã được kiểm và các thay đổi cần ý kiến đã được người dùng nghiệm thu. `READY FOR DEPLOY REVIEW` **không phải quyền deploy** và không đồng nghĩa production đã được kiểm chứng. Nếu còn gate thiếu, ghi NO-GO cùng bước mở khóa cụ thể.

Cuối cùng trả lời ngắn gọn bằng tiếng Việt: thay đổi chính, lệnh/kết quả mới, quyết định đang chờ người dùng, file báo cáo và bước tiếp theo. Không tự commit/push/merge/deploy/tạo tài nguyên trả phí khi chưa có quyền tương ứng.
