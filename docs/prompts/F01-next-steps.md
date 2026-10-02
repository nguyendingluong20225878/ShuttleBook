# Prompt tiếp tục phần còn lại của F01

Copy toàn bộ prompt dưới đây vào phiên làm việc tiếp theo.

---

Bạn là agent tích hợp chịu trách nhiệm hoàn tất phần còn lại của ShuttleBook F01 trong repository hiện tại.

## Điểm bắt đầu bắt buộc

1. Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, tài liệu nghiệp vụ `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`, toàn bộ feature/testcase F01 hiện có và `docs/setup.md`.
2. Kiểm tra Git, source và bằng chứng runtime trước khi sửa. Không làm lại F01.2/F01.3 nếu không có lỗi hồi quy cụ thể.
3. Giữ trạng thái đã được người dùng nghiệm thu ngày 2026-10-02: **F01.2 DONE**, **F01.3 DONE**. Các kiểm tra quyền vận hành phụ thuộc endpoint F02/F06 được thực hiện tại feature sở hữu endpoint.
4. Không tự commit/push/merge/deploy nếu phiên mới không có yêu cầu rõ ràng.

## Mốc 1 — Đóng F01.1 nếu còn mở

- Đối chiếu `docs/features/F01.1-customer-registration.md`, `docs/testing/F01.1-test-cases.md` và bằng chứng trong `docs/progress.md`.
- Chỉ bổ sung kiểm thử hoặc sửa mã cho tiêu chí còn thiếu; không làm lại phần đã có bằng chứng PASS.
- Kiểm tra PostgreSQL/PostGIS thật cho transaction/concurrency/migration và browser nối API/Mailpit thật ở các ca bắt buộc.
- Cập nhật từng testcase và chuyển F01.1 sang DONE chỉ khi acceptance còn lại đạt.

## Mốc 2 — Planner cho F01.4 Admin bootstrap/hardening

- Tạo đặc tả `docs/features/F01.4-admin-bootstrap.md` và testcase `docs/testing/F01.4-test-cases.md` trước khi code.
- Ghi scope/non-goals, actor, threat model, acceptance criteria, API/error contract, dữ liệu/migration, audit/redaction, concurrency/idempotency và kế hoạch recovery/rotation.
- Đối chiếu nghiệp vụ hiện có. Nếu chính sách bootstrap Admin chưa đủ để thiết kế an toàn, hỏi người dùng tối thiểu các quyết định thực sự chặn: nguồn thông tin xác thực ban đầu, bootstrap một lần hay có recovery, môi trường được phép chạy, và cơ chế rotation/revocation.
- Không tạo tài khoản/mật khẩu mặc định, public admin-registration API, secret trong source/log/database plaintext, hoặc cho client tự gán `ADMIN`.

## Mốc 3 — Thiết kế và triển khai F01.4

- Chốt contract trước khi code; triển khai lát cắt Database → API/CLI an toàn → UI nếu đặc tả yêu cầu → kiểm thử → review.
- Bootstrap phải fail safe khi thiếu/sai cấu hình, có audit đã redact, chống chạy lặp/cạnh tranh và không làm thay đổi role của account hiện hữu ngoài contract.
- Backend luôn kiểm tra trạng thái, account type và scope; PostgreSQL là nguồn dữ liệu chuẩn.
- Test migration trên DB mới và chạy lặp; test bootstrap đồng thời/idempotency, secret leakage, authorization, recovery/rotation theo contract đã chốt.

## Mốc 4 — Tích hợp và đóng F01

- Chạy build, typecheck, API tests, PostgreSQL integration tests và browser tests phù hợp; báo PASS/FAIL/NOT RUN/BLOCKED kèm lệnh và lý do.
- Thực hiện independent review khi có thể, sửa lỗi nghiêm trọng và chạy lại phần bị ảnh hưởng.
- Đối chiếu trạng thái F01.1–F01.4. Không đổi trạng thái đã nghiệm thu nếu không có bằng chứng hồi quy; không đánh dấu F01 tổng thể DONE khi F01.1 hoặc F01.4 còn thiếu acceptance.
- Cập nhật `docs/progress.md`, feature spec và testcase sau mỗi mốc. Báo cáo cuối bằng tiếng Việt, không ghi secrets/OTP/token/PII.

---
