# Prompt — cải thiện giao diện partner trước F06

## Bối cảnh và nguồn tham khảo

Đọc AGENTS.md, process/progress, F01.3, F02, F03, F04/F05 và testcase hiện có. Tham khảo [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) và checklist của skill: chọn thiết kế theo tác vụ, màu có tương phản, semantic HTML, focus, responsive, loading/error/empty states. Không cài skill toàn cục hoặc chạy installer.

Đã chạy generator với `sports facility management dashboard`: trả thiết kế marketing thể thao, không phù hợp cổng vận hành. Thử lại `SaaS operations dashboard`: vẫn gợi ý hero/landing và glassmorphism. Đây không phải kết quả khớp hoàn toàn. Quyết định riêng của dự án: dashboard nền sáng, sidebar, card phẳng, teal đậm, chữ hệ thống hỗ trợ tiếng Việt; ưu tiên biểu mẫu vận hành và trạng thái thật. Không áp dụng hero, kính mờ hoặc số liệu giả. React stack search khuyến nghị semantic HTML và state cục bộ.

## Mục tiêu và phạm vi

Triển khai ở `apps/partner-web` (5174). Cải thiện cả đăng ký/xác minh/đăng nhập và workspace chủ sân. Tách Tổng quan, Hồ sơ doanh nghiệp, Cơ sở, Sân, Lịch & giá, Ảnh cơ sở, Thanh toán & QR, Thông báo. Sidebar desktop; menu mở/đóng trên mobile với bàn phím, Escape, focus trở về nút mở. Hash URL hỗ trợ Back/Forward và deep link sau đăng nhập. Có skip link, tiêu đề từng trang, active navigation.

Tổng quan lấy số cơ sở/sân/cấu hình từ response thật và hướng dẫn bước tiếp theo. Hồ sơ nháp có checklist ảnh, QR, sân và lịch/giá trước gửi duyệt. Active có liên kết vận hành; pending/changes-requested hiển thị đúng trạng thái/lý do. Giữ đầy đủ sửa nháp, tạo cơ sở bằng MapTiler, sân, giờ/giá, upload, gửi duyệt, revision địa chỉ/QR, booking policy, lịch tuần, giá ưu tiên, preview và bảo trì. Không thêm luồng F06, biểu đồ doanh thu hoặc quản lý đơn chưa có API.

## Thiết kế và cấu trúc

- CSS semantic tokens: primary #0F766E, primary dark #115E59, nền #F3F6F6, surface trắng, text #172B2A, muted #536665; status có chữ kèm màu. Chữ nội dung 16px, line-height 1.5; input/button tối thiểu 44px; spacing 8/12/16/24/32, card bo 16px.
- Layout: sidebar khoảng 248px, content tối đa 1280px, form nhiều cột khi đủ chỗ và một cột trên điện thoại. Tránh overflow toàn trang ở 375/768/1024/1440px; danh sách giờ/giá xuống dòng. SVG icons nhất quán, không emoji; không cần ảnh stock, font CDN hoặc dependency mới.
- `layouts/PartnerShell.tsx`, `components/PartnerIcon.tsx`, `features/workspace/navigation.ts`, `features/workspace/PartnerOverview.tsx`, `assets/partner.css`. Tái dùng nghiệp vụ trong PartnerOnboarding/PartnerOperations; không di chuyển hàng loạt file ngoài phạm vi.
- Khi đổi mục giữ giá trị form trong phiên; khi đổi business xóa lựa chọn sân/cơ sở và dữ liệu form tạm của business cũ. Các picker bản đồ chỉ khởi tạo khi trang được mở và resize khi hiện lại. ID label của nhiều picker phải duy nhất.
- Loading khi tải hồ sơ/cấu hình, lỗi có nút thử lại, nút submit disable khi đang gửi; trạng thái upload nói rõ đã lưu/chưa có. Thông báo đọc/chưa đọc, xử lý lỗi và tải lại.

## Contract dữ liệu/API/error

Không đổi schema, migration hoặc API. PostgreSQL/PostGIS tiếp tục là nguồn chuẩn. Đọc business detail/list/notifications như hiện tại. Mutation giữ endpoint/method/DTO, bearer, If-Match/version, upload presign → PUT → complete → attach. Giữ refresh single-flight và token chỉ trong memory, không lưu vào localStorage/sessionStorage/URL. 401 refresh thất bại quay login; 403/404/409/validation/incomplete-profile hiển thị lỗi backend, không tự bypass duyệt hoặc scope. Pending không có form chỉnh sửa, revision pending giữ thông tin đã publish, backend vẫn quyết định quyền/gửi duyệt. Không gửi secrets trong log/test/screenshot.

## Acceptance criteria / kiểm thử trước bàn giao

1. Đủ các tác vụ F01–F03 ở đúng mục, API payload và giới hạn quyền không đổi; counters/checklist phản ánh dữ liệu API.
2. Desktop/mobile không chồng nhãn hoặc overflow; navigation rõ, menu Escape/focus/keyboard/Back/Forward hoạt động. Deep link không mở dữ liệu khi chưa login.
3. Giữ form khi đổi mục; đổi business không mang ID/input/QR tạm sang scope mới. Pending/revision pending không có mutation UI; thông báo đọc được và có retry khi lỗi.
4. MapTiler xác nhận tọa độ và nhiều label độc lập; ảnh/QR vẫn dùng kho private hiện có, không thêm S3 thật.
5. Build/typecheck, regression browser mock cho identity/F02 và UI mới; luồng browser → API/PostGIS/Mailpit/Worker thật ở DB tạm. Không dùng mock làm bằng chứng integration. Review screenshot desktop/mobile và các viewport nêu trên.
6. Ghi PASS/FAIL/NOT RUN/BLOCKED, lệnh và hạn chế; cập nhật progress, hướng dẫn test tay. Không tự chốt DONE F05, triển khai F06, commit/push hoặc migrate DB development.

Thực hiện tuần tự: chốt prompt/testcase → component/layout/CSS → nối lại form → kiểm thử → sửa lỗi → review ảnh và contract → tài liệu bàn giao.
