# Kết quả feedback sân, giá và hồ sơ — 2026-10-14

## Đã thực hiện

| Kiểm tra | Kết quả | Bằng chứng |
| --- | --- | --- |
| Backend Release build | PASS, 0 warning, 0 error | `.local/partner-followup-build.log` |
| Typecheck bốn workspace | PASS | `.local/partner-followup-typecheck-final.log` |
| API tests | PASS 96/96 | `.local/partner-followup-api.log` |
| PostGIS tập trung: đổi số sân nguyên tử, liên hệ, giá từng sân/ngày và giá đặc biệt, profile | PASS 3/3, 0 skip trên DB tạm | `.local/partner-followup-db-verified.log` |
| Web build ba cổng | PASS | `.local/partner-followup-web-final-build.log` |
| Fixture hồ sơ/ẩn mã thông báo desktop và mobile | PASS 4/4 | `.local/partner-followup-profile-pass.log` |
| Toàn bộ fixture browser | PASS 332, SKIP 8 live-only, FAIL 0; desktop/mobile | `.local/partner-followup-web-all.log` |
| Live browser → API → Worker → PostgreSQL/PostGIS → Mailpit | PASS 8/8, 0 skip; DB tạm đã xóa | `.local/partner-followup-live-final.log` |

Lượt live đầu 6/8 do assertion cũ tìm mã đơn trong thông báo Partner. UI đã ẩn mã theo yêu cầu; assertion được thay bằng kiểm tra nội dung không chứa mã và click vẫn dẫn đúng booking. Lượt live cuối 8/8. Lượt fixture hồ sơ đầu có dữ liệu danh sách cơ sở sai contract làm trang Customer lỗi; fixture đã sửa và recheck 4/4. Không tính các lượt lỗi này là bằng chứng PASS.

## Giới hạn và việc cần kiểm định tay

- Giá ở database phát triển của người dùng chưa được đọc trực tiếp; API local cổng 5080 hiện không chạy. Ảnh Partner đang mở Thứ hai, còn Customer mở 28/10 (Thứ tư), nên riêng hai ảnh đó chưa chứng minh một giá bị lệch. API trên DB tạm xác nhận giá theo từng sân/ngày; trường hợp 40.000/70.000 so với 100.000 còn có thể do quy tắc giá đặc biệt cũ. Customer nay đánh dấu `*` ở ca dùng giá đặc biệt; Partner cảnh báo khi có nhiều quy tắc cũ, chỉ thay/xóa khi Owner chủ động. Đối chiếu đúng cơ sở, sân, ngày, giờ theo [checklist](Partner-followup-manual.md).
- Migration `AccountDisplayName` đã chạy trên DB tạm của test/live; database phát triển hiện tại chưa được thay đổi trong lượt này. Cần backup và chạy migration local trước khi thử profile trên dữ liệu phát triển, rồi khởi động lại API.
- Chưa commit/push/merge/deploy; CI trên working tree hiện tại và AWS production **NOT RUN**. F09 production gate vẫn độc lập.
