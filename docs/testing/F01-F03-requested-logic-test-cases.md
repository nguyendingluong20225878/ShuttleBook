# Testcase thay đổi F01–F03 ngày 2026-10-06

Trạng thái: **IN_PROGRESS**. Môi trường Windows PowerShell tại repo root; DB test dùng database PostGIS tạm do script tạo và dọn. MapTiler/S3 provider thật chưa có cấu hình để kiểm chứng.

| ID | Mục tiêu và bước | Kết quả mong đợi | Loại | Bằng chứng hiện tại |
|---|---|---|---|---|
| A01 | Admin login rồi F5 trong 30 phút | Khôi phục Admin, không có token trong local/sessionStorage | Browser + API/DB | Browser desktop/mobile PASS với API giả; API cookie/Origin 1/1 PASS |
| A02 | Đặt `last_activity_at` cũ 31 phút rồi refresh/F5 | `401`, family bị revoke, về login | PostgreSQL + browser | DB PASS 1/1, browser nhánh restore 401 desktop/mobile PASS (API giả) |
| A03 | Không thao tác 30 phút, refresh nền hoặc tải dữ liệu nền | Không gia hạn mốc idle | Browser + DB | NOT RUN; cần kiểm tra live/clock giả |
| A04 | Logout, rotate, suspend, revoke; thử restore/bearer cũ | Mất hiệu lực ngay | PostgreSQL + API | NOT RUN cho cookie restore; các ca F01.4 cũ đã có |
| A05 | Hai tab F5/restore đồng thời | Cùng khôi phục một refresh token ổn định; không revoke oan family | PostgreSQL + browser | DB service PASS 1/1 sau đổi sang endpoint restore không rotate; browser đa tab live NOT RUN |
| G01 | Thêm venue bằng gợi ý Google, xem map rồi xác nhận | Form gửi địa chỉ và tọa độ đã chọn, PostGIS lưu đúng | Browser + DB | Browser giả lập desktop/mobile PASS; provider thật NOT RUN |
| G02 | Không chọn/xác nhận địa chỉ mới | Chặn submit, không ghi venue | Browser | Desktop/mobile PASS trong `npm.cmd run test:web`; chạy lại mục tiêu sau marker Google 4/4 PASS |
| G03 | Sửa draft/revision, thử business khác và timezone có khoảng trắng | Giữ scope/version/approval; chuẩn hóa timezone | API + DB + browser | Code đã sửa; testcase mới NOT RUN |
| P01 | Upgrade F03 có court/session hiện hữu | Chính sách mặc định 30/30/20; dữ liệu cũ giữ nguyên | PostgreSQL | Hai ca migration upgrade đã PASS; assert policy riêng NOT RUN |
| P02 | Owner đặt block 60/90, min là bội số, hold 20 | GET phản hồi đúng, version/audit tăng; giá preview theo 30 phút | API + PostgreSQL | PostGIS thật PASS 1/1 gồm owner scope, If-Match, giá preview 60/90 và check constraint |
| P03 | Block/min/hold sai, thiếu/sai If-Match, owner ngoài scope | `400`/`428`/`412`/`404` tương ứng, không ghi | API + PostgreSQL | NOT RUN |
| P04 | Giá cao điểm/cuối tuần/ngày lễ cụ thể và bảo trì overlap | Tổng mỗi ca đúng; allocation không chồng | PostgreSQL | Bộ F03 cũ đã PASS; chính sách mới chưa chạy full |

Lượt gần nhất: build/typecheck PASS; API 78/78 PASS; web 34 PASS/8 live SKIP; Postgres ca mục tiêu PASS khi server mở. `npm.cmd run test:db` đầy đủ lần cuối 3 PASS/21 FAIL do `127.0.0.1:54329` từ chối kết nối; không quy các lỗi kết nối này thành lỗi assertion của code. Cần bật lại DB và chạy full suite trước khi đóng hạng mục.
