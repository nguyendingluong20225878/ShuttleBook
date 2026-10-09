# Customer — tài khoản không sidebar, tìm sân bằng khoảng cách

Ngày 2026-10-09. Yêu cầu mới thay acceptance marker trước đó: Customer không hiển thị bản đồ; trang login/register/verify dùng header ngang, không sidebar. Người dùng xác nhận các mục recovery, pagination/read, preview và Admin của mốc1 đã PASS; không tự suy nghiệm thu tay hai thay đổi mới.

## Phạm vi và acceptance trước code

- CustomerIdentity truyền layout tài khoản vào CustomerShell; header ngang có brand/Tìm sân, nội dung giữa trang và responsive. Luồng login/register/verify/returnTo, memory session và sidebar ở workspace khác giữ contract hiện có.
- Customer search bỏ VenueMap và ô bản đồ; danh sách dùng toàn chiều rộng. Chọn cơ sở qua tên hoặc Xem lịch vẫn điều hướng SPA, giữ phiên.
- Dùng vị trí của tôi xin quyền theo thao tác người dùng, tìm trong bán kính bằng API nearby/PostGIS hiện có. Distance nearby lấy từ server; nhãn rõ khoảng cách đường thẳng từ vị trí của bạn, không phải đường đi.
- Khi tìm tên sau khi đã cấp vị trí, giữ vị trí trong memory và hiển thị khoảng cách đường thẳng ước lượng từ tọa độ venue trả về. Không thay filter/booking truth ở PostGIS; không persist vị trí lên storage/DB. Khi tìm quanh khu vực tự chọn, nhãn phân biệt khoảng cách tới khu vực với vị trí hiện tại.
- Chưa có quyền/vị trí: có hướng dẫn; không dựng khoảng cách0. Loading/error/denied giữ tìm tên hoạt động. Không gọi SDK/tile map chỉ để hiển thị danh sách.
- Hủy/ignore response tìm kiếm/load-more thuộc query cũ để không ghép nhầm cơ sở/khoảng cách. Không đổi backend/schema, giữ API/error contract.

## Kiểm thử và bàn giao

Build/typecheck Customer; browser desktop/mobile account không sidebar, overflow/skip/form; no map/SDK; geolocation success/denied, radius, distance0/1.2km, giữ vị trí sau tìm tên, danh sách→quote giữ phiên và pagination response cũ. Các mục mốc1 đã PASS chỉ giữ bằng chứng, không yêu cầu người dùng nghiệm thu lại toàn bộ.

## Kết quả mới

TypeScript/Vite3portal PASS; 60/60 affected browser desktop/mobile PASS,0skip,30,1s. Login screenshots và root visual review đạt; nghiệm thu tay bản sửa mới chưa nhận. Xem progress và mục2 checklist bàn giao. Không thay API/schema/migrate/commit/push.

