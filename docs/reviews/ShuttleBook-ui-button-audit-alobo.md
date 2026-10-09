# ShuttleBook — audit từng control, đối chiếu ALOBO

Cập nhật sau audit (09/10): đã triển khai năm nhóm UI/recovery được chọn; xem [kết quả mới](../testing/UI-post-audit-improvements-results.md) và [nghiệm thu tay](../testing/UI-post-audit-improvements-manual.md). Các finding/số liệu bên dưới là ảnh chụp hiện trạng trước đợt sửa, không phải danh sách lỗi còn mở đã được cập nhật toàn bộ.

Thực hiện: **08–09/10/2026**, Asia/Saigon. Prompt đã chạy: [ShuttleBook-ui-button-audit-alobo](../prompts/ShuttleBook-ui-button-audit-alobo.md). Đánh giá hiện trạng và đề xuất; chưa triển khai backlog. [Đánh giá sản phẩm trước đó](ShuttleBook-product-readiness.md) vẫn là tài liệu về DB, vận hành và roadmap.

## 1. Kết luận

**ShuttleBook có nền tảng đặt sân local tốt và UI ba vai trò đã thống nhất; ALOBO có công cụ vận hành và lối đi trên mobile phong phú hơn.** Chưa đủ bằng chứng để tuyên bố toàn bộ ShuttleBook tốt hơn ALOBO. Với tiêu chí rất cụ thể, ShuttleBook đưa Guest thẳng vào tìm sân; phiên mới ALOBO quan sát có màn giới thiệu với lựa chọn bỏ qua. Đây chỉ là khác biệt bước vào luồng, không phải đo tỷ lệ chuyển đổi.

Các thế mạnh đã có bằng chứng của ShuttleBook: lịch tất cả sân cùng cơ sở, giá từng ca, countdown giữ chỗ, cố định toàn kỳ, thanh toán có đối chiếu, private media, scope backend và Admin không xác nhận tiền. Những điều này là **GOOD**; không suy thành ALOBO thiếu chúng khi chưa được truy cập backend của họ.

Khoảng trống rõ nhất để dùng hằng ngày: khôi phục phiên Customer/Partner, retry sau lỗi mạng, lịch sử không bị polling làm mất trang, thao tác nhập giá hàng loạt, tìm hồ sơ Admin, nút trợ giúp/auth, phân quyền nhân viên và báo cáo. Đề xuất theo rủi ro và tần suất sử dụng; chưa thêm tính năng ngoài quy tắc nghiệp vụ hiện tại.

**F07 giữ DONE local** theo nghiệm thu trước. Audit mới ghi rủi ro/recovery và backlog sản phẩm; không tự coi mọi đề xuất là lỗi nghiệm thu core hoặc tự thay đổi policy.

## 2. Bằng chứng và cách đọc

| Hạng mục | Kết quả mới | Giới hạn |
|---|---|---|
| Build ba portal | **PASS**, `scripts/Build-Web.ps1` → TypeScript + Vite cả ba app | Không build lại backend trong lượt audit UI |
| Browser regression hiện tại | **158 PASS / 8 SKIP**, desktop + Pixel 7, 1,2 phút; `scripts/Test-Web.ps1 --workers=2` | UI fixture; 8 ca live có chủ đích SKIP, không tính PASS |
| Probe recovery casual | **REPRODUCED UI FIXTURE**: POST bị abort → tiến clock121s → nút create disabled, chỉ1 POST; link danh sách đơn còn | Không mô phỏng commit DB thật; chưa kết luận mất đơn/đặt trùng |
| Source inventory | Ba reviewer theo vai trò + root đối chiếu nhãn/source/test/screenshot | Source không thay runtime; các testcase đề xuất vẫn NOT RUN nếu chưa có evidence riêng |
| ALOBO public browser | Đã xem onboarding, Guest chrome sau bỏ qua và trang đăng nhập chủ sân; desktop1440×1000/mobile390×844 | Guest còn spinner dữ liệu khi chụp; không chấm hiệu năng. Flutter DOM không expose đủ labels nên không đếm1 DOM control thành1 nút thật |
| ALOBO nghiệp vụ | Đã đọc hướng dẫn chính thức cập nhật năm2026 | Hướng dẫn không chứng minh concurrency, storage, mọi loading/error hoặc quyền thực tế |
| API/PostGIS/Worker/payment live | **NOT RUN mới** | Evidence local trước đó được dẫn trong product review/F07 results, không đổi tên thành test mới |
| NVDA, contrast đo bằng công cụ, zoom200%, Safari/iOS, MapTiler/S3 thật, ngân hàng, pilot | **NOT RUN** | Không suy PASS từ ảnh hoặc Chromium fixture |

Log mới: `.local/ui-button-audit/build.log`, `browser.log`, `browser-exit.txt`; probe: `casual-recovery-after-ttl.json/png`. Build đầu đã đạt nhưng wrapper test đầu dừng do PowerShell coi cảnh báo stderr là lỗi; đã chỉnh **helper local** và chạy lại toàn suite đạt. Không sửa source để làm test pass. Browser/helper do audit tạo đã được đóng. Inventory/ảnh/log nằm `.local`, bị Git ignore; không chứa secrets hoặc account thật.

### Mã đánh giá

- **GOOD:** ShuttleBook làm tốt và có căn cứ riêng.
- **BETTER-EVIDENCED:** lợi thế ở một tiêu chí có bằng chứng cả hai phía.
- **GAP:** thiếu hoặc kém thuận tiện so với capability ALOBO được xác nhận.
- **PARITY-PARTIAL:** có nhiệm vụ tương tự, chưa đủ để kết luận trải nghiệm/backend ngang nhau.
- **UNKNOWN:** chưa quan sát được phía ALOBO.

**P1** là ưu tiên xác minh/sửa trước pilot có giao dịch; finding chỉ từ source vẫn phải có test tái hiện. **P2** là UX/resilience hoặc mở rộng sản phẩm, không tự là bug. `NONE` nghĩa giữ cách làm hiện tại. Một control có thể có GOOD và gap cùng lúc. Đánh giá screen-reader chưa chạy là thiếu evidence, không phải kết luận thất bại.

## 3. Nguồn ALOBO và mức tiếp cận

Mã trong inventory dẫn về bảng này. Ngày truy cập:08/10/2026; ngày dưới là ngày cập nhật ghi trên bài, không phải ngày test ứng dụng.

| Mã | Nguồn chính thức | Điều quan sát có thể dùng |
|---|---|---|
| A01 | [Website](https://www.alobo.vn/) · [Wiki](https://wiki.alobo.vn/) | Giới thiệu sản phẩm, chỉ mục hướng dẫn; marketing không là runtime evidence |
| A02 | [Đặt lịch online](https://wiki.alobo.vn/article/huong-dan-dat-lich-online-va-duyet-don/) —06/04/2026 | Chọn nơi chơi/lịch, tiếp tục thông tin người chơi, thanh toán; có lựa chọn dịch vụ/voucher |
| A03 | [Duyệt đơn online](https://wiki.alobo.vn/article/huong-dan-duyet-don-dat-lich-online/) —29/05/2026 | Đối chiếu bill với tiền nhận trước quyết định; hold sau tạo đơn có hạn6 phút, màu đỏ biểu thị giữ tạm |
| A04 | [Bảng giá](https://wiki.alobo.vn/article/huong-dan-cai-bang-gia/) —22/04/2026 | Nhập theo sân/phạm vi, ngày/giờ, nhóm đối tượng; giá lịch ngày và tháng riêng |
| A05 | [Lịch tháng cố định](https://wiki.alobo.vn/article/huong-dan-dat-lich-thang-co-dinh/) —15/04/2026 | Operator thiết lập tháng và nhiều thứ, thông tin khách, tiền cọc tùy chọn |
| A06 | [Tạo và phân quyền nhân viên](https://wiki.alobo.vn/article/huong-dan-tao-va-phan-quyen-nhan-vien/) —15/05/2026 | Luồng nhân viên/chọn chi nhánh/quyền; chưa thử bằng tài khoản |
| A07 | [Khóa sân](https://wiki.alobo.vn/article/huong-dan-khoa-san/) —10/04/2026 | Theo ngày hoặc tuần, ngoại lệ; owner được tạo lịch trên sân đã khóa online |
| A08 | [Xuất báo cáo ngày](https://wiki.alobo.vn/article/huong-dan-xuat-bao-cao-ngay/) —19/05/2026 | Chọn ngày, loại báo cáo và xuất Excel |
| A09 | [Mở đặt online](https://wiki.alobo.vn/article/huong-dan-mo-dat-lich-online/) —19/05/2026 | Bật/tắt nhận online, chia sẻ link, cấu hình QR theo tiền/nội dung |
| A10 | [Bản2.10.3](https://wiki.alobo.vn/article/alobo-2103-cap-nhat-phien-ban-alobo-quan-ly-alobo-dat-lich/) —21/09/2026 | Nâng cấp chat/quyền/POS/báo cáo; không coi toàn bộ là phạm vi MVP ShuttleBook |
| A11 | [Web khách](https://datlich.alobo.vn/) · [Web chủ sân](https://app.alobo.vn/) | Screenshot fresh phiên không đăng nhập; chỉ control nhìn thấy, không bấm đăng nhập/OTP/tạo đơn |

Ảnh A11: `.local/ui-button-audit/alobo-public-manager-desktop.png`, `alobo-manager-mobile-fresh.png`, `alobo-player-mobile-fresh.png`, `alobo-player-guest-mobile.png`, `alobo-player-guest-desktop.png`. Ảnh mobile mới dùng context390×844 riêng; ảnh cũ resize cùng page chỉ giữ làm trace, không dùng kết luận geometry. Không có account nội bộ ALOBO Admin; mọi so sánh Admin authenticated là UNKNOWN.

## 4. Những điểm đang làm tốt và đối chiếu cụ thể

| Control/luồng | ShuttleBook hiện tại | So với ALOBO / kết luận |
|---|---|---|
| C03/C28/C29/C37 — tìm sân | Guest vào tìm nơi chơi, đọc lịch trước login; CTA có động từ và nơi đi | **BETTER-EVIDENCED hẹp:** một bước vào tìm kiếm ít hơn trong phiên fresh quan sát A11; ALOBO có lựa chọn bỏ qua giới thiệu, không phải bắt đăng nhập |
| C30/C34/C37 — vị trí và fallback | Từ chối geolocation/bản đồ lỗi vẫn tìm theo tên và vào lịch bằng danh sách | **GOOD**; A11 chỉ thấy chrome, không thử lỗi provider của ALOBO nên UNKNOWN về khả năng phục hồi phía họ |
| C43/C44/C47/C48 — lịch | Tất cả sân cùng bảng, chọn ca trống, nhãn accessible có giờ/trạng thái/giá; sticky/scroll được browser test | **PARITY-PARTIAL A02/A03** về lịch và giữ sân; chưa xem lịch authenticated ALOBO để chấm geometry hay hiệu năng |
| C52/C62/C63 — báo giá | Giá từng30 phút, countdown giữ tạm; toàn kỳ hiển thị buổi/xung đột, không âm thầm bỏ buổi | **GOOD**; A05 có lịch tháng nhưng không công bố giao dịch DB, nên không nói ALOBO thiếu all-or-none |
| C67–C74 / Partner quyết định | Báo chuyển và bổ sung, owner kiểm tiền/bằng chứng, mismatch sang đối chiếu; lỗi phiên bản yêu cầu đọc lại | **PARITY-PARTIAL A03**. Policy thời hạn khác nhau; quote120s của ShuttleBook không so trực tiếp với hạn sau tạo đơn của ALOBO |
| C75/A17–A19 / private proof | Media cấp quyền, cleanup khi mất scope, booking giữ snapshot | **GOOD**; không truy cập storage ALOBO nên privacy superiority UNKNOWN |
| A04/A06 — phiên Admin | F5 restore trong idle30 phút kể từ thao tác cuối, giữ draft điều hướng, không background polling kéo dài idle | **GOOD**; ALOBO checkbox nhớ đăng nhập không chứng minh TTL/storage policy tương đương |
| A20/A22/A24 — quyền Admin | Duyệt hồ sơ và cảnh báo đối chiếu; xác nhận tiền thuộc owner | **GOOD**, ALOBO Admin UNKNOWN; phân tách này phù hợp nghiệp vụ đã chốt |
| Partner lịch/giá | Form rows cho ngày/giờ/giá, giá30 phút kèm quy đổi theo giờ, preview, lỗi không giả thành success | **PARITY-PARTIAL A04**; hiện không phải JSON editor. Chưa có apply-to-many/nhóm giá nên còn gap vận hành |
| Partner khóa bảo trì | Allocation bảo trì chặn giao nhau theo dữ liệu chuẩn | **Khác policy A07:** khóa online ALOBO vẫn cho owner tạo lịch. ShuttleBook phù hợp tiêu chí không override bảo trì; không kết luận ALOBO bị double-booking |
| Shell ba portal | Palette teal, cards/form/nav tương đồng, menu mobile, trạng thái/error/empty rõ trong những ca được test | **GOOD**. A11 mobile có điều hướng dưới màn hình thuận tiện; tính đồng bộ đẹp/xấu cần người dùng chốt, chưa đo usability |

Ảnh ShuttleBook mới đã xem: Customer search desktop/mobile (geolocation denied, map fail, text dài), Partner Lịch & giá desktop/mobile, Admin overview/detail mobile. Ảnh fixture trong `test-results/`; không dùng synthetic số tiền/tên để đánh giá hoạt động thực tế của cơ sở.

## 5. Những điểm chưa tốt — xử lý theo thứ tự

| Ưu tiên / ID | Control và kịch bản | Ảnh hưởng / căn cứ | Đề xuất, kiểm chứng và effort |
|---|---|---|---|
| P1 verify —C06 / Partner logout | Refresh/logout lỗi hoặc đua nhau, rồi F5 khi online | Source-risk phiên/revoke; UI chưa đủ xác nhận cookie server. Không suy thành leak đã runtime | Barrier test API/PostGIS + browser; generation guard và chỉ báo revoke đúng kết quả nếu test xác nhận. **M** |
| P1 recovery —C39 | Customer đang login → bấm marker cơ sở | `window.location.href` reload toàn trang trong khi auth ở memory; source xác nhận, end-to-end marker chưa chạy | Dùng route SPA như card; test marker vs card giữ phiên/returnTo +keyboard. **S** |
| P1 recovery —C53 | Click create, phản hồi lỗi mạng, chờ hết quoteTTL | Probe UI mới xác nhận nút bị disable. Link Đơn của tôi còn; DB commit/lostresponse thật NOT RUN | Theo dõi intent đã gửi; cho retry cùng key sauTTL hoặc bắt buộc reconcile. Không tự tạo intent/quote mới trước kiểm đơn. API+DB lostresponse case và browser; **M** |
| P2 data view —C29/C38 | Tải trang sau rồi đổi query/radius trong lúc request cũ chậm | Source không có generation/abort cho loadMore; có khả năng lẫn kết quả | Abort+epoch/cursor scope; test delay đổi query/radius rồi trả response cũ. **S/M** |
| P2 recovery —Partner/A24 | Load thêm đơn/thông báo → polling/mark-read | Source có nhánh thay firstpage, mất cửa sổ đã tải; data DB chưa mất | Merge theo id/cursorwindow, giữ scroll; 21+/51+records, fake clock/mark-read. **M** |
| P2 money boundary —C52/C53 | Tổng casual ngoài JS safe integer | `number` có thể làm tròn; series/detail đã dùng exactstring. Chưa quan sát booking thật ở biên này | Contract amountExact thống nhất hoặc bound hợp lệ; boundary test+UIformat, giữ VND30 phút. **M** |
| P2 mobile workflow —C04/C05/C07 | Khách muốn đi nhanh lịch/đơn/inbox, login từ trang hiện tại | A11 có bottomnav/shortcut; Shuttle mobile topnav và link hiện chưa đồng đều giữreturnTo/focus | Prototype bottom3–4destinations, không nhân đôi CTA; giữreturnTo, routefocus/keyboard, usability5người. **M** |
| P2 auth usability —C17/C26/A03/Partner auth | Gõ mật khẩu dài trên điện thoại, quên mật khẩu | A11 có eye/forgot/help; Shuttle chưa có recoveryflow. Không chấm checkbox ALOBO là lưu password unsafe | Show/hide accessible **S**; password-reset API/contact/security **M/L**, không thêm nút giả khi chưa có backend |
| P2 operations —Partner giá | Nhiều sân cùng giá/giờ, thay cả tuần | Rows dễ hơn rawJSON nhưng nhập lặp nhiều; A04 có phạm vi và đối tượng giá | Copy lịch tuần/apply nhiều sân, previewdiff+atomic/scope gate; giá theo nhóm cần policy riêng. **M/L** |
| P2 admin —A14/A17/A20–A22 | >100 hồ sơ, đổi bankQR, lý do1001chars, quyết định một click | Sourcecap100, thiếusearch/cursor/old-new/confirm/max1000UI | Cursor+queryscope, diff, countermax/field errors, confirm phạm vi; test race409/twoAdmins. **M** |
| P2 thanh toán —C69/C75/Partner proof | Chuyển trên cùng điện thoại, nhập lại tiền/nội dung, đọc ảnh nhỏ | QR upload/inline; thiếu copy tiền/nội dung/previewzoom. A09 có QR điền dữ liệu | Copy buttons+feedback+privatezoom; QR động sau chốt provider/contract; không tự xác nhận tiền theo ảnh. **S/M** |

**Không cộng số dòng P1/P2 thành số bug.** Inventory có cả đề xuất, thiếu evidence a11y và rủi ro cần test; chỉ probe C53 có tái hiện UI mới trong lượt này. Các điểm chưa tái hiện không được gán FAIL transaction. Nếu focused gate xác nhận auth/data nghiêm trọng, phải sửa/xác minh trước nhận giao dịch thật.


### Điểm cụ thể cần đọc trong inventory Partner

- **P41 — Sân:** chọn sân nháp chưa nạp cấu hình hiện có vào form lịch; cần kiểm không ghi đè ngoài ý muốn khi owner sửa.
- **P64–P66 — Block/Lưu quy định:** đổi block cần hướng dẫn điều chỉnh minimum hợp lệ trước gửi, thay thông báo validation chung.
- **P82 — Xem giá:** thao tác đọc dùng runner báo “Đã lưu”; nên báo “Đã tính giá” và giữ kết quả đọc tách trạng thái mutation.
- **P83/P84 — Khóa ca bảo trì:** preview và bảo trì đang dùng chung state ngày/giờ; cần kiểm người dùng có nhận ra dữ liệu bị thay đổi và có muốn tách draft hay không.
- **P54–P56 — Gửi thay đổi để duyệt:** đổi địa chỉ/liên hệ đang nằm cùng Payments và yêu cầu nhập lại QR/tài khoản; đề xuất revision từng nhóm thông tin, có diff và giữ approval guard.
- **P104/P106 — Xác nhận tiền/Từ chối cuối:** điểm tốt là kiểm số tiền, reason và acknowledgement giải phóng toàn kỳ; cải thiện phải giữ các guard này, không chỉ đổi màu nút.

Đây là nhận xét source/UX; các tình huống focused mới vẫn NOT RUN. **S/M/L** trong bảng là độ lớn tương đối của thay đổi, không phải cam kết ngày giao.

## 6. Nút/chức năng còn thiếu so với nhu cầu vận hành

Đây là capability chưa triển khai; không đưa vào inventory như button đang tồn tại. Không đổi bất biến `CONFIRMED` là kết thúc hoặc thêm customer hủy/đổi/check-in.

| Nút/luồng cần cân nhắc | ALOBO có căn cứ | ShuttleBook / hướng đi |
|---|---|---|
| Mời nhân viên, chọn cơ sở/quyền, thu hồi | A06 | **GAP/F08**; ưu tiên least privilege, revoke phải kiểm backend/payment/read/notification |
| Báo cáo tiền sân, xuất Excel | A08 | **GAP/F09**; chỉ PAID chuẩn, theo timezone/scope, pending/review không cộng doanh thu |
| Bật/tắt nhận online, copy link cơ sở | A09 | **GAP**; giữ booking cũ, guard backend, UX pending/blocked rõ |
| Chia sẻ cơ sở / lưu yêu thích | A09/A11 | **GAP trải nghiệm**; share đơn giản trước, favorite theo nhu cầu thật |
| Áp dụng giá nhiều sân / nhóm khách / riêng lịch tháng | A04 | **GAP vận hành**; không tự thay snapshot/policy fullseries đã chốt |
| Khóa lịch lặp theo tuần + ngoại lệ | A07 | **GAP**; phải transaction all-or-none, báo conflict, không mở override bảo trì |
| Owner nhập đơn khách tới trực tiếp / lịch cố định nhiều thứ / đặt cọc | A05 | **GAP phạm vi actor/policy**; chưa là bug F07 Customer mộtthứ. Chốt quyền/giá/thanh toán trước code |
| Quên mật khẩu, trợ giúp, show/hide | A11 | **GAP**; backend reset dùng one-time/expiry/rate limits, feedback không lộ tài khoản |
| Copy tiền/nội dung, QR có tiền | A09 | **GAP**; giữ xác nhận owner; QR động không tự đồng nghĩa bankingintegration |
| Filter/search đơn, chỉ chưa đọc, thời điểm cập nhật | A11 chưa đủ chứng minh tất cả | **Đề xuất GOOD/UNKNOWN**, dựa nhu cầu ShuttleBook, không gán ALOBO hơn ở control chưa thấy |
| Chat/POS/voucher/services/native push | A02/A10 | Feature ngoài core, xếp sau pilot/F08/release gates; không bắt làm hết để DONE local |

## 7. Hướng nghiệm thu và bước tiếp theo

[Checklist từng control](../testing/ShuttleBook-ui-button-acceptance.md) có testcase agent có thể tự chạy và thao tác bạn kiểm bằng tay. Status của testcase đề xuất mặc định NOT RUN; suite158PASS chỉ áp dụng các test đã chạy được liệt kê trong checklist, không chứng minh mọi trạng thái mọi button.

Thứ tự đề xuất: **xác minh/sửa recovery/auth/query/polling → cải thiện auth/mobile/giá/đối chiếu → F08 nhân viên → F09 phát hành/báo cáo → pilot ít cơ sở**. Root chưa code, commit/push, migrate DB development, đăng ký ALOBO, gửi OTP hay tạo tài nguyên trả phí trong audit này.

## 8. Inventory chi tiết theo vai trò

Mỗi record là **control logic**, có thể là nút/link/tab/input/select/scroll/file hoặc family động. Không đếm mỗi sân, ca30 phút hay record thông báo thành một nút riêng. Nhãn động được giữ template. Native MapTiler là một dòng chưa quan sát, không tính thành nút đã kiểm. Nguồn có file:line để đọc implementation; testcase/gate được đặt ở checklist cùng ID.

Danh mục có **220 record logic**: Customer83, Partner112, Admin25; trong đó **218 control/family có source xác định và2 dòng SDK chưa quan sát**. Trường input cũng là control, không gọi tổng này là số nút. Ba reviewer đọc19TSX Customer và toàn bộ TSX Partner/Admin; record chia theo màn/trạng thái nên số nhóm không đồng nghĩa số routes.

| Vai trò | Records | Nhóm màn/trạng thái trong danh mục |
|---|---:|---:|
| Customer / Guest | 83 | 43 |
| Partner | 112 | 25 |
| Admin | 25 | 8 |

### Customer / Guest

#### Shell mọi trang

<a id="c01"></a>

**C01 — Chuyển đến nội dung** (link)

- **Hành động:** Focus #customer-content, không đổi route
- **Trạng thái:** Luôn có; hiện khi keyboard focus
- **Đang tốt:** Skip ngăn tab qua sidebar lặp; handler preventDefault giữ hash
- **Chưa tốt / P2:** Chưa có kiểm NVDA/zoom 200%; không suy đã đạt từ skip test
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/customer-web/src/components/CustomerShell.tsx:25](../../apps/customer-web/src/components/CustomerShell.tsx#L25). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c02"></a>

**C02 — ShuttleBook — Tìm sân** (brand link)

- **Hành động:** SPA /venues qua App click interceptor
- **Trạng thái:** Luôn có; accessible label riêng, logo aria-hidden
- **Đang tốt:** Cùng một đường về tìm sân; không remount SessionProvider
- **Chưa tốt / P2:** Route mới không tự focus heading/main hoặc reset scroll trong App:19
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/components/CustomerShell.tsx:29](../../apps/customer-web/src/components/CustomerShell.tsx#L29). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c03"></a>

**C03 — Tìm sân** (nav link)

- **Hành động:** SPA /venues
- **Trạng thái:** Guest và Customer; aria-current theo path
- **Đang tốt:** Guest xem sân trước khi login; active nav cả trang chi tiết
- **Chưa tốt / P2:** Chưa có lưu query/nearby khi quay về; route /venues trống làm mất bộ lọc đã chọn
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerSession.tsx:62](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L62). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Shell Customer

<a id="c04"></a>

**C04 — Đơn của tôi** (nav link)

- **Hành động:** SPA /me/bookings
- **Trạng thái:** Chỉ khi có session
- **Đang tốt:** Một lối vào tất cả đơn casual/fixed, không lẫn owner
- **Chưa tốt / P2:** F5 phải login lại theo contract memory; bất tiện cần policy enhancement riêng
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerSession.tsx:62](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L62). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c05"></a>

**C05 — Thông báo** (nav link badge family)

- **Hành động:** SPA /me/notifications
- **Trạng thái:** Có session; badge 1–99 hoặc 99+, aria-label số chưa đọc
- **Đang tốt:** Badge dùng unreadCount API, không bịa số
- **Chưa tốt / P2:** Không có filter chưa đọc hay bulk mark-read; capability chưa có, không phải lỗi core
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:49](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L49). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c06"></a>

**C06 — Đăng xuất** (button)

- **Hành động:** Clear memory, replace /login, POST /auth/logout
- **Trạng thái:** Chỉ khi có session; không spinner/disable
- **Đang tốt:** Xóa state trước request, không để người dùng tiếp tục đọc đơn cũ
- **Chưa tốt / P1:** catch bị bỏ qua, không biết server revoke thất bại; refresh/logout cạnh tranh cần DB test
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerSession.tsx:63](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L63). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Shell Guest

<a id="c07"></a>

**C07 — Đăng nhập** (nav link)

- **Hành động:** SPA /login
- **Trạng thái:** Chỉ Guest
- **Đang tốt:** Lối vào login luôn rõ; browse vẫn dùng được
- **Chưa tốt / P2:** Nav link không kèm returnTo trang hiện tại, login từ chi tiết về /venues mặc định
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerSession.tsx:63](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L63). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Footer mọi trang

<a id="c08"></a>

**C08 — Tìm cơ sở & lịch trống** (link)

- **Hành động:** SPA /venues
- **Trạng thái:** Luôn có
- **Đang tốt:** Có đường về tìm sân khi ở cuối nội dung dài
- **Chưa tốt / P2:** Không giữ query/nearby của trang trước; same destination với C03
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/components/CustomerShell.tsx:47](../../apps/customer-web/src/components/CustomerShell.tsx#L47). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tài khoản giới thiệu

<a id="c09"></a>

**C09 — Tìm sân gần bạn và xem lịch trống** (primary link)

- **Hành động:** SPA /venues
- **Trạng thái:** Guest/Customer ở identity
- **Đang tốt:** Nêu task thay vì chỉ nút đăng ký, phù hợp khám phá trước login
- **Chưa tốt / P2:** Label hứa gần bạn nhưng mở list mặc định, chưa lấy GPS hay nearby
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:139](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L139). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tài khoản đã login

<a id="c10"></a>

**C10 — Tìm sân & xem lịch** (primary link)

- **Hành động:** SPA /venues
- **Trạng thái:** Chỉ có session
- **Đang tốt:** Không còn chỉ dòng Đang hoạt động, cho tiếp tục booking
- **Chưa tốt / P2:** Không có sân vừa xem/gợi ý lần gần nhất; không suy cá nhân hóa từ CTA
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:147](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L147). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c11"></a>

**C11 — Xem đơn của tôi** (secondary link)

- **Hành động:** SPA /me/bookings
- **Trạng thái:** Chỉ có session
- **Đang tốt:** Nêu rõ task xem trạng thái đơn sau login
- **Chưa tốt / P2:** Không có đơn gần nhất trên dashboard; phải thêm bước mở list
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:148](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L148). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c12"></a>

**C12 — Xem thông báo** (secondary link)

- **Hành động:** SPA /me/notifications
- **Trạng thái:** Chỉ có session
- **Đang tốt:** Tài khoản có đường vào inbox, cùng state với badge shell
- **Chưa tốt / P2:** Không hiển thị unreadCount tại CTA này, phải đọc badge nav
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:148](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L148). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Auth tabs

<a id="c13"></a>

**C13 — Đăng ký** (button tab)

- **Hành động:** Navigate /, giữ location.search
- **Trạng thái:** Guest; disabled khi register, không disabled theo submitting
- **Đang tốt:** Giữ returnTo khi chuyển login/register; form current rõ bằng nền
- **Chưa tốt / P2:** Cho đổi tab khi request đang gửi, response cũ có thể điều hướng verify sau đã sang login
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:151](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L151). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

<a id="c14"></a>

**C14 — Đăng nhập** (button tab)

- **Hành động:** Navigate /login, giữ location.search
- **Trạng thái:** Guest; disabled khi login
- **Đang tốt:** Có thể đổi đăng ký/login không reload mất returnTo
- **Chưa tốt / P2:** Active dùng disabled thay aria-current/tab semantics; request cũ chưa abort khi đổi tab
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:152](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L152). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Đăng ký và Đăng nhập

<a id="c15"></a>

**C15 — Phương thức liên hệ** (select family)

- **Hành động:** Chọn Email hoặc Số điện thoại
- **Trạng thái:** Guest, cả 2 form; input type/placeholder đổi theo contactType
- **Đang tốt:** Label bọc selector, có email và phone rõ
- **Chưa tốt / P2:** Đổi type vẫn giữ contact trước đó, có thể gửi email dưới phone; cần reset hoặc validation gợi ý
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:157](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L157). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đăng ký

<a id="c16"></a>

**C16 — Email / Số điện thoại E.164** (text/email/tel input family)

- **Hành động:** Set contact cho POST register
- **Trạng thái:** required; email native typeMismatch, phone E.164 placeholder
- **Đang tốt:** Autocomplete email/tel và ví dụ +84901234567 hỗ trợ nhập
- **Chưa tốt / P2:** Chỉ hướng dẫn E.164, chưa tự chuẩn hóa 09... cho người dùng Việt Nam
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:158](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L158). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c17"></a>

**C17 — Mật khẩu** (password input)

- **Hành động:** Set mật khẩu cho POST register
- **Trạng thái:** required, minLength12, autocomplete new-password
- **Đang tốt:** Help có 3 nhóm ký tự và aria-describedby; không lưu storage
- **Chưa tốt / P2:** Không có hiện/ẩn mật khẩu hay strength feedback trước submit
- **So ALOBO:** GAP A11 — tiện ích nhập mật khẩu; auth backend chưa so sánh.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:159](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L159). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c18"></a>

**C18 — Nhập lại mật khẩu** (password input)

- **Hành động:** So khớp trước gọi API
- **Trạng thái:** required minLength12; aria-invalid khi mismatch
- **Đang tốt:** Mismatch không phát request; feedback được focus
- **Chưa tốt / P2:** aria-invalid dựa message chung; sau sửa input vẫn còn cờ đến lần submit kế
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:160](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L160). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c19"></a>

**C19 — Đăng ký / Đang đăng ký…** (submit button)

- **Hành động:** POST /auth/register;202 sang verify
- **Trạng thái:** disabled submitting; form aria-busy; network/429/delivery errors
- **Đang tốt:** Không double-click khi pending; password/code được xóa sau202; thông báo tránh lộ tài khoản
- **Chưa tốt / P2:** Form fields và tabs vẫn đổi được trong request, thiếu request generation guard
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:162](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L162). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Xác minh local

<a id="c20"></a>

**C20 — Mailpit** (external link)

- **Hành động:** http://localhost:8025 target=_blank
- **Trạng thái:** DEV only
- **Đang tốt:** Không lộ link debug production; noreferrer
- **Chưa tốt / NONE:** Chỉ local delivery, không chứng minh OTP gửi email/SMS thật
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:167](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L167). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Xác minh

<a id="c21"></a>

**C21 — Mã xác minh 6 chữ số** (numeric text input)

- **Hành động:** Set code cho verify-contact
- **Trạng thái:** required; disable nếu !contact; pattern/max6; xóa non-digit
- **Đang tốt:** OTP autocomplete one-time-code, mobile numeric keyboard
- **Chưa tốt / P2:** Không hiện thời hạn OTP/countdown, user biết hết hạn sau submit
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:168](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L168). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c22"></a>

**C22 — Xác minh / Đang xử lý…** (submit button)

- **Hành động:** POST /auth/verify-contact, success sang login
- **Trạng thái:** disabled submitting hoặc thiếu contact
- **Đang tốt:** Không auto-login nhầm, clear OTP khi success; thông báo invalid/expired
- **Chưa tốt / P2:** Không phân biệt invalid với expired bằng câu chung; không đưa nút gửi lại ngay trong error
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:169](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L169). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c23"></a>

**C23 — Gửi lại mã** (button)

- **Hành động:** POST /auth/verification-resend
- **Trạng thái:** disabled submitting/!contact; Retry-After được đưa vào message
- **Đang tốt:** Server202/429/503 được phản hồi, tránh báo gửi thành công giả
- **Chưa tốt / P2:** Không cooldown countdown trên nút, người dùng phải thử rồi thấy429
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:170](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L170). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Xác minh thiếu contact

<a id="c24"></a>

**C24 — Quay lại đăng ký** (button)

- **Hành động:** Navigate / giữ returnTo
- **Trạng thái:** Chỉ !contact
- **Đang tốt:** Deep link verify hoặc mất history.state có đường hồi phục
- **Chưa tốt / P2:** Không có nhập lại contact tại verify, phải qua đăng ký từ đầu
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:171](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L171). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đăng nhập

<a id="c25"></a>

**C25 — Email / Số điện thoại E.164** (text/email/tel input family)

- **Hành động:** Set contact cho login
- **Trạng thái:** required; autocomplete username
- **Đang tốt:** Hỗ trợ password manager và label contact theo type
- **Chưa tốt / P2:** Phone vẫn yêu cầu E.164 chưa thân thiện nhập09...; type switch giữ input cũ
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:177](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L177). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c26"></a>

**C26 — Mật khẩu** (password input)

- **Hành động:** Set password login
- **Trạng thái:** required; autocomplete current-password
- **Đang tốt:** Password manager phù hợp, xóa password sau response
- **Chưa tốt / P2:** Chưa có Hiện mật khẩu/Quên mật khẩu; missing recovery capability, không là control tồn tại
- **So ALOBO:** GAP A11 — tiện ích nhập mật khẩu; auth backend chưa so sánh.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:178](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L178). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c27"></a>

**C27 — Đăng nhập / Đang đăng nhập…** (submit button)

- **Hành động:** POST /auth/login; parse ACTIVE CUSTOMER; safeReturnTo
- **Trạng thái:** disabled submitting; 429/generic401/wrongrole/network feedback
- **Đang tốt:** Không nhận ADMIN hoặc operator token; returnTo allowlist chống redirect ngoài
- **Chưa tốt / P2:** F5 mất phiên memory theo contract; login từ nav không giữ trang chi tiết (C07)
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/auth/CustomerIdentity.tsx:179](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L179). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tìm cơ sở

<a id="c28"></a>

**C28 — Tên sân hoặc địa chỉ** (text input)

- **Hành động:** Set query, Enter submit Tìm sân
- **Trạng thái:** Guest/Customer; có URL q khởi tạo
- **Đang tốt:** Search text dùng nguồn API nội bộ, không bắt cấp GPS
- **Chưa tốt / P2:** Không có maxlength hay clear input, có thể gửi query quá dài rồi400 chung
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:104](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L104). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c29"></a>

**C29 — Tìm sân** (submit button)

- **Hành động:** GET /venues?q=..., replace URL q; reset nearby
- **Trạng thái:** Luôn có, chưa disable khi loading
- **Đang tốt:** Không bắt login; trim query, tìm cả tên/địa chỉ; abort initial request cũ
- **Chưa tốt / P2:** Đổi query khi Xem thêm pending có thể trộn page cũ bởi loadMore không guard
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:105](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L105). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

<a id="c30"></a>

**C30 — Dùng vị trí của tôi** (button)

- **Hành động:** Browser geolocation -> GET /venues/nearby
- **Trạng thái:** Luôn có; timeout10s; GPS failure alert
- **Đang tốt:** Denied GPS vẫn tìm bằng tên; nearby theo lat/lng/radius, không dùng tên làm khoảng cách
- **Chưa tốt / P2:** Không loading/disable khi xin GPS; bấm nhiều hoặc GPS cũ về sau text search có thể đổi mode bất ngờ
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:107](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L107). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Tìm khu vực

<a id="c31"></a>

**C31 — Nhập khu vực trên bản đồ** (text autocomplete input)

- **Hành động:** MapTiler geocoding Vietnam vi sau400ms và>=3chars
- **Trạng thái:** Chỉ khi có key; xóa suggestions khi nhập; network error locationError
- **Đang tốt:** Không yêu cầu người dùng biết tọa độ; debounce và abort request cũ
- **Chưa tốt / P2:** Chưa có combobox aria-expanded/activedescendant/ArrowDown, nhập dưới3 ký tự không giải thích
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:109](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L109). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c32"></a>

**C32 — {suggestion.place_name}** (suggestion button family)

- **Hành động:** Chọn point từ geometry/center, set nearby
- **Trạng thái:** Có suggestions; label place_name, list aria-label
- **Đang tốt:** Địa chỉ đầy đủ chọn trực tiếp, clear suggestion/error sau chọn
- **Chưa tốt / P2:** Chỉ native buttons tab, chưa combobox keyboard; lat/lng provider thực chưa test ở audit
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:114](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L114). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tìm cơ sở

<a id="c33"></a>

**C33 — Bán kính** (select)

- **Hành động:** Set 3/5/10/20km -> effect fetch
- **Trạng thái:** Luôn có; mặc định5km
- **Đang tốt:** Đơn vị km dễ hiểu; nearby request radiusMeters nhất quán
- **Chưa tốt / P2:** Selector vẫn active ở list mode, đổi radius refetch list nhưng không lọc theo bán kính dễ gây hiểu nhầm
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:116](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L116). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tìm cơ sở lỗi

<a id="c34"></a>

**C34 — Thử lại** (button)

- **Hành động:** Increment retry -> reload current search
- **Trạng thái:** Chỉ error; chưa disable loading
- **Đang tốt:** Người dùng có recovery cho lỗi API, không reload cả session
- **Chưa tốt / P2:** Retry loadMore lỗi sẽ chạy initial page và reset items, không retry cùng cursor
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:126](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L126). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tìm cơ sở trống

<a id="c35"></a>

**C35 — Đổi tìm kiếm** (button)

- **Hành động:** Focus #venue-query
- **Trạng thái:** Chỉ !loading,!error,items=0
- **Đang tốt:** Hành động cụ thể đưa focus về field thay vì empty dead end
- **Chưa tốt / P2:** Nếu nearby rỗng, CTA chỉ focus text mà không gợi đổi radius/chọn khu vực
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:128](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L128). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Kết quả cơ sở

<a id="c36"></a>

**C36 — {venue.name}** (card title link family)

- **Hành động:** SPA /venues/{venue.id}
- **Trạng thái:** Mỗi venue result; ảnh fallback/địa chỉ/distance
- **Đang tốt:** Mở cùng detail flow như CTA, giữ Customer memory session
- **Chưa tốt / P2:** Card chưa có min/max giá hoặc giờ hoạt động/tình trạng nhanh; phải vào từng cơ sở
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:131](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L131). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c37"></a>

**C37 — Xem lịch các sân →** (card action link family)

- **Hành động:** SPA /venues/{venue.id}
- **Trạng thái:** Mỗi venue result
- **Đang tốt:** Nêu rõ bước kế là lịch toàn bộ sân, khác marker full navigation
- **Chưa tốt / P2:** Nhiều CTA có cùng accessible name chưa có aria-label kèm venue, khó phân biệt bằng danh sách links
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:133](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L133). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Tìm cơ sở pagination

<a id="c38"></a>

**C38 — Xem thêm cơ sở** (button)

- **Hành động:** GET same mode/query/radius + cursor rồi append
- **Trạng thái:** Chỉ nextCursor; disabled loading; không label loading riêng
- **Đang tốt:** Cursor append cho danh sách, không tải toàn bộ data một lần
- **Chưa tốt / P2:** Không AbortSignal/search-generation/dedup; response page cũ có thể append vào query/radius mới
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueSearchPage.tsx:135](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L135). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Bản đồ cơ sở

<a id="c39"></a>

**C39 — Xem {venue.name}** (marker button family)

- **Hành động:** window.location.href=/venues/{id}
- **Trạng thái:** Có key/SDK; title+aria-label venue; dot visual
- **Đang tốt:** Marker có accessible name riêng và đúng tọa độ venue
- **Chưa tốt / P1:** Full navigation làm session memory vềnull, khác card SPA; CSS marker36px<44px target chung
- **So ALOBO:** UNKNOWN — A11 chưa thử marker, giữ nhận xét source/session riêng.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/VenueMap.tsx:50](../../apps/customer-web/src/features/venues/components/VenueMap.tsx#L50). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Bản đồ MapTiler

<a id="c40"></a>

**C40 — Controls native MapTiler: NOT OBSERVED** (SDK control family)

- **Hành động:** SDK render drag/zoom/attribution nếu SDK thêm
- **Trạng thái:** Key/SDK mới hiện map; code không cấu hình NavigationControl riêng
- **Đang tốt:** Lỗi map không chặn list và có fallback message
- **Chưa tốt / NONE:** Không có markup/label native ở source; chưa quan sát SDK thật, không đoán nút zoom nào tồn tại
- **So ALOBO:** UNKNOWN / NOT OBSERVED — SDK MapTiler không được invent button labels.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/VenueMap.tsx:48](../../apps/customer-web/src/features/venues/components/VenueMap.tsx#L48). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Chi tiết cơ sở

<a id="c41"></a>

**C41 — Danh sách cơ sở** (breadcrumb link)

- **Hành động:** SPA /venues
- **Trạng thái:** Luôn có cả loading/error
- **Đang tốt:** Có đường ra khi detail đang tải, không cần Back browser
- **Chưa tốt / P2:** Bỏ query/nearby trước đó; không giữ vị trí danh sách
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:89](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L89). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Chi tiết lỗi

<a id="c42"></a>

**C42 — Quay lại danh sách** (error link)

- **Hành động:** SPA /venues
- **Trạng thái:** Chỉ detail error
- **Đang tốt:** 404/unpublished có hướng hồi phục rõ
- **Chưa tốt / P2:** Không có Thử lại detail riêng, lỗi mạng phải quay list hoặc F5 mất phiên
- **So ALOBO:** PARITY-PARTIAL A11 — cùng nhóm tìm nơi chơi; lỗi/loading/rights phía ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:91](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L91). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Lịch cơ sở

<a id="c43"></a>

**C43 — Vãng lai** (mode toggle)

- **Hành động:** Set casual và history mode=casual
- **Trạng thái:** Có venue; aria-pressed
- **Đang tốt:** Mode rõ, dùng chung bảng sân/ca cho cả hai hình thức
- **Chưa tốt / P2:** Mode change không phát popstate nên App URL state chưa sync; cần kiểm Back và selection
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:98](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L98). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c44"></a>

**C44 — Cố định hằng tuần** (mode toggle)

- **Hành động:** Set fixed và history mode=fixed
- **Trạng thái:** Có venue; aria-pressed; rule >=2 giờ/tháng visible
- **Đang tốt:** Minimum cập nhật >=120 và giải thích kỳ từ bước chọn sân
- **Chưa tốt / P2:** Ở đây chưa nêu thanh toán 100% cả kỳ; phải tới series review mới biết
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:100](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L100). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c45"></a>

**C45 — Ngày chơi** (date input)

- **Hành động:** Set date, clear schedule, history date -> GET availability
- **Trạng thái:** min venue-today; không max60 tại control
- **Đang tốt:** Múi giờ dùng venue, không lấy timezone máy để quyết định ngày
- **Chưa tốt / P2:** Cho chọn >60 ngày dù booking horizon60, chỉ biết bị từ chối ở quote; chưa shortcut hôm nay/ngày mai
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:104](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L104). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c46"></a>

**C46 — Làm mới lịch** (button)

- **Hành động:** Increment retry -> GET availability ngày chọn
- **Trạng thái:** Có venue; loading status; không disable
- **Đang tốt:** Có refresh chủ động ngoài polling30s/focus, không auto mutation
- **Chưa tốt / P2:** Không hiện last-updated; B có thể thấy ô cũ tới refresh, không phải realtime push
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/VenueDetailPage.tsx:107](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L107). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Bảng sân × giờ

<a id="c47"></a>

**C47 — {court.name}, {time} đến {slot.endsAt}, Còn trống, {price}** (slot button family)

- **Hành động:** Chọn/deselect ca; fill range nếu trống; switch court reset selection
- **Trạng thái:** Chỉ AVAILABLE button; aria-pressed; unavailable span; mỗi ô30phút
- **Đang tốt:** Tất cả sân thành rows; label gồm sân/giờ/giá; không click ca kín; minimum liên tiếp
- **Chưa tốt / P2:** Bấm ô giữa range giữ nhánh dài hơn và bỏ nhánh kia, cần UX giải thích; tổng tham khảo number có precision boundary
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:91](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L91). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c48"></a>

**C48 — Bảng lịch sân, cuộn ngang để xem các giờ khác** (focusable scroll region)

- **Hành động:** Cuộn ngang bảng với cột sân sticky
- **Trạng thái:** Có axis; tabIndex0; table row/column scope
- **Đang tốt:** Giữ30phút/ô đọc được, full-day không nén chữ như ảnh cũ
- **Chưa tốt / P2:** Mỗi slot một tab stop nên keyboard traversal dài; chưa arrow navigation hoặc jump giờ
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:68](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L68). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Lịch chọn vãng lai

<a id="c49"></a>

**C49 — Tiếp tục đặt vãng lai** (button)

- **Hành động:** navigate /booking-review với venue,court,date,start,end
- **Trạng thái:** Có selection; disabled dưới minimum
- **Đang tốt:** Không tính block modulus; minimum đủ cho4/5/6/7ca; guard login ở review
- **Chưa tốt / P2:** Guest chỉ biết cần login sau click, CTA chưa nêu điều đó; route mới chưa focus heading
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:105](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L105). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Lịch chọn cố định

<a id="c50"></a>

**C50 — Tiếp tục đặt cố định** (button)

- **Hành động:** navigate /series-review giữ court/date/range
- **Trạng thái:** Mode fixed; có selection; disabled dưới max120/minimum
- **Đang tốt:** Đi chung entry point; quote toàn kỳ chứ không đặt mỗi buổi rời
- **Chưa tốt / P2:** Đã có note giá ngày khác giá cả kỳ; vẫn thiếu focus chuyển bước
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:105](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L105). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo giá vãng lai

<a id="c51"></a>

**C51 — Quay lại lịch các sân** (link)

- **Hành động:** SPA /venues/{venueId}?date=...
- **Trạng thái:** Luôn có trong review
- **Đang tốt:** Quay đúng venue/ngày, không mất phiên
- **Chưa tốt / P2:** Không restore chọn ca; quote hold cũ còn tới TTL nên A nhìn ô mình là kín, chưa có label own hold
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:56](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L56). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo giá/đơn casual và từng buổi fixed

<a id="c52"></a>

**C52 — Chi tiết giá từng ca 30 phút** (details summary family)

- **Hành động:** Native expand/collapse price list
- **Trạng thái:** Quote/detail khi có slots; mỗi occurrence có summary
- **Đang tốt:** Tổng nổi trước, giá30phút chi tiết mở khi cần; slot dùng exact string nếu API có
- **Chưa tốt / P2:** Series nhiều summary cùng nhãn thiếu ngày trong accessible name; casual API quote chỉ number có boundary >2^53
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingFacts.tsx:13](../../apps/customer-web/src/features/bookings/BookingFacts.tsx#L13). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo giá vãng lai

<a id="c53"></a>

**C53 — Xác nhận tạo đơn / Đang tạo đơn…** (button)

- **Hành động:** POST /bookings cùng Idempotency-Key -> detail
- **Trạng thái:** Quote hợp lệ; disabled pending/hết hạn; busy ref
- **Đang tốt:** Không double-submit; consumed/changed/expired clear quote, có countdown giữ tạm2phút
- **Chưa tốt / P1:** Lost create response rồi TTL: create:44 chặn retry kể cả same key; khách phải tự vào Đơn tôi; quote.amount number không exact
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:63](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L63). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2; root probe mới REPRODUCED_UI_FIXTURE abort+TTL (DB commit NOT RUN).

<a id="c54"></a>

**C54 — Lấy báo giá mới** (button)

- **Hành động:** POST /availability/quote, replace quote+intent
- **Trạng thái:** Disabled loading/submitting; dùng authenticated request
- **Đang tốt:** Giá đổi/hết hạn cần xác nhận báo giá mới, không tạo order tự động
- **Chưa tốt / P2:** Bấm trong TTL thay quote nhưng backend không kéo TTL; message chưa nói rõ refresh không gia hạn
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:65](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L65). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c55"></a>

**C55 — Xem Đơn của tôi** (link)

- **Hành động:** SPA /me/bookings
- **Trạng thái:** Luôn có review
- **Đang tốt:** Đường khôi phục khi network lost/QUOTE_CONSUMED, không cần tạo đơn mới
- **Chưa tốt / P2:** Không có link trực tiếp đơn đã tạo nếu QUOTE_CONSUMED; phải chọn trong list
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:66](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L66). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Thiết lập cố định

<a id="c56"></a>

**C56 — Quay lại lịch các sân** (link)

- **Hành động:** SPA venue?date=start&mode=fixed, /venues nếu thiếu ID
- **Trạng thái:** Luôn có form
- **Đang tốt:** Giữ mode fixed/ngày khi quay lại lịch
- **Chưa tốt / P2:** Quote hold toàn kỳ còn tới TTL, chưa phân biệt own hold với người khác
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:114](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L114). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c57"></a>

**C57 — Ngày trong tuần** (select)

- **Hành động:** Set weekday, invalidate quote/intent
- **Trạng thái:** Disabled loading/submitting bởi fieldset
- **Đang tốt:** MONDAY–SUNDAY nhãn Việt; form đổi buộc quote mới
- **Chưa tốt / P2:** Đổi weekday không tự điều chỉnh startsOn; ngày đầu kỳ có thể không là buổi đầu, cần preview rõ
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:119](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L119). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c58"></a>

**C58 — Giờ bắt đầu** (time input)

- **Hành động:** Set time, invalidate quote, validate30phút
- **Trạng thái:** Step1800; disabled busy
- **Đang tốt:** Hỗ trợ picker và check mốc30phút trước gọi API
- **Chưa tốt / P2:** HTML time picker có thể nhập mốc15phút, chỉ thấy lỗi khi quote; không offer available times
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:120](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L120). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c59"></a>

**C59 — Thời lượng mỗi buổi (phút)** (number input)

- **Hành động:** Set duration, invalidate quote; validate >=minimum và chia hết30
- **Trạng thái:** min max120/court,step30,max1440; disabled busy
- **Đang tốt:** 150phút/5ca được nhận, user thấy giờ kết thúc
- **Chưa tốt / P2:** Phải nhập phút raw number, chưa preset2h/2.5h/3h; max1440 khác validation cùng ngày
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:121](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L121). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c60"></a>

**C60 — Ngày bắt đầu kỳ** (date input)

- **Hành động:** Set startsOn, invalidate quote
- **Trạng thái:** min venue-today,max+60; disabled busy
- **Đang tốt:** Horizon rõ và theo venue timezone
- **Chưa tốt / P2:** Đổi startsOn không tự đẩy endsOn; có thể để kỳ <1tháng, lỗi không chỉ field cần sửa
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:122](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L122). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c61"></a>

**C61 — Ngày kết thúc kỳ** (date input)

- **Hành động:** Set endsOn, invalidate quote
- **Trạng thái:** min calendarMonthAfter start,max today+60
- **Đang tốt:** Tối thiểu1tháng lịch; clamp cuối tháng có helper
- **Chưa tốt / P2:** Horizon/max12buổi dùng note/text error, chưa preview số buổi trước quote
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:123](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L123). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo giá cố định

<a id="c62"></a>

**C62 — Xem báo giá toàn kỳ / Lấy báo giá mới cho kỳ / Đang kiểm tra toàn kỳ…** (submit button)

- **Hành động:** POST /booking-series/quote, preview all, hold all nếu valid
- **Trạng thái:** Disabled loading/submitting; busy ref; conflict200canCreatefalse
- **Đang tốt:** Conflict liệt kê ngày, không bỏ trùng/ngầm đặt phần trống; toàn kỳ hold120s
- **Chưa tốt / P2:** Đổi form sau quote xóa quote UI nhưng hold cũ còn tới TTL; chưa thông báo điều này
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:126](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L126). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c63"></a>

**C63 — Xác nhận tạo lịch cố định / Thử lại tạo lịch cùng yêu cầu / Đang tạo lịch…** (button)

- **Hành động:** POST /booking-series cùng key/quote; success canonical detail
- **Trạng thái:** OnlycanCreate+quoteId; busy/expiry guard; attempted canRetry sauTTL
- **Đang tốt:** Retry mất response giữ key sauTTL, tổng exact/QR100%/all-or-none; clear bad intent
- **Chưa tốt / P2:** API403/404 clear quote đúng; thành công chưa focus heading/scroll, cần kiểm keyboard
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:142](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L142). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c64"></a>

**C64 — Xem Đơn của tôi** (link)

- **Hành động:** SPA /me/bookings
- **Trạng thái:** Luôn có khi đã session
- **Đang tốt:** Thoát review khi không chắc request thành công, không tự tạo quote mới
- **Chưa tốt / P2:** Không có filter cố định trên list, phải tìm seriesNo thủ công
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/SeriesReview.tsx:146](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L146). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Chi tiết đơn

<a id="c65"></a>

**C65 — Làm mới đơn** (button)

- **Hành động:** GET /bookings/{id} qua visible polling
- **Trạng thái:** Luôn có, cả error/loading; không disable
- **Đang tốt:** Version cũ không ghi đè command mới; 401/403/404 clear private details
- **Chưa tốt / P2:** Không spinner/disable tại nút hoặc last-updated; liên tiếp click abort/restart đọc đơn
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:118](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L118). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c66"></a>

**C66 — Đơn của tôi** (link)

- **Hành động:** SPA /me/bookings
- **Trạng thái:** Luôn có kể cả error
- **Đang tốt:** Đơn không tìm thấy có lối về list; no cancel/change đúng nghiệp vụ
- **Chưa tốt / P2:** Danh sách chưa filter status/ngày/casual-fixed nên tra cứu lịch sử nhiều đơn chậm
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:118](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L118). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn chờ chuyển

<a id="c67"></a>

**C67 — Đã chuyển khoản** (button opener)

- **Hành động:** Mở payment form, chưa POST
- **Trạng thái:** AWAITING_TRANSFER; disable quá deadline trừ retry intent hợp lệ
- **Đang tốt:** Không coi bấm opener là PAID, có bước gửi rõ và owner confirm
- **Chưa tốt / P2:** Mở form chưa move focus đến heading/file; chưa có Đóng/Hủy form, customer phải đi trang khác
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:108](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L108). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn cần đối chiếu

<a id="c68"></a>

**C68 — Bổ sung bằng chứng** (button opener)

- **Hành động:** Mở form bổ sung cùng booking/payment
- **Trạng thái:** NEEDS_REVIEW, không bị deadline chuyển cũ khóa
- **Đang tốt:** Không phải tạo đơn/chuyển tiền lại; lịch toàn kỳ vẫn giữ
- **Chưa tốt / P2:** Không focus form hay đóng form; note không đọc cho người dùng bằng field-specific help
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:108](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L108). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo chuyển/Bổ sung

<a id="c69"></a>

**C69 — Ảnh chụp màn hình chuyển khoản (không bắt buộc)** (file input)

- **Hành động:** Chọn png/jpeg/webp <=5MB; submit presign -> PUT -> complete READY
- **Trạng thái:** Optional; disabled pending; file MIME/size validation và filename
- **Đang tốt:** Proof riêng, checksum SHA256, media complete kiểm READY; retry không upload lại file READY
- **Chưa tốt / P2:** Không preview ảnh đã chọn, chỉ filename; không camera capture hint; chưa browser/provider thật mobile/S3
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:113](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L113). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c70"></a>

**C70 — Bỏ ảnh biên lai** (button)

- **Hành động:** Clear file input, upload intent và lỗi file
- **Trạng thái:** Chỉ file hoặc fileError; disabled pending
- **Đang tốt:** File sai được gỡ để gửi report không ảnh; không bắt mã giao dịch
- **Chưa tốt / P2:** Không xóa upload đã READY trên server, orphan cleanup lifecycle cần provider/operational design riêng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:119](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L119). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c71"></a>

**C71 — Ghi chú (không bắt buộc)** (textarea)

- **Hành động:** Set note, trim, tối đa1000char
- **Trạng thái:** Optional, maxLength1000; disabled pending
- **Đang tốt:** Có label bằng useId; screenshot-only report không cần giao dịch dài
- **Chưa tốt / P2:** Không đếm ký tự còn lại; note draft mất khi F5/logout theo memory
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:120](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L120). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn chờ chuyển

<a id="c72"></a>

**C72 — Gửi báo chuyển khoản / Đang xử lý…** (submit button)

- **Hành động:** POST /bookings/{id}/transfer-evidence cùng key/If-Match
- **Trạng thái:** disable pending/expired/mustReload/fileError; busyref
- **Đang tốt:** Report screenshot-only/không ảnh, một intent; lost response replay sau deadline khi body không đổi
- **Chưa tốt / P2:** Form errors role alert nhưng không auto focus, keyboard phải dò lại; chưa có cancel form
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:123](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L123). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn cần đối chiếu

<a id="c73"></a>

**C73 — Gửi bổ sung bằng chứng / Đang xử lý…** (submit button)

- **Hành động:** POST cùng transfer-evidence kind SUPPLEMENT
- **Trạng thái:** NEEDS_REVIEW; deadline cũ không khóa; pending/mustReload guards
- **Đang tốt:** Bổ sung append history, owner đối chiếu lại, không giải phóng lịch hay báo PAID
- **Chưa tốt / P2:** Không mở note/reason ngay sát field, khách phải đọc lịch sử phía khác để biết cần bổ sung gì
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:123](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L123). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Báo chuyển conflict

<a id="c74"></a>

**C74 — Tải lại để kiểm tra đơn** (button)

- **Hành động:** GET /bookings/{id}, clear intent/mustReload, giữ draft
- **Trạng thái:** Chỉ mustReload sau409/412/deadline; disabled pending
- **Đang tốt:** Không tự replay stale version, user kiểm trạng thái rồi chủ động submit
- **Chưa tốt / P2:** Label chỉ reload, chưa nhắc đã giữ draft ở UI; cần xác minh trạng thái đổi làm form unmount đúng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingTransfer.tsx:124](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L124). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### QR/ảnh lịch sử lỗi

<a id="c75"></a>

**C75 — Tải lại ảnh** (button family)

- **Hành động:** Refetch authenticated private path -> object URL
- **Trạng thái:** Chỉ image error; allowed API route regex, blob revoked cleanup
- **Đang tốt:** Không mở URL tùy ý; private QR/proof không gửi bearer sang public URL
- **Chưa tốt / P2:** Không có phóng to/tải ảnh QR/proof và không copy tiền/nội dung; mobile một máy khó quét QR upload tĩnh
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/customer-web/src/features/bookings/PrivateImage.tsx:19](../../apps/customer-web/src/features/bookings/PrivateImage.tsx#L19). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn của tôi

<a id="c76"></a>

**C76 — {item.series?.seriesNo ?? item.bookingNo}** (booking link family)

- **Hành động:** SPA /bookings/{bookingId}
- **Trạng thái:** Mỗi item; nhóm fixed một row/tổng cả kỳ/status
- **Đang tốt:** Mã đơn rõ, không nhân bản series thành nhiều payment; tiền ưu tiên exact
- **Chưa tốt / P2:** Link chỉ mã, accessible name không có venue/date; không thấy nhãn hành động Xem đơn ngay
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:132](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L132). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c77"></a>

**C77 — Làm mới danh sách** (button)

- **Hành động:** Reset pageCursor -> first page; nếu first thì refresh
- **Trạng thái:** Luôn có; không disable loading
- **Đang tốt:** Khách chủ động về dữ liệu mới nhất, session-required guard
- **Chưa tốt / P2:** Khi đã tải nhiều trang nút reset toàn bộ list; không giải thích sẽ về trang đầu hay giữ scroll
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:136](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L136). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Đơn của tôi pagination

<a id="c78"></a>

**C78 — Xem thêm đơn** (button)

- **Hành động:** Set pageCursor và GET before rồi merge dedup
- **Trạng thái:** Chỉ cursor; không disabled loading
- **Đang tốt:** Merge bằng bookingId chống trùng trên trang hiện tại
- **Chưa tốt / P2:** Không disable pending; button vẫn cho click nhiều với cursor cũ; polling chỉ refresh trang active nên đầu list có thể stale
- **So ALOBO:** GOOD + UNKNOWN — có báo giá của ShuttleBook; A02/A05 không đủ chứng minh quote TTL/replay/all-or-none tương đương.
- **Source / evidence:** [apps/customer-web/src/features/bookings/BookingPages.tsx:137](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L137). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Inbox

<a id="c79"></a>

**C79 — Làm mới thông báo** (button)

- **Hành động:** Reload first notifications via provider polling
- **Trạng thái:** Luôn có; loading status ở page
- **Đang tốt:** Unread count từ API và polling khi visible, không fake analytics
- **Chưa tốt / P2:** Không disabled loading; provider error không clear cached items với403/404, cần resilience test chứ chưa runtime bug
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:71](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L71). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

#### Inbox trống

<a id="c80"></a>

**C80 — Tìm sân & xem lịch** (link)

- **Hành động:** SPA /venues
- **Trạng thái:** Chỉ !loading,!items,!error
- **Đang tốt:** Empty inbox vẫn có next action đặt sân
- **Chưa tốt / P2:** Chưa điều hướng tới đơn chờ hoặc recent sân, vẫn list tổng quát
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:74](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L74). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Inbox notification

<a id="c81"></a>

**C81 — Xem đơn đặt sân** (action link family)

- **Hành động:** Validated CUSTOMER_BOOKING + UUID -> SPA booking, async markRead
- **Trạng thái:** Chỉ action hợp lệ/customer UUID; no external href
- **Đang tốt:** Chống link operator/admin/arbitrary URL; xem đơn cũng đánh dấu đọc
- **Chưa tốt / P2:** Không đợi read hoàn thành trước navigate; readerror có thể chỉ thấy sau quay inbox, chưa loading/readfeedback tại row
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:78](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L78). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

<a id="c82"></a>

**C82 — Đánh dấu đã đọc** (button family)

- **Hành động:** POST /me/notifications/{id}/read; refresh count
- **Trạng thái:** Chỉ unread; reading Set chống đồng thời; chưa disabled/spinnerrow
- **Đang tốt:** Idempotent read, older item cũng cập nhật, badge thật
- **Chưa tốt / P2:** Nút vẫn enabled lúc POST nhưng serverrequest dedup, người dùng không thấy trạng thái đang xử lý; không mark all
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:79](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L79). SOURCE_CONFIRMED action/good; focused runtime NOT RUN; suite tổng xem mục2; historical browser do root tổng hợp

#### Inbox pagination

<a id="c83"></a>

**C83 — Xem thêm thông báo / Đang tải…** (button)

- **Hành động:** GET notifications?before=... append older, dedup items
- **Trạng thái:** Chỉ nextCursor; disabled paging; AbortController unmount
- **Đang tốt:** Không reset older khi providerpoll firstpage, tránh lặp bằngid
- **Chưa tốt / P2:** Sau refresh firstpage nhiều newitems có thể gap giữa cửa sổ first và older; chưa guard logout cache older riêng trongpage
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:81](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L81). SOURCE_CONFIRMED action/good; SOURCE_RISK hoặc enhancement cho gap; kịch bản risk focused runtime NOT RUN; suite tổng xem mục2

### Partner

#### Auth

<a id="p01"></a>

**P01 — ShuttleBook** (link)

- **Hành động:** Về /, reload
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** Brand nhất quán
- **Chưa tốt / P2:** Reload mất form chưa gửi
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:135](../../apps/partner-web/src/main.tsx#L135). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p02"></a>

**P02 — Đăng ký (chuyển màn)** (button)

- **Hành động:** setView(register), clear message
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** Đổi màn không request
- **Chưa tốt / P2:** Không disable khi gửi, response muộn có thể đổi view sau đó
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:147](../../apps/partner-web/src/main.tsx#L147). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p03"></a>

**P03 — Đăng nhập (chuyển màn)** (button)

- **Hành động:** setView(login), clear message
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** Tách đăng nhập/đăng ký
- **Chưa tốt / P2:** Response register/verify muộn có thể đổi view lại
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:148](../../apps/partner-web/src/main.tsx#L148). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p04"></a>

**P04 — Phương thức liên hệ** (select family)

- **Hành động:** Email/Số điện thoại register và login
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** Hỗ trợ hai contact
- **Chưa tốt / P2:** Phone09 chưa chuẩn hóa E.164; label kỹ thuật
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:153](../../apps/partner-web/src/main.tsx#L153). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p05"></a>

**P05 — Đăng ký** (submit)

- **Hành động:** POST partner-auth/register → verify
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Fields: Email hoặc Số điện thoại E.164 required type email/tel; Mật khẩu required password minLength12; Nhập lại mật khẩu required password. Confirm mismatch không POST; input chưa disabled khi submitting.
- **Đang tốt:** Confirm password trước API;202 riêng tư; busy disable
- **Chưa tốt / P2:** Error chưa chỉ field sai, không quên mật khẩu
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:165](../../apps/partner-web/src/main.tsx#L165). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p06"></a>

**P06 — Mailpit** (external link)

- **Hành động:** Tab localhost8025 chỉ DEV
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** Nói rõ local không gửi contact thật
- **Chưa tốt / NONE:** Provider production chưa kiểm, không phải ưu thế ALOBO
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:171](../../apps/partner-web/src/main.tsx#L171). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p07"></a>

**P07 — Xác minh** (submit)

- **Hành động:** POST partner-auth/verify → login
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Field: Mã xác minh6 chữ số required, inputMode numeric, pattern[0-9]{6}, maxLength6; onChange bỏ ký tự không phải số; mã sai/hết hạn feedback chung.
- **Đang tốt:** 6 số numeric sanitize; busy Đang kiểm tra…
- **Chưa tốt / P2:** Không countdown hạn OTP
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:174](../../apps/partner-web/src/main.tsx#L174). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p08"></a>

**P08 — Gửi lại mã** (button)

- **Hành động:** POST verification-resend
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Đang tốt:** 202 riêng tư;429 Retry-After;busy disable
- **Chưa tốt / P2:** Không cooldown trước click, chung submitting
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:175](../../apps/partner-web/src/main.tsx#L175). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p09"></a>

**P09 — Đăng nhập** (submit)

- **Hành động:** POST auth/login→onboarding
- **Trạng thái:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Fields: Email hoặc Số điện thoại E.164 required; Mật khẩu required password. Input chưa có autocomplete credential; không nút hiện/ẩn mật khẩu hay quên mật khẩu.
- **Đang tốt:** Chặn sai role;clear password;busy label
- **Chưa tốt / P2:** F5 login lại là memory policy hiện tại, productgapP2; không tự gán bug idle
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/main.tsx:188](../../apps/partner-web/src/main.tsx#L188). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Shell

<a id="p10"></a>

**P10 — Đến nội dung chính** (skip link)

- **Hành động:** Focus #partner-content
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Keyboard bỏ sidebar
- **Chưa tốt / NONE:** Screen reader mới NOT RUN
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/layouts/PartnerShell.tsx:24](../../apps/partner-web/src/layouts/PartnerShell.tsx#L24). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p11"></a>

**P11 — ShuttleBook desktop/mobile** (link family)

- **Hành động:** #/overview
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Hash SPA giữ memory session
- **Chưa tốt / P2:** Rời form không cảnh báo chưa lưu
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/layouts/PartnerShell.tsx:28](../../apps/partner-web/src/layouts/PartnerShell.tsx#L28). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p12"></a>

**P12 — Menu** (toggle)

- **Hành động:** Mở/đóng nav;Escape trả focus
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** aria-expanded/controls,Escape
- **Chưa tốt / NONE:** Không suy WCAG PASS từ source
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/layouts/PartnerShell.tsx:29](../../apps/partner-web/src/layouts/PartnerShell.tsx#L29). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p13"></a>

**P13 — Tổng quan** (nav link)

- **Hành động:** #/overview
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Current page+heading focus
- **Chưa tốt / P2:** Dashboard cấu hình, thiếu lịch/công suất/doanh thu hôm nay
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:4](../../apps/partner-web/src/features/workspace/navigation.ts#L4). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p14"></a>

**P14 — Hồ sơ doanh nghiệp** (nav link)

- **Hành động:** #/profile
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Draft edit/published readonly
- **Chưa tốt / P2:** Chưa sửa legalName/contact doanh nghiệp active
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:5](../../apps/partner-web/src/features/workspace/navigation.ts#L5). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p15"></a>

**P15 — Cơ sở** (nav link)

- **Hành động:** #/venues
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Phân cấp venue rõ
- **Chưa tốt / P2:** Active không add venue
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:6](../../apps/partner-web/src/features/workspace/navigation.ts#L6). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p16"></a>

**P16 — Sân** (nav link)

- **Hành động:** #/courts
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Tên sân theo venue
- **Chưa tốt / P2:** Active không add/rename/suspend; chưa search nhiều sân
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:7](../../apps/partner-web/src/features/workspace/navigation.ts#L7). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p17"></a>

**P17 — Lịch & giá** (nav link)

- **Hành động:** #/schedule
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Draft setup/active operations riêng
- **Chưa tốt / P2:** Partner chưa lịch ngày mọi sân, dropdown từng sân
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:8](../../apps/partner-web/src/features/workspace/navigation.ts#L8). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p18"></a>

**P18 — Đơn đặt sân** (nav link)

- **Hành động:** #/bookings
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Pending giải thích không vận hành
- **Chưa tốt / P2:** Chưa tạo đơn khách gọi điện/quầy, chưa calendar ngày
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:9](../../apps/partner-web/src/features/workspace/navigation.ts#L9). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p19"></a>

**P19 — Ảnh cơ sở** (nav link)

- **Hành động:** #/media
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Tách ảnh khỏi QR
- **Chưa tốt / P2:** Active chưa thay ảnh; không thumbnail/gallery
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:10](../../apps/partner-web/src/features/workspace/navigation.ts#L10). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p20"></a>

**P20 — Thanh toán & QR** (nav link)

- **Hành động:** #/payments
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Revision giữ bản hiện hành khi chờ duyệt
- **Chưa tốt / P2:** QR tĩnh, chưa preview QR hiện hành
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:11](../../apps/partner-web/src/features/workspace/navigation.ts#L11). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p21"></a>

**P21 — Thông báo** (nav link)

- **Hành động:** #/notifications
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Badge tổng unread
- **Chưa tốt / P2:** Chưa unreadfilter/search/markall
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/partner-web/src/features/workspace/navigation.ts:12](../../apps/partner-web/src/features/workspace/navigation.ts#L12). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p22"></a>

**P22 — Đăng xuất** (icon button)

- **Hành động:** POST auth/logout→clear memory login
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** aria-label, disabledbusy;local đóng cả khi mạng lỗi
- **Chưa tốt / P1:** Refresh/logout familyrace SOURCE_RISK cần PostGIS, chưa runtime xác nhận
- **So ALOBO:** UNKNOWN A11 — chỉ biết controls login public; trạng thái auth/OTP/TTL chưa thử.
- **Source / evidence:** [apps/partner-web/src/layouts/PartnerShell.tsx:42](../../apps/partner-web/src/layouts/PartnerShell.tsx#L42). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p23"></a>

**P23 — Doanh nghiệp** (select)

- **Hành động:** Load scope mới/resetdraft/close detail
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Disabled busy/loading/bookingBusy; xóa scope cũ
- **Chưa tốt / P2:** Không confirm mất draft; không searchable
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:156](../../apps/partner-web/src/PartnerOnboarding.tsx#L156). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p24"></a>

**P24 — Thử tải lại hồ sơ** (button)

- **Hành động:** GET businesses/detail
- **Trạng thái:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Đang tốt:** Loadfail không dựng createform giả
- **Chưa tốt / P2:** Error kỹ thuật code chưa hướng dẫn field/next action
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:165](../../apps/partner-web/src/PartnerOnboarding.tsx#L165). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Tổng quan

<a id="p25"></a>

**P25 — Xem hồ sơ / Quản lý lịch & giá** (CTA link)

- **Hành động:** Draft→profile/active→schedule
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** CTA theo giai đoạn owner
- **Chưa tốt / P2:** Active tới cấu hình thay lịch đặt ngày
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:23](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L23). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p26"></a>

**P26 — Thông tin doanh nghiệp** (checklist link)

- **Hành động:** #/profile
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Dẫn form liên quan
- **Chưa tốt / P2:** done=true theo tồn tại record, không validation completeness
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:13](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L13). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p27"></a>

**P27 — Cơ sở và sân** (checklist link)

- **Hành động:** #/venues
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Mỗi venue phải có court
- **Chưa tốt / P2:** Thiếu sân vẫn trỏ venues thêm bước courts
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:14](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L14). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p28"></a>

**P28 — Giờ hoạt động và bảng giá** (checklist link)

- **Hành động:** #/schedule
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Check tất cả court có dữ liệu
- **Chưa tốt / P2:** Có rows không chứng minh đủ giá/không gap
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:15](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L15). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p29"></a>

**P29 — Ảnh cơ sở** (checklist link)

- **Hành động:** #/media
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Check mọi venue có imageId
- **Chưa tốt / P2:** Chưa thumbnail để kiểm nội dung
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:16](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L16). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p30"></a>

**P30 — Tài khoản nhận tiền và QR** (checklist link)

- **Hành động:** #/payments
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Check mọi venue paymentAccount
- **Chưa tốt / P2:** Boolean không chứng minh QR scan đúng tài khoản
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:17](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L17). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p31"></a>

**P31 — Quản lý / Thêm cơ sở** (link family)

- **Hành động:** #/venues;Thêm chỉ empty
- **Trạng thái:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Đang tốt:** Empty có next action
- **Chưa tốt / P2:** Active zero-venue edge chưa có addform; không suy gặp thực tế
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/workspace/PartnerOverview.tsx:39](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L39). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Hồ sơ nháp

<a id="p32"></a>

**P32 — Tạo hồ sơ nháp** (submit)

- **Hành động:** POST business→venues
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên hiển thị, Tên pháp lý, Liên hệ đều required; businessName/legalName/contact local; không ghi secret.
- **Đang tốt:** Business trước venue/court;busyfieldset
- **Chưa tốt / P2:** Genericvalidation, draft mấtF5
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:184](../../apps/partner-web/src/PartnerOnboarding.tsx#L184). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p33"></a>

**P33 — Gửi hồ sơ duyệt** (button)

- **Hành động:** POST business/submit +reload
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** DRAFT-only;backend readiness
- **Chưa tốt / P2:** Chưa disable theo readiness/thiếu fieldlink khi400
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:188](../../apps/partner-web/src/PartnerOnboarding.tsx#L188). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Hồ sơ

<a id="p34"></a>

**P34 — Tải lại hồ sơ** (button)

- **Hành động:** GET detail
- **Trạng thái:** Có label, các trạng thái hiển thị/disabled được đọc từ source; không quy đổi thành runtime PASS.
- **Đang tốt:** Disabled busy/loading; cập nhật Admin status
- **Chưa tốt / P2:** Rerender version có thể mất edit chưa lưu
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:191](../../apps/partner-web/src/PartnerOnboarding.tsx#L191). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Hồ sơ nháp

<a id="p35"></a>

**P35 — Lưu thay đổi** (submit)

- **Hành động:** PUT business If-Match
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên, Tên pháp lý, Liên hệ required defaultValue từ detail; FormData gửi PUT If-Match detail.version; uncontrolled fields có thể reset theo detail key.
- **Đang tốt:** Concurrency version
- **Chưa tốt / P2:** 412/validation chỉcode, chưa chỉ field
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:209](../../apps/partner-web/src/PartnerOnboarding.tsx#L209). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Cơ sở nháp

<a id="p36"></a>

**P36 — Lưu cơ sở** (submit)

- **Hành động:** POST venues confirmedlocation
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên cơ sở, Liên hệ cơ sở, Múi giờ IANA required; timezone mặc định Asia/Ho_Chi_Minh. Địa chỉ/latitude/longitude hidden từ confirm MapTiler, không manual coordinate inputs.
- **Đang tốt:** Không nhập lat/lon; requiredconfirm
- **Chưa tốt / P2:** Múi giờ IANA vẫn là text kỹ thuật, thiếu selector/hint cho owner. Backend đã Trim timezone; không coi trailing-space là lỗi validation hiện tại.
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:227](../../apps/partner-web/src/PartnerOnboarding.tsx#L227). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p37"></a>

**P37 — Sửa cơ sở** (submit family)

- **Hành động:** PUT venue If-Match
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên, Liên hệ cơ sở, Múi giờ required defaultValue; MapTiler existing address/coords; FormData hiddenlocation; If-Match venue.version.
- **Đang tốt:** Scope/card từng venue
- **Chưa tốt / P2:** Không dirty/diff; field errors chung
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:248](../../apps/partner-web/src/PartnerOnboarding.tsx#L248). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Sân nháp

<a id="p38"></a>

**P38 — Sửa sân** (submit family)

- **Hành động:** PUT court name
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Field: Tên sân required defaultValue; PUT name; courtstatus là text readonly, không có selector trạng thái thật.
- **Đang tốt:** Rename record đúng scope
- **Chưa tốt / P2:** Không If-Match court UI; cần contract concurrency
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:258](../../apps/partner-web/src/PartnerOnboarding.tsx#L258). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p39"></a>

**P39 — Cơ sở (Thêm sân)** (select)

- **Hành động:** Set courtVenue
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Required emptyoption
- **Chưa tốt / P2:** Single venue vẫn chọn; chưa searchable
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:268](../../apps/partner-web/src/PartnerOnboarding.tsx#L268). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p40"></a>

**P40 — Lưu sân** (submit)

- **Hành động:** POST venue/courts
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Field: Tên sân required localcourtName; Cơ sở select riêngP39; clear tên khi success. Không status selector.
- **Đang tốt:** Clear tên success; scopedvenue
- **Chưa tốt / P2:** Chưa bulk3–7court; active không add tương đương
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:271](../../apps/partner-web/src/PartnerOnboarding.tsx#L271). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Lịch nháp

<a id="p41"></a>

**P41 — Sân** (select)

- **Hành động:** Set scheduleCourt
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Option venue/court rõ
- **Chưa tốt / P2:** Không load existingconfig vào scheduleRows; mẫu global dễ ghi đè nhầm
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:285](../../apps/partner-web/src/PartnerOnboarding.tsx#L285). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p42"></a>

**P42 — Ngày** (select family)

- **Hành động:** Set weekdayrow
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** 7 label tiếng Việt
- **Chưa tốt / P2:** Không multiday/copy tuần
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:288](../../apps/partner-web/src/PartnerOnboarding.tsx#L288). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p43"></a>

**P43 — Từ / Đến / Giá/30 phút** (input family)

- **Hành động:** Set window+amount
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Step30min/price>0
- **Chưa tốt / P2:** Không quy đổi giá/giờ ởdraft; gap/overlap chờ API
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:291](../../apps/partner-web/src/PartnerOnboarding.tsx#L291). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p44"></a>

**P44 — Xóa khung** (button family)

- **Hành động:** Delete localrow
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Không writeDB tới save
- **Chưa tốt / P2:** Không undo, xóa hết vẫn chưa readiness
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:297](../../apps/partner-web/src/PartnerOnboarding.tsx#L297). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p45"></a>

**P45 — Thêm khung giá** (button)

- **Hành động:** Append Tue07–22 price100000
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Nhiều khung giờ/ngày
- **Chưa tốt / P2:** Default dễ overlap, thiếu copy/template
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:299](../../apps/partner-web/src/PartnerOnboarding.tsx#L299). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p46"></a>

**P46 — Lưu giờ/giá** (submit)

- **Hành động:** PUT hours first-last +prices
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Cùng court giờ/giá
- **Chưa tốt / P2:** Không biểu đạt nghỉ giữa ngày; missingcoverage error chung
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:301](../../apps/partner-web/src/PartnerOnboarding.tsx#L301). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Ảnh nháp

<a id="p47"></a>

**P47 — Cơ sở** (select)

- **Hành động:** Set imageVenue
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Ảnh scoped venue
- **Chưa tốt / P2:** Single venue vẫn chọn
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:311](../../apps/partner-web/src/PartnerOnboarding.tsx#L311). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p48"></a>

**P48 — Ảnh cơ sở** (file input)

- **Hành động:** Set imageFile
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Accept PNG/JPEG/WebP,max5MB
- **Chưa tốt / P2:** Không preview/crop/remove; nativefilename clear cần kiểm
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:313](../../apps/partner-web/src/PartnerOnboarding.tsx#L313). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p49"></a>

**P49 — Tải ảnh** (submit)

- **Hành động:** SHA256→presign→PUT→complete→attach
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Checksum/persist sau complete
- **Chưa tốt / P2:** Không progress/thumbnail/phase error; S3 thậtNOTRUN
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:314](../../apps/partner-web/src/PartnerOnboarding.tsx#L314). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### QR nháp

<a id="p50"></a>

**P50 — Cơ sở** (select)

- **Hành động:** Set paymentVenue
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** QR scopedvenue
- **Chưa tốt / P2:** Đổi venue không reset bankfields riêng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:325](../../apps/partner-web/src/PartnerOnboarding.tsx#L325). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p51"></a>

**P51 — Ảnh QR** (file input)

- **Hành động:** Set qrFile
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Đang tốt:** Required validimages max5MB
- **Chưa tốt / P2:** Không preview/scan/account match
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:330](../../apps/partner-web/src/PartnerOnboarding.tsx#L330). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p52"></a>

**P52 — Lưu QR và tài khoản** (submit)

- **Hành động:** UploadQR +PUT payment-account
- **Trạng thái:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Mã ngân hàng, Tên tài khoản required text; Số tài khoản required inputMode numeric (không pattern/digits bound tại UI); QR/file và cơ sở select riêngP50/P51; account number clear sau success.
- **Đang tốt:** Mask storedaccount;clear accountnumber
- **Chưa tốt / P2:** Mãngân hàng rawtext; chưa picker/QRdynamic
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:331](../../apps/partner-web/src/PartnerOnboarding.tsx#L331). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Empty cấu hình

<a id="p53"></a>

**P53 — Đến mục Cơ sở** (link)

- **Hành động:** #/venues
- **Trạng thái:** Có label, các trạng thái hiển thị/disabled được đọc từ source; không quy đổi thành runtime PASS.
- **Đang tốt:** Prerequisite rõ
- **Chưa tốt / P2:** Active emptyedge không addform
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:336](../../apps/partner-web/src/PartnerOnboarding.tsx#L336). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Revision ACTIVE

<a id="p54"></a>

**P54 — Cơ sở** (select)

- **Hành động:** Set revisionvenue/currentfields
- **Trạng thái:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ.
- **Đang tốt:** Prefill currentdata
- **Chưa tốt / P2:** Đổi địa chỉ ởpayments khó tìm từvenues
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:351](../../apps/partner-web/src/PartnerOnboarding.tsx#L351). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p55"></a>

**P55 — QR mới** (file input)

- **Hành động:** Set qrFile required
- **Trạng thái:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ.
- **Đang tốt:** Criticalinfo gửi duyệt
- **Chưa tốt / P2:** Đổi liênhệ/address cũng phải newQR/account; chưa táchrevision
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:368](../../apps/partner-web/src/PartnerOnboarding.tsx#L368). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p56"></a>

**P56 — Gửi thay đổi để duyệt** (submit)

- **Hành động:** UploadQR +POST revisions
- **Trạng thái:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ. Fields: Liên hệ cơ sở, Múi giờ, Mã ngân hàng, Tên tài khoản required; Số tài khoản required inputMode numeric. Revision location từMapTiler; QR mới và venue riêngP54/P55. Không preview so sánh old/new.
- **Đang tốt:** PENDING khóa duplicate; giữ bảncũ
- **Chưa tốt / P2:** Không summaryold/new; fields toàn bộ bắt buộc
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOnboarding.tsx:369](../../apps/partner-web/src/PartnerOnboarding.tsx#L369). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Địa chỉ

<a id="p57"></a>

**P57 — Địa chỉ cơ sở trên MapTiler** (autocomplete input)

- **Hành động:** Debounce400ms≥4chars VN5results
- **Trạng thái:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Đang tốt:** Abort query cũ; confirm trướcsave
- **Chưa tốt / P2:** Thiếu combobox/listbox/Arrow keys/noresults/sửa pin
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/MapTilerPlacePicker.tsx:121](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L121). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p58"></a>

**P58 — {địa chỉ gợi ý}** (button family)

- **Hành động:** Candidate +center zoom17 marker
- **Trạng thái:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Đang tốt:** Xem trướccommit
- **Chưa tốt / P2:** Arrow keys không combobox; realprovider chưa kiểm
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/MapTilerPlacePicker.tsx:132](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L132). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p59"></a>

**P59 — Xác nhận vị trí này** (button)

- **Hành động:** Commit hiddenlocation/onChange
- **Trạng thái:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Đang tốt:** Explicitconfirm; gõ lại invalidates
- **Chưa tốt / P2:** Không drag/clickpin/reverse correction
- **So ALOBO:** UNKNOWN — chưa thử hồ sơ/chấp thuận ALOBO.
- **Source / evidence:** [apps/partner-web/src/MapTilerPlacePicker.tsx:135](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L135). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p60"></a>

**P60 — MapTiler SDK controls (chưa quan sát nhãn)** (library controls)

- **Hành động:** SDK default map
- **Trạng thái:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Đang tốt:** Mapcanvas định vị
- **Chưa tốt / P2:** Exactbuttons UNKNOWN, không invent zoom+/− labels
- **So ALOBO:** UNKNOWN / NOT OBSERVED — SDK MapTiler không được invent button labels.
- **Source / evidence:** [apps/partner-web/src/MapTilerPlacePicker.tsx:68](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L68). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Vận hành ACTIVE

<a id="p61"></a>

**P61 — Sân vận hành** (select)

- **Hành động:** GET operations+maintenance court
- **Trạng thái:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Đang tốt:** Bỏ kết quả tải về muộn của sân cũ, khóa chọn khi bận
- **Chưa tốt / P2:** Đổi sân mất form chưa lưu; thiếu lịch ngày mọi sân
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:79](../../apps/partner-web/src/PartnerOperations.tsx#L79). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p62"></a>

**P62 — Thử tải lại cấu hình** (button)

- **Hành động:** run loadcourt
- **Trạng thái:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Đang tốt:** Có nút thử lại khi tải lỗi
- **Chưa tốt / P2:** GET nhưng thông báo Đã lưu; nút chưa bị khóa riêng khi bận
- **So ALOBO:** GOOD + UNKNOWN — nhận xét riêng ShuttleBook, thiếu control tương ứng ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:84](../../apps/partner-web/src/PartnerOperations.tsx#L84). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p63"></a>

**P63 — Quy định đặt / Lịch tuần / Giá theo ngày / Xem thử giá / Bảo trì** (anchor family)

- **Hành động:** Scroll/focus5sections
- **Trạng thái:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Đang tốt:** Chuyển tới section và focus bàn phím
- **Chưa tốt / P2:** Form dài trên mobile cần thử thực tế; chưa có tab section hiện hành
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:90](../../apps/partner-web/src/PartnerOperations.tsx#L90). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Quy định ACTIVE

<a id="p64"></a>

**P64 — Block hiển thị** (select)

- **Hành động:** 30/60/90; minimum=maxblock
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Block chia theo ca 30 phút
- **Chưa tốt / P2:** Đổi block60→90 khi minimum120 chưa tự căn bội90
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:102](../../apps/partner-web/src/PartnerOperations.tsx#L102). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p65"></a>

**P65 — Thời lượng đặt tối thiểu / Giữ chỗ trước khi báo chuyển khoản** (number input family)

- **Hành động:** Minimumblock..480/hold5..60
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Giới hạn tối thiểu và thời gian giữ rõ
- **Chưa tốt / P2:** Chưa giải thích giữ báo giá120s khác giữ chờ thanh toán
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:107](../../apps/partner-web/src/PartnerOperations.tsx#L107). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p66"></a>

**P66 — Lưu quy định** (submit)

- **Hành động:** PUT booking-policy If-Match +load
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** If-Match chống ghi đè cấu hình
- **Chưa tốt / P2:** Thiếu tóm tắt tác động booking mới/cũ và lỗi theo trường
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:112](../../apps/partner-web/src/PartnerOperations.tsx#L112). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Lịch tuần ACTIVE

<a id="p67"></a>

**P67 — Ngày (giờ mở cửa)** (select family)

- **Hành động:** Set hoursweekday
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Đủ bảy ngày tiếng Việt
- **Chưa tốt / P2:** Thiếu sao chép và áp dụng nhiều ngày
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:120](../../apps/partner-web/src/PartnerOperations.tsx#L120). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p68"></a>

**P68 — Mở / Đóng** (time input family)

- **Hành động:** Set opens/closes30min
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Giờ theo múi giờ cơ sở
- **Chưa tốt / P2:** Một khoảng mỗi ngày chưa biểu đạt nghỉ giữa ngày
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:123](../../apps/partner-web/src/PartnerOperations.tsx#L123). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p69"></a>

**P69 — Xóa ngày** (button family)

- **Hành động:** Remove hoursrow local
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Xóa ở form trước khi lưu
- **Chưa tốt / P2:** Không hoàn tác, giá cùng ngày có thể còn lại
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:127](../../apps/partner-web/src/PartnerOperations.tsx#L127). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p70"></a>

**P70 — Thêm ngày mở** (button)

- **Hành động:** Append Mon08–10
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Thêm ngày hoạt động được
- **Chưa tốt / P2:** Mặc định thứ Hai dễ trùng, thiếu sao chép ngày
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:129](../../apps/partner-web/src/PartnerOperations.tsx#L129). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p71"></a>

**P71 — Ngày (giá cơ bản)** (select family)

- **Hành động:** Set pricesweekday
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Bảng giá riêng theo thứ và sân
- **Chưa tốt / P2:** Không áp dụng nhóm T2–T6/cuối tuần
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:133](../../apps/partner-web/src/PartnerOperations.tsx#L133). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p72"></a>

**P72 — Từ / Đến / Giá/30 phút** (input family)

- **Hành động:** Set priceswindow+amount
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Hiển thị giá quy đổi theo giờ
- **Chưa tốt / P2:** JS Number cực trị>2^53 cần kiểm roundtrip; lỗi phủ giá chung
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:136](../../apps/partner-web/src/PartnerOperations.tsx#L136). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p73"></a>

**P73 — Xóa giá** (button family)

- **Hành động:** Remove pricelocal
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Xóa ở form trước khi lưu
- **Chưa tốt / P2:** Thiếu hoàn tác và xem phạm vi giá còn lại
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:143](../../apps/partner-web/src/PartnerOperations.tsx#L143). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p74"></a>

**P74 — Thêm khung giá** (button)

- **Hành động:** Append Mon08–10 price100000
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Thêm nhiều khung giá được
- **Chưa tốt / P2:** Mặc định dễ trùng giờ, chưa nhân bản khung
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:145](../../apps/partner-web/src/PartnerOperations.tsx#L145). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p75"></a>

**P75 — Lưu lịch tuần** (submit)

- **Hành động:** PUT hours/prices If-Match
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Lưu giờ và giá cùng phiên bản
- **Chưa tốt / P2:** Thiếu so sánh trước/sau, lỗi không gắn field
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:147](../../apps/partner-web/src/PartnerOperations.tsx#L147). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Giá ngày ACTIVE

<a id="p76"></a>

**P76 — Thứ** (select family)

- **Hành động:** Set ruleweekday
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Quy tắc theo ngày và thứ
- **Chưa tốt / P2:** Ngày lễ một ngày vẫn phải chọn đúng thứ thủ công
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:160](../../apps/partner-web/src/PartnerOperations.tsx#L160). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p77"></a>

**P77 — Hiệu lực từ / Đến ngày / Từ / Đến / Giá/30 phút / Ưu tiên** (input family)

- **Hành động:** Requiredrulefields priority1–1000
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Hiệu lực và giá quy đổi rõ
- **Chưa tốt / P2:** Độ ưu tiên khó hiểu; chưa xem quy tắc thắng từng ca
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:156](../../apps/partner-web/src/PartnerOperations.tsx#L156). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p78"></a>

**P78 — Xóa quy tắc** (button family)

- **Hành động:** Remove rulelocal
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Xóa trước khi lưu
- **Chưa tốt / P2:** Chưa hoàn tác hoặc tóm tắt tác động
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:172](../../apps/partner-web/src/PartnerOperations.tsx#L172). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p79"></a>

**P79 — Thêm quy tắc giá** (button)

- **Hành động:** Append today weekdayMon08–10
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Hỗ trợ giá ngày đặc biệt
- **Chưa tốt / P2:** Mặc định thứ Hai có thể không khớp ngày hôm nay
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:174](../../apps/partner-web/src/PartnerOperations.tsx#L174). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p80"></a>

**P80 — Lưu giá theo ngày** (submit)

- **Hành động:** PUT pricing-rules If-Match+load
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Backend kiểm độ ưu tiên/phiên bản
- **Chưa tốt / P2:** Thiếu bảng giá hiệu lực và lỗi theo trường
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:178](../../apps/partner-web/src/PartnerOperations.tsx#L178). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Xem thử giá ACTIVE

<a id="p81"></a>

**P81 — Ngày / Từ / Đến** (date/time input family)

- **Hành động:** Set preview/maintenance sharedstate
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Bắt buộc ngày và giờ theo ca30 phút
- **Chưa tốt / P2:** Xem giá và bảo trì chung state, đổi một bên đổi bên kia
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:186](../../apps/partner-web/src/PartnerOperations.tsx#L186). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p82"></a>

**P82 — Xem giá** (submit)

- **Hành động:** GET price-preview total/count
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Giá tính tại server
- **Chưa tốt / P2:** GET hiện Đã lưu; không hiển thị giá/ưu tiên từng ca dù API có
- **So ALOBO:** PARITY-PARTIAL A04 — cùng nhóm giá; quy trình chi tiết/loading chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:189](../../apps/partner-web/src/PartnerOperations.tsx#L189). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Bảo trì ACTIVE

<a id="p83"></a>

**P83 — Ngày / Từ / Đến / Lý do** (input family)

- **Hành động:** Maintenance date/time/reason5–500
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Múi giờ rõ, lý do5–500 ký tự
- **Chưa tốt / P2:** Chung state xem giá; không có preview lịch trước khóa
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:199](../../apps/partner-web/src/PartnerOperations.tsx#L199). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p84"></a>

**P84 — Khóa ca bảo trì** (submit)

- **Hành động:** POST maintenance+load
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** DB chống đè booking hoặc quote còn hạn
- **Chưa tốt / P2:** Thiếu tóm tắt sân/ngày/giờ xác nhận và lịch ngày owner
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:203](../../apps/partner-web/src/PartnerOperations.tsx#L203). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p85"></a>

**P85 — Hủy bảo trì** (button family)

- **Hành động:** POST maintenance/id/cancel+load
- **Trạng thái:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Đang tốt:** Hủy bảo trì riêng, trả trống khi hợp lệ
- **Chưa tốt / P2:** Không xác nhận trước thao tác giải phóng, chưa hoàn tác
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/PartnerOperations.tsx:210](../../apps/partner-web/src/PartnerOperations.tsx#L210). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Đơn ACTIVE

<a id="p86"></a>

**P86 — Cơ sở xem đơn** (select)

- **Hành động:** Close detail/resetfilters/scope
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Generation/abort chặn dữ liệu scope cũ
- **Chưa tốt / P2:** Không khóa selector lúc quyết định đang gửi; server có thể commit sau khi panel đóng. Đây là source-risk về thông báo kết quả, chưa chứng minh lộ dữ liệu
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:130](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L130). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p87"></a>

**P87 — Chờ xác nhận** (count button)

- **Hành động:** Apply awaiting filter
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Một click tới hàng đợi cần xử lý
- **Chưa tốt / P2:** Phạm vi count/ngày chưa rõ; thiếu nút Tất cả
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:137](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L137). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p88"></a>

**P88 — Cần bổ sung** (count button)

- **Hành động:** Apply reviewfilter
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Hàng đợi cần bổ sung riêng
- **Chưa tốt / P2:** Giữ bộ lọc ngày; thiếu nút Tất cả/reset
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:139](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L139). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p89"></a>

**P89 — Trạng thái đơn** (select)

- **Hành động:** Draft6states+All; applyLọc
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Sáu trạng thái đúng nghiệp vụ
- **Chưa tốt / P2:** Chưa lọc quá hạn/loại đơn/tìm mã đơn
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:143](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L143). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p90"></a>

**P90 — Từ ngày / Đến ngày** (date input family)

- **Hành động:** Draftrange ≤366days
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Giải thích múi giờ và lịch cố định khớp bất kỳ buổi
- **Chưa tốt / P2:** Thiếu Hôm nay/Tuần này/xóa ngày nhanh
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:145](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L145). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p91"></a>

**P91 — Lọc đơn** (submit)

- **Hành động:** Applydraftfilters+GET
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Kiểm ngày ngược và tối đa366 ngày trước API
- **Chưa tốt / P2:** Thiếu xóa bộ lọc; không khóa khi quyết định đang gửi
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:147](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L147). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p92"></a>

**P92 — Tải lại đơn** (button)

- **Hành động:** Reload list/detail
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Khóa khi đang tải hoặc tải thêm
- **Chưa tốt / P2:** Chưa khóa lúc quyết định đang gửi; reload có thể đóng form và xóa intent. Cần test kết quả server; đồng thời reset các trang đã tải
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:147](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L147). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p93"></a>

**P93 — Xem đơn {bookingNo}** (button family)

- **Hành động:** Deep linkbookingId+GET detail
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Mã đơn rõ, aria-expanded và tiền exact
- **Chưa tốt / P2:** Không scroll/focus panel chi tiết dưới danh sách dài
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:157](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L157). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p94"></a>

**P94 — Xem thêm đơn** (button)

- **Hành động:** Cursor appenddedup
- **Trạng thái:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Đang tốt:** Có cursor, dedup và guard đổi scope
- **Chưa tốt / P2:** Polling5s thay bằng trang đầu, bỏ các trang đã tải thêm. Source xác nhận; reviewer chưa tái hiện runtime mới
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:161](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L161). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Chi tiết đơn

<a id="p95"></a>

**P95 — Đóng chi tiết** (button)

- **Hành động:** ClearbookingId
- **Trạng thái:** bookingId hash, detailLoading/error/retry/poll5s; skip actionbusy và version cũ; authorizedbusiness switching.403/404 xóa cache. Close/reload chưa khóa actionBusy.
- **Đang tốt:** Đóng panel không hủy booking
- **Chưa tốt / P2:** Đóng khi gửi chỉ abort phía UI; server có thể commit, cần xem lại kết quả rõ
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:164](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L164). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p96"></a>

**P96 — Tải lại chi tiết (fetch lỗi)** (button)

- **Hành động:** setReload
- **Trạng thái:** bookingId hash, detailLoading/error/retry/poll5s; skip actionbusy và version cũ; authorizedbusiness switching.403/404 xóa cache. Close/reload chưa khóa actionBusy.
- **Đang tốt:** 403/404 xóa dữ liệu chi tiết private, có retry
- **Chưa tốt / P2:** Nút chưa khóa loading; tải lại cả danh sách mất trang đã thêm
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:166](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L166). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Biên lai

<a id="p97"></a>

**P97 — Xem biên lai** (button family)

- **Hành động:** BearerGET /uploads/id/view→blob
- **Trạng thái:** Có proofURL hợp lệ private endpoint; Xem khóa loading/nhãn Đang tải biên lai…; erroralert; blob show/hide+unmountcleanup. Khôngpubliclink.
- **Đang tốt:** Validate private path, gửi Bearer và tải theo yêu cầu
- **Chưa tốt / P2:** Thiếu zoom/tải ảnh; ảnh biên lai điện thoại cần thử độ dễ đọc
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:204](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L204). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p98"></a>

**P98 — Ẩn biên lai** (button family)

- **Hành động:** RevokeobjectURL/hide
- **Trạng thái:** Có proofURL hợp lệ private endpoint; Xem khóa loading/nhãn Đang tải biên lai…; erroralert; blob show/hide+unmountcleanup. Khôngpubliclink.
- **Đang tốt:** Ẩn/đóng component giải phóng blobURL
- **Chưa tốt / NONE:** Chưa kiểm zoom bàn phím/screen reader; control tương đương ALOBO UNKNOWN
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/PartnerBookings.tsx:203](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L203). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Quyết định

<a id="p99"></a>

**P99 — Quyết định** (select)

- **Hành động:** CONFIRMED/NEEDS_REVIEW/FINAL_REJECTION
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Chỉ có form ở chờ xác nhận/cần bổ sung
- **Chưa tốt / P2:** Mặc định xác nhận và prefill tiền có thể khuyến khích click nhanh, cần đào tạo đối chiếu ngân hàng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:91](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L91). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p100"></a>

**P100 — Số tiền thực nhận (đ) / Ghi chú đối chiếu (không bắt buộc)** (input/textarea family)

- **Hành động:** ExactBigIntdigits18/note1000
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Sai tiền không POST, BigInt bảo toàn số lớn
- **Chưa tốt / P2:** Prefill tổng tiền không chứng minh thực nhận; phải kiểm ngân hàng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:95](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L95). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p101"></a>

**P101 — Lý do đối chiếu** (select)

- **Hành động:** 4reasoncodes
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Lý do cấu trúc, không ép mã giao dịch
- **Chưa tốt / NONE:** Không phát hiện gap source riêng; exact control ALOBO UNKNOWN
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:99](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L99). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p102"></a>

**P102 — Nội dung gửi khách** (textarea)

- **Hành động:** Requiredreason≤1000
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Bắt buộc owner giải thích; lưu audit và gửi thông báo
- **Chưa tốt / P2:** Thiếu mẫu lý do; chất lượng lời nhắn cần người dùng đọc
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:101](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L101). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p103"></a>

**P103 — Tôi xác nhận không chấp nhận giao dịch và giải phóng {khung giờ / toàn bộ N buổi}** (checkbox)

- **Hành động:** Explicitfinalrejectionack
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Nói rõ giải phóng cả kỳ và tính cuối cùng
- **Chưa tốt / NONE:** Không undo đúng terminal policy; không coi thiếu undo booking là bug
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:104](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L104). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p104"></a>

**P104 — Xác nhận đã nhận đủ tiền** (submit branch)

- **Hành động:** POST confirm-payment If-Match/idempotency
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Retry cùng intent; đủ100%; PAID/CONFIRMED kết thúc
- **Chưa tốt / NONE:** Phải kiểm giao dịch ngân hàng thật; ảnh không tự chứng minh PAID
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p105"></a>

**P105 — Gửi yêu cầu bổ sung** (submit branch)

- **Hành động:** POST reject-payment NEEDS_REVIEW
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Giữ cả kỳ, thông báo outbox, không hết hạn do owner chậm
- **Chưa tốt / P2:** Chưa chat; không tự đổi review policy thành chat
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p106"></a>

**P106 — Xác nhận từ chối cuối cùng** (submit branch)

- **Hành động:** POST FINAL_REJECTION
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** Màu nguy hiểm, checkbox+lý do, giải phóng cả kỳ
- **Chưa tốt / NONE:** Từ chối cuối cùng không undo theo policy; cần phân biệt với bổ sung
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p107"></a>

**P107 — Tải lại chi tiết (quyết định stale)** (button)

- **Hành động:** Clearintent/reloadversion
- **Trạng thái:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Đang tốt:** 412/state conflict không tự gửi lại
- **Chưa tốt / P2:** Nút chưa khóa loading riêng; phải đọc trạng thái mới trước submit
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thanh toán/đối chiếu; chi tiết button/state chưa thử bên ALOBO.
- **Source / evidence:** [apps/partner-web/src/features/bookings/BookingDecisions.tsx:111](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L111). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Cố định detail

<a id="p108"></a>

**P108 — Danh sách buổi, cuộn ngang khi cần** (scrollable region)

- **Hành động:** Keyboardtable occurrence dates/amounts/status
- **Trạng thái:** Series có occurrences: table ngày/giờ/giá/trạng thái, tabindex0 scroll region. Không mutation từng buổi.
- **Đang tốt:** Bảng cuộn có focus, một quyết định cả kỳ
- **Chưa tốt / NONE:** Không xác nhận từng buổi/check-in theo đúng phạm vi
- **So ALOBO:** PARITY-PARTIAL A02/A03/A07 — cùng nhóm lịch; policy/geometry/control cụ thể chưa đối chiếu trực tiếp.
- **Source / evidence:** [apps/partner-web/src/features/bookings/SeriesSchedule.tsx:16](../../apps/partner-web/src/features/bookings/SeriesSchedule.tsx#L16). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

#### Thông báo

<a id="p109"></a>

**P109 — Tải lại thông báo** (button)

- **Hành động:** GET firstpage
- **Trạng thái:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Đang tốt:** Khóa khi tải, lỗi/empty/retry rõ
- **Chưa tốt / P2:** Thiếu thời điểm cập nhật; trang đầu bỏ lịch sử đã tải thêm
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:51](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L51). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p110"></a>

**P110 — Xem đơn đặt sân** (button family)

- **Hành động:** Openbooking+markread
- **Trạng thái:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Đang tốt:** UUID/action hợp lệ, đi đúng business được phép
- **Chưa tốt / P2:** Đánh dấu đọc song song lỗi cần feedback rõ; đây không tạo booking
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:58](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L58). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p111"></a>

**P111 — Đã đọc** (button family)

- **Hành động:** POSTread+refresh
- **Trạng thái:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Đang tốt:** Chỉ unread có nút, khóa theo notice đang xử lý, badge toàn bộ
- **Chưa tốt / P2:** Nhãn Đã đọc dễ hiểu như trạng thái thay hành động; thiếu đánh dấu tất cả/lọc chưa đọc
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:59](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L59). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

<a id="p112"></a>

**P112 — Thông báo trước đó** (button)

- **Hành động:** Cursor appenddedup
- **Trạng thái:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Đang tốt:** Có cursor/dedup và khóa loading
- **Chưa tốt / P2:** Polling5s thay trang đầu, mất thông báo cũ và cursor. Source xác nhận; reviewer chưa chạy runtime riêng
- **So ALOBO:** PARITY-PARTIAL A03 — cùng nhóm thông báo; pagination/retry/rights bên ALOBO chưa thử.
- **Source / evidence:** [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:63](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L63). SOURCE_CONFIRMED; reviewer không chạy runtime mới. Root có browser gate mới riêng; không coi source là PASS.

### Admin

#### Đăng nhập

<a id="a01"></a>

**A01 — Phương thức liên hệ · Email / Số điện thoại** (select)

- **Hành động:** Đổi contactType email/phone, không gọi API; đổi nhãn và validation contact.
- **Trạng thái:** Chỉ khi restore xong và chưa có session; disabled busy.
- **Đang tốt:** Phương thức rõ, label htmlFor và native select hỗ trợ keyboard.
- **Chưa tốt / NONE:** NONE — giữ; không coi lựa chọn Email/phone là thiếu sót.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/main.tsx:202](../../apps/admin-web/src/main.tsx#L202). SOURCE_CONFIRMED; focused runtime của record NOT RUN

<a id="a02"></a>

**A02 — Email / Số điện thoại E.164** (input email/tel)

- **Hành động:** Cập nhật contact; trim trước POST /api/v1/admin-auth/login.
- **Trạng thái:** Conditional nhãn theo contactType; disabled busy; lỗi validation ở status chung.
- **Đang tốt:** autoComplete=username; validation email/E.164 trước API; không hiển thị khác biệt tồn tại tài khoản.
- **Chưa tốt / P2:** P2 đề xuất: lỗi validation chỉ chung, không aria-invalid/error riêng từng field; số E.164 có thể khó nhập với người dùng quen 09xxx.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/main.tsx:206](../../apps/admin-web/src/main.tsx#L206). SOURCE_CONFIRMED; tests/web/admin-identity.spec.ts existing assertions; focused runtime của record NOT RUN

<a id="a03"></a>

**A03 — Mật khẩu** (input password)

- **Hành động:** Cập nhật password; gửi cùng contact khi Đăng nhập.
- **Trạng thái:** disabled busy; clear password sau response/error; không lưu storage.
- **Đang tốt:** autoComplete=current-password; hỗ trợ password manager, password không retained sau lỗi network/API.
- **Chưa tốt / P2:** P2 đề xuất UX: chưa có Hiện/Ẩn mật khẩu; không phải lỗi auth/backend.
- **So ALOBO:** GAP A11 — tiện ích nhập mật khẩu; auth backend chưa so sánh.
- **Source / evidence:** [apps/admin-web/src/main.tsx:210](../../apps/admin-web/src/main.tsx#L210). SOURCE_CONFIRMED; focused runtime của record NOT RUN

<a id="a04"></a>

**A04 — Đăng nhập / Đang đăng nhập…** (button submit)

- **Hành động:** POST /api/v1/admin-auth/login credentials include; validate ADMIN ACTIVE; GET /api/v1/admin-auth/me; mở AdminWorkspace.
- **Trạng thái:** disabled busy; status generic invalid credentials, network và 429 Retry-After tính phút; restoring chỉ hiện Đang kiểm tra phiên quản trị…; F5 restore cookie; idle30 phút từ thao tác.
- **Đang tốt:** Chặn double-submit, non-Admin response không vào workspace, safe errors, memory token + HttpOnly restore; policy idle xử lý pointer/keydown thay polling.
- **Chưa tốt / P2:** P2: 429 chỉ ghi số phút, button enabled lại ngay; chưa countdown/lockout UX. Lỗi API bộ phận 401/403 yêu cầu đăng nhập lại nhưng shell không trực tiếp đưa login: cần Đăng xuất trước.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/main.tsx:212](../../apps/admin-web/src/main.tsx#L212). SOURCE_CONFIRMED; historical browser/live evidence docs/progress.md + tests/web/admin-identity.spec.ts/admin-live.spec.ts; focused runtime của record NOT RUN

#### Điều hướng desktop/mobile

<a id="a05"></a>

**A05 — Đến nội dung chính** (link skip)

- **Hành động:** preventDefault; focus #admin-main, không đổi hash route.
- **Trạng thái:** Focus-visible; chỉ workspace; keyboard Enter; main tabIndex=-1.
- **Đang tốt:** Có lối bỏ navigation, không làm mất #approvals; icon decorative aria-hidden.
- **Chưa tốt / NONE:** NONE — giữ; chưa chấm screen-reader/contrast vì chưa đo mới.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:22](../../apps/admin-web/src/layouts/AdminShell.tsx#L22). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:77 asserts skip focus/hash; focused runtime của record NOT RUN

<a id="a06"></a>

**A06 — Đăng xuất / Đang đăng xuất…** (button family desktop+mobile)

- **Hành động:** Xóa local session/increment generation trước POST /api/v1/auth/logout refreshToken + bearer; finally giữ session null.
- **Trạng thái:** Chỉ workspace; disabled busy; mobile header hoặc sidebar footer; late refresh không revive UI; network logout catch vẫn xóa local.
- **Đang tốt:** Không token storage, clear private workspace, late response generation guard; logout dễ thấy ở cả viewport.
- **Chưa tốt / P2:** SOURCE_RISK/P2: network logout failure vẫn nói Đã đăng xuất dù server chưa xác nhận revoke; cần diễn tập offline logout→online F5 và đối chiếu cookie/session. Không tuyên bố runtime bug.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:27](../../apps/admin-web/src/layouts/AdminShell.tsx#L27); [apps/admin-web/src/layouts/AdminShell.tsx:42](../../apps/admin-web/src/layouts/AdminShell.tsx#L42); [apps/admin-web/src/main.tsx:174](../../apps/admin-web/src/main.tsx#L174). SOURCE_CONFIRMED; historical admin live/browser logout/reload; offline scenario new NOT RUN

<a id="a07"></a>

**A07 — Mở menu quản trị / Đóng menu quản trị** (icon button toggle)

- **Hành động:** setMenuOpen; Escape đóng và focus lại opener; đổi route đóng menu.
- **Trạng thái:** Mobile <=800px; aria-controls, aria-expanded, aria-label; icon decorative.
- **Đang tốt:** Có tên accessible, touch target44px; không fake dialog/focus trap cho nav inline.
- **Chưa tốt / NONE:** NONE — giữ; pointer-outside close không bắt buộc với menu inline.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:28](../../apps/admin-web/src/layouts/AdminShell.tsx#L28). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:126 width375/768/1024/1440 geometry and Escape assertions; focused runtime của record NOT RUN

<a id="a08"></a>

**A08 — ShuttleBook Quản trị** (brand link)

- **Hành động:** href #overview; SPA hash navigation không document reload.
- **Trạng thái:** Desktop sidebar; mobile brand là span không clickable.
- **Đang tốt:** Brand desktop quay tổng quan không mất session memory.
- **Chưa tốt / NONE:** NONE — giữ; mobile đã có nav Tổng quan, brand không clickable là lựa chọn hợp lệ.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:34](../../apps/admin-web/src/layouts/AdminShell.tsx#L34). SOURCE_CONFIRMED; focused runtime của record NOT RUN

<a id="a09"></a>

**A09 — Tổng quan** (navigation link)

- **Hành động:** href #overview; useAdminNavigation đọc hash/change; giữ mounted approval/notification state.
- **Trạng thái:** aria-current=page khi active; mobile menu đóng sau click.
- **Đang tốt:** Back/Forward/hash route; drafts giữ khi đi giữa trang; không background polling gia hạn idle.
- **Chưa tốt / P2:** P2 đề xuất: route đổi chưa explicit focus h1/scroll policy; cần kiểm keyboard thật, không coi source thiếu là lỗi runtime chắc chắn.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:5](../../apps/admin-web/src/features/workspace/navigation.ts#L5). SOURCE_CONFIRMED; existing navigation history assertions; focus-on-route new NOT RUN

<a id="a10"></a>

**A10 — Duyệt hồ sơ** (navigation link)

- **Hành động:** href #approvals; chỉ hiện approval panel, notification vẫn mounted hidden.
- **Trạng thái:** aria-current khi active; route không API riêng, mounted component giữ state.
- **Đang tốt:** Không duplicate notice source; draft lý do giữ qua Back/Forward.
- **Chưa tốt / P2:** P2 cùng A09: chưa explicit route focus/scroll; không tự cho Admin xác nhận tiền.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:6](../../apps/admin-web/src/features/workspace/navigation.ts#L6). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:77; focused runtime của record NOT RUN

<a id="a11"></a>

**A11 — Thông báo** (navigation link)

- **Hành động:** href #notifications; ẩn approval panel nhưng giữ mounted detail/draft.
- **Trạng thái:** aria-current khi active; không reload chỉ vì hash/focus window.
- **Đang tốt:** Thông báo một nguồn, không auto GET keep-alive trái idle30 phút.
- **Chưa tốt / P2:** P2 cùng A09: focus/scroll khi route đổi cần xác minh.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:7](../../apps/admin-web/src/features/workspace/navigation.ts#L7). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:146 idle background assertion; focused runtime của record NOT RUN

#### Tổng quan

<a id="a12"></a>

**A12 — Hồ sơ chờ xử lý · {count} · Trong danh sách đã tải · Mở danh sách hồ sơ** (metric card link)

- **Hành động:** href #approvals; count từ rows.length của GET pending approval list.
- **Trạng thái:** Đang tải… / Chưa tải được / số thật; không fake analytics; count tối đa100 từ backend.
- **Đang tốt:** Ghi rõ Trong danh sách đã tải, lỗi không giả thành0; link mở đúng queue.
- **Chưa tốt / P2:** P2 volume: không có tổng toàn hệ thống/cursor nên >100 còn nhiều hồ sơ nhưng metric không phải total; đã có caption giảm hiểu nhầm.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/features/workspace/AdminWorkspace.tsx:20](../../apps/admin-web/src/features/workspace/AdminWorkspace.tsx#L20); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:419](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L419). SOURCE_CONFIRMED; existing overview/failure assertions; >100 focused runtime của record NOT RUN

<a id="a13"></a>

**A13 — Thông báo chưa đọc · {count} · Xem thông báo** (metric card link)

- **Hành động:** href #notifications; unreadCount API scoped, fallback loaded rows count khi thiếu field.
- **Trạng thái:** Đang tải… / Chưa tải được / count; updated mark-read reload.
- **Đang tốt:** Server unreadCount gồm cả nhiều trang; một notifications component cho summary/list.
- **Chưa tốt / NONE:** NONE — giữ; không suy ALOBO có/không cùng summary.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/features/workspace/AdminWorkspace.tsx:21](../../apps/admin-web/src/features/workspace/AdminWorkspace.tsx#L21). SOURCE_CONFIRMED; existing overview unread1→0 assertion; focused runtime của record NOT RUN

#### Danh sách hồ sơ chờ duyệt

<a id="a14"></a>

**A14 — Tải lại danh sách** (button)

- **Hành động:** GET /api/v1/admin/approval-requests/; abort previous list; giữ selected/detail trừ denied; backend chỉ Admin ACTIVE.
- **Trạng thái:** disabled busy||loading; loading/empty/error distinct; 401/403 xóa private rows/detail/images; không polling.
- **Đang tốt:** Retry rõ, abort/generation guard response cũ; không biến network failure thành empty.
- **Chưa tốt / P2:** P2 volume: chưa search/filter/cursor cho pending list max100; STATE_CONFLICT/VERSION_CONFLICT lỗi chỉ code chung, refresh list không có hướng dẫn stale-specific.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:150](../../apps/admin-web/src/AdminApprovals.tsx#L150). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:109/:162 assertions; focused runtime của record NOT RUN

<a id="a15"></a>

**A15 — {businessName} · Hồ sơ mới / Thay đổi cơ sở · {submittedAt}** (row button family)

- **Hành động:** GET /api/v1/admin/approval-requests/{id}; clear old selection/reason/private URLs before fetch.
- **Trạng thái:** disabled busy||detailLoading; selected style; status Đang tải chi tiết hồ sơ…; malformed snapshot error, no decision controls.
- **Đang tốt:** Kind badge/date/selected state; defensive snapshot parse; business/court schedule details sau click.
- **Chưa tốt / P2:** P2 UX: chọn hồ sơ khác xóa reason draft ngay, chưa unsaved-warning; tên row dựa businessName, revision nhiều cơ sở chưa có venueName trong row.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:155](../../apps/admin-web/src/AdminApprovals.tsx#L155). SOURCE_CONFIRMED; existing deep-link/detail/refresh-mid-fetch assertions; focused runtime của record NOT RUN

#### Chi tiết hồ sơ mới / thay đổi cơ sở

<a id="a16"></a>

**A16 — Đóng chi tiết** (button)

- **Hành động:** setSelected(null), setReason(''), clear/revoke private image URLs.
- **Trạng thái:** Chỉ parsed profile/revision visible; disabled busy.
- **Đang tốt:** Đóng nhanh, xóa ảnh riêng và reason; không thay đổi backend.
- **Chưa tốt / P2:** P2: không cảnh báo reason chưa gửi sẽ mất; malformed snapshot không render close button nhưng row/reload vẫn có.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:166](../../apps/admin-web/src/AdminApprovals.tsx#L166). SOURCE_CONFIRMED; focused runtime của record NOT RUN

#### Chi tiết thay đổi cơ sở

<a id="a17"></a>

**A17 — Xem QR mới / Đang tải ảnh…** (button private image)

- **Hành động:** GET /api/v1/uploads/{qrUploadId}/view bearer; blob URL QR mới inline width240; revoke on switch/denied/unmount.
- **Trạng thái:** disabled any imageBusy||busy; revision only; error generic; private permissions checked backend.
- **Đang tốt:** Không public URL/cache token; QR proposed hiển thị riêng, blob cleanup.
- **Chưa tốt / P2:** P2 decision UX: revision chỉ proposed address/contact/timezone/location/bank/QR; chưa hiển thị hiện hành→đề nghị hay highlight đổi tài khoản; ảnh chưa zoom/download. Không có bằng chứng ALOBO Admin nên UNKNOWN comparison.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:171](../../apps/admin-web/src/AdminApprovals.tsx#L171); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146). SOURCE_CONFIRMED; focused runtime của record NOT RUN

#### Chi tiết hồ sơ mới

<a id="a18"></a>

**A18 — Xem ảnh cơ sở / Đang tải ảnh…** (button private image family per venue)

- **Hành động:** GET /api/v1/uploads/{imageUploadId}/view bearer; show inline blob ảnh venue.
- **Trạng thái:** disabled any imageBusy||busy; visible each venue; image failed shows error; image401/403 denyAccess.
- **Đang tốt:** Xem ảnh được cấp quyền; tránh URL public; access denied xóa ảnh/profile; alt Ảnh {venue.name}.
- **Chưa tốt / P2:** P2 proposal: inline image width240 chưa mở lớn/zoom; không thể kết luận nhỏ khó đọc nếu chưa xem screenshot ảnh thực.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:181](../../apps/admin-web/src/AdminApprovals.tsx#L181); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:162 load blob+deny purge; focused runtime của record NOT RUN

<a id="a19"></a>

**A19 — Xem QR / Đang tải ảnh…** (button private image family per venue)

- **Hành động:** GET /api/v1/uploads/{qrUploadId}/view bearer; QR inline blob.
- **Trạng thái:** disabled any imageBusy||busy; alt QR {venue.name}; switch/unmount cleanup.
- **Đang tốt:** QR/tài khoản/giá ca đặt trong cùng hồ sơ, Admin xem trước publish; không xác nhận payment.
- **Chưa tốt / P2:** P2 proposal cùng A18: chưa zoom/download QR; đòi hỏi thao tác zoom browser khi ảnh nhiều chi tiết, cần test tay.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:181](../../apps/admin-web/src/AdminApprovals.tsx#L181); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146). SOURCE_CONFIRMED; focused runtime của record NOT RUN

#### Chi tiết hồ sơ mới / thay đổi cơ sở

<a id="a20"></a>

**A20 — Phê duyệt / Đang lưu…** (button mutation)

- **Hành động:** POST /api/v1/admin/approval-requests/{id}/approve; backend Admin ACTIVE, serializable+FOR UPDATE+state/version/media/schedule guards; success clear detail/reload Đã lưu quyết định.
- **Trạng thái:** Only selected.status=PENDING + parsed valid profile/revision; disabled busy; 409 conflict renders code generic, no optimistic approve.
- **Đang tốt:** Server guards/audit/outbox; frontend busy duplicate guard; đúng quyền duyệt chứ không nhận tiền.
- **Chưa tốt / P2:** P2 operational UX: một click gửi ngay, chưa review/confirm tên/phạm vi/QR; VERSION_CONFLICT/STATE_CONFLICT chưa giải thích tiếng Việt/hướng refresh detail. Không khẳng định thiếu API idempotency là double-write bug vì server status lock.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:196](../../apps/admin-web/src/AdminApprovals.tsx#L196); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:432](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L432). SOURCE_CONFIRMED; prior integrated F02 approval gate in docs/progress.md; focused runtime của record NOT RUN

<a id="a21"></a>

**A21 — Lý do cần chỉnh sửa** (textarea)

- **Hành động:** Cập nhật reason draft; chỉ gửi khi click Yêu cầu chỉnh sửa.
- **Trạng thái:** Only pending; disabled busy; helper tối thiểu10 ký tự; draft giữ giữa hash pages, reset close/switch/success.
- **Đang tốt:** htmlFor/useId/aria-describedby, yêu cầu reason thay silent reject; phản hồi owner qua outbox.
- **Chưa tốt / P2:** P2 VALIDATION SOURCE: không maxLength/counter1000 trong UI; >1000 vẫn enabled dù backend TextOk10–1000 sẽ trả 400 VALIDATION_FAILED; lack field-specific message.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:197](../../apps/admin-web/src/AdminApprovals.tsx#L197); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:511](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L511). SOURCE_CONFIRMED; existing draft preservation/requestbody assertion; >1000 runtime NOT RUN

<a id="a22"></a>

**A22 — Yêu cầu chỉnh sửa** (button mutation)

- **Hành động:** POST /api/v1/admin/approval-requests/{id}/request-changes {reason}; backend pending transition, owner notification/audit; onboarding returns draft.
- **Trạng thái:** disabled busy||reason.trim().length<10; busy text vẫn Yêu cầu chỉnh sửa (section aria-busy); success clear/reload; failures preserve reason unless deny.
- **Đang tốt:** Không gửi reason ngắn, busy duplicate guard, decision ghi backend/outbox; không tự thêm reject/cancel booking.
- **Chưa tốt / P2:** P2: missing >1000 frontend bound (A21); mutation lỗi chỉ code chung; button này không đổi Đang lưu… riêng nên loading nhìn kém rõ hơn approve.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminApprovals.tsx:200](../../apps/admin-web/src/AdminApprovals.tsx#L200). SOURCE_CONFIRMED; existing tests/web/f07-admin-ui.spec.ts:77 checks exact body/success; focused runtime của record NOT RUN

#### Thông báo

<a id="a23"></a>

**A23 — Làm mới thông báo** (button)

- **Hành động:** GET /api/v1/me/notifications first page; replace list, server unreadCount+cursor; abort previous load.
- **Trạng thái:** disabled busy||reading; loading vs empty/error;401/403clearcache; no polling/focus-refresh.
- **Đang tốt:** Chủ động refresh giữ idle policy; retry rõ; no hidden timer renew; scope backend.
- **Chưa tốt / P2:** P2 proposal: chưa last-updated indicator/unread filter; first page refresh intentionally reset page window, cần phân biệt với mark-read reset A24.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminNotifications.tsx:67](../../apps/admin-web/src/AdminNotifications.tsx#L67). SOURCE_CONFIRMED; existing f06-admin retry+idle and f07 failure tests; focused runtime của record NOT RUN

<a id="a24"></a>

**A24 — Đánh dấu đã đọc / Đang cập nhật…** (button family per unread notice)

- **Hành động:** POST /api/v1/me/notifications/{id}/read; on success await load() WITHOUT cursor => replace items first page.
- **Trạng thái:** Only !readAt;disabled reading||busy; loading per id; server read idempotent; denied clears items; failure shows retry message.
- **Đang tốt:** Đọc rõ, unread counter authoritative; alert text nói Admin không nhận tiền/khung giờ vẫn giữ.
- **Chưa tốt / P2:** P2 SOURCE_CONFIRMED: sau Xem thêm, đánh dấu một notification trang2 làm list quay về50 bản đầu; mất vị trí lịch sử (not yet runtime). ADMIN_PAYMENT_ALERT chỉ nhắc Liên hệ chủ sân, không có link/contact lookup/action tới owner; chưa có admin scoped contact flow, không tự mở payment rights.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminNotifications.tsx:76](../../apps/admin-web/src/AdminNotifications.tsx#L76); [apps/admin-web/src/AdminNotifications.tsx:60](../../apps/admin-web/src/AdminNotifications.tsx#L60); [apps/admin-web/src/AdminNotifications.tsx:41](../../apps/admin-web/src/AdminNotifications.tsx#L41). SOURCE_CONFIRMED; existing unread1→0; paged scenario focused runtime của record NOT RUN

<a id="a25"></a>

**A25 — Xem thêm thông báo** (button pagination)

- **Hành động:** GET /api/v1/me/notifications?before={nextCursor}; append unique ids; update cursor/server unreadCount.
- **Trạng thái:** Visible next!=null;disabled busy||reading; paging error preserves old rows; no separate loading label on this button (section aria-busy).
- **Đang tốt:** Cursor+dedup append, không tải tất cả, errors retry; thông báo không thêm xác nhận tiền Admin.
- **Chưa tốt / P2:** P2 phối hợp A24: loaded older page mất sau mark-read; chưa visible Đang tải thêm…/pagecount và filter. Backend default50,max100; không tạo gap bắt buộc mark-allread.
- **So ALOBO:** UNKNOWN — không có account Admin ALOBO; đánh giá bằng nghiệp vụ ShuttleBook.
- **Source / evidence:** [apps/admin-web/src/AdminNotifications.tsx:78](../../apps/admin-web/src/AdminNotifications.tsx#L78). SOURCE_CONFIRMED; paging runtime new NOT RUN

