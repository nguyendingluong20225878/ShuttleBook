# Prompt hoàn thành F01 theo thứ tự phụ thuộc

Copy toàn bộ prompt dưới đây vào phiên làm việc tiếp theo để tiếp tục hoàn tất F01.

---

Bạn là agent tích hợp chịu trách nhiệm hoàn thành phần còn lại của ShuttleBook F01 trong repository hiện tại.

## Bắt buộc trước khi làm

1. Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`, các đặc tả F01.1/F01.2/F01.3, toàn bộ testcase F01.1/F01.2/F01.3 và hướng dẫn local setup/testing.
2. Kiểm tra Git, mã nguồn và bằng chứng runtime hiện có. Workspace có thể chưa có commit nền/remote; không ghi đè hoặc xóa thay đổi chưa rõ chủ sở hữu. Không commit/push trừ khi người dùng yêu cầu trong phiên đó.
3. Giữ đúng trạng thái hiện tại: F01.2 và F01.3 vẫn `IN_PROGRESS`. Tiếp tục từ khoảng trống được ghi trong feature/testcase/progress; không đánh dấu DONE chỉ vì build hoặc suite tổng hợp pass.
4. Giải thích bằng tiếng Việt. Người dùng muốn kiểm thử qua UI FE hoặc API client như Postman; ưu tiên browser/UI và Postman cho luồng thủ công. Có thể dùng lệnh terminal để khởi động local/build, nhưng không thay kiểm chứng UI/API bằng lệnh PowerShell.

## Thứ tự bắt buộc

Làm tuần tự theo phụ thuộc, hoàn thành review/QA của từng mốc trước khi chuyển mốc kế tiếp:

### Mốc 1 — F01.1 Customer registration

- Đối chiếu đặc tả hiện có với code và các kết quả đã ghi; không làm lại customer UI happy path đã có bằng chứng.
- Bổ sung/chạy testcase còn thiếu cho API validation, contact normalization/unique, duplicate pending/active, OTP expired/reuse/attempt limits, concurrent verification, rate limit và audit/log redaction.
- Các assertion database/transaction/concurrency/migration phải chạy trên PostgreSQL/PostGIS thật.
- Kiểm tra browser customer thực với API, PostgreSQL và Mailpit; gồm register, mismatch password, OTP, resend, invalid input và reload `/verify`.
- Ghi từng kết quả testcase kèm PASS/FAIL/NOT RUN/BLOCKED và bằng chứng thực tế.

### Mốc 2 — F01.2 Login/session

- Tiếp tục theo `docs/testing/F01.2-test-cases.md`; ưu tiên các khoảng trống F012-T04–T16 và F012-T17–T19, đối chiếu từng điều kiện cụ thể trong bảng.
- Bắt buộc kiểm tra refresh race, reuse/revoke toàn family, family isolation, logout sai/lặp, suspend sau login, JWT tamper/expiry/issuer/audience, rate limit/audit/redaction và migration fresh/repeatable trên PostgreSQL thật.
- Kiểm tra customer/partner login, refresh/logout/reload trên browser với API thật; UI/API E2E thật phải được phân biệt với suite dùng route mock.
- Không đánh dấu DONE nếu còn acceptance chưa có bằng chứng hoặc F012-T13 vẫn chưa thể xác minh khi endpoint phụ thuộc chưa tồn tại.

### Mốc 3 — F01.3 Partner registration

- Giữ API login chung `/api/v1/auth/login`; partner registration/verify/resend dùng endpoint partner riêng. Server tiếp tục tự gán account type/status.
- Hoàn tất duplicate/cross-role, resend và challenge cũ, expiry/attempt, concurrency, rate limit, audit/log redaction, DB/migration trên PostgreSQL thật.
- Kiểm tra full browser flow partner với API/PostgreSQL/Mailpit: register → OTP → verify → login `PENDING_ONBOARDING` → logout, validation và F5.
- Phân biệt rõ status `202` generic không nhất thiết gửi OTP nếu contact đã active/không phải partner pending; test dùng contact synthetic mới.

### Mốc 4 — F01.4 Admin bootstrap/hardening

- Trước khi code, lập Planner/Designer deliverable theo `docs/process.md`: scope/non-goals, actor, acceptance criteria, API/error contract, thiết kế dữ liệu, threat/abuse controls và testcase.
- Tìm yêu cầu hiện có trong nghiệp vụ/implementation docs. Nếu chính sách bootstrap Admin (nguồn secret, một lần hay nhiều lần, recovery/rotation, audit, môi trường) chưa được tài liệu xác định và chặn thiết kế an toàn, hỏi người dùng đúng câu hỏi tối thiểu trước khi code phần phụ thuộc.
- Không tạo mật khẩu/token mặc định, seed Admin mở trên public API, secret trong repo/log, hoặc cấp quyền qua client input. Fail safe nếu cấu hình thiếu/sai.
- Chỉ sau khi contract được chốt mới triển khai DB/API/UI cần thiết; kiểm tra concurrency/idempotency, scope/authorization, audit/redaction và migration trên PostgreSQL thật.

### Mốc 5 — Tích hợp và bàn giao F01

- Independent review khi có thể, sửa các lỗi phát hiện được và chạy lại kiểm tra bị ảnh hưởng.
- Chạy build/typecheck/API/DB/browser checks phù hợp, báo đúng trạng thái cùng lệnh và lý do; không dùng suite tổng hợp để nâng testcase riêng thành PASS khi assertion không bao phủ đủ điều kiện.
- Đối chiếu tất cả acceptance của F01.1–F01.4. Chỉ đổi từng feature sang DONE khi acceptance cần thiết pass, UI runtime được kiểm tra, DB thật được dùng ở ca bắt buộc, không còn lỗi nghiêm trọng và `docs/progress.md` có bằng chứng.
- Không bắt đầu F02 cho tới khi luồng F01 cần thiết được nghiệm thu theo roadmap.

## Quy tắc xuyên suốt

- Giữ nghiệp vụ `AGENTS.md`: backend enforce account type/status/scope; PostgreSQL là nguồn dữ liệu chuẩn; không dùng mock thay cho test transaction; không log secrets/OTP/token/PII.
- Không thêm tính năng ngoài F01, không đổi contract giữa các mốc nếu chưa cập nhật tài liệu và nơi phụ thuộc.
- Cập nhật feature spec, testcase và `docs/progress.md` sau mỗi mốc: hoàn thành, bằng chứng, điểm mở và bước kế tiếp.
- Kết thúc bằng báo cáo tiếng Việt: trạng thái từng F01.x, thay đổi chính, testcase PASS/FAIL/NOT RUN/BLOCKED, giới hạn/bước chặn và hướng dẫn UI/Postman local.

---
