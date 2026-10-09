# Prompt — audit ShuttleBook đến từng nút, đối chiếu ALOBO

Ngày tạo: 2026-10-08. Thực thi theo yêu cầu người dùng: viết prompt và chạy audit; F07 đã DONE local. Vai trò: kỹ sư full-stack/UX reviewer có 10 năm kinh nghiệm. Không tự tuyên bố đã vận hành ALOBO hay tự sửa feature chỉ vì phát hiện gap.

## Mục tiêu

Đánh giá cụ thể từng màn hình và control hiện có của Customer/Guest, Partner và Admin: điều tốt, điều chưa tốt, hành động khi bấm, điều kiện hiển thị/disable, loading/error/success, quyền và bước tiếp. So sánh với ALOBO bằng source hoặc màn hình công khai quan sát được. Đưa backlog rõ và chỉ ra testcase agent tự kiểm/bạn nghiệm thu tay.

## Đọc và kiểm trước

Đọc AGENTS.md, docs/process.md, docs/progress.md, docs/reviews/ShuttleBook-product-readiness.md, đặc tả F01–F07 và testcase UI tương ứng. Kiểm Git status và mã nguồn hiện tại; không reset, stage, commit/push. Đọc tất cả TSX ba apps, routes/session/API contracts liên quan; dùng rg trước khi tìm. Giữ bất biến business→venue→court, ca30 phút/snapshot, quote TTL/fixed all-or-none, report/owner-confirm/outbox, CONFIRMED kết thúc, customer không cancel/change và không check-in/no-show.

## Đầu ra trước khi kết luận

1. Danh mục màn hình và control theo role, bao phủ cả trạng thái Guest/login/pending owner/active owner/pending approval/review/confirmed/empty/error/expired.
2. Mỗi control logic có ID ổn định. Bao gồm button, link hành động, tab/toggle, selector, file upload, menu/icon và các button động. Không tính mỗi record/ô giờ/ngày lặp thành control riêng: ghi family và tham số, liệt kê hành động khác nhau.
3. Mỗi dòng: screen, nhãn chính xác, type, action/destination/API, visible/disabled/loading/error, điều tốt, gap + severity, file:line, evidence status, test tự động đề xuất và test tay.
4. Đánh giá đến từng nút, không chỉ nói toàn trang đẹp/xấu. Đối chiếu checklist inventory với mọi `<button>`, `<a>`, input submit/file, select và role button/tab; ghi custom/library controls chưa quan sát được.

## Chuẩn so sánh ALOBO

Truy cập nguồn chính thức, ghi URL/ngày và phân biệt lời giới thiệu, guide, screenshot và trải nghiệm tương tác trực tiếp:

- https://www.alobo.vn/
- https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/
- https://wiki.alobo.vn/article/alobo-2103-cap-nhat-phien-ban-alobo-quan-ly-alobo-dat-lich/
- Chỉ mục hướng dẫn và bài hướng dẫn đặt lịch/xác nhận/giá/phân quyền nếu truy cập được.

Có thể xem app/web công khai bằng browser. Không đăng ký/mượn account, không gửi OTP/tin nhắn, tạo booking hay giao dịch trên ALOBO. Với màn authenticated không được cấp account: UNKNOWN/NOT OBSERVED, không đoán mọi label và không bịa click path. Screenshot có thể hỗ trợ bố cục/control visible nhưng không chứng minh loading/rights/backend.

Đặt mã nguồn A01... cho guide/screenshot chính thức và cite gần claim; không trích toàn bài. Mỗi so sánh có label:

- GOOD: điểm ShuttleBook làm tốt có bằng chứng của chính nó, chưa đủ để nói hơn ALOBO.
- BETTER-EVIDENCED: chỉ dùng khi cùng task có evidence đủ ở hai bên và nêu tiêu chí rõ.
- GAP: ShuttleBook chưa có hoặc thao tác kém thuận tiện so với capability ALOBO được source xác nhận.
- PARITY-PARTIAL: có năng lực tương tự nhưng khác policy/phạm vi, không suy hai backend giống nhau.
- UNKNOWN: chưa quan sát phía ALOBO; không kết luận ShuttleBook tốt/xấu hơn.

Không gán thế mạnh chống trùng/TTL/privacy của ShuttleBook thành “ALOBO không có”. Không coi thiếu chat/POS/native/check-in là bug booking core; đề xuất theo nhu cầu và bất biến. Admin duyệt/scope nội bộ ALOBO chưa truy cập được phải UNKNOWN.

## Kiểm thực tế và evidence

- Khi có điều kiện chạy browser trên build current/DB test riêng hoặc fixture hiện có. Không chạm dữ liệu thật/development database. Không phát sinh S3 trả phí/deploy.
- Có thể chạy các Playwright testcase có sẵn liên quan đúng audit, không viết test chỉ mirror code; giữ port/profile/process riêng và dọn helper của agent.
- Browser fixture chỉ chứng minh UI/action; DB transaction dùng evidence PostGIS thật đã ghi, không thay bằng fixture. Lượt mới và evidence ngày trước phải tách.
- Duyệt desktop/mobile, focus/keyboard, label/touch targets, scroll và loading/error/empty. Xem screenshot thật khi chấm hình học/style. Không chấm contrast/zoom/screen-reader đã PASS nếu chưa đo.
- Nếu apps/credentials/provider unavailable: tiếp tục source/test artifact audit, ghi NOT RUN cho live; không dừng toàn bộ hoặc báo đã test mọi button.
- Không in token/OTP/secrets/PII. Chỉ chụp fixture/synthetic data; không đọc .env ra output.

## Chạy song song có kiểm soát

AGENTS cho phép phân công độc lập: reviewer Customer, reviewer Partner, reviewer Admin/benchmark. Root thống nhất schema inventory trước khi giao; child chỉ viết artifact phạm vi riêng trong .local/ui-button-audit, không sửa source hoặc tài liệu chung. Root tích hợp, kiểm coverage và kết luận.

## Các artifact phải hoàn thành

- `docs/prompts/ShuttleBook-ui-button-audit-alobo.md`: prompt này và ghi nhận thực thi.
- `docs/reviews/ShuttleBook-ui-button-audit-alobo.md`: kết luận, phương pháp/giới hạn, role/screen summary, so sánh ALOBO có nguồn, bảng control từng role và backlog P1/P2 có test/gate.
- `docs/testing/ShuttleBook-ui-button-acceptance.md`: checklist theo button/luồng: tiền điều kiện, thao tác, expected, agent-automated vs user-manual, PASS/FAIL/NOT RUN đúng evidence. Không tự gán tất cả PASS từ source.
- Evidence screenshots/log/inventory trong `.local/ui-button-audit/` và kết nối với artifacts cũ khi dùng.
- `docs/progress.md`: mốc audit hoàn tất, findings, bằng chứng/lệnh, limitations và bước tiếp; giữ F07 DONE theo phạm vi đã nghiệm thu.

## Báo cáo và chốt

Nêu 5–10 điểm tốt rõ và 5–10 gaps ưu tiên có control ID, kịch bản, ảnh hưởng, fix đề xuất, testcase và effort S/M/L. Với mỗi so sánh nêu evidence ALOBO hoặc UNKNOWN; các nút chưa có phải nằm bảng “missing capabilities”, không bịa control đang tồn tại.

Final tiếng Việt tóm tắt điều tốt/gaps đáng xử lý, link prompt/report/checklist; nói rõ phạm vi đã kiểm source/browser/live và chưa kiểm. Không tự code backlog, commit/push, migrate DB thật hoặc triển khai production trong nhiệm vụ audit.

## Ghi nhận thực thi — 2026-10-09

Đã chạy audit08–09/10/2026: ba reviewer theo role và root tổng hợp220 record,218 control/family có source +2 SDK chưa quan sát. Build ba app PASS; browser158 PASS/8 SKIP; probe C53 tái hiện giới hạn retry UI sauTTL, commit DB thật NOT RUN. Kết quả: [báo cáo](../reviews/ShuttleBook-ui-button-audit-alobo.md) và [checklist](../testing/ShuttleBook-ui-button-acceptance.md). F07 giữ DONE local. Lượt này hoàn thành đánh giá và kế hoạch nghiệm thu, chưa thực hiện các sửa/backlog đề xuất.
