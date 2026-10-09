# F09 — kết quả diễn tập local 2026-10-10

Phạm vi: PostgreSQL/PostGIS và Mailpit Docker local, API/Worker Release, ba web Vite. Chỉ dùng database thử; không dùng chuyển khoản thật. Các script một lần và log chi tiết nằm trong `.local/` (Git ignore), không ghi token, OTP, QR, payload hoặc connection string vào tài liệu này.

| Gate | Kết quả | Bằng chứng runtime |
|---|---|---|
| B01–B03 backup/restore | PASS | `scripts/Invoke-LocalBackupDrill.ps1` Docker mode: dump + 286 file media; Verify SHA-256; bản sao media sửa một byte bị Verify/RestoreDrill chặn; số booking/payment/series/allocation/evidence/media trên source và restore lần lượt 10/6/1/17/6/9; 8 upload READY có file và SHA-256 khớp. |
| B04 private QR/proof trên restore | PASS trên bản thử phụ | Tạo DB `sb_restore_20261009T174256Z_a2394edcdf3848b4857c772efcea7cb3` từ backup; tạo bốn tài khoản giả, xác minh qua Mailpit. Customer đúng đơn tải QR và proof; Owner đúng business tải proof; bytes trả về khớp SHA-256 trong DB restore. Customer khác/Owner khác nhận 404, Partner gọi QR Customer nhận 403. API thử đã dừng. |
| O03 Worker retry/restart | PASS trên bản thử phụ | Tạo một outbox `BOOKING_CREATED` có người nhận sai: Worker ghi 1 attempt và lên lịch retry. Sửa người nhận, khởi động lại: message processed, đúng 1 notification. Khởi động lần nữa: vẫn 1 notification và 1 attempt. Booking `AWAITING_OWNER_CONFIRMATION` + payment `TRANSFER_REPORTED` vẫn giữ allocation chưa release suốt ba lần chạy. Quá SLA tạo đúng 1 outbox đã xử lý, 3 thông báo Owner/Admin và 0 cặp người nhận trùng. |
| Browser → API → Worker → PostGIS/Mailpit | PASS 8/8 | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release`: Playwright desktop/mobile, F01–F07 gồm đăng ký, onboarding, booking vãng lai và kỳ cố định. Exit 0; DB tạm `shuttlebook_f014_live_*` được script kiểm đúng tên rồi drop. |
| U04 nghiệm thu thị giác/a11y toàn bộ ba cổng | NOT RUN | Có ảnh live Customer/Partner desktop/mobile để tham khảo tại `.local/m03/screenshots`; chưa thay cho kiểm 375/768/1024/1440, zoom 200%, bàn phím và NVDA bởi người dùng. |
| O04 rà log riêng tư toàn luồng | NOT RUN | Ba lần Worker drill không có dòng warning/error; chưa kiểm toàn bộ trace và log API dưới tình huống lỗi. |

Để tạo quyền thử B04, script **chỉ trên DB restore phụ** tạm vô hiệu FK trigger trong một giao dịch, đổi Customer của một booking vãng lai và các dòng proof/evidence liên quan sang tài khoản giả, rồi kiểm lại hai quan hệ khóa ngoại ghép. Lần đầu cập nhật bị FK chặn và rollback, không làm đổi DB nguồn hoặc hai bản restore đối chiếu. Phương pháp này tạo fixture kiểm quyền, không phải quy trình sửa booking vận hành.

Log Worker: `.local/f09-worker-575723939c8e46fa8cbe20738b5f82a4/`. API B04: `.local/restore-access-201e1a77c4ba42ec9ee06f95afd145cc/`. Các file này chứa dữ liệu thử riêng tư, không đưa vào Git hoặc chia sẻ công khai.

Gate còn mở: checklist [F09 UI manual](F09-ui-manual-checklist.md); nơi lưu backup vận hành, retention, RPO/RTO và người nhận cảnh báo chưa chốt. F09 tổng vẫn **NO-GO** trước deploy.
