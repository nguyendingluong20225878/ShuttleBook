# Testcase F00 — Project foundation

Môi trường local: Windows PowerShell, .NET SDK 10.0.401 trong `.tools/dotnet`, PostgreSQL/PostGIS Docker local. Không ghi connection string hoặc mật khẩu vào tài liệu này.

| ID | Mục tiêu | Tiền điều kiện | Các bước | Kết quả mong đợi | Loại test | Kết quả thực tế |
|---|---|---|---|---|---|---|
| F00-T01 | Build ba web độc lập | `npm ci` hoàn tất | `npm.cmd run build` | Ba portal typecheck và tạo `dist` | Build | PASS — 2026-09-24, customer/partner/admin đều build thành công |
| F00-T02 | Smoke UI desktop/mobile | Chromium Playwright đã cài, các cổng preview không bị chiếm | `npm.cmd run test:web` | Mỗi portal có title, heading, thông báo khởi tạo và không overflow | E2E | PASS — 2026-09-24, 6/6 test pass trong 6.9 giây |
| F00-T03 | Build API, Worker, Migrator và test project | Restore hoàn tất | `scripts/dotnet.ps1 build backend/ShuttleBook.slnx --no-restore` | 0 warning, 0 error | Build | PASS — 2026-09-24 |
| F00-T04 | Baseline migration chạy lặp an toàn | Docker PostGIS healthy, `.env` tồn tại | Chạy Migrator hai lần | Cả hai lần thành công; lần sau không áp dụng lại migration | Integration | PASS — 2026-09-24, báo `Database migrations applied successfully` hai lần |
| F00-T05 | Kiểm tra baseline trên PostgreSQL/PostGIS thật | Docker PostGIS và quyền `CREATE DATABASE` local | `scripts/Test-Database.ps1` | DB tạm có migration/extensions; readiness đổi đúng khi extension/history bị thiếu; DB tạm được dọn | Integration | PASS — 2026-09-24, 1/1 test pass, 45.2 giây |
| F00-T06 | Contract live/ready, lỗi và CORS | Backend build hoàn tất | `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-restore` | API test pass; kiểm tra 200/503, Problem Details, trace ID, CORS allowlist | Integration/API | PASS — 2026-09-24, 15/15 test pass |
| F00-T07 | Runtime health với database local | Docker và migration nền đã sẵn sàng | Chạy API, gọi `/health/live` và `/health/ready` | Cả hai trả 200 `Healthy` | Runtime smoke | Chưa chạy trong phiên nghiệm thu |
| F00-T08 | CI có cùng check với database thật | Repo có remote GitHub | Push branch và xem workflow | Web/backend jobs đều pass | CI | NOT RUN — chưa có remote/push |

Lệnh PowerShell script phải dùng `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...` khi máy chặn execution policy. `PASS` chỉ xác nhận lệnh/môi trường nêu trong dòng đó.
