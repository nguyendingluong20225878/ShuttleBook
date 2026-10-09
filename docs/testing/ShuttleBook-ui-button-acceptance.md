# ShuttleBook — checklist nghiệm thu từng control sau audit ALOBO

Thực hiện:08–09/10/2026. [Báo cáo chi tiết](../reviews/ShuttleBook-ui-button-audit-alobo.md) · [Prompt đã thực thi](../prompts/ShuttleBook-ui-button-audit-alobo.md).

## 1. Agent đã làm và bạn cần kiểm bằng tay

| Việc | Agent có thể làm | Bạn cần kiểm |
|---|---|---|
| Build/typecheck, contract/error, điều kiện disabled/loading | Đã build ba app và chạy regression UI mới; có thể thêm targeted test khi triển khai fix | Nhãn dễ hiểu, trạng thái chờ có rõ, thao tác có phù hợp cách bạn vận hành |
| Validation/quyền/scope/idempotency/concurrency/TTL/migration | Có thể chạy API + PostgreSQL/PostGIS thật trên DB tạm; **chưa chạy mới** trong lượt UI audit | Không cần bạn tự tạo hàng loạt race; kiểm flow thường và cho dữ liệu nghiệp vụ thực tế |
| UI desktop/mobile/keyboard/response chậm/mất mạng | Suite158PASS mới; có thể tự tái hiện với fixture synthetic và test không phá DB | Chrome/Edge của bạn, điện thoại thật, mạng thật, thao tác một tay, zoom/đọc ảnh và quyết định UX |
| Xem ảnh bill/QR, nhập tiền, owner confirm | Có thể test private media và transitions trên DB tạm | Đối chiếu ngân hàng thực, đánh giá bill và QR của chính cơ sở; ảnh không chứng minh đã nhận tiền |
| Địa chỉ MapTiler, S3 AWS, email/SMS/provider | Có thể integration khi có cấu hình và tài nguyên test được cấp | Xác nhận đúng cổng/ngõ/tọa độ, QR tài khoản thật và delivery thật; provider thật hiện NOT RUN |
| Benchmark ALOBO | Đã đọc guide/xem trang công khai; có thể đối chiếu screenshot bạn cung cấp | Nếu muốn chấm vận hành sâu: bạn có account hợp pháp tự chạy/cung cấp ảnh đã che PII; không gửi mật khẩu/token |
| Pilot/production | Có thể hỗ trợ telemetry/load/backup/restore/runbook | Chốt policy, chi phí, tổ chức vận hành và nghiệm thu với chủ sân/khách thật |

Không có lý do buộc bạn làm tay các boundary/race mà có thể tự động hóa. Những ca liên quan nhận tiền thật, địa chỉ chính xác và cảm giác sử dụng phải có quyết định người dùng; agent không tự chấm chúng PASS.

## 2. Môi trường và lệnh PowerShell trong VS Code

Chạy tại `C:\Users\luong\Desktop\CLong`. Đọc [setup](../setup.md) và [F07 manual](F07-quote-reservations-manual.md) để khởi tạo/migrate đúng DB local của bạn; lượt audit này chưa migrate development.

### Kiểm tự động đã chạy

```powershell
npm.cmd run build
npm.cmd run test:web -- --workers=2
```

Trước browser suite, dừng các dev server do bạn đang chạy ở5173–5175 để tránh xung đột port. Đây là lệnh public để tái chạy; agent dùng wrapper `scripts/Build-Web.ps1` và `scripts/Test-Web.ps1` tương ứng, với profile tạm vì môi trường helper thiếu biến Windows. Không cần sửa cấu hình máy của bạn theo helper. **158 PASS / 8 SKIP**, hai browser projects. Không đặt biến live rồi gọi test trực tiếp vào DB của bạn; live harness cần DB test riêng theo hướng dẫn của repo.

### Nghiệm thu tay integrated

Bật Docker. Mỗi process chạy trong terminal riêng; giữ process đang chạy. Biến môi trường PowerShell phải dùng `$env:...`.

```powershell
# Terminal API
$env:Media__Mode = 'Local'
npm.cmd run dev:api
```

```powershell
# Terminal Worker
$env:Media__Mode = 'Local'
npm.cmd run dev:worker
```

```powershell
# Terminal Customer / Partner / Admin riêng
npm.cmd run dev:customer
npm.cmd run dev:partner
npm.cmd run dev:admin
```

```powershell
# Terminal kiểm tra health; phải có API đang chạy
curl.exe -i http://localhost:5080/health/live
curl.exe -i http://localhost:5080/health/ready
```

Live200 chỉ chứng minh process; Ready200 chứng minh dependency readiness. Ready503 thì kiểm Docker/DB/migration/config theo setup trước khi test flow. Không suy UI lỗi từ việc API chưa bật. App ports: Customer5173, Partner5174, Admin5175; dev nếu báo port khác phải theo URL actual, CORS và `VITE_API_BASE_URL` đúng setup.

Dùng dữ liệu test: ít nhất1business đã publish,1venue có ≥2courts, opening/prices đầy đủ và QR test; Customer A/B ACTIVE, ownerđúngbusiness, Admin ACTIVE. Dùng date tương lai trong60ngày, từng buổi fixed≥max120phút/minimumcourt, kỳ≥1tháng/max12buổi. Không chuyển tiền thật để chạy regression. Đơn test đã report không tự giải phóng do owner chậm; phải xử lý qua flow authorized.

## 3. Gate ưu tiên trước pilot

Các ca sau **NOT RUN integrated mới**, ngoại trừ quan sát C53UI fixture. Có thể giao agent tự chạy trên DB tạm khi bắt đầu fix; bạn chỉ chốt trải nghiệm/policy.

| Gate | Tiền điều kiện và thao tác | Expected sản phẩm cần đạt | Kết quả audit |
|---|---|---|---|
| G01 auth race /C06 +Partner | Barrier refresh với logout và offline logout; online lại→F5/login; kiểm session/cookie backend | Revoke không bị refresh hồi sinh, UI nói đúng kết quả, không tiếp tục private request của scope cũ | SOURCE_RISK; NOT RUN mới |
| G02 marker /C39 | LoginCustomer→search→marker; so cardroute, tiếp tục đặt; Tab/Enter marker | Giữ phiên trong SPA, đúng venue/returnTo, accessible name; nút thao tác được trên mobile | Source fullreload; NOT RUN marker mới |
| G03 recovery /C53 | Tạo đơn: server commit rồi mất response; chờ120s; gửi lại cùng key hoặc kiểm danh sách | Đúng1đơn, đúngallocation; cùngintent trả đơn cũ sauTTL; UI không báo giải phóng slot của đơn đã tạo | REPRODUCEDUI: nút disabled sauabort+TTL; commitDBNOT RUN |
| G04 cursor /C29/C38 | Delay trang2queryA; đổi query/radiusB; trả responseA muộn | Không lẫn kết quả/cursorA vàoB, loading/error đúng scope | SOURCE_RISK; NOT RUN |
| G05 paging /Partner/A24 | ≥21đơn hoặc≥51thông báo, tải thêm, đợipoll/mark-read trang2 | Giữ rows/window/scroll, không duplicate; unread authoritative | SOURCE_CONFIRMED; NOT RUNfocused |
| G06 amounts /C52/C53 | Tổng VND >2^53, giá từngslot và aggregate; hoặc limitserver được chốt | Exactstring/display/request đồng nhất hoặc từ chối bound có errorcontract; không silentround | SOURCE_RISK; NOT RUNboundary mới |
| G07 approval /A14/A17/A20–A22 | >100hồ sơ, revisionbankQR,9/10/1000/1001chars,2Adminsdecision | Tìm/paging đủ; diff trướcsubmit;validation rõ; conflict409khôngsuccess giả | GAPSOURCE; NOT RUNfocused |
| G08 accessibility | Keyboard tất cả nav/forms/dialog, zoom200%, NVDA, device touch, giờbankQR dài | Focus đến nội dung mới, labels/lỗi đọc được, contrast/touch đáp ứng tiêu chuẩn đã chốt | NOT RUNscreenreader/contrast/device thật |

P1 source-risk chưa được coi là lỗi giao dịch đã tái hiện. Khi xác minh có lỗi nghiêm trọng, xử lý trước pilot. Chưa có fix nên không đánh gate PASS hoặc tạo kết quả nghiệm thu giả.

## 4. Ca đã chạy trong browser suite mới

Bảng dưới đếm testcase executions của suite (cùng ca chạy desktop/mobile), không cộng thành controlcoverage và không cộng với gate lịch sử. Các test live SKIP vì chưa bật harness API/DB test trong lượt này. Những ca mới đề xuất ở mục5 mặc định NOT RUN.

| Test file | PASS executions | SKIP executions |
|---|---:|---:|
| [admin-identity.spec.ts](../../tests/web/admin-identity.spec.ts) | 10 | 0 |
| [admin-live.spec.ts](../../tests/web/admin-live.spec.ts) | 0 | 2 |
| [customer-identity.spec.ts](../../tests/web/customer-identity.spec.ts) | 6 | 0 |
| [customer-registration-live.spec.ts](../../tests/web/customer-registration-live.spec.ts) | 0 | 4 |
| [f02-live.spec.ts](../../tests/web/f02-live.spec.ts) | 0 | 2 |
| [f02-onboarding.spec.ts](../../tests/web/f02-onboarding.spec.ts) | 6 | 0 |
| [f04-discovery.spec.ts](../../tests/web/f04-discovery.spec.ts) | 14 | 0 |
| [f05-booking.spec.ts](../../tests/web/f05-booking.spec.ts) | 6 | 0 |
| [f06-admin.spec.ts](../../tests/web/f06-admin.spec.ts) | 4 | 0 |
| [f06-customer.spec.ts](../../tests/web/f06-customer.spec.ts) | 22 | 0 |
| [f06-partner.spec.ts](../../tests/web/f06-partner.spec.ts) | 18 | 0 |
| [f07-admin-ui.spec.ts](../../tests/web/f07-admin-ui.spec.ts) | 14 | 0 |
| [f07-customer-ui.spec.ts](../../tests/web/f07-customer-ui.spec.ts) | 6 | 0 |
| [f07-partner-series.spec.ts](../../tests/web/f07-partner-series.spec.ts) | 8 | 0 |
| [f07-series-customer.spec.ts](../../tests/web/f07-series-customer.spec.ts) | 22 | 0 |
| [partner-identity.spec.ts](../../tests/web/partner-identity.spec.ts) | 6 | 0 |
| [partner-workspace.spec.ts](../../tests/web/partner-workspace.spec.ts) | 10 | 0 |
| [portals.spec.ts](../../tests/web/portals.spec.ts) | 6 | 0 |

Tổng: **158PASS/8SKIP**. Source của testcase chứa assertion cụ thể; không tự suy ca keyboard/TTL/concurrency/provider khác cũng đã chạy.

## 5. Testcase theo control ID

Tiền điều kiện chung: role/scope đúng trong môi trường test mục2; mở màn tương ứng, đưa control vào state được ghi dưới. Mỗi ca phải chạy trên dữ liệu synthetic/test; với mutation cần có quyền và ghi lại outcome backend khi integration. Các bước/expected dưới là kế hoạch chưa tự chạy, trừ probe C53 được đánh rõ.

### Customer / Guest

#### Shell mọi trang

<a id="c01"></a>

**C01 — Chuyển đến nội dung**

- **Tiền điều kiện/state:** Luôn có; hiện khi keyboard focus
- **Agent có thể tự động hóa:** Đề xuất browser/API: Tab đầu rồi Enter phải focus main và không đổi URL
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Tab đầu rồi Enter phải focus main và không đổi URL
- **Expected đối chiếu:** Focus #customer-content, không đổi route Giữ phần tốt: Skip ngăn tab qua sidebar lặp; handler preventDefault giữ hash
- **Điểm cần quan sát hoặc cải thiện:** Chưa có kiểm NVDA/zoom 200%; không suy đã đạt từ skip test
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C01](../reviews/ShuttleBook-ui-button-audit-alobo.md#c01) · [apps/customer-web/src/components/CustomerShell.tsx:25](../../apps/customer-web/src/components/CustomerShell.tsx#L25)

<a id="c02"></a>

**C02 — ShuttleBook — Tìm sân**

- **Tiền điều kiện/state:** Luôn có; accessible label riêng, logo aria-hidden
- **Agent có thể tự động hóa:** Đề xuất browser/API: Từ cuối trang bấm brand rồi kiểm heading focus và scroll
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Từ cuối trang bấm brand rồi kiểm heading focus và scroll
- **Expected đối chiếu:** SPA /venues qua App click interceptor Giữ phần tốt: Cùng một đường về tìm sân; không remount SessionProvider
- **Điểm cần quan sát hoặc cải thiện:** Route mới không tự focus heading/main hoặc reset scroll trong App:19
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C02](../reviews/ShuttleBook-ui-button-audit-alobo.md#c02) · [apps/customer-web/src/components/CustomerShell.tsx:29](../../apps/customer-web/src/components/CustomerShell.tsx#L29)

<a id="c03"></a>

**C03 — Tìm sân**

- **Tiền điều kiện/state:** Guest và Customer; aria-current theo path
- **Agent có thể tự động hóa:** Đề xuất browser/API: Tìm query/khu vực rồi vào chi tiết và quay lại Tìm sân
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Tìm query/khu vực rồi vào chi tiết và quay lại Tìm sân
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Guest xem sân trước khi login; active nav cả trang chi tiết
- **Điểm cần quan sát hoặc cải thiện:** Chưa có lưu query/nearby khi quay về; route /venues trống làm mất bộ lọc đã chọn
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C03](../reviews/ShuttleBook-ui-button-audit-alobo.md#c03) · [apps/customer-web/src/features/auth/CustomerSession.tsx:62](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L62)

#### Shell Customer

<a id="c04"></a>

**C04 — Đơn của tôi**

- **Tiền điều kiện/state:** Chỉ khi có session
- **Agent có thể tự động hóa:** Đề xuất browser/API: Vào đơn, F5, login lại phải trở về đúng danh sách
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Vào đơn, F5, login lại phải trở về đúng danh sách
- **Expected đối chiếu:** SPA /me/bookings Giữ phần tốt: Một lối vào tất cả đơn casual/fixed, không lẫn owner
- **Điểm cần quan sát hoặc cải thiện:** F5 phải login lại theo contract memory; bất tiện cần policy enhancement riêng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C04](../reviews/ShuttleBook-ui-button-audit-alobo.md#c04) · [apps/customer-web/src/features/auth/CustomerSession.tsx:62](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L62)

<a id="c05"></a>

**C05 — Thông báo**

- **Tiền điều kiện/state:** Có session; badge 1–99 hoặc 99+, aria-label số chưa đọc
- **Agent có thể tự động hóa:** Đề xuất browser/API: Fixture 0/1/100 unread, link và badge accessible đúng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Fixture 0/1/100 unread, link và badge accessible đúng
- **Expected đối chiếu:** SPA /me/notifications Giữ phần tốt: Badge dùng unreadCount API, không bịa số
- **Điểm cần quan sát hoặc cải thiện:** Không có filter chưa đọc hay bulk mark-read; capability chưa có, không phải lỗi core
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C05](../reviews/ShuttleBook-ui-button-audit-alobo.md#c05) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:49](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L49)

<a id="c06"></a>

**C06 — Đăng xuất**

- **Tiền điều kiện/state:** Chỉ khi có session; không spinner/disable
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay refresh rồi logout; family phải bị revoke; thử mạng lỗi không khôi phục UI cũ
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay refresh rồi logout; family phải bị revoke; thử mạng lỗi không khôi phục UI cũ
- **Expected đối chiếu:** Clear memory, replace /login, POST /auth/logout Giữ phần tốt: Xóa state trước request, không để người dùng tiếp tục đọc đơn cũ
- **Điểm cần quan sát hoặc cải thiện:** catch bị bỏ qua, không biết server revoke thất bại; refresh/logout cạnh tranh cần DB test
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C06](../reviews/ShuttleBook-ui-button-audit-alobo.md#c06) · [apps/customer-web/src/features/auth/CustomerSession.tsx:63](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L63)

#### Shell Guest

<a id="c07"></a>

**C07 — Đăng nhập**

- **Tiền điều kiện/state:** Chỉ Guest
- **Agent có thể tự động hóa:** Đề xuất browser/API: Guest mở chi tiết rồi dùng nav Đăng nhập; kiểm destination sau login
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Guest mở chi tiết rồi dùng nav Đăng nhập; kiểm destination sau login
- **Expected đối chiếu:** SPA /login Giữ phần tốt: Lối vào login luôn rõ; browse vẫn dùng được
- **Điểm cần quan sát hoặc cải thiện:** Nav link không kèm returnTo trang hiện tại, login từ chi tiết về /venues mặc định
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C07](../reviews/ShuttleBook-ui-button-audit-alobo.md#c07) · [apps/customer-web/src/features/auth/CustomerSession.tsx:63](../../apps/customer-web/src/features/auth/CustomerSession.tsx#L63)

#### Footer mọi trang

<a id="c08"></a>

**C08 — Tìm cơ sở & lịch trống**

- **Tiền điều kiện/state:** Luôn có
- **Agent có thể tự động hóa:** Đề xuất browser/API: Ở cuối series bấm footer, kiểm scroll/focus và bộ lọc
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Ở cuối series bấm footer, kiểm scroll/focus và bộ lọc
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Có đường về tìm sân khi ở cuối nội dung dài
- **Điểm cần quan sát hoặc cải thiện:** Không giữ query/nearby của trang trước; same destination với C03
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C08](../reviews/ShuttleBook-ui-button-audit-alobo.md#c08) · [apps/customer-web/src/components/CustomerShell.tsx:47](../../apps/customer-web/src/components/CustomerShell.tsx#L47)

#### Tài khoản giới thiệu

<a id="c09"></a>

**C09 — Tìm sân gần bạn và xem lịch trống**

- **Tiền điều kiện/state:** Guest/Customer ở identity
- **Agent có thể tự động hóa:** Đề xuất browser/API: Bấm CTA và kiểm list mặc định, rồi người dùng phải chủ động chọn vị trí
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Bấm CTA và kiểm list mặc định, rồi người dùng phải chủ động chọn vị trí
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Nêu task thay vì chỉ nút đăng ký, phù hợp khám phá trước login
- **Điểm cần quan sát hoặc cải thiện:** Label hứa gần bạn nhưng mở list mặc định, chưa lấy GPS hay nearby
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C09](../reviews/ShuttleBook-ui-button-audit-alobo.md#c09) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:139](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L139)

#### Tài khoản đã login

<a id="c10"></a>

**C10 — Tìm sân & xem lịch**

- **Tiền điều kiện/state:** Chỉ có session
- **Agent có thể tự động hóa:** Đề xuất browser/API: Login rồi mở identity, CTA đi đến list mà giữ session
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Login rồi mở identity, CTA đi đến list mà giữ session
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Không còn chỉ dòng Đang hoạt động, cho tiếp tục booking
- **Điểm cần quan sát hoặc cải thiện:** Không có sân vừa xem/gợi ý lần gần nhất; không suy cá nhân hóa từ CTA
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C10](../reviews/ShuttleBook-ui-button-audit-alobo.md#c10) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:147](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L147)

<a id="c11"></a>

**C11 — Xem đơn của tôi**

- **Tiền điều kiện/state:** Chỉ có session
- **Agent có thể tự động hóa:** Đề xuất browser/API: Identity đã login sang list, click đơn và Back
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Identity đã login sang list, click đơn và Back
- **Expected đối chiếu:** SPA /me/bookings Giữ phần tốt: Nêu rõ task xem trạng thái đơn sau login
- **Điểm cần quan sát hoặc cải thiện:** Không có đơn gần nhất trên dashboard; phải thêm bước mở list
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C11](../reviews/ShuttleBook-ui-button-audit-alobo.md#c11) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:148](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L148)

<a id="c12"></a>

**C12 — Xem thông báo**

- **Tiền điều kiện/state:** Chỉ có session
- **Agent có thể tự động hóa:** Đề xuất browser/API: Có unread, CTA mở inbox giữ badge đúng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Có unread, CTA mở inbox giữ badge đúng
- **Expected đối chiếu:** SPA /me/notifications Giữ phần tốt: Tài khoản có đường vào inbox, cùng state với badge shell
- **Điểm cần quan sát hoặc cải thiện:** Không hiển thị unreadCount tại CTA này, phải đọc badge nav
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C12](../reviews/ShuttleBook-ui-button-audit-alobo.md#c12) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:148](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L148)

#### Auth tabs

<a id="c13"></a>

**C13 — Đăng ký**

- **Tiền điều kiện/state:** Guest; disabled khi register, không disabled theo submitting
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay login/register request, đổi tab rồi hoàn thành response, kiểm route không nhảy bất ngờ
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay login/register request, đổi tab rồi hoàn thành response, kiểm route không nhảy bất ngờ
- **Expected đối chiếu:** Navigate /, giữ location.search Giữ phần tốt: Giữ returnTo khi chuyển login/register; form current rõ bằng nền
- **Điểm cần quan sát hoặc cải thiện:** Cho đổi tab khi request đang gửi, response cũ có thể điều hướng verify sau đã sang login
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C13](../reviews/ShuttleBook-ui-button-audit-alobo.md#c13) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:151](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L151)

<a id="c14"></a>

**C14 — Đăng nhập**

- **Tiền điều kiện/state:** Guest; disabled khi login
- **Agent có thể tự động hóa:** Đề xuất browser/API: Keyboard chuyển tab khi đăng ký pending; kiểm trạng thái/focus/response cũ
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Keyboard chuyển tab khi đăng ký pending; kiểm trạng thái/focus/response cũ
- **Expected đối chiếu:** Navigate /login, giữ location.search Giữ phần tốt: Có thể đổi đăng ký/login không reload mất returnTo
- **Điểm cần quan sát hoặc cải thiện:** Active dùng disabled thay aria-current/tab semantics; request cũ chưa abort khi đổi tab
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C14](../reviews/ShuttleBook-ui-button-audit-alobo.md#c14) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:152](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L152)

#### Đăng ký và Đăng nhập

<a id="c15"></a>

**C15 — Phương thức liên hệ**

- **Tiền điều kiện/state:** Guest, cả 2 form; input type/placeholder đổi theo contactType
- **Agent có thể tự động hóa:** Đề xuất browser/API: Nhập email rồi đổi phone; nhập phone rồi đổi email, kiểm lỗi dễ hiểu
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Nhập email rồi đổi phone; nhập phone rồi đổi email, kiểm lỗi dễ hiểu
- **Expected đối chiếu:** Chọn Email hoặc Số điện thoại Giữ phần tốt: Label bọc selector, có email và phone rõ
- **Điểm cần quan sát hoặc cải thiện:** Đổi type vẫn giữ contact trước đó, có thể gửi email dưới phone; cần reset hoặc validation gợi ý
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C15](../reviews/ShuttleBook-ui-button-audit-alobo.md#c15) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:157](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L157)

#### Đăng ký

<a id="c16"></a>

**C16 — Email / Số điện thoại E.164**

- **Tiền điều kiện/state:** required; email native typeMismatch, phone E.164 placeholder
- **Agent có thể tự động hóa:** Đề xuất browser/API: Email sai chặn native; phone 09... và +849... theo contract
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Email sai chặn native; phone 09... và +849... theo contract
- **Expected đối chiếu:** Set contact cho POST register Giữ phần tốt: Autocomplete email/tel và ví dụ +84901234567 hỗ trợ nhập
- **Điểm cần quan sát hoặc cải thiện:** Chỉ hướng dẫn E.164, chưa tự chuẩn hóa 09... cho người dùng Việt Nam
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C16](../reviews/ShuttleBook-ui-button-audit-alobo.md#c16) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:158](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L158)

<a id="c17"></a>

**C17 — Mật khẩu**

- **Tiền điều kiện/state:** required, minLength12, autocomplete new-password
- **Agent có thể tự động hóa:** Đề xuất browser/API: Autofill password; nhập đủ12 nhưng thiếu3 nhóm, kiểm lỗi sau submit
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Autofill password; nhập đủ12 nhưng thiếu3 nhóm, kiểm lỗi sau submit
- **Expected đối chiếu:** Set mật khẩu cho POST register Giữ phần tốt: Help có 3 nhóm ký tự và aria-describedby; không lưu storage
- **Điểm cần quan sát hoặc cải thiện:** Không có hiện/ẩn mật khẩu hay strength feedback trước submit
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C17](../reviews/ShuttleBook-ui-button-audit-alobo.md#c17) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:159](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L159)

<a id="c18"></a>

**C18 — Nhập lại mật khẩu**

- **Tiền điều kiện/state:** required minLength12; aria-invalid khi mismatch
- **Agent có thể tự động hóa:** Đề xuất browser/API: Submit mismatch, sửa confirm đúng rồi kiểm aria-invalid/feedback trước submit lại
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Submit mismatch, sửa confirm đúng rồi kiểm aria-invalid/feedback trước submit lại
- **Expected đối chiếu:** So khớp trước gọi API Giữ phần tốt: Mismatch không phát request; feedback được focus
- **Điểm cần quan sát hoặc cải thiện:** aria-invalid dựa message chung; sau sửa input vẫn còn cờ đến lần submit kế
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C18](../reviews/ShuttleBook-ui-button-audit-alobo.md#c18) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:160](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L160)

<a id="c19"></a>

**C19 — Đăng ký / Đang đăng ký…**

- **Tiền điều kiện/state:** disabled submitting; form aria-busy; network/429/delivery errors
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay202, sửa contact/đổi tab, kiểm verify contact thuộc request nào
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay202, sửa contact/đổi tab, kiểm verify contact thuộc request nào
- **Expected đối chiếu:** POST /auth/register;202 sang verify Giữ phần tốt: Không double-click khi pending; password/code được xóa sau202; thông báo tránh lộ tài khoản
- **Điểm cần quan sát hoặc cải thiện:** Form fields và tabs vẫn đổi được trong request, thiếu request generation guard
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C19](../reviews/ShuttleBook-ui-button-audit-alobo.md#c19) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:162](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L162)

#### Xác minh local

<a id="c20"></a>

**C20 — Mailpit**

- **Tiền điều kiện/state:** DEV only
- **Agent có thể tự động hóa:** Đề xuất browser/API: Build production không thấy Mailpit, local mở tab mới
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Build production không thấy Mailpit, local mở tab mới
- **Expected đối chiếu:** http://localhost:8025 target=_blank Giữ phần tốt: Không lộ link debug production; noreferrer
- **Điểm cần quan sát hoặc cải thiện:** Chỉ local delivery, không chứng minh OTP gửi email/SMS thật
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C20](../reviews/ShuttleBook-ui-button-audit-alobo.md#c20) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:167](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L167)

#### Xác minh

<a id="c21"></a>

**C21 — Mã xác minh 6 chữ số**

- **Tiền điều kiện/state:** required; disable nếu !contact; pattern/max6; xóa non-digit
- **Agent có thể tự động hóa:** Đề xuất browser/API: Paste chữ/dấu cách/7digits; mã cũ/hết hạn hiển thị lỗi
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Paste chữ/dấu cách/7digits; mã cũ/hết hạn hiển thị lỗi
- **Expected đối chiếu:** Set code cho verify-contact Giữ phần tốt: OTP autocomplete one-time-code, mobile numeric keyboard
- **Điểm cần quan sát hoặc cải thiện:** Không hiện thời hạn OTP/countdown, user biết hết hạn sau submit
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C21](../reviews/ShuttleBook-ui-button-audit-alobo.md#c21) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:168](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L168)

<a id="c22"></a>

**C22 — Xác minh / Đang xử lý…**

- **Tiền điều kiện/state:** disabled submitting hoặc thiếu contact
- **Agent có thể tự động hóa:** Đề xuất browser/API: Verify success/expired/429, đường phục hồi sang resend và login
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Verify success/expired/429, đường phục hồi sang resend và login
- **Expected đối chiếu:** POST /auth/verify-contact, success sang login Giữ phần tốt: Không auto-login nhầm, clear OTP khi success; thông báo invalid/expired
- **Điểm cần quan sát hoặc cải thiện:** Không phân biệt invalid với expired bằng câu chung; không đưa nút gửi lại ngay trong error
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C22](../reviews/ShuttleBook-ui-button-audit-alobo.md#c22) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:169](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L169)

<a id="c23"></a>

**C23 — Gửi lại mã**

- **Tiền điều kiện/state:** disabled submitting/!contact; Retry-After được đưa vào message
- **Agent có thể tự động hóa:** Đề xuất browser/API: Resend liên tiếp429/Retry-After, kiểm nút và thời gian gợi ý
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Resend liên tiếp429/Retry-After, kiểm nút và thời gian gợi ý
- **Expected đối chiếu:** POST /auth/verification-resend Giữ phần tốt: Server202/429/503 được phản hồi, tránh báo gửi thành công giả
- **Điểm cần quan sát hoặc cải thiện:** Không cooldown countdown trên nút, người dùng phải thử rồi thấy429
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C23](../reviews/ShuttleBook-ui-button-audit-alobo.md#c23) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:170](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L170)

#### Xác minh thiếu contact

<a id="c24"></a>

**C24 — Quay lại đăng ký**

- **Tiền điều kiện/state:** Chỉ !contact
- **Agent có thể tự động hóa:** Đề xuất browser/API: Mở /verify trực tiếp, dùng nút quay lại và giữ booking returnTo
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Mở /verify trực tiếp, dùng nút quay lại và giữ booking returnTo
- **Expected đối chiếu:** Navigate / giữ returnTo Giữ phần tốt: Deep link verify hoặc mất history.state có đường hồi phục
- **Điểm cần quan sát hoặc cải thiện:** Không có nhập lại contact tại verify, phải qua đăng ký từ đầu
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C24](../reviews/ShuttleBook-ui-button-audit-alobo.md#c24) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:171](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L171)

#### Đăng nhập

<a id="c25"></a>

**C25 — Email / Số điện thoại E.164**

- **Tiền điều kiện/state:** required; autocomplete username
- **Agent có thể tự động hóa:** Đề xuất browser/API: Autofill login, đổi type, thử chuẩn09/+84 và kiểm thông báo
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Autofill login, đổi type, thử chuẩn09/+84 và kiểm thông báo
- **Expected đối chiếu:** Set contact cho login Giữ phần tốt: Hỗ trợ password manager và label contact theo type
- **Điểm cần quan sát hoặc cải thiện:** Phone vẫn yêu cầu E.164 chưa thân thiện nhập09...; type switch giữ input cũ
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C25](../reviews/ShuttleBook-ui-button-audit-alobo.md#c25) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:177](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L177)

<a id="c26"></a>

**C26 — Mật khẩu**

- **Tiền điều kiện/state:** required; autocomplete current-password
- **Agent có thể tự động hóa:** Đề xuất browser/API: Password manager/mobile paste; người dùng quên mật khẩu chưa có bước phục hồi
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Password manager/mobile paste; người dùng quên mật khẩu chưa có bước phục hồi
- **Expected đối chiếu:** Set password login Giữ phần tốt: Password manager phù hợp, xóa password sau response
- **Điểm cần quan sát hoặc cải thiện:** Chưa có Hiện mật khẩu/Quên mật khẩu; missing recovery capability, không là control tồn tại
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C26](../reviews/ShuttleBook-ui-button-audit-alobo.md#c26) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:178](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L178)

<a id="c27"></a>

**C27 — Đăng nhập / Đang đăng nhập…**

- **Tiền điều kiện/state:** disabled submitting; 429/generic401/wrongrole/network feedback
- **Agent có thể tự động hóa:** Đề xuất browser/API: Wrong role/invalid credentials/external returnTo và login rồi F5
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Wrong role/invalid credentials/external returnTo và login rồi F5
- **Expected đối chiếu:** POST /auth/login; parse ACTIVE CUSTOMER; safeReturnTo Giữ phần tốt: Không nhận ADMIN hoặc operator token; returnTo allowlist chống redirect ngoài
- **Điểm cần quan sát hoặc cải thiện:** F5 mất phiên memory theo contract; login từ nav không giữ trang chi tiết (C07)
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C27](../reviews/ShuttleBook-ui-button-audit-alobo.md#c27) · [apps/customer-web/src/features/auth/CustomerIdentity.tsx:179](../../apps/customer-web/src/features/auth/CustomerIdentity.tsx#L179)

#### Tìm cơ sở

<a id="c28"></a>

**C28 — Tên sân hoặc địa chỉ**

- **Tiền điều kiện/state:** Guest/Customer; có URL q khởi tạo
- **Agent có thể tự động hóa:** Đề xuất browser/API: Nhập query rất dài/Unicode/Enter và xóa trắng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Nhập query rất dài/Unicode/Enter và xóa trắng
- **Expected đối chiếu:** Set query, Enter submit Tìm sân Giữ phần tốt: Search text dùng nguồn API nội bộ, không bắt cấp GPS
- **Điểm cần quan sát hoặc cải thiện:** Không có maxlength hay clear input, có thể gửi query quá dài rồi400 chung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C28](../reviews/ShuttleBook-ui-button-audit-alobo.md#c28) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:104](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L104)

<a id="c29"></a>

**C29 — Tìm sân**

- **Tiền điều kiện/state:** Luôn có, chưa disable khi loading
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay page cũ rồi tìm query mới, kết quả cũ không được append
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay page cũ rồi tìm query mới, kết quả cũ không được append
- **Expected đối chiếu:** GET /venues?q=..., replace URL q; reset nearby Giữ phần tốt: Không bắt login; trim query, tìm cả tên/địa chỉ; abort initial request cũ
- **Điểm cần quan sát hoặc cải thiện:** Đổi query khi Xem thêm pending có thể trộn page cũ bởi loadMore không guard
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C29](../reviews/ShuttleBook-ui-button-audit-alobo.md#c29) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:105](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L105)

<a id="c30"></a>

**C30 — Dùng vị trí của tôi**

- **Tiền điều kiện/state:** Luôn có; timeout10s; GPS failure alert
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay GPS rồi tìm text, deny/timeout và nhiều click cần giữ intent mới
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay GPS rồi tìm text, deny/timeout và nhiều click cần giữ intent mới
- **Expected đối chiếu:** Browser geolocation -> GET /venues/nearby Giữ phần tốt: Denied GPS vẫn tìm bằng tên; nearby theo lat/lng/radius, không dùng tên làm khoảng cách
- **Điểm cần quan sát hoặc cải thiện:** Không loading/disable khi xin GPS; bấm nhiều hoặc GPS cũ về sau text search có thể đổi mode bất ngờ
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C30](../reviews/ShuttleBook-ui-button-audit-alobo.md#c30) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:107](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L107)

#### Tìm khu vực

<a id="c31"></a>

**C31 — Nhập khu vực trên bản đồ**

- **Tiền điều kiện/state:** Chỉ khi có key; xóa suggestions khi nhập; network error locationError
- **Agent có thể tự động hóa:** Đề xuất browser/API: Gõ>=3, ArrowDown/Enter/Escape, lỗi provider, mobile suggestions
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Gõ>=3, ArrowDown/Enter/Escape, lỗi provider, mobile suggestions
- **Expected đối chiếu:** MapTiler geocoding Vietnam vi sau400ms và>=3chars Giữ phần tốt: Không yêu cầu người dùng biết tọa độ; debounce và abort request cũ
- **Điểm cần quan sát hoặc cải thiện:** Chưa có combobox aria-expanded/activedescendant/ArrowDown, nhập dưới3 ký tự không giải thích
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C31](../reviews/ShuttleBook-ui-button-audit-alobo.md#c31) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:109](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L109)

<a id="c32"></a>

**C32 — {suggestion.place_name}**

- **Tiền điều kiện/state:** Có suggestions; label place_name, list aria-label
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chọn gợi ý Hoàng Cầu, kiểm lat/lng đúng thứ tự và nearby radius
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chọn gợi ý Hoàng Cầu, kiểm lat/lng đúng thứ tự và nearby radius
- **Expected đối chiếu:** Chọn point từ geometry/center, set nearby Giữ phần tốt: Địa chỉ đầy đủ chọn trực tiếp, clear suggestion/error sau chọn
- **Điểm cần quan sát hoặc cải thiện:** Chỉ native buttons tab, chưa combobox keyboard; lat/lng provider thực chưa test ở audit
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C32](../reviews/ShuttleBook-ui-button-audit-alobo.md#c32) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:114](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L114)

#### Tìm cơ sở

<a id="c33"></a>

**C33 — Bán kính**

- **Tiền điều kiện/state:** Luôn có; mặc định5km
- **Agent có thể tự động hóa:** Đề xuất browser/API: Đổi radius khi list vs nearby; label/kết quả phải giải thích khác biệt
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Đổi radius khi list vs nearby; label/kết quả phải giải thích khác biệt
- **Expected đối chiếu:** Set 3/5/10/20km -> effect fetch Giữ phần tốt: Đơn vị km dễ hiểu; nearby request radiusMeters nhất quán
- **Điểm cần quan sát hoặc cải thiện:** Selector vẫn active ở list mode, đổi radius refetch list nhưng không lọc theo bán kính dễ gây hiểu nhầm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C33](../reviews/ShuttleBook-ui-button-audit-alobo.md#c33) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:116](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L116)

#### Tìm cơ sở lỗi

<a id="c34"></a>

**C34 — Thử lại**

- **Tiền điều kiện/state:** Chỉ error; chưa disable loading
- **Agent có thể tự động hóa:** Đề xuất browser/API: Fail page2 rồi Thử lại, kiểm mất trang đã tải hay giữ cursor
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Fail page2 rồi Thử lại, kiểm mất trang đã tải hay giữ cursor
- **Expected đối chiếu:** Increment retry -> reload current search Giữ phần tốt: Người dùng có recovery cho lỗi API, không reload cả session
- **Điểm cần quan sát hoặc cải thiện:** Retry loadMore lỗi sẽ chạy initial page và reset items, không retry cùng cursor
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C34](../reviews/ShuttleBook-ui-button-audit-alobo.md#c34) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:126](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L126)

#### Tìm cơ sở trống

<a id="c35"></a>

**C35 — Đổi tìm kiếm**

- **Tiền điều kiện/state:** Chỉ !loading,!error,items=0
- **Agent có thể tự động hóa:** Đề xuất browser/API: Nearby empty dùng CTA, sửa query rồi tìm
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Nearby empty dùng CTA, sửa query rồi tìm
- **Expected đối chiếu:** Focus #venue-query Giữ phần tốt: Hành động cụ thể đưa focus về field thay vì empty dead end
- **Điểm cần quan sát hoặc cải thiện:** Nếu nearby rỗng, CTA chỉ focus text mà không gợi đổi radius/chọn khu vực
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C35](../reviews/ShuttleBook-ui-button-audit-alobo.md#c35) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:128](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L128)

#### Kết quả cơ sở

<a id="c36"></a>

**C36 — {venue.name}**

- **Tiền điều kiện/state:** Mỗi venue result; ảnh fallback/địa chỉ/distance
- **Agent có thể tự động hóa:** Đề xuất browser/API: Tên/địa chỉ rất dài vẫn click được; login vẫn giữ khi mở title
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Tên/địa chỉ rất dài vẫn click được; login vẫn giữ khi mở title
- **Expected đối chiếu:** SPA /venues/{venue.id} Giữ phần tốt: Mở cùng detail flow như CTA, giữ Customer memory session
- **Điểm cần quan sát hoặc cải thiện:** Card chưa có min/max giá hoặc giờ hoạt động/tình trạng nhanh; phải vào từng cơ sở
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C36](../reviews/ShuttleBook-ui-button-audit-alobo.md#c36) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:131](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L131)

<a id="c37"></a>

**C37 — Xem lịch các sân →**

- **Tiền điều kiện/state:** Mỗi venue result
- **Agent có thể tự động hóa:** Đề xuất browser/API: Screen-reader links list phân biệt cơ sở; mở CTA giữ session
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Screen-reader links list phân biệt cơ sở; mở CTA giữ session
- **Expected đối chiếu:** SPA /venues/{venue.id} Giữ phần tốt: Nêu rõ bước kế là lịch toàn bộ sân, khác marker full navigation
- **Điểm cần quan sát hoặc cải thiện:** Nhiều CTA có cùng accessible name chưa có aria-label kèm venue, khó phân biệt bằng danh sách links
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C37](../reviews/ShuttleBook-ui-button-audit-alobo.md#c37) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:133](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L133)

#### Tìm cơ sở pagination

<a id="c38"></a>

**C38 — Xem thêm cơ sở**

- **Tiền điều kiện/state:** Chỉ nextCursor; disabled loading; không label loading riêng
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chậm page2 searchA rồi đổiqueryB/radius, pageA không lẫnB
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chậm page2 searchA rồi đổiqueryB/radius, pageA không lẫnB
- **Expected đối chiếu:** GET same mode/query/radius + cursor rồi append Giữ phần tốt: Cursor append cho danh sách, không tải toàn bộ data một lần
- **Điểm cần quan sát hoặc cải thiện:** Không AbortSignal/search-generation/dedup; response page cũ có thể append vào query/radius mới
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C38](../reviews/ShuttleBook-ui-button-audit-alobo.md#c38) · [apps/customer-web/src/features/venues/VenueSearchPage.tsx:135](../../apps/customer-web/src/features/venues/VenueSearchPage.tsx#L135)

#### Bản đồ cơ sở

<a id="c39"></a>

**C39 — Xem {venue.name}**

- **Tiền điều kiện/state:** Có key/SDK; title+aria-label venue; dot visual
- **Agent có thể tự động hóa:** Đề xuất browser/API: Login -> click marker -> detail -> quote không được bắt login; đo touch36px
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Login -> click marker -> detail -> quote không được bắt login; đo touch36px
- **Expected đối chiếu:** window.location.href=/venues/{id} Giữ phần tốt: Marker có accessible name riêng và đúng tọa độ venue
- **Điểm cần quan sát hoặc cải thiện:** Full navigation làm session memory vềnull, khác card SPA; CSS marker36px<44px target chung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C39](../reviews/ShuttleBook-ui-button-audit-alobo.md#c39) · [apps/customer-web/src/features/venues/components/VenueMap.tsx:50](../../apps/customer-web/src/features/venues/components/VenueMap.tsx#L50)

#### Bản đồ MapTiler

<a id="c40"></a>

**C40 — Controls native MapTiler: NOT OBSERVED**

- **Tiền điều kiện/state:** Key/SDK mới hiện map; code không cấu hình NavigationControl riêng
- **Agent có thể tự động hóa:** Đề xuất browser/API: Browser provider thật kiểm controls/attribution/keyboard/failed SDK fallback
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Browser provider thật kiểm controls/attribution/keyboard/failed SDK fallback
- **Expected đối chiếu:** SDK render drag/zoom/attribution nếu SDK thêm Giữ phần tốt: Lỗi map không chặn list và có fallback message
- **Điểm cần quan sát hoặc cải thiện:** Không có markup/label native ở source; chưa quan sát SDK thật, không đoán nút zoom nào tồn tại
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C40](../reviews/ShuttleBook-ui-button-audit-alobo.md#c40) · [apps/customer-web/src/features/venues/components/VenueMap.tsx:48](../../apps/customer-web/src/features/venues/components/VenueMap.tsx#L48)

#### Chi tiết cơ sở

<a id="c41"></a>

**C41 — Danh sách cơ sở**

- **Tiền điều kiện/state:** Luôn có cả loading/error
- **Agent có thể tự động hóa:** Đề xuất browser/API: Từ result query vào detail rồi breadcrumb quay lại
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Từ result query vào detail rồi breadcrumb quay lại
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Có đường ra khi detail đang tải, không cần Back browser
- **Điểm cần quan sát hoặc cải thiện:** Bỏ query/nearby trước đó; không giữ vị trí danh sách
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C41](../reviews/ShuttleBook-ui-button-audit-alobo.md#c41) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:89](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L89)

#### Chi tiết lỗi

<a id="c42"></a>

**C42 — Quay lại danh sách**

- **Tiền điều kiện/state:** Chỉ detail error
- **Agent có thể tự động hóa:** Đề xuất browser/API: Detail503 -> link list; lỗi tạm thời xem khả năng retry
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Detail503 -> link list; lỗi tạm thời xem khả năng retry
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: 404/unpublished có hướng hồi phục rõ
- **Điểm cần quan sát hoặc cải thiện:** Không có Thử lại detail riêng, lỗi mạng phải quay list hoặc F5 mất phiên
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C42](../reviews/ShuttleBook-ui-button-audit-alobo.md#c42) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:91](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L91)

#### Lịch cơ sở

<a id="c43"></a>

**C43 — Vãng lai**

- **Tiền điều kiện/state:** Có venue; aria-pressed
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chọn fixed/casual, Back/Forward và minimum/chọn ca không lệch
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chọn fixed/casual, Back/Forward và minimum/chọn ca không lệch
- **Expected đối chiếu:** Set casual và history mode=casual Giữ phần tốt: Mode rõ, dùng chung bảng sân/ca cho cả hai hình thức
- **Điểm cần quan sát hoặc cải thiện:** Mode change không phát popstate nên App URL state chưa sync; cần kiểm Back và selection
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C43](../reviews/ShuttleBook-ui-button-audit-alobo.md#c43) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:98](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L98)

<a id="c44"></a>

**C44 — Cố định hằng tuần**

- **Tiền điều kiện/state:** Có venue; aria-pressed; rule >=2 giờ/tháng visible
- **Agent có thể tự động hóa:** Đề xuất browser/API: Switch fixed, chọn150 phút rồi Continue, kiểm điều kiện và disclosure thanh toán
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Switch fixed, chọn150 phút rồi Continue, kiểm điều kiện và disclosure thanh toán
- **Expected đối chiếu:** Set fixed và history mode=fixed Giữ phần tốt: Minimum cập nhật >=120 và giải thích kỳ từ bước chọn sân
- **Điểm cần quan sát hoặc cải thiện:** Ở đây chưa nêu thanh toán 100% cả kỳ; phải tới series review mới biết
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C44](../reviews/ShuttleBook-ui-button-audit-alobo.md#c44) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:100](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L100)

<a id="c45"></a>

**C45 — Ngày chơi**

- **Tiền điều kiện/state:** min venue-today; không max60 tại control
- **Agent có thể tự động hóa:** Đề xuất browser/API: Timezone khác, hôm qua/61ngày, Back lịch ngày cũ
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Timezone khác, hôm qua/61ngày, Back lịch ngày cũ
- **Expected đối chiếu:** Set date, clear schedule, history date -> GET availability Giữ phần tốt: Múi giờ dùng venue, không lấy timezone máy để quyết định ngày
- **Điểm cần quan sát hoặc cải thiện:** Cho chọn >60 ngày dù booking horizon60, chỉ biết bị từ chối ở quote; chưa shortcut hôm nay/ngày mai
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C45](../reviews/ShuttleBook-ui-button-audit-alobo.md#c45) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:104](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L104)

<a id="c46"></a>

**C46 — Làm mới lịch**

- **Tiền điều kiện/state:** Có venue; loading status; không disable
- **Agent có thể tự động hóa:** Đề xuất browser/API: A lấy quote,B mở lịch đã cũ, bấm refresh phải thấy Đã kín
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: A lấy quote,B mở lịch đã cũ, bấm refresh phải thấy Đã kín
- **Expected đối chiếu:** Increment retry -> GET availability ngày chọn Giữ phần tốt: Có refresh chủ động ngoài polling30s/focus, không auto mutation
- **Điểm cần quan sát hoặc cải thiện:** Không hiện last-updated; B có thể thấy ô cũ tới refresh, không phải realtime push
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C46](../reviews/ShuttleBook-ui-button-audit-alobo.md#c46) · [apps/customer-web/src/features/venues/VenueDetailPage.tsx:107](../../apps/customer-web/src/features/venues/VenueDetailPage.tsx#L107)

#### Bảng sân × giờ

<a id="c47"></a>

**C47 — {court.name}, {time} đến {slot.endsAt}, Còn trống, {price}**

- **Tiền điều kiện/state:** Chỉ AVAILABLE button; aria-pressed; unavailable span; mỗi ô30phút
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chọn4/5/7ca, bỏ đầu/cuối/giữa, qua ô kín, switch court và auto-refresh
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chọn4/5/7ca, bỏ đầu/cuối/giữa, qua ô kín, switch court và auto-refresh
- **Expected đối chiếu:** Chọn/deselect ca; fill range nếu trống; switch court reset selection Giữ phần tốt: Tất cả sân thành rows; label gồm sân/giờ/giá; không click ca kín; minimum liên tiếp
- **Điểm cần quan sát hoặc cải thiện:** Bấm ô giữa range giữ nhánh dài hơn và bỏ nhánh kia, cần UX giải thích; tổng tham khảo number có precision boundary
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C47](../reviews/ShuttleBook-ui-button-audit-alobo.md#c47) · [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:91](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L91)

<a id="c48"></a>

**C48 — Bảng lịch sân, cuộn ngang để xem các giờ khác**

- **Tiền điều kiện/state:** Có axis; tabIndex0; table row/column scope
- **Agent có thể tự động hóa:** Đề xuất browser/API: Keyboard cuộn ngày dài375/1440, zoom200%, header/court sticky
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Keyboard cuộn ngày dài375/1440, zoom200%, header/court sticky
- **Expected đối chiếu:** Cuộn ngang bảng với cột sân sticky Giữ phần tốt: Giữ30phút/ô đọc được, full-day không nén chữ như ảnh cũ
- **Điểm cần quan sát hoặc cải thiện:** Mỗi slot một tab stop nên keyboard traversal dài; chưa arrow navigation hoặc jump giờ
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C48](../reviews/ShuttleBook-ui-button-audit-alobo.md#c48) · [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:68](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L68)

#### Lịch chọn vãng lai

<a id="c49"></a>

**C49 — Tiếp tục đặt vãng lai**

- **Tiền điều kiện/state:** Có selection; disabled dưới minimum
- **Agent có thể tự động hóa:** Đề xuất browser/API: Guest chọn150 phút -> login -> đúng quote; không create chỉ bằng chọn ô
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Guest chọn150 phút -> login -> đúng quote; không create chỉ bằng chọn ô
- **Expected đối chiếu:** navigate /booking-review với venue,court,date,start,end Giữ phần tốt: Không tính block modulus; minimum đủ cho4/5/6/7ca; guard login ở review
- **Điểm cần quan sát hoặc cải thiện:** Guest chỉ biết cần login sau click, CTA chưa nêu điều đó; route mới chưa focus heading
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C49](../reviews/ShuttleBook-ui-button-audit-alobo.md#c49) · [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:105](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L105)

#### Lịch chọn cố định

<a id="c50"></a>

**C50 — Tiếp tục đặt cố định**

- **Tiền điều kiện/state:** Mode fixed; có selection; disabled dưới max120/minimum
- **Agent có thể tự động hóa:** Đề xuất browser/API: Guest chọn fixed5ca -> login -> form150 phút đúng, chưa quote/hold trước submit
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Guest chọn fixed5ca -> login -> form150 phút đúng, chưa quote/hold trước submit
- **Expected đối chiếu:** navigate /series-review giữ court/date/range Giữ phần tốt: Đi chung entry point; quote toàn kỳ chứ không đặt mỗi buổi rời
- **Điểm cần quan sát hoặc cải thiện:** Đã có note giá ngày khác giá cả kỳ; vẫn thiếu focus chuyển bước
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C50](../reviews/ShuttleBook-ui-button-audit-alobo.md#c50) · [apps/customer-web/src/features/venues/components/ScheduleGrid.tsx:105](../../apps/customer-web/src/features/venues/components/ScheduleGrid.tsx#L105)

#### Báo giá vãng lai

<a id="c51"></a>

**C51 — Quay lại lịch các sân**

- **Tiền điều kiện/state:** Luôn có trong review
- **Agent có thể tự động hóa:** Đề xuất browser/API: Sau quote bấm Back, lịch busy tới TTL nhưng chưa có booking
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Sau quote bấm Back, lịch busy tới TTL nhưng chưa có booking
- **Expected đối chiếu:** SPA /venues/{venueId}?date=... Giữ phần tốt: Quay đúng venue/ngày, không mất phiên
- **Điểm cần quan sát hoặc cải thiện:** Không restore chọn ca; quote hold cũ còn tới TTL nên A nhìn ô mình là kín, chưa có label own hold
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C51](../reviews/ShuttleBook-ui-button-audit-alobo.md#c51) · [apps/customer-web/src/features/bookings/BookingPages.tsx:56](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L56)

#### Báo giá/đơn casual và từng buổi fixed

<a id="c52"></a>

**C52 — Chi tiết giá từng ca 30 phút**

- **Tiền điều kiện/state:** Quote/detail khi có slots; mỗi occurrence có summary
- **Agent có thể tự động hóa:** Đề xuất browser/API: Mở bằng Enter/Space, từng buổi giá khác, số tiền >2^53 hiển thị đúng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Mở bằng Enter/Space, từng buổi giá khác, số tiền >2^53 hiển thị đúng
- **Expected đối chiếu:** Native expand/collapse price list Giữ phần tốt: Tổng nổi trước, giá30phút chi tiết mở khi cần; slot dùng exact string nếu API có
- **Điểm cần quan sát hoặc cải thiện:** Series nhiều summary cùng nhãn thiếu ngày trong accessible name; casual API quote chỉ number có boundary >2^53
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C52](../reviews/ShuttleBook-ui-button-audit-alobo.md#c52) · [apps/customer-web/src/features/bookings/BookingFacts.tsx:13](../../apps/customer-web/src/features/bookings/BookingFacts.tsx#L13)

#### Báo giá vãng lai

<a id="c53"></a>

**C53 — Xác nhận tạo đơn / Đang tạo đơn…**

- **Tiền điều kiện/state:** Quote hợp lệ; disabled pending/hết hạn; busy ref
- **Agent có thể tự động hóa:** Đề xuất browser/API: Commit create mất response, tiến clock qua TTL, retry same key khôi phục1đơn; quote tiền lớn
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Commit create mất response, tiến clock qua TTL, retry same key khôi phục1đơn; quote tiền lớn
- **Expected đối chiếu:** POST /bookings cùng Idempotency-Key -> detail Giữ phần tốt: Không double-submit; consumed/changed/expired clear quote, có countdown giữ tạm2phút
- **Điểm cần quan sát hoặc cải thiện:** Lost create response rồi TTL: create:44 chặn retry kể cả same key; khách phải tự vào Đơn tôi; quote.amount number không exact
- **Trạng thái hiện tại:** REPRODUCED UI FIXTURE cho disable sauTTL; integration mấtresponse saucommit NOT RUN. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C53](../reviews/ShuttleBook-ui-button-audit-alobo.md#c53) · [apps/customer-web/src/features/bookings/BookingPages.tsx:63](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L63)

<a id="c54"></a>

**C54 — Lấy báo giá mới**

- **Tiền điều kiện/state:** Disabled loading/submitting; dùng authenticated request
- **Agent có thể tự động hóa:** Đề xuất browser/API: Quote ởt=0 rồi new quote t=30s không kéo hạn tới150s; giá mới cần xác nhận
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Quote ởt=0 rồi new quote t=30s không kéo hạn tới150s; giá mới cần xác nhận
- **Expected đối chiếu:** POST /availability/quote, replace quote+intent Giữ phần tốt: Giá đổi/hết hạn cần xác nhận báo giá mới, không tạo order tự động
- **Điểm cần quan sát hoặc cải thiện:** Bấm trong TTL thay quote nhưng backend không kéo TTL; message chưa nói rõ refresh không gia hạn
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C54](../reviews/ShuttleBook-ui-button-audit-alobo.md#c54) · [apps/customer-web/src/features/bookings/BookingPages.tsx:65](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L65)

<a id="c55"></a>

**C55 — Xem Đơn của tôi**

- **Tiền điều kiện/state:** Luôn có review
- **Agent có thể tự động hóa:** Đề xuất browser/API: Create đã commit mất response rồi mở list chỉ1booking
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Create đã commit mất response rồi mở list chỉ1booking
- **Expected đối chiếu:** SPA /me/bookings Giữ phần tốt: Đường khôi phục khi network lost/QUOTE_CONSUMED, không cần tạo đơn mới
- **Điểm cần quan sát hoặc cải thiện:** Không có link trực tiếp đơn đã tạo nếu QUOTE_CONSUMED; phải chọn trong list
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C55](../reviews/ShuttleBook-ui-button-audit-alobo.md#c55) · [apps/customer-web/src/features/bookings/BookingPages.tsx:66](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L66)

#### Thiết lập cố định

<a id="c56"></a>

**C56 — Quay lại lịch các sân**

- **Tiền điều kiện/state:** Luôn có form
- **Agent có thể tự động hóa:** Đề xuất browser/API: Quote fixed rồi Back và chọn lại; không hiểu nhầm đã có booking
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Quote fixed rồi Back và chọn lại; không hiểu nhầm đã có booking
- **Expected đối chiếu:** SPA venue?date=start&mode=fixed, /venues nếu thiếu ID Giữ phần tốt: Giữ mode fixed/ngày khi quay lại lịch
- **Điểm cần quan sát hoặc cải thiện:** Quote hold toàn kỳ còn tới TTL, chưa phân biệt own hold với người khác
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C56](../reviews/ShuttleBook-ui-button-audit-alobo.md#c56) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:114](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L114)

<a id="c57"></a>

**C57 — Ngày trong tuần**

- **Tiền điều kiện/state:** Disabled loading/submitting bởi fieldset
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chọn weekday khác ngày start, preview first occurrence đúng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chọn weekday khác ngày start, preview first occurrence đúng
- **Expected đối chiếu:** Set weekday, invalidate quote/intent Giữ phần tốt: MONDAY–SUNDAY nhãn Việt; form đổi buộc quote mới
- **Điểm cần quan sát hoặc cải thiện:** Đổi weekday không tự điều chỉnh startsOn; ngày đầu kỳ có thể không là buổi đầu, cần preview rõ
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C57](../reviews/ShuttleBook-ui-button-audit-alobo.md#c57) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:119](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L119)

<a id="c58"></a>

**C58 — Giờ bắt đầu**

- **Tiền điều kiện/state:** Step1800; disabled busy
- **Agent có thể tự động hóa:** Đề xuất browser/API: 18:15 lỗi/focus feedback,18:30 valid; qua nửa đêm reject
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 18:15 lỗi/focus feedback,18:30 valid; qua nửa đêm reject
- **Expected đối chiếu:** Set time, invalidate quote, validate30phút Giữ phần tốt: Hỗ trợ picker và check mốc30phút trước gọi API
- **Điểm cần quan sát hoặc cải thiện:** HTML time picker có thể nhập mốc15phút, chỉ thấy lỗi khi quote; không offer available times
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C58](../reviews/ShuttleBook-ui-button-audit-alobo.md#c58) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:120](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L120)

<a id="c59"></a>

**C59 — Thời lượng mỗi buổi (phút)**

- **Tiền điều kiện/state:** min max120/court,step30,max1440; disabled busy
- **Agent có thể tự động hóa:** Đề xuất browser/API: 119/120/150/NaN/1440, min court lớn hơn120
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 119/120/150/NaN/1440, min court lớn hơn120
- **Expected đối chiếu:** Set duration, invalidate quote; validate >=minimum và chia hết30 Giữ phần tốt: 150phút/5ca được nhận, user thấy giờ kết thúc
- **Điểm cần quan sát hoặc cải thiện:** Phải nhập phút raw number, chưa preset2h/2.5h/3h; max1440 khác validation cùng ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C59](../reviews/ShuttleBook-ui-button-audit-alobo.md#c59) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:121](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L121)

<a id="c60"></a>

**C60 — Ngày bắt đầu kỳ**

- **Tiền điều kiện/state:** min venue-today,max+60; disabled busy
- **Agent có thể tự động hóa:** Đề xuất browser/API: Đổi31/01 sang tháng ngắn, endsOn cũ cần gợi ý sửa
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Đổi31/01 sang tháng ngắn, endsOn cũ cần gợi ý sửa
- **Expected đối chiếu:** Set startsOn, invalidate quote Giữ phần tốt: Horizon rõ và theo venue timezone
- **Điểm cần quan sát hoặc cải thiện:** Đổi startsOn không tự đẩy endsOn; có thể để kỳ <1tháng, lỗi không chỉ field cần sửa
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C60](../reviews/ShuttleBook-ui-button-audit-alobo.md#c60) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:122](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L122)

<a id="c61"></a>

**C61 — Ngày kết thúc kỳ**

- **Tiền điều kiện/state:** min calendarMonthAfter start,max today+60
- **Agent có thể tự động hóa:** Đề xuất browser/API: Kỳ<1tháng/>60ngày,31/01->28/02,13buổi backend reject
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Kỳ<1tháng/>60ngày,31/01->28/02,13buổi backend reject
- **Expected đối chiếu:** Set endsOn, invalidate quote Giữ phần tốt: Tối thiểu1tháng lịch; clamp cuối tháng có helper
- **Điểm cần quan sát hoặc cải thiện:** Horizon/max12buổi dùng note/text error, chưa preview số buổi trước quote
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C61](../reviews/ShuttleBook-ui-button-audit-alobo.md#c61) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:123](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L123)

#### Báo giá cố định

<a id="c62"></a>

**C62 — Xem báo giá toàn kỳ / Lấy báo giá mới cho kỳ / Đang kiểm tra toàn kỳ…**

- **Tiền điều kiện/state:** Disabled loading/submitting; busy ref; conflict200canCreatefalse
- **Agent có thể tự động hóa:** Đề xuất browser/API: Conflict tuần4 không create/partial hold; quote replacement thất bại giữ old hold tới TTL
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Conflict tuần4 không create/partial hold; quote replacement thất bại giữ old hold tới TTL
- **Expected đối chiếu:** POST /booking-series/quote, preview all, hold all nếu valid Giữ phần tốt: Conflict liệt kê ngày, không bỏ trùng/ngầm đặt phần trống; toàn kỳ hold120s
- **Điểm cần quan sát hoặc cải thiện:** Đổi form sau quote xóa quote UI nhưng hold cũ còn tới TTL; chưa thông báo điều này
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C62](../reviews/ShuttleBook-ui-button-audit-alobo.md#c62) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:126](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L126)

<a id="c63"></a>

**C63 — Xác nhận tạo lịch cố định / Thử lại tạo lịch cùng yêu cầu / Đang tạo lịch…**

- **Tiền điều kiện/state:** OnlycanCreate+quoteId; busy/expiry guard; attempted canRetry sauTTL
- **Agent có thể tự động hóa:** Đề xuất browser/API: Lost response -> expired retry1series;409consumed clear; focus heading detail mới
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Lost response -> expired retry1series;409consumed clear; focus heading detail mới
- **Expected đối chiếu:** POST /booking-series cùng key/quote; success canonical detail Giữ phần tốt: Retry mất response giữ key sauTTL, tổng exact/QR100%/all-or-none; clear bad intent
- **Điểm cần quan sát hoặc cải thiện:** API403/404 clear quote đúng; thành công chưa focus heading/scroll, cần kiểm keyboard
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C63](../reviews/ShuttleBook-ui-button-audit-alobo.md#c63) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:142](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L142)

<a id="c64"></a>

**C64 — Xem Đơn của tôi**

- **Tiền điều kiện/state:** Luôn có khi đã session
- **Agent có thể tự động hóa:** Đề xuất browser/API: Mất response create, mở list1group, quay canonical detail
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Mất response create, mở list1group, quay canonical detail
- **Expected đối chiếu:** SPA /me/bookings Giữ phần tốt: Thoát review khi không chắc request thành công, không tự tạo quote mới
- **Điểm cần quan sát hoặc cải thiện:** Không có filter cố định trên list, phải tìm seriesNo thủ công
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C64](../reviews/ShuttleBook-ui-button-audit-alobo.md#c64) · [apps/customer-web/src/features/bookings/SeriesReview.tsx:146](../../apps/customer-web/src/features/bookings/SeriesReview.tsx#L146)

#### Chi tiết đơn

<a id="c65"></a>

**C65 — Làm mới đơn**

- **Tiền điều kiện/state:** Luôn có, cả error/loading; không disable
- **Agent có thể tự động hóa:** Đề xuất browser/API: Stale GET sau report không khôi phục form; nhiều click và lỗi403 phải clear ảnh
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Stale GET sau report không khôi phục form; nhiều click và lỗi403 phải clear ảnh
- **Expected đối chiếu:** GET /bookings/{id} qua visible polling Giữ phần tốt: Version cũ không ghi đè command mới; 401/403/404 clear private details
- **Điểm cần quan sát hoặc cải thiện:** Không spinner/disable tại nút hoặc last-updated; liên tiếp click abort/restart đọc đơn
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C65](../reviews/ShuttleBook-ui-button-audit-alobo.md#c65) · [apps/customer-web/src/features/bookings/BookingPages.tsx:118](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L118)

<a id="c66"></a>

**C66 — Đơn của tôi**

- **Tiền điều kiện/state:** Luôn có kể cả error
- **Agent có thể tự động hóa:** Đề xuất browser/API: 404 chi tiết -> list, các nhóm booking/status đúng
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 404 chi tiết -> list, các nhóm booking/status đúng
- **Expected đối chiếu:** SPA /me/bookings Giữ phần tốt: Đơn không tìm thấy có lối về list; no cancel/change đúng nghiệp vụ
- **Điểm cần quan sát hoặc cải thiện:** Danh sách chưa filter status/ngày/casual-fixed nên tra cứu lịch sử nhiều đơn chậm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C66](../reviews/ShuttleBook-ui-button-audit-alobo.md#c66) · [apps/customer-web/src/features/bookings/BookingPages.tsx:118](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L118)

#### Đơn chờ chuyển

<a id="c67"></a>

**C67 — Đã chuyển khoản**

- **Tiền điều kiện/state:** AWAITING_TRANSFER; disable quá deadline trừ retry intent hợp lệ
- **Agent có thể tự động hóa:** Đề xuất browser/API: Open chỉ hiện form không POST; deadline vừa tới disable; keyboard focus
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Open chỉ hiện form không POST; deadline vừa tới disable; keyboard focus
- **Expected đối chiếu:** Mở payment form, chưa POST Giữ phần tốt: Không coi bấm opener là PAID, có bước gửi rõ và owner confirm
- **Điểm cần quan sát hoặc cải thiện:** Mở form chưa move focus đến heading/file; chưa có Đóng/Hủy form, customer phải đi trang khác
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C67](../reviews/ShuttleBook-ui-button-audit-alobo.md#c67) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:108](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L108)

#### Đơn cần đối chiếu

<a id="c68"></a>

**C68 — Bổ sung bằng chứng**

- **Tiền điều kiện/state:** NEEDS_REVIEW, không bị deadline chuyển cũ khóa
- **Agent có thể tự động hóa:** Đề xuất browser/API: NEEDS_REVIEW sau deadline vẫn mở form, không auto tạo đơn/cancel
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: NEEDS_REVIEW sau deadline vẫn mở form, không auto tạo đơn/cancel
- **Expected đối chiếu:** Mở form bổ sung cùng booking/payment Giữ phần tốt: Không phải tạo đơn/chuyển tiền lại; lịch toàn kỳ vẫn giữ
- **Điểm cần quan sát hoặc cải thiện:** Không focus form hay đóng form; note không đọc cho người dùng bằng field-specific help
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C68](../reviews/ShuttleBook-ui-button-audit-alobo.md#c68) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:108](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L108)

#### Báo chuyển/Bổ sung

<a id="c69"></a>

**C69 — Ảnh chụp màn hình chuyển khoản (không bắt buộc)**

- **Tiền điều kiện/state:** Optional; disabled pending; file MIME/size validation và filename
- **Agent có thể tự động hóa:** Đề xuất browser/API: 0byte/5MB/5MB+1/PDF/fake MIME, valid image, upload mất response và READY retry
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 0byte/5MB/5MB+1/PDF/fake MIME, valid image, upload mất response và READY retry
- **Expected đối chiếu:** Chọn png/jpeg/webp <=5MB; submit presign -> PUT -> complete READY Giữ phần tốt: Proof riêng, checksum SHA256, media complete kiểm READY; retry không upload lại file READY
- **Điểm cần quan sát hoặc cải thiện:** Không preview ảnh đã chọn, chỉ filename; không camera capture hint; chưa browser/provider thật mobile/S3
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C69](../reviews/ShuttleBook-ui-button-audit-alobo.md#c69) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:113](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L113)

<a id="c70"></a>

**C70 — Bỏ ảnh biên lai**

- **Tiền điều kiện/state:** Chỉ file hoặc fileError; disabled pending
- **Agent có thể tự động hóa:** Đề xuất browser/API: Chọn ảnh lỗi/valid rồi bỏ, submit không proofUploadId và không upload
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Chọn ảnh lỗi/valid rồi bỏ, submit không proofUploadId và không upload
- **Expected đối chiếu:** Clear file input, upload intent và lỗi file Giữ phần tốt: File sai được gỡ để gửi report không ảnh; không bắt mã giao dịch
- **Điểm cần quan sát hoặc cải thiện:** Không xóa upload đã READY trên server, orphan cleanup lifecycle cần provider/operational design riêng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C70](../reviews/ShuttleBook-ui-button-audit-alobo.md#c70) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:119](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L119)

<a id="c71"></a>

**C71 — Ghi chú (không bắt buộc)**

- **Tiền điều kiện/state:** Optional, maxLength1000; disabled pending
- **Agent có thể tự động hóa:** Đề xuất browser/API: Note1000/1001, unicode, emptytrim; mạng lỗi vẫn giữ note để retry
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Note1000/1001, unicode, emptytrim; mạng lỗi vẫn giữ note để retry
- **Expected đối chiếu:** Set note, trim, tối đa1000char Giữ phần tốt: Có label bằng useId; screenshot-only report không cần giao dịch dài
- **Điểm cần quan sát hoặc cải thiện:** Không đếm ký tự còn lại; note draft mất khi F5/logout theo memory
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C71](../reviews/ShuttleBook-ui-button-audit-alobo.md#c71) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:120](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L120)

#### Đơn chờ chuyển

<a id="c72"></a>

**C72 — Gửi báo chuyển khoản / Đang xử lý…**

- **Tiền điều kiện/state:** disable pending/expired/mustReload/fileError; busyref
- **Agent có thể tự động hóa:** Đề xuất browser/API: Lost response qua deadline replay cùngbody/key, editbody không được báo trễ;412 requires reload
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Lost response qua deadline replay cùngbody/key, editbody không được báo trễ;412 requires reload
- **Expected đối chiếu:** POST /bookings/{id}/transfer-evidence cùng key/If-Match Giữ phần tốt: Report screenshot-only/không ảnh, một intent; lost response replay sau deadline khi body không đổi
- **Điểm cần quan sát hoặc cải thiện:** Form errors role alert nhưng không auto focus, keyboard phải dò lại; chưa có cancel form
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C72](../reviews/ShuttleBook-ui-button-audit-alobo.md#c72) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:123](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L123)

#### Đơn cần đối chiếu

<a id="c73"></a>

**C73 — Gửi bổ sung bằng chứng / Đang xử lý…**

- **Tiền điều kiện/state:** NEEDS_REVIEW; deadline cũ không khóa; pending/mustReload guards
- **Agent có thể tự động hóa:** Đề xuất browser/API: Sau owner NEEDS_REVIEW, gửi bổ sung có/không ảnh, history giữ bằng chứng cũ
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Sau owner NEEDS_REVIEW, gửi bổ sung có/không ảnh, history giữ bằng chứng cũ
- **Expected đối chiếu:** POST cùng transfer-evidence kind SUPPLEMENT Giữ phần tốt: Bổ sung append history, owner đối chiếu lại, không giải phóng lịch hay báo PAID
- **Điểm cần quan sát hoặc cải thiện:** Không mở note/reason ngay sát field, khách phải đọc lịch sử phía khác để biết cần bổ sung gì
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C73](../reviews/ShuttleBook-ui-button-audit-alobo.md#c73) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:123](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L123)

#### Báo chuyển conflict

<a id="c74"></a>

**C74 — Tải lại để kiểm tra đơn**

- **Tiền điều kiện/state:** Chỉ mustReload sau409/412/deadline; disabled pending
- **Agent có thể tự động hóa:** Đề xuất browser/API: 412 -> draft intact -> reload newer version -> chỉ submit khi user bấm lại
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 412 -> draft intact -> reload newer version -> chỉ submit khi user bấm lại
- **Expected đối chiếu:** GET /bookings/{id}, clear intent/mustReload, giữ draft Giữ phần tốt: Không tự replay stale version, user kiểm trạng thái rồi chủ động submit
- **Điểm cần quan sát hoặc cải thiện:** Label chỉ reload, chưa nhắc đã giữ draft ở UI; cần xác minh trạng thái đổi làm form unmount đúng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C74](../reviews/ShuttleBook-ui-button-audit-alobo.md#c74) · [apps/customer-web/src/features/bookings/BookingTransfer.tsx:124](../../apps/customer-web/src/features/bookings/BookingTransfer.tsx#L124)

#### QR/ảnh lịch sử lỗi

<a id="c75"></a>

**C75 — Tải lại ảnh**

- **Tiền điều kiện/state:** Chỉ image error; allowed API route regex, blob revoked cleanup
- **Agent có thể tự động hóa:** Đề xuất browser/API: Private403/no arbitraryURL/retry, S3 redirect credentials, thử QR và ảnh full-size trên mobile
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Private403/no arbitraryURL/retry, S3 redirect credentials, thử QR và ảnh full-size trên mobile
- **Expected đối chiếu:** Refetch authenticated private path -> object URL Giữ phần tốt: Không mở URL tùy ý; private QR/proof không gửi bearer sang public URL
- **Điểm cần quan sát hoặc cải thiện:** Không có phóng to/tải ảnh QR/proof và không copy tiền/nội dung; mobile một máy khó quét QR upload tĩnh
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C75](../reviews/ShuttleBook-ui-button-audit-alobo.md#c75) · [apps/customer-web/src/features/bookings/PrivateImage.tsx:19](../../apps/customer-web/src/features/bookings/PrivateImage.tsx#L19)

#### Đơn của tôi

<a id="c76"></a>

**C76 — {item.series?.seriesNo ?? item.bookingNo}**

- **Tiền điều kiện/state:** Mỗi item; nhóm fixed một row/tổng cả kỳ/status
- **Agent có thể tự động hóa:** Đề xuất browser/API: Casual/fixed một group, từng6statuses, 18digit exact, screen-reader links list
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Casual/fixed một group, từng6statuses, 18digit exact, screen-reader links list
- **Expected đối chiếu:** SPA /bookings/{bookingId} Giữ phần tốt: Mã đơn rõ, không nhân bản series thành nhiều payment; tiền ưu tiên exact
- **Điểm cần quan sát hoặc cải thiện:** Link chỉ mã, accessible name không có venue/date; không thấy nhãn hành động Xem đơn ngay
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C76](../reviews/ShuttleBook-ui-button-audit-alobo.md#c76) · [apps/customer-web/src/features/bookings/BookingPages.tsx:132](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L132)

<a id="c77"></a>

**C77 — Làm mới danh sách**

- **Tiền điều kiện/state:** Luôn có; không disable loading
- **Agent có thể tự động hóa:** Đề xuất browser/API: Tải3pages rồi refresh, kiểm reset và vị trí scroll; scope403 clearitems
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Tải3pages rồi refresh, kiểm reset và vị trí scroll; scope403 clearitems
- **Expected đối chiếu:** Reset pageCursor -> first page; nếu first thì refresh Giữ phần tốt: Khách chủ động về dữ liệu mới nhất, session-required guard
- **Điểm cần quan sát hoặc cải thiện:** Khi đã tải nhiều trang nút reset toàn bộ list; không giải thích sẽ về trang đầu hay giữ scroll
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C77](../reviews/ShuttleBook-ui-button-audit-alobo.md#c77) · [apps/customer-web/src/features/bookings/BookingPages.tsx:136](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L136)

#### Đơn của tôi pagination

<a id="c78"></a>

**C78 — Xem thêm đơn**

- **Tiền điều kiện/state:** Chỉ cursor; không disabled loading
- **Agent có thể tự động hóa:** Đề xuất browser/API: Delay page2 và click nhiều, filter duplicates; newstatus ởpage1 sau chuyểnpage3 không stale dài
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Delay page2 và click nhiều, filter duplicates; newstatus ởpage1 sau chuyểnpage3 không stale dài
- **Expected đối chiếu:** Set pageCursor và GET before rồi merge dedup Giữ phần tốt: Merge bằng bookingId chống trùng trên trang hiện tại
- **Điểm cần quan sát hoặc cải thiện:** Không disable pending; button vẫn cho click nhiều với cursor cũ; polling chỉ refresh trang active nên đầu list có thể stale
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C78](../reviews/ShuttleBook-ui-button-audit-alobo.md#c78) · [apps/customer-web/src/features/bookings/BookingPages.tsx:137](../../apps/customer-web/src/features/bookings/BookingPages.tsx#L137)

#### Inbox

<a id="c79"></a>

**C79 — Làm mới thông báo**

- **Tiền điều kiện/state:** Luôn có; loading status ở page
- **Agent có thể tự động hóa:** Đề xuất browser/API: 403/404 refetch kiểm cache/badge; note normal suspended401 refresh sẽ clearsession
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: 403/404 refetch kiểm cache/badge; note normal suspended401 refresh sẽ clearsession
- **Expected đối chiếu:** Reload first notifications via provider polling Giữ phần tốt: Unread count từ API và polling khi visible, không fake analytics
- **Điểm cần quan sát hoặc cải thiện:** Không disabled loading; provider error không clear cached items với403/404, cần resilience test chứ chưa runtime bug
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C79](../reviews/ShuttleBook-ui-button-audit-alobo.md#c79) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:71](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L71)

#### Inbox trống

<a id="c80"></a>

**C80 — Tìm sân & xem lịch**

- **Tiền điều kiện/state:** Chỉ !loading,!items,!error
- **Agent có thể tự động hóa:** Đề xuất browser/API: Empty inbox -> find venues giữ login; error không báo Bạn đã theo dõi mọi cập nhật sai
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Empty inbox -> find venues giữ login; error không báo Bạn đã theo dõi mọi cập nhật sai
- **Expected đối chiếu:** SPA /venues Giữ phần tốt: Empty inbox vẫn có next action đặt sân
- **Điểm cần quan sát hoặc cải thiện:** Chưa điều hướng tới đơn chờ hoặc recent sân, vẫn list tổng quát
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C80](../reviews/ShuttleBook-ui-button-audit-alobo.md#c80) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:74](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L74)

#### Inbox notification

<a id="c81"></a>

**C81 — Xem đơn đặt sân**

- **Tiền điều kiện/state:** Chỉ action hợp lệ/customer UUID; no external href
- **Agent có thể tự động hóa:** Đề xuất browser/API: CLICK link khi read503 vẫn mở đúng đơn, quay inbox còn unread và thấy lỗi
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: CLICK link khi read503 vẫn mở đúng đơn, quay inbox còn unread và thấy lỗi
- **Expected đối chiếu:** Validated CUSTOMER_BOOKING + UUID -> SPA booking, async markRead Giữ phần tốt: Chống link operator/admin/arbitrary URL; xem đơn cũng đánh dấu đọc
- **Điểm cần quan sát hoặc cải thiện:** Không đợi read hoàn thành trước navigate; readerror có thể chỉ thấy sau quay inbox, chưa loading/readfeedback tại row
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C81](../reviews/ShuttleBook-ui-button-audit-alobo.md#c81) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:78](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L78)

<a id="c82"></a>

**C82 — Đánh dấu đã đọc**

- **Tiền điều kiện/state:** Chỉ unread; reading Set chống đồng thời; chưa disabled/spinnerrow
- **Agent có thể tự động hóa:** Đề xuất browser/API: Doubleclick chỉ1POST, networkfail vẫn unread, successrow/badge cập nhật
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Doubleclick chỉ1POST, networkfail vẫn unread, successrow/badge cập nhật
- **Expected đối chiếu:** POST /me/notifications/{id}/read; refresh count Giữ phần tốt: Idempotent read, older item cũng cập nhật, badge thật
- **Điểm cần quan sát hoặc cải thiện:** Nút vẫn enabled lúc POST nhưng serverrequest dedup, người dùng không thấy trạng thái đang xử lý; không mark all
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C82](../reviews/ShuttleBook-ui-button-audit-alobo.md#c82) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:79](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L79)

#### Inbox pagination

<a id="c83"></a>

**C83 — Xem thêm thông báo / Đang tải…**

- **Tiền điều kiện/state:** Chỉ nextCursor; disabled paging; AbortController unmount
- **Agent có thể tự động hóa:** Đề xuất browser/API: Loadolder -> nhận nhiều notification mới -> refresh rồi tải thêm; no gaps/duplicates; logout unmount
- **Bạn thao tác/đánh giá:** Thao tác tay desktop/mobile: Loadolder -> nhận nhiều notification mới -> refresh rồi tải thêm; no gaps/duplicates; logout unmount
- **Expected đối chiếu:** GET notifications?before=... append older, dedup items Giữ phần tốt: Không reset older khi providerpoll firstpage, tránh lặp bằngid
- **Điểm cần quan sát hoặc cải thiện:** Sau refresh firstpage nhiều newitems có thể gap giữa cửa sổ first và older; chưa guard logout cache older riêng trongpage
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét C83](../reviews/ShuttleBook-ui-button-audit-alobo.md#c83) · [apps/customer-web/src/features/notifications/CustomerNotifications.tsx:81](../../apps/customer-web/src/features/notifications/CustomerNotifications.tsx#L81)

### Partner

#### Auth

<a id="p01"></a>

**P01 — ShuttleBook**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "ShuttleBook" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "ShuttleBook" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Về /, reload Giữ phần tốt: Brand nhất quán
- **Điểm cần quan sát hoặc cải thiện:** Reload mất form chưa gửi
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P01](../reviews/ShuttleBook-ui-button-audit-alobo.md#p01) · [apps/partner-web/src/main.tsx:135](../../apps/partner-web/src/main.tsx#L135)

<a id="p02"></a>

**P02 — Đăng ký (chuyển màn)**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Đăng ký (chuyển màn)" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Đăng ký (chuyển màn)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** setView(register), clear message Giữ phần tốt: Đổi màn không request
- **Điểm cần quan sát hoặc cải thiện:** Không disable khi gửi, response muộn có thể đổi view sau đó
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P02](../reviews/ShuttleBook-ui-button-audit-alobo.md#p02) · [apps/partner-web/src/main.tsx:147](../../apps/partner-web/src/main.tsx#L147)

<a id="p03"></a>

**P03 — Đăng nhập (chuyển màn)**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Đăng nhập (chuyển màn)" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Đăng nhập (chuyển màn)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** setView(login), clear message Giữ phần tốt: Tách đăng nhập/đăng ký
- **Điểm cần quan sát hoặc cải thiện:** Response register/verify muộn có thể đổi view lại
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P03](../reviews/ShuttleBook-ui-button-audit-alobo.md#p03) · [apps/partner-web/src/main.tsx:148](../../apps/partner-web/src/main.tsx#L148)

<a id="p04"></a>

**P04 — Phương thức liên hệ**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Phương thức liên hệ" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Phương thức liên hệ" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Email/Số điện thoại register và login Giữ phần tốt: Hỗ trợ hai contact
- **Điểm cần quan sát hoặc cải thiện:** Phone09 chưa chuẩn hóa E.164; label kỹ thuật
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P04](../reviews/ShuttleBook-ui-button-audit-alobo.md#p04) · [apps/partner-web/src/main.tsx:153](../../apps/partner-web/src/main.tsx#L153)

<a id="p05"></a>

**P05 — Đăng ký**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Fields: Email hoặc Số điện thoại E.164 required type email/tel; Mật khẩu required password minLength12; Nhập lại mật khẩu required password. Confirm mismatch không POST; input chưa disabled khi submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Đăng ký" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Đăng ký" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST partner-auth/register → verify Giữ phần tốt: Confirm password trước API;202 riêng tư; busy disable
- **Điểm cần quan sát hoặc cải thiện:** Error chưa chỉ field sai, không quên mật khẩu
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P05](../reviews/ShuttleBook-ui-button-audit-alobo.md#p05) · [apps/partner-web/src/main.tsx:165](../../apps/partner-web/src/main.tsx#L165)

<a id="p06"></a>

**P06 — Mailpit**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Mailpit" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Mailpit" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Tab localhost8025 chỉ DEV Giữ phần tốt: Nói rõ local không gửi contact thật
- **Điểm cần quan sát hoặc cải thiện:** Provider production chưa kiểm, không phải ưu thế ALOBO
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P06](../reviews/ShuttleBook-ui-button-audit-alobo.md#p06) · [apps/partner-web/src/main.tsx:171](../../apps/partner-web/src/main.tsx#L171)

<a id="p07"></a>

**P07 — Xác minh**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Field: Mã xác minh6 chữ số required, inputMode numeric, pattern[0-9]{6}, maxLength6; onChange bỏ ký tự không phải số; mã sai/hết hạn feedback chung.
- **Agent có thể tự động hóa:** Browser fixture: "Xác minh" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Xác minh" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST partner-auth/verify → login Giữ phần tốt: 6 số numeric sanitize; busy Đang kiểm tra…
- **Điểm cần quan sát hoặc cải thiện:** Không countdown hạn OTP
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P07](../reviews/ShuttleBook-ui-button-audit-alobo.md#p07) · [apps/partner-web/src/main.tsx:174](../../apps/partner-web/src/main.tsx#L174)

<a id="p08"></a>

**P08 — Gửi lại mã**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting.
- **Agent có thể tự động hóa:** Browser fixture: "Gửi lại mã" ở Auth, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Gửi lại mã" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST verification-resend Giữ phần tốt: 202 riêng tư;429 Retry-After;busy disable
- **Điểm cần quan sát hoặc cải thiện:** Không cooldown trước click, chung submitting
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P08](../reviews/ShuttleBook-ui-button-audit-alobo.md#p08) · [apps/partner-web/src/main.tsx:175](../../apps/partner-web/src/main.tsx#L175)

<a id="p09"></a>

**P09 — Đăng nhập**

- **Tiền điều kiện/state:** Guest. Chuyển màn theo view; submit/resend khóa khi submitting, login/register/verify đổi nhãn busy; message status lỗi API/mạng/429 và thành công. Nav và field chưa khóa theo submitting. Fields: Email hoặc Số điện thoại E.164 required; Mật khẩu required password. Input chưa có autocomplete credential; không nút hiện/ẩn mật khẩu hay quên mật khẩu.
- **Agent có thể tự động hóa:** partner-identity.spec.ts đăng ký/login/sai role; f06-partner.spec.ts:114 F5 giữ deeplink nhưng login lại, đúng memory policy. Không coi test lịch sử là gate mới.
- **Bạn thao tác/đánh giá:** Auth: sử dụng "Đăng nhập" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST auth/login→onboarding Giữ phần tốt: Chặn sai role;clear password;busy label
- **Điểm cần quan sát hoặc cải thiện:** F5 login lại là memory policy hiện tại, productgapP2; không tự gán bug idle
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P09](../reviews/ShuttleBook-ui-button-audit-alobo.md#p09) · [apps/partner-web/src/main.tsx:188](../../apps/partner-web/src/main.tsx#L188)

#### Shell

<a id="p10"></a>

**P10 — Đến nội dung chính**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Đến nội dung chính" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Đến nội dung chính" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Focus #partner-content Giữ phần tốt: Keyboard bỏ sidebar
- **Điểm cần quan sát hoặc cải thiện:** Screen reader mới NOT RUN
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P10](../reviews/ShuttleBook-ui-button-audit-alobo.md#p10) · [apps/partner-web/src/layouts/PartnerShell.tsx:24](../../apps/partner-web/src/layouts/PartnerShell.tsx#L24)

<a id="p11"></a>

**P11 — ShuttleBook desktop/mobile**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "ShuttleBook desktop/mobile" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "ShuttleBook desktop/mobile" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/overview Giữ phần tốt: Hash SPA giữ memory session
- **Điểm cần quan sát hoặc cải thiện:** Rời form không cảnh báo chưa lưu
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P11](../reviews/ShuttleBook-ui-button-audit-alobo.md#p11) · [apps/partner-web/src/layouts/PartnerShell.tsx:28](../../apps/partner-web/src/layouts/PartnerShell.tsx#L28)

<a id="p12"></a>

**P12 — Menu**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** partner-workspace.spec.ts:86 Escape trả focus, Back/deeplink, unique map labels. Root có browser gate riêng mới.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Menu" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Mở/đóng nav;Escape trả focus Giữ phần tốt: aria-expanded/controls,Escape
- **Điểm cần quan sát hoặc cải thiện:** Không suy WCAG PASS từ source
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P12](../reviews/ShuttleBook-ui-button-audit-alobo.md#p12) · [apps/partner-web/src/layouts/PartnerShell.tsx:29](../../apps/partner-web/src/layouts/PartnerShell.tsx#L29)

<a id="p13"></a>

**P13 — Tổng quan**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Tổng quan" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Tổng quan" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/overview Giữ phần tốt: Current page+heading focus
- **Điểm cần quan sát hoặc cải thiện:** Dashboard cấu hình, thiếu lịch/công suất/doanh thu hôm nay
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P13](../reviews/ShuttleBook-ui-button-audit-alobo.md#p13) · [apps/partner-web/src/features/workspace/navigation.ts:4](../../apps/partner-web/src/features/workspace/navigation.ts#L4)

<a id="p14"></a>

**P14 — Hồ sơ doanh nghiệp**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Hồ sơ doanh nghiệp" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Hồ sơ doanh nghiệp" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/profile Giữ phần tốt: Draft edit/published readonly
- **Điểm cần quan sát hoặc cải thiện:** Chưa sửa legalName/contact doanh nghiệp active
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P14](../reviews/ShuttleBook-ui-button-audit-alobo.md#p14) · [apps/partner-web/src/features/workspace/navigation.ts:5](../../apps/partner-web/src/features/workspace/navigation.ts#L5)

<a id="p15"></a>

**P15 — Cơ sở**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/venues Giữ phần tốt: Phân cấp venue rõ
- **Điểm cần quan sát hoặc cải thiện:** Active không add venue
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P15](../reviews/ShuttleBook-ui-button-audit-alobo.md#p15) · [apps/partner-web/src/features/workspace/navigation.ts:6](../../apps/partner-web/src/features/workspace/navigation.ts#L6)

<a id="p16"></a>

**P16 — Sân**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Sân" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/courts Giữ phần tốt: Tên sân theo venue
- **Điểm cần quan sát hoặc cải thiện:** Active không add/rename/suspend; chưa search nhiều sân
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P16](../reviews/ShuttleBook-ui-button-audit-alobo.md#p16) · [apps/partner-web/src/features/workspace/navigation.ts:7](../../apps/partner-web/src/features/workspace/navigation.ts#L7)

<a id="p17"></a>

**P17 — Lịch & giá**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Lịch & giá" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Lịch & giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/schedule Giữ phần tốt: Draft setup/active operations riêng
- **Điểm cần quan sát hoặc cải thiện:** Partner chưa lịch ngày mọi sân, dropdown từng sân
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P17](../reviews/ShuttleBook-ui-button-audit-alobo.md#p17) · [apps/partner-web/src/features/workspace/navigation.ts:8](../../apps/partner-web/src/features/workspace/navigation.ts#L8)

<a id="p18"></a>

**P18 — Đơn đặt sân**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Đơn đặt sân" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Đơn đặt sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/bookings Giữ phần tốt: Pending giải thích không vận hành
- **Điểm cần quan sát hoặc cải thiện:** Chưa tạo đơn khách gọi điện/quầy, chưa calendar ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P18](../reviews/ShuttleBook-ui-button-audit-alobo.md#p18) · [apps/partner-web/src/features/workspace/navigation.ts:9](../../apps/partner-web/src/features/workspace/navigation.ts#L9)

<a id="p19"></a>

**P19 — Ảnh cơ sở**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Ảnh cơ sở" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Ảnh cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/media Giữ phần tốt: Tách ảnh khỏi QR
- **Điểm cần quan sát hoặc cải thiện:** Active chưa thay ảnh; không thumbnail/gallery
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P19](../reviews/ShuttleBook-ui-button-audit-alobo.md#p19) · [apps/partner-web/src/features/workspace/navigation.ts:10](../../apps/partner-web/src/features/workspace/navigation.ts#L10)

<a id="p20"></a>

**P20 — Thanh toán & QR**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Thanh toán & QR" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Thanh toán & QR" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/payments Giữ phần tốt: Revision giữ bản hiện hành khi chờ duyệt
- **Điểm cần quan sát hoặc cải thiện:** QR tĩnh, chưa preview QR hiện hành
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P20](../reviews/ShuttleBook-ui-button-audit-alobo.md#p20) · [apps/partner-web/src/features/workspace/navigation.ts:11](../../apps/partner-web/src/features/workspace/navigation.ts#L11)

<a id="p21"></a>

**P21 — Thông báo**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Thông báo" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Thông báo" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/notifications Giữ phần tốt: Badge tổng unread
- **Điểm cần quan sát hoặc cải thiện:** Chưa unreadfilter/search/markall
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P21](../reviews/ShuttleBook-ui-button-audit-alobo.md#p21) · [apps/partner-web/src/features/workspace/navigation.ts:12](../../apps/partner-web/src/features/workspace/navigation.ts#L12)

<a id="p22"></a>

**P22 — Đăng xuất**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Focused PostGIS barrier refresh/logout cùng family, mọi token sau logout bị từ chối; runtime race mới NOT RUN. partner-identity.spec.ts chỉ chứng minh logout local lịch sử.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Đăng xuất" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST auth/logout→clear memory login Giữ phần tốt: aria-label, disabledbusy;local đóng cả khi mạng lỗi
- **Điểm cần quan sát hoặc cải thiện:** Refresh/logout familyrace SOURCE_RISK cần PostGIS, chưa runtime xác nhận
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P22](../reviews/ShuttleBook-ui-button-audit-alobo.md#p22) · [apps/partner-web/src/layouts/PartnerShell.tsx:42](../../apps/partner-web/src/layouts/PartnerShell.tsx#L42)

<a id="p23"></a>

**P23 — Doanh nghiệp**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** partner-workspace.spec.ts:60 draftscope cleared; f06-partner.spec.ts:92 private detail cleared. Kiểm thêm loadmuộn và command pending.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Doanh nghiệp" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Load scope mới/resetdraft/close detail Giữ phần tốt: Disabled busy/loading/bookingBusy; xóa scope cũ
- **Điểm cần quan sát hoặc cải thiện:** Không confirm mất draft; không searchable
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P23](../reviews/ShuttleBook-ui-button-audit-alobo.md#p23) · [apps/partner-web/src/PartnerOnboarding.tsx:156](../../apps/partner-web/src/PartnerOnboarding.tsx#L156)

<a id="p24"></a>

**P24 — Thử tải lại hồ sơ**

- **Tiền điều kiện/state:** Session VENUE_OPERATOR pending/active. Nav mọi trạng thái, aria-current trang; business khóa busy/loading/bookingBusy, logout khóa logoutBusy. F5 mất session memory theo policy hiện tại.
- **Agent có thể tự động hóa:** Browser fixture: "Thử tải lại hồ sơ" ở Shell, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Shell: sử dụng "Thử tải lại hồ sơ" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** GET businesses/detail Giữ phần tốt: Loadfail không dựng createform giả
- **Điểm cần quan sát hoặc cải thiện:** Error kỹ thuật code chưa hướng dẫn field/next action
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P24](../reviews/ShuttleBook-ui-button-audit-alobo.md#p24) · [apps/partner-web/src/PartnerOnboarding.tsx:165](../../apps/partner-web/src/PartnerOnboarding.tsx#L165)

#### Tổng quan

<a id="p25"></a>

**P25 — Xem hồ sơ / Quản lý lịch & giá**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Xem hồ sơ / Quản lý lịch & giá" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Xem hồ sơ / Quản lý lịch & giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Draft→profile/active→schedule Giữ phần tốt: CTA theo giai đoạn owner
- **Điểm cần quan sát hoặc cải thiện:** Active tới cấu hình thay lịch đặt ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P25](../reviews/ShuttleBook-ui-button-audit-alobo.md#p25) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:23](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L23)

<a id="p26"></a>

**P26 — Thông tin doanh nghiệp**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Thông tin doanh nghiệp" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Thông tin doanh nghiệp" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/profile Giữ phần tốt: Dẫn form liên quan
- **Điểm cần quan sát hoặc cải thiện:** done=true theo tồn tại record, không validation completeness
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P26](../reviews/ShuttleBook-ui-button-audit-alobo.md#p26) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:13](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L13)

<a id="p27"></a>

**P27 — Cơ sở và sân**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở và sân" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Cơ sở và sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/venues Giữ phần tốt: Mỗi venue phải có court
- **Điểm cần quan sát hoặc cải thiện:** Thiếu sân vẫn trỏ venues thêm bước courts
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P27](../reviews/ShuttleBook-ui-button-audit-alobo.md#p27) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:14](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L14)

<a id="p28"></a>

**P28 — Giờ hoạt động và bảng giá**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Giờ hoạt động và bảng giá" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Giờ hoạt động và bảng giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/schedule Giữ phần tốt: Check tất cả court có dữ liệu
- **Điểm cần quan sát hoặc cải thiện:** Có rows không chứng minh đủ giá/không gap
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P28](../reviews/ShuttleBook-ui-button-audit-alobo.md#p28) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:15](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L15)

<a id="p29"></a>

**P29 — Ảnh cơ sở**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Ảnh cơ sở" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Ảnh cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/media Giữ phần tốt: Check mọi venue có imageId
- **Điểm cần quan sát hoặc cải thiện:** Chưa thumbnail để kiểm nội dung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P29](../reviews/ShuttleBook-ui-button-audit-alobo.md#p29) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:16](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L16)

<a id="p30"></a>

**P30 — Tài khoản nhận tiền và QR**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Tài khoản nhận tiền và QR" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Tài khoản nhận tiền và QR" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/payments Giữ phần tốt: Check mọi venue paymentAccount
- **Điểm cần quan sát hoặc cải thiện:** Boolean không chứng minh QR scan đúng tài khoản
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P30](../reviews/ShuttleBook-ui-button-audit-alobo.md#p30) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:17](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L17)

<a id="p31"></a>

**P31 — Quản lý / Thêm cơ sở**

- **Tiền điều kiện/state:** Có detail. CTA theo ACTIVE/DRAFT; checklist/counters theo dữ liệu; Thêm cơ sở chỉ empty; link không mutate API.
- **Agent có thể tự động hóa:** Browser fixture: "Quản lý / Thêm cơ sở" ở Tổng quan, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Tổng quan: sử dụng "Quản lý / Thêm cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/venues;Thêm chỉ empty Giữ phần tốt: Empty có next action
- **Điểm cần quan sát hoặc cải thiện:** Active zero-venue edge chưa có addform; không suy gặp thực tế
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P31](../reviews/ShuttleBook-ui-button-audit-alobo.md#p31) · [apps/partner-web/src/features/workspace/PartnerOverview.tsx:39](../../apps/partner-web/src/features/workspace/PartnerOverview.tsx#L39)

#### Hồ sơ nháp

<a id="p32"></a>

**P32 — Tạo hồ sơ nháp**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên hiển thị, Tên pháp lý, Liên hệ đều required; businessName/legalName/contact local; không ghi secret.
- **Agent có thể tự động hóa:** Browser fixture: "Tạo hồ sơ nháp" ở Hồ sơ nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Hồ sơ nháp: sử dụng "Tạo hồ sơ nháp" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST business→venues Giữ phần tốt: Business trước venue/court;busyfieldset
- **Điểm cần quan sát hoặc cải thiện:** Genericvalidation, draft mấtF5
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P32](../reviews/ShuttleBook-ui-button-audit-alobo.md#p32) · [apps/partner-web/src/PartnerOnboarding.tsx:184](../../apps/partner-web/src/PartnerOnboarding.tsx#L184)

<a id="p33"></a>

**P33 — Gửi hồ sơ duyệt**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Thiếu từng image/QR/price/hour phải báo actionable; pending no mutate; doubleclick/concurrency DB.
- **Bạn thao tác/đánh giá:** Hồ sơ nháp: sử dụng "Gửi hồ sơ duyệt" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST business/submit +reload Giữ phần tốt: DRAFT-only;backend readiness
- **Điểm cần quan sát hoặc cải thiện:** Chưa disable theo readiness/thiếu fieldlink khi400
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P33](../reviews/ShuttleBook-ui-button-audit-alobo.md#p33) · [apps/partner-web/src/PartnerOnboarding.tsx:188](../../apps/partner-web/src/PartnerOnboarding.tsx#L188)

#### Hồ sơ

<a id="p34"></a>

**P34 — Tải lại hồ sơ**

- **Tiền điều kiện/state:** Có label, các trạng thái hiển thị/disabled được đọc từ source; không quy đổi thành runtime PASS.
- **Agent có thể tự động hóa:** Browser fixture: "Tải lại hồ sơ" ở Hồ sơ, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Hồ sơ: sử dụng "Tải lại hồ sơ" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** GET detail Giữ phần tốt: Disabled busy/loading; cập nhật Admin status
- **Điểm cần quan sát hoặc cải thiện:** Rerender version có thể mất edit chưa lưu
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P34](../reviews/ShuttleBook-ui-button-audit-alobo.md#p34) · [apps/partner-web/src/PartnerOnboarding.tsx:191](../../apps/partner-web/src/PartnerOnboarding.tsx#L191)

#### Hồ sơ nháp

<a id="p35"></a>

**P35 — Lưu thay đổi**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên, Tên pháp lý, Liên hệ required defaultValue từ detail; FormData gửi PUT If-Match detail.version; uncontrolled fields có thể reset theo detail key.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu thay đổi" ở Hồ sơ nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Hồ sơ nháp: sử dụng "Lưu thay đổi" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT business If-Match Giữ phần tốt: Concurrency version
- **Điểm cần quan sát hoặc cải thiện:** 412/validation chỉcode, chưa chỉ field
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P35](../reviews/ShuttleBook-ui-button-audit-alobo.md#p35) · [apps/partner-web/src/PartnerOnboarding.tsx:209](../../apps/partner-web/src/PartnerOnboarding.tsx#L209)

#### Cơ sở nháp

<a id="p36"></a>

**P36 — Lưu cơ sở**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên cơ sở, Liên hệ cơ sở, Múi giờ IANA required; timezone mặc định Asia/Ho_Chi_Minh. Địa chỉ/latitude/longitude hidden từ confirm MapTiler, không manual coordinate inputs.
- **Agent có thể tự động hóa:** Backend trim timezone giữ behavior; IANA sai có hướng dẫn rõ, unconfirmed MapTiler không tạo venue, scope business đúng. Test provider thật là gate riêng.
- **Bạn thao tác/đánh giá:** Cơ sở nháp: sử dụng "Lưu cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST venues confirmedlocation Giữ phần tốt: Không nhập lat/lon; requiredconfirm
- **Điểm cần quan sát hoặc cải thiện:** Múi giờ IANA vẫn là text kỹ thuật, thiếu selector/hint cho owner. Backend đã Trim timezone; không coi trailing-space là lỗi validation hiện tại.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P36](../reviews/ShuttleBook-ui-button-audit-alobo.md#p36) · [apps/partner-web/src/PartnerOnboarding.tsx:227](../../apps/partner-web/src/PartnerOnboarding.tsx#L227)

<a id="p37"></a>

**P37 — Sửa cơ sở**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Tên, Liên hệ cơ sở, Múi giờ required defaultValue; MapTiler existing address/coords; FormData hiddenlocation; If-Match venue.version.
- **Agent có thể tự động hóa:** Browser fixture: "Sửa cơ sở" ở Cơ sở nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Cơ sở nháp: sử dụng "Sửa cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT venue If-Match Giữ phần tốt: Scope/card từng venue
- **Điểm cần quan sát hoặc cải thiện:** Không dirty/diff; field errors chung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P37](../reviews/ShuttleBook-ui-button-audit-alobo.md#p37) · [apps/partner-web/src/PartnerOnboarding.tsx:248](../../apps/partner-web/src/PartnerOnboarding.tsx#L248)

#### Sân nháp

<a id="p38"></a>

**P38 — Sửa sân**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Field: Tên sân required defaultValue; PUT name; courtstatus là text readonly, không có selector trạng thái thật.
- **Agent có thể tự động hóa:** Browser fixture: "Sửa sân" ở Sân nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Sân nháp: sử dụng "Sửa sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT court name Giữ phần tốt: Rename record đúng scope
- **Điểm cần quan sát hoặc cải thiện:** Không If-Match court UI; cần contract concurrency
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P38](../reviews/ShuttleBook-ui-button-audit-alobo.md#p38) · [apps/partner-web/src/PartnerOnboarding.tsx:258](../../apps/partner-web/src/PartnerOnboarding.tsx#L258)

<a id="p39"></a>

**P39 — Cơ sở (Thêm sân)**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở (Thêm sân)" ở Sân nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Sân nháp: sử dụng "Cơ sở (Thêm sân)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set courtVenue Giữ phần tốt: Required emptyoption
- **Điểm cần quan sát hoặc cải thiện:** Single venue vẫn chọn; chưa searchable
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P39](../reviews/ShuttleBook-ui-button-audit-alobo.md#p39) · [apps/partner-web/src/PartnerOnboarding.tsx:268](../../apps/partner-web/src/PartnerOnboarding.tsx#L268)

<a id="p40"></a>

**P40 — Lưu sân**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Field: Tên sân required localcourtName; Cơ sở select riêngP39; clear tên khi success. Không status selector.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu sân" ở Sân nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Sân nháp: sử dụng "Lưu sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST venue/courts Giữ phần tốt: Clear tên success; scopedvenue
- **Điểm cần quan sát hoặc cải thiện:** Chưa bulk3–7court; active không add tương đương
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P40](../reviews/ShuttleBook-ui-button-audit-alobo.md#p40) · [apps/partner-web/src/PartnerOnboarding.tsx:271](../../apps/partner-web/src/PartnerOnboarding.tsx#L271)

#### Lịch nháp

<a id="p41"></a>

**P41 — Sân**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Sân" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set scheduleCourt Giữ phần tốt: Option venue/court rõ
- **Điểm cần quan sát hoặc cải thiện:** Không load existingconfig vào scheduleRows; mẫu global dễ ghi đè nhầm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P41](../reviews/ShuttleBook-ui-button-audit-alobo.md#p41) · [apps/partner-web/src/PartnerOnboarding.tsx:285](../../apps/partner-web/src/PartnerOnboarding.tsx#L285)

<a id="p42"></a>

**P42 — Ngày**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Ngày" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Ngày" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set weekdayrow Giữ phần tốt: 7 label tiếng Việt
- **Điểm cần quan sát hoặc cải thiện:** Không multiday/copy tuần
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P42](../reviews/ShuttleBook-ui-button-audit-alobo.md#p42) · [apps/partner-web/src/PartnerOnboarding.tsx:288](../../apps/partner-web/src/PartnerOnboarding.tsx#L288)

<a id="p43"></a>

**P43 — Từ / Đến / Giá/30 phút**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Từ / Đến / Giá/30 phút" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Từ / Đến / Giá/30 phút" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set window+amount Giữ phần tốt: Step30min/price>0
- **Điểm cần quan sát hoặc cải thiện:** Không quy đổi giá/giờ ởdraft; gap/overlap chờ API
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P43](../reviews/ShuttleBook-ui-button-audit-alobo.md#p43) · [apps/partner-web/src/PartnerOnboarding.tsx:291](../../apps/partner-web/src/PartnerOnboarding.tsx#L291)

<a id="p44"></a>

**P44 — Xóa khung**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Xóa khung" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Xóa khung" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Delete localrow Giữ phần tốt: Không writeDB tới save
- **Điểm cần quan sát hoặc cải thiện:** Không undo, xóa hết vẫn chưa readiness
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P44](../reviews/ShuttleBook-ui-button-audit-alobo.md#p44) · [apps/partner-web/src/PartnerOnboarding.tsx:297](../../apps/partner-web/src/PartnerOnboarding.tsx#L297)

<a id="p45"></a>

**P45 — Thêm khung giá**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Thêm khung giá" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Thêm khung giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Append Tue07–22 price100000 Giữ phần tốt: Nhiều khung giờ/ngày
- **Điểm cần quan sát hoặc cải thiện:** Default dễ overlap, thiếu copy/template
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P45](../reviews/ShuttleBook-ui-button-audit-alobo.md#p45) · [apps/partner-web/src/PartnerOnboarding.tsx:299](../../apps/partner-web/src/PartnerOnboarding.tsx#L299)

<a id="p46"></a>

**P46 — Lưu giờ/giá**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu giờ/giá" ở Lịch nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch nháp: sử dụng "Lưu giờ/giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT hours first-last +prices Giữ phần tốt: Cùng court giờ/giá
- **Điểm cần quan sát hoặc cải thiện:** Không biểu đạt nghỉ giữa ngày; missingcoverage error chung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P46](../reviews/ShuttleBook-ui-button-audit-alobo.md#p46) · [apps/partner-web/src/PartnerOnboarding.tsx:301](../../apps/partner-web/src/PartnerOnboarding.tsx#L301)

#### Ảnh nháp

<a id="p47"></a>

**P47 — Cơ sở**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở" ở Ảnh nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Ảnh nháp: sử dụng "Cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set imageVenue Giữ phần tốt: Ảnh scoped venue
- **Điểm cần quan sát hoặc cải thiện:** Single venue vẫn chọn
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P47](../reviews/ShuttleBook-ui-button-audit-alobo.md#p47) · [apps/partner-web/src/PartnerOnboarding.tsx:311](../../apps/partner-web/src/PartnerOnboarding.tsx#L311)

<a id="p48"></a>

**P48 — Ảnh cơ sở**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Ảnh cơ sở" ở Ảnh nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Ảnh nháp: sử dụng "Ảnh cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set imageFile Giữ phần tốt: Accept PNG/JPEG/WebP,max5MB
- **Điểm cần quan sát hoặc cải thiện:** Không preview/crop/remove; nativefilename clear cần kiểm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P48](../reviews/ShuttleBook-ui-button-audit-alobo.md#p48) · [apps/partner-web/src/PartnerOnboarding.tsx:313](../../apps/partner-web/src/PartnerOnboarding.tsx#L313)

<a id="p49"></a>

**P49 — Tải ảnh**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** FailPUT/complete/attach riêng; checksum/type5MB; filename/state sau commit; F5 persisted image. AWSprovider NOTRUN.
- **Bạn thao tác/đánh giá:** Ảnh nháp: sử dụng "Tải ảnh" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** SHA256→presign→PUT→complete→attach Giữ phần tốt: Checksum/persist sau complete
- **Điểm cần quan sát hoặc cải thiện:** Không progress/thumbnail/phase error; S3 thậtNOTRUN
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P49](../reviews/ShuttleBook-ui-button-audit-alobo.md#p49) · [apps/partner-web/src/PartnerOnboarding.tsx:314](../../apps/partner-web/src/PartnerOnboarding.tsx#L314)

#### QR nháp

<a id="p50"></a>

**P50 — Cơ sở**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở" ở QR nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** QR nháp: sử dụng "Cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set paymentVenue Giữ phần tốt: QR scopedvenue
- **Điểm cần quan sát hoặc cải thiện:** Đổi venue không reset bankfields riêng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P50](../reviews/ShuttleBook-ui-button-audit-alobo.md#p50) · [apps/partner-web/src/PartnerOnboarding.tsx:325](../../apps/partner-web/src/PartnerOnboarding.tsx#L325)

<a id="p51"></a>

**P51 — Ảnh QR**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network.
- **Agent có thể tự động hóa:** Browser fixture: "Ảnh QR" ở QR nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** QR nháp: sử dụng "Ảnh QR" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set qrFile Giữ phần tốt: Required validimages max5MB
- **Điểm cần quan sát hoặc cải thiện:** Không preview/scan/account match
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P51](../reviews/ShuttleBook-ui-button-audit-alobo.md#p51) · [apps/partner-web/src/PartnerOnboarding.tsx:330](../../apps/partner-web/src/PartnerOnboarding.tsx#L330)

<a id="p52"></a>

**P52 — Lưu QR và tài khoản**

- **Tiền điều kiện/state:** DRAFT, đúng page; parent fieldset khóa busy/loading. Pending/ACTIVE form chỉnh nháp ẩn. Required/select/time/file theo form; success Đã lưu +reload, lỗi generic code/network. Fields: Mã ngân hàng, Tên tài khoản required text; Số tài khoản required inputMode numeric (không pattern/digits bound tại UI); QR/file và cơ sở select riêngP50/P51; account number clear sau success.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu QR và tài khoản" ở QR nháp, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** QR nháp: sử dụng "Lưu QR và tài khoản" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** UploadQR +PUT payment-account Giữ phần tốt: Mask storedaccount;clear accountnumber
- **Điểm cần quan sát hoặc cải thiện:** Mãngân hàng rawtext; chưa picker/QRdynamic
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P52](../reviews/ShuttleBook-ui-button-audit-alobo.md#p52) · [apps/partner-web/src/PartnerOnboarding.tsx:331](../../apps/partner-web/src/PartnerOnboarding.tsx#L331)

#### Empty cấu hình

<a id="p53"></a>

**P53 — Đến mục Cơ sở**

- **Tiền điều kiện/state:** Có label, các trạng thái hiển thị/disabled được đọc từ source; không quy đổi thành runtime PASS.
- **Agent có thể tự động hóa:** Browser fixture: "Đến mục Cơ sở" ở Empty cấu hình, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Empty cấu hình: sử dụng "Đến mục Cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** #/venues Giữ phần tốt: Prerequisite rõ
- **Điểm cần quan sát hoặc cải thiện:** Active emptyedge không addform
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P53](../reviews/ShuttleBook-ui-button-audit-alobo.md#p53) · [apps/partner-web/src/PartnerOnboarding.tsx:336](../../apps/partner-web/src/PartnerOnboarding.tsx#L336)

#### Revision ACTIVE

<a id="p54"></a>

**P54 — Cơ sở**

- **Tiền điều kiện/state:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ.
- **Agent có thể tự động hóa:** Browser fixture: "Cơ sở" ở Revision ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Revision ACTIVE: sử dụng "Cơ sở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set revisionvenue/currentfields Giữ phần tốt: Prefill currentdata
- **Điểm cần quan sát hoặc cải thiện:** Đổi địa chỉ ởpayments khó tìm từvenues
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P54](../reviews/ShuttleBook-ui-button-audit-alobo.md#p54) · [apps/partner-web/src/PartnerOnboarding.tsx:351](../../apps/partner-web/src/PartnerOnboarding.tsx#L351)

<a id="p55"></a>

**P55 — QR mới**

- **Tiền điều kiện/state:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ.
- **Agent có thể tự động hóa:** Browser fixture: "QR mới" ở Revision ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Revision ACTIVE: sử dụng "QR mới" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set qrFile required Giữ phần tốt: Criticalinfo gửi duyệt
- **Điểm cần quan sát hoặc cải thiện:** Đổi liênhệ/address cũng phải newQR/account; chưa táchrevision
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P55](../reviews/ShuttleBook-ui-button-audit-alobo.md#p55) · [apps/partner-web/src/PartnerOnboarding.tsx:368](../../apps/partner-web/src/PartnerOnboarding.tsx#L368)

<a id="p56"></a>

**P56 — Gửi thay đổi để duyệt**

- **Tiền điều kiện/state:** ACTIVE/pagepayments, visited payments; approval khácPENDING. Parentfieldset khóa busy/loading. Tất cả critical fields/newQR required; pending ẩn mutation, currentpublic giữ bản cũ. Fields: Liên hệ cơ sở, Múi giờ, Mã ngân hàng, Tên tài khoản required; Số tài khoản required inputMode numeric. Revision location từMapTiler; QR mới và venue riêngP54/P55. Không preview so sánh old/new.
- **Agent có thể tự động hóa:** Pending formẩn; current QR/public unchanged, approve new revision; address-only burden manual. MapTilerreal NOTRUN.
- **Bạn thao tác/đánh giá:** Revision ACTIVE: sử dụng "Gửi thay đổi để duyệt" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** UploadQR +POST revisions Giữ phần tốt: PENDING khóa duplicate; giữ bảncũ
- **Điểm cần quan sát hoặc cải thiện:** Không summaryold/new; fields toàn bộ bắt buộc
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P56](../reviews/ShuttleBook-ui-button-audit-alobo.md#p56) · [apps/partner-web/src/PartnerOnboarding.tsx:369](../../apps/partner-web/src/PartnerOnboarding.tsx#L369)

#### Địa chỉ

<a id="p57"></a>

**P57 — Địa chỉ cơ sở trên MapTiler**

- **Tiền điều kiện/state:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Agent có thể tự động hóa:** Missingkey/unconfirmed save, slowqueryabort, keyboardcombobox/unique labels. Realprovider pin/address31ngõ16HoàngCầu NOTRUN bởi reviewer.
- **Bạn thao tác/đánh giá:** Địa chỉ: sử dụng "Địa chỉ cơ sở trên MapTiler" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Debounce400ms≥4chars VN5results Giữ phần tốt: Abort query cũ; confirm trướcsave
- **Điểm cần quan sát hoặc cải thiện:** Thiếu combobox/listbox/Arrow keys/noresults/sửa pin
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P57](../reviews/ShuttleBook-ui-button-audit-alobo.md#p57) · [apps/partner-web/src/MapTilerPlacePicker.tsx:121](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L121)

<a id="p58"></a>

**P58 — {địa chỉ gợi ý}**

- **Tiền điều kiện/state:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Agent có thể tự động hóa:** Browser fixture: "{địa chỉ gợi ý}" ở Địa chỉ, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Địa chỉ: sử dụng "{địa chỉ gợi ý}" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Candidate +center zoom17 marker Giữ phần tốt: Xem trướccommit
- **Điểm cần quan sát hoặc cải thiện:** Arrow keys không combobox; realprovider chưa kiểm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P58](../reviews/ShuttleBook-ui-button-audit-alobo.md#p58) · [apps/partner-web/src/MapTilerPlacePicker.tsx:132](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L132)

<a id="p59"></a>

**P59 — Xác nhận vị trí này**

- **Tiền điều kiện/state:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Agent có thể tự động hóa:** Browser fixture: "Xác nhận vị trí này" ở Địa chỉ, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Địa chỉ: sử dụng "Xác nhận vị trí này" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Commit hiddenlocation/onChange Giữ phần tốt: Explicitconfirm; gõ lại invalidates
- **Điểm cần quan sát hoặc cải thiện:** Không drag/clickpin/reverse correction
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P59](../reviews/ShuttleBook-ui-button-audit-alobo.md#p59) · [apps/partner-web/src/MapTilerPlacePicker.tsx:135](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L135)

<a id="p60"></a>

**P60 — MapTiler SDK controls (chưa quan sát nhãn)**

- **Tiền điều kiện/state:** MapTiler key required, loading debounce/geocode và errors alert; suggestion chỉ có results; candidate explicit confirm; gõ lại xóa location. Parentfieldset có thể disable. SDK buttons UNKNOWN.
- **Agent có thể tự động hóa:** NOT OBSERVED: cấu hình MapTiler key test, quan sát DOM/control thực tế, ghi nhãn SDK đúng rồi kiểm keyboard, zoom, focus, attribution và lỗi CDN. Không bấm control do suy đoán từ source.
- **Bạn thao tác/đánh giá:** Provider thật: quan sát controls xuất hiện trên bản đồ, thử pan/zoom/focus trên phone và desktop, kiểm attribution. Exact controls hiện NOT OBSERVED.
- **Expected đối chiếu:** SDK default map Giữ phần tốt: Mapcanvas định vị
- **Điểm cần quan sát hoặc cải thiện:** Exactbuttons UNKNOWN, không invent zoom+/− labels
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P60](../reviews/ShuttleBook-ui-button-audit-alobo.md#p60) · [apps/partner-web/src/MapTilerPlacePicker.tsx:68](../../apps/partner-web/src/MapTilerPlacePicker.tsx#L68)

#### Vận hành ACTIVE

<a id="p61"></a>

**P61 — Sân vận hành**

- **Tiền điều kiện/state:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Agent có thể tự động hóa:** Browser fixture: "Sân vận hành" ở Vận hành ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Vận hành ACTIVE: sử dụng "Sân vận hành" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** GET operations+maintenance court Giữ phần tốt: Bỏ kết quả tải về muộn của sân cũ, khóa chọn khi bận
- **Điểm cần quan sát hoặc cải thiện:** Đổi sân mất form chưa lưu; thiếu lịch ngày mọi sân
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P61](../reviews/ShuttleBook-ui-button-audit-alobo.md#p61) · [apps/partner-web/src/PartnerOperations.tsx:79](../../apps/partner-web/src/PartnerOperations.tsx#L79)

<a id="p62"></a>

**P62 — Thử tải lại cấu hình**

- **Tiền điều kiện/state:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Agent có thể tự động hóa:** Browser fixture: "Thử tải lại cấu hình" ở Vận hành ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Vận hành ACTIVE: sử dụng "Thử tải lại cấu hình" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** run loadcourt Giữ phần tốt: Có nút thử lại khi tải lỗi
- **Điểm cần quan sát hoặc cải thiện:** GET nhưng thông báo Đã lưu; nút chưa bị khóa riêng khi bận
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P62](../reviews/ShuttleBook-ui-button-audit-alobo.md#p62) · [apps/partner-web/src/PartnerOperations.tsx:84](../../apps/partner-web/src/PartnerOperations.tsx#L84)

<a id="p63"></a>

**P63 — Quy định đặt / Lịch tuần / Giá theo ngày / Xem thử giá / Bảo trì**

- **Tiền điều kiện/state:** ACTIVE/pageschedule, loadedoperations; select khóa busy. Loading clearconfig, error có retry; khôngcourt có empty; formsfieldset khóa busy. Lỗi/Đã lưu global.
- **Agent có thể tự động hóa:** Browser fixture: "Quy định đặt / Lịch tuần / Giá theo ngày / Xem thử giá / Bảo trì" ở Vận hành ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Vận hành ACTIVE: sử dụng "Quy định đặt / Lịch tuần / Giá theo ngày / Xem thử giá / Bảo trì" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Scroll/focus5sections Giữ phần tốt: Chuyển tới section và focus bàn phím
- **Điểm cần quan sát hoặc cải thiện:** Form dài trên mobile cần thử thực tế; chưa có tab section hiện hành
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P63](../reviews/ShuttleBook-ui-button-audit-alobo.md#p63) · [apps/partner-web/src/PartnerOperations.tsx:90](../../apps/partner-web/src/PartnerOperations.tsx#L90)

#### Quy định ACTIVE

<a id="p64"></a>

**P64 — Block hiển thị**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Block hiển thị" ở Quy định ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quy định ACTIVE: sử dụng "Block hiển thị" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** 30/60/90; minimum=maxblock Giữ phần tốt: Block chia theo ca 30 phút
- **Điểm cần quan sát hoặc cải thiện:** Đổi block60→90 khi minimum120 chưa tự căn bội90
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P64](../reviews/ShuttleBook-ui-button-audit-alobo.md#p64) · [apps/partner-web/src/PartnerOperations.tsx:102](../../apps/partner-web/src/PartnerOperations.tsx#L102)

<a id="p65"></a>

**P65 — Thời lượng đặt tối thiểu / Giữ chỗ trước khi báo chuyển khoản**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Thời lượng đặt tối thiểu / Giữ chỗ trước khi báo chuyển khoản" ở Quy định ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quy định ACTIVE: sử dụng "Thời lượng đặt tối thiểu / Giữ chỗ trước khi báo chuyển khoản" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Minimumblock..480/hold5..60 Giữ phần tốt: Giới hạn tối thiểu và thời gian giữ rõ
- **Điểm cần quan sát hoặc cải thiện:** Chưa giải thích giữ báo giá120s khác giữ chờ thanh toán
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P65](../reviews/ShuttleBook-ui-button-audit-alobo.md#p65) · [apps/partner-web/src/PartnerOperations.tsx:107](../../apps/partner-web/src/PartnerOperations.tsx#L107)

<a id="p66"></a>

**P66 — Lưu quy định**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Block60/min120→90 cần bội90;412 giữ bản cũ; booking/paymentdeadline snapshot cũ không đổi.
- **Bạn thao tác/đánh giá:** Quy định ACTIVE: sử dụng "Lưu quy định" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT booking-policy If-Match +load Giữ phần tốt: If-Match chống ghi đè cấu hình
- **Điểm cần quan sát hoặc cải thiện:** Thiếu tóm tắt tác động booking mới/cũ và lỗi theo trường
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P66](../reviews/ShuttleBook-ui-button-audit-alobo.md#p66) · [apps/partner-web/src/PartnerOperations.tsx:112](../../apps/partner-web/src/PartnerOperations.tsx#L112)

#### Lịch tuần ACTIVE

<a id="p67"></a>

**P67 — Ngày (giờ mở cửa)**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Ngày (giờ mở cửa)" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Ngày (giờ mở cửa)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set hoursweekday Giữ phần tốt: Đủ bảy ngày tiếng Việt
- **Điểm cần quan sát hoặc cải thiện:** Thiếu sao chép và áp dụng nhiều ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P67](../reviews/ShuttleBook-ui-button-audit-alobo.md#p67) · [apps/partner-web/src/PartnerOperations.tsx:120](../../apps/partner-web/src/PartnerOperations.tsx#L120)

<a id="p68"></a>

**P68 — Mở / Đóng**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Mở / Đóng" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Mở / Đóng" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set opens/closes30min Giữ phần tốt: Giờ theo múi giờ cơ sở
- **Điểm cần quan sát hoặc cải thiện:** Một khoảng mỗi ngày chưa biểu đạt nghỉ giữa ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P68](../reviews/ShuttleBook-ui-button-audit-alobo.md#p68) · [apps/partner-web/src/PartnerOperations.tsx:123](../../apps/partner-web/src/PartnerOperations.tsx#L123)

<a id="p69"></a>

**P69 — Xóa ngày**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Xóa ngày" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Xóa ngày" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Remove hoursrow local Giữ phần tốt: Xóa ở form trước khi lưu
- **Điểm cần quan sát hoặc cải thiện:** Không hoàn tác, giá cùng ngày có thể còn lại
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P69](../reviews/ShuttleBook-ui-button-audit-alobo.md#p69) · [apps/partner-web/src/PartnerOperations.tsx:127](../../apps/partner-web/src/PartnerOperations.tsx#L127)

<a id="p70"></a>

**P70 — Thêm ngày mở**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Thêm ngày mở" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Thêm ngày mở" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Append Mon08–10 Giữ phần tốt: Thêm ngày hoạt động được
- **Điểm cần quan sát hoặc cải thiện:** Mặc định thứ Hai dễ trùng, thiếu sao chép ngày
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P70](../reviews/ShuttleBook-ui-button-audit-alobo.md#p70) · [apps/partner-web/src/PartnerOperations.tsx:129](../../apps/partner-web/src/PartnerOperations.tsx#L129)

<a id="p71"></a>

**P71 — Ngày (giá cơ bản)**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Ngày (giá cơ bản)" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Ngày (giá cơ bản)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set pricesweekday Giữ phần tốt: Bảng giá riêng theo thứ và sân
- **Điểm cần quan sát hoặc cải thiện:** Không áp dụng nhóm T2–T6/cuối tuần
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P71](../reviews/ShuttleBook-ui-button-audit-alobo.md#p71) · [apps/partner-web/src/PartnerOperations.tsx:133](../../apps/partner-web/src/PartnerOperations.tsx#L133)

<a id="p72"></a>

**P72 — Từ / Đến / Giá/30 phút**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Từ / Đến / Giá/30 phút" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Từ / Đến / Giá/30 phút" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set priceswindow+amount Giữ phần tốt: Hiển thị giá quy đổi theo giờ
- **Điểm cần quan sát hoặc cải thiện:** JS Number cực trị>2^53 cần kiểm roundtrip; lỗi phủ giá chung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P72](../reviews/ShuttleBook-ui-button-audit-alobo.md#p72) · [apps/partner-web/src/PartnerOperations.tsx:136](../../apps/partner-web/src/PartnerOperations.tsx#L136)

<a id="p73"></a>

**P73 — Xóa giá**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Xóa giá" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Xóa giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Remove pricelocal Giữ phần tốt: Xóa ở form trước khi lưu
- **Điểm cần quan sát hoặc cải thiện:** Thiếu hoàn tác và xem phạm vi giá còn lại
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P73](../reviews/ShuttleBook-ui-button-audit-alobo.md#p73) · [apps/partner-web/src/PartnerOperations.tsx:143](../../apps/partner-web/src/PartnerOperations.tsx#L143)

<a id="p74"></a>

**P74 — Thêm khung giá**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Thêm khung giá" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Thêm khung giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Append Mon08–10 price100000 Giữ phần tốt: Thêm nhiều khung giá được
- **Điểm cần quan sát hoặc cải thiện:** Mặc định dễ trùng giờ, chưa nhân bản khung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P74](../reviews/ShuttleBook-ui-button-audit-alobo.md#p74) · [apps/partner-web/src/PartnerOperations.tsx:145](../../apps/partner-web/src/PartnerOperations.tsx#L145)

<a id="p75"></a>

**P75 — Lưu lịch tuần**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu lịch tuần" ở Lịch tuần ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Lịch tuần ACTIVE: sử dụng "Lưu lịch tuần" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT hours/prices If-Match Giữ phần tốt: Lưu giờ và giá cùng phiên bản
- **Điểm cần quan sát hoặc cải thiện:** Thiếu so sánh trước/sau, lỗi không gắn field
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P75](../reviews/ShuttleBook-ui-button-audit-alobo.md#p75) · [apps/partner-web/src/PartnerOperations.tsx:147](../../apps/partner-web/src/PartnerOperations.tsx#L147)

#### Giá ngày ACTIVE

<a id="p76"></a>

**P76 — Thứ**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Thứ" ở Giá ngày ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Giá ngày ACTIVE: sử dụng "Thứ" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set ruleweekday Giữ phần tốt: Quy tắc theo ngày và thứ
- **Điểm cần quan sát hoặc cải thiện:** Ngày lễ một ngày vẫn phải chọn đúng thứ thủ công
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P76](../reviews/ShuttleBook-ui-button-audit-alobo.md#p76) · [apps/partner-web/src/PartnerOperations.tsx:160](../../apps/partner-web/src/PartnerOperations.tsx#L160)

<a id="p77"></a>

**P77 — Hiệu lực từ / Đến ngày / Từ / Đến / Giá/30 phút / Ưu tiên**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Hiệu lực từ / Đến ngày / Từ / Đến / Giá/30 phút / Ưu tiên" ở Giá ngày ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Giá ngày ACTIVE: sử dụng "Hiệu lực từ / Đến ngày / Từ / Đến / Giá/30 phút / Ưu tiên" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Requiredrulefields priority1–1000 Giữ phần tốt: Hiệu lực và giá quy đổi rõ
- **Điểm cần quan sát hoặc cải thiện:** Độ ưu tiên khó hiểu; chưa xem quy tắc thắng từng ca
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P77](../reviews/ShuttleBook-ui-button-audit-alobo.md#p77) · [apps/partner-web/src/PartnerOperations.tsx:156](../../apps/partner-web/src/PartnerOperations.tsx#L156)

<a id="p78"></a>

**P78 — Xóa quy tắc**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Xóa quy tắc" ở Giá ngày ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Giá ngày ACTIVE: sử dụng "Xóa quy tắc" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Remove rulelocal Giữ phần tốt: Xóa trước khi lưu
- **Điểm cần quan sát hoặc cải thiện:** Chưa hoàn tác hoặc tóm tắt tác động
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P78](../reviews/ShuttleBook-ui-button-audit-alobo.md#p78) · [apps/partner-web/src/PartnerOperations.tsx:172](../../apps/partner-web/src/PartnerOperations.tsx#L172)

<a id="p79"></a>

**P79 — Thêm quy tắc giá**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Thêm quy tắc giá" ở Giá ngày ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Giá ngày ACTIVE: sử dụng "Thêm quy tắc giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Append today weekdayMon08–10 Giữ phần tốt: Hỗ trợ giá ngày đặc biệt
- **Điểm cần quan sát hoặc cải thiện:** Mặc định thứ Hai có thể không khớp ngày hôm nay
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P79](../reviews/ShuttleBook-ui-button-audit-alobo.md#p79) · [apps/partner-web/src/PartnerOperations.tsx:174](../../apps/partner-web/src/PartnerOperations.tsx#L174)

<a id="p80"></a>

**P80 — Lưu giá theo ngày**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Lưu giá theo ngày" ở Giá ngày ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Giá ngày ACTIVE: sử dụng "Lưu giá theo ngày" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** PUT pricing-rules If-Match+load Giữ phần tốt: Backend kiểm độ ưu tiên/phiên bản
- **Điểm cần quan sát hoặc cải thiện:** Thiếu bảng giá hiệu lực và lỗi theo trường
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P80](../reviews/ShuttleBook-ui-button-audit-alobo.md#p80) · [apps/partner-web/src/PartnerOperations.tsx:178](../../apps/partner-web/src/PartnerOperations.tsx#L178)

#### Xem thử giá ACTIVE

<a id="p81"></a>

**P81 — Ngày / Từ / Đến**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Ngày / Từ / Đến" ở Xem thử giá ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Xem thử giá ACTIVE: sử dụng "Ngày / Từ / Đến" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Set preview/maintenance sharedstate Giữ phần tốt: Bắt buộc ngày và giờ theo ca30 phút
- **Điểm cần quan sát hoặc cải thiện:** Xem giá và bảo trì chung state, đổi một bên đổi bên kia
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P81](../reviews/ShuttleBook-ui-button-audit-alobo.md#p81) · [apps/partner-web/src/PartnerOperations.tsx:186](../../apps/partner-web/src/PartnerOperations.tsx#L186)

<a id="p82"></a>

**P82 — Xem giá**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Server weekday/peak/holiday total/ca priority; GET không nói Đã lưu; snapshotbooking cũ vẫn đúng.
- **Bạn thao tác/đánh giá:** Xem thử giá ACTIVE: sử dụng "Xem giá" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** GET price-preview total/count Giữ phần tốt: Giá tính tại server
- **Điểm cần quan sát hoặc cải thiện:** GET hiện Đã lưu; không hiển thị giá/ưu tiên từng ca dù API có
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P82](../reviews/ShuttleBook-ui-button-audit-alobo.md#p82) · [apps/partner-web/src/PartnerOperations.tsx:189](../../apps/partner-web/src/PartnerOperations.tsx#L189)

#### Bảo trì ACTIVE

<a id="p83"></a>

**P83 — Ngày / Từ / Đến / Lý do**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Ngày / Từ / Đến / Lý do" ở Bảo trì ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Bảo trì ACTIVE: sử dụng "Ngày / Từ / Đến / Lý do" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Maintenance date/time/reason5–500 Giữ phần tốt: Múi giờ rõ, lý do5–500 ký tự
- **Điểm cần quan sát hoặc cải thiện:** Chung state xem giá; không có preview lịch trước khóa
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P83](../reviews/ShuttleBook-ui-button-audit-alobo.md#p83) · [apps/partner-web/src/PartnerOperations.tsx:199](../../apps/partner-web/src/PartnerOperations.tsx#L199)

<a id="p84"></a>

**P84 — Khóa ca bảo trì**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Khóa ca bảo trì" ở Bảo trì ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Bảo trì ACTIVE: sử dụng "Khóa ca bảo trì" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST maintenance+load Giữ phần tốt: DB chống đè booking hoặc quote còn hạn
- **Điểm cần quan sát hoặc cải thiện:** Thiếu tóm tắt sân/ngày/giờ xác nhận và lịch ngày owner
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P84](../reviews/ShuttleBook-ui-button-audit-alobo.md#p84) · [apps/partner-web/src/PartnerOperations.tsx:203](../../apps/partner-web/src/PartnerOperations.tsx#L203)

<a id="p85"></a>

**P85 — Hủy bảo trì**

- **Tiền điều kiện/state:** Loaded operations trên ACTIVE/pageschedule; fieldset disabled busy. Date/time required step30 phút, number bounds theo form; mutation If-Match khi policy/schedule/pricing. Preview/maintenance cùng state ngày/giờ; globalmessage.
- **Agent có thể tự động hóa:** Browser fixture: "Hủy bảo trì" ở Bảo trì ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Bảo trì ACTIVE: sử dụng "Hủy bảo trì" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST maintenance/id/cancel+load Giữ phần tốt: Hủy bảo trì riêng, trả trống khi hợp lệ
- **Điểm cần quan sát hoặc cải thiện:** Không xác nhận trước thao tác giải phóng, chưa hoàn tác
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P85](../reviews/ShuttleBook-ui-button-audit-alobo.md#p85) · [apps/partner-web/src/PartnerOperations.tsx:210](../../apps/partner-web/src/PartnerOperations.tsx#L210)

#### Đơn ACTIVE

<a id="p86"></a>

**P86 — Cơ sở xem đơn**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** f06-partner.spec.ts:139 delayedoldlist abort. Thêm confirmdelay→switchvenue→reopen verify one serverdecision và feedback không sai; chưa gate runtime mới.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Cơ sở xem đơn" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Close detail/resetfilters/scope Giữ phần tốt: Generation/abort chặn dữ liệu scope cũ
- **Điểm cần quan sát hoặc cải thiện:** Không khóa selector lúc quyết định đang gửi; server có thể commit sau khi panel đóng. Đây là source-risk về thông báo kết quả, chưa chứng minh lộ dữ liệu
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P86](../reviews/ShuttleBook-ui-button-audit-alobo.md#p86) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:130](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L130)

<a id="p87"></a>

**P87 — Chờ xác nhận**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Chờ xác nhận" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Chờ xác nhận" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Apply awaiting filter Giữ phần tốt: Một click tới hàng đợi cần xử lý
- **Điểm cần quan sát hoặc cải thiện:** Phạm vi count/ngày chưa rõ; thiếu nút Tất cả
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P87](../reviews/ShuttleBook-ui-button-audit-alobo.md#p87) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:137](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L137)

<a id="p88"></a>

**P88 — Cần bổ sung**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Cần bổ sung" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Cần bổ sung" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Apply reviewfilter Giữ phần tốt: Hàng đợi cần bổ sung riêng
- **Điểm cần quan sát hoặc cải thiện:** Giữ bộ lọc ngày; thiếu nút Tất cả/reset
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P88](../reviews/ShuttleBook-ui-button-audit-alobo.md#p88) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:139](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L139)

<a id="p89"></a>

**P89 — Trạng thái đơn**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Trạng thái đơn" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Trạng thái đơn" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Draft6states+All; applyLọc Giữ phần tốt: Sáu trạng thái đúng nghiệp vụ
- **Điểm cần quan sát hoặc cải thiện:** Chưa lọc quá hạn/loại đơn/tìm mã đơn
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P89](../reviews/ShuttleBook-ui-button-audit-alobo.md#p89) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:143](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L143)

<a id="p90"></a>

**P90 — Từ ngày / Đến ngày**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Từ ngày / Đến ngày" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Từ ngày / Đến ngày" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Draftrange ≤366days Giữ phần tốt: Giải thích múi giờ và lịch cố định khớp bất kỳ buổi
- **Điểm cần quan sát hoặc cải thiện:** Thiếu Hôm nay/Tuần này/xóa ngày nhanh
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P90](../reviews/ShuttleBook-ui-button-audit-alobo.md#p90) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:145](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L145)

<a id="p91"></a>

**P91 — Lọc đơn**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Lọc đơn" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Lọc đơn" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Applydraftfilters+GET Giữ phần tốt: Kiểm ngày ngược và tối đa366 ngày trước API
- **Điểm cần quan sát hoặc cải thiện:** Thiếu xóa bộ lọc; không khóa khi quyết định đang gửi
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P91](../reviews/ShuttleBook-ui-button-audit-alobo.md#p91) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:147](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L147)

<a id="p92"></a>

**P92 — Tải lại đơn**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Delayconfirm rồi reload→reopen, server quyết định đúng một lần; không báo lỗi giả hoặc gửi lại mới. Kiểm retention >20records saupoll.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Tải lại đơn" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Reload list/detail Giữ phần tốt: Khóa khi đang tải hoặc tải thêm
- **Điểm cần quan sát hoặc cải thiện:** Chưa khóa lúc quyết định đang gửi; reload có thể đóng form và xóa intent. Cần test kết quả server; đồng thời reset các trang đã tải
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P92](../reviews/ShuttleBook-ui-button-audit-alobo.md#p92) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:147](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L147)

<a id="p93"></a>

**P93 — Xem đơn {bookingNo}**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** Browser fixture: "Xem đơn {bookingNo}" ở Đơn ACTIVE, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Xem đơn {bookingNo}" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Deep linkbookingId+GET detail Giữ phần tốt: Mã đơn rõ, aria-expanded và tiền exact
- **Điểm cần quan sát hoặc cải thiện:** Không scroll/focus panel chi tiết dưới danh sách dài
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P93](../reviews/ShuttleBook-ui-button-audit-alobo.md#p93) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:157](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L157)

<a id="p94"></a>

**P94 — Xem thêm đơn**

- **Tiền điều kiện/state:** Business ACTIVE mới render; list theo venue, loading/error/empty. Draft filters áp dụng Lọc; listpoll5s skip actionbusy. More disabledmoreLoading; generation/abort chống late scope;403/404 xóa privatecache.
- **Agent có thể tự động hóa:** >20records, loadmore, đợi6s, giữ records/cursor/scroll.403/404 vẫn clearcache. Chưa thấy testcase retention này trong các file đã đọc.
- **Bạn thao tác/đánh giá:** Đơn ACTIVE: sử dụng "Xem thêm đơn" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Cursor appenddedup Giữ phần tốt: Có cursor, dedup và guard đổi scope
- **Điểm cần quan sát hoặc cải thiện:** Polling5s thay bằng trang đầu, bỏ các trang đã tải thêm. Source xác nhận; reviewer chưa tái hiện runtime mới
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P94](../reviews/ShuttleBook-ui-button-audit-alobo.md#p94) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:161](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L161)

#### Chi tiết đơn

<a id="p95"></a>

**P95 — Đóng chi tiết**

- **Tiền điều kiện/state:** bookingId hash, detailLoading/error/retry/poll5s; skip actionbusy và version cũ; authorizedbusiness switching.403/404 xóa cache. Close/reload chưa khóa actionBusy.
- **Agent có thể tự động hóa:** Browser fixture: "Đóng chi tiết" ở Chi tiết đơn, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Chi tiết đơn: sử dụng "Đóng chi tiết" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** ClearbookingId Giữ phần tốt: Đóng panel không hủy booking
- **Điểm cần quan sát hoặc cải thiện:** Đóng khi gửi chỉ abort phía UI; server có thể commit, cần xem lại kết quả rõ
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P95](../reviews/ShuttleBook-ui-button-audit-alobo.md#p95) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:164](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L164)

<a id="p96"></a>

**P96 — Tải lại chi tiết (fetch lỗi)**

- **Tiền điều kiện/state:** bookingId hash, detailLoading/error/retry/poll5s; skip actionbusy và version cũ; authorizedbusiness switching.403/404 xóa cache. Close/reload chưa khóa actionBusy.
- **Agent có thể tự động hóa:** Browser fixture: "Tải lại chi tiết (fetch lỗi)" ở Chi tiết đơn, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Chi tiết đơn: sử dụng "Tải lại chi tiết (fetch lỗi)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** setReload Giữ phần tốt: 403/404 xóa dữ liệu chi tiết private, có retry
- **Điểm cần quan sát hoặc cải thiện:** Nút chưa khóa loading; tải lại cả danh sách mất trang đã thêm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P96](../reviews/ShuttleBook-ui-button-audit-alobo.md#p96) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:166](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L166)

#### Biên lai

<a id="p97"></a>

**P97 — Xem biên lai**

- **Tiền điều kiện/state:** Có proofURL hợp lệ private endpoint; Xem khóa loading/nhãn Đang tải biên lai…; erroralert; blob show/hide+unmountcleanup. Khôngpubliclink.
- **Agent có thể tự động hóa:** f06-partner.spec.ts:92 Bearerprivate +:242 revoke clears privateproof. Phone receipt legibility/zoom cầntesttay.
- **Bạn thao tác/đánh giá:** Biên lai: sử dụng "Xem biên lai" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** BearerGET /uploads/id/view→blob Giữ phần tốt: Validate private path, gửi Bearer và tải theo yêu cầu
- **Điểm cần quan sát hoặc cải thiện:** Thiếu zoom/tải ảnh; ảnh biên lai điện thoại cần thử độ dễ đọc
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P97](../reviews/ShuttleBook-ui-button-audit-alobo.md#p97) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:204](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L204)

<a id="p98"></a>

**P98 — Ẩn biên lai**

- **Tiền điều kiện/state:** Có proofURL hợp lệ private endpoint; Xem khóa loading/nhãn Đang tải biên lai…; erroralert; blob show/hide+unmountcleanup. Khôngpubliclink.
- **Agent có thể tự động hóa:** Browser fixture: "Ẩn biên lai" ở Biên lai, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Biên lai: sử dụng "Ẩn biên lai" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** RevokeobjectURL/hide Giữ phần tốt: Ẩn/đóng component giải phóng blobURL
- **Điểm cần quan sát hoặc cải thiện:** Chưa kiểm zoom bàn phím/screen reader; control tương đương ALOBO UNKNOWN
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P98](../reviews/ShuttleBook-ui-button-audit-alobo.md#p98) · [apps/partner-web/src/features/bookings/PartnerBookings.tsx:203](../../apps/partner-web/src/features/bookings/PartnerBookings.tsx#L203)

#### Quyết định

<a id="p99"></a>

**P99 — Quyết định**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Quyết định" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Quyết định" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** CONFIRMED/NEEDS_REVIEW/FINAL_REJECTION Giữ phần tốt: Chỉ có form ở chờ xác nhận/cần bổ sung
- **Điểm cần quan sát hoặc cải thiện:** Mặc định xác nhận và prefill tiền có thể khuyến khích click nhanh, cần đào tạo đối chiếu ngân hàng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P99](../reviews/ShuttleBook-ui-button-audit-alobo.md#p99) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:91](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L91)

<a id="p100"></a>

**P100 — Số tiền thực nhận (đ) / Ghi chú đối chiếu (không bắt buộc)**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Số tiền thực nhận (đ) / Ghi chú đối chiếu (không bắt buộc)" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Số tiền thực nhận (đ) / Ghi chú đối chiếu (không bắt buộc)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** ExactBigIntdigits18/note1000 Giữ phần tốt: Sai tiền không POST, BigInt bảo toàn số lớn
- **Điểm cần quan sát hoặc cải thiện:** Prefill tổng tiền không chứng minh thực nhận; phải kiểm ngân hàng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P100](../reviews/ShuttleBook-ui-button-audit-alobo.md#p100) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:95](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L95)

<a id="p101"></a>

**P101 — Lý do đối chiếu**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Lý do đối chiếu" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Lý do đối chiếu" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** 4reasoncodes Giữ phần tốt: Lý do cấu trúc, không ép mã giao dịch
- **Điểm cần quan sát hoặc cải thiện:** Không phát hiện gap source riêng; exact control ALOBO UNKNOWN
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P101](../reviews/ShuttleBook-ui-button-audit-alobo.md#p101) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:99](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L99)

<a id="p102"></a>

**P102 — Nội dung gửi khách**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Nội dung gửi khách" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Nội dung gửi khách" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Requiredreason≤1000 Giữ phần tốt: Bắt buộc owner giải thích; lưu audit và gửi thông báo
- **Điểm cần quan sát hoặc cải thiện:** Thiếu mẫu lý do; chất lượng lời nhắn cần người dùng đọc
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P102](../reviews/ShuttleBook-ui-button-audit-alobo.md#p102) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:101](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L101)

<a id="p103"></a>

**P103 — Tôi xác nhận không chấp nhận giao dịch và giải phóng {khung giờ / toàn bộ N buổi}**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Tôi xác nhận không chấp nhận giao dịch và giải phóng {khung giờ / toàn bộ N buổi}" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Tôi xác nhận không chấp nhận giao dịch và giải phóng {khung giờ / toàn bộ N buổi}" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Explicitfinalrejectionack Giữ phần tốt: Nói rõ giải phóng cả kỳ và tính cuối cùng
- **Điểm cần quan sát hoặc cải thiện:** Không undo đúng terminal policy; không coi thiếu undo booking là bug
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P103](../reviews/ShuttleBook-ui-button-audit-alobo.md#p103) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:104](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L104)

<a id="p104"></a>

**P104 — Xác nhận đã nhận đủ tiền**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** f06-partner.spec.ts:159 retry sameintent;:205 stale;:223 exactmoney;:242 revokedscope. f07-partner-series.spec.ts:59 oneanchor100%. PostGIS thật riêng cho concurrency/freshscope.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Xác nhận đã nhận đủ tiền" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST confirm-payment If-Match/idempotency Giữ phần tốt: Retry cùng intent; đủ100%; PAID/CONFIRMED kết thúc
- **Điểm cần quan sát hoặc cải thiện:** Phải kiểm giao dịch ngân hàng thật; ảnh không tự chứng minh PAID
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P104](../reviews/ShuttleBook-ui-button-audit-alobo.md#p104) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107)

<a id="p105"></a>

**P105 — Gửi yêu cầu bổ sung**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** f06-partner.spec.ts:178; f07-partner-series.spec.ts:83. NEEDS_REVIEW giữ cả kỳ và outbox dedup; customer supplement→owner confirm; không autoexpiry.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Gửi yêu cầu bổ sung" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST reject-payment NEEDS_REVIEW Giữ phần tốt: Giữ cả kỳ, thông báo outbox, không hết hạn do owner chậm
- **Điểm cần quan sát hoặc cải thiện:** Chưa chat; không tự đổi review policy thành chat
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P105](../reviews/ShuttleBook-ui-button-audit-alobo.md#p105) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107)

<a id="p106"></a>

**P106 — Xác nhận từ chối cuối cùng**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** f06-partner.spec.ts:178 checkbox required; f07-partner-series.spec.ts:83 nêu cả kỳ. PostGIS verify allallocrelease/no partial/state+outbox.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Xác nhận từ chối cuối cùng" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POST FINAL_REJECTION Giữ phần tốt: Màu nguy hiểm, checkbox+lý do, giải phóng cả kỳ
- **Điểm cần quan sát hoặc cải thiện:** Từ chối cuối cùng không undo theo policy; cần phân biệt với bổ sung
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P106](../reviews/ShuttleBook-ui-button-audit-alobo.md#p106) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:107](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L107)

<a id="p107"></a>

**P107 — Tải lại chi tiết (quyết định stale)**

- **Tiền điều kiện/state:** Chỉ AWAITING_OWNER_CONFIRMATION/NEEDS_REVIEW. Busy hoặc stale khóa fieldset; Đang gửi quyết định…; lỗi alert. Action chọn3 loại; reviewoption disabled nếu đãreview. Finalcheckbox bắt buộc.412/stateconflict cần reload, quyền bịthu hồi xóaprivate. Series áp dụng cả kỳ100%.
- **Agent có thể tự động hóa:** Browser fixture: "Tải lại chi tiết (quyết định stale)" ở Quyết định, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Quyết định: sử dụng "Tải lại chi tiết (quyết định stale)" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Clearintent/reloadversion Giữ phần tốt: 412/state conflict không tự gửi lại
- **Điểm cần quan sát hoặc cải thiện:** Nút chưa khóa loading riêng; phải đọc trạng thái mới trước submit
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P107](../reviews/ShuttleBook-ui-button-audit-alobo.md#p107) · [apps/partner-web/src/features/bookings/BookingDecisions.tsx:111](../../apps/partner-web/src/features/bookings/BookingDecisions.tsx#L111)

#### Cố định detail

<a id="p108"></a>

**P108 — Danh sách buổi, cuộn ngang khi cần**

- **Tiền điều kiện/state:** Series có occurrences: table ngày/giờ/giá/trạng thái, tabindex0 scroll region. Không mutation từng buổi.
- **Agent có thể tự động hóa:** Browser fixture: "Danh sách buổi, cuộn ngang khi cần" ở Cố định detail, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Cố định detail: sử dụng "Danh sách buổi, cuộn ngang khi cần" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Keyboardtable occurrence dates/amounts/status Giữ phần tốt: Bảng cuộn có focus, một quyết định cả kỳ
- **Điểm cần quan sát hoặc cải thiện:** Không xác nhận từng buổi/check-in theo đúng phạm vi
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P108](../reviews/ShuttleBook-ui-button-audit-alobo.md#p108) · [apps/partner-web/src/features/bookings/SeriesSchedule.tsx:16](../../apps/partner-web/src/features/bookings/SeriesSchedule.tsx#L16)

#### Thông báo

<a id="p109"></a>

**P109 — Tải lại thông báo**

- **Tiền điều kiện/state:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Agent có thể tự động hóa:** Browser fixture: "Tải lại thông báo" ở Thông báo, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Thông báo: sử dụng "Tải lại thông báo" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** GET firstpage Giữ phần tốt: Khóa khi tải, lỗi/empty/retry rõ
- **Điểm cần quan sát hoặc cải thiện:** Thiếu thời điểm cập nhật; trang đầu bỏ lịch sử đã tải thêm
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P109](../reviews/ShuttleBook-ui-button-audit-alobo.md#p109) · [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:51](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L51)

<a id="p110"></a>

**P110 — Xem đơn đặt sân**

- **Tiền điều kiện/state:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Agent có thể tự động hóa:** Browser fixture: "Xem đơn đặt sân" ở Thông báo, kiểm đích/API/body/scope, điều kiện ẩn/disabled, loading/lỗi không gửi trùng; bàn phím/Back. Có specs partner-workspace, partner-identity, f06-partner, f07-partner-series; coverage từng ca cần đối chiếu, không mặc định PASS.
- **Bạn thao tác/đánh giá:** Thông báo: sử dụng "Xem đơn đặt sân" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Openbooking+markread Giữ phần tốt: UUID/action hợp lệ, đi đúng business được phép
- **Điểm cần quan sát hoặc cải thiện:** Đánh dấu đọc song song lỗi cần feedback rõ; đây không tạo booking
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P110](../reviews/ShuttleBook-ui-button-audit-alobo.md#p110) · [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:58](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L58)

<a id="p111"></a>

**P111 — Đã đọc**

- **Tiền điều kiện/state:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Agent có thể tự động hóa:** partner-workspace.spec.ts:104 read/error/retry/unread; kiểm thêm paginationretention sauread và globalcount.
- **Bạn thao tác/đánh giá:** Thông báo: sử dụng "Đã đọc" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** POSTread+refresh Giữ phần tốt: Chỉ unread có nút, khóa theo notice đang xử lý, badge toàn bộ
- **Điểm cần quan sát hoặc cải thiện:** Nhãn Đã đọc dễ hiểu như trạng thái thay hành động; thiếu đánh dấu tất cả/lọc chưa đọc
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P111](../reviews/ShuttleBook-ui-button-audit-alobo.md#p111) · [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:59](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L59)

<a id="p112"></a>

**P112 — Thông báo trước đó**

- **Tiền điều kiện/state:** Session; loading/error/empty, poll5s visible/focus. Refresh/more khóa loading, read khóa notice đangreading. Xem đơn chỉ action+UUID. Read/poll refreshfirstpage.
- **Agent có thể tự động hóa:** Loadolder, wait6s, readone; giữ history/cursor, globalunread đúng, dedup. Runtime riêng reviewerNOTRUN.
- **Bạn thao tác/đánh giá:** Thông báo: sử dụng "Thông báo trước đó" trên desktop và điện thoại; kiểm thao tác đúng cơ sở/sân, hiểu next step/error, không mất form ngoài ý muốn.
- **Expected đối chiếu:** Cursor appenddedup Giữ phần tốt: Có cursor/dedup và khóa loading
- **Điểm cần quan sát hoặc cải thiện:** Polling5s thay trang đầu, mất thông báo cũ và cursor. Source xác nhận; reviewer chưa chạy runtime riêng
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét P112](../reviews/ShuttleBook-ui-button-audit-alobo.md#p112) · [apps/partner-web/src/features/notifications/PartnerNotifications.tsx:63](../../apps/partner-web/src/features/notifications/PartnerNotifications.tsx#L63)

### Admin

#### Đăng nhập

<a id="a01"></a>

**A01 — Phương thức liên hệ · Email / Số điện thoại**

- **Tiền điều kiện/state:** Chỉ khi restore xong và chưa có session; disabled busy.
- **Agent có thể tự động hóa:** Chọn phone/email, kiểm type tel/email, label E.164 và body admin-auth/login; test chưa bao phủ riêng switch.
- **Bạn thao tác/đánh giá:** Tab→select, nhập +84 trên mobile; thử email sai định dạng.
- **Expected đối chiếu:** Đổi contactType email/phone, không gọi API; đổi nhãn và validation contact. Giữ phần tốt: Phương thức rõ, label htmlFor và native select hỗ trợ keyboard.
- **Điểm cần quan sát hoặc cải thiện:** NONE — giữ; không coi lựa chọn Email/phone là thiếu sót.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A01](../reviews/ShuttleBook-ui-button-audit-alobo.md#a01) · [apps/admin-web/src/main.tsx:202](../../apps/admin-web/src/main.tsx#L202)

<a id="a02"></a>

**A02 — Email / Số điện thoại E.164**

- **Tiền điều kiện/state:** Conditional nhãn theo contactType; disabled busy; lỗi validation ở status chung.
- **Agent có thể tự động hóa:** Invalid phone/email không gửi login; focus đúng field khi lỗi; không phá E.164 backend.
- **Bạn thao tác/đánh giá:** Thử 09xxx và +849xxx, xem hướng dẫn có hiểu ngay không.
- **Expected đối chiếu:** Cập nhật contact; trim trước POST /api/v1/admin-auth/login. Giữ phần tốt: autoComplete=username; validation email/E.164 trước API; không hiển thị khác biệt tồn tại tài khoản.
- **Điểm cần quan sát hoặc cải thiện:** P2 đề xuất: lỗi validation chỉ chung, không aria-invalid/error riêng từng field; số E.164 có thể khó nhập với người dùng quen 09xxx.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A02](../reviews/ShuttleBook-ui-button-audit-alobo.md#a02) · [apps/admin-web/src/main.tsx:206](../../apps/admin-web/src/main.tsx#L206)

<a id="a03"></a>

**A03 — Mật khẩu**

- **Tiền điều kiện/state:** disabled busy; clear password sau response/error; không lưu storage.
- **Agent có thể tự động hóa:** Nếu thêm show/hide thì toggle type, giữ value, accessible name và không submit ngẫu nhiên.
- **Bạn thao tác/đánh giá:** Dùng password manager/mobile; chốt có cần show/hide không.
- **Expected đối chiếu:** Cập nhật password; gửi cùng contact khi Đăng nhập. Giữ phần tốt: autoComplete=current-password; hỗ trợ password manager, password không retained sau lỗi network/API.
- **Điểm cần quan sát hoặc cải thiện:** P2 đề xuất UX: chưa có Hiện/Ẩn mật khẩu; không phải lỗi auth/backend.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A03](../reviews/ShuttleBook-ui-button-audit-alobo.md#a03) · [apps/admin-web/src/main.tsx:210](../../apps/admin-web/src/main.tsx#L210)

<a id="a04"></a>

**A04 — Đăng nhập / Đang đăng nhập…**

- **Tiền điều kiện/state:** disabled busy; status generic invalid credentials, network và 429 Retry-After tính phút; restoring chỉ hiện Đang kiểm tra phiên quản trị…; F5 restore cookie; idle30 phút từ thao tác.
- **Agent có thể tự động hóa:** Existing wrong-role/429/network/F5/idle/late-refresh tests; bổ sung 401 detail→hành động login và 429 retry UX; server policy cần DB thật.
- **Bạn thao tác/đánh giá:** F5 trước30 phút, để idle hơn30 phút rồi thao tác; thử mạng lỗi và thông báo login.
- **Expected đối chiếu:** POST /api/v1/admin-auth/login credentials include; validate ADMIN ACTIVE; GET /api/v1/admin-auth/me; mở AdminWorkspace. Giữ phần tốt: Chặn double-submit, non-Admin response không vào workspace, safe errors, memory token + HttpOnly restore; policy idle xử lý pointer/keydown thay polling.
- **Điểm cần quan sát hoặc cải thiện:** P2: 429 chỉ ghi số phút, button enabled lại ngay; chưa countdown/lockout UX. Lỗi API bộ phận 401/403 yêu cầu đăng nhập lại nhưng shell không trực tiếp đưa login: cần Đăng xuất trước.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A04](../reviews/ShuttleBook-ui-button-audit-alobo.md#a04) · [apps/admin-web/src/main.tsx:212](../../apps/admin-web/src/main.tsx#L212)

#### Điều hướng desktop/mobile

<a id="a05"></a>

**A05 — Đến nội dung chính**

- **Tiền điều kiện/state:** Focus-visible; chỉ workspace; keyboard Enter; main tabIndex=-1.
- **Agent có thể tự động hóa:** Existing deep-link skip không đổi route; test thêm keyboard trên mỗi page.
- **Bạn thao tác/đánh giá:** Tab đầu tiên→Enter; thử NVDA và zoom200%.
- **Expected đối chiếu:** preventDefault; focus #admin-main, không đổi hash route. Giữ phần tốt: Có lối bỏ navigation, không làm mất #approvals; icon decorative aria-hidden.
- **Điểm cần quan sát hoặc cải thiện:** NONE — giữ; chưa chấm screen-reader/contrast vì chưa đo mới.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A05](../reviews/ShuttleBook-ui-button-audit-alobo.md#a05) · [apps/admin-web/src/layouts/AdminShell.tsx:22](../../apps/admin-web/src/layouts/AdminShell.tsx#L22)

<a id="a06"></a>

**A06 — Đăng xuất / Đang đăng xuất…**

- **Tiền điều kiện/state:** Chỉ workspace; disabled busy; mobile header hoặc sidebar footer; late refresh không revive UI; network logout catch vẫn xóa local.
- **Agent có thể tự động hóa:** Existing late refresh + logout/reload; bổ sung offline logout/server cookie restore test DB thật, không dùng fixture để chứng minh revoke.
- **Bạn thao tác/đánh giá:** Ngắt mạng, Đăng xuất, kết nối lại/F5; kiểm trạng thái và lời thông báo.
- **Expected đối chiếu:** Xóa local session/increment generation trước POST /api/v1/auth/logout refreshToken + bearer; finally giữ session null. Giữ phần tốt: Không token storage, clear private workspace, late response generation guard; logout dễ thấy ở cả viewport.
- **Điểm cần quan sát hoặc cải thiện:** SOURCE_RISK/P2: network logout failure vẫn nói Đã đăng xuất dù server chưa xác nhận revoke; cần diễn tập offline logout→online F5 và đối chiếu cookie/session. Không tuyên bố runtime bug.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A06](../reviews/ShuttleBook-ui-button-audit-alobo.md#a06) · [apps/admin-web/src/layouts/AdminShell.tsx:27](../../apps/admin-web/src/layouts/AdminShell.tsx#L27); [apps/admin-web/src/layouts/AdminShell.tsx:42](../../apps/admin-web/src/layouts/AdminShell.tsx#L42); [apps/admin-web/src/main.tsx:174](../../apps/admin-web/src/main.tsx#L174)

<a id="a07"></a>

**A07 — Mở menu quản trị / Đóng menu quản trị**

- **Tiền điều kiện/state:** Mobile <=800px; aria-controls, aria-expanded, aria-label; icon decorative.
- **Agent có thể tự động hóa:** Existing responsive controls>=44, Escape focus restoration; test thêm navigation closes menu.
- **Bạn thao tác/đánh giá:** 375px→menu→chọn trang; thử Escape bằng keyboard ngoài.
- **Expected đối chiếu:** setMenuOpen; Escape đóng và focus lại opener; đổi route đóng menu. Giữ phần tốt: Có tên accessible, touch target44px; không fake dialog/focus trap cho nav inline.
- **Điểm cần quan sát hoặc cải thiện:** NONE — giữ; pointer-outside close không bắt buộc với menu inline.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A07](../reviews/ShuttleBook-ui-button-audit-alobo.md#a07) · [apps/admin-web/src/layouts/AdminShell.tsx:28](../../apps/admin-web/src/layouts/AdminShell.tsx#L28)

<a id="a08"></a>

**A08 — ShuttleBook Quản trị**

- **Tiền điều kiện/state:** Desktop sidebar; mobile brand là span không clickable.
- **Agent có thể tự động hóa:** Click brand desktop→#overview/session vẫn valid; mobile span không giả control.
- **Bạn thao tác/đánh giá:** Từ detail quay Tổng quan qua brand.
- **Expected đối chiếu:** href #overview; SPA hash navigation không document reload. Giữ phần tốt: Brand desktop quay tổng quan không mất session memory.
- **Điểm cần quan sát hoặc cải thiện:** NONE — giữ; mobile đã có nav Tổng quan, brand không clickable là lựa chọn hợp lệ.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A08](../reviews/ShuttleBook-ui-button-audit-alobo.md#a08) · [apps/admin-web/src/layouts/AdminShell.tsx:34](../../apps/admin-web/src/layouts/AdminShell.tsx#L34)

<a id="a09"></a>

**A09 — Tổng quan**

- **Tiền điều kiện/state:** aria-current=page khi active; mobile menu đóng sau click.
- **Agent có thể tự động hóa:** Back/Forward giữ draft; navigation keyboard heading focus/scroll testcase.
- **Bạn thao tác/đánh giá:** Tab/Enter từ cuối page và Back, kiểm có nhận biết trang mới.
- **Expected đối chiếu:** href #overview; useAdminNavigation đọc hash/change; giữ mounted approval/notification state. Giữ phần tốt: Back/Forward/hash route; drafts giữ khi đi giữa trang; không background polling gia hạn idle.
- **Điểm cần quan sát hoặc cải thiện:** P2 đề xuất: route đổi chưa explicit focus h1/scroll policy; cần kiểm keyboard thật, không coi source thiếu là lỗi runtime chắc chắn.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A09](../reviews/ShuttleBook-ui-button-audit-alobo.md#a09) · [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:5](../../apps/admin-web/src/features/workspace/navigation.ts#L5)

<a id="a10"></a>

**A10 — Duyệt hồ sơ**

- **Tiền điều kiện/state:** aria-current khi active; route không API riêng, mounted component giữ state.
- **Agent có thể tự động hóa:** Deep link #approvals + hash history, keyboard focus, hidden notifications không có focusable controls.
- **Bạn thao tác/đánh giá:** Đi Thông báo→Back→Duyệt hồ sơ, giữ lý do đang nhập.
- **Expected đối chiếu:** href #approvals; chỉ hiện approval panel, notification vẫn mounted hidden. Giữ phần tốt: Không duplicate notice source; draft lý do giữ qua Back/Forward.
- **Điểm cần quan sát hoặc cải thiện:** P2 cùng A09: chưa explicit route focus/scroll; không tự cho Admin xác nhận tiền.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A10](../reviews/ShuttleBook-ui-button-audit-alobo.md#a10) · [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:6](../../apps/admin-web/src/features/workspace/navigation.ts#L6)

<a id="a11"></a>

**A11 — Thông báo**

- **Tiền điều kiện/state:** aria-current khi active; không reload chỉ vì hash/focus window.
- **Agent có thể tự động hóa:** Nav không phát notifications extra GET, không renew idle; route focus test.
- **Bạn thao tác/đánh giá:** Chọn Thông báo bằng keyboard/mobile và quay lại draft.
- **Expected đối chiếu:** href #notifications; ẩn approval panel nhưng giữ mounted detail/draft. Giữ phần tốt: Thông báo một nguồn, không auto GET keep-alive trái idle30 phút.
- **Điểm cần quan sát hoặc cải thiện:** P2 cùng A09: focus/scroll khi route đổi cần xác minh.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A11](../reviews/ShuttleBook-ui-button-audit-alobo.md#a11) · [apps/admin-web/src/layouts/AdminShell.tsx:37](../../apps/admin-web/src/layouts/AdminShell.tsx#L37); [apps/admin-web/src/features/workspace/navigation.ts:7](../../apps/admin-web/src/features/workspace/navigation.ts#L7)

#### Tổng quan

<a id="a12"></a>

**A12 — Hồ sơ chờ xử lý · {count} · Trong danh sách đã tải · Mở danh sách hồ sơ**

- **Tiền điều kiện/state:** Đang tải… / Chưa tải được / số thật; không fake analytics; count tối đa100 từ backend.
- **Agent có thể tự động hóa:** >100 pending: total vs loadedCount rõ và paging không skip/dup khi triển khai; error!=zero.
- **Bạn thao tác/đánh giá:** Kiểm hiểu đây là loaded count, chọn link đi queue.
- **Expected đối chiếu:** href #approvals; count từ rows.length của GET pending approval list. Giữ phần tốt: Ghi rõ Trong danh sách đã tải, lỗi không giả thành0; link mở đúng queue.
- **Điểm cần quan sát hoặc cải thiện:** P2 volume: không có tổng toàn hệ thống/cursor nên >100 còn nhiều hồ sơ nhưng metric không phải total; đã có caption giảm hiểu nhầm.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A12](../reviews/ShuttleBook-ui-button-audit-alobo.md#a12) · [apps/admin-web/src/features/workspace/AdminWorkspace.tsx:20](../../apps/admin-web/src/features/workspace/AdminWorkspace.tsx#L20); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:419](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L419)

<a id="a13"></a>

**A13 — Thông báo chưa đọc · {count} · Xem thông báo**

- **Tiền điều kiện/state:** Đang tải… / Chưa tải được / count; updated mark-read reload.
- **Agent có thể tự động hóa:** Unread counter across pages, error summary, read decrement once.
- **Bạn thao tác/đánh giá:** Đánh dấu đã đọc rồi xem count tổng quan.
- **Expected đối chiếu:** href #notifications; unreadCount API scoped, fallback loaded rows count khi thiếu field. Giữ phần tốt: Server unreadCount gồm cả nhiều trang; một notifications component cho summary/list.
- **Điểm cần quan sát hoặc cải thiện:** NONE — giữ; không suy ALOBO có/không cùng summary.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A13](../reviews/ShuttleBook-ui-button-audit-alobo.md#a13) · [apps/admin-web/src/features/workspace/AdminWorkspace.tsx:21](../../apps/admin-web/src/features/workspace/AdminWorkspace.tsx#L21)

#### Danh sách hồ sơ chờ duyệt

<a id="a14"></a>

**A14 — Tải lại danh sách**

- **Tiền điều kiện/state:** disabled busy||loading; loading/empty/error distinct; 401/403 xóa private rows/detail/images; không polling.
- **Agent có thể tự động hóa:** 503→retry, denied cache purge; >100 pending/oldest ordering paging; stale decision user action.
- **Bạn thao tác/đánh giá:** Lỗi mạng→Tải lại; tìm hồ sơ giữa nhiều tên tương tự.
- **Expected đối chiếu:** GET /api/v1/admin/approval-requests/; abort previous list; giữ selected/detail trừ denied; backend chỉ Admin ACTIVE. Giữ phần tốt: Retry rõ, abort/generation guard response cũ; không biến network failure thành empty.
- **Điểm cần quan sát hoặc cải thiện:** P2 volume: chưa search/filter/cursor cho pending list max100; STATE_CONFLICT/VERSION_CONFLICT lỗi chỉ code chung, refresh list không có hướng dẫn stale-specific.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A14](../reviews/ShuttleBook-ui-button-audit-alobo.md#a14) · [apps/admin-web/src/AdminApprovals.tsx:150](../../apps/admin-web/src/AdminApprovals.tsx#L150)

<a id="a15"></a>

**A15 — {businessName} · Hồ sơ mới / Thay đổi cơ sở · {submittedAt}**

- **Tiền điều kiện/state:** disabled busy||detailLoading; selected style; status Đang tải chi tiết hồ sơ…; malformed snapshot error, no decision controls.
- **Agent có thể tự động hóa:** Delayed A response→B selection không leak; dirty reason switching confirmation if added; revision venue identifying row.
- **Bạn thao tác/đánh giá:** Nhập lý do rồi chọn hồ sơ khác; xem có biết đang xét cơ sở nào.
- **Expected đối chiếu:** GET /api/v1/admin/approval-requests/{id}; clear old selection/reason/private URLs before fetch. Giữ phần tốt: Kind badge/date/selected state; defensive snapshot parse; business/court schedule details sau click.
- **Điểm cần quan sát hoặc cải thiện:** P2 UX: chọn hồ sơ khác xóa reason draft ngay, chưa unsaved-warning; tên row dựa businessName, revision nhiều cơ sở chưa có venueName trong row.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A15](../reviews/ShuttleBook-ui-button-audit-alobo.md#a15) · [apps/admin-web/src/AdminApprovals.tsx:155](../../apps/admin-web/src/AdminApprovals.tsx#L155)

#### Chi tiết hồ sơ mới / thay đổi cơ sở

<a id="a16"></a>

**A16 — Đóng chi tiết**

- **Tiền điều kiện/state:** Chỉ parsed profile/revision visible; disabled busy.
- **Agent có thể tự động hóa:** Close clears selected/images and dirty reason behavior explicit; malformed snapshot no actionable decisions.
- **Bạn thao tác/đánh giá:** Nhập lý do→Đóng rồi mở lại; kiểm warning mong muốn.
- **Expected đối chiếu:** setSelected(null), setReason(''), clear/revoke private image URLs. Giữ phần tốt: Đóng nhanh, xóa ảnh riêng và reason; không thay đổi backend.
- **Điểm cần quan sát hoặc cải thiện:** P2: không cảnh báo reason chưa gửi sẽ mất; malformed snapshot không render close button nhưng row/reload vẫn có.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A16](../reviews/ShuttleBook-ui-button-audit-alobo.md#a16) · [apps/admin-web/src/AdminApprovals.tsx:166](../../apps/admin-web/src/AdminApprovals.tsx#L166)

#### Chi tiết thay đổi cơ sở

<a id="a17"></a>

**A17 — Xem QR mới / Đang tải ảnh…**

- **Tiền điều kiện/state:** disabled any imageBusy||busy; revision only; error generic; private permissions checked backend.
- **Agent có thể tự động hóa:** Revision bank/QR diff correctness + authorized QR read; image403 clears private caches; zoom keyboard nếu triển khai.
- **Bạn thao tác/đánh giá:** Đọc QR/new account trên điện thoại, so hồ sơ đang công bố trước approve.
- **Expected đối chiếu:** GET /api/v1/uploads/{qrUploadId}/view bearer; blob URL QR mới inline width240; revoke on switch/denied/unmount. Giữ phần tốt: Không public URL/cache token; QR proposed hiển thị riêng, blob cleanup.
- **Điểm cần quan sát hoặc cải thiện:** P2 decision UX: revision chỉ proposed address/contact/timezone/location/bank/QR; chưa hiển thị hiện hành→đề nghị hay highlight đổi tài khoản; ảnh chưa zoom/download. Không có bằng chứng ALOBO Admin nên UNKNOWN comparison.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A17](../reviews/ShuttleBook-ui-button-audit-alobo.md#a17) · [apps/admin-web/src/AdminApprovals.tsx:171](../../apps/admin-web/src/AdminApprovals.tsx#L171); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146)

#### Chi tiết hồ sơ mới

<a id="a18"></a>

**A18 — Xem ảnh cơ sở / Đang tải ảnh…**

- **Tiền điều kiện/state:** disabled any imageBusy||busy; visible each venue; image failed shows error; image401/403 denyAccess.
- **Agent có thể tự động hóa:** Private authorized read/fail/revoke, image retry; keyboard close zoom nếu thêm.
- **Bạn thao tác/đánh giá:** Xem ảnh chi tiết trên mobile, chốt có cần phóng to.
- **Expected đối chiếu:** GET /api/v1/uploads/{imageUploadId}/view bearer; show inline blob ảnh venue. Giữ phần tốt: Xem ảnh được cấp quyền; tránh URL public; access denied xóa ảnh/profile; alt Ảnh {venue.name}.
- **Điểm cần quan sát hoặc cải thiện:** P2 proposal: inline image width240 chưa mở lớn/zoom; không thể kết luận nhỏ khó đọc nếu chưa xem screenshot ảnh thực.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A18](../reviews/ShuttleBook-ui-button-audit-alobo.md#a18) · [apps/admin-web/src/AdminApprovals.tsx:181](../../apps/admin-web/src/AdminApprovals.tsx#L181); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146)

<a id="a19"></a>

**A19 — Xem QR / Đang tải ảnh…**

- **Tiền điều kiện/state:** disabled any imageBusy||busy; alt QR {venue.name}; switch/unmount cleanup.
- **Agent có thể tự động hóa:** Correct qr id each venue, private read denies wrong role and cleanup; mobile geometry.
- **Bạn thao tác/đánh giá:** So bank name/number với QR nhìn thấy; không thực hiện chuyển khoản test này.
- **Expected đối chiếu:** GET /api/v1/uploads/{qrUploadId}/view bearer; QR inline blob. Giữ phần tốt: QR/tài khoản/giá ca đặt trong cùng hồ sơ, Admin xem trước publish; không xác nhận payment.
- **Điểm cần quan sát hoặc cải thiện:** P2 proposal cùng A18: chưa zoom/download QR; đòi hỏi thao tác zoom browser khi ảnh nhiều chi tiết, cần test tay.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A19](../reviews/ShuttleBook-ui-button-audit-alobo.md#a19) · [apps/admin-web/src/AdminApprovals.tsx:181](../../apps/admin-web/src/AdminApprovals.tsx#L181); [apps/admin-web/src/AdminApprovals.tsx:146](../../apps/admin-web/src/AdminApprovals.tsx#L146)

#### Chi tiết hồ sơ mới / thay đổi cơ sở

<a id="a20"></a>

**A20 — Phê duyệt / Đang lưu…**

- **Tiền điều kiện/state:** Only selected.status=PENDING + parsed valid profile/revision; disabled busy; 409 conflict renders code generic, no optimistic approve.
- **Agent có thể tự động hóa:** Hai Admin approve/requestchanges race DB; doubleclick one pending request; stale409 actionable copy; confirm cancel issues zero POST if added.
- **Bạn thao tác/đánh giá:** Đọc đầy đủ bank/QR/giá trước approve; chốt có cần màn xác nhận.
- **Expected đối chiếu:** POST /api/v1/admin/approval-requests/{id}/approve; backend Admin ACTIVE, serializable+FOR UPDATE+state/version/media/schedule guards; success clear detail/reload Đã lưu quyết định. Giữ phần tốt: Server guards/audit/outbox; frontend busy duplicate guard; đúng quyền duyệt chứ không nhận tiền.
- **Điểm cần quan sát hoặc cải thiện:** P2 operational UX: một click gửi ngay, chưa review/confirm tên/phạm vi/QR; VERSION_CONFLICT/STATE_CONFLICT chưa giải thích tiếng Việt/hướng refresh detail. Không khẳng định thiếu API idempotency là double-write bug vì server status lock.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A20](../reviews/ShuttleBook-ui-button-audit-alobo.md#a20) · [apps/admin-web/src/AdminApprovals.tsx:196](../../apps/admin-web/src/AdminApprovals.tsx#L196); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:432](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L432)

<a id="a21"></a>

**A21 — Lý do cần chỉnh sửa**

- **Tiền điều kiện/state:** Only pending; disabled busy; helper tối thiểu10 ký tự; draft giữ giữa hash pages, reset close/switch/success.
- **Agent có thể tự động hóa:** 9/10/1000/1001 chars incl trimmed spaces, same server normalize; counter/accessibility, draft preserves navigation.
- **Bạn thao tác/đánh giá:** Viết lý do rõ và dài, xem có biết giới hạn và owner cần sửa chỗ nào.
- **Expected đối chiếu:** Cập nhật reason draft; chỉ gửi khi click Yêu cầu chỉnh sửa. Giữ phần tốt: htmlFor/useId/aria-describedby, yêu cầu reason thay silent reject; phản hồi owner qua outbox.
- **Điểm cần quan sát hoặc cải thiện:** P2 VALIDATION SOURCE: không maxLength/counter1000 trong UI; >1000 vẫn enabled dù backend TextOk10–1000 sẽ trả 400 VALIDATION_FAILED; lack field-specific message.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A21](../reviews/ShuttleBook-ui-button-audit-alobo.md#a21) · [apps/admin-web/src/AdminApprovals.tsx:197](../../apps/admin-web/src/AdminApprovals.tsx#L197); [backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs:511](../../backend/src/ShuttleBook.Api/Onboarding/OnboardingEndpoints.cs#L511)

<a id="a22"></a>

**A22 — Yêu cầu chỉnh sửa**

- **Tiền điều kiện/state:** disabled busy||reason.trim().length<10; busy text vẫn Yêu cầu chỉnh sửa (section aria-busy); success clear/reload; failures preserve reason unless deny.
- **Agent có thể tự động hóa:** Trim<=1000, delayed POST disabled/loading,403 private clear,409 keep draft/error copy, lost response reread actual pending state.
- **Bạn thao tác/đánh giá:** Nhấn lưu chậm, kiểm nhận biết đang gửi; sửa reason sau lỗi.
- **Expected đối chiếu:** POST /api/v1/admin/approval-requests/{id}/request-changes {reason}; backend pending transition, owner notification/audit; onboarding returns draft. Giữ phần tốt: Không gửi reason ngắn, busy duplicate guard, decision ghi backend/outbox; không tự thêm reject/cancel booking.
- **Điểm cần quan sát hoặc cải thiện:** P2: missing >1000 frontend bound (A21); mutation lỗi chỉ code chung; button này không đổi Đang lưu… riêng nên loading nhìn kém rõ hơn approve.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A22](../reviews/ShuttleBook-ui-button-audit-alobo.md#a22) · [apps/admin-web/src/AdminApprovals.tsx:200](../../apps/admin-web/src/AdminApprovals.tsx#L200)

#### Thông báo

<a id="a23"></a>

**A23 — Làm mới thông báo**

- **Tiền điều kiện/state:** disabled busy||reading; loading vs empty/error;401/403clearcache; no polling/focus-refresh.
- **Agent có thể tự động hóa:** 503→refresh, idle unchanged by background focus; updated timestamp correctly distinguishes last success vs request attempt.
- **Bạn thao tác/đánh giá:** Tải lại từ trang nhiều notification; xem cách nhận biết dữ liệu mới.
- **Expected đối chiếu:** GET /api/v1/me/notifications first page; replace list, server unreadCount+cursor; abort previous load. Giữ phần tốt: Chủ động refresh giữ idle policy; retry rõ; no hidden timer renew; scope backend.
- **Điểm cần quan sát hoặc cải thiện:** P2 proposal: chưa last-updated indicator/unread filter; first page refresh intentionally reset page window, cần phân biệt với mark-read reset A24.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A23](../reviews/ShuttleBook-ui-button-audit-alobo.md#a23) · [apps/admin-web/src/AdminNotifications.tsx:67](../../apps/admin-web/src/AdminNotifications.tsx#L67)

<a id="a24"></a>

**A24 — Đánh dấu đã đọc / Đang cập nhật…**

- **Tiền điều kiện/state:** Only !readAt;disabled reading||busy; loading per id; server read idempotent; denied clears items; failure shows retry message.
- **Agent có thể tự động hóa:** 51+ synthetic notices:load more→mark page2→giữ page window/scroll/no duplicates; wrong recipient404; alert contains owner contact/scope only if approved feature.
- **Bạn thao tác/đánh giá:** Xem thêm→đánh dấu dòng cuối, kiểm vị trí; thử xử lý quá hạn có biết liên hệ ai.
- **Expected đối chiếu:** POST /api/v1/me/notifications/{id}/read; on success await load() WITHOUT cursor => replace items first page. Giữ phần tốt: Đọc rõ, unread counter authoritative; alert text nói Admin không nhận tiền/khung giờ vẫn giữ.
- **Điểm cần quan sát hoặc cải thiện:** P2 SOURCE_CONFIRMED: sau Xem thêm, đánh dấu một notification trang2 làm list quay về50 bản đầu; mất vị trí lịch sử (not yet runtime). ADMIN_PAYMENT_ALERT chỉ nhắc Liên hệ chủ sân, không có link/contact lookup/action tới owner; chưa có admin scoped contact flow, không tự mở payment rights.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A24](../reviews/ShuttleBook-ui-button-audit-alobo.md#a24) · [apps/admin-web/src/AdminNotifications.tsx:76](../../apps/admin-web/src/AdminNotifications.tsx#L76); [apps/admin-web/src/AdminNotifications.tsx:60](../../apps/admin-web/src/AdminNotifications.tsx#L60); [apps/admin-web/src/AdminNotifications.tsx:41](../../apps/admin-web/src/AdminNotifications.tsx#L41)

<a id="a25"></a>

**A25 — Xem thêm thông báo**

- **Tiền điều kiện/state:** Visible next!=null;disabled busy||reading; paging error preserves old rows; no separate loading label on this button (section aria-busy).
- **Agent có thể tự động hóa:** 51/101notice paging/dedup/retry; mark-read giữ loadedwindow; repeatedclick one active request; revoked access purge.
- **Bạn thao tác/đánh giá:** Mobile tải nhiều trang, đọc một notification cũ rồi quay lại.
- **Expected đối chiếu:** GET /api/v1/me/notifications?before={nextCursor}; append unique ids; update cursor/server unreadCount. Giữ phần tốt: Cursor+dedup append, không tải tất cả, errors retry; thông báo không thêm xác nhận tiền Admin.
- **Điểm cần quan sát hoặc cải thiện:** P2 phối hợp A24: loaded older page mất sau mark-read; chưa visible Đang tải thêm…/pagecount và filter. Backend default50,max100; không tạo gap bắt buộc mark-allread.
- **Trạng thái hiện tại:** NOT RUN cho testcase focused đề xuất; SOURCE REVIEWED. Regression liên quan xem mục4. Chưa có biên bản tay riêng cho ca này.
- **Đối chiếu:** [Nhận xét A25](../reviews/ShuttleBook-ui-button-audit-alobo.md#a25) · [apps/admin-web/src/AdminNotifications.tsx:78](../../apps/admin-web/src/AdminNotifications.tsx#L78)

## 6. Ghi nhận nghiệm thu

Dùng mẫu cho từng ID, không tick PASS cả trang khi mới xem source:

| ID / ngày / người test | Môi trường, role, dữ liệu test | Thao tác thực tế | Expected | Actual, ảnh/log không secrets | PASS/FAIL/NOT RUN/BLOCKED |
|---|---|---|---|---|---|
| Ví dụ C53 | DB tạm + customer test | Mất response saucommit, chờTTL, cùngkeyretry | Mộtđơn, trảđơn cũ | Điền bằng chứng thực | NOT RUN |

User đã nghiệm thu F07 tổng thể trước audit; không tự tạo biên bản từng control mới từ xác nhận đó. Khi có fix, cập nhật đúng các ca bị ảnh hưởng và gate cần thiết; không phải chạy lại toàn bộ UI sau mỗi chỉnh label nhỏ.
