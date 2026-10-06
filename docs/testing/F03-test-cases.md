# F03 — testcase và bằng chứng

Trạng thái: **PASS local; người dùng xác nhận nghiệm thu tay và tạm thời chấp thuận ngày 2026-10-06**. Không có log chi tiết riêng cho từng bước tay trong hội thoại. Ca transaction/constraint dùng PostgreSQL/PostGIS thật trên database tạm.

| ID | Bước / dữ liệu | Kỳ vọng | Loại | Kết quả |
|---|---|---|---|---|
| F03-T01 | Fresh/repeat migration, nâng cấp DB F02 đã có business/venue/court/lịch/giá/QR | Dữ liệu cũ nguyên vẹn, version/default và constraint mới đúng | DB | PASS — F03 upgrade + baseline fresh/repeat |
| F03-T02 | Pending/customer/suspended/owner B gọi operator API của court A | Pending/customer 403, owner B 404, token suspend 401 ở auth; không đổi DB | API+DB | PASS — F03 integration |
| F03-T03 | Owner active sửa lịch tuần/giá cơ bản; thiếu giá, giá 0, lệch lưới 30 phút | Hợp lệ lưu nguyên tử; sai trả 400, dữ liệu cũ không đổi | API+DB | PASS — F03 integration |
| F03-T04 | Hai lần cập nhật lịch/giá cùng version | Một 200, một 412; không mất update | DB concurrency | PASS — hai PUT đồng thời |
| F03-T05 | Override theo khoảng ngày/day/khung giờ/priority; preview nhiều mức giá | Chọn priority cao nhất từng ca, cộng tiền nguyên đúng | API+DB | PASS — 4 ca, tổng 900.000 VND |
| F03-T06 | Rule cùng priority giao ngày/giờ, request race hoặc SQL trực tiếp | PostgreSQL constraint/API từ chối, không có rule trùng | DB constraint | PASS — API 400, SQLSTATE 23P01 |
| F03-T07 | Rule ngoài giờ mở hoặc thiếu giá cơ bản, preview ngày/giờ sai | 400 hoặc 409 đúng contract, không tạo quote | API+DB | PASS — 400/409, version giữ nguyên |
| F03-T08 | Tạo maintenance theo local timezone; xem UTC trong DB | UTC đúng, cùng court interval `[start,end)` | API+DB | PASS — 08:00 Asia/Ho_Chi_Minh = 01:00 UTC |
| F03-T09 | Tạo maintenance quá khứ/lệch slot/overlap/race, SQL trực tiếp | 400/409 và exclusion constraint chặn giao nhau | DB concurrency | PASS — 400/409, SQLSTATE 23P01, POST đồng thời |
| F03-T10 | Cancel maintenance hai lần; tạo lại cùng slot | Allocation RELEASED, audit một lần, slot dùng lại được | API+DB | PASS — F03 integration |
| F03-T11 | QR sau publish qua revision; trước/sau Admin approve | QR/tài khoản published còn nguyên tới approve, không có direct write | API+DB+UI | PASS — F02 revision DB và live browser desktop/mobile |
| F03-T12 | Partner desktop/mobile thao tác lịch/giá/preview/bảo trì qua API/PostGIS tạm | Luồng UI hoạt động, không lưu token vào storage | Browser live | PASS — 2 viewport trên PostGIS tạm |

## Bằng chứng

| Nhóm | Lệnh PowerShell tại repo root | Kết quả |
|---|---|---|
| Backend build | `dotnet build backend/ShuttleBook.slnx --no-restore` | PASS — 0 warning/error |
| API | `npm.cmd run test:api` | PASS — 77/77 |
| DB/PostGIS | `npm.cmd run test:db -- --logger "console;verbosity=normal"`; chạy lại ca baseline và F03 bằng `--filter` | Lượt đầy đủ 22/23; ca baseline cũ FAIL do constraint F03 dùng `btree_gist`. Đã sửa kỳ vọng và chạy lại baseline 1/1 PASS; F03 2/2 PASS. Các ca còn lại 22/22 đã PASS ở lượt đầy đủ; chưa chạy lại nguyên bộ sau sửa test |
| Frontend | `npm.cmd run typecheck`; `npm.cmd run build` | PASS — 3 web |
| Browser | `npm.cmd run test:web`; `npm.cmd run test:identity-live` | PASS — 32/32 mock/smoke, 8 live bị skip chủ đích; live 8/8 desktop/mobile trên DB tạm |
| Whitespace | `git diff --check` | PASS — exit 0; chỉ có cảnh báo chuyển CRLF/LF của Git |

Không đánh dấu PASS bằng mock/source. Không chạy migration/drop database development, không in secrets/contact/QR thật. Review tự thực hiện tuần tự; không có reviewer độc lập. S3 provider thật và nghiệm thu tay vẫn thuộc phần mở của F02/F03; không suy diễn từ adapter local.
