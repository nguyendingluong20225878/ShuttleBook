# M03 — kết quả code và kiểm thử local

Ngày 2026-10-09. Người dùng duyệt Customer/Partner idle 30 phút, absolute 30 ngày từ đăng nhập; owner vận hành và F08 DEFERRED. Không triển khai staff/S3/production.

## Bổ sung nghiệm thu người dùng — 2026-10-09

Người dùng xác nhận đã chạy typecheck, build web, build backend --no-restore, test:api và test:db đều PASS; test tay các trường hợp cũng PASS. M03 DONE local theo xác nhận này và gate agent bên dưới. Không có log/count/mã testcase tay mới từ người dùng nên không gán số lượng hoặc giả lập biên bản. Các dòng chờ nghiệm thu dưới đây là trạng thái lịch sử trước xác nhận. Thay đổi mới về trang chi tiết Partner theo [đặc tả riêng](../features/Partner-booking-detail-route.md), không suy nghiệm thu tay bản mới từ xác nhận bản cũ.

## Phần hoàn tất

- Browser API login/restore/activity/logout theo portal; cookie HttpOnly/Strict/path riêng, exact Origin + JSON, no-store, không trả refresh token trong JSON.
- Access chỉ ở memory, restore trước khi hiển thị trang riêng, retry khi mất mạng. Activity từ chuột/bàn phím/wheel/cảm ứng, có trailing flush; polling và scroll do layout không tăng idle. Deadline 30 ngày giữ từ login.
- Legacy refresh/logout và browser restore/activity dùng chung family advisory lock; logout token đã consumed thu hồi replacement. JWT kiểm fresh family/status/type/idle. Guard epoch sau đọc JSON login và response muộn; logout đa tab và đổi tài khoản dọn dữ liệu phiên cũ.
- Partner restore cập nhật view=session; logout về login. Customer returnTo nội bộ giữ đúng URL. Quote/create intent vẫn memory, không tăng thời gian giữ sân.
- Không thêm migration, dùng LastActivityAt đã có. Live dùng Release/API5081/DB tạm vì Debug executable của API người dùng đang chạy bị khóa. Không dừng API5080/dev server của người dùng.

## Gate thực tế

| Gate | Kết quả | Bằng chứng Git ignored |
|---|---|---|
| TypeScript workspace ban đầu | PASS, 6,7s | Console; 3 web + package ui |
| TypeScript/Vite ba web cuối | PASS | .local/m03/web-build.log |
| Backend Release cuối | PASS, 0 warning/error, 26,57s | .local/m03/backend-final-build.log |
| API regression | PASS 96/96, 9s | .local/m03/backend-api.log |
| PostGIS focused ban đầu | PASS 16/16, 6m57s | .local/m03/backend-db-focused.log; 5 M03 + Identity1 + Admin10 |
| PostGIS rộng | 105 PASS / 1 FAIL / 0 SKIP, 106 ca, 20m4s | .local/m03/db-final.log và db-final.trx |
| PostGIS recheck cuối | PASS 5/5, 17s | .local/m03/db-recheck.log và db-recheck.trx |
| Browser full | 244 PASS / 8 SKIP / 0 FAIL, 2,6m; M03 32/32 | .local/m03/browser-final.log |
| Browser affected sau sửa transition/message | PASS 44/44, 23s | .local/m03/browser-transition-final.log |
| Live browser → API/PostGIS/Worker/Mailpit cuối | PASS 8/8, 58,9s | .local/m03/live-accepted.log |

### DB fail/recheck

Một ca Browser_parallel_restore_activity_and_logout… timeout 5s ở Fixture.Create/control.OpenAsync, trước CREATE DATABASE và logic/assertion. Fixture chuyển localhost thành127.0.0.1 sau kiểm tra target local, timeout kết nối15s và dispose khi mở thất bại. Không đổi command timeout, barrier, assertions hoặc application code.

Rebuild Release và recheck cả5 ca M03 PASS, gồm đúng ca bị lỗi. Root đối chiếu TRX: **106 testName duy nhất có PASS qua full + recheck**; không gọi một lượt full106/106PASS và không cộng4 ca lặp thành110.

### Các lỗi đã sửa

- Focused đầu23PASS/3FAIL: activity do layout scroll và fixture đổi tài khoản. Dùng wheel/touch input, kiểm account replacement với GET cũ có delay.
- Browser full đầu236PASS/2FAIL/8SKIP: test gửi Tab trước startup restore xong. Chờ form mount, giữ assertions skiplink/Enter/focus.
- Recheck36PASS/2FAIL: message SESSION_CHANGED cần hiển thị đúng. Ba test có delay login body/restore/activity đều đạt ở gate cuối.
- Live đầu8PASS. Khi thêm Partner F5, hai lượt có6PASS/2FAIL: giả định contact form còn sau reload, Partner restore chưa đồng bộ view, thao tác chưa chờ logout/resend HTTP hoàn tất. Đã sửa source transition, điền Email rõ ràng và waiter204/200/202; budget SMTP120s. Live cuối8PASS bao gồm F5 Customer/Partner, casual/fixed, private proof/report/confirm.

## Review, ảnh và giới hạn

- Review độc lập đóng, không còn finding P0/P1 đã biết trong scope M03. Review tìm các race epoch/cookie/activity đã sửa; kết quả review không thay test runtime.
- Root xem ảnh Partner F5 mobile và Customer F5 mobile. PNG cả desktop/mobile lưu ở .local/m03/screenshots/customer-f5-*.png và partner-f5-*.png. QR/biên lai TEST không phải chuyển tiền thật.
- Request đã xác thực trước logout/hết idle có thể hoàn tất transaction đã nhận hoặc chờ khóa. Logout chặn auth/restore mới, không cam kết rollback mọi inflight write; frontend bỏ response muộn.
- Live DB đã drop đúng tên, normal web build phục hồi, preview/API5081/Worker test đóng. API5080 và dev web IPv6 của người dùng vẫn giữ. **Restart API khi nghiệm thu tay bản mới**.
- Chưa có biên bản nghiệm thu tay mới trên máy/thiết bị người dùng. Mốc2 auth/search/quyền có gate trong scope; casual amount cực lớn/quota toàn tài khoản còn quyết định/code/gate riêng. CI hosted/S3/provider/backup restore/load/security/pilot NOT RUN. F08 hoãn, không DONE.
- Không commit/push/deploy/reset/migrate development. Handoff: [M03 manual](M03-browser-sessions-manual.md), [mốc2–5](M02-M05-acceptance-guide.md).
