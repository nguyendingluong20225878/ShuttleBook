# Điều chỉnh UI sau audit — 2026-10-09

Cập nhật nghiệm thu2026-10-09: người dùng xác nhận nhóm2–5 PASS. Acceptance marker ở nhóm1 được thay bằng [Customer không map + account không sidebar](Customer-distance-auth-layout.md); hai thay đổi mới đã build/browser PASS, chờ nghiệm thu tay. Các số liệu/contract bên dưới là phạm vi đợt sửa trước.

Trạng thái: **code và kiểm thử local hoàn tất; chờ nghiệm thu tay bản sửa**. [Kết quả](../testing/UI-post-audit-improvements-results.md) · [Nghiệm thu tay](../testing/UI-post-audit-improvements-manual.md).

## Phạm vi

Sửa năm nhóm trong kết luận audit: marker giữ phiên SPA; recovery tạo đơn vãng lai; giữ trang đơn/thông báo khi refresh/poll/read; thông báo Xem giá; Admin tìm/filter/paging và đối chiếu published → proposed trước quyết định.

Không triển khai tiện ích mật khẩu, phân quyền nhân viên, báo cáo. Không đổi TTL, chính sách fixed series, allocation, quyền payment/approval. Không persist token/intent qua F5. Không commit/push, migrate development hoặc deploy. F07 giữ DONE local theo nghiệm thu trước; đợt sửa UI nghiệm thu riêng.

## Acceptance

1. Marker dùng navigation hiện có, đúng venue và giữ phiên memory đang đăng nhập, không tải lại document.
2. Trước create POST, lưu attempted flag và key/body bất biến. Intent đã gửi được replay cùng key/body dù quoteTTL hết. Quote chưa từng gửi hếtTTL vẫn bị khóa. Khi chưa biết kết quả, không tự lấy quote mới; UI hướng dẫn xem Đơn của tôi hoặc kiểm lại yêu cầu cũ. Lỗi xác định theo contract cũ xóa quote/intent, cho người dùng chủ động lấy báo giá mới.
3. Poll/refresh đơn Partner đọc lại cửa sổ các trang đã tải theo cursor mới; dữ liệu từ server, dedup, không giữ status cũ sau mutation hoặc response muộn ngoài scope. Notification refresh/read giữ cửa sổ đã tải và lấy số chưa đọc từ server. Denied vẫn purge dữ liệu private. Filter/scope mới reset cửa sổ phù hợp.
4. Xem giá thành công báo Đã tính giá; write báo Đã lưu. Failure không mang success style hoặc kết quả preview cũ. Chuyển sân xóa preview.
5. Admin có thể xem hơn100 pending, tìm tên doanh nghiệp và loại hồ sơ; pagination deterministic. Revision hiển thị published hiện tại cùng proposed, gồm bank/account/QR/version. Hai bước approve rõ ràng; cancel chưa ghi quyết định. Lý do trim10–1000. Conflict409 không báo thành công và hướng dẫn tải lại. Mobile không che nút hoặc tràn toàn trang; bảng đối chiếu cuộn nội bộ và truy cập được bằng bàn phím.

## Dữ liệu và contract thống nhất trước code

Không cần migration: dùng approval_requests, businesses, venues, venue_payment_accounts hiện có. PostgreSQL/PostGIS giữ nguồn chuẩn; không thêm trạng thái domain.

### Danh sách Admin

`GET /api/v1/admin/approval-requests/?paged=true&limit=20&q={query}&kind={kind}&before={cursor}`

```text
data: {
  items: Row[],
  nextCursor: string | null,
  totalCount: number,
  pendingCount: number
}
```

- Row giữ các field cũ; totalCount là pending phù hợp filter, pendingCount là toàn bộ pending.
- q trim, tối đa120 ký tự; search literal, không coi %/_ là wildcard nhập từ người dùng.
- kind rỗng hoặc ONBOARDING/VENUE_REVISION; limit1–100, mặc định20.
- Cursor opaque, keyset submittedAt + id tăng dần, ràng buộc q/kind. Client chỉ gửi lại nextCursor; không dùng offset. `before` giữ tên query theo contract đã chốt, dù thứ tự queue đi từ cũ tới mới.
- Query trùng/không biết, invalid filter/limit/cursor:400 VALIDATION_FAILED. Fresh Admin guard:403 FORBIDDEN.
- Không có query: giữ array legacy cap100. UI hỗ trợ cả legacy array và paged envelope trong giai đoạn tương thích; test fixture legacy không thay gate API thật.

### Chi tiết Admin

`GET /api/v1/admin/approval-requests/{id}` giữ snapshot/field cũ, thêm:

```text
current: {
  venueName, version, address, contact, timezone, latitude, longitude,
  bankCode, accountName, accountNumber, qrUploadId
} | null
```

Current dành cho revision, có scope business/venue; onboarding null. Đây là thông tin published hiện tại để đối chiếu, không đổi snapshot booking/payment. Approve/request-changes giữ transaction/guard cũ; backend vẫn kiểm version và trạng thái khi ghi. QR xem qua private media authorization hiện có.

## Testcase và kết quả

- Browser desktop/mobile: marker giữ phiên; lỗi mạng + TTL replay cùng key/body; quote chưa gửi hếtTTL; definitive errors; các trang đơn/thông báo, mutation/read/poll, scope mới/response muộn/deny; preview copy/style; tìm/paging/current/QR/reason/confirm/cancel/409/mobile.
- ASP.NET test host + PostgreSQL/PostGIS tạm: queue103 hồ sơ, cursor sau quyết định không skip, filter/query/cursor invalid, foreign role; published current đúng và GET không ghi dữ liệu; replay tạo đơn đã commit sauTTL vẫn đúng booking/allocation.
- Build/backend/API regression và root review; phân biệt UI fixtures, DB thật và các ca live chưa chạy. Chi tiết lệnh/PASS/FAIL/NOT RUN ở báo cáo kết quả.
