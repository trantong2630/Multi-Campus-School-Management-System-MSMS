# 4. Software Product Development — MSMS

Cấu trúc phân rã: Module → Feature → Task. Mỗi Task là một hành động nghiệp vụ hoàn chỉnh của một actor. Các task thuộc cùng một Feature tô cùng màu ở cột Feature. Căn theo Business Specification V3.1 (Oct 20, 2026) — 9 Module / 33 Feature / 264 Task.

## Mục lục
- 4.1 IAM — Identity & Access (Định danh & Truy cập)
- 4.2 SCH — School Setup (Thiết lập trường)
- 4.3 PPL — People & Assignment (Hồ sơ & Phụ trách)
- 4.4 SCD — Timetable & Teaching Schedule (Thời khóa biểu & Lịch dạy)
- 4.5 ATT — Attendance (Điểm danh)
- 4.6 LRN — Learning (Học tập)
- 4.7 PAR — Parent Services (Dịch vụ phụ huynh)
- 4.8 COM — Communication (Truyền thông)
- 4.9 ADM — Administration & Oversight (Quản trị & Giám sát)

---

## 4.1 IAM — Identity & Access (Định danh & Truy cập)

### IAM-01 Authentication & Authorization (Đăng nhập & Phân quyền)

**4.1.1 — Đăng nhập / Log In**  
`BE + Web + Mobile` · 1.5 MD  
Đăng nhập bằng tên đăng nhập và mật khẩu, dùng chung cho cả sáu loại tài khoản. Tài khoản Đã khóa không vào được; thông báo lỗi không tiết lộ tài khoản có tồn tại hay không (BR-01, BR-05). Mỗi lần đăng nhập tạo một phiên theo thiết bị và trả access token ngắn hạn (15 phút) kèm refresh token (7 ngày, xoay vòng mỗi lần dùng, chỉ lưu bản băm); Web nhận refresh token qua cookie HttpOnly không đặt hạn (các tab dùng chung, đóng trình duyệt là phải đăng nhập lại; access token chỉ giữ trong bộ nhớ trang); mobile nhận trong body. Client gọi `POST /api/auth/refresh` để lấy access token mới; web và API phải cùng domain khi deploy.

**4.1.2 — Đăng xuất / Log Out**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.1.1  
Thu hồi phiên của thiết bị hiện tại ở Backend (`POST /api/auth/logout`): access token và refresh token của phiên đó mất hiệu lực ngay. Không có chức năng đăng xuất khỏi mọi thiết bị (BR-05).

**4.1.3 — Phân giải phạm vi và trách nhiệm tại ngày hiện tại / Resolve Scope and Responsibilities**  
`BE` · 2 MD · dep: 4.1.1  
Sau khi đăng nhập, hệ thống xác định phạm vi dữ liệu người đó chạm tới được TẠI NGÀY HIỆN TẠI: phân hiệu phụ trách, tổ chuyên môn, lớp chủ nhiệm, lớp và môn mình phụ trách chuyên môn, ghi danh hiệu lực, hoặc liên kết phụ huynh – học sinh. Luôn lấy bản đang hiệu lực, không lấy bản mới nhất (BR-09).

**4.1.4 — Kiểm quyền hai tầng cho mọi chức năng / Enforce Two-layer Authorization**  
`BE` · 2 MD · dep: 4.1.3  
Mọi yêu cầu qua hai lần kiểm độc lập: loại tài khoản có được gọi chức năng này không, và đối tượng cụ thể có nằm trong phạm vi của người gọi không. Giao diện không bao giờ là căn cứ phân quyền (BR-01).

**4.1.5 — Cấp quyền hành vi theo vai trò / Role-bound Action Policies**  
`BE` · 0.5 MD · dep: 4.1.4  
Các hành vi chỉ đúng vai trò làm được — làm và nộp bài, gửi và rút đơn nghỉ, tạo hội thoại và nhắn tin, soạn — giao — nhân bản đề, công bố điểm, thao tác nhận xét AI, điểm danh với tư cách giáo viên của tiết — chỉ được cấp cho vai trò tương ứng bằng Authorization Policy thông thường. School Admin không nằm trong policy nào của chúng và không có cờ bỏ qua phân quyền (BR-02).

**4.1.6 — Kiểm trạng thái tài khoản ở mỗi request / Enforce Account Status per Request**  
`BE` · 1 MD · dep: 4.1.4  
Backend đọc trạng thái tài khoản và kiểm phiên (chưa đăng xuất, chưa bị thu hồi, chưa hết hạn) ở mỗi request, nên tài khoản bị khóa hoặc phiên đã đăng xuất bị từ chối ngay ở thao tác kế tiếp. Khóa tài khoản, reset mật khẩu và đổi loại tài khoản thu hồi mọi phiên của tài khoản; đổi mật khẩu thu hồi các phiên khác ngoài phiên đang dùng. Refresh token cũ bị dùng lại thì thu hồi cả phiên (BR-05).

**4.1.7 — Hiển thị menu theo loại tài khoản và trách nhiệm / Render Role-based Navigation**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.1.3  
Menu dựng theo loại tài khoản cộng trách nhiệm đang hiệu lực. Giáo viên chỉ thấy chức năng của lớp và môn mình phụ trách; thêm mục chủ nhiệm hoặc tổ trưởng khi đang giữ trách nhiệm đó. School Admin không thấy Dashboard, Thời khóa biểu, Xuất dữ liệu và các chức năng làm việc đứng tên người khác.

**4.1.8 — Kiểm thử phạm vi theo loại tài khoản và trách nhiệm / Scope Tests per Role**  
`QA` · 2.5 MD · dep: 4.1.4, 4.1.5  
Mỗi loại tài khoản và mỗi trách nhiệm có bộ kiểm thử riêng trên dữ liệu mẫu. Không dùng tài khoản School Admin để kiểm phạm vi của loại khác vì tài khoản này có phạm vi toàn trường nên không phát hiện được rò rỉ (mục 2.5).

### IAM-02 Account Management (Quản lý tài khoản)

**4.1.9 — Xem danh sách tài khoản / View Account List**  
`BE + Web` · 1.5 MD · dep: 4.1.4  
Xem toàn bộ tài khoản cùng loại, trạng thái, phân hiệu chính và thời điểm đăng nhập gần nhất. Mỗi hồ sơ có đúng một tài khoản (BR-01).

**4.1.10 — Tìm kiếm và lọc tài khoản / Search and Filter Accounts**  
`BE + Web` · 1 MD · dep: 4.1.9  
Thu hẹp danh sách theo tên, mã hồ sơ, tên đăng nhập, loại tài khoản, trạng thái và phân hiệu.

**4.1.11 — Tạo tài khoản cho một hồ sơ / Create Single Account**  
`BE + Web` · 1.5 MD · dep: 4.1.9  
Chọn một hồ sơ chưa có tài khoản, chọn loại tài khoản phù hợp với loại hồ sơ; hệ thống sinh tên đăng nhập và mật khẩu tạm (BR-03, BR-04).

**4.1.12 — Tạo tài khoản hàng loạt từ danh bạ / Bulk Create Accounts from Directory**  
`BE + Web` · 2 MD · dep: 4.1.11  
Chọn nhiều dòng trên danh bạ rồi tạo một lượt. Danh bạ học sinh ra Student, danh bạ phụ huynh ra Parent, danh bạ cán bộ chọn một loại cho cả lô. Hồ sơ đã có tài khoản thì bỏ qua (BR-04).

**4.1.13 — Nhận danh sách mật khẩu tạm sau khi tạo / Receive Temporary Password List**  
`BE + Web` · 1 MD · dep: 4.1.12  
Mật khẩu tạm hiển thị MỘT lần ngay trong màn hình kết quả tạo, kèm file Excel tải ngay để bàn giao. File không được lưu và không xuất lại được; mất thì phải cấp lại mật khẩu (BR-04).

**4.1.14 — Khóa tài khoản / Lock Account**  
`BE + Web` · 1 MD · dep: 4.1.9  
Khóa tài khoản khiến người đó không đăng nhập được và phiên đang mở bị từ chối ở request kế tiếp; Backend thu hồi mọi phiên của tài khoản. Hồ sơ và lịch sử nghiệp vụ giữ nguyên. Hệ quả lên lịch dạy và trách nhiệm xử lý ở 4.1.15 (BR-05).

**4.1.15 — Khóa tài khoản giáo viên: chuyển tiết và đóng trách nhiệm / Lock Teacher: Reassign Lessons and Close Duties**  
`BE + Web` · 1.5 MD · dep: 4.1.14  
Khóa tài khoản không bị chặn bởi lịch dạy. Cùng một transaction: với tài khoản Teacher, tiết Đã xếp lịch chưa khóa từ hôm nay trở đi ở mọi phân hiệu chuyển Chờ xử lý (Chỉnh tay, để trống giáo viên) và Vice Principal phân hiệu chứa tiết nhận thông báo để xử lý theo 4.4.29–4.4.34; với mọi tài khoản cán bộ, trách nhiệm và phụ trách chuyên môn đang hiệu lực (phụ trách phân hiệu, tổ trưởng, chủ nhiệm, phụ trách chuyên môn) được đặt ngày kết thúc là ngày khóa và quyền mất ngay lúc khóa. Mở khóa lại không khôi phục các trách nhiệm đã đóng. Màn hình cảnh báo số tiết, phân hiệu và danh sách trách nhiệm trước khi xác nhận; Audit ghi số tiết và số trách nhiệm bị ảnh hưởng (BR-05, BR-09).

**4.1.16 — Mở khóa tài khoản / Unlock Account**  
`BE + Web` · 0.5 MD · dep: 4.1.14  
Mở lại tài khoản đang bị khóa. Tài khoản chưa từng đổi mật khẩu lần đầu quay về trạng thái Chờ đổi mật khẩu (BR-05).

**4.1.17 — Đổi loại tài khoản / Change Account Type**  
`BE + Web` · 1.5 MD · dep: 4.1.9, 4.1.15  
Cấp sai loại thì đổi loại tài khoản bằng MỘT thao tác: ghi Audit, cấp lại mật khẩu tạm, đưa tài khoản về Chờ đổi mật khẩu và thu hồi mọi phiên. Trách nhiệm đang hiệu lực kết thúc vào ngày đổi, và nếu loại cũ là Teacher thì tiết tương lai chuyển Chờ xử lý, cùng cách với 4.1.15. Hồ sơ và mọi lịch sử nghiệp vụ giữ nguyên vì chúng gắn với hồ sơ, không gắn với tài khoản (BR-01, BR-05, BR-28).

**4.1.18 — Kiểm thử vòng đời tài khoản / Account Lifecycle Tests**  
`QA` · 1.5 MD · dep: 4.1.12, 4.1.15, 4.1.17  
Tạo đơn lẻ và hàng loạt; một hồ sơ không tạo được tài khoản thứ hai; khóa tài khoản giáo viên còn tiết sắp tới không bị chặn, tiết chuyển Chờ xử lý, Vice Principal nhận thông báo và trách nhiệm kết thúc vào ngày khóa; đổi loại tài khoản cũng đóng trách nhiệm mà không làm mất lịch sử; mật khẩu tạm không xem lại được.

### IAM-03 Password (Mật khẩu)

**4.1.19 — Đổi mật khẩu bắt buộc ở lần đăng nhập đầu / Force First-time Password Change**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.1.1  
Tài khoản ở trạng thái Chờ đổi mật khẩu chỉ gọi được chức năng đổi mật khẩu và đăng xuất; đổi xong chuyển sang Đang hoạt động (BR-04).

**4.1.20 — Đổi mật khẩu khi đang đăng nhập / Change Password**  
`BE + Web + Mobile` · 1 MD · dep: 4.1.1  
Yêu cầu nhập mật khẩu hiện tại rồi đặt mật khẩu mới. Mật khẩu chỉ lưu dạng băm (BR-04).

**4.1.21 — Yêu cầu đặt lại mật khẩu khi quên / Request Password Reset**  
`BE + Web + Mobile` · 1.5 MD  
Người dùng nhập tên đăng nhập. Nếu tài khoản Đang hoạt động và hồ sơ có email thì hệ thống sinh OTP 6 chữ số. Màn hình luôn trả cùng một thông điệp, không tiết lộ tài khoản có tồn tại hay có email không (BR-04).

**4.1.22 — Gửi mã OTP dùng Gmail SMTP / Send OTP via Gmail SMTP**  
`BE` · 1.5 MD · dep: 4.1.21  
Gửi email chỉ chứa mã OTP và thời hạn, từ hộp thư chung của trường, tới email ghi trong hồ sơ. Không hàng đợi, không thử lại tự động; Gmail SMTP hỏng thì người dùng bấm Gửi lại mã (mục 3.2).

**4.1.23 — Gửi lại mã / Resend Code**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.1.21  
Gửi lại OTP, sớm nhất 60 giây sau lần gửi trước. Mã mới thay mã cũ; mỗi tài khoản chỉ có một OTP còn hiệu lực (BR-04).

**4.1.24 — Đặt lại mật khẩu bằng mã / Reset Password with Code**  
`BE + Web + Mobile` · 1 MD · dep: 4.1.21  
Nhập đúng OTP rồi đặt mật khẩu mới. OTP hết hạn sau 10 phút và mất hiệu lực sau 5 lần nhập sai (BR-04).

**4.1.25 — School Admin cấp lại mật khẩu tạm / Admin Reset Password**  
`BE + Web` · 1 MD · dep: 4.1.9  
Đường dự phòng cho hồ sơ không có email hoặc khi Gmail SMTP hỏng. Sinh mật khẩu tạm mới, hiển thị một lần, tài khoản quay về Chờ đổi mật khẩu; ghi Audit (BR-04, BR-28).

**4.1.26 — Kiểm thử mật khẩu và OTP / Password and OTP Tests**  
`QA` · 1.5 MD · dep: 4.1.21, 4.1.24  
Mã hết hạn, nhập sai quá số lần, gửi lại quá sớm, mã cũ mất hiệu lực khi có mã mới, hồ sơ không có email, Gmail SMTP hỏng.

### IAM-04 User Profile (Hồ sơ cá nhân)

**4.1.27 — Xem hồ sơ cá nhân / View Own Profile**  
`BE + Web + Mobile` · 1 MD · dep: 4.1.3  
Xem thông tin cá nhân, mã hồ sơ, loại tài khoản và trách nhiệm đang giữ của chính mình.

**4.1.28 — Cập nhật trường cá nhân được phép / Update Own Profile**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.1.27  
Người dùng sửa các trường cá nhân được phép, gồm email dùng để nhận OTP. Không có luồng xác nhận email riêng — email là một trường thông tin bình thường (BR-04).

**4.1.29 — Tải và đổi ảnh đại diện / Upload Avatar**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.1.27  
Tải ảnh JPG hoặc PNG làm ảnh đại diện, dùng chung cách lưu file với ảnh bài đăng bảng tin (BR-27).

**4.1.30 — Xem trạng thái tài khoản của mình / View Own Account Status**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.1.27  
Xem loại tài khoản, trạng thái và lần đăng nhập gần nhất của chính mình.

---

## 4.2 SCH — School Setup (Thiết lập trường)

### SCH-01 Campus (Phân hiệu)

**4.2.1 — Xem danh sách phân hiệu / View Campus List**  
`BE + Web` · 0.5 MD · dep: 4.1.4  
Xem các phân hiệu cùng mã, tên, địa chỉ và tình trạng hoạt động. Thông tin nhận diện của trường làm ở SCH-07.

**4.2.2 — Tạo phân hiệu / Create Campus**  
`BE + Web` · 1 MD · dep: 4.2.1  
Khai phân hiệu mới với mã và tên duy nhất toàn trường. Tên là thứ các file import tra cứu nên không được trùng (BR-07).

**4.2.3 — Cập nhật phân hiệu / Update Campus**  
`BE + Web` · 0.5 MD · dep: 4.2.2  
Sửa tên, địa chỉ và mô tả của phân hiệu.

**4.2.4 — Ngừng hoạt động phân hiệu / Deactivate Campus**  
`BE + Web` · 1 MD · dep: 4.2.2  
Danh mục đã có dữ liệu tham chiếu thì ngừng hoạt động, không xóa. Bị chặn nếu phân hiệu còn lớp đang hoạt động (BR-07).

### SCH-02 Department & Subject (Tổ chuyên môn & Môn học)

**4.2.5 — Xem danh mục tổ chuyên môn và môn học / View Department and Subject Catalog**  
`BE + Web` · 1 MD · dep: 4.1.4  
Xem danh sách tổ, các môn thuộc từng tổ và viết tắt của mỗi môn.

**4.2.6 — Tạo tổ chuyên môn / Create Department**  
`BE + Web` · 0.5 MD · dep: 4.2.5  
Khai tổ chuyên môn mới với mã và tên duy nhất.

**4.2.7 — Cập nhật tổ chuyên môn / Update Department**  
`BE + Web` · 0.5 MD · dep: 4.2.6  
Sửa tên và mô tả tổ chuyên môn.

**4.2.8 — Ngừng hoạt động tổ chuyên môn / Deactivate Department**  
`BE + Web` · 0.5 MD · dep: 4.2.6  
Ngừng dùng tổ thay vì xóa khi tổ đã có môn hoặc trách nhiệm tổ trưởng tham chiếu (BR-07).

**4.2.9 — Tạo môn học kèm viết tắt / Create Subject with Short Code**  
`BE + Web` · 1 MD · dep: 4.2.6  
Khai môn học thuộc một tổ, kèm viết tắt duy nhất toàn trường CHỈ GỒM CHỮ VÀ SỐ — vì dấu gạch ngang là dấu tách trong ô MÔN-GV của file thời khóa biểu (BR-07, BR-12).

**4.2.10 — Cập nhật môn học / Update Subject**  
`BE + Web` · 1 MD · dep: 4.2.9  
Sửa tên môn, tổ phụ trách và viết tắt. Đổi viết tắt chỉ ảnh hưởng các lần import sau, không sửa dữ liệu đã có.

**4.2.11 — Ngừng hoạt động môn học / Deactivate Subject**  
`BE + Web` · 0.5 MD · dep: 4.2.9  
Ngừng dùng môn khi đã có bài tập, phụ trách chuyên môn hay thời khóa biểu tham chiếu tới nó (BR-07).

### SCH-03 Academic Year, Semester & Non-teaching Days (Năm học, Học kỳ & Ngày nghỉ)

**4.2.12 — Xem danh sách năm học / View Academic Years**  
`BE + Web` · 0.5 MD · dep: 4.1.4  
Xem các năm học đã khai, khoảng thời gian và năm nào đang là năm hiện hành.

**4.2.13 — Tạo năm học / Create Academic Year**  
`BE + Web` · 1 MD · dep: 4.2.12  
Khai năm học với nhãn, ngày bắt đầu và ngày kết thúc. Ngày bắt đầu là ngày hiệu lực mặc định của ghi danh khi import học sinh (BR-08, BR-11).

**4.2.14 — Đặt năm học hiện hành / Set Current Academic Year**  
`BE + Web` · 0.5 MD · dep: 4.2.13  
Đúng một năm học được đánh dấu hiện hành tại một thời điểm; các màn hình vận hành mặc định lọc theo năm này (BR-08).

**4.2.15 — Khai báo hai học kỳ của năm học / Configure Two Semesters**  
`BE + Web` · 1.5 MD · dep: 4.2.13  
Mỗi năm có đúng hai học kỳ, nằm trong năm học, không chồng nhau, kỳ 1 trước kỳ 2. Học kỳ chỉ là mốc lọc và mốc Phát hành; không có quy trình chuyển kỳ (BR-08).

**4.2.16 — Cập nhật học kỳ / Update Semester**  
`BE + Web` · 0.5 MD · dep: 4.2.15  
Sửa ngày bắt đầu và kết thúc của học kỳ trong giới hạn của năm học.

**4.2.17 — Khai báo ngày nghỉ theo kế hoạch / Declare Planned Non-teaching Day**  
`BE + Web` · 1 MD · dep: 4.2.13  
Khai ngày nghỉ gắn năm học, cấp toàn trường hoặc cấp một phân hiệu. Khai trước khi Phát hành thì Phát hành bỏ qua ngày đó; khai sau thì theo luồng ngày nghỉ đột xuất (BR-08, BR-15).

**4.2.18 — Xem lịch ngày nghỉ theo phân hiệu / View Non-teaching Day Calendar**  
`BE + Web` · 1 MD · dep: 4.2.17  
Xem ngày nghỉ dạng lịch tháng, lọc theo phân hiệu và năm học.

**4.2.19 — Xóa ngày nghỉ theo kế hoạch / Delete Planned Non-teaching Day**  
`BE + Web` · 0.5 MD · dep: 4.2.17  
Xóa ngày nghỉ chưa dùng tới. Xóa KHÔNG khôi phục tiết đã bị hủy trước đó; muốn dạy lại thì thêm tiết bù (BR-15).

**4.2.20 — Xác định ngày học của phân hiệu / Resolve School Day**  
`BE` · 1 MD · dep: 4.2.15, 4.2.17  
Quy tắc dùng chung trả lời một ngày có phải ngày dạy của phân hiệu không: nằm trong một học kỳ và không trùng ngày nghỉ của toàn trường hay của phân hiệu đó. Mọi chỗ sinh tiết và thêm tiết bù đều gọi quy tắc này (mục 6.2).

### SCH-04 Period Slots (Khung giờ tiết)

**4.2.21 — Xem khung giờ tiết / View Period Slots**  
`BE + Web` · 0.5 MD · dep: 4.1.4  
Xem bảng khung giờ dùng chung toàn trường theo buổi Sáng và buổi Chiều.

**4.2.22 — Tạo khung giờ tiết / Create Period Slot**  
`BE + Web` · 1 MD · dep: 4.2.21  
Khai một khung giờ, khóa là cặp (Buổi, Tiết) — Sáng tiết 1 và Chiều tiết 1 là hai khung khác nhau. Số khung mỗi buổi do trường tự khai, không cố định (BR-07).

**4.2.23 — Cập nhật giờ của khung tiết / Update Period Slot Times**  
`BE + Web` · 1 MD · dep: 4.2.22  
Sửa giờ bắt đầu và kết thúc của khung giờ.

**4.2.24 — Ngừng dùng khung giờ tiết / Deactivate Period Slot**  
`BE + Web` · 0.5 MD · dep: 4.2.22  
Không bỏ được khung đang có tiết học tham chiếu; mọi tiết kể cả tiết bù phải nằm đúng một khung (BR-07).

### SCH-05 Class & Enrollment (Lớp & Ghi danh)

**4.2.25 — Xem danh sách lớp theo năm và phân hiệu / View Class List**  
`BE + Web` · 1 MD · dep: 4.2.2, 4.2.13  
Xem các lớp của một năm học tại một phân hiệu cùng khối, sĩ số hiện tại và chủ nhiệm.

**4.2.26 — Tạo lớp / Create Class**  
`BE + Web` · 1 MD · dep: 4.2.25  
Khai lớp thuộc một phân hiệu và một năm học, khối 6–9, tên lớp duy nhất trong phạm vi đó (BR-07).

**4.2.27 — Cập nhật lớp / Update Class**  
`BE + Web` · 0.5 MD · dep: 4.2.26  
Sửa tên lớp và khối trong cùng phân hiệu và năm học.

**4.2.28 — Ngừng hoạt động lớp / Deactivate Class**  
`BE + Web` · 0.5 MD · dep: 4.2.26  
Ngừng dùng lớp mà giữ nguyên dữ liệu đã phát sinh. Bị chặn nếu lớp còn ghi danh đang mở (BR-07).

**4.2.29 — Xem danh sách học sinh của lớp / View Class Roster**  
`BE + Web` · 1 MD · dep: 4.2.26  
Xem học sinh có ghi danh hiệu lực của lớp tại ngày đang xem, kèm ngày bắt đầu ghi danh.

**4.2.30 — Ghi danh học sinh vào lớp / Enroll Student into Class**  
`BE + Web` · 1.5 MD · dep: 4.2.29  
Thêm học sinh vào lớp với ngày hiệu lực chọn tay. Mỗi học sinh chỉ có MỘT ghi danh đang mở trong một năm học; để trống ngày kết thúc nghĩa là tới hết năm học (BR-09).

**4.2.31 — Chuyển lớp giữa năm học / Transfer Student Between Classes**  
`BE + Web` · 2 MD · dep: 4.2.30  
Một thao tác, một transaction: đặt ngày kết thúc của ghi danh cũ là NGÀY LIỀN TRƯỚC ngày hiệu lực của ghi danh mới rồi mở bản mới. Hai khoảng không chạm nhau ngày nào; dữ liệu đã phát sinh ở lớp cũ giữ nguyên (BR-09).

**4.2.32 — Kết thúc ghi danh của học sinh / Close Student Enrollment**  
`BE + Web` · 1 MD · dep: 4.2.30  
Đóng ghi danh đang mở kèm lý do chuyển lớp, chuyển trường hoặc tốt nghiệp.

**4.2.33 — Lấy danh sách học sinh hiệu lực tại một ngày / Resolve Roster at Date**  
`BE` · 1 MD · dep: 4.2.30  
Quy tắc dùng chung trả về học sinh đang học của lớp tại một ngày cụ thể: ngày bắt đầu ≤ D và (ngày kết thúc trống hoặc ≥ D). Dùng cho điểm danh, giao bài và sổ liên lạc (mục 6.2).

**4.2.34 — Kiểm thử khoảng hiệu lực ghi danh / Enrollment Validity Tests**  
`QA` · 1.5 MD · dep: 4.2.30, 4.2.31  
Không có hai ghi danh mở cùng lúc; chuyển lớp không để hở hay chồng ngày; điểm danh và sổ liên lạc của ngày cũ vẫn đọc đúng lớp cũ.

### SCH-06 Year Rollover (Kết chuyển năm học)

**4.2.35 — Gợi ý danh sách lớp năm học mới / Suggest Classes for New Year**  
`BE + Web` · 2 MD · dep: 4.2.26  
Hệ thống dựng danh sách lớp năm mới từ năm cũ, gợi ý tên và khối tăng 1. Người dùng sửa tên hoặc bỏ bớt lớp ngay trên màn hình gợi ý trước khi tạo (BR-09).

**4.2.36 — Tạo lớp của năm mới từ danh sách gợi ý / Create Classes from Suggestion**  
`BE + Web` · 1 MD · dep: 4.2.35  
Tạo lớp đã chốt cho năm mới. KHÔNG sao chép thời khóa biểu, phụ trách chuyên môn hay trách nhiệm — những thứ đó khai lại cho năm mới.

**4.2.37 — Chuyển nguyên lớp sang năm học mới / Move Entire Class to New Year**  
`BE + Web` · 2 MD · dep: 4.2.36  
Chuyển toàn bộ học sinh của một lớp năm cũ sang một lớp năm mới trong một transaction. Hệ thống không tự xét lên lớp hay lưu ban; học sinh đã có ghi danh mở ở năm mới thì bỏ qua nên chạy lại không tạo trùng (BR-09).

**4.2.38 — Kiểm thử kết chuyển năm học / Rollover Tests**  
`QA` · 1 MD · dep: 4.2.37  
Chạy lại không sinh ghi danh trùng; dữ liệu năm cũ không bị đụng tới; lớp năm mới không mang theo phụ trách chuyên môn và trách nhiệm của năm cũ.

### SCH-07 School Profile (Thông tin trường)

**4.2.39 — Xem thông tin trường / View School Profile**
`BE + Web` · 1 MD · dep: 4.1.4
Xem tên, địa chỉ, điện thoại, email, website và logo của trường. Chưa lưu lần nào thì giao diện dùng tên mặc định MSMS. Tên và logo là thông tin công khai nên đọc được cả khi chưa đăng nhập (SCH-07).

**4.2.40 — Cập nhật thông tin trường / Update School Profile**
`BE + Web` · 1.5 MD · dep: 4.2.39
Hiệu trưởng và School Admin cùng sửa. Tên trường bắt buộc; email và website kiểm định dạng. Đúng một bản, lưu người sửa và thời điểm sửa ngay trên bản ghi; không ghi Audit vì không thuộc ba nhóm của BR-28.

**4.2.41 — Tải và gỡ logo trường / Upload and Remove School Logo**
`BE + Web` · 2 MD · dep: 4.2.40
Một ảnh JPG hoặc PNG, tối đa 2 MB, kiểm theo nội dung file chứ không tin đuôi file. Dùng chung cách lưu file với ảnh đại diện và ảnh bảng tin; đổi hoặc gỡ logo thì xóa file cũ (BR-27).

**4.2.42 — Hiển thị tên và logo trường ở đăng nhập và thanh bên / Show School Identity**
`BE + Web` · 1 MD · dep: 4.2.39
Màn hình đăng nhập, đầu thanh bên và tiêu đề tab trình duyệt hiện tên và logo trường. Không lấy được thì dùng biểu tượng và tên mặc định, không báo lỗi.

**4.2.43 — Kiểm thử thông tin trường / School Profile Tests**
`QA` · 1.5 MD · dep: 4.2.41
Chỉ Hiệu trưởng và School Admin sửa được; file không phải ảnh hoặc quá 2 MB bị từ chối; đổi logo xóa file cũ; chưa có tên thì không tải được logo; người chưa đăng nhập đọc được tên và logo nhưng không sửa được.

---

## 4.3 PPL — People & Assignment (Hồ sơ & Phụ trách)

### PPL-01 Staff Directory (Danh bạ cán bộ)

**4.3.1 — Sinh mã hồ sơ và tên đăng nhập / Generate Profile Code and Login Name**  
`BE` · 2 MD  
Hệ thống sinh mã hồ sơ khi tạo hồ sơ: tiền tố HS / CB / PH cộng 6 chữ số lấy từ MỘT bộ đếm chung cho cả ba loại, nên không hồ sơ nào trùng phần số. Tên đăng nhập = tên + chữ cái đầu họ và đệm + tiền tố + phần số, bỏ dấu, chữ thường — Trần Đức Kiên mã HS186194 thành kientdhs186194. Không nhập tay, không sửa, không cần cơ chế hậu tố (BR-03).

**4.3.2 — Xem danh bạ cán bộ / View Staff Directory**  
`BE + Web` · 1.5 MD · dep: 4.1.4  
Xem hồ sơ cán bộ cùng phân hiệu chính, tổ chuyên môn, viết tắt giáo viên và loại tài khoản.

**4.3.3 — Tìm kiếm và lọc cán bộ / Search and Filter Staff**  
`BE + Web` · 1 MD · dep: 4.3.2  
Lọc theo phân hiệu chính, tổ chuyên môn, loại tài khoản và tình trạng có viết tắt hay chưa.

**4.3.4 — Tạo hồ sơ cán bộ / Create Staff Profile**  
`BE + Web` · 1.5 MD · dep: 4.3.1, 4.3.2  
Khai hồ sơ cán bộ với Họ tên, Ngày sinh, Số định danh cá nhân (12 chữ số, duy nhất, không sửa sau khi tạo) và Phân hiệu chính bắt buộc; hệ thống sinh mã hồ sơ. Số định danh là khóa nhận diện khi import (BR-10).

**4.3.5 — Cập nhật hồ sơ cán bộ / Update Staff Profile**  
`BE + Web` · 1 MD · dep: 4.3.4  
Sửa thông tin liên hệ, tổ chuyên môn, phân hiệu chính và chức danh. Import không bao giờ cập nhật hồ sơ cũ, mọi sửa đổi làm ở đây (BR-11).

**4.3.6 — Gán viết tắt giáo viên / Assign Teacher Short Code**  
`BE + Web` · 1 MD · dep: 4.3.4  
Gán viết tắt duy nhất toàn trường, chỉ gồm chữ và số. Không có viết tắt thì không xuất hiện được trong file thời khóa biểu (BR-10, BR-12).

**4.3.7 — Ngừng hoạt động hồ sơ cán bộ / Deactivate Staff Profile**  
`BE + Web` · 1 MD · dep: 4.3.4  
Bị chặn nếu người đó còn trách nhiệm, phụ trách chuyên môn hay tiết dạy sắp tới chưa chuyển giao.

**4.3.8 — Tải file mẫu import cán bộ / Download Staff Import Template**  
`BE + Web` · 0.5 MD  
Tải mẫu Excel cố định của danh bạ cán bộ. Mẫu này không được thêm cột, đổi tên cột hay đổi thứ tự cột (BR-11).

**4.3.9 — Import cán bộ từ file Excel / Import Staff from Excel**  
`BE + Web` · 2 MD · dep: 4.3.4, 4.3.8  
Import một bước: gửi file, hệ thống validate TOÀN BỘ file rồi ghi, tất cả trong một transaction. Có bất kỳ lỗi nào thì không ghi gì. Số định danh là cột bắt buộc, 12 chữ số, duy nhất toàn trường. Người đã tồn tại (trùng Số định danh) thì bỏ qua cả dòng, không cập nhật hồ sơ cũ (BR-10, BR-11).

**4.3.10 — Xem bảng lỗi và kết quả import cán bộ / View Staff Import Result**  
`BE + Web` · 1.5 MD · dep: 4.3.9  
Bảng lỗi kèm số dòng, cột và giá trị gốc; mục Đã có, bỏ qua kèm mã hồ sơ đã tồn tại; số hồ sơ đã tạo khi thành công (BR-11).

**4.3.11 — Kiểm thử import cán bộ / Staff Import Tests**  
`QA` · 1.5 MD · dep: 4.3.9  
Một ô sai là không ghi gì; thiếu hoặc sai định dạng số định danh là lỗi; hai dòng trùng số định danh là lỗi; hai người trùng họ tên và ngày sinh nhưng khác số định danh vẫn thành hai hồ sơ; chạy lại cùng một file không tạo bản trùng; viết tắt trùng bị chặn.

### PPL-02 Student Directory (Danh bạ học sinh)

**4.3.12 — Xem danh bạ học sinh / View Student Directory**  
`BE + Web` · 1.5 MD · dep: 4.1.4  
Xem hồ sơ học sinh cùng lớp đang học, phân hiệu và trạng thái Đang học / Đã chuyển trường / Đã tốt nghiệp.

**4.3.13 — Tìm kiếm và lọc học sinh / Search and Filter Students**  
`BE + Web` · 1 MD · dep: 4.3.12  
Lọc theo năm học, phân hiệu, lớp và trạng thái; tìm theo tên hoặc mã hồ sơ.

**4.3.14 — Tạo hồ sơ học sinh / Create Student Profile**  
`BE + Web` · 1.5 MD · dep: 4.3.12  
Khai hồ sơ học sinh trên giao diện với Họ tên, Ngày sinh và Số định danh cá nhân (12 chữ số, duy nhất toàn trường, không sửa sau khi tạo) bắt buộc; hệ thống sinh mã hồ sơ (BR-03, BR-10).

**4.3.15 — Cập nhật hồ sơ học sinh / Update Student Profile**  
`BE + Web` · 1 MD · dep: 4.3.14  
Sửa thông tin học sinh trên giao diện; số định danh không sửa được. Import không bao giờ cập nhật hồ sơ đã có (BR-10, BR-11).

**4.3.16 — Tải file mẫu import học sinh / Download Student Import Template**  
`BE + Web` · 0.5 MD  
Tải mẫu Excel cố định của danh bạ học sinh, gồm hai ô meta Năm học và Phân hiệu và cột bắt buộc Số định danh (BR-11).

**4.3.17 — Import học sinh kèm ghi danh và phụ huynh / Import Students with Enrollment and Guardian**  
`BE + Web` · 2.5 MD · dep: 4.3.14, 4.3.16, 4.2.30, 4.3.23  
Một dòng = một học sinh kèm phụ huynh đại diện, nhận diện học sinh theo Số định danh (cột bắt buộc), tạo tối đa bốn thứ trong một transaction: hồ sơ học sinh, ghi danh (chỉ khi cột Tên lớp có giá trị, hiệu lực từ ngày bắt đầu năm học), hồ sơ phụ huynh và liên kết. Các dòng cùng số điện thoại dùng chung một hồ sơ phụ huynh. Hệ thống không tự tạo lớp (BR-11).

**4.3.18 — Xem bảng lỗi và kết quả import học sinh / View Student Import Result**  
`BE + Web` · 1.5 MD · dep: 4.3.17  
Bảng lỗi kèm số dòng, cột, giá trị gốc — tên lớp không khớp, cùng số điện thoại mà họ tên phụ huynh khác nhau, thiếu hoặc trùng số định danh trong file; mục Đã có, bỏ qua kèm mã hồ sơ đã tồn tại; số học sinh, ghi danh và phụ huynh đã tạo (BR-11).

**4.3.19 — Chuyển trường cho học sinh / Transfer Student Out**  
`BE + Web` · 1.5 MD · dep: 4.3.14  
Đóng ghi danh đang mở, đổi trạng thái sang Đã chuyển trường và khóa tài khoản học sinh trong cùng thao tác; làm được cho một hoặc nhiều học sinh (BR-05).

**4.3.20 — Tốt nghiệp học sinh / Graduate Student**  
`BE + Web` · 1 MD · dep: 4.3.19  
Giống chuyển trường nhưng đổi trạng thái sang Đã tốt nghiệp; dùng cho khối 9 cuối năm, chọn nhiều học sinh một lượt (BR-05).

**4.3.21 — Kiểm thử import học sinh / Student Import Tests**  
`QA` · 2 MD · dep: 4.3.17  
Lớp không tồn tại là lỗi; thiếu hoặc trùng số định danh là lỗi; học sinh đã có (trùng số định danh) bị bỏ qua cả dòng kể cả khi đổi lớp; hai học sinh trùng họ tên và ngày sinh nhưng khác số định danh thành hai hồ sơ; nhiều con cùng một số điện thoại ra một hồ sơ phụ huynh; chạy lại cùng file không tạo trùng.

### PPL-03 Parent Directory & Linking (Danh bạ phụ huynh & Liên kết)

**4.3.22 — Xem danh bạ phụ huynh / View Guardian Directory**  
`BE + Web` · 1.5 MD · dep: 4.1.4  
Xem hồ sơ phụ huynh cùng số điện thoại và các con đang được liên kết.

**4.3.23 — Tạo hồ sơ phụ huynh / Create Guardian Profile**  
`BE + Web` · 1 MD · dep: 4.3.22  
Khai hồ sơ phụ huynh trên giao diện. Số điện thoại bắt buộc và duy nhất vì đó là khóa nhận diện phụ huynh khi import (BR-10, BR-11).

**4.3.24 — Cập nhật hồ sơ phụ huynh / Update Guardian Profile**  
`BE + Web` · 1 MD · dep: 4.3.23  
Sửa họ tên, nghề nghiệp, địa chỉ và số điện thoại; số mới vẫn phải duy nhất.

**4.3.25 — Xem các con được liên kết / View Linked Students**  
`BE + Web` · 0.5 MD · dep: 4.3.23  
Từ một phụ huynh xem danh sách con, lớp và phân hiệu của từng con. Một phụ huynh liên kết được nhiều học sinh (BR-10).

**4.3.26 — Đổi phụ huynh đại diện của học sinh / Change Representative Guardian**  
`BE + Web` · 1.5 MD · dep: 4.3.23  
Chuyển liên kết của một học sinh sang phụ huynh khác, có sẵn hoặc tạo mới. Mỗi học sinh có đúng một phụ huynh đại diện; hội thoại của phụ huynh cũ trở thành chỉ đọc (BR-10, BR-25).

### PPL-04 Responsibility (Trách nhiệm)

**4.3.27 — Xem trách nhiệm đang hiệu lực / View Active Responsibilities**  
`BE + Web` · 1.5 MD · dep: 4.1.4  
Ba nhóm: phụ trách phân hiệu, tổ trưởng và chủ nhiệm lớp. Mỗi đích tối đa một bản đang hiệu lực (BR-09).

**4.3.28 — Gán phụ trách phân hiệu / Assign Campus Responsibility**  
`BE + Web` · 1 MD · dep: 4.3.27  
Principal hoặc School Admin gán một Vice Principal phụ trách một phân hiệu, kèm ngày bắt đầu. Chỉ gán được cho loại tài khoản Vice Principal (BR-09).

**4.3.29 — Gán tổ trưởng / Assign Department Head**  
`BE + Web` · 1 MD · dep: 4.3.27  
Gán một Teacher làm tổ trưởng của một tổ chuyên môn, kèm ngày bắt đầu. Tổ trưởng là trách nhiệm, không phải loại tài khoản (BR-09).

**4.3.30 — Gán chủ nhiệm lớp / Assign Homeroom Teacher**  
`BE + Web` · 1 MD · dep: 4.3.27  
Gán một Teacher làm chủ nhiệm của một lớp, kèm ngày bắt đầu (BR-09).

**4.3.31 — Thay người đảm nhiệm trách nhiệm / Replace Responsibility Holder**  
`BE + Web` · 1.5 MD · dep: 4.3.28  
Một thao tác, một transaction: ngày kết thúc của bản cũ là NGÀY LIỀN TRƯỚC ngày hiệu lực của bản mới, nên không ngày nào có hai người cùng hiệu lực. Bản cũ giữ nguyên làm lịch sử (BR-09).

**4.3.32 — Kết thúc trách nhiệm / End Responsibility**  
`BE + Web` · 0.5 MD · dep: 4.3.28  
Đặt ngày kết thúc là ngày thao tác cho một trách nhiệm; quyền tương ứng mất ngay tại thời điểm thao tác, không đợi hết ngày; loại tài khoản không đổi. Gán lại trong cùng ngày tạo bản mới bắt đầu từ hôm nay (BR-09).

**4.3.33 — Xem lịch sử trách nhiệm theo đích / View Responsibility History**  
`BE + Web` · 1 MD · dep: 4.3.28  
Xem những ai đã từng phụ trách một phân hiệu, một tổ hay một lớp và trong khoảng thời gian nào.

**4.3.34 — Cảnh báo lớp chưa có chủ nhiệm / View Classes Without Homeroom Teacher**  
`BE + Web` · 1 MD · dep: 4.3.30  
Liệt kê lớp của năm hiện hành không có trách nhiệm chủ nhiệm đang hiệu lực — đó là lớp mà đơn xin nghỉ nằm chờ tới khi có chủ nhiệm được gán (BR-19, mục 5.1).

**4.3.35 — Kiểm thử phạm vi theo trách nhiệm / Responsibility Scope Tests**  
`QA` · 1.5 MD · dep: 4.3.28, 4.3.30  
Hết chủ nhiệm thì mất quyền duyệt đơn và xem sổ lớp ngay; chủ nhiệm mới thấy đơn đang chờ; gán trách nhiệm cho sai loại tài khoản bị chặn.

### PPL-05 Teaching Assignment (Phụ trách chuyên môn)

**4.3.36 — Xem lưới phụ trách chuyên môn / View Teaching Assignment Grid**  
`BE + Web` · 2 MD · dep: 4.2.26, 4.2.9  
Lưới lớp theo môn của một phân hiệu trong một năm học, mỗi ô hiển thị giáo viên đang phụ trách.

**4.3.37 — Gán giáo viên phụ trách môn ở lớp / Assign Teacher to Class and Subject**  
`BE + Web` · 2 MD · dep: 4.3.36  
Vice Principal gán phụ trách chuyên môn cho lớp thuộc phân hiệu mình nhưng chọn giáo viên từ danh sách TOÀN TRƯỜNG, không cần xác nhận chéo. Mỗi (lớp, môn, năm học) có một giáo viên phụ trách chuyên môn đang hiệu lực; người nhận phải có tài khoản Teacher không bị khóa (BR-06, BR-09).

**4.3.38 — Xem nhanh lịch dạy của giáo viên khi chọn / Preview Teacher Schedule While Assigning**  
`BE + Web` · 1 MD · dep: 4.3.37  
Khi chọn giáo viên, xem nhanh lịch dạy hiện có của người đó ở mọi phân hiệu để tránh dồn tải.

**4.3.39 — Thay giáo viên đang phụ trách chuyên môn / Replace Assigned Teacher**  
`BE + Web` · 1.5 MD · dep: 4.3.37  
Kết thúc bản phụ trách cũ ở ngày liền trước và mở bản mới. Quyền với bài tập của (lớp, môn) luôn theo phụ trách chuyên môn đang hiệu lực, không theo người tạo: người mới tiếp quản, người cũ mất quyền ngay (BR-09).

**4.3.40 — Kết thúc phụ trách chuyên môn / End Teaching Assignment**  
`BE + Web` · 0.5 MD · dep: 4.3.37  
Đặt ngày kết thúc là ngày thao tác cho phụ trách chuyên môn; quyền với bài tập của (lớp, môn) mất ngay tại thời điểm thao tác, không đợi hết ngày, cùng quy tắc với 4.3.32 (kết thúc trách nhiệm). Gán lại trong cùng ngày tạo bản mới bắt đầu từ hôm nay. Dữ liệu bài tập đã phát sinh giữ nguyên (BR-09).

**4.3.41 — Kiểm thử chuyển quyền theo phụ trách chuyên môn / Assignment Handover Tests**  
`QA` · 1.5 MD · dep: 4.3.39  
Giáo viên mới công bố được điểm bài do người cũ giao; người cũ mất quyền ngay tại thời điểm thay người hoặc kết thúc (không đợi hết ngày); phụ trách chuyên môn không ảnh hưởng quyền điểm danh (quyền đó theo tiết).

---

## 4.4 SCD — Timetable & Teaching Schedule (Thời khóa biểu & Lịch dạy)

### SCD-01 Timetable Import & Publish (Import & Phát hành thời khóa biểu)

**4.4.1 — Tải mẫu thời khóa biểu sinh theo cấu hình / Download Generated Timetable Template**  
`BE + Web` · 2 MD · dep: 4.2.22, 4.2.26  
Vice Principal chọn phân hiệu và học kỳ, Backend dựng file ngay lúc đó: số hàng theo khung giờ đã khai, số cột theo lớp của (phân hiệu, năm học), ba ô meta điền sẵn, và NỘI DUNG ĐIỀN SẴN theo phiên bản Đã phát hành gần nhất. Nhờ vậy sửa lịch vài lớp là tải về, sửa vài ô, tải lên (BR-12).

**4.4.2 — Tải lên file thời khóa biểu / Upload Timetable File**  
`BE + Web` · 2.5 MD · dep: 4.4.1  
Một file phủ ĐỦ MỌI LỚP của phân hiệu trong một học kỳ. Hàng là Thứ × Buổi × Tiết (Thứ 2–7), cột là lớp đọc từ cột D sang phải tới ô tiêu đề trống đầu tiên; mỗi ô ghi MÔN-GV, ô trống là tiết trống. Thiếu lớp nào là lỗi (BR-12).

**4.4.3 — Đối chiếu viết tắt môn và giáo viên / Resolve Subject and Teacher Short Codes**  
`BE` · 1.5 MD · dep: 4.4.2  
Tách ô theo dấu gạch ngang ĐẦU TIÊN, đối chiếu không phân biệt hoa thường và bỏ khoảng trắng thừa. Giáo viên phải có viết tắt và tài khoản Teacher không bị khóa — Chờ đổi mật khẩu vẫn hợp lệ (BR-06, BR-12).

**4.4.4 — Xem bảng lỗi theo ô của file / View Cell-level Import Errors**  
`BE + Web` · 2 MD · dep: 4.4.3  
Mọi lỗi kèm địa chỉ ô và giá trị gốc: viết tắt không khớp, giáo viên không đủ điều kiện dạy, tên lớp không thuộc phạm vi, hai cột cùng tên lớp, thiếu lớp, hàng tiêu đề bị sửa, (Buổi, Tiết) không có trong khung giờ, một giáo viên ở hai lớp cùng (Thứ, Buổi, Tiết). Có một lỗi là từ chối CẢ FILE (BR-12).

**4.4.5 — Lưu file hợp lệ thành phiên bản nháp / Save Draft Timetable Version**  
`BE` · 1 MD · dep: 4.4.4  
File hợp lệ được lưu thành một phiên bản Nháp của (phân hiệu, học kỳ). Mỗi phiên bản là hồ sơ của một lần import (BR-12, BR-14).

**4.4.6 — Xem lưới phiên bản nháp / View Draft Timetable Version**  
`BE + Web` · 1.5 MD · dep: 4.4.5  
Xem lại nội dung đã import dưới dạng lưới trước khi chạy xem trước.

**4.4.7 — Xem danh sách phiên bản thời khóa biểu / View Timetable Versions**  
`BE + Web` · 1 MD · dep: 4.4.5  
Xem các phiên bản Nháp và Đã phát hành của một phân hiệu và học kỳ, đọc lại được như lịch sử. Phát hành phiên bản mới không đổi trạng thái phiên bản trước (BR-14).

**4.4.8 — Kiểm xung đột lịch toàn trường / Check School-wide Schedule Conflicts**  
`BE` · 2 MD · dep: 4.4.5  
Quy tắc dùng chung: trùng lịch là cùng NGÀY và cùng KHUNG GIỜ. Mỗi lớp có tối đa một tiết Đã xếp lịch hoặc Chờ xử lý, và mỗi giáo viên có tối đa một tiết Đã xếp lịch (tiết Chờ xử lý để trống giáo viên), tại một (ngày, khung giờ), kiểm trên toàn trường vì giáo viên dạy nhiều phân hiệu (BR-13).

**4.4.9 — Xem trước kết quả phát hành / Preview Publish Result**  
`BE + Web` · 2 MD · dep: 4.4.8  
Hiển thị số tiết sẽ xóa và sẽ tạo, nhóm theo lớp và ngày, kèm CẢNH BÁO số tiết Chỉnh tay, tiết bù và tiết Chờ xử lý sẽ bị xóa — người dùng phải xác nhận. Xem trước không được lưu lại; lúc bấm Phát hành hệ thống tính lại (BR-14).

**4.4.10 — Xem danh sách xung đột giáo viên / View Teacher Conflicts**  
`BE + Web` · 1.5 MD · dep: 4.4.8  
Mỗi dòng xung đột ghi rõ tiết đang chiếm chỗ thuộc phân hiệu nào và ai là Vice Principal phụ trách phân hiệu đó, vì xung đột liên phân hiệu phải do Vice Principal phía kia sửa. Còn xung đột là CHẶN Phát hành (BR-14).

**4.4.11 — Phát hành thời khóa biểu / Publish Timetable**  
`BE + Web` · 2.5 MD · dep: 4.4.9, 4.4.10  
Một transaction, hai bước: xóa mọi tiết của phân hiệu trong học kỳ TỪ NGÀY MAI TRỞ ĐI — ghi đè sạch, không giữ tiết đã chỉnh tay — rồi sinh lại từ file trong khoảng max(ngày mai, ngày bắt đầu học kỳ) đến hết học kỳ, bỏ qua ngày nghỉ. Tiết đã qua và tiết đã có điểm danh không bao giờ bị đụng tới (BR-14).

**4.4.12 — Phát hành lại thời khóa biểu / Republish Timetable**  
`BE + Web` · 1.5 MD · dep: 4.4.11  
Import file mới cho cùng (phân hiệu, học kỳ) rồi Phát hành lần nữa; chạy đúng hai bước trên. Nguồn sự thật của thời khóa biểu hiện hành là LỊCH DẠY, không phải phiên bản (BR-14).

**4.4.13 — Gửi thông báo gộp sau khi sinh lại tiết / Notify Affected Teachers**  
`BE` · 1.5 MD · dep: 4.4.11  
Sau khi sinh lại, gửi MỘT thông báo gộp cho mỗi giáo viên có tiết thực sự thay đổi; giáo viên có lịch không đổi thì không nhận thông báo. Ghi Audit kèm số tiết xóa và tạo (BR-14, BR-26, BR-28).

**4.4.14 — Xem lưới thời khóa biểu của phân hiệu / View Campus Timetable Grid**  
`BE + Web` · 2 MD · dep: 4.4.11  
Xem lịch hiện hành của phân hiệu dạng lưới, đọc từ lịch dạy nên phản ánh cả các điều chỉnh tay (BR-14).

**4.4.15 — Xem lịch dạy của mình / View My Teaching Schedule**  
`BE + Web + Mobile` · 2 MD · dep: 4.4.11  
Giáo viên xem lịch dạy theo ngày và theo tuần, gồm tiết bù và tiết được phân dạy thay; tiết Chờ xử lý và Đã hủy hiện rõ trạng thái.

**4.4.16 — Xem lịch học của lớp / View Class Schedule**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.4.11  
Học sinh xem lịch học của lớp mình theo ngày và theo tuần.

**4.4.17 — Xem lịch học của con / View Child Schedule**  
`BE + Web + Mobile` · 1 MD · dep: 4.4.16  
Phụ huynh xem lịch học của từng con được liên kết.

**4.4.18 — Kiểm thử phát hành và phát hành lại / Publish Tests**  
`QA` · 2.5 MD · dep: 4.4.11, 4.4.12  
Tiết đã qua và tiết đã điểm danh không bị xóa; tiết Chỉnh tay tương lai bị xóa đúng như cảnh báo; còn xung đột thì không Phát hành được; ngày nghỉ không sinh tiết; chỉ giáo viên có tiết đổi mới nhận thông báo.

### SCD-02 Schedule Adjustment (Điều chỉnh lịch dạy)

**4.4.19 — Xem lịch tuần của phân hiệu để điều chỉnh / View Weekly Campus Schedule**  
`BE + Web` · 2 MD · dep: 4.4.11  
Lưới tuần theo lớp và khung giờ của phân hiệu, mỗi ô cho mở các thao tác điều chỉnh. Tiết bị khóa hiện rõ là không thao tác được (mục 6.2).

**4.4.20 — Đổi giáo viên của tiết / Change Lesson Teacher**  
`BE + Web` · 1.5 MD · dep: 4.4.19  
Thay giáo viên của một tiết chưa bị khóa. KHÔNG kiểm phụ trách chuyên môn — phụ trách chuyên môn độc lập với thời khóa biểu — nhưng vẫn kiểm xung đột lịch (BR-13, BR-15).

**4.4.21 — Hủy tiết / Cancel Lesson**  
`BE + Web` · 1 MD · dep: 4.4.19  
Đưa tiết chưa bị khóa về Đã hủy. Tiết Đã hủy không dạy, không điểm danh, không tính chuyên cần (BR-15).

**4.4.22 — Thêm tiết bù / Add Make-up Lesson**  
`BE + Web` · 1.5 MD · dep: 4.4.19  
Tạo tiết mới ở một (ngày, khung giờ) trống; ngày phải là ngày học của phân hiệu, ≥ hôm nay và trong cùng học kỳ. Bù cho tiết đã hủy thì ghi lại tiết gốc để truy vết. Đổi giờ một tiết = hủy tiết cũ + thêm tiết bù (BR-15).

**4.4.23 — Khai ngày nghỉ đột xuất / Declare Ad-hoc Day Off**  
`BE + Web` · 2 MD · dep: 4.4.19  
Vice Principal khai cho phân hiệu mình, ngày ≥ hôm nay; nghỉ toàn trường thì Vice Principal mỗi phân hiệu khai cho phân hiệu mình. Mọi tiết Đã xếp lịch hoặc Chờ xử lý CHƯA CÓ ĐIỂM DANH của ngày đó chuyển sang Đã hủy; Phát hành sau đó cũng bỏ qua ngày này (BR-15).

**4.4.24 — Xem trước số tiết sẽ bị hủy / Preview Lessons to Be Cancelled**  
`BE + Web` · 1 MD · dep: 4.4.23  
Trước khi xác nhận ngày nghỉ đột xuất, hiển thị danh sách tiết sẽ bị hủy theo lớp và giáo viên.

**4.4.25 — Đặt nguồn tiết thành Chỉnh tay / Mark Lesson as Manually Adjusted**  
`BE` · 0.5 MD · dep: 4.4.20, 4.4.21, 4.4.22  
Mọi thao tác tay đổi nguồn tiết từ Sinh tự động sang Chỉnh tay, tạo thông báo cho giáo viên liên quan và học sinh của lớp, và ghi Audit (BR-15, BR-26, BR-28).

**4.4.26 — Kiểm thử điều chỉnh lịch / Adjustment Tests**  
`QA` · 2 MD · dep: 4.4.20, 4.4.21, 4.4.22, 4.4.23  
Không thao tác được trên tiết bị khóa; tiết bù trùng khung giờ bị chặn; xóa ngày nghỉ không khôi phục tiết đã hủy; mọi thao tác đều sinh Audit và thông báo.

### SCD-03 Teacher Absence (Xử lý giáo viên vắng)

**4.4.27 — Khai báo giáo viên vắng / Declare Teacher Absence**  
`BE + Web` · 2 MD  
Khai giáo viên, từ ngày – đến ngày, buổi (sáng / chiều / cả ngày) và lý do. Đây KHÔNG phải đơn xin nghỉ của cán bộ — MSMS chỉ ghi nhận hệ quả lên lịch dạy (BR-16).

**4.4.28 — Đưa tiết bị ảnh hưởng về Chờ xử lý / Move Affected Lessons to Pending**  
`BE` · 1.5 MD · dep: 4.4.27  
Khai báo không tự đổi lịch: mọi tiết Đã xếp lịch chưa bị khóa của giáo viên đó trong khoảng, ở MỌI PHÂN HIỆU, chuyển sang Chờ xử lý (Chỉnh tay) và để trống giáo viên; tiết trống không chiếm chỗ của giáo viên nào khi kiểm xung đột. Mỗi tiết ghi lại khai báo đã đưa nó về Chờ xử lý. Vice Principal của phân hiệu chứa tiết nhận thông báo. Khóa hoặc đổi loại tài khoản Teacher (4.1.15, 4.1.17) cũng đưa tiết về Chờ xử lý và để trống giáo viên theo cách này, nhưng không gắn với khai báo nào (BR-05, BR-13, BR-16, BR-26).

**4.4.29 — Xem danh sách tiết Chờ xử lý / View Pending Lessons**  
`BE + Web` · 1.5 MD · dep: 4.4.28  
Danh sách tiết Chờ xử lý theo phân hiệu, lọc theo ngày và lớp, mở được màn hình quyết định từng tiết.

**4.4.30 — Xem gợi ý giáo viên rảnh / View Free Teacher Suggestions**  
`BE + Web` · 2 MD · dep: 4.4.29  
Teacher đủ điều kiện dạy, không có tiết Đã xếp lịch tại (ngày, khung giờ) đó ở bất kỳ phân hiệu nào và không có khai báo vắng phủ ngày và buổi đó. Danh sách sắp theo tên, tối đa 10 người, KHÔNG xếp hạng ưu tiên — Vice Principal tự chọn (BR-06, BR-13, BR-16).

**4.4.31 — Xử lý tiết chờ bằng cách chọn giáo viên dạy thay / Resolve by Substitute Teacher**  
`BE + Web` · 1.5 MD · dep: 4.4.30  
Lựa chọn 1: gán giáo viên được chọn, tiết quay lại Đã xếp lịch. Backend kiểm lại điều kiện dạy và xung đột lịch toàn trường lúc lưu (BR-06, BR-13, BR-16).

**4.4.32 — Xử lý tiết chờ bằng cách hủy / Resolve by Cancelling**  
`BE + Web` · 0.5 MD · dep: 4.4.29  
Lựa chọn 2: tiết chuyển sang Đã hủy.

**4.4.33 — Hủy tiết chờ và tạo tiết bù / Cancel Pending and Add Make-up**  
`BE + Web` · 1.5 MD · dep: 4.4.29  
Lựa chọn 3: hủy tiết rồi tạo tiết bù ở (ngày, khung giờ) trống theo quy tắc tiết bù, ghi lại tiết gốc (BR-15, BR-16).

**4.4.34 — Xử lý tiết chờ đã quá ngày / Resolve Overdue Pending Lesson**  
`BE + Web` · 1 MD · dep: 4.4.29, 4.4.31  
Tiết Chờ xử lý đã qua ngày mà chưa quyết hiện trên Dashboard và xử lý theo cùng ba lựa chọn; nếu chọn giáo viên thì School Admin nhập bù điểm danh trên màn hình điểm danh (BR-16, BR-17).

**4.4.35 — Xóa khai báo giáo viên vắng / Delete Teacher Absence**  
`BE + Web` · 1.5 MD · dep: 4.4.28  
Xóa khai báo thì các tiết còn Chờ xử lý do khai báo đó được trả về giáo viên của khai báo, trở lại Đã xếp lịch, với điều kiện giáo viên không trùng lịch tại tiết đó và vẫn đủ điều kiện dạy (tài khoản Teacher không bị khóa, BR-06). Tiết trùng lịch hoặc giáo viên không còn đủ điều kiện giữ nguyên Chờ xử lý và được báo cho Vice Principal; tiết đã xử lý không bị đụng tới; tiết trống do khóa hoặc đổi loại tài khoản không tự khôi phục. Ghi Audit, thông báo giáo viên và học sinh của lớp (BR-06, BR-13, BR-16, BR-26, BR-28).

**4.4.36 — Xem lịch dạy của mọi giáo viên toàn trường / View School-wide Teacher Calendar**  
`BE + Web` · 1.5 MD · dep: 4.4.14  
Vice Principal xem lịch dạy của mọi giáo viên ở mọi phân hiệu để chọn người khi gán phụ trách chuyên môn hoặc dạy thay (mục 2.3).

**4.4.37 — Kiểm thử xử lý giáo viên vắng / Absence Tests**  
`QA` · 2 MD · dep: 4.4.28, 4.4.31, 4.4.35  
Tiết Chờ xử lý để trống giáo viên và không điểm danh được; tiết ở phân hiệu khác cũng bị đưa về Chờ xử lý; gợi ý không trả về người đã có tiết hoặc đang vắng; ba lựa chọn ghi đúng Audit; tiết trống không chiếm chỗ giáo viên khi kiểm xung đột; xóa khai báo trả tiết về đúng giáo viên, bỏ qua tiết đã xử lý và tiết trùng lịch, không khôi phục tiết trống do khóa tài khoản.

---

## 4.5 ATT — Attendance (Điểm danh)

### ATT-01 Period Attendance (Điểm danh theo tiết)

**4.5.1 — Xem danh sách tiết cần điểm danh hôm nay / View Today's Lessons to Mark**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.4.15  
Giáo viên xem các tiết mình dạy trong ngày hôm nay kèm tình trạng đã điểm danh hay chưa. Ngày nghiệp vụ theo giờ Việt Nam do Backend quyết định (mục 1, nguyên tắc 5).

**4.5.2 — Mở bảng điểm danh của tiết / Open Lesson Attendance Sheet**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.5.1  
Danh sách là học sinh có ghi danh hiệu lực ở lớp VÀO NGÀY CỦA TIẾT, không phải danh sách hiện tại (BR-17).

**4.5.3 — Ghi điểm danh cho tiết / Save Lesson Attendance**  
`BE + Web + Mobile` · 2 MD · dep: 4.5.2  
Giáo viên của tiết gửi một trong ba giá trị Có mặt / Muộn / Vắng kèm ghi chú tùy chọn. Quyền dựa vào TIẾT chứ không dựa vào phụ trách chuyên môn, nên giáo viên dạy thay điểm danh được. Mỗi học sinh có đúng một bản ghi cho mỗi tiết, có hiệu lực ngay, không có bước chốt (BR-17).

**4.5.4 — Sửa điểm danh trong ngày của tiết / Update Attendance Same Day**  
`BE + Web + Mobile` · 1 MD · dep: 4.5.3  
Giáo viên của tiết sửa được tới 23:59 ngày của tiết; sau đó không sửa được nữa (BR-17).

**4.5.5 — Chặn điểm danh ngoài điều kiện / Block Attendance Outside Conditions**  
`BE` · 1 MD · dep: 4.5.3  
Chỉ điểm danh trên tiết Đã xếp lịch. Tiết Chờ xử lý và tiết Đã hủy không điểm danh được; giáo viên không phải giáo viên của tiết bị chặn ở Backend (BR-16, BR-17).

**4.5.6 — Xem danh sách tiết đã qua chưa điểm danh / View Lessons Missing Attendance**  
`BE + Web` · 1.5 MD · dep: 4.5.3  
Tiết Đã xếp lịch có ngày nhỏ hơn hôm nay và chưa có bản ghi điểm danh nào. Danh sách này cũng là một chỉ số trên Dashboard của Principal và Vice Principal (mục 5.1); School Admin tìm tiết cần nhập bù bằng cách chọn lớp và khoảng ngày trên màn hình điểm danh.

**4.5.7 — School Admin nhập bù điểm danh / Backfill Attendance**  
`BE + Web` · 2 MD · dep: 4.5.6  
Sau ngày của tiết, CHỈ School Admin nhập bù, trên cùng màn hình điểm danh, cho tiết Đã xếp lịch có ngày ≤ hôm nay. Đây là việc duy nhất School Admin làm thay trên dữ liệu giảng dạy, ghi là người nhập (BR-02, BR-17).

**4.5.8 — School Admin sửa điểm danh tiết đã qua / Correct Attendance After the Day**  
`BE + Web` · 1 MD · dep: 4.5.7  
Sửa bản ghi đã có của tiết đã qua. Vice Principal thấy số liệu nhưng không sửa (mục 2.3, BR-17).

**4.5.9 — Lưu người nhập, thời điểm và cờ nhập bù / Record Attendance Trail**  
`BE` · 1 MD · dep: 4.5.7  
Mỗi bản ghi lưu người nhập, thời điểm và cờ nhập bù ngay trên bản ghi nghiệp vụ. Điểm danh không vào Audit Log (BR-17, BR-28).

**4.5.10 — Suy nhãn có phép và không phép / Derive Excused Label**  
`BE` · 1.5 MD · dep: 4.5.3  
KHÔNG LƯU, luôn tính lúc đọc: Vắng + có đơn Đã duyệt phủ ngày của tiết và đúng buổi thì là Vắng có phép, còn lại là Vắng không phép. Đơn duyệt muộn tự đổi nhãn các buổi vắng đã ghi, không cần chạy lại gì (BR-18).

**4.5.11 — Xem chuyên cần của mình / View Own Attendance**  
`BE + Web + Mobile` · 2 MD · dep: 4.5.10  
Học sinh xem chuyên cần theo tiết và theo tuần với bốn nhãn Có mặt / Muộn / Vắng có phép / Vắng không phép.

**4.5.12 — Xem chuyên cần của con / View Child Attendance**  
`BE + Web + Mobile` · 1 MD · dep: 4.5.11  
Phụ huynh xem chuyên cần của từng con được liên kết.

**4.5.13 — Xem chuyên cần lớp chủ nhiệm / View Homeroom Attendance**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.5.10  
Chủ nhiệm xem chuyên cần của lớp mình theo ngày và theo khoảng ngày.

**4.5.14 — Kiểm thử điểm danh và nhập bù / Attendance Tests**  
`QA` · 2 MD · dep: 4.5.3, 4.5.7, 4.5.10  
Giáo viên không sửa được sau ngày của tiết; School Admin nhập bù được cho tiết đã qua; danh sách học sinh lấy theo ghi danh tại ngày của tiết; đơn duyệt muộn đổi nhãn ngay; tiết Đã hủy không vào thống kê.

---

## 4.6 LRN — Learning (Học tập)

### LRN-01 Question Bank & Quiz (Ngân hàng câu hỏi & Đề)

**4.6.1 — Xem ngân hàng câu hỏi / View Question Bank**  
`BE + Web` · 1.5 MD · dep: 4.2.9  
Ngân hàng dùng chung toàn trường: mọi Teacher xem và dùng câu Sẵn dùng của mọi môn (BR-20).

**4.6.2 — Lọc câu hỏi theo môn, khối và người tạo / Filter Questions**  
`BE + Web` · 1 MD · dep: 4.6.1  
Thu hẹp ngân hàng theo môn, khối, chủ đề, người tạo và trạng thái Nháp hay Sẵn dùng.

**4.6.3 — Soạn câu hỏi trắc nghiệm / Create Question**  
`BE + Web` · 1.5 MD · dep: 4.6.1  
Câu hỏi thuộc một môn, 4 lựa chọn, đúng 1 đáp án, giải thích tùy chọn (BR-20).

**4.6.4 — Cập nhật câu hỏi của mình / Update Own Question**  
`BE + Web` · 1 MD · dep: 4.6.3  
Chỉ người tạo sửa được. Câu đã nằm trong một đề ĐÃ GIAO thì không sửa được nữa (BR-20).

**4.6.5 — Ẩn câu hỏi khỏi ngân hàng / Hide Question**  
`BE + Web` · 0.5 MD · dep: 4.6.3  
Câu đã khóa chỉ còn ẩn khỏi ngân hàng; ẩn không ảnh hưởng đề cũ. Muốn phiên bản khác thì soạn câu mới (BR-20).

**4.6.6 — Yêu cầu AI sinh nháp câu hỏi / Request AI Question Drafts**  
`BE + Web` · 2.5 MD · dep: 4.6.3  
Giáo viên chọn môn, khối, chủ đề và số câu; AI trả về các câu ở trạng thái Nháp gắn cờ AI. AI hỏng thì giáo viên soạn thủ công, không ảnh hưởng luồng nào khác (mục 3.1, BR-20).

**4.6.7 — Xem và sửa nháp AI / Review and Edit AI Drafts**  
`BE + Web` · 1.5 MD · dep: 4.6.6  
Giáo viên sửa nội dung, lựa chọn và đáp án của câu nháp trước khi xác nhận.

**4.6.8 — Xác nhận nháp AI thành câu Sẵn dùng / Confirm AI Draft**  
`BE + Web` · 1 MD · dep: 4.6.7  
Câu do AI sinh là nháp cho tới khi giáo viên xác nhận; chỉ câu Sẵn dùng mới đưa vào đề được (BR-20).

**4.6.9 — Xóa nháp AI / Delete AI Draft**  
`BE + Web` · 0.5 MD · dep: 4.6.7  
Bỏ các câu nháp không dùng.

**4.6.10 — Tạo đề từ câu hỏi Sẵn dùng / Create Quiz**  
`BE + Web` · 1.5 MD · dep: 4.6.3  
Đề gồm các câu Sẵn dùng CÙNG MỘT MÔN, soạn thủ công (4.6.3) hoặc xác nhận từ nháp AI (4.6.8); tạo đề không phụ thuộc AI. Đề thuộc về người tạo (BR-20).

**4.6.11 — Sắp thứ tự và đặt điểm từng câu / Order Questions and Set Points**  
`BE + Web` · 1.5 MD · dep: 4.6.10  
Đặt thứ tự câu và điểm từng câu; tổng điểm đề là mẫu số khi quy về thang 10 (BR-21).

**4.6.12 — Cập nhật đề chưa khóa / Update Unlocked Quiz**  
`BE + Web` · 1 MD · dep: 4.6.10  
Chỉ người tạo sửa hoặc xóa đề chưa khóa (BR-20).

**4.6.13 — Nhân bản đề / Clone Quiz**  
`BE + Web` · 1 MD · dep: 4.6.10  
Mọi Teacher nhân bản được đề của người khác; bản nhân bản thuộc về người bấm nhân bản và chưa khóa. Đây là cách giáo viên tiếp quản lớp dùng lại đề của người tiền nhiệm (BR-20).

**4.6.14 — Xem trước đề / Preview Quiz**  
`BE + Web` · 1 MD · dep: 4.6.10  
Xem đề đúng như học sinh sẽ thấy trước khi giao.

**4.6.15 — Kiểm thử quyền sở hữu câu hỏi và đề / Ownership Tests**  
`QA` · 1.5 MD · dep: 4.6.4, 4.6.12, 4.6.13  
Người khác không sửa được câu và đề của mình; đề đã giao khóa vĩnh viễn kể cả với người tạo; nhân bản tạo bản mới thuộc người bấm.

### LRN-02 Assignment & Submission (Giao bài & Nộp bài)

**4.6.16 — Giao đề cho lớp / Assign Quiz to Class**  
`BE + Web` · 2 MD · dep: 4.6.10, 4.3.37  
Teacher ĐANG PHỤ TRÁCH CHUYÊN MÔN ở (lớp, môn của đề) giao ĐỀ MÌNH SỞ HỮU cho lớp đó, chọn thời điểm mở (mặc định ngay) và hạn nộp lớn hơn hiện tại. Khi bài chuyển sang Đã giao, hệ thống lưu danh sách học sinh có ghi danh hiệu lực ở lớp lúc đó làm DANH SÁCH ĐƯỢC GIAO; danh sách này không đổi khi học sinh chuyển lớp về sau (BR-21).

**4.6.17 — Cập nhật bài đang ở Nháp / Update Draft Assignment**  
`BE + Web` · 1 MD · dep: 4.6.16  
Bài ở Nháp sửa được. Chuyển sang Đã giao thì không sửa và không gia hạn được nữa (BR-21).

**4.6.18 — Xóa bài đang ở Nháp / Delete Draft Assignment**  
`BE + Web` · 0.5 MD · dep: 4.6.16  
Xóa bài chưa giao.

**4.6.19 — Khóa đề khi giao lần đầu / Lock Quiz on First Assignment**  
`BE` · 1 MD · dep: 4.6.16  
Giao lần đầu thì đề khóa VĨNH VIỄN — từ đó không ai sửa được, kể cả người tạo. Muốn cho làm lại thì nhân bản đề và giao như bài mới (BR-20, BR-21).

**4.6.20 — Xem danh sách bài đã giao theo lớp / View Assignments by Class**  
`BE + Web` · 1.5 MD · dep: 4.6.16  
Giáo viên xem bài của lớp và môn mình phụ trách kèm trạng thái, hạn nộp và số lượt đã nộp.

**4.6.21 — Xem danh sách bài của mình / View My Assignments**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.6.16  
Học sinh xem bài được giao kèm hạn nộp và tình trạng đã nộp hay chưa.

**4.6.22 — Mở đề và làm bài / Open and Take Quiz**  
`BE + Web + Mobile` · 2 MD · dep: 4.6.21  
Học sinh trong danh sách được giao (4.6.16) mở bài từ thời điểm mở tới hạn nộp. Mở đề không tính lượt, không giới hạn thời lượng (BR-21).

**4.6.23 — Tự lưu nháp bài làm / Autosave Attempt**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.6.22  
Bài làm tự lưu nháp trong lúc làm; nháp chưa nộp KHÔNG được chấm (BR-21).

**4.6.24 — Nộp bài một lượt / Submit Attempt**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.6.23  
Tối đa MỘT lần nộp. Backend từ chối nộp sau hạn và nộp lại. Không kiểm lại ghi danh khi nộp nên chuyển lớp giữa chừng không làm mất bài (BR-21, mục 7.8).

**4.6.25 — Chấm tự động khi nộp / Auto-grade on Submission**  
`BE` · 2 MD · dep: 4.6.24  
Backend chấm ngay khi nộp: điểm = tổng điểm câu đúng / tổng điểm đề × 10. Chưa nộp KHÔNG phải 0 điểm — hiển thị Không nộp, chỉ áp dụng cho học sinh trong danh sách được giao; mẫu số tỉ lệ nộp là số học sinh của danh sách đó (BR-21).

**4.6.26 — Kiểm thử nộp bài và hạn nộp / Submission Tests**  
`QA` · 2 MD · dep: 4.6.24, 4.6.25  
Nộp sau hạn bị chặn; nộp lần hai bị chặn; nháp không ra điểm; học sinh ngoài danh sách được giao không mở được bài; chuyển lớp sau khi giao hoặc sau khi nộp không đổi người nhận, mẫu số và bài đã nộp.

### LRN-03 Grading & Score Publication (Chấm bài & Công bố điểm)

**4.6.27 — Xem kết quả bài theo lớp / View Class Results**  
`BE + Web` · 1.5 MD · dep: 4.6.25  
Teacher đang phụ trách chuyên môn xem điểm từng học sinh, ai chưa nộp, trước khi công bố (BR-22).

**4.6.28 — Xem thống kê câu sai nhiều nhất / View Most-missed Questions**  
`BE + Web` · 1.5 MD · dep: 4.6.27  
Số và tỉ lệ học sinh ĐÃ NỘP chọn sai mỗi câu, sắp theo mức sai nhiều nhất (BR-22).

**4.6.29 — Công bố điểm cho lớp / Publish Scores**  
`BE + Web` · 1.5 MD · dep: 4.6.27  
Chỉ khi bài ĐÃ ĐÓNG (đã giao và quá hạn nộp), bởi Teacher đang phụ trách chuyên môn. Công bố cho cả lớp một lần, KHÔNG THU HỒI; tạo thông báo cho học sinh và phụ huynh (BR-22, BR-26).

**4.6.30 — Xem điểm đã công bố / View Published Score**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.6.29  
Học sinh xem điểm và đáp án đúng sau khi giáo viên công bố.

**4.6.31 — Xem điểm của con đã công bố / View Child's Published Score**  
`BE + Web + Mobile` · 1 MD · dep: 4.6.30  
Phụ huynh xem điểm đã công bố của từng con được liên kết.

**4.6.32 — Kiểm thử công bố điểm / Publication Tests**  
`QA` · 1.5 MD · dep: 4.6.29  
Bài chưa đóng không công bố được; giáo viên hết phụ trách chuyên môn không công bố được; công bố rồi không thu hồi được; học sinh chưa nộp hiện Không nộp chứ không phải 0.

### LRN-04 AI Learning Analysis (Phân tích học tập bằng AI)

**4.6.33 — Tính chỉ số cấp lớp và cấp học sinh / Compute Analysis Metrics**  
`BE` · 2 MD · dep: 4.6.25  
Backend tính sẵn: cấp lớp là tỉ lệ đúng theo câu và phân bố điểm; cấp học sinh (chỉ học sinh đã nộp) là kết quả bài này kèm tối đa 5 bài gần nhất cùng môn ĐÃ CÔNG BỐ ĐIỂM của chính em đó (BR-23).

**4.6.34 — Gắn mã giả lập trước khi gửi AI / Pseudonymize Data**  
`BE` · 1.5 MD · dep: 4.6.33  
AI chỉ nhận dữ liệu đã tính sẵn gắn mã giả lập. KHÔNG BAO GIỜ gửi tên, mã học sinh, ngày sinh, tên lớp, tên trường hay bài làm nguyên văn (mục 3.1, BR-23).

**4.6.35 — Gọi AI và nhận nhận xét nháp / Call AI and Receive Draft Comments**  
`BE + Web` · 2 MD · dep: 4.6.34  
Chạy được sau khi bài đã đóng và có ít nhất một bài nộp. Kết quả về ở trạng thái Nháp; AI không chấm điểm, không quyết định đúng sai, không ghi trực tiếp vào dữ liệu nghiệp vụ (mục 3.1, BR-23).

**4.6.36 — Xem nhận xét nháp cấp lớp / View Class-level Draft Comment**  
`BE + Web` · 1 MD · dep: 4.6.35  
Giáo viên xem nhận xét chung của cả lớp kèm các chỉ số đã tính.

**4.6.37 — Xem nhận xét nháp từng học sinh / View Student Draft Comments**  
`BE + Web` · 1.5 MD · dep: 4.6.35  
Giáo viên xem nhận xét nháp của từng học sinh đã nộp.

**4.6.38 — Chỉnh nhận xét / Edit Comment**  
`BE + Web` · 1 MD · dep: 4.6.37  
Giáo viên sửa nội dung nhận xét khi còn ở Nháp (BR-23).

**4.6.39 — Duyệt nhận xét / Approve Comment**  
`BE + Web` · 1 MD · dep: 4.6.38  
Chuyển nhận xét từ Nháp sang Đã duyệt; chỉ Teacher đang phụ trách chuyên môn làm được (BR-02, BR-23).

**4.6.40 — Công bố nhận xét / Publish Comment**  
`BE + Web` · 1 MD · dep: 4.6.39  
Công bố cho học sinh và phụ huynh; công bố nhận xét ĐỘC LẬP với công bố điểm (BR-23, BR-26).

**4.6.41 — Duyệt và công bố hàng loạt / Bulk Approve and Publish**  
`BE + Web` · 1 MD · dep: 4.6.39  
Duyệt và công bố nhiều nhận xét trong một thao tác.

**4.6.42 — Tạo lại nhận xét khi Lỗi hoặc Nháp / Regenerate Failed or Draft Analysis**  
`BE + Web` · 1.5 MD · dep: 4.6.35  
AI lỗi thì nhận xét ở trạng thái Lỗi kèm lý do, giáo viên chạy lại. Nhận xét còn Nháp cũng tạo lại được và nội dung Nháp cũ bị thay bằng kết quả mới. Nhận xét đã Đã duyệt hoặc Đã công bố thì không tạo lại và không bị ghi đè. Không ảnh hưởng chấm, nộp và công bố điểm (mục 3.1, BR-23).

**4.6.43 — Xem nhận xét đã công bố / View Published Comment**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.6.40  
Student và Parent chỉ thấy nhận xét Đã công bố.

**4.6.44 — Kiểm thử phân tích AI / AI Analysis Tests**  
`QA` · 2 MD · dep: 4.6.34, 4.6.35, 4.6.40  
Dữ liệu gửi đi không chứa thông tin định danh; bài chưa đóng hoặc không có bài nộp thì không chạy được; AI hỏng không chặn chấm và công bố điểm; nhận xét Nháp không lộ cho học sinh; tạo lại được khi Nháp hoặc Lỗi, bị chặn khi đã duyệt hoặc công bố.

### LRN-05 Department Monitoring (Giám sát cấp tổ)

**4.6.45 — Xem tiến độ bài tập của tổ / View Department Assignment Progress**  
`BE + Web` · 2 MD · dep: 4.6.20, 4.3.29  
Tổ trưởng xem tiến độ bài tập của MỌI LỚP thuộc các môn trong tổ mình: trạng thái, hạn nộp, số nộp trên sĩ số, đã công bố điểm chưa.

**4.6.46 — Lọc tiến độ theo môn, lớp và khoảng hạn nộp / Filter Department Progress**  
`BE + Web` · 1 MD · dep: 4.6.45  
Thu hẹp bảng tiến độ theo môn, lớp, phân hiệu và khoảng hạn nộp.

**4.6.47 — Xem chi tiết một bài ở chế độ chỉ đọc / View Assignment Detail Read-only**  
`BE + Web` · 1.5 MD · dep: 4.6.45  
Mở chi tiết một bài để xem kết quả tổng hợp, không có đường nào ghi.

**4.6.48 — Kiểm thử chặn mọi đường ghi của tổ trưởng / Read-only Enforcement Tests**  
`QA` · 1 MD · dep: 4.6.45  
Backend chặn mọi API ghi khi người gọi chỉ có trách nhiệm tổ trưởng: không giao bài, không công bố điểm, không sửa nhận xét của lớp mình không phụ trách.

---

## 4.7 PAR — Parent Services (Dịch vụ phụ huynh)

### PAR-01 Leave Request (Đơn xin nghỉ)

**4.7.1 — Gửi đơn xin nghỉ / Submit Leave Request**  
`BE + Web + Mobile` · 2 MD · dep: 4.3.25, 4.3.30  
Chỉ Parent gửi, chỉ cho con được liên kết đang có ghi danh mở. Đơn chọn từ ngày – đến ngày và buổi (cả ngày / sáng / chiều) kèm lý do; ngày bắt đầu phải ≥ hôm nay và trong năm học hiện hành. Khi gửi, đơn ghi lại lớp học sinh đang có ghi danh mở (BR-19).

**4.7.2 — Kiểm xung đột đơn theo ngày và buổi / Check Leave Request Overlap**  
`BE` · 1.5 MD · dep: 4.7.1  
Hai đơn của cùng một học sinh xung đột khi chồng ngày VÀ chồng buổi; một đơn cả ngày chồng với mọi đơn khác. Nhờ vậy xin nghỉ sáng và chiều cùng một ngày bằng hai đơn riêng là hợp lệ (BR-19).

**4.7.3 — Xem danh sách đơn đã gửi / View Submitted Leave Requests**  
`BE + Web + Mobile` · 1 MD · dep: 4.7.1  
Phụ huynh xem đơn của từng con cùng trạng thái Chờ duyệt / Đã duyệt / Đã từ chối / Đã rút.

**4.7.4 — Rút đơn đang chờ / Withdraw Pending Request**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.7.1  
Parent rút được khi đơn còn ở Chờ duyệt (BR-19).

**4.7.5 — Xem hộp đơn của lớp chủ nhiệm / View Homeroom Leave Inbox**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.1  
Người duyệt là chủ nhiệm ĐANG HIỆU LỰC của lớp ghi trên đơn tại lúc duyệt, nên đổi chủ nhiệm khi đơn còn chờ thì người mới thấy đơn (BR-09, BR-19).

**4.7.6 — Duyệt đơn xin nghỉ / Approve Leave Request**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.5  
Quyết định là cuối cùng. Đơn duyệt KHÔNG tự sinh vắng — nó chỉ quyết định nhãn có phép khi học sinh bị ghi vắng; học sinh có đơn mà vẫn đi học thì ghi Có mặt (BR-18, BR-19).

**4.7.7 — Từ chối đơn xin nghỉ / Reject Leave Request**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.7.5  
Từ chối kèm lý do; bị từ chối thì phụ huynh gửi đơn mới. Kết quả tạo thông báo cho Parent (BR-19, BR-26).

**4.7.8 — Kiểm thử đơn xin nghỉ / Leave Request Tests**  
`QA` · 2 MD · dep: 4.7.2, 4.7.6  
Ngày bắt đầu trong quá khứ bị chặn; hai đơn chồng ngày và chồng buổi bị chặn còn sáng/chiều riêng thì không; đổi chủ nhiệm giữa chừng vẫn đúng một người duyệt; đơn duyệt muộn đổi nhãn vắng đã ghi.

### PAR-02 Weekly Student Report (Sổ liên lạc tuần)

**4.7.9 — Tính sổ liên lạc tuần khi mở / Compute Weekly Report on Read**  
`BE` · 2.5 MD · dep: 4.5.10, 4.6.29  
TÍNH TRỰC TIẾP khi mở, không lưu, không có job sinh sổ, không ai viết tay. Một sổ = một học sinh + một tuần (thứ Hai – Chủ nhật) của năm học hiện hành mà học sinh có ghi danh (BR-24).

**4.7.10 — Xem sổ liên lạc tuần của con / View Child's Weekly Report**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.9  
Gồm chuyên cần theo tiết Đã xếp lịch đã diễn ra; bài tập có hạn nộp trong tuần với số được giao, đã nộp, không nộp; điểm và nhận xét đã công bố của các bài đó. Không xếp hạng, không điểm trung bình lớp, không hạnh kiểm (BR-24).

**4.7.11 — Xem sổ liên lạc tuần của mình / View Own Weekly Report**  
`BE + Web + Mobile` · 1 MD · dep: 4.7.9  
Học sinh xem sổ của chính mình, cùng nội dung như phụ huynh xem.

**4.7.12 — Chọn tuần để xem / Select Report Week**  
`BE + Web + Mobile` · 1 MD · dep: 4.7.9  
Xem được mọi tuần của năm học hiện hành mà học sinh có ghi danh; mục không có dữ liệu để trống.

**4.7.13 — Chủ nhiệm xem sổ của học sinh lớp mình / View Class Weekly Reports**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.9  
Chủ nhiệm mở sổ của từng học sinh trong lớp chủ nhiệm.

**4.7.14 — Kiểm thử sổ liên lạc / Weekly Report Tests**  
`QA` · 1 MD · dep: 4.7.9  
Nhập bù điểm danh và đơn duyệt muộn được phản ánh ngay lần mở sau; tiết Đã hủy không vào thống kê; bài chưa công bố điểm không hiện điểm.

### PAR-03 Parent–Teacher Messaging (Hộp thư phụ huynh – giáo viên)

**4.7.15 — Tạo hội thoại với giáo viên / Create Conversation**  
`BE + Web + Mobile` · 2 MD · dep: 4.3.37, 4.3.30  
Chỉ Parent TẠO hội thoại, về một con được liên kết, với chủ nhiệm lớp con đang học hoặc giáo viên đang phụ trách chuyên môn ở lớp đó. Mỗi bộ (phụ huynh, cán bộ, học sinh) có một hội thoại (BR-25).

**4.7.16 — Xem danh sách hội thoại / View Conversation List**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.15  
Hai bên xem danh sách hội thoại của mình kèm tin nhắn gần nhất.

**4.7.17 — Gửi tin nhắn / Send Message**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.7.15  
Chỉ văn bản, tối đa 2.000 ký tự. Không file, không gọi, không nhóm. Tin nhắn mới tạo thông báo cho người nhận (BR-25, BR-26).

**4.7.18 — Kiểm quan hệ mỗi lần gửi / Verify Relationship on Each Send**  
`BE` · 1 MD · dep: 4.7.17  
Quan hệ được kiểm MỖI LẦN GỬI; quan hệ đã kết thúc thì hội thoại thành chỉ đọc, lịch sử giữ nguyên (BR-25).

**4.7.19 — Đánh dấu đã đọc và đếm tin chưa đọc / Mark Read and Count Unread**  
`BE + Web + Mobile` · 1 MD · dep: 4.7.17  
Đếm tin chưa đọc theo từng hội thoại và tổng trên thanh điều hướng.

**4.7.20 — Xem hội thoại chỉ đọc khi quan hệ kết thúc / View Read-only Conversation**  
`BE + Web + Mobile` · 1 MD · dep: 4.7.18  
Hội thoại chỉ đọc vẫn mở xem được, nút gửi bị tắt và Backend từ chối gửi (BR-25).

**4.7.21 — Kiểm thử hộp thư / Messaging Tests**  
`QA` · 1.5 MD · dep: 4.7.18  
Phụ huynh không tạo được hội thoại với giáo viên không dạy con mình; hết phụ trách chuyên môn thì hội thoại thành chỉ đọc; School Admin không đọc được hội thoại và không có đường gửi tin.

---

## 4.8 COM — Communication (Truyền thông)

### COM-01 Notification Center (Trung tâm thông báo)

**4.8.1 — Phát thông báo theo danh sách sự kiện đóng / Dispatch Event Notifications**  
`BE` · 2 MD · dep: 4.1.4  
Chỉ trong ứng dụng, không gửi email. Danh sách sự kiện là DANH SÁCH ĐÓNG: phát hành thời khóa biểu, tiết bị đổi — hủy — thêm bù — ngày nghỉ đột xuất — trả lại giáo viên khi xóa khai báo vắng, tiết chuyển Chờ xử lý, đơn nghỉ mới, đơn được duyệt hoặc từ chối, bài mới được giao, điểm và nhận xét được công bố, tin nhắn mới (BR-26).

**4.8.2 — Xem trung tâm thông báo / View Notification Center**  
`BE + Web + Mobile` · 1.5 MD · dep: 4.8.1  
Danh sách thông báo của chính mình, phân biệt đã đọc và chưa đọc.

**4.8.3 — Mở thông báo và kiểm quyền khi mở / Open Notification with Re-check**  
`BE + Web + Mobile` · 1 MD · dep: 4.8.2  
Có thông báo KHÔNG đồng nghĩa có quyền — Backend kiểm lại quyền khi người dùng bấm vào, rồi mới đưa tới bản ghi nguồn (BR-26).

**4.8.4 — Đánh dấu đã đọc / Mark Notification Read**  
`BE + Web + Mobile` · 0.5 MD · dep: 4.8.2  
Đánh dấu một hoặc tất cả thông báo là đã đọc.

**4.8.5 — Kiểm thử thông báo / Notification Tests**  
`QA` · 1 MD · dep: 4.8.1, 4.8.3  
Chỉ đúng nhóm người nhận được thông báo; mở thông báo của dữ liệu ngoài phạm vi bị từ chối; bảng tin không sinh thông báo; sự kiện realtime chỉ tới đúng người nhận và chỉ sau khi lưu thành công; kết nối bị ngắt khi tài khoản bị khóa; nối lại thì không mất tin; đang nhập và online chỉ hiện cho bên kia trong cùng hội thoại; hub không cho gửi tin vượt BR-25.

**4.8.6 — Hub SignalR và xác thực kết nối / SignalR Hub and Connection Auth**  
`BE` · 2 MD · dep: 4.8.1, 4.1.6  
Một hub SignalR cho cả thông báo và tin nhắn. Kết nối xác thực bằng access token qua query string `access_token`, đi qua cùng kiểm tài khoản và phiên như REST: tài khoản bị khóa hoặc phiên bị thu hồi thì kết nối bị ngắt; access token đổi thì client dựng lại kết nối. Mỗi tài khoản vào một nhóm riêng để đẩy đúng người. Không thêm bảng CSDL; chạy một instance nên không cần backplane.

**4.8.7 — Đẩy thông báo và tin nhắn mới sau khi lưu / Push After Commit**  
`BE` · 1.5 MD · dep: 4.8.6, 4.7.17  
Sự kiện `NotificationCreated` và `MessageReceived` chỉ được đẩy **sau khi** `SaveChanges` thành công, để giao dịch bị hoàn tác không sinh thông báo ảo. Chữ ký `NotificationSender` giữ nguyên, module khác không đổi. Gửi tin vẫn qua REST và kiểm quan hệ mỗi lần gửi (BR-25); hub không nhận lệnh gửi tin. Quyền vẫn do Backend kiểm khi mở (BR-26).

**4.8.8 — Đang nhập, đã xem và online/offline / Typing, Seen and Presence**  
`BE` · 1.5 MD · dep: 4.8.6, 4.7.19  
Hub nhận thêm lệnh `Typing(conversationId)` và `JoinConversation(conversationId)`; mỗi lệnh kiểm người gọi thuộc hội thoại rồi chuyển cho bên kia. Đang nhập không lưu CSDL, tự tắt sau vài giây. Đã xem tính khi người nhận mở hội thoại: ghi `Message.ReadAt` và đẩy `MessageRead` cho người gửi. Online khi tài khoản còn ít nhất một kết nối, offline khi mất hết (nhiều tab, nhiều thiết bị); trạng thái chỉ hiện cho bên kia trong cùng hội thoại. School Admin không tham gia hội thoại nên không hiện online và không gửi đang nhập.

**4.8.9 — Realtime trên Web / Web Realtime Client**  
`Web` · 2 MD · dep: 4.8.7, 4.8.8  
Dùng `@microsoft/signalr`, kết nối một lần ở lớp xác thực. Chuông thông báo và hộp thư cập nhật ngay khi có sự kiện; hiện chỉ báo đang nhập, đã xem, chấm online. Khi nối lại sau khi mất kết nối thì tải lại phần đã lỡ qua REST.

**4.8.10 — Realtime trên Mobile / Mobile Realtime Client**  
`Mobile` · 3 MD · dep: 4.8.7, 4.8.8  
Dùng `signalr_netcore` trong dịch vụ Riverpod; chỉ giữ kết nối khi app đang mở, ra nền thì ngắt và tính là offline. Khi quay lại hoặc nối lại thì tải phần đã lỡ qua REST. Giữ polling chậm làm dự phòng nếu kết nối không dựng được. Cùng chức năng như Web: chuông, hộp thư, đang nhập, đã xem, online.

### COM-02 News Feed (Bảng tin)

**4.8.11 — Xem bảng tin theo phạm vi người đọc / View News Feed**  
`BE + Web + Mobile` · 2 MD · dep: 4.1.4  
Bài Nội bộ chỉ cán bộ đọc; bài Toàn trường mọi người đọc, gồm học sinh và phụ huynh. Ảnh đầu là ảnh bìa hiện trên danh sách (BR-27).

**4.8.12 — Soạn bài đăng và chọn phạm vi / Create Post with Scope**  
`BE + Web` · 1.5 MD · dep: 4.8.11  
MỌI CÁN BỘ đăng được — School Admin, Principal, Vice Principal, Teacher — và chọn một trong hai phạm vi Nội bộ hoặc Toàn trường. Soạn bài chỉ có trên web (BR-27, mục 8.1).

**4.8.13 — Đính kèm ảnh cho bài đăng / Attach Post Images**  
`BE + Web` · 2 MD · dep: 4.8.12  
Tối đa 5 ảnh JPG hoặc PNG, mỗi ảnh ≤ 2 MB. Dùng chung cách lưu file với ảnh đại diện người dùng (BR-27).

**4.8.14 — Cập nhật bài đăng của mình / Update Own Post**  
`BE + Web` · 1 MD · dep: 4.8.12  
Người đăng sửa nội dung, phạm vi và ảnh của bài mình.

**4.8.15 — Ẩn và hiện lại bài / Hide and Unhide Post**  
`BE + Web` · 1 MD · dep: 4.8.12  
Trạng thái Đã đăng ↔ Đã ẩn, không có trạng thái nháp. Người đăng ẩn bài của mình; School Admin và Principal ẩn bài của người khác, bắt buộc nhập lý do, lưu người ẩn, thời điểm, lý do; tác giả và nội dung giữ nguyên. Ẩn bài giữ ảnh (BR-27).

**4.8.16 — Xóa bài và xóa file ảnh kèm theo / Delete Post and Its Images**  
`BE + Web` · 0.5 MD · dep: 4.8.15  
Xóa bài thì xóa luôn file ảnh, khác với ẩn bài (BR-27).

**4.8.17 — Kiểm thử phạm vi bảng tin / News Feed Scope Tests**  
`QA` · 1 MD · dep: 4.8.11, 4.8.12  
Học sinh và phụ huynh không thấy bài Nội bộ; giáo viên đăng được không cần trách nhiệm; School Admin và Principal ẩn được bài của người khác nhưng phải nhập lý do, Teacher và Vice Principal thì không; bảng tin không tạo thông báo.

---

## 4.9 ADM — Administration & Oversight (Quản trị & Giám sát)

### ADM-01 Dashboard (Bảng điều khiển)

**4.9.1 — Tính sáu chỉ số theo phạm vi / Compute Dashboard Metrics**  
`BE` · 2.5 MD · dep: 4.4.29, 4.5.6, 4.7.5, 4.6.16, 4.6.24, 4.3.34  
Sáu chỉ số: tiết Chờ xử lý, tiết đã qua chưa điểm danh, chuyên cần 7 ngày, đơn nghỉ chờ duyệt (chỉ Vice Principal; Principal không thấy), tỉ lệ nộp bài 30 ngày (mẫu số là danh sách được giao ở 4.6.16), lớp chưa có chủ nhiệm. Một tiết, đơn hay bài thuộc phân hiệu nào là theo PHÂN HIỆU CỦA LỚP liên quan (mục 5.1).

**4.9.2 — Xem Dashboard / View Dashboard**  
`BE + Web` · 2 MD · dep: 4.9.1  
Principal xem toàn trường và không đổi được; Vice Principal xem cố định phân hiệu mình. School Admin không có Dashboard (mục 5.1).

**4.9.3 — Mở danh sách gốc từ một chỉ số / Drill into KPI Source List**  
`BE + Web` · 1.5 MD · dep: 4.9.2  
Mỗi số mở ra danh sách gốc: tiết Chờ xử lý mở màn hình xử lý của SCD-03, lớp chưa có chủ nhiệm mở màn hình trách nhiệm của PPL-04, đơn nghỉ (chỉ Vice Principal) mở hộp đơn của PAR-01 ở chế độ chỉ đọc, không có nút duyệt hay từ chối; không có nút nhập bù, School Admin nhập bù ở màn hình điểm danh (mục 5.1).

**4.9.4 — Kiểm thử phạm vi Dashboard / Dashboard Scope Tests**  
`QA` · 1 MD · dep: 4.9.2  
Vice Principal không xem được số liệu phân hiệu khác; con số khớp với danh sách gốc mở ra; nút nhập bù không hiện với Vice Principal.

### ADM-02 Audit Log (Nhật ký kiểm toán)

**4.9.5 — Ghi nhật ký cho ba nhóm sự kiện / Record Audit Entry**  
`BE` · 2 MD · dep: 4.1.12, 4.4.11  
Đúng ba nhóm: tài khoản (tạo đơn lẻ và hàng loạt, khóa, mở, đổi loại tài khoản, School Admin reset mật khẩu — không ghi mật khẩu); phát hành thời khóa biểu (phiên bản, số tiết xóa và tạo); điều chỉnh lịch (đổi giáo viên, hủy, tiết bù, ngày nghỉ đột xuất, khai báo, xóa khai báo và xử lý giáo viên vắng). Thao tác hàng loạt ghi MỘT bản ghi kèm danh sách ID (BR-28).

**4.9.6 — Tra cứu nhật ký kiểm toán / Browse Audit Log**  
`BE + Web` · 1.5 MD · dep: 4.9.5  
School Admin xem nhật ký theo thứ tự thời gian.

**4.9.7 — Lọc nhật ký theo nhóm, người và thời gian / Filter Audit Log**  
`BE + Web` · 1 MD · dep: 4.9.6  
Lọc theo ba nhóm sự kiện, người thực hiện và khoảng thời gian (BR-28).

**4.9.8 — Xem chi tiết một bản ghi nhật ký / View Audit Entry Detail**  
`BE + Web` · 1 MD · dep: 4.9.6  
Xem nội dung đầy đủ của một bản ghi, gồm danh sách ID của thao tác hàng loạt.

**4.9.9 — Kiểm thử tính chỉ ghi thêm / Append-only Tests**  
`QA` · 1 MD · dep: 4.9.5  
Không có API sửa hay xóa nhật ký; quyền CSDL chặn UPDATE và DELETE; việc ngoài ba nhóm (đăng nhập, nộp bài, điểm danh, trách nhiệm, phụ trách chuyên môn, kết chuyển) không vào nhật ký.

### ADM-03 Data Export (Xuất dữ liệu)

**4.9.10 — Xuất điểm danh theo lớp và khoảng ngày / Export Class Attendance**  
`BE + Web` · 1.5 MD · dep: 4.5.3  
Nguồn là bản ghi điểm danh, kèm nhãn cuối suy ra. Chủ nhiệm xuất lớp mình, Vice Principal xuất phân hiệu mình (BR-18, mục 5.2).

**4.9.11 — Xuất thời khóa biểu hiện hành / Export Current Timetable**  
`BE + Web` · 1.5 MD · dep: 4.4.11  
Nguồn là LỊCH DẠY chứ không phải phiên bản thời khóa biểu, để phản ánh cả các điều chỉnh tay. Vice Principal xuất phân hiệu mình (BR-14, mục 5.2).

**4.9.12 — Xuất kết quả bài tập theo lớp / Export Class Assignment Results**  
`BE + Web` · 1.5 MD · dep: 4.6.25  
Nguồn là lượt làm bài. Chỉ Teacher đang phụ trách chuyên môn xuất được (mục 5.2).

**4.9.13 — Kiểm thử phạm vi xuất dữ liệu / Export Scope Tests**  
`QA` · 1 MD · dep: 4.9.10, 4.9.11, 4.9.12  
Mỗi mục chỉ xuất đúng phạm vi của người gọi; danh sách tài khoản kèm mật khẩu tạm KHÔNG phải một mục xuất và không xuất lại được (BR-04).

---

**Tổng:** 264 task · 352 MD
