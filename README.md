# ShuttleBook

Nền tảng đặt sân cầu lông: ba cổng React/TypeScript/Vite dùng chung ASP.NET Core API, Worker và PostgreSQL/PostGIS.

**Đang kiểm chứng F01.1–F01.3 — đăng ký, xác minh và phiên đăng nhập.** Ba portal và API đã có mã nguồn; booking chưa được triển khai. Xem [tiến độ và kết quả kiểm thử](docs/progress.md) trước khi tiếp tục.

- [Setup local từng bước](docs/setup.md)
- [Quy trình và roadmap](docs/process.md)
- [Hướng dẫn cho agent](AGENTS.md)
- [Thiết kế nghiệp vụ/kỹ thuật](docs/README.md)
- [Đặc tả F00](docs/features/F00-project-foundation.md)
- [Testcase F00](docs/testing/F00-test-cases.md)

## Cấu trúc

```text
apps/{customer-web,partner-web,admin-web}  # Ba web độc lập
packages/ui                              # Thành phần chung
backend/src/                             # API, Infrastructure, Migrator, Worker
backend/tests/                           # API contract + integration DB thật
tests/web/                               # Smoke test production builds
scripts/                                 # Setup và lệnh PowerShell
docs/                                    # Quyết định, feature, testcase, tiến độ
```

## Thiết lập lần đầu (PowerShell, tại thư mục repo)

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Initialize-Local.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-DotNet.ps1
npm.cmd ci
npm.cmd run setup:browsers
```

## Chạy hằng ngày (mỗi lệnh dev trong terminal riêng)

```powershell
npm.cmd run db:up
npm.cmd run db:migrate
npm.cmd run dev:api
npm.cmd run dev:partner
```

`npm.cmd run dev:customer` và `npm.cmd run dev:admin` chạy hai cổng còn lại. Mỗi tiến trình dev chạy trong terminal riêng; dừng bằng Ctrl+C.

Doctor báo thiếu Docker thì làm theo [setup](docs/setup.md) trước khi migrate/test database. Web có thể build và xem trước khi có database. Không nhận scaffold hoặc build frontend thành công là toàn bộ F00 đã hoàn thành.
