# ShuttleBook — runbook kiểm trước deploy tại local

Ngày 2026-10-09. Đây là **quy trình thử**, chưa cấp quyền deploy. Làm trong PowerShell tại repo root. PostgreSQL/PostGIS và media Local là một cặp dữ liệu; chỉ backup khi đã dừng ghi API/Worker hoặc có snapshot đồng thời.

## 1. Gate code và môi trường

```powershell
npm.cmd ci
npm.cmd run typecheck
$env:VITE_MAPTILER_API_KEY='ci-fixture-key' # Chỉ cho browser fixture; không dùng để deploy.
npm.cmd run build
npm.cmd run test:api
npm.cmd run test:web -- --workers=2
npm.cmd run test:db
```

`test:db` cần PostgreSQL/PostGIS tạm có quyền `CREATE DATABASE`. Browser fixture dùng key MapTiler giả và stub HTTP provider, không xác nhận MapTiler thật. Chạy migrator hai lần trên **DB thử** để kiểm repeat; khi release, backup trước migration và có rollback ứng dụng. Không downgrade schema trên dữ liệu thật theo mặc định.

## 2. Backup và restore drill

Cài PostgreSQL CLI cùng major server (`pg_dump`, `pg_restore`, `createdb`, `psql`) và đưa vào PATH **của phiên PowerShell**. Đặt `PGHOST`, `PGPORT`, `PGUSER`, `PGDATABASE`, `PGPASSWORD` bằng môi trường tiến trình; không gõ mật khẩu trên dòng lệnh hoặc đưa vào repo/log. Script mặc định đọc media private tại `backend/src/ShuttleBook.Api/.media-local`. DB cần đã có migration mới nhất.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Invoke-LocalBackupDrill.ps1 -Mode Backup
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Invoke-LocalBackupDrill.ps1 -Mode Verify -BackupDirectory .local/backups/<id-vừa-tạo>
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Invoke-LocalBackupDrill.ps1 -Mode RestoreDrill -BackupDirectory .local/backups/<id-vừa-tạo>
```

Backup tạo `.local/backups/<id>/database.dump`, `media/`, `manifest.json` với SHA-256. Restore kiểm file trước khi tạo **DB mới `sb_restore_*`** và media mới dưới `.local/restore-drill`; so số hàng bookings/payments/series/allocations/evidence/media. Script không xóa DB thử; sau nghiệm thu, người vận hành rà tên DB/path trước khi tự dọn. `.local/` bị Git ignore nhưng vẫn chứa dữ liệu riêng tư: chỉ giữ trên máy thử có quyền phù hợp. Chưa dùng script này như lịch backup vận hành.

Drill đầy đủ cần khởi động API riêng trỏ DB/media restore, đăng nhập các tài khoản test, xác nhận QR/proof đúng người xem được và người ngoài quyền nhận 403/404. Kiểm booking đã `TRANSFER_REPORTED`/`AWAITING_OWNER_CONFIRMATION` không bị Worker expiry giải phóng; khởi động lại Worker, xem outbox retry xử lý một lần. Không dùng tiền/ngân hàng thật. RPO/RTO, retention, destination ngoài vùng chạy và người nhận cảnh báo **chưa chốt**.

## 3. Quan sát local và rollback

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Get-LocalOperationalStatus.ps1` kiểm API health, exit nonzero khi live/ready không 200. Thêm `-Database` khi đã đặt PG* và có `psql`: chỉ in số đếm, không in payload/PII. `workerVerified=false` nhắc rằng snapshot không chứng minh Worker còn chạy.
- `GET /health/live` trả 200 khi API chạy; `GET /health/ready` trả 200 chỉ khi dependency ready, 503 nếu không. Worker là process riêng, cần giám sát tiến trình và outbox; API ready không chứng minh Worker đang chạy.
- Với DB thử, xem `outbox_messages` có `processed_at IS NULL AND next_attempt_at <= now()`; `attempts >= 8` là ngưỡng retry kỹ thuật hiện tại. Không xuất `payload` hoặc thông tin cá nhân vào log. Xem `payments` có `first_reported_at <= now() - interval '30 minutes'` và trạng thái `TRANSFER_REPORTED`/`NEEDS_REVIEW`; đây là owner confirmation SLA hiện tại. Đối chiếu booking/series anchor trước xử lý tay.
- Giữ bản build ứng dụng trước release, rollback ứng dụng nếu health hoặc luồng chính lỗi. Migration chỉ tiến một chiều nếu có thay đổi dữ liệu không đảo an toàn; phục hồi DB/media phải từ cặp backup đã kiểm và theo quyết định vận hành, không ghi đè máy đang chạy để “thử”.
- Trước môi trường ngoài local: kiểm HTTPS, CORS origin đúng từng cổng, HttpOnly/SameSite/Secure cookie, secret ngoài repo, private QR/proof, cấu hình PostGIS, Worker, backup/alert và người trực xử lý. Chạy lại browser→API→Worker→PostGIS/Mailpit và test quyền trên môi trường thử được phép.

Ghi `PASS/FAIL/NOT RUN/BLOCKED` cho từng lệnh và đường dẫn log/run; hosted CI cần workflow URL từ commit ứng viên. Không coi script tồn tại hoặc YAML hợp lệ là restore/CI đã PASS.
