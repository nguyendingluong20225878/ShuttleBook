# Prompt triển khai F02 — onboarding chủ sân và duyệt hồ sơ

> Dùng prompt này sau khi đã đưa mốc F01.4 lên Git. Trạng thái tại lúc viết: F01.1–F01.4 đã có bằng chứng nghiệm thu local trong `docs/progress.md`, nhưng F01.4 và một số chỉnh sửa F01.1 vẫn đang là working tree chưa commit. Chạy lại `git status` và đối chiếu commit mới trước khi sửa. Không ghi đè, reset, commit hoặc push thay người dùng.

---

Bạn là agent tích hợp của ShuttleBook, chịu trách nhiệm **hoàn thành F02 theo luồng đang có trong code**. Trả lời và ghi tài liệu bằng tiếng Việt. Tiếp tục từ code và bằng chứng thực tế, không xây lại F01.

## 1. Đọc và chốt contract trước khi code

1. Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, `docs/README.md`, `docs/00-luong-nghiep-vu.md` đến `docs/05-implementation-guide.md`, `docs/features/F01.3-partner-registration.md`, `docs/features/F01.4-admin-bootstrap.md`, testcase tương ứng và `docs/setup.md`.
2. Kiểm tra Git, migration, `ShuttleBookDbContext`, identity/JWT guard, partner/admin portal, test API/DB/browser và bằng chứng runtime. Giữ các thay đổi F01 của người dùng. F01.4 có Admin CLI/login/me; F02 nối tiếp chính các phiên này.
3. Hiện repo chưa có `docs/features/F02-*.md` hay `docs/testing/F02-*.md`. Lập chúng **trước khi code**: actor, phạm vi/non-goals, acceptance, request/response/error contract, quyền theo business/venue, schema/migration, transaction/concurrency, UI states và testcase. Ghi rõ giả định cùng mâu thuẫn tài liệu.
4. Ranh giới F02/F03: F02 nhận thông tin business → venue → court, vị trí, ảnh và thông tin QR/giá cần cho hồ sơ duyệt. F03 mới mở vận hành giờ, pricing rules theo ca, payment account và maintenance. Không phê duyệt một hồ sơ được mô tả là “đủ giá/QR” khi dữ liệu ấy chưa có. Chọn một contract nháp rõ ràng cho giá/QR tại F02 và được F03 tái sử dụng, hoặc tích hợp phần cấu hình tối thiểu F03 cần để gửi duyệt; ghi quyết định vào đặc tả. Không tự tạo booking, availability, nearby search hay invitation ở F02.

## 2. Luồng nghiệp vụ bắt buộc

1. User `VENUE_OPERATOR/PENDING_ONBOARDING` đã xác minh và đăng nhập tại partner portal tạo business `DRAFT`. Backend lấy `sub` từ access token, đồng thời tạo đúng một `business_membership` `OWNER/PENDING` cho chính user ấy. Không nhận `ownerId`, `accountType`, role, membership hoặc trạng thái từ client. Chặn hồ sơ rác/duplicate và không cho operator pending chiếm business của người khác.
2. Owner của business tạo/sửa một hoặc nhiều venue `DRAFT`, mỗi venue có địa chỉ, tọa độ PostGIS `geography(Point,4326)`, timezone IANA, contact, ảnh/tiện ích nếu cần; tạo/sửa court thuộc venue. Owner chỉ đọc/sửa resource trong business mình sở hữu. Trong lúc `PENDING_APPROVAL`, khóa các trường quan trọng và từ chối submit lặp.
3. Media dùng S3 private và presigned PUT theo tài liệu: API sinh key, giới hạn MIME/size/checksum, lưu upload pending; `complete` kiểm tra object thực rồi mới mark READY. Chỉ gắn ảnh/QR READY đúng resource/scope; không tin URL hoặc key do client tự đưa. Nếu môi trường local chưa có S3 tương thích, cấu hình adapter local có kiểm chứng tương đương và ghi rõ giới hạn; không giả báo upload pass.
4. Owner gửi duyệt khi hồ sơ đủ dữ liệu theo contract đã chốt. Một transaction tạo `approval_request` kèm snapshot, chuyển business/venue sang `PENDING_APPROVAL`, audit và đảm bảo retry/concurrency không có hai hồ sơ pending. Không cho pending owner publish, tự active membership, xác nhận payment hoặc dùng API operator vận hành.
5. Admin `ACTIVE` có quyền duyệt xem danh sách/chi tiết pending. `request-changes` bắt buộc lý do, lock hồ sơ, lưu người/thời điểm/audit và đưa business/venue về `DRAFT` cho owner sửa rồi gửi lại. `approve` lock hồ sơ và kiểm tra lại dữ liệu/snapshot; chuyển business + owner membership + user thành `ACTIVE`, venue thành `PUBLISHED`, court hợp lệ thành `ACTIVE`, ghi audit. Duyệt lặp/race phải có một quyết định; không tạo thêm membership hoặc side effect lặp. Chỉ Admin được quyết định.
6. Sau duyệt, partner portal hiển thị trạng thái hoạt động; sửa guard frontend hiện chỉ chấp nhận `PENDING_ONBOARDING` ở `apps/partner-web/src/main.tsx` để chấp nhận `ACTIVE` sau phê duyệt, nhưng vẫn kiểm tra account type. Admin portal hiển thị danh sách, chi tiết và thao tác phê duyệt/yêu cầu sửa. Chỉ venue `PUBLISHED` cùng court `ACTIVE` được phép xuất hiện trong contract đọc public của F04; không tạo endpoint public trả nháp hoặc thông tin ngân hàng nhạy cảm. Thay đổi nhạy cảm sau publish dùng revision chờ duyệt theo `docs/02-states-and-sequences.md`, không làm biến mất dữ liệu đang hoạt động.

## 3. API và lỗi cần thiết kế cụ thể

- Giữ prefix `/api/v1`, envelope `{ data, traceId }` và `application/problem+json` với `status`, `code`, `traceId` theo F01.
- Giữ các route tài liệu đã nêu: `POST /partner-onboarding/businesses`, `POST /partner-onboarding/businesses/{id}/venues`, `POST /partner-onboarding/businesses/{id}/submit`, `GET /admin/approval-requests`, `POST /admin/approval-requests/{id}/approve`, `POST /admin/approval-requests/{id}/request-changes`, `POST /uploads/presign`, `POST /uploads/{id}/complete`. Bổ sung GET/PUT/court routes có tên nhất quán để UI thực sự hoàn thành luồng; mô tả từng DTO/status và field allowlist trong đặc tả.
- Xử lý rõ: `401 UNAUTHORIZED`, `403 FORBIDDEN`, `404 NOT_FOUND` (không để lộ business khác), `400 VALIDATION_FAILED`/`UNSUPPORTED_FIELD`, `409` cho state conflict/submit lặp/stale revision, `429 RATE_LIMITED`, lỗi upload provider phù hợp. Không để exception DB/S3 lộ PII hoặc secret.
- Mỗi endpoint kiểm tra account type và **trạng thái user hiện tại từ DB**, membership cùng resource scope; JWT claim không thay thế guard. Admin login/me hiện có là nền cho guard Admin.

## 4. Thứ tự triển khai và kiểm thử

1. **Database:** migration tăng dần cho business, owner membership, venue/PostGIS, court, approval/snapshot và upload metadata cần thiết. FK/unique/check/index bảo vệ integrity; migration fresh, lặp và nâng cấp từ dữ liệu F01. PostgreSQL/PostGIS thật là nguồn chuẩn, không thay bằng mock.
2. **API:** triển khai các transaction/guard/state transition, validation chặt và audit. Phân quyền business/venue dựa trên DB. Không dùng client input để nâng quyền hoặc publish.
3. **UI:** partner hoàn tất draft → venue/court/media → xem lại → submit → xem trạng thái/lý do sửa; admin list/detail → approve/request changes. Giữ access/refresh token trong state theo F01, xử lý expiry/reload và trạng thái loading/error/empty. Không phá luồng register/login/logout hiện tại.
4. **QA:** testcase theo rủi ro gồm owner A không sửa business B, customer/pending chưa xác minh không tạo hồ sơ, owner không tự publish, Admin mới quyết định được, thiếu trường/giá/QR không submit, request-changes rồi resubmit, approve lặp và race, snapshot bất biến, upload sai type/size/checksum/scope, venue nháp không lộ public, migration fresh/upgrade, browser desktop/mobile thực qua API/PostgreSQL/S3 local tương thích nếu có. Test transaction/concurrency bằng PostgreSQL/PostGIS thật.
5. Chạy build/typecheck, API tests, DB integration, browser tests và `git diff --check`; review độc lập nếu có điều kiện. Báo từng nhóm **PASS/FAIL/NOT RUN/BLOCKED** cùng lệnh và lý do; test host mock không thay bằng chứng DB/UI runtime. Sửa lỗi review, chạy lại phần ảnh hưởng.

## 5. Bàn giao

- Cập nhật `docs/features/F02-*.md`, `docs/testing/F02-*.md`, `docs/progress.md`, `docs/setup.md` theo code và kết quả thật. Chỉ ghi **F02 DONE** khi toàn bộ acceptance và kiểm thử bắt buộc đạt, không còn lỗi nghiêm trọng. Nếu phụ thuộc giá/QR hoặc S3 chưa khả dụng thì ghi mốc con và giữ **IN_PROGRESS**, nêu blocker cụ thể.
- Hướng dẫn tại repo root trên PowerShell: chuẩn bị dịch vụ local, migrate an toàn, chạy API/partner/admin portal, bước UI hoặc Postman, kết quả mong đợi và lỗi thường gặp. Không chạy migration/drop trên DB có dữ liệu thật; không in/lưu secret, token, QR hoặc contact thật trong bằng chứng.
- Không commit/push/merge/deploy nếu người dùng chưa yêu cầu. Không thêm check-in, customer cancel/reschedule, booking/payment hoặc staff invitation ngoài F02.

---
