# M03 — khôi phục phiên Customer/Partner

Ngày 2026-10-09, scope/contract trước code. Người dùng hoãn F08: bản MVP nghiệm thu đợt này chỉ owner vận hành, không staff; F08 DEFERRED, không DONE.

**DONE local — người dùng xác nhận ngày 2026-10-09:** các lệnh typecheck/build/backend build/test:api/test:db và các ca test tay đã chạy đều PASS. Xác nhận này bổ sung các bằng chứng agent trong M03-browser-sessions-results; người dùng chưa cung cấp log/count/biên bản từng testcase, không tự dựng chúng. Production/S3/CI hosted/backup/load vẫn là gate riêng. UI Partner tách trang chi tiết đơn sau xác nhận được nghiệm thu riêng trong Partner-booking-detail-route.md.

## Policy được duyệt

- Customer/Partner idle30phút từ thao tác thật, absolute30ngày từ login, access10phút. Pending owner được vào onboarding; không cấp quyền vận hành mới. Admin giữ flow cũ.
- Cookie HttpOnly riêng theo portal, SameSite Strict, Secure ngoài Development/HTTPS; exact Origin + JSON/custom header chống CSRF. Access chỉ memory, không token localStorage/sessionStorage. Không persist quote/create intent mới; F5 không gia hạn quote/booking.
- Polling/refresh/restore tự động không tăng LastActivityAt. Activity POST chỉ từ pointer/keyboard/scroll người dùng, throttle tối đa60s; frontend phải giữ mốc chưa gửi và flush theo trailing timer, không bỏ thao tác cuối. Activity sau idle không hồi sinh phiên. Boundary được test clock.
- Logout server thành công thu hồi family, xóa cookie, broadcast đóng phiên cùng portal. Response restore muộn không được hồi sinh UI. Lỗi mạng logout báo rõ chưa server-confirm, phải ngăn restore tự động ở browser và có retry logout trước restore.
- Request đã xác thực trước logout/hết idle có thể hoàn tất transaction đang chạy/chờ khóa; logout chặn xác thực/restore mới, không cam kết rollback mọi thao tác đã được nhận. Frontend bỏ response muộn. Các fresh checks business về user/membership vẫn giữ.

## Data/API

Dùng refresh_sessions.LastActivityAt sẵn có; non-admin có LastActivityAt là browser family. Absolute ExpiresAt giữ từ login, không cộng30ngày mỗi lần refresh. Family advisory lock dùng chung refresh/logout/browser restore/activity: lookup family trước lock, re-read sau lock; consumed token có thể logout đúng family, không bỏ sót replacement. JWT kiểm family unrevoked + unconsumed + expiry + browser idle, không touch activity do polling.

POST `/api/v1/browser-auth/{portal}/{action}`, portal customer/partner:

- login body `{contactType,contact,password}`; kiểm role trước cấp session/cookie. 200 `{data:{tokenType,accessToken,expiresInSeconds,refreshExpiresAt,idleExpiresAt,user},traceId}`; **không refreshToken JSON**.
- restore body `{}` + credentials include: cookie stable như cơ chế Admin restore, kiểm idle/absolute/fresh user; phát access mới, không touch activity, không rotate cookie token nên đa tab không gây reuse giả.
- activity body `{}` + credentials include: fresh cookie/session/role/idle; cập nhật activity khi còn hạn; response200 token envelope cùng contract, frontend đồng bộ idle deadline.
- logout body `{}` + credentials include: không cần access còn hạn; thu hồi family cookie đúng role, idempotent204 kể cả cookie đã invalid/missing, clear cookie. Ngoài Origin403 không thao tác/xóa cookie.
- Errors400VALIDATION_FAILED/UNSUPPORTED_FIELD (body parser hiện có),401INVALID_CREDENTIALS/INVALID_REFRESH_TOKEN,403FORBIDDEN,429RATE_LIMITED. No-store mọi response auth. Allowed origins cấu hình BrowserSession:CustomerOrigin/PartnerOrigin; Development mặc định5173/5174. Không tin header portal/Origin để nhận bất kỳ account type.
- Cookie `shuttlebook_customer_refresh`/`shuttlebook_partner_refresh`, Path `/api/v1/browser-auth/customer` hoặc partner, MaxAge bằng thời gian absolute còn lại. Body origin/json validation mọi action, không cookie wildcard/CORS tùy ý.

## UI/acceptance

Chặn private children trong startup restore để không redirect login sớm; guest public vẫn truy cập sau restore401. Restore network/5xx có retry/message, không giả INVALID_CREDENTIALS. Single-flight/epoch guard; cross-tab logout signal không chứa token; idle watchdog theo deadline backend, thao tác sau idle không mở lại. Customer returnTo nội bộ giữ hiện tại; Partner URL navigation giữ khi F5. Booking detail sau bank return phục hồi dữ liệu bằng GET, không create lại. API legacy /auth/login/refresh/logout giữ tương thích client hiện có.

## Testcase/gates

PostGIS thật: customer/owner login+restore+activity, exact idle30m/absolute30d, polling không touch, role/origin/cookie/no token JSON, pending/suspend/revoke, restore/activity/logout concurrent, legacy refresh/logout barrier có replacement và consumed logout. Browser desktop/mobile: restore F5/private route/Partner detail, customer và partner cùng profile không collision, no JS-readable refresh/token storage, expired->login, logout2tabs/late restore/network retry, auth các luồng casual/fixed/proof/report. Regression build/API/DB/UI và live local. HostedCI/provider/S3/production riêng, không đổi gate thành PASS.
