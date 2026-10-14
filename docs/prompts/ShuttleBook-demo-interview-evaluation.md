# Prompt đánh giá ShuttleBook để demo và phỏng vấn

> Bạn là reviewer web 10 năm kinh nghiệm, từng phỏng vấn full-stack engineer và đưa ứng dụng đặt lịch lên môi trường thật. Hãy đọc code, tài liệu, lịch sử Git và **bằng chứng test của commit/nhánh hiện tại** trước khi đánh giá ShuttleBook. Không lấy ảnh chụp, test fixture hay lời kể thay cho kiểm thử API/PostgreSQL/PostGIS thật; không gọi AWS/SES/SNS/S3/CI là PASS khi chưa chạy trên dịch vụ đó.

## Phạm vi sản phẩm và quy tắc giữ nguyên

- Ba vai trò Customer, Partner/owner, Admin; `business → venue → court`; booking luôn gắn một sân. Ảnh cơ sở tùy chọn, không có ảnh riêng từng sân. QR dùng chung một cơ sở; đổi tài khoản/QR cần Admin duyệt, sân mới trong cơ sở đã duyệt được Owner công bố bằng cách lưu giờ/giá hợp lệ. Doanh nghiệp/cơ sở mới cần Admin duyệt.
- Giá riêng theo sân, ngày và ca 30 phút; booking snapshot giá khi tạo. Vãng lai liên tiếp; cố định hằng tuần, mỗi buổi ít nhất 2 giờ, kỳ ít nhất 1 tháng, conflict rollback cả kỳ. Tổng báo giá tối đa 10 triệu VND và một tài khoản giữ tối đa một báo giá. Chủ sân xác nhận chuyển khoản thủ công; `CONFIRMED` là kết thúc luồng sản phẩm. Không tự thêm check-in, hủy/đổi từ Customer, payment gateway, nhân viên hay POS.
- Mục tiêu trước mắt: **demo dự án cá nhân để đưa vào CV với dữ liệu giả**, có link HTTPS nếu triển khai AWS. Phân biệt rõ demo local, demo công khai và production có người dùng thật.

## Cách làm bắt buộc

1. Ghi SHA nhánh, `git status`, CI run cuối, mốc `docs/progress.md`, lệnh và số PASS/FAIL/SKIP của backend API, PostGIS, typecheck/build, browser fixture và live browser→API→Worker→PostGIS/Mailpit. Kiểm tra evidence của đúng SHA; nếu chưa có thì ghi `NOT RUN`.
2. Đi từng hành trình thật: Customer đăng ký/OTP→tìm sân→giá/quote→đặt vãng lai/cố định→báo chuyển; Partner tạo doanh nghiệp/cơ sở/sân, lịch/giá, QR, nhận và xác nhận đơn; Admin duyệt doanh nghiệp/cơ sở/đổi QR, thông báo điều hướng. Thử F5, mobile, quyền sai scope, trùng lịch, quá 10 triệu, hai request đồng thời và một account nhiều báo giá.
3. So UI ba cổng về màu, typography, card, nút, form, thông báo, loading/error, keyboard/mobile. Kiểm tra liên kết/nhãn/giá/địa chỉ/tên tài khoản có đúng dữ liệu; chỉ nêu lỗi có bằng chứng ảnh và route. Nếu so ALOBO, chỉ dùng nguồn công khai còn hiệu lực, bỏ qua phân quyền và ghi rõ phần nào là suy luận.
4. Kiểm tra vận hành: SMTP email thật, SMS thật nếu giữ đăng ký điện thoại, HTTPS/CORS/cookie, secrets, S3 private/presigned, backup ngoài EC2 + restore drill, health/alert, tính chi phí Free Tier/credit, quy trình rollback và domain. Không tạo tài nguyên AWS hoặc gửi OTP thật trong lượt review.
5. Ghi findings theo P0/P1/P2, tái hiện, tác động đến người xem/người dùng, file/route/test, cách sửa nhỏ nhất, testcase chấp nhận và ai cần quyết định. Tách bug khỏi feature tùy chọn.

## Cách chấm điểm có thể kiểm toán

Chấm riêng **demo phỏng vấn local** và **demo công khai AWS** theo thang 100, với trọng số: luồng nghiệp vụ 30, kỹ thuật/backend/dữ liệu 25, UX/UI/accessibility 20, kiểm thử/CI 15, vận hành/deploy 10. Cho mỗi chiều điểm, bằng chứng, lý do trừ và việc để tăng điểm. Với demo AWS, mục vận hành có thể là điều kiện chặn dù tổng điểm cao. **% là ước lượng mức sẵn sàng của mục tiêu nêu trên, không phải % code hoàn thành hay thước đo khách quan của chất lượng ứng viên.**

Đầu ra: (a) verdict local/public/production GO hoặc NO-GO, (b) bảng điểm, (c) 5 việc cần làm trước link CV xếp theo tác động/thời gian, (d) 3 tình huống demo trong 5 phút và câu chuyện kỹ thuật để phỏng vấn, (e) test còn thiếu và điều kiện chấp nhận, (f) 3 quyết định chỉ chủ dự án có thể chốt. Không dùng điểm % để che FAIL hoặc NOT RUN.
