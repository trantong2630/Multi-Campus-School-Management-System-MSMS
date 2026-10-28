# MSMS — Business Specification

Oct 20, 2026 · V3.1 · @Trần Đức Kiên

Multi-campus School Management System — tài liệu nghiệp vụ chuẩn cho BA, FE, BE, QA.

## 1. Hệ thống và phạm vi

MSMS là hệ thống quản trị điều hành cho **một trường THCS (khối 6–9) có nhiều phân hiệu**. MSMS không quản lý hồ sơ pháp lý — học bạ, sổ điểm chính thức, báo cáo cho phòng giáo dục — mà quản lý **việc dạy và học hằng ngày**: ai dạy lớp nào, tiết nào đã diễn ra, học sinh có mặt hay không, bài tập rèn luyện được giao và làm ra sao, phụ huynh biết được gì về con mình.

MSMS là hệ thống độc lập, tự quản lý dữ liệu của mình; không trao đổi dữ liệu với phần mềm chính thức của trường.

### Trong phạm vi

| Nhóm | Nội dung |
| --- | --- |
| Thiết lập | Thông tin trường (tên, địa chỉ, liên hệ, logo), phân hiệu, tổ chuyên môn, môn học, năm học, học kỳ, ngày nghỉ, khung giờ tiết, lớp |
| Hồ sơ | Cán bộ, học sinh, phụ huynh, ghi danh, liên kết phụ huynh – học sinh |
| Trách nhiệm | Trách nhiệm (phân hiệu, tổ, lớp chủ nhiệm) |
| Vận hành | Thời khóa biểu, phụ trách chuyên môn (tính từ thời khóa biểu), lịch dạy thực tế, xử lý giáo viên vắng, điểm danh theo tiết |
| Học tập | Ngân hàng câu hỏi, bài tập trắc nghiệm, chấm tự động, công bố điểm, phân tích AI, giám sát tổ |
| Dịch vụ phụ huynh | Đơn xin nghỉ, sổ liên lạc tuần, hộp thư phụ huynh – giáo viên |
| Truyền thông | Thông báo trong ứng dụng, bảng tin |
| Giám sát | Dashboard, Audit Log, xuất Excel |

### Ngoài phạm vi

Tuyển sinh. Sổ điểm và học bạ chính thức — **điểm trong MSMS chỉ là điểm bài tập rèn luyện**. LMS và kho học liệu. Tự xếp thời khóa biểu. Nhân sự, lương, nghỉ phép của cán bộ — MSMS chỉ xử lý **hệ quả lên lịch dạy**. Chat nhóm, gọi thoại, gọi video. AI chấm tự luận hoặc chấm điểm. Quy trình chuyển học kỳ. Giả đăng nhập hoặc chuyển vai trò. **Thông báo qua email** — mọi thông báo chỉ nằm trong ứng dụng; email chỉ dùng cho mã OTP quên mật khẩu. **Job chạy nền theo lịch** — hệ thống không có job nào.

Gửi file: không có, trừ **ảnh đính kèm bài đăng bảng tin**, **ảnh đại diện người dùng** và **logo trường** (BR-27).

### Nguyên tắc nền

1. Mọi dữ liệu gắn với **năm học, phân hiệu hoặc lớp, và khoảng hiệu lực**. Luôn lấy đối tượng có hiệu lực tại ngày nghiệp vụ, không lấy đối tượng hiện tại.
2. **Không ghi đè lịch sử.** Thứ đã diễn ra không bị sửa bởi thời khóa biểu mới, phụ trách mới hay kết chuyển năm.
3. **Không lưu thứ tính ra được.** Trạng thái suy được từ ngày giờ hoặc dữ liệu khác thì tính lúc đọc (mục 6.2). Nhờ vậy hệ thống không cần job cập nhật trạng thái.
4. **Quyền luôn kiểm tra tại Backend**, không dựa vào giao diện.
5. **Ngày nghiệp vụ theo giờ Việt Nam (Asia/Ho_Chi_Minh).** Backend quyết định "hôm nay", hạn nộp và mọi điều kiện thời gian.
6. **Hệ thống gợi ý, con người quyết định.** MSMS không tự xếp giáo viên dạy thay, không tự tạo tiết bù, không tự công bố điểm hay nhận xét.
7. **Đủ dùng hơn đầy đủ.** Quy tắc nào làm luồng phức tạp hơn giá trị nó mang lại thì bỏ.

---

## 2. Actor và phân quyền

### 2.1. Loại tài khoản

Có **6 loại tài khoản; một tài khoản mang đúng một loại** (BR-01). Mỗi hồ sơ có đúng một tài khoản.

| Loại tài khoản | Tiếng Việt | Hồ sơ | Phạm vi lấy từ |
| --- | --- | --- | --- |
| **School Admin** | Quản trị hệ thống | Cán bộ | Toàn trường (mục 2.4) |
| **Principal** | Hiệu trưởng | Cán bộ | Toàn trường |
| **Vice Principal** | Hiệu phó | Cán bộ | Trách nhiệm phân hiệu |
| **Teacher** | Giáo viên | Cán bộ | Phụ trách chuyên môn, tiết được giao dạy, trách nhiệm chủ nhiệm / tổ trưởng |
| **Student** | Học sinh | Học sinh | Ghi danh hiệu lực |
| **Parent** | Phụ huynh | Phụ huynh | Liên kết phụ huynh – học sinh |

### 2.2. Trách nhiệm

**Homeroom Teacher** (chủ nhiệm) và **Subject Department Head** (tổ trưởng) **không phải loại tài khoản**. Đó là trách nhiệm gán cho tài khoản Teacher, có khoảng hiệu lực. Hết trách nhiệm thì quyền tương ứng tự mất, tài khoản không đổi.

| Trách nhiệm | Gán cho loại | Đích | Ai gán |
| --- | --- | --- | --- |
| Phụ trách phân hiệu | Vice Principal | Một phân hiệu | Principal, School Admin |
| Tổ trưởng | Teacher | Một tổ chuyên môn | Principal, School Admin |
| Chủ nhiệm | Teacher | Một lớp | Principal, School Admin |

**Phụ trách chuyên môn** (giáo viên – môn – lớp) là một thứ khác, không phải trách nhiệm và không do ai gán: hệ thống tính từ thời khóa biểu đã phát hành, xem BR-14 và SCD-04.

### 2.3. Ai làm được gì

| Actor | Đọc | Ghi |
| --- | --- | --- |
| **School Admin** | Mọi dữ liệu vận hành toàn trường, kể cả điểm và nhận xét chưa công bố. **Không** đọc hội thoại phụ huynh – giáo viên | Mọi danh mục (kể cả thông tin trường), hồ sơ, tài khoản, trách nhiệm, ngày nghỉ cấp trường hoặc phân hiệu, **nhập bù và sửa điểm danh**. **Không** có Dashboard, **không** xuất Excel, **không** làm thời khóa biểu, **không** làm việc đứng tên người khác (mục 2.4) |
| **Principal** | Toàn trường: lịch dạy, điểm danh, Dashboard, bảng tin. **Không** đọc đơn nghỉ | Trách nhiệm (mục 2.2), thông tin trường, bài đăng bảng tin |
| **Vice Principal** | Phân hiệu mình: lịch dạy, điểm danh, đơn nghỉ, Dashboard. Màn hình Lịch dạy chỉ cho xem phân hiệu mình; riêng khi chọn giáo viên cho tiết hoặc dạy thay thì **danh sách giáo viên là toàn trường**, kèm trạng thái rảnh hoặc bận, kiểm trùng lịch cũng toàn trường (BR-13); màn hình Phụ trách chuyên môn cũng xem **toàn trường** | Thời khóa biểu, điều chỉnh lịch, giáo viên vắng — cho phân hiệu mình. Bài đăng bảng tin. Xuất Excel phân hiệu mình. **Không** sửa điểm danh |
| **Teacher** | Tiết mình dạy; lớp + môn mình phụ trách. Nếu chủ nhiệm: hồ sơ, chuyên cần, đơn nghỉ, sổ liên lạc của lớp chủ nhiệm. Nếu tổ trưởng: bài tập các môn của tổ | Điểm danh tiết mình dạy **trong ngày của tiết**; soạn câu hỏi và đề của mình; giao bài, công bố điểm và nhận xét cho lớp + môn mình phụ trách; trả lời tin nhắn; bài đăng bảng tin. Nếu chủ nhiệm: duyệt đơn nghỉ |
| **Student** | Lịch học, bài tập, điểm và nhận xét đã công bố, chuyên cần, sổ liên lạc của chính mình; bài đăng Toàn trường | Làm và nộp bài |
| **Parent** | Như Student, cho từng con được liên kết | Gửi và rút đơn nghỉ, tạo hội thoại và nhắn tin |

Người vừa là cán bộ vừa là phụ huynh dùng **hai hồ sơ và hai tài khoản riêng** (BR-03).

### 2.4. Quy tắc School Admin

School Admin là quản trị viên toàn trường: **vượt phạm vi, không làm việc đứng tên người khác, không vượt điều kiện nghiệp vụ** (BR-02). Không có cờ bỏ qua phân quyền; các việc dưới đây chỉ đơn giản là không cấp cho School Admin, và giao diện không hiển thị chúng.

Việc đứng tên người khác — chỉ đúng vai trò làm được:

| Hành vi | Chỉ ai làm được |
| --- | --- |
| Làm bài, nộp bài | Student, bằng tài khoản của mình |
| Gửi, rút đơn nghỉ | Parent có liên kết với học sinh |
| Tạo hội thoại, gửi tin nhắn | Parent có liên kết (tạo và gửi); cán bộ của hội thoại (chỉ gửi) |
| Soạn, giao, nhân bản đề | Teacher — soạn đề của mình; giao thì cần phụ trách chuyên môn đang hiệu lực |
| Công bố điểm | Teacher đang phụ trách chuyên môn |
| Tạo lại, chỉnh, duyệt, công bố nhận xét AI | Teacher đang phụ trách chuyên môn |
| Điểm danh với tư cách giáo viên dạy | Giáo viên của tiết, trong ngày của tiết |

Việc duy nhất School Admin làm thay được trên dữ liệu giảng dạy là nhập bù và sửa điểm danh của tiết đã qua (ghi là người sửa, BR-17). School Admin không duyệt đơn nghỉ, không đọc hội thoại, không thao tác thời khóa biểu, không có Dashboard và không xuất Excel. Mọi thao tác ghi lưu người thực hiện ngay trên bản ghi nghiệp vụ.

### 2.5. Kiểm thử phân quyền

**Không dùng tài khoản School Admin để kiểm thử phạm vi** của loại khác — tài khoản này có phạm vi toàn trường nên không phát hiện được rò rỉ. Mỗi loại tài khoản, và mỗi trách nhiệm (chủ nhiệm, tổ trưởng, phụ trách phân hiệu), có kiểm thử riêng trên môi trường kiểm thử với dữ liệu mẫu.

---

## 3. Hệ thống bên ngoài

Cả hai đều là **tiện ích, không phải điều kiện vận hành**: hệ thống chạy đúng nghiệp vụ khi cả hai cùng hỏng.

### 3.1. AI Assistant

|  |  |
| --- | --- |
| Dùng ở đâu | Sinh nháp câu hỏi trắc nghiệm (LRN-01); phân tích kết quả học tập của lớp và từng học sinh (LRN-04) |
| Nhận vào | Chỉ dữ liệu **đã tính sẵn, gắn mã giả lập**: tỉ lệ đúng/sai theo câu, phân bố điểm, so sánh với tối đa 5 bài gần nhất cùng môn của chính học sinh đó |
| Không bao giờ nhận | Tên, mã học sinh, ngày sinh, tên lớp, tên trường, bài làm nguyên văn |
| Không bao giờ làm | Chấm điểm; quyết định đúng/sai; tự công bố; ghi trực tiếp vào dữ liệu nghiệp vụ |

Câu hỏi do AI sinh là **nháp** cho tới khi giáo viên xác nhận. Nhận xét do AI sinh là **nháp** cho tới khi giáo viên duyệt và công bố. **AI hỏng hoặc thiếu dữ liệu:** chấm, nộp, công bố điểm vẫn chạy; giáo viên soạn đề thủ công.

**Cần rà soát trước khi vận hành thật:** sự đồng ý của phụ huynh về việc gửi dữ liệu học tập cho nhà cung cấp AI, điều khoản không dùng dữ liệu để huấn luyện, quy định bảo vệ dữ liệu cá nhân.

### 3.2. Gmail SMTP

**Chỉ dùng để gửi mã OTP quên mật khẩu**, từ hộp thư chung của trường. Không gửi thông báo, không đọc hộp thư của ai. OTP gửi tới **email ghi trong hồ sơ người dùng**; không có luồng xác nhận email riêng.

Email chỉ chứa mã OTP và thời hạn. **Gmail SMTP hỏng:** người dùng bấm "Gửi lại mã" sau đó; vẫn còn đường School Admin reset mật khẩu (BR-04). Không có hàng đợi, không thử lại tự động.

---

## 4. Luồng nghiệp vụ

Mọi tính năng phục vụ một trong bốn luồng. Tính năng không nằm trong luồng nào thì không đưa vào hệ thống.

### Luồng 0 — Thiết lập đầu năm

1. **School Admin** khai phân hiệu, tổ chuyên môn, môn học (kèm viết tắt), khung giờ tiết.
2. **School Admin** tạo năm học, hai học kỳ, ngày nghỉ cấp trường hoặc cấp phân hiệu.
3. **School Admin** tạo lớp của năm học cho từng phân hiệu — năm sau dùng kết chuyển (SCH-06).
4. **School Admin** import cán bộ, rồi import học sinh. Một lần import học sinh tạo hồ sơ học sinh, ghi danh, hồ sơ phụ huynh và liên kết (BR-11).
5. **School Admin** chọn nhiều dòng trên danh bạ để tạo tài khoản hàng loạt, nhận danh sách mật khẩu tạm và bàn giao (BR-04).
6. **Principal** gán trách nhiệm: Vice Principal – phân hiệu, tổ trưởng – tổ, chủ nhiệm – lớp.

### Luồng A — Vận hành giảng dạy

1. **Vice Principal** tải file mẫu thời khóa biểu — hệ thống sinh theo cấu hình và điền sẵn bản phát hành gần nhất — sửa rồi import theo học kỳ; file chỉ cần gồm các lớp muốn đổi. Hệ thống đối chiếu viết tắt, báo ô lỗi; file hợp lệ thành bản Nháp (BR-12).
2. **Vice Principal** sửa bản Nháp ngay trên lưới — kéo tiết, thêm tiết, đổi giáo viên, xóa tiết — và xem các xung đột lịch toàn trường được đánh dấu tại ô (BR-12, BR-13).
3. **Vice Principal** xem trước kết quả, xác nhận cảnh báo về tiết sẽ bị xóa, rồi Phát hành. Hệ thống sinh tiết học tới hết học kỳ cho các lớp trong bản Nháp, trừ ngày nghỉ (BR-14).
4. Sau khi Phát hành, phụ trách chuyên môn của giáo viên tự có theo thời khóa biểu; **Vice Principal** xem ở màn hình Phụ trách chuyên môn, không nhập tay (BR-14, SCD-04).
5. Khi có việc phát sinh, **Vice Principal** đổi giáo viên, hủy tiết hoặc thêm tiết bù (BR-15).
6. Giáo viên vắng: **Vice Principal** khai báo, hệ thống đưa các tiết bị ảnh hưởng về Chờ xử lý và gợi ý giáo viên rảnh; Vice Principal quyết định từng tiết (BR-16).
7. **Giáo viên của tiết** điểm danh trong ngày của tiết. Sau ngày đó chỉ **School Admin** nhập bù hoặc sửa (BR-17).

### Luồng B — Học tập

Đây là **bài tập rèn luyện**, không phải sổ điểm chính thức.

1. **Teacher** soạn câu hỏi trắc nghiệm 4 lựa chọn, 1 đáp án — thủ công hoặc từ nháp AI — và ghép thành đề (BR-20).
2. **Teacher** giao đề của mình cho lớp mình phụ trách, kèm hạn nộp (BR-21).
3. **Student** làm bài, tự lưu nháp, nộp **một lượt** trước hạn; Backend chấm ngay (BR-21).
4. Hết hạn, **Teacher** xem kết quả và thống kê câu sai nhiều nhất, rồi công bố điểm (BR-22).
5. **Teacher** chạy phân tích AI, chỉnh, duyệt, công bố nhận xét (BR-23).
6. **Tổ trưởng** theo dõi tiến độ của tổ ở chế độ đọc (LRN-05).

### Luồng C — Dịch vụ phụ huynh và truyền thông

1. **Parent** gửi đơn xin nghỉ; **chủ nhiệm** duyệt hoặc từ chối (BR-19). Đơn duyệt không tự sinh vắng — nó quyết định nhãn có phép khi học sinh bị ghi vắng (BR-18).
2. **Parent** và **Student** xem **sổ liên lạc tuần**, tính trực tiếp từ dữ liệu (BR-24).
3. **Parent** nhắn tin 1–1 với chủ nhiệm hoặc giáo viên đang dạy con (BR-25).
4. Mọi sự kiện đáng chú ý tạo **thông báo trong ứng dụng** (BR-26).
5. **Mọi cán bộ** đăng bài bảng tin, chọn phạm vi Nội bộ hoặc Toàn trường (BR-27).
6. **Principal**, **Vice Principal** theo dõi Dashboard; **School Admin** tra Audit Log (BR-28, ADM-01, ADM-02).

---

## 5. Danh mục Module – Feature

**9 Module / 33 Feature.** ID dùng tiền tố module — ví dụ Task `SCH-02-BE-03`. Phân rã Task ở file WBS.

| Module | Tên đầy đủ | Tiếng Việt | Số Feature |
| --- | --- | --- | --- |
| IAM | Identity & Access | Định danh & Truy cập | 4 |
| SCH | School Setup | Thiết lập trường | 7 |
| PPL | People & Responsibility | Hồ sơ & Trách nhiệm | 4 |
| SCD | Timetable & Teaching Schedule | Thời khóa biểu & Lịch dạy | 4 |
| ATT | Attendance | Điểm danh | 1 |
| LRN | Learning | Học tập | 5 |
| PAR | Parent Services | Dịch vụ phụ huynh | 3 |
| COM | Communication | Truyền thông | 2 |
| ADM | Administration & Oversight | Quản trị & Giám sát | 3 |
|  |  | **Tổng** | **33** |

Ký hiệu trong cột Ghi / Đọc: **SA** Quản trị · **HT** Hiệu trưởng · **HP** Hiệu phó · **GV** Giáo viên · **CN** chủ nhiệm · **TT** tổ trưởng · **HS** Học sinh · **PH** Phụ huynh. 📱 = có trên mobile (mục 8.1).

Cột Đọc là quyền đọc **dữ liệu**, không phải quyền có **màn hình**. Màn hình quản lý một danh mục chỉ hiện cho loại tài khoản có việc làm trên đó; loại chỉ đọc dùng dữ liệu ấy ngay tại màn hình nghiệp vụ của mình (lưới thời khóa biểu, lịch dạy, trang chủ, bộ chọn trong form). Ai thấy màn hình nào xem bảng menu ở Dev_Conventions §4.7.

### IAM — Identity & Access (4)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| IAM-01 | Đăng nhập & Phân quyền | Đăng nhập, đăng xuất, phân giải phạm vi, trách nhiệm và phụ trách chuyên môn tại ngày hiện tại, kiểm quyền hai tầng, phân quyền hành vi theo vai trò | — | mọi loại 📱 | BR-01, 02 |
| IAM-02 | Quản lý tài khoản | Tạo đơn lẻ và hàng loạt từ danh bạ, mật khẩu tạm hiện một lần, khóa – mở, đổi loại tài khoản | SA | SA | BR-01, 03, 04, 05 |
| IAM-03 | Mật khẩu | Buộc đổi lần đầu, đổi khi đang dùng, quên mật khẩu bằng OTP, School Admin reset | chính chủ 📱, SA | — | BR-04 |
| IAM-04 | Hồ sơ cá nhân | Xem và sửa trường cá nhân được phép, gồm email nhận OTP và ảnh đại diện; đổi mật khẩu | chính chủ 📱 | chính chủ 📱 | BR-04 |

### SCH — School Setup (7) — chỉ SA ghi (riêng SCH-07: SA và HT cùng ghi), chỉ web

Màn hình Thiết lập chỉ dành cho SA; HT chỉ có màn hình Thông tin trường. HP và GV không có màn hình nào của SCH — cột Đọc thêm là dữ liệu họ dùng ở màn hình khác.

| ID | Feature | Nội dung chính | Đọc thêm | BR |
| --- | --- | --- | --- | --- |
| SCH-01 | Phân hiệu | Mã, tên, địa chỉ, ngừng hoạt động | HT, HP | BR-07 |
| SCH-02 | Tổ chuyên môn & Môn học | Tổ, môn thuộc tổ, viết tắt môn | HP, GV | BR-07 |
| SCH-03 | Năm học, Học kỳ & Ngày nghỉ | Năm học hiện hành, hai học kỳ, ngày nghỉ cấp trường hoặc phân hiệu | HP, GV | BR-08 |
| SCH-04 | Khung giờ tiết | Khung giờ dùng chung toàn trường, khóa là cặp (Buổi, Tiết) | HP, GV | BR-07 |
| SCH-05 | Lớp & Ghi danh | Lớp theo phân hiệu và năm học; ghi danh có hiệu lực; chuyển lớp giữa năm | HP, CN | BR-07, 09 |
| SCH-06 | Kết chuyển năm học | Sao chép danh sách lớp sang năm mới (gợi ý tên và khối tăng 1, sửa được trước khi tạo); chuyển nguyên lớp. Không tự xét lên lớp hay lưu ban, không sao chép thời khóa biểu hay trách nhiệm | — | BR-09 |
| SCH-07 | Thông tin trường | Tên trường, địa chỉ, điện thoại, email, website và logo. Đúng một bản; tên trường hiện ở thanh đường dẫn đầu trang, tiêu đề tab trình duyệt và lời chào Trang chủ, logo hiện ở Trang chủ; ai cũng đọc được, kể cả chưa đăng nhập. **HT và SA cùng ghi** | HP, GV | BR-27 |

### PPL — People & Responsibility (4)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| PPL-01 | Danh bạ cán bộ | Hồ sơ cán bộ, import Excel, gán viết tắt giáo viên | SA | HT, HP | BR-03, 10, 11 |
| PPL-02 | Danh bạ học sinh | Hồ sơ học sinh, import kèm ghi danh và phụ huynh, chuyển trường, tốt nghiệp | SA | HP, CN | BR-03, 10, 11 |
| PPL-03 | Danh bạ phụ huynh & Liên kết | Hồ sơ phụ huynh; mỗi học sinh một phụ huynh đại diện, một phụ huynh nhiều con; đổi phụ huynh đại diện | SA | CN, HP | BR-10 |
| PPL-04 | Trách nhiệm | Gán và thay phụ trách phân hiệu, tổ trưởng, chủ nhiệm; cảnh báo lớp chưa có chủ nhiệm | HT, SA | cán bộ | BR-09 |

### SCD — Timetable & Teaching Schedule (4)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| SCD-01 | Import & Phát hành thời khóa biểu | Tải mẫu điền sẵn, import theo học kỳ (file gồm các lớp cần đổi), đối chiếu viết tắt, bảng lỗi theo ô, sửa bản Nháp bằng kéo thả, kiểm xung đột toàn trường, xem trước, Phát hành và Phát hành lại | HP | lưới TKB: HP · lịch cá nhân: GV 📱, HS 📱, PH 📱 | BR-06, 12, 13, 14 |
| SCD-02 | Điều chỉnh lịch dạy | Đổi giáo viên của tiết, hủy tiết, thêm tiết bù | HP | GV 📱, HS 📱, PH 📱 | BR-13, 15 |
| SCD-03 | Xử lý giáo viên vắng | Khai báo vắng theo ngày hoặc theo tiết, xóa khai báo, danh sách tiết Chờ xử lý, gợi ý giáo viên rảnh, xử lý từng tiết: dạy thay hoặc hủy | HP | HP | BR-13, 16 |
| SCD-04 | Phụ trách chuyên môn | Màn hình chỉ đọc, tính từ thời khóa biểu đã phát hành: mỗi giáo viên một dòng, liệt kê các mục môn – lớp kèm số tiết mỗi tuần và tổng tiết; lọc theo phân hiệu và môn; xem toàn trường | — | HP | BR-14 |

### ATT — Attendance (1)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| ATT-01 | Điểm danh theo tiết | Giáo viên của tiết điểm danh và sửa trong ngày của tiết; School Admin nhập bù và sửa tiết đã qua trên cùng màn hình; nhãn có phép suy từ đơn nghỉ đã duyệt | GV của tiết 📱, SA | HS 📱, PH 📱, CN 📱, HP, HT, SA | BR-17, 18 |

### LRN — Learning (5)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| LRN-01 | Ngân hàng câu hỏi & Đề | Câu hỏi dùng chung theo môn, nháp AI, ghép đề, nhân bản đề của người khác | GV (của mình) | mọi GV | BR-20 |
| LRN-02 | Giao bài & Nộp bài | Giao đề mình sở hữu cho lớp mình phụ trách; học sinh làm bài, tự lưu nháp, nộp một lượt | GV giao · HS làm 📱 | HS 📱 | BR-21 |
| LRN-03 | Chấm bài & Công bố điểm | Chấm ngay khi nộp, thống kê câu sai nhiều nhất, công bố điểm cho cả lớp | GV phụ trách | GV · sau công bố: HS 📱, PH 📱 | BR-22 |
| LRN-04 | Phân tích học tập bằng AI | Tính chỉ số, ẩn danh, gọi AI, chỉnh – duyệt – công bố nhận xét, chạy lại khi lỗi | GV phụ trách | sau công bố: HS 📱, PH 📱 | BR-23 |
| LRN-05 | Giám sát cấp tổ | Tổ trưởng xem tiến độ bài tập của mọi lớp thuộc các môn trong tổ: trạng thái, hạn nộp, số nộp / sĩ số, đã công bố điểm chưa. Chỉ đọc, Backend chặn mọi đường ghi | — | TT | — |

### PAR — Parent Services (3)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| PAR-01 | Đơn xin nghỉ | Gửi, rút, duyệt, từ chối | PH 📱, CN 📱 | PH 📱, CN 📱, HP, SA | BR-19 |
| PAR-02 | Sổ liên lạc tuần | Chuyên cần, bài tập có hạn trong tuần, điểm và nhận xét đã công bố — tính trực tiếp, chỉ đọc | — | HS 📱, PH 📱, CN 📱 | BR-24 |
| PAR-03 | Hộp thư phụ huynh – giáo viên | Hội thoại 1–1 về một học sinh, chỉ văn bản | PH 📱, GV 📱 tạo và gửi | — | BR-25 |

### COM — Communication (2)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| COM-01 | Trung tâm thông báo | Thông báo trong ứng dụng, đã đọc / chưa đọc, kiểm quyền khi mở | hệ thống | mọi loại 📱 | BR-26 |
| COM-02 | Bảng tin | Bài đăng phạm vi Nội bộ hoặc Toàn trường, kèm tối đa 5 ảnh | mọi cán bộ (web) | Nội bộ: cán bộ 📱 · Toàn trường: mọi người 📱 | BR-27 |

### ADM — Administration & Oversight (3)

| ID | Feature | Nội dung chính | Đọc | BR |
| --- | --- | --- | --- | --- |
| ADM-01 | Dashboard | Bốn số quy mô và biểu đồ học sinh theo khối theo phạm vi; Principal lọc được theo phân hiệu và có bảng so sánh phân hiệu; số nào có màn hình gốc thì bấm vào mở ra (mục 5.1) | HT, HP | — |
| ADM-02 | Audit Log | Ba nhóm sự kiện, chỉ ghi thêm, lọc theo nhóm, người và thời gian | SA | BR-28 |
| ADM-03 | Xuất dữ liệu | Ba mục xuất Excel (mục 5.2) | theo từng mục | — |

### 5.1. Dashboard (ADM-01)

**Phạm vi:** Principal mặc định xem **toàn trường** và **lọc được theo từng phân hiệu**; khi xem toàn trường có thêm bảng so sánh các phân hiệu, bấm một hàng thì lọc theo phân hiệu đó. Vice Principal xem cố định phân hiệu mình phụ trách, không có bộ lọc (phụ trách nhiều phân hiệu thì lọc được trong số đó). School Admin không có Dashboard. Một tiết, đơn hay bài thuộc phân hiệu nào là theo **phân hiệu của lớp** liên quan; cán bộ tính theo **phân hiệu chính**.

**Trang chủ không phải Dashboard.** Trang chủ là màn đầu tiên của mọi loại tài khoản, mang tính chào đón: lời chào theo buổi, tên trường và năm học hiện hành. Cán bộ thấy thêm **Hôm nay** (khung giờ tiết của trường, đánh dấu tiết đang diễn ra theo giờ thực; cán bộ có tiết — trừ School Admin — thấy lớp và môn mình dạy ở từng tiết; ngày nghỉ cấp trường hoặc Chủ nhật thì báo trường nghỉ), **Năm học** (học kỳ hiện tại, đang ở tuần thứ mấy) và **Ngày nghỉ sắp tới** (bốn ngày gần nhất). Học sinh và phụ huynh thấy thông tin liên hệ của trường. Trang chủ không có số liệu thống kê, lối tắt hay bảng tin; số liệu chỉ nằm ở Dashboard.

**Số quy mô** (năm học hiện hành, tại hôm nay):

| Số | Đếm gì | Bấm vào ra gì |
| --- | --- | --- |
| Lớp đang hoạt động | Lớp đang hoạt động của năm hiện hành | — |
| Học sinh đang học | Học sinh có ghi danh hiệu lực hôm nay ở các lớp trên | — |
| Cán bộ đang công tác | Hồ sơ cán bộ đang hoạt động, theo phân hiệu chính | Danh bạ cán bộ |
| Tiết dạy hôm nay | Tiết Đã xếp lịch có ngày là hôm nay | Lịch dạy |

Kèm biểu đồ cột **Học sinh theo khối** (một chuỗi, rê chuột thấy số học sinh và số lớp của khối).

Tiết Chờ xử lý không nằm trên Dashboard — Vice Principal theo dõi ở danh sách tiết Chờ xử lý của SCD-03. Lớp chưa có chủ nhiệm cũng không — cảnh báo nằm trên màn hình trách nhiệm của PPL-04.

Bảng so sánh phân hiệu gồm: Phân hiệu · Lớp · Học sinh · Cán bộ.

### 5.2. Xuất Excel (ADM-03)

| Mục | Nguồn dữ liệu | Ai xuất |
| --- | --- | --- |
| Điểm danh theo lớp, theo khoảng ngày | Bản ghi điểm danh, kèm nhãn cuối suy ra (BR-18) | Chủ nhiệm (lớp mình), Vice Principal (phân hiệu mình) |
| Thời khóa biểu hiện hành, theo phân hiệu và khoảng ngày | **Lịch dạy**, không phải phiên bản thời khóa biểu — để phản ánh cả điều chỉnh tay | Vice Principal (phân hiệu mình) |
| Kết quả bài tập theo lớp | Lượt làm bài | Teacher đang phụ trách chuyên môn |

Danh sách tài khoản vừa tạo kèm mật khẩu tạm **không phải một mục xuất**: file sinh tức thời trong kết quả của thao tác tạo tài khoản, không lưu, không xuất lại (BR-04).

---

## 6. Dữ liệu và trạng thái

Lược đồ CSDL thiết kế sau, từ tài liệu này. Mục này chốt **trạng thái nào được lưu** và **trạng thái nào luôn tính lúc đọc**.

### 6.1. Trạng thái được lưu

| Đối tượng | Giá trị lưu | Ghi chú |
| --- | --- | --- |
| Tài khoản | Chờ đổi mật khẩu / Đang hoạt động / Đã khóa | Chờ đổi mật khẩu = đã cấp mật khẩu tạm, chưa đổi lần đầu |
| Tiết học | Đã xếp lịch / Chờ xử lý / Đã hủy | Không có trạng thái "đã dời" — đổi giờ là hủy + thêm bù. Tiết Chờ xử lý để trống giáo viên (BR-16) |
| Nguồn tiết | Sinh tự động / Chỉnh tay | Sinh tự động do Phát hành tạo; mọi thao tác tay chuyển thành Chỉnh tay |
| Điểm danh | Có mặt / Muộn / Vắng | Đúng ba giá trị giáo viên gửi |
| Đơn xin nghỉ | Chờ duyệt / Đã duyệt / Đã từ chối / Đã rút | |
| Câu hỏi | Nháp / Sẵn dùng | Nháp = nháp AI chưa xác nhận |
| Bài được giao | Nháp / Đã giao | |
| Nhận xét AI | Nháp / Đã duyệt / Đã công bố / Lỗi | |
| Phiên bản thời khóa biểu | Nháp / Đã phát hành | Mỗi phiên bản là hồ sơ của một lần import, đọc lại được như lịch sử |
| Bài đăng | Đã đăng / Đã ẩn | Không có trạng thái nháp |
| Học sinh | Đang học / Đã chuyển trường / Đã tốt nghiệp | |

### 6.2. Trạng thái suy ra — không lưu

**Mọi truy vấn nghiệp vụ đi qua cùng một cách tính, không tự viết lại điều kiện.** "Hôm nay" luôn theo giờ Việt Nam.

| Trạng thái | Điều kiện |
| --- | --- |
| Tiết **đã diễn ra** | Đã xếp lịch **và** (đã có ít nhất một bản ghi điểm danh **hoặc** ngày của tiết < hôm nay) |
| Tiết **bị khóa** | Đã diễn ra, hoặc ngày của tiết < hôm nay. Tiết bị khóa không đổi giáo viên, không hủy |
| Điểm danh **có phép** | Vắng **và** có đơn Đã duyệt của học sinh phủ ngày của tiết, cùng buổi hoặc cả ngày |
| Điểm danh **không phép** | Vắng và không có đơn như trên |
| Bài **đã đóng** | Đã giao **và** thời điểm hiện tại > hạn nộp |
| Điểm **đã công bố** | Có thời điểm công bố điểm |
| Ghi danh **hiệu lực tại ngày D** | Ngày bắt đầu ≤ D **và** (ngày kết thúc để trống **hoặc** ≥ D); để trống nghĩa là tới hết năm học |

Nhãn cuối của điểm danh có bốn giá trị: **Có mặt / Muộn / Vắng có phép / Vắng không phép**. Đơn duyệt muộn tự đổi nhãn các buổi vắng đã ghi, không cần chạy lại gì.

---

## 7. Quy tắc nghiệp vụ

**28 quy tắc.** Những ràng buộc cố tình không đặt nằm ở mục 7.8.

### 7.1. Danh tính và quyền

**BR-01 — Loại tài khoản.** Sáu loại ở mục 2.1, mỗi tài khoản mang đúng một loại. **Một hồ sơ có đúng một tài khoản.** Cấp sai loại thì đổi loại tài khoản — một thao tác, ghi Audit, cấp lại mật khẩu tạm và đưa tài khoản về Chờ đổi mật khẩu; hồ sơ và mọi lịch sử nghiệp vụ giữ nguyên vì chúng gắn với hồ sơ, không gắn với tài khoản. Quyền = loại tài khoản + trách nhiệm hoặc phụ trách chuyên môn đang hiệu lực + đối tượng + trạng thái đối tượng. Không có bảng quyền động, không đa vai trò, không chọn vai trò sau đăng nhập.

**BR-02 — School Admin.** Vượt phạm vi: thao tác mọi phân hiệu, lớp, môn mà không cần phụ trách chuyên môn. Không làm việc đứng tên người khác: không làm hành vi ở mục 2.4, không giả đăng nhập, không duyệt đơn nghỉ, không đọc hội thoại, không làm thời khóa biểu. Riêng bảng tin, School Admin (cùng Principal) ẩn được bài của người khác kèm lý do (BR-27). Không vượt điều kiện nghiệp vụ: vẫn bị chặn khi sửa tiết bị khóa, khi dữ liệu sai mẫu.

**BR-03 — Mã hồ sơ và tên đăng nhập.** Hệ thống sinh **mã hồ sơ** khi tạo hồ sơ, không nhập tay, không sửa: tiền tố `HS` / `CB` / `PH` + 6 chữ số lấy từ **một bộ đếm chung cho cả ba loại**, nên không có hai hồ sơ nào trùng phần số. **Tên đăng nhập** = tên + chữ cái đầu họ và đệm + tiền tố + phần số, bỏ dấu, chữ thường — Trần Đức Kiên mã `HS186194` → `kientdhs186194`. Phần số duy nhất nên tên đăng nhập không trùng, không có cơ chế hậu tố. Người vừa là cán bộ vừa là phụ huynh có hai hồ sơ, hai mã, hai tên đăng nhập.

**BR-04 — Mật khẩu.** School Admin tạo tài khoản đơn lẻ cho hồ sơ chưa có tài khoản, hoặc chọn nhiều dòng trên danh bạ để tạo hàng loạt: danh bạ học sinh → Student, phụ huynh → Parent, cán bộ → chọn một loại cho cả lô. Mỗi tài khoản nhận **mật khẩu tạm** sinh ngẫu nhiên, hiển thị **một lần** trong kết quả tạo kèm file Excel tải ngay. Tài khoản ở Chờ đổi mật khẩu; lần đăng nhập đầu bắt buộc đổi, sau đó thành Đang hoạt động. Mật khẩu chỉ lưu dạng băm.

**Quên mật khẩu:** nhập tên đăng nhập → nếu tài khoản Đang hoạt động và hồ sơ có email thì gửi **OTP 6 chữ số** tới email đó → nhập đúng thì đặt mật khẩu mới. OTP hết hạn sau 10 phút, sai 5 lần thì mất hiệu lực, gửi lại sau tối thiểu 60 giây; mỗi tài khoản chỉ có một OTP còn hiệu lực. Màn hình luôn trả cùng một thông điệp, không tiết lộ tài khoản có tồn tại hay có email không. **Hồ sơ không có email:** School Admin cấp lại mật khẩu tạm. Người dùng sửa email của mình ở hồ sơ cá nhân như một trường thông tin bình thường.

**BR-05 — Khóa tài khoản.** Phiên đăng nhập dùng access token sống 15 phút và refresh token sống 7 ngày. Web: refresh token trong cookie HttpOnly, access token chỉ giữ trong bộ nhớ trang; Mobile: nhận cả hai token trong body. Tài khoản Đã khóa không đăng nhập được; phiên đang mở bị từ chối ở request kế tiếp, vì Backend đọc trạng thái tài khoản mỗi request. School Admin khóa và mở tay. Đăng xuất **thu hồi** phiên trên **thiết bị hiện tại**: token của phiên đó mất hiệu lực ngay ở Backend, không chỉ bị client bỏ đi; người dùng không có "đăng xuất mọi thiết bị". Khóa tài khoản, School Admin reset mật khẩu hoặc đổi loại tài khoản thì Backend thu hồi mọi phiên của tài khoản đó; người dùng tự đổi mật khẩu thì các thiết bị khác phải đăng nhập lại. Hồ sơ và lịch sử giữ nguyên.

**Khóa tài khoản không bị chặn bởi lịch dạy.** Trong cùng thao tác khóa (một transaction), với tài khoản loại Teacher: mọi tiết Đã xếp lịch chưa bị khóa của giáo viên đó từ hôm nay trở đi, ở bất kỳ phân hiệu nào, chuyển sang **Chờ xử lý** (Chỉnh tay, **để trống giáo viên**) và Vice Principal của phân hiệu chứa tiết nhận thông báo để xử lý theo BR-16; với mọi tài khoản cán bộ, mọi trách nhiệm đang hiệu lực của người đó (phụ trách phân hiệu, tổ trưởng, chủ nhiệm) được **đặt ngày kết thúc là ngày khóa** và **quyền tương ứng mất ngay lúc khóa** (BR-09). Mở khóa lại không khôi phục các trách nhiệm đã đóng; muốn có lại thì gán mới. Trước khi khóa, màn hình cảnh báo số tiết và phân hiệu bị ảnh hưởng, cùng danh sách trách nhiệm sẽ đóng. Đổi loại tài khoản cũng đóng trách nhiệm, và chuyển tiết nếu loại cũ là Teacher, theo cùng cách.

Học sinh chuyển trường hoặc tốt nghiệp thì tài khoản học sinh tự khóa trong cùng thao tác.

**BR-06 — Ai được dạy.** Người làm giáo viên của tiết hoặc xuất hiện trong file thời khóa biểu phải có **tài khoản loại Teacher không ở trạng thái Đã khóa** — Chờ đổi mật khẩu vẫn hợp lệ, vì đầu năm giáo viên chưa kịp đăng nhập lần đầu. Điều kiện này kiểm **khi gán** giáo viên cho tiết hoặc khi import file; tiết Chờ xử lý để trống giáo viên nên không có gì để kiểm, và giáo viên được chọn cho tiết đó phải qua kiểm tra này. Principal, Vice Principal, School Admin không dạy.

### 7.2. Danh mục, hồ sơ, khoảng hiệu lực

**BR-07 — Danh mục nền.** Phân hiệu có mã và tên duy nhất — tên là thứ các file import tra cứu. Môn học thuộc một tổ chuyên môn và có **viết tắt duy nhất toàn trường, chỉ gồm chữ và số** (vì dấu `-` là dấu tách trong ô `MÔN-GV`). Khung giờ tiết **dùng chung toàn trường**, khóa là cặp **(Buổi, Tiết)** — Sáng tiết 1 và Chiều tiết 1 là hai khung khác nhau; số khung mỗi buổi do trường tự khai, không cố định; mọi tiết học kể cả tiết bù phải nằm đúng một khung. Lớp thuộc một phân hiệu và một năm học, tên lớp duy nhất trong phạm vi đó, khối 6–9. Danh mục đã có dữ liệu tham chiếu thì **ngừng hoạt động**, không xóa.

**BR-08 — Năm học, học kỳ, ngày nghỉ.** Đúng một năm học được đánh dấu hiện hành. Mỗi năm có đúng hai học kỳ, nằm trong năm học, không chồng nhau, kỳ 1 trước kỳ 2. Học kỳ chỉ là mốc lọc và mốc Phát hành; không có quy trình chuyển kỳ. Ngày nghỉ gắn năm học, cấp **toàn trường** hoặc **một phân hiệu**. Ngày nghỉ khai trước khi Phát hành thì Phát hành bỏ qua; khai sau thì tiết đã sinh không tự đổi, Vice Principal hủy từng tiết (BR-15).

**BR-09 — Khoảng hiệu lực.** Trách nhiệm và ghi danh đều có ngày bắt đầu và ngày kết thúc; **để trống ngày kết thúc nghĩa là còn hiệu lực** (với ghi danh là tới hết năm học). Mỗi đích có **tối đa một bản đang hiệu lực**:

- một phân hiệu — một Vice Principal phụ trách
- một tổ chuyên môn — một tổ trưởng
- một lớp — một chủ nhiệm
- một học sinh — một ghi danh mở trong một năm học

**Thay người hoặc chuyển lớp = một thao tác, một transaction:** đặt ngày kết thúc của bản cũ là **ngày liền trước** ngày hiệu lực của bản mới, rồi mở bản mới — hai khoảng không được chạm nhau một ngày nào. Bản cũ giữ nguyên làm lịch sử; dữ liệu đã phát sinh không bị đụng tới.

**Kết thúc không do thay người** (khóa tài khoản, đổi loại tài khoản, bấm kết thúc trách nhiệm): ngày kết thúc là **ngày thao tác** và quyền **mất ngay tại thời điểm thao tác**, không đợi hết ngày. Khoảng hiệu lực chỉ dùng để tra cứu theo ngày; kiểm quyền tại thời điểm xử lý dựa trên việc bản ghi đã có ngày kết thúc từ hôm nay trở về trước hay chưa. Mở khóa tài khoản không mở lại bản cũ; gán lại trong cùng ngày thì tạo **bản mới** bắt đầu từ hôm nay, không sửa bản cũ; bản cũ và bản mới có thể cùng nằm trong một ngày nhưng chỉ có một bản đang hiệu lực tại mọi thời điểm.

Phụ trách phân hiệu chỉ gán cho Vice Principal; tổ trưởng và chủ nhiệm chỉ gán cho Teacher.

**BR-10 — Hồ sơ người.** Ba loại hồ sơ: học sinh, cán bộ, phụ huynh. **Họ tên, ngày sinh và số định danh cá nhân (12 chữ số) bắt buộc** với hồ sơ học sinh và cán bộ; số định danh **duy nhất toàn trường** và là khóa nhận diện khi import. Số định danh không sửa được sau khi tạo. Cán bộ bắt buộc có **phân hiệu chính**, có thể có **viết tắt giáo viên duy nhất toàn trường, chỉ gồm chữ và số**; không có viết tắt thì không xuất hiện được trong file thời khóa biểu. Phụ huynh bắt buộc có **số điện thoại** — đó là khóa nhận diện của phụ huynh.

**Mỗi học sinh có đúng một phụ huynh đại diện; một phụ huynh liên kết được nhiều học sinh.** Đổi phụ huynh đại diện làm trên giao diện.

### 7.3. Import Excel

**BR-11 — Import Excel.** Chỉ hai file hồ sơ: cán bộ và học sinh (file thời khóa biểu theo BR-12). **Import một bước:** người dùng gửi file, hệ thống validate **toàn bộ file** rồi ghi, tất cả trong một transaction. Có bất kỳ lỗi nào thì **không ghi gì**, trả bảng lỗi kèm số dòng, cột, giá trị gốc; người dùng sửa file và gửi lại.

**Nhận diện người đã tồn tại:** học sinh và cán bộ theo **Số định danh cá nhân** (cột bắt buộc, duy nhất; hai người trùng họ tên và ngày sinh vẫn là hai người nếu khác số định danh); phụ huynh theo **số điện thoại**. Hai dòng trong cùng file trùng khóa nhận diện là lỗi. Người đã tồn tại thì **bỏ qua cả dòng** và báo ở mục "Đã có, bỏ qua" — import **không cập nhật** hồ sơ cũ; sửa hồ sơ làm trên giao diện. Nhờ vậy chạy lại cùng một file không tạo bản trùng.

**File học sinh** có hai ô meta bắt buộc là Năm học và Phân hiệu, áp dụng cho mọi dòng. Một dòng = một học sinh kèm phụ huynh đại diện, tạo tối đa bốn thứ: hồ sơ học sinh, ghi danh (chỉ khi cột Tên lớp có giá trị; ngày hiệu lực = ngày bắt đầu năm học), hồ sơ phụ huynh, và liên kết. Các dòng cùng số điện thoại dùng chung một hồ sơ phụ huynh và liên kết nhiều con. Lỗi: tên lớp không khớp lớp của (phân hiệu, năm học); cùng số điện thoại mà họ tên phụ huynh khác nhau. Hệ thống **không tự tạo lớp**.

Danh sách cột bắt buộc và tùy chọn của từng file nằm trong `MSMS_Excel_Import_Templates.xlsx`.

### 7.4. Thời khóa biểu và lịch dạy

**BR-12 — File thời khóa biểu.** Một bố cục Excel lưới duy nhất: hàng là Thứ × Buổi × Tiết (Thứ 2–7), cột là lớp. Ba ô meta bắt buộc: Phân hiệu, Năm học, Học kỳ. Mỗi ô ghi **`MÔN-GV`** (viết tắt môn – viết tắt giáo viên); ô trống là tiết trống. Đối chiếu không phân biệt hoa thường, bỏ khoảng trắng thừa; dấu `-` đầu tiên là dấu tách.

**File mẫu do hệ thống sinh, không phải file tĩnh.** Vice Principal chọn phân hiệu + học kỳ và bấm "Tải mẫu"; Backend dựng file ngay lúc đó: số hàng theo **khung giờ đã khai**, số cột theo **lớp của (phân hiệu, năm học)**, ba ô meta điền sẵn, và **nội dung điền sẵn theo phiên bản Đã phát hành gần nhất** của (phân hiệu, học kỳ) đó — chưa có phiên bản nào thì để trống. Nhờ vậy sửa lịch vài lớp là tải về, sửa vài ô, xóa các cột lớp không đổi, tải lên.

**File chỉ cần gồm các lớp muốn đổi.** Đọc các cột lớp từ cột D sang phải tới ô tiêu đề trống đầu tiên. Mỗi lớp có trong file được ghi đè hoàn toàn khi Phát hành — cột lớp để trống toàn bộ nghĩa là lớp không có tiết nào; lớp không có trong file giữ nguyên lịch hiện hành.

Lỗi kèm địa chỉ ô: viết tắt môn hoặc viết tắt giáo viên không khớp; giáo viên không đủ điều kiện dạy (BR-06); tên lớp không thuộc (phân hiệu, năm học); hai cột cùng một tên lớp; hàng tiêu đề bị sửa; hàng có (Buổi, Tiết) không nằm trong khung giờ đã khai; một giáo viên xuất hiện ở hai lớp trong cùng (Thứ, Buổi, Tiết). **Có một lỗi là từ chối cả file**, không import một phần. File hợp lệ được lưu thành một phiên bản **Nháp** chỉ chứa các lớp có trong file.

**Sửa bản Nháp.** Trước khi Phát hành, Vice Principal sửa bản Nháp ngay trên lưới, không cần import lại. Chỉ sửa được phiên bản đang Nháp, và chỉ trong các lớp của phiên bản đó. Bốn thao tác:

- **Kéo tiết** sang ô khác trong cùng cột lớp: thả vào ô trống thì chuyển tiết, thả vào ô có tiết thì đổi chỗ hai tiết. Không kéo sang lớp khác.
- **Thêm tiết** vào ô trống: chọn một môn đang dùng của trường và một giáo viên.
- **Đổi giáo viên** của tiết.
- **Xóa tiết.**

Giáo viên được chọn phải đủ điều kiện như file import (BR-06, BR-12). Thao tác gây xung đột (BR-13) **vẫn được lưu** và ô xung đột được đánh dấu tại ô, để Vice Principal đi qua trạng thái trung gian khi đổi nhiều tiết; còn xung đột thì Phát hành bị chặn (BR-14). Mỗi thao tác lưu ngay vào bản Nháp; ô khác với nội dung file import được đánh dấu, và khôi phục được toàn bộ bản Nháp về nội dung file import. Sửa bản Nháp không đụng tới lịch dạy, không tạo thông báo và không ghi Audit.

**BR-13 — Xung đột lịch.** Trùng lịch = cùng **ngày** và cùng **khung giờ**. Trong toàn trường, mỗi lớp có tối đa một tiết Đã xếp lịch hoặc Chờ xử lý, và mỗi giáo viên có tối đa một tiết Đã xếp lịch, tại một (ngày, khung giờ). Tiết Chờ xử lý để trống giáo viên nên không chiếm chỗ của ai (BR-16). Áp dụng cho bản Nháp (BR-12), Phát hành, mọi điều chỉnh tay, và lọc gợi ý giáo viên rảnh. Giáo viên dạy nhiều phân hiệu được kiểm chung. Khi kiểm bản Nháp, các lớp không có trong phiên bản vẫn được tính theo lịch hiện hành, vì chúng không bị ghi đè.

**BR-14 — Phát hành và Phát hành lại.** Vice Principal Phát hành một phiên bản Nháp. Khi bấm Phát hành, hệ thống chạy **xem trước** trong hộp xác nhận và hiển thị: số tiết sẽ xóa và sẽ tạo, nhóm theo lớp và ngày; **cảnh báo số tiết Chỉnh tay, tiết bù và tiết Chờ xử lý sẽ bị xóa**, người dùng phải xác nhận; **danh sách lớp của (phân hiệu, năm học) vẫn không có tiết nào** trong học kỳ sau Phát hành (cảnh báo, không chặn); và danh sách xung đột giáo viên. Mỗi dòng xung đột ghi rõ **lớp và phân hiệu đang chiếm chỗ giáo viên**; Vice Principal xử lý bằng cách đổi sang giáo viên khác cho ô trùng trong bản Nháp.

**Còn xung đột giáo viên là chặn Phát hành.** Phát hành cũng bị chặn khi bản Nháp có lớp đã ngừng hoạt động hoặc giáo viên không còn đủ điều kiện dạy (BR-06); thông báo nêu rõ lớp hoặc giáo viên để xử lý.

Phát hành chạy trong **một transaction**, hai bước:

1. **Xóa** mọi tiết của **các lớp có trong phiên bản**, trong học kỳ đó, **từ ngày mai trở đi** — **ghi đè sạch**, không giữ lại tiết đã chỉnh tay. Lớp không có trong phiên bản không bị đụng tới.
2. **Sinh lại** từ phiên bản, khoảng từ **max(ngày mai, ngày bắt đầu học kỳ)** đến hết học kỳ, chỉ các ngày không phải ngày nghỉ của toàn trường hoặc của phân hiệu đó.

**Tiết đã qua và tiết đã có điểm danh không bao giờ bị đụng tới.**

Xem trước **không được lưu lại**; lúc bấm Phát hành hệ thống tính lại và thực hiện, nên con số cuối cùng mới là con số ghi vào phiên bản. Phát hành thành công: phiên bản thành Đã phát hành; **phiên bản trước không bị đổi trạng thái** — mỗi phiên bản là hồ sơ của một lần import, đọc lại được như lịch sử. Hệ thống gửi **một thông báo chung** (BR-26) và ghi Audit (BR-28). **Phát hành lại** là import file mới cho cùng (phân hiệu, học kỳ) rồi Phát hành theo đúng hai bước trên.

**Nguồn sự thật của thời khóa biểu hiện hành là lịch dạy**, không phải phiên bản — vì chỉ lịch dạy mới phản ánh các điều chỉnh tay ở BR-15 và BR-16. Mọi màn hình xem lịch và mục xuất Excel đều đọc từ lịch dạy.

**Phụ trách chuyên môn** (giáo viên – môn – lớp) không nhập tay và không lưu: hệ thống tính từ thời khóa biểu mỗi khi cần. Với mỗi lớp, lấy phiên bản **Đã phát hành mới nhất của lớp** trong học kỳ hiện hành — lớp không có trong một lần Phát hành sau vẫn theo phiên bản trước đó của chính nó. Giáo viên có tiết của một môn trong phiên bản ấy **đang phụ trách chuyên môn** môn đó ở lớp đó; số tiết mỗi tuần là số ô của (lớp, môn, giáo viên) trong phiên bản. Một (lớp, môn) có thể có nhiều giáo viên. Chưa Phát hành thì chưa có phụ trách chuyên môn. Điều chỉnh lịch và dạy thay (BR-15, BR-16) chỉ đổi lịch dạy nên không đổi phụ trách chuyên môn; muốn đổi thì sửa thời khóa biểu rồi Phát hành lại.

Quyền với bài tập của một (lớp, môn) **luôn theo phụ trách chuyên môn đang hiệu lực**, không theo người tạo: Phát hành đổi giáo viên thì người mới tiếp quản, người cũ mất quyền ngay.

Ghi Audit (BR-28) và gửi một thông báo gộp cho **mỗi giáo viên có tiết thực sự thay đổi** sau khi sinh lại — giáo viên có lịch không đổi thì không nhận thông báo.

**BR-15 — Điều chỉnh lịch.** Vice Principal của phân hiệu thao tác trên tiết **chưa bị khóa** (mục 6.2). Mọi thao tác đặt nguồn tiết thành **Chỉnh tay**, kiểm BR-13, tạo thông báo cho giáo viên liên quan và học sinh của lớp, và ghi Audit.

- **Đổi giáo viên:** thay giáo viên của tiết. Không đổi phụ trách chuyên môn (BR-14).
- **Hủy:** tiết → Đã hủy. Tiết Đã hủy không dạy, không điểm danh, không tính chuyên cần.
- **Thêm tiết bù:** tạo tiết mới ở một (ngày, khung giờ) trống của lớp, chọn môn và giáo viên; ngày phải là ngày học của phân hiệu (không Chủ nhật, không ngày nghỉ), ≥ hôm nay, trong một học kỳ của năm học. Tiết bù là một tiết thêm mới, không liên kết với tiết đã hủy nào.

**Đổi giờ một tiết = hủy tiết cũ + thêm tiết bù**; không có thao tác "dời" riêng.

**BR-16 — Giáo viên vắng.** Vice Principal có tiết của giáo viên đó trong phân hiệu mình khai báo, theo một trong hai cách: **theo ngày** (từ ngày – đến ngày, vắng cả ngày) hoặc **theo tiết** (một ngày, chọn các tiết vắng trong ngày, liền hay rời đều được), kèm lý do. Đây **không phải đơn xin nghỉ của cán bộ** — MSMS chỉ ghi nhận hệ quả lên lịch dạy.

Khai báo **không tự đổi lịch** và **chỉ tác động tiết ở phân hiệu của người khai**: mọi tiết Đã xếp lịch chưa bị khóa của giáo viên đó trong phạm vi khai báo, ở phân hiệu mình phụ trách, chuyển sang **Chờ xử lý** (Chỉnh tay) và **để trống giáo viên**. Giáo viên dạy nhiều phân hiệu vắng thì Vice Principal mỗi phân hiệu khai phần của mình. Tiết trống không chiếm chỗ của giáo viên nào khi kiểm xung đột (BR-13). Mỗi tiết ghi lại khai báo đã đưa nó về Chờ xử lý. Khóa hoặc đổi loại tài khoản Teacher (BR-05) cũng đưa tiết về Chờ xử lý và để trống giáo viên theo cách này, nhưng không gắn với khai báo nào; Vice Principal của phân hiệu chứa tiết nhận thông báo trong trường hợp đó. Vice Principal chọn cho từng tiết Chờ xử lý một trong hai cách:

1. **Chọn giáo viên dạy thay** → Đã xếp lịch với giáo viên được chọn, lấy từ danh sách gợi ý bên dưới.
2. **Hủy** → Đã hủy. Muốn dạy lại thì thêm tiết bù (BR-15).

Tiết Chờ xử lý **không điểm danh được**. Tiết Chờ xử lý cũng bị khóa khi qua ngày (mục 6.2): không còn trong danh sách của SCD-03, không dạy thay, không hủy được nữa.

**Xóa khai báo vắng** theo kiểu tất cả hoặc không: chỉ xóa được khi **chưa có tiết nào** của khai báo bị xử lý (dạy thay, hủy), giáo viên **vẫn đủ điều kiện dạy** (BR-06) và **không trùng lịch** ở tiết nào (BR-13). Khi đó mọi tiết của khai báo từ hôm nay trở đi trở về giáo viên, trạng thái Đã xếp lịch; tiết đã qua ngày bị khóa nên giữ nguyên. Không đạt thì báo lỗi nêu rõ nguyên nhân và không xóa gì; Vice Principal xử lý các tiết còn lại rồi xóa lại. Tiết trống do khóa hoặc đổi loại tài khoản không thuộc khai báo nào nên không tự khôi phục.

**Gợi ý giáo viên rảnh:** Teacher đủ điều kiện dạy (BR-06), không có tiết Đã xếp lịch tại (ngày, khung giờ) đó ở bất kỳ phân hiệu nào (BR-13), không có khai báo vắng phủ ngày và tiết đó. Trả danh sách sắp theo tên, tối đa 10 người; **không xếp hạng ưu tiên** — Vice Principal tự chọn.

### 7.5. Điểm danh và đơn xin nghỉ

**BR-17 — Điểm danh.** Chỉ **giáo viên của tiết** điểm danh, chỉ trên tiết Đã xếp lịch, chỉ **trong ngày của tiết** (tới 23:59). Điểm danh dựa vào tiết, không dựa vào phụ trách chuyên môn — giáo viên dạy thay điểm danh được. Danh sách là học sinh có ghi danh hiệu lực ở lớp vào ngày của tiết. Giáo viên gửi một trong **ba giá trị**: Có mặt, Muộn, Vắng; kèm ghi chú tùy chọn. Mỗi học sinh có đúng một bản ghi cho mỗi tiết; lưu lại là cập nhật bản ghi đó, có hiệu lực ngay, không có bước chốt.

Sau ngày của tiết giáo viên không sửa được nữa. **Chỉ School Admin** nhập bù hoặc sửa, trên **cùng màn hình điểm danh**, cho tiết Đã xếp lịch có ngày ≤ hôm nay. Bản ghi lưu **người nhập, thời điểm, cờ nhập bù**. Vice Principal thấy số liệu nhưng không sửa. School Admin tìm tiết cần nhập bù bằng cách chọn lớp và khoảng ngày trên màn hình điểm danh.

**BR-18 — Có phép / không phép.** **Không lưu, luôn suy ra** (mục 6.2): Vắng + có đơn Đã duyệt của học sinh phủ ngày của tiết và đúng buổi ("sáng" = tiết khung Sáng, "chiều" = tiết khung Chiều, "cả ngày" = mọi tiết) → **có phép**; còn lại → **không phép**. Đơn duyệt muộn tự đổi nhãn các buổi vắng đã ghi, không cần chạy lại gì. **Đơn không tự sinh vắng:** học sinh có đơn mà vẫn đi học thì ghi Có mặt. Vì BR-19 không cho xin nghỉ cho ngày đã qua, buổi vắng mà phụ huynh báo muộn luôn là **không phép** — giới hạn này được chấp nhận.

**BR-19 — Đơn xin nghỉ.** Chỉ Parent gửi, chỉ cho con được liên kết đang có ghi danh mở. Đơn chọn từ ngày – đến ngày và buổi (cả ngày / sáng / chiều), áp dụng cho mọi ngày trong khoảng; kèm lý do. **Ngày bắt đầu phải ≥ hôm nay**, trong năm học hiện hành — nghỉ đã xảy ra rồi thì không xin bù đơn; giáo viên đã ghi vắng và School Admin sửa bằng BR-17.

**Hai đơn của cùng một học sinh xung đột khi chồng ngày *và* chồng buổi.** Một học sinh không được có hai đơn Chờ duyệt hoặc Đã duyệt xung đột với nhau. Hai đơn cùng buổi thì chồng; một đơn "cả ngày" chồng với mọi đơn khác. Nhờ vậy xin nghỉ **sáng** và **chiều** cùng một ngày bằng hai đơn riêng là hợp lệ.

Parent rút được khi còn Chờ duyệt. Khi gửi, đơn ghi lại **lớp học sinh đang có ghi danh mở**. Người duyệt là **chủ nhiệm đang hiệu lực của lớp đó tại lúc duyệt** — đổi chủ nhiệm khi đơn còn chờ thì người mới thấy đơn; nhờ quy tắc ngày liền trước ở BR-09, tại mọi thời điểm chỉ có đúng một người như vậy. Lớp chưa có chủ nhiệm: đơn nằm chờ tới khi có chủ nhiệm được gán (PPL-04). Quyết định là cuối cùng; bị từ chối thì gửi đơn mới. Kết quả tạo thông báo cho Parent.

### 7.6. Bài tập và phân tích

**BR-20 — Câu hỏi và đề.** Câu hỏi thuộc một môn, 4 lựa chọn, đúng 1 đáp án, giải thích tùy chọn. Soạn thủ công **hoặc từ nháp AI**: giáo viên chọn môn, khối, chủ đề, số câu → AI trả về các câu Nháp gắn cờ AI → giáo viên sửa và xác nhận thành **Sẵn dùng**. Chỉ câu Sẵn dùng mới đưa vào đề được.

Ngân hàng câu hỏi **dùng chung toàn trường**: mọi Teacher xem và dùng câu Sẵn dùng của mọi môn; **chỉ người tạo sửa** được. Câu hỏi đã nằm trong một đề **đã giao** thì không sửa được nữa — chỉ còn **ẩn khỏi ngân hàng**; ẩn không ảnh hưởng đề cũ; muốn phiên bản khác thì soạn câu mới.

Đề gồm các câu Sẵn dùng cùng môn, có thứ tự và điểm từng câu. **Đề thuộc về người tạo:** chỉ người tạo sửa hoặc xóa đề chưa khóa. Mọi Teacher đều xem được đề trong danh sách và **nhân bản** đề của người khác; bản nhân bản thuộc về người bấm nhân bản và chưa khóa. **Giao lần đầu thì đề khóa vĩnh viễn** — từ đó không ai sửa được, kể cả người tạo. Giáo viên tiếp quản lớp giữa năm vì vậy dùng lại được đề của người tiền nhiệm bằng cách nhân bản.

**BR-21 — Giao bài và nộp bài.** Teacher **đang phụ trách chuyên môn** ở (lớp, môn của đề) giao **đề mình sở hữu** cho lớp đó: chọn thời điểm mở (mặc định ngay) và hạn nộp (> hiện tại). Bài ở Nháp sửa và xóa được; chuyển **Đã giao** thì khóa đề, tạo thông báo cho học sinh của lớp, và **không sửa hay gia hạn được nữa**. Muốn cho làm lại: nhân bản đề, giao như bài mới.

**Danh sách được giao:** lúc bài chuyển sang Đã giao, hệ thống lưu lại danh sách học sinh có ghi danh hiệu lực ở lớp tại thời điểm đó. Danh sách này không đổi về sau: học sinh chuyển lớp hoặc vào lớp sau thời điểm giao không làm đổi người nhận bài, mẫu số tỉ lệ nộp, nhãn "Không nộp" và số liệu sổ liên lạc (BR-24). Học sinh vào lớp sau khi giao không được giao bài đó.

Học sinh trong danh sách được giao mở bài từ thời điểm mở tới hạn nộp. Mở đề không tính lượt. Bài tự lưu nháp; không giới hạn thời lượng. **Tối đa một lần nộp**; Backend từ chối nộp sau hạn và nộp lại. Chấm ngay khi nộp: điểm = tổng điểm câu đúng / tổng điểm đề × 10. **Nháp chưa nộp không được chấm. Chưa nộp không phải 0 điểm** — hiển thị "Không nộp".

**BR-22 — Công bố điểm.** Chỉ khi bài **đã đóng** (mục 6.2), bởi Teacher đang phụ trách chuyên môn. Công bố cho cả lớp một lần, **không thu hồi**. Trước khi công bố, giáo viên xem điểm từng học sinh và **thống kê câu sai nhiều nhất** (số và tỉ lệ học sinh đã nộp chọn sai mỗi câu). Công bố tạo thông báo cho học sinh và phụ huynh.

**BR-23 — Phân tích AI.** Teacher đang phụ trách chuyên môn chạy phân tích sau khi bài đã đóng và có ít nhất một bài nộp. Backend tính chỉ số, gắn **mã giả lập**, rồi gửi AI (mục 3.1):

- **Cấp lớp:** tỉ lệ đúng theo câu, phân bố điểm.
- **Cấp học sinh** (chỉ học sinh đã nộp): kết quả bài này, kèm **tối đa 5 bài gần nhất cùng môn đã công bố điểm** của chính học sinh đó. Có bao nhiêu bài trước thì gửi bấy nhiêu.

Kết quả về ở **Nháp**. Giáo viên chỉnh, **duyệt**, rồi **công bố** từng nhận xét hoặc hàng loạt; **tạo lại** được khi nhận xét còn Nháp (nội dung Nháp cũ bị thay bằng kết quả mới). Nhận xét đã Đã duyệt hoặc Đã công bố thì không tạo lại và không bị ghi đè. Student và Parent chỉ thấy nhận xét Đã công bố. Công bố nhận xét **độc lập** với công bố điểm. AI lỗi → **Lỗi** kèm lý do, giáo viên chạy lại; không ảnh hưởng chấm, nộp, công bố điểm.

### 7.7. Dịch vụ phụ huynh, truyền thông, giám sát

**BR-24 — Sổ liên lạc tuần.** **Tính trực tiếp khi mở, không lưu, không có job sinh sổ, không ai viết tay.** Một sổ = một học sinh + một tuần (thứ Hai – Chủ nhật), xem được cho mọi tuần của năm học hiện hành mà học sinh có ghi danh. Gồm:

- **Chuyên cần:** số tiết có mặt, muộn, vắng có phép, vắng không phép, chưa điểm danh — chỉ tiết Đã xếp lịch đã diễn ra.
- **Bài tập có hạn nộp trong tuần:** số được giao, đã nộp, không nộp (đã đóng mà chưa nộp).
- **Điểm và nhận xét đã công bố** của các bài đó.

Không xếp hạng, không điểm trung bình lớp, không hạnh kiểm. Mục không có dữ liệu để trống. Số liệu luôn theo dữ liệu hiện tại — nhập bù điểm danh hay đơn duyệt muộn đều được phản ánh ngay lần mở sau.

**BR-25 — Hộp thư.** Cả Parent và Teacher đều có thể **tạo** hội thoại về một học sinh: Phụ huynh tạo với **chủ nhiệm** lớp con đang học hoặc **giáo viên đang phụ trách chuyên môn** ở lớp đó; Giáo viên (chủ nhiệm hoặc đang phụ trách chuyên môn) tạo với phụ huynh của học sinh thuộc lớp mình đang dạy. Mỗi bộ (phụ huynh, cán bộ, học sinh) có đúng một hội thoại. Hai bên nhắn văn bản tối đa 2.000 ký tự; không file, không gọi, không nhóm. Quan hệ được kiểm **mỗi lần gửi**: quan hệ đã kết thúc thì hội thoại thành **chỉ đọc**, lịch sử giữ nguyên. School Admin **không đọc và không nhắn** trong hội thoại. Tin nhắn mới hiện ngay ở bên nhận đang mở ứng dụng (web và mobile, qua SignalR), kèm chỉ báo **đang nhập**, **đã xem** (khi người nhận mở hội thoại) và **online/offline** của bên kia trong cùng hội thoại; School Admin không tham gia hội thoại nên không có trạng thái này. Realtime chỉ là kênh đẩy: việc gửi và kiểm quan hệ vẫn qua Backend như trên, mất kết nối thì tải lại phần đã lỡ.

**BR-26 — Thông báo.** Chỉ trong ứng dụng; không gửi email (ngoại lệ duy nhất là OTP quên mật khẩu ở BR-04, không phải thông báo). Thông báo mới hiện ngay trên web và mobile đang mở (SignalR); không có push khi app đóng. Có thông báo **không đồng nghĩa có quyền** — Backend kiểm lại khi người dùng bấm vào. Danh sách sự kiện là **danh sách đóng**:

| Sự kiện | Người nhận |
| --- | --- |
| Phát hành thời khóa biểu | Giáo viên có tiết trong phiên bản và học sinh của các lớp trong phiên bản — một thông báo chung "đã có thời khóa biểu mới" mỗi người |
| Tiết bị đổi giáo viên, hủy, thêm bù, có giáo viên dạy thay, trả lại giáo viên khi xóa khai báo vắng | Giáo viên liên quan; học sinh của lớp |
| Tiết chuyển Chờ xử lý | Vice Principal của phân hiệu chứa tiết |
| Đơn nghỉ mới | Chủ nhiệm (BR-19) |
| Đơn được duyệt hoặc từ chối | Parent gửi đơn |
| Bài mới được giao | Học sinh của lớp |
| Điểm được công bố; nhận xét được công bố | Học sinh và phụ huynh |
| Tin nhắn mới | Người nhận trong hội thoại |

**BR-27 — Bảng tin.** **Mọi cán bộ đăng được** — School Admin, Principal, Vice Principal, Teacher, kể cả khi đang giữ trách nhiệm chủ nhiệm hay tổ trưởng. Khi đăng, chọn một trong hai phạm vi:

| Phạm vi | Ai đọc |
| --- | --- |
| **Nội bộ** | Chỉ cán bộ |
| **Toàn trường** | Mọi người, gồm học sinh và phụ huynh |

Bài kèm tối đa **5 ảnh** (JPG hoặc PNG, mỗi ảnh ≤ 2 MB); ảnh đầu là ảnh bìa hiện trên danh sách. Trạng thái **Đã đăng ↔ Đã ẩn**, không có nháp. Người đăng sửa và ẩn bài của mình. **School Admin và Principal ẩn được bài của người khác**, bắt buộc nhập lý do; bài giữ nguyên tác giả và nội dung, ghi lại người ẩn, thời điểm và lý do để truy vết. Người ẩn (hoặc tác giả, với bài tự ẩn) hiện lại được bài. Ẩn bài giữ ảnh; xóa bài xóa luôn file. Bảng tin **không tạo thông báo**.

Ảnh đại diện người dùng (IAM-04) dùng chung cách lưu này. **Logo trường** (SCH-07) cũng dùng chung cách lưu: một ảnh duy nhất, JPG hoặc PNG, ≤ 2 MB, kiểm theo nội dung file; đổi logo thì file cũ bị xóa.

**BR-28 — Audit Log.** Ghi đúng **3 nhóm**:

1. **Tài khoản:** tạo (đơn lẻ, hàng loạt), khóa, mở, đổi loại tài khoản, School Admin reset mật khẩu. Không ghi mật khẩu.
2. **Phát hành thời khóa biểu:** phiên bản, số tiết xóa và tạo.
3. **Điều chỉnh lịch:** đổi giáo viên, hủy, tiết bù, khai báo, xóa khai báo và xử lý giáo viên vắng.

Thao tác hàng loạt ghi **một bản ghi kèm danh sách ID**. Audit **chỉ ghi thêm** — không có API sửa hay xóa, quyền CSDL chặn UPDATE và DELETE. Việc khác (đăng nhập, nộp bài, điểm danh, trách nhiệm, kết chuyển) **không vào Audit** — người thực hiện và thời điểm đã lưu ngay trên bản ghi nghiệp vụ.

### 7.8. Những ràng buộc cố tình không đặt

Mỗi điều dưới đây nếu thêm vào sẽ làm lược đồ khó thiết kế mà chặn được rất ít tình huống thật:

- Giáo viên **không bắt buộc** dạy môn thuộc tổ chuyên môn của mình.
- Giáo viên **không bắt buộc** thuộc phân hiệu của lớp — đây là hệ thống đa phân hiệu, dạy liên phân hiệu là chuyện thường.
- **Dạy thay không thành phụ trách chuyên môn:** phụ trách chuyên môn chỉ tính từ thời khóa biểu đã phát hành, không tính từ tiết điều chỉnh (BR-14).
- **Không giới hạn sĩ số lớp**, không quy định số tiết mỗi môn mỗi tuần.
- **Không kiểm lại ghi danh khi nộp bài** — chuyển lớp giữa chừng không làm mất bài đã nộp.
- **Khớp loại tài khoản với loại hồ sơ kiểm ở tầng dịch vụ**, không ở cơ sở dữ liệu.

---

## 8. Mobile và việc còn để mở

### 8.1. Phạm vi mobile

**Web là nền tảng đầy đủ: mọi chức năng có giao diện đều có trên web, cho mọi loại tài khoản. Mobile chỉ là một phần trong đó.**

> **Mobile chỉ có việc làm được bằng một tay, giữa hai tiết, tại lớp. Mọi màn hình dạng bảng, lưới, import, cấu hình chỉ có trên web.**

| Actor | Mobile |
| --- | --- |
| **Parent, Student** | Toàn bộ chức năng của mình |
| **Teacher** | Lịch dạy của mình · điểm danh và sửa trong ngày · hộp thư · thông báo · bảng tin (đọc) · hồ sơ cá nhân · nếu chủ nhiệm: hộp đơn nghỉ, duyệt và từ chối, sổ liên lạc lớp mình |
| **School Admin, Principal, Vice Principal** | Không có mobile, dùng web |

Chỉ có trên web: soạn câu hỏi và đề, giao bài, xem kết quả bài tập theo lớp, công bố điểm, chỉnh và duyệt nhận xét AI, giám sát cấp tổ, soạn bài đăng; toàn bộ SCH, PPL, SCD (trừ màn hình xem lịch cá nhân) và ADM.

### 8.2. Ký hiệu Platform trong WBS

| Giá trị | Nghĩa |
| --- | --- |
| `BE` | Chỉ Backend: lược đồ, service dùng chung, quy tắc không có giao diện riêng |
| `BE + Web` | Có giao diện web, không có mobile |
| `BE + Web + Mobile` | Có cả hai |

Mọi Task có giao diện đều mang `Web`; `Mobile` chỉ thêm vào khi Task thuộc phạm vi mục 8.1. Không có Task nào chỉ có Mobile mà không có Web.
| `QA` | Kiểm thử phân quyền và ràng buộc trên môi trường kiểm thử (mục 2.5) |

### 8.3. Việc kỹ thuật còn để mở

Thuộc Feature Spec / OpenAPI / hạ tầng, không suy diễn từ tài liệu này: lược đồ CSDL chi tiết; danh sách API, mã lỗi, wireframe, test case chi tiết; nơi lưu ảnh và quy ước đường dẫn; cấu hình Gmail SMTP và hạn mức gửi; nhà cung cấp AI và hạn mức gọi; yêu cầu phi chức năng (quy mô, hiệu năng giờ cao điểm nộp bài, sao lưu); rà soát pháp lý cho phân tích AI.

### 8.4. Quy tắc cho nhóm phát triển

1. Đọc mục 1–7, rồi Feature đang làm và Feature liên quan.
2. FE và BE thống nhất OpenAPI cho Feature trước khi code.
3. Mỗi Task gắn Feature ID, và gắn BR ID nếu Task hiện thực một quy tắc.
4. Backend kiểm quyền và điều kiện nghiệp vụ **độc lập với nhau**; trạng thái suy ra luôn tính theo mục 6.2, không tự viết lại điều kiện.
5. Kiểm thử tối thiểu mỗi Feature: thành công; phân quyền từng loại và School Admin; dữ liệu sai; xung đột và trùng; tác động lịch sử; hạn thời gian; AI hoặc Gmail hỏng.
6. Gặp điều chưa có trong tài liệu: dừng và hỏi, không tự quyết.

### 8.5. Từ vựng — một thứ một tên

| Thuật ngữ | Nghĩa | Trong code |
| --- | --- | --- |
| **Trách nhiệm** | Chủ nhiệm lớp, tổ trưởng, phụ trách phân hiệu (mục 2.2) | `Responsibility` |
| **Phụ trách chuyên môn** | Giáo viên dạy một môn ở một lớp, tính từ thời khóa biểu đã phát hành (BR-14, SCD-04) | không có bảng riêng; truy vấn từ `TimetableEntry` |
| **Thời khóa biểu** | Bản mẫu tuần, có phiên bản, phát hành | `TimetableVersion` |
| **Lịch dạy** | Tiết thực tế theo ngày, có điều chỉnh tay — nguồn sự thật (BR-14) | `TeachingSession` |
| **Cán bộ** | Hồ sơ nhân sự, gồm cả Hiệu trưởng và Hiệu phó | `Staff` |
| **Giáo viên** | Chỉ loại tài khoản Teacher | `Teacher` |

Tài liệu không dùng cụm "phân công giảng dạy" nữa — đã đổi thành **phụ trách chuyên môn** ở mọi chỗ. Chi tiết nhãn hiển thị xem `MSMS_Dev_Conventions.md` mục 4.
