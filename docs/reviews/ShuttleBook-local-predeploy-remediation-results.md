# ShuttleBook — kết quả hoàn thiện local trước deploy

Ngày 2026-10-09. **NO-GO / chưa đủ bằng chứng để xin deploy.** Báo cáo này chỉ tính lệnh chạy trong lượt thực hiện prompt; không đổi trạng thái F09 thành DONE.

## Quyết định người dùng

- Trần 10.000.000 VND trên tổng một booking vãng lai hoặc tổng toàn kỳ cố định (một payment anchor); vượt trần từ chối tại quote, UI giải thích.
- Mỗi Customer tối đa một quote hold active, chung casual/fixed và mọi sân; quote mới bị từ chối, quote cũ giữ nguyên.
- Giữ nhận diện ShuttleBook, chuẩn hóa thành phần; backup viết script và diễn tập dữ liệu thử trước. Destination, retention, RPO/RTO và người nhận cảnh báo chưa chốt.
- Cho phép tạo nhánh `codex/f09-local-readiness`, commit/push các thay đổi F09 để chạy workflow; không merge/deploy.

## Thay đổi đã chuẩn bị

| Lát cắt | Thay đổi | Trạng thái |
|---|---|---|
| Tiền/quota | API trả `amountExact` và `pricePerSlotExact` cho casual, server tính nguyên VND, chặn tổng >10 triệu ở quote/create casual/fixed. PostgreSQL advisory transaction lock theo Customer + đọc hold active toàn tài khoản; HTTP 409 `AMOUNT_LIMIT_EXCEEDED`, `ACTIVE_QUOTE_EXISTS`. UI giải thích và không chủ động lấy quote mới trước TTL. | Focused PostGIS integration **PASS 6/6**; full suite đang chạy. |
| CI | Workflow Ubuntu build workspace và gọi Playwright CLI, `workflow_dispatch`, MapTiler fixture key và Identity test keys; M03 timing dựa trên mốc sự kiện, Admin test đợi request async. | Local PASS; hosted **NOT RUN**. |
| Backup/monitoring | Script backup DB + media/manifest SHA-256, restore DB/media thử riêng; script snapshot health/outbox/owner SLA, runbook local. | Cú pháp PASS; drill runtime **BLOCKED**. |
| UI | Token/primitive CSS chung cho ba cổng, áp dụng trước vào Customer quote, Partner quyết định tiền, Admin duyệt hồ sơ. Giữ cấu trúc trang; focus teal, tổng tiền exact và thông báo quota/cap. | Build/browser fixture PASS; manual matrix chưa đạt. |

## Bằng chứng mới

| Gate/lệnh | Kết quả | Giới hạn |
|---|---|---|
| `npm.cmd run typecheck --workspaces --if-present` | **PASS**, 4 workspaces. | Static check. |
| `npm.cmd run build --workspaces --if-present` với `VITE_MAPTILER_API_KEY=ci-fixture-key` trong session | **PASS**, ba portal. | Key giả chỉ cho fixture; không là provider live. |
| `dotnet build backend/ShuttleBook.slnx --no-restore -v q` | **PASS**, 0 warning/error sau test code mới. | Compile không thay integration. |
| `npm.cmd run test:api` | **PASS 96/96**. | Chạy qua script nạp test config; direct focused 2/2 với CI dummy keys cũng PASS. |
| `scripts/Test-Web.ps1 --workers=2 --reporter=line` trên build cuối | **PASS 248, SKIP 8, FAIL 0**, 1,5 phút. | 8 live test có chủ đích SKIP; fixture không chứng minh API/PostGIS/Worker. |
| `scripts/Test-Database.ps1` filtered tiền/quota/race và số tiền lịch sử | **PASS 6/6**, 1m27 sau khi PostGIS tại `127.0.0.1:54329` hoạt động lại. | Full DB đang chạy; `pg_dump`, `pg_restore`, `createdb`, `psql` vẫn chưa có trong PATH phiên này. |
| PowerShell parser cho backup/health scripts | **PASS**. | Backup/restore thực tế **NOT RUN**. |
| `Get-LocalOperationalStatus.ps1` | **FAIL đúng trạng thái**: `liveHttp=0`, `readyHttp=0`, exit 1 vì API local không chạy. | Worker chưa xác minh. |
| `git diff --check` | **PASS**, chỉ cảnh báo chuyển CRLF→LF. | Nhánh thử nghiệm sẽ chỉ chứa file F09 đã chọn. |

Lượt browser đầu trên build không có MapTiler fixture key: 239 PASS/5 FAIL/10 SKIP; 4 Partner test thiếu control địa chỉ, 1 Admin assert request đến sớm. Sau bổ sung key giả, chờ request và rebuild, focused 6/6 và full cuối 248/8 đạt. Lượt direct API không nạp OTP pepper: 94 PASS/2 FAIL do cấu hình test; sau dùng script local và CI dummy pepper, 96/96 và focused 2/2 đạt. Các FAIL lịch sử này không được trình bày thành lỗi backend tiền/quota.

## Gate còn mở

1. Full PostGIS DB suite và hosted CI phải kết thúc, sửa mọi regression nếu có. Focused tiền/quota/race casual↔fixed trên hai sân đã **PASS 6/6**; chưa suy full suite từ focused.
2. PostgreSQL CLI + media thử: backup, checksum tamper, restore DB/media riêng; đối chiếu QR/proof quyền Customer/Owner, Worker restart/retry và booking đã báo chuyển không tự giải phóng.
3. Hosted GitHub Actions trên commit ứng viên: quyền commit/push nhánh thử đã được cấp; cần ghi run URL/SHA/status. Workflow đọc source không là CI PASS.
4. Manual UI từng nhóm Customer/Partner/Admin ở 375/768/1024/1440 và zoom 200%, keyboard/focus/NVDA, screenshot before/after và nghiệm thu cảm nhận. Fixture mobile quote đã được xem, chưa bao phủ toàn bộ.
5. Backup destination/retention/RPO/RTO, người nhận/kênh cảnh báo, ngưỡng tải và pilot người vận hành phải được chốt trước vận hành thật. HTTPS/CORS/cookie/secrets, live browser→API→Worker→PostGIS/Mailpit và release/rollback cần test môi trường thử khi có quyền.

Không đổi booking đã tạo, không thêm feature nhân viên F08, không dùng ngân hàng thật hoặc tài nguyên trả phí. Không merge/deploy.
