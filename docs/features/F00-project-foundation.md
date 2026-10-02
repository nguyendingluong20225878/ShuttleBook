# F00 — Project foundation

Trạng thái: IN_PROGRESS. Mục tiêu: có nền tảng phát triển tái lập được trước khi viết feature nghiệp vụ.

## Phạm vi

- Git local, hướng dẫn agent, quy trình, roadmap, nhật ký tiến độ.
- Ba React/TypeScript/Vite web build riêng; API ASP.NET Core và Worker chạy riêng.
- PostgreSQL/PostGIS local, migration nền có lịch sử và chạy lại an toàn.
- Health liveness/readiness, lỗi Problem Details có trace ID, CORS local allowlist.
- Cấu hình môi trường, lệnh restore/build/test, CI và setup.

Ngoài phạm vi: đăng nhập, bảng nghiệp vụ, booking, QR upload, Maps, AWS, triển khai production. Trang web F00 chỉ là trang khởi tạo, không mô phỏng booking đã hoạt động.

## Actor và quyền

Developer chạy môi trường local. Health public chỉ trả trạng thái tổng hợp, không trả secrets/exception nội bộ. Chưa có endpoint nghiệp vụ hoặc hệ thống phân quyền; không dùng F00 cho dữ liệu thật.

## Contract

| Endpoint | Thành công | Thất bại |
|---|---|---|
| `GET /health/live` | 200, `{"status":"Healthy"}` khi process phục vụ request | Không phụ thuộc database |
| `GET /health/ready` | 200 khi DB kết nối được, migration nền và extensions đã có | 503, `{"status":"Unhealthy"}`; không trả connection string |

Endpoint không tồn tại: 404 Problem Details. Exception không xử lý: 500 Problem Details, trace ID, không stack trace/secret trong response. Health và lỗi dùng `Cache-Control: no-store` khi thích hợp.

## Dữ liệu và cấu hình

Migration nền chỉ bật `postgis`, `btree_gist` và lịch sử migration; không tạo bảng booking/user giả. Migration chạy bằng lệnh riêng, không ngầm chạy khi API/Worker khởi động. PostgreSQL local dùng cổng chỉ bind loopback và named volume.

Môi trường chính: Windows PowerShell; không trộn dependency cài trong WSL vào Windows. Toolchain/phiên bản cụ thể ghi tại setup và manifest. `.env` local bị ignore; `.env.example` không có secrets thật.

## Luồng và UI

Developer setup → restore → chạy DB → migrate → chạy API/Worker → mở từng web → build/test. Khi thiếu DB, liveness vẫn trả 200 và readiness trả 503. Thiếu connection string phải có lỗi cấu hình rõ ràng, không fallback sang DB thật.

Web F00 chỉ hiển thị tên cổng và thông báo đang phát triển. Chưa gọi API nên loading/error/empty từ API chưa áp dụng; các trạng thái đó sẽ được đặc tả ở feature dữ liệu đầu tiên.

## Acceptance criteria

- AC01: Git local ở `main`, các file hướng dẫn và ignore có đủ; không có remote/commit tự tạo.
- AC02: Ba web typecheck/build độc lập và có lệnh dev/cổng riêng.
- AC03: API và Worker build, khởi động được với cấu hình local.
- AC04: Migration chạy trên PostgreSQL/PostGIS mới, chạy lại không đổi schema; extensions và migration history được xác minh.
- AC05: `/health/live` và `/health/ready` phân biệt DB hoạt động/DB lỗi đúng contract; readiness kiểm tra baseline đã áp dụng.
- AC06: CORS chỉ cho ba origin local đã cấu hình; lỗi 404/500 có Problem Details và trace ID.
- AC07: Kiểm thử backend quan trọng và smoke frontend pass; CI khai báo cùng các check với DB thật.
- AC08: Tài liệu setup có lệnh/cwd/expected result/troubleshooting; kết quả thật được ghi trong testcase, không nhận scaffold là nghiệm thu xong.

## Kiểm thử

Theo [F00-test-cases.md](../testing/F00-test-cases.md). Reviewer độc lập kiểm tra nền tảng và hướng dẫn; những kiểm thử phụ thuộc công cụ chưa cài ghi BLOCKED/NOT RUN và F00 vẫn IN_PROGRESS.
