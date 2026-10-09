# Prompt — cải thiện UI Admin trong F07

## Mục tiêu và quyết định thiết kế

Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, đặc tả F02/F06 và `docs/prompts/partner-ui-refresh.md`. Tham khảo [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill): thiết kế theo tác vụ, semantic HTML, trạng thái tải/lỗi/trống, focus bàn phím, tương phản và responsive. Root đã đọc skill upstream và khảo sát generator; chọn dashboard phẳng nền sáng theo tác vụ vận hành, giữ teal của partner. Không áp dụng hero marketing, font monospace hoặc số liệu minh họa.

Cải thiện cổng Admin 5175 thành workspace có Tổng quan, Hồ sơ chờ duyệt và Thông báo. Bố cục sidebar trên desktop, menu thu gọn trên mobile; nội dung hồ sơ, thông tin nhận tiền, giờ/giá và quyết định dễ đọc. Tổng quan chỉ hiển thị số hồ sơ từ response danh sách và số thông báo chưa đọc từ API. API hồ sơ giới hạn 100 dòng nên caption phải nói rõ “Trong danh sách đã tải”, không giả là tổng toàn hệ thống. Khi chưa tải được phải hiển thị dấu chờ/lỗi, không giả số 0.

## Phạm vi và contract

- Chỉ sửa `apps/admin-web/src/**`, thêm `tests/web/f07-admin-ui.spec.ts` và prompt này. Không đổi database/API hoặc quyền.
- Giữ cơ chế đăng nhập Admin: token trong memory, restore bằng cookie HttpOnly sau F5, single-flight ban đầu, hết hạn sau 30 phút kể từ thao tác thật cuối cùng. Shared restore promise chứa session đã parse, không parse cùng Response hai lần trong React development StrictMode. Không dùng polling/điều hướng tự động/focus để kéo dài phiên: authenticated request cập nhật activity ở server nên thông báo chỉ tải khi mount hoặc người dùng bấm làm mới/đánh dấu đọc. Logout và response gia hạn đến muộn vẫn không khôi phục phiên đã đóng.
- Điều hướng hash `#overview`, `#approvals`, `#notifications` có Back/Forward và deep link sau login. Chỉ mount một approval component và một notification component trong phiên; ẩn panel ngoài trang để giữ draft khi chuyển mục. Thông báo lấy một nguồn duy nhất, không hiển thị trùng trong hồ sơ.
- Giữ endpoint, bearer và DTO hiện có cho list/detail/approve/request-changes/upload view/notification read. Lý do yêu cầu chỉnh sửa tối thiểu 10 ký tự như hiện tại. Backend tiếp tục quyết định quyền và trạng thái.
- Admin chỉ đọc cảnh báo quá hạn đối chiếu; không có xác nhận tiền, bằng chứng khách hoặc thao tác booking của owner. Ảnh hồ sơ/QR dùng endpoint private hiện có; blob URL được dọn khi đổi hồ sơ, token hoặc unmount.
- Tải lỗi mạng/5xx giữ dữ liệu trước đó, có nút thử lại và `role=alert`; không hiển thị trạng thái trống trong khi chưa tải thành công. 401/403 phải xóa dữ liệu và ảnh private đã tải. Token đổi phải thoát trạng thái tải chi tiết bị abort, không để danh sách bị khóa; generation chặn response cũ ghi đè cache mới. Nút đang xử lý disable để tránh gửi lặp. Không log token hoặc dữ liệu bí mật.

## Cấu trúc và UI

- Tách `components/AdminIcon.tsx`, `layouts/AdminShell.tsx`, `features/workspace/AdminWorkspace.tsx`, `features/workspace/navigation.ts`; tái dùng `AdminApprovals.tsx` và `AdminNotifications.tsx`.
- Màu primary `#0F766E`, nền `#F3F6F6`, surface trắng, text `#172B2A`, muted `#536665`; trạng thái có chữ đi kèm màu. Font system hỗ trợ tiếng Việt, body 16px/line-height 1.5, control tối thiểu 44px, card bo 16px. Icon SVG đồng nhất, không emoji.
- Có skip link, landmark/nav label, `aria-current`, focus-visible. Menu mobile đóng bằng Escape và trả focus về nút mở; thao tác form có label tường minh, giữ text khi chuyển mục.
- Responsive 375/768/1024/1440px, không overflow toàn trang. Danh sách hồ sơ là card với tên/kind/ngày, chi tiết dùng nhóm thông tin và danh sách giờ/giá; không ép bảng rộng trên điện thoại. Tôn trọng prefers-reduced-motion.

## Acceptance và kiểm thử

1. Login/restore/logout/rate limit và 30 phút inactivity vẫn đạt regression identity/F06; token không nằm trong storage/URL.
2. Navigation desktop/mobile, Escape/focus, Back/Forward và deep link hoạt động. Default Tổng quan vẫn hiển thị các hồ sơ/cảnh báo thực tế.
3. Counter phản ánh response và cập nhật sau quyết định/đánh dấu đã đọc; lỗi API không bị hiển thị như danh sách rỗng. Không fetch thông báo trùng từ approval component.
4. Detail/ảnh/QR và approve/request-changes giữ đúng payload/quyền; draft lý do không mất khi chuyển mục. Cảnh báo không có owner payment action hoặc proof.
5. Typecheck/build Admin; browser regression hiện có và suite mới cho navigation/state/accessibility/responsive, xem screenshot desktop/mobile. Browser mock chỉ là bằng chứng UI; PostGIS/live do root kiểm thử riêng.
6. Ghi rõ PASS/FAIL/NOT RUN và lệnh trong kết quả bàn giao; không commit/push/migrate DB development hoặc tự đánh dấu toàn F07 DONE.

## Bằng chứng triển khai phần Admin — 2026-10-07

- PASS: `npm.cmd run typecheck --workspace @shuttlebook/admin-web` và `npm.cmd run build --workspace @shuttlebook/admin-web` (Vite 21 modules, không lỗi).
- PASS runtime development StrictMode: PowerShell đặt `$env:PLAYWRIGHT_BROWSERS_PATH = (Join-Path (Get-Location) '.tools/playwright')`, rồi `node .local/admin-strict-restore.mjs`. Helper local mở Vite development riêng ở 5185, Playwright Chromium, mock một session restore hợp lệ; kết quả một request restore, mở đúng deep link `#approvals`, không page error và không token trong storage. Đã dừng đúng process do helper tạo. Đây là bằng chứng UI/auth frontend với API fixture, không phải nghiệm thu cookie/backend thật.
- Suite mới `tests/web/f07-admin-ui.spec.ts`: counters/nguồn thông báo duy nhất, deep link/skip/Back/Forward/draft/decision payload, retry, responsive và menu Escape/focus, idle không kéo dài bởi focus/hash, cache 401/403, refresh giữa lúc tải chi tiết. Root chạy regression kết hợp và cập nhật kết quả cuối ở progress/testcase tổng.
