# IAM 4.1.7–4.1.18 — Báo cáo triển khai

Ngày: 01/11/2026. Tiếp tục từ code hiện có trên nhánh `dev`, giữ nguyên sáu loại tài khoản: SchoolAdmin, Principal, VicePrincipal, Teacher, Student, Parent.

## Phạm vi

| WBS | Kết quả Backend / Web |
| --- | --- |
| 4.1.7 | Menu và kiểm URL dùng cùng điều kiện theo role/trách nhiệm. Hiệu phó cần phân hiệu đang phụ trách; giao bài cần phụ trách chuyên môn; chủ nhiệm và tổ trưởng có menu riêng. School Admin chỉ có Thống kê hệ thống trong Bảng điều khiển, không có thời khóa biểu, xuất dữ liệu hoặc hành vi của role khác. |
| 4.1.8 | Thêm bộ test riêng cho sáu role, ba trách nhiệm, phụ trách chuyên môn từ bản đã phát hành, ghi danh học sinh và liên kết phụ huynh; kiểm tra cả đối tượng ngoài phạm vi. |
| 4.1.9 | Danh sách tài khoản giữ các cột loại, trạng thái, phân hiệu chính và lần đăng nhập gần nhất; bổ sung thứ tự ổn định khi phân trang. |
| 4.1.10 | Kiểm thử tìm theo tên, mã hồ sơ, tên đăng nhập; lọc loại, trạng thái, phân hiệu và phân trang. |
| 4.1.11 | Thêm nút tạo đơn lẻ tại trang Tài khoản, chọn hồ sơ từ danh bạ. Backend kiểm loại tài khoản tương thích với hồ sơ, trạng thái cán bộ và tài khoản đã tồn tại. |
| 4.1.12 | Thêm tạo hàng loạt cho danh bạ cán bộ/học sinh/phụ huynh trong IAM. Cán bộ chọn một loại cho lô; Student/Parent được suy từ danh bạ; bỏ qua hồ sơ đã có tài khoản, hồ sơ thiếu/không hợp lệ. Tối đa 200 hồ sơ/lần. Một audit cho cả lô, kể cả khi chỉ tạo thành công một tài khoản. |
| 4.1.13 | Kết quả và mật khẩu tạm chỉ giữ trong state của màn hình; đóng thì xóa state và nội dung modal. Excel được tạo từ kết quả vừa nhận, không lưu file hoặc mật khẩu thô trên server, không có API đọc lại mật khẩu. |
| 4.1.14 | Khóa tài khoản, thu hồi mọi phiên; kiểm thử access/refresh token cũ bị từ chối. |
| 4.1.15 | Cảnh báo trước khi khóa; chuyển các tiết Scheduled chưa có điểm danh từ hôm nay tại mọi phân hiệu sang Pending/Manual, bỏ giáo viên; thông báo đúng hiệu phó đang phụ trách, có tài khoản phù hợp và chưa khóa. Đóng đúng trách nhiệm đang hiệu lực, ghi số tiết/trách nhiệm vào audit; giữ lịch sử và các bản trách nhiệm chưa đến ngày hiệu lực. |
| 4.1.16 | Mở khóa khôi phục Active hoặc PendingPasswordChange theo việc đã đổi mật khẩu lần đầu. Không khôi phục phiên cũ hoặc trách nhiệm đã đóng. |
| 4.1.17 | Trước khi đổi loại có cảnh báo tiết/trách nhiệm. Thao tác cấp mật khẩu tạm mới, đóng trách nhiệm/chuyển tiết theo loại cũ, thu hồi mọi phiên, ghi audit và giữ hồ sơ/lịch sử. |
| 4.1.18 | Kiểm thử vòng đời tài khoản bằng InMemory và SQL Server thật: unique constraint, commit toàn bộ tác động, rollback khi lưu lỗi kể cả ExecuteUpdate thu hồi phiên. |

Mobile hiện chỉ có `mobile/.gitkeep`, chưa có ứng dụng Flutter. Phần mobile của 4.1.7 chưa triển khai trong đợt này.

## Các file đã sửa

| File | Nội dung |
| --- | --- |
| `backend/MSMS.Api/Modules/Iam/Controllers/AccountsController.cs` | API đọc danh bạ phục vụ chọn hồ sơ, giữ policy SchoolAdmin cho toàn bộ endpoint tài khoản. |
| `backend/MSMS.Api/Modules/Iam/Dtos/AccountDtos.cs` | DTO danh bạ chọn tài khoản. |
| `backend/MSMS.Api/Modules/Iam/Services/AccountService.cs` | Danh bạ, validation tạo tài khoản, kết quả bỏ qua, audit đơn/lô, đóng trách nhiệm hiện tại, lọc người nhận thông báo và kiểm tra Excel. |
| `backend/MSMS.Api/Modules/Iam/Services/SessionService.cs` | Giữ ExecuteUpdate trên relational database; thêm đường tương đương cho InMemory để test thu hồi phiên. |
| `web/src/features/iam/accountApi.ts` | Gọi API danh bạ và tạo đơn lẻ. |
| `web/src/features/iam/types.ts` | Kiểu dữ liệu danh bạ. |
| `web/src/features/iam/pages/AccountsPage.tsx` | Tạo đơn/lô, cảnh báo tác động khi đổi loại, cập nhật cache và hiển thị ngay kết quả mật khẩu. |
| `web/src/features/iam/components/TempPasswordModal.tsx` | Hủy nội dung modal khi đóng. |
| `web/src/app/menu.tsx` | Bổ sung kiểm trách nhiệm phân hiệu/phụ trách chuyên môn cho menu và URL tương ứng. |
| `web/src/features/par/types.ts` | Sửa phần chú thích bị hỏng ở dòng 45 của code có sẵn, để TypeScript build được; không thay đổi kiểu dữ liệu hoặc logic PAR. |

## Các file đã thêm

| File | Nội dung |
| --- | --- |
| `web/src/features/iam/components/CreateAccountsModal.tsx` | Chọn hồ sơ từ ba danh bạ, tạo đơn/lô, phân trang và hiển thị lỗi. |
| `web/src/features/iam/components/AccountImpact.tsx` | Nội dung cảnh báo tiết và trách nhiệm dùng chung cho khóa/đổi loại. |
| `web/tests/iam-navigation.test.mjs` | Sáu test menu và quyền truy cập URL theo sáu role/trách nhiệm. |
| `backend/MSMS.Tests/Modules/Iam/AccountTestFixture.cs` | Dữ liệu mẫu và ngữ cảnh IAM. |
| `backend/MSMS.Tests/Modules/Iam/AccountLifecycleTests.cs` | Test nghiệp vụ quản lý tài khoản và bảo toàn lịch sử. |
| `backend/MSMS.Tests/Modules/Iam/AccountSqlServerTests.cs` | Ba integration test với database SQL Server tạm riêng. |
| `backend/MSMS.Tests/Authorization/IamScopeTests.cs` | Test phạm vi từng role và trách nhiệm. |
| `backend/MSMS.Tests/Authorization/IamPolicyTests.cs` | Test policy từng role và bảo vệ endpoint quản lý tài khoản. |
| `docs/IAM_4.1.7-4.1.18_Implementation.md` | Báo cáo này. |

Không sửa schema, migration, package/dependency hoặc code nghiệp vụ của SCH, PPL, SCD, LRN, COM, ADM. Các tác động lịch/trách nhiệm/thông báo được xử lý qua luồng IAM hiện có và các thành phần Common hiện có.

## Xác minh

- `dotnet test backend/MSMS.sln --no-restore --verbosity minimal` với biến môi trường `MSMS_IAM_SQLSERVER`: 410/410 đạt, gồm ba test SQL Server thật.
- `node --test tests/*.test.mjs` trong `web`: 36/36 đạt.
- `npm run build` trong `web`: đạt. Vite còn cảnh báo bundle lớn, chưa thay đổi cách chia bundle trong phạm vi IAM.
- `npm run lint` trong `web`: exit code 0; còn các cảnh báo ở code có sẵn ngoài phần thay đổi.
- `npx oxlint src/features/iam src/app/menu.tsx tests/iam-navigation.test.mjs`: đạt, không có cảnh báo.
- `git diff --check`: đạt; `git ls-files --unmerged`: không có file xung đột.

Các integration test chỉ chạy khi `MSMS_IAM_SQLSERVER` chứa connection string đến SQL Server có quyền tạo database. Test luôn thay tên database thành `MSMS_IamTests_<GUID>`, tạo schema từ model và xóa đúng database đó khi kết thúc. Khi không đặt biến môi trường, ba test này được skip; các test còn lại chạy bằng InMemory. Đợt kiểm tra này đã chạy cả ba test SQL Server và dọn các database tạm.

Chưa kiểm thử giao diện bằng trình duyệt hoặc ứng dụng mobile.
