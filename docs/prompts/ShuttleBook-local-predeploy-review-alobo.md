# Prompt — review toàn diện ShuttleBook trước deploy, tham chiếu ALOBO

Bạn là **reviewer web senior với tiêu chuẩn đánh giá của người có 10 năm kinh nghiệm** về sản phẩm, UX/UI, frontend, backend, dữ liệu, bảo mật, kiểm thử và vận hành. Hãy đánh giá ShuttleBook đang có trong workspace này bằng bằng chứng hiện hành, đề xuất cải tiến cụ thể và lập danh sách điều kiện cần đạt **ở local trước khi deploy**. Viết báo cáo bằng tiếng Việt, thẳng thắn, có thể giao việc và kiểm chứng được.

## 1. Đọc bối cảnh và xác lập phạm vi

- Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, `docs/README.md`, các tài liệu `docs/00-*.md` đến `docs/05-*.md`, đặc tả/testcase feature liên quan, `docs/features/F09-production-readiness.md`, các báo cáo review và kết quả test gần nhất. Kiểm `git status`, diff chưa commit, source và bằng chứng runtime trước khi kết luận. Quyết định mới nhất của người dùng ưu tiên tài liệu lịch sử.
- Ba cổng cần đánh giá: **Customer/Guest, Partner (chủ sân), Admin**. Kiểm cả API, Worker, PostgreSQL/PostGIS, migration, cấu hình và script local. Phân biệt rõ: thiết kế trên giấy, đã có code, đã chạy test local, đã nghiệm thu tay, và đã chứng minh trong môi trường ngoài local.
- **F08 đã CANCELLED**: bỏ mời/ủy quyền/phân quyền tài khoản nhân viên. Không đề xuất hoặc đưa F08 vào điều kiện deploy. Vẫn kiểm quyền Customer/Admin và membership, business/venue scope của owner ở backend. F09 chưa hoàn thành; S3 thật và báo cáo doanh thu là tùy chọn theo quyết định hiện tại.
- Giữ các bất biến nghiệp vụ: `business → venue → court`; ca 30 phút và snapshot giá/QR; nearby chỉ theo vị trí/bán kính; vãng lai liên tiếp; cố định hằng tuần, mỗi buổi ít nhất 2 giờ, kỳ ít nhất một tháng, conflict rollback toàn kỳ; quote giữ tạm đến TTL; PostgreSQL/PostGIS và exclusion constraint chống trùng; Customer báo chuyển, Owner đối chiếu/xác nhận để payment `PAID`, booking `CONFIRMED`; đơn đã báo chuyển không tự giải phóng do Owner chậm; transactional outbox retry/idempotency. `CONFIRMED` kết thúc luồng; Customer không tự hủy/đổi; không thêm check-in/check-out/no-show.

## 2. So sánh ALOBO có kiểm chứng

- Truy cập nguồn chính thức hiện hành như [alobo.vn](https://www.alobo.vn/) và [ALOBO Wiki](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/); ghi URL, ngày truy cập, ngày cập nhật nếu có. Chỉ so sánh cùng nhiệm vụ người dùng: tìm sân, xem lịch, đặt lẻ/cố định, thông tin giá/QR, theo dõi đơn, chủ sân theo dõi lịch và xác nhận, Admin xử lý hồ sơ, trải nghiệm mobile.
- **Bỏ qua feature phân quyền, mời và tài khoản nhân viên của ALOBO**. Cũng không lấy POS, social, chat, loyalty hay báo cáo làm lỗi bắt buộc của ShuttleBook khi chúng ngoài phạm vi MVP hiện tại. Nếu thấy giá trị, đưa vào phần cơ hội sau local readiness, nêu chi phí và điều kiện ưu tiên.
- Mỗi nhận định “ShuttleBook mạnh hơn/yếu hơn ALOBO” phải nêu tiêu chí, bằng chứng của **cả hai bên** và mức tin cậy. Khi chỉ thấy trang marketing/hướng dẫn/screenshot của ALOBO, nói đúng mức quan sát được; không suy ra backend, hiệu năng, bảo mật hay hành vi của màn hình phải đăng nhập. Không sao chép logo, hình ảnh hay UI từng pixel.

## 3. Audit đa chiều

Lập inventory các route/màn hình/trạng thái chính của cả ba cổng rồi đánh giá từng nhiệm vụ và đường đi, gồm happy path, loading, empty, validation, lỗi mạng, hết hạn, quyền không đủ, F5/Back/Forward. Với mỗi chiều, nêu **điểm mạnh**, **điểm yếu/rủi ro**, tác động, bằng chứng `file:line` hoặc runtime, và cải tiến:

1. **Sản phẩm và nghiệp vụ:** số bước hoàn thành tác vụ; tính rõ ràng của giá, ca, quote TTL, QR và trạng thái chờ; luồng Guest → Customer, casual/fixed, Customer → Owner → Admin; phù hợp các bất biến trên.
2. **Đồng nhất UI của mọi trang:** tạo bảng đối chiếu Customer/Partner/Admin về màu và semantic tokens, typography, spacing/grid, chiều rộng container, header/sidebar/navigation, card/table/form, button hierarchy, icon, ảnh, border/shadow, thông báo, modal, trạng thái booking/payment, giọng văn và cách đặt nhãn. Chỉ ra trang lệch phong cách, thành phần trùng nhưng thể hiện khác, và quy tắc chung đề xuất cho `packages/ui` hoặc token dùng chung. Cho phép khác biệt theo tác vụ mỗi role nhưng vẫn nhận ra cùng một sản phẩm.
3. **Responsive và accessibility:** kiểm viewport 375/768/1024/1440, zoom 200%, keyboard/focus, label, trạng thái không chỉ dựa màu, contrast, target chạm, overflow và lịch ca 30 phút. Không báo PASS cho thứ chưa đo.
4. **Frontend quality:** route/deep link, session, form/error handling, race/polling, API contract, performance khi danh sách dài, asset/bundle, trạng thái lặp, khả năng bảo trì component/CSS.
5. **Backend, dữ liệu và tiền:** validation, authorization/scope trên server, giá chính xác, snapshot, idempotency, quote/booking allocation và concurrency trên PostgreSQL/PostGIS thật, cố định all-or-none, migration fresh/repeat/upgrade, Worker/outbox/retry, quyền xem ảnh QR/proof.
6. **Security, privacy, operations:** auth/refresh, secret hygiene, CORS/cookie/HTTPS config, rate limit, upload riêng tư, logging không rò dữ liệu, health/alert, backup và restore drill, runbook/release rollback, CI chạy thật, giới hạn tải và lỗi phụ thuộc.
7. **Kiểm thử và tài liệu:** acceptance coverage, UI fixture so với end-to-end thật, browser/API/DB test, manual evidence, các testcase `NOT RUN`/`BLOCKED`, hướng dẫn chạy local PowerShell/WSL và sự khớp giữa spec, progress và source.

## 4. Bằng chứng phải thu thập

- Ưu tiên `rg`/`rg --files`; kiểm lại số dòng trước khi dẫn. Duyệt UI thật và chụp screenshot desktop/mobile với dữ liệu thử nếu môi trường cho phép. Dùng tài khoản/dữ liệu thử riêng; không làm thay đổi dữ liệu thật. Không coi screenshot hoặc source là bằng chứng transaction/runtime.
- Chạy các gate phù hợp từ **repo root trong PowerShell**: `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run test:api`, `npm.cmd run test:db`, `npm.cmd run test:web`, và backend build qua `scripts/dotnet.ps1`. Kiểm `docker compose ps` khi test DB. Có thể dùng tập test hẹp trước rồi suite liên quan; ghi lệnh, ngày, môi trường, số pass/fail/skip và log. Phân biệt test fixture UI với API/DB thật. Không ghi lại kết quả cũ như test mới.
- Thực hiện focused tests cho rủi ro cao: hai khách tranh cùng court/slot, quote TTL và lost-response replay, casual exact money, full-series rollback, owner scope, payment report/confirm/retry, outbox Worker restart, migration, backup/restore vào môi trường thử. Nếu chưa chạy được, ghi `NOT RUN` hoặc `BLOCKED` cùng lý do và điều kiện mở khóa.
- Không in secret, OTP, token, connection string, proof thật hoặc PII vào báo cáo/log. Không sửa cấu hình máy toàn cục, tạo tài nguyên trả phí, commit/push/merge hoặc deploy production.

## 5. Cách ưu tiên và chuẩn bị local

- Phân loại từng finding thành **lỗi đã tái hiện**, **rủi ro từ source**, **điểm yếu UX**, hoặc **đề xuất sản phẩm**; gắn P0/P1/P2 và mức tin cậy. Mỗi finding cần: ID, vai trò/màn hình, bước tái hiện, expected/actual, bằng chứng, ảnh hưởng, root cause nếu xác định, phương án sửa nhỏ nhất, acceptance và test cần chạy lại. Không biến mọi điểm khác ALOBO thành bug.
- Đưa **P0 local trước deploy** cho lỗi tiền/dữ liệu/quyền/đặt trùng/luồng chính, lỗi UI khiến không hoàn thành tác vụ, test/gate quan trọng thất bại, và các phần F09 thiết yếu có thể chuẩn bị local (CI script tương thích, backup/restore thử, health/alert/runbook, cấu hình release). P1 là nhất quán UI và UX có ảnh hưởng nhưng không chặn an toàn; P2 là nâng cấp sau pilot. Nếu phát hiện lỗi nghiêm trọng, nêu rõ gate `NO-GO`.
- Với UI, đề xuất **một mini design system cụ thể**: token màu/typography/spacing/radius, component dùng chung, quy tắc trạng thái, tên gọi và thứ tự migrate từng màn hình. Minh họa before/after bằng mô tả hoặc mockup nếu hữu ích, nhưng không thay thế việc kiểm từng trang. Nêu acceptance bằng đo đạc/kiểm tra được, ví dụ không overflow ở viewport đã chọn, cùng hierarchy của action tương đương, focus visible, trạng thái booking hiển thị nhất quán.
- Lập thứ tự sửa phụ thuộc và dự toán S/M/L với giả định. Nếu được yêu cầu triển khai tiếp, phải viết scope, acceptance, API/error contract, thiết kế dữ liệu và testcase cho từng lát cắt trước code; sửa từng lát, review và chạy lại test bị ảnh hưởng. Không tự coi audit là quyền deploy hoặc thay đổi dữ liệu thật.

## 6. Đầu ra bắt buộc

Tạo báo cáo `docs/reviews/ShuttleBook-local-predeploy-review.md` gồm:

1. Kết luận `GO/NO-GO cho bước chuẩn bị local` và riêng `production NOT VERIFIED` nếu chưa có bằng chứng; 5 rủi ro ưu tiên.
2. Phạm vi/môi trường, inventory trang và ma trận nhất quán UI ba cổng, các screenshot hoặc link evidence.
3. Bảng so sánh theo nhiệm vụ với ALOBO: ShuttleBook mạnh/yếu/tương đương/chưa rõ, hai nguồn, mức tin cậy; bỏ qua feature phân quyền ALOBO.
4. Findings đa chiều theo mẫu ở trên, ghi điểm mạnh có bằng chứng và giới hạn quan sát.
5. Backlog P0/P1/P2, mini design system, thứ tự xử lý, acceptance, test/gate local và checklist trước deploy.
6. Bảng lệnh và kết quả `PASS/FAIL/NOT RUN/BLOCKED`, phân biệt test mới và lịch sử; phần còn mở cần người dùng quyết định.

Cập nhật `docs/progress.md` sau mốc review, không sửa trạng thái feature thành DONE chỉ từ code hoặc báo cáo. Trả lời ngắn gọn bằng tiếng Việt: kết luận, ba việc cần làm đầu tiên, những gate đã chạy/chưa chạy và liên kết báo cáo. Nếu thiếu điều kiện để kiểm một phần, vẫn hoàn thành phần độc lập và ghi giới hạn rõ.
