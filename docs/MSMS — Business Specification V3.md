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
| Hồ sơ | Cán bộ, học sinh, phụ huynh, xếp lớp, liên kết phụ huynh – học sinh |
| Trách nhiệm | Trách nhiệm (phân hiệu, tổ, lớp chủ nhiệm) |
| Vận hành | Thời khóa biểu, phụ trách chuyên môn (tính từ thời khóa biểu), lịch dạy thực tế, xử lý giáo viên vắng, điểm danh theo tiết |
| Học tập | Ngân hàng câu hỏi, bài tập rèn luyện trắc nghiệm, chấm tự động, phân tích bằng AI (MSMS AI) |
| Dịch vụ phụ huynh | Đơn xin nghỉ, báo cáo tuần, hộp thư phụ huynh – giáo viên |
| Truyền thông | Thông báo trong ứng dụng, bảng tin |
| Giám sát | Bảng điều khiển, hỏi đáp số liệu với MSMS AI, Audit Log |

### Ngoài phạm vi

Tuyển sinh. Sổ điểm và học bạ chính thức — **điểm trong MSMS chỉ là điểm bài tập rèn luyện**. LMS và kho học liệu. Tự xếp thời khóa biểu. Nhân sự, lương, nghỉ phép của cán bộ — MSMS chỉ xử lý **hệ quả lên lịch dạy**. Chat nhóm, gọi thoại, gọi video. AI chấm tự luận hoặc chấm điểm. Quy trình chuyển học kỳ. Giả đăng nhập hoặc chuyển vai trò. **Thông báo qua email** — mọi thông báo chỉ nằm trong ứng dụng; email chỉ dùng cho mã OTP quên mật khẩu và để báo giáo viên khi lịch dạy của họ đổi (mục 3.2). **Job chạy nền theo lịch** — hệ thống chỉ có một lượt quét định kỳ tự nộp bài tập đã hết hạn thực tế (BR-21), không có job nào khác.

Gửi file: không có, trừ **ảnh đính kèm bài đăng bảng tin**, **ảnh đại diện người dùng** và **logo trường** (BR-27).

### Nguyên tắc nền

1. Mọi dữ liệu gắn với **năm học, phân hiệu hoặc lớp, và khoảng hiệu lực**. Luôn lấy đối tượng có hiệu lực tại ngày nghiệp vụ, không lấy đối tượng hiện tại.
2. **Không ghi đè lịch sử.** Thứ đã diễn ra không bị sửa bởi thời khóa biểu mới, phụ trách mới hay kết chuyển năm.
3. **Không lưu thứ tính ra được.** Trạng thái suy được từ ngày giờ hoặc dữ liệu khác thì tính lúc đọc (mục 6.2). Nhờ vậy hệ thống không cần job cập nhật trạng thái.
4. **Quyền luôn kiểm tra tại Backend**, không dựa vào giao diện.
5. **Ngày nghiệp vụ theo giờ Việt Nam (Asia/Ho_Chi_Minh).** Backend quyết định "hôm nay", hạn nộp và mọi điều kiện thời gian.
6. **Hệ thống gợi ý, con người quyết định.** MSMS không tự xếp giáo viên dạy thay, không tự tạo tiết bù.
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
| **Pupil** | Học sinh | Học sinh | Xếp lớp hiệu lực |
| **Parent** | Phụ huynh | Phụ huynh | Liên kết phụ huynh – học sinh |

### 2.2. Trách nhiệm

**Homeroom Teacher** (chủ nhiệm) và **Subject Department Head** (tổ trưởng) **không phải loại tài khoản**. Đó là trách nhiệm gán cho tài khoản Teacher, có khoảng hiệu lực. Hết trách nhiệm thì quyền tương ứng tự mất, tài khoản không đổi.

| Trách nhiệm | Gán cho loại | Đích | Ai gán |
| --- | --- | --- | --- |
| Phụ trách phân hiệu | Vice Principal | Một phân hiệu | Principal |
| Tổ trưởng | Teacher | Một tổ chuyên môn | Principal |
| Chủ nhiệm | Teacher | Một lớp | Principal |

**Phụ trách chuyên môn** (giáo viên – môn – lớp) là một thứ khác, không phải trách nhiệm và không do ai gán: hệ thống tính từ thời khóa biểu đã phát hành, xem BR-14 và SCD-04.

### 2.3. Ai làm được gì

| Actor | Đọc | Ghi |
| --- | --- | --- |
| **School Admin** | Mọi dữ liệu vận hành toàn trường, trừ điểm và phân tích bài tập. **Không** đọc hội thoại phụ huynh – giáo viên | Mọi danh mục (kể cả thông tin trường), hồ sơ, tài khoản, ngày nghỉ cấp trường hoặc phân hiệu, **nhập bù và sửa điểm danh**. Đọc bảng tin nhưng **không** đăng hay ẩn bài (BR-27). **Không** có Dashboard học vụ (chỉ có Thống kê hệ thống), **không** gán trách nhiệm, **không** làm thời khóa biểu, **không** làm việc đứng tên người khác (mục 2.4) |
| **Principal** | Toàn trường: lịch dạy, điểm danh, Bảng điều khiển, bảng tin; qua MSMS AI thêm chuyên cần, tỷ lệ nộp và điểm trung bình bài tập theo từng học sinh (BR-29). **Không** đọc đơn nghỉ | Trách nhiệm (mục 2.2), bài đăng bảng tin (ẩn được bài của người khác kèm lý do, BR-27) |
| **Vice Principal** | Phân hiệu mình: lịch dạy, điểm danh, Bảng điều khiển, MSMS AI như Principal (BR-29). Màn hình Lịch dạy chỉ cho xem phân hiệu mình; riêng khi chọn giáo viên cho tiết hoặc dạy thay thì **danh sách giáo viên là toàn trường**, kèm trạng thái rảnh hoặc bận, kiểm trùng lịch cũng toàn trường (BR-13); màn hình Phụ trách chuyên môn cũng xem **toàn trường** | Thời khóa biểu, điều chỉnh lịch — cho phân hiệu mình. Duyệt khai báo giáo viên vắng và xử lý riêng tiết Chờ xử lý ở phân hiệu mình; **không** khai báo giáo viên vắng. Bài đăng bảng tin. **Không** sửa điểm danh |
| **Teacher** | Tiết mình dạy; lớp + môn mình phụ trách. Nếu chủ nhiệm: hồ sơ, chuyên cần, đơn nghỉ, báo cáo tuần của lớp chủ nhiệm. Nếu tổ trưởng: số liệu tổng hợp của tổ ở Tình hình tổ chuyên môn (chỉ đọc), không xem bài làm hay điểm từng học sinh | Điểm danh tiết mình dạy **trong ngày của tiết**; soạn câu hỏi của mình; giao bài cho lớp + môn mình phụ trách; trả lời tin nhắn; bài đăng bảng tin. Nếu chủ nhiệm: duyệt đơn nghỉ. Nếu tổ trưởng: khai báo giáo viên thuộc tổ mình vắng và quyết định từng tiết Chờ xử lý bằng đề xuất (dạy thay hoặc hủy); Vice Principal duyệt |
| **Pupil** | Lịch học, bài tập, điểm, đáp án và phân tích AI của bài mình đã nộp, chuyên cần, báo cáo tuần của chính mình; bài đăng Toàn trường | Làm và nộp bài |
| **Parent** | Như Pupil, cho từng con được liên kết | Gửi và rút đơn nghỉ, tạo hội thoại và nhắn tin |

Người vừa là cán bộ vừa là phụ huynh dùng **hai hồ sơ và hai tài khoản riêng** (BR-03).

### 2.4. Quy tắc School Admin

School Admin là quản trị viên toàn trường: **vượt phạm vi, không làm việc đứng tên người khác, không vượt điều kiện nghiệp vụ** (BR-02). Không có cờ bỏ qua phân quyền; các việc dưới đây chỉ đơn giản là không cấp cho School Admin, và giao diện không hiển thị chúng.

Việc đứng tên người khác — chỉ đúng vai trò làm được:

| Hành vi | Chỉ ai làm được |
| --- | --- |
| Làm bài, nộp bài | Pupil, bằng tài khoản của mình |
| Gửi, rút đơn nghỉ | Parent có liên kết với học sinh |
| Tạo hội thoại, gửi tin nhắn | Parent có liên kết (tạo và gửi); cán bộ của hội thoại (chỉ gửi) |
| Soạn câu hỏi, giao bài | Teacher — soạn câu hỏi của mình; giao bài thì cần phụ trách chuyên môn đang hiệu lực |
| Điểm danh với tư cách giáo viên dạy | Giáo viên của tiết, trong ngày của tiết |

Việc duy nhất School Admin làm thay được trên dữ liệu giảng dạy là nhập bù và sửa điểm danh của tiết có ngày từ hôm nay trở về trước (ghi là người sửa, BR-17). Hai thao tác danh mục của School Admin có hệ quả lên lịch dạy nhưng không phải thao tác lịch dạy: khóa tài khoản giáo viên (BR-05) và khai ngày nghỉ (BR-08). School Admin không duyệt đơn nghỉ, không đọc hội thoại, không thao tác thời khóa biểu, chỉ có Thống kê hệ thống chứ không có số liệu học vụ, và không gán trách nhiệm. Mọi thao tác ghi lưu người thực hiện ngay trên bản ghi nghiệp vụ.

### 2.5. Kiểm thử phân quyền

**Không dùng tài khoản School Admin để kiểm thử phạm vi** của loại khác — tài khoản này có phạm vi toàn trường nên không phát hiện được rò rỉ. Mỗi loại tài khoản, và mỗi trách nhiệm (chủ nhiệm, tổ trưởng, phụ trách phân hiệu), có kiểm thử riêng trên môi trường kiểm thử với dữ liệu mẫu.

---

## 3. Hệ thống bên ngoài

Cả hai đều là **tiện ích, không phải điều kiện vận hành**: hệ thống chạy đúng nghiệp vụ khi cả hai cùng hỏng.

### 3.1. AI Assistant

|  |  |
| --- | --- |
| Dùng ở đâu | Sinh nháp câu hỏi trắc nghiệm (LRN-01); phân tích từng bài làm của học sinh (LRN-04); trả lời câu hỏi số liệu của Principal, Vice Principal trên màn Tình hình (ADM-01, BR-29) |
| Nhận vào | Sinh nháp: môn, khối, tên bài, ghi chú tùy chọn của giáo viên và ảnh các trang sách giáo khoa của bài. Phân tích bài làm: nội dung câu hỏi, các lựa chọn, đáp án đúng, lựa chọn của học sinh ở từng câu và điểm — **không kèm danh tính**. Hỏi đáp số liệu: câu hỏi, các lượt hỏi đáp gần nhất, số liệu tổng hợp do Backend tính và tên phân hiệu, khối, lớp, tổ, môn; người chỉ ở dạng mã giả (BR-29) |
| Không bao giờ nhận | Họ tên, mã học sinh hay cán bộ, ngày sinh, CCCD, liên lạc, địa chỉ, phụ huynh, tên trường. Tên lớp chỉ có ở hỏi đáp số liệu, không gắn với tên người |
| Không bao giờ làm | Chấm điểm; quyết định đúng/sai; ghi trực tiếp vào dữ liệu nghiệp vụ ngoài phân tích của chính bài làm; tự truy vấn dữ liệu (hỏi đáp số liệu chỉ gọi các truy vấn chỉ đọc Backend khai báo sẵn) |

Trợ lý AI của hệ thống tên là **MSMS AI**. Câu hỏi do AI sinh là **nháp** cho tới khi giáo viên xác nhận. Phân tích bài làm do AI sinh **tự động khi học sinh nộp bài**, không qua giáo viên duyệt. **AI hỏng hoặc thiếu dữ liệu:** chấm, nộp bài, xem điểm vẫn chạy; phần phân tích để trống, không thử lại; giáo viên soạn câu hỏi thủ công; hỏi đáp số liệu báo lỗi và không đoán số, màn Tình hình vẫn chạy.

**Cần rà soát trước khi vận hành thật:** sự đồng ý của phụ huynh về việc gửi dữ liệu học tập cho nhà cung cấp AI, điều khoản không dùng dữ liệu để huấn luyện, quy định bảo vệ dữ liệu cá nhân; quyền sử dụng ảnh trang sách giáo khoa (bản quyền thuộc nhà xuất bản) khi lưu trên kho ảnh, gửi cho nhà cung cấp AI để soạn câu hỏi và hiển thị cho học sinh, phụ huynh ở mục xem lại trong sách.

### 3.2. Gmail SMTP

**Dùng cho hai việc**, từ hộp thư chung của trường; không đọc hộp thư của ai, không có luồng xác nhận email riêng. Gửi tới **email ghi trong hồ sơ người dùng**.

1. **Mã OTP quên mật khẩu** (BR-04). Email chỉ chứa mã OTP và thời hạn. **Gmail SMTP hỏng:** người dùng bấm "Gửi lại mã" sau đó; vẫn còn đường School Admin reset mật khẩu.
2. **Báo giáo viên khi lịch dạy của họ đổi** (BR-15, BR-16), chỉ cho **bốn** trường hợp do Vice Principal thao tác: được phân công dạy thay (đổi giáo viên, xử lý riêng tiết Chờ xử lý hoặc duyệt khai báo giáo viên vắng), được phân công dạy tiết bù, không còn dạy tiết (đổi giáo viên cho người khác) và có tiết bị hủy. Email gửi tới email ghi trong hồ sơ giáo viên, chỉ nêu việc đã đổi, tiết (môn, lớp, ngày, khung giờ) và nhắc xem lịch dạy trên MSMS; hồ sơ không có email thì bỏ qua. Email đi kèm thông báo trong ứng dụng chứ không thay thế. **Gmail SMTP hỏng:** thao tác xếp lịch vẫn thành công, email không gửi được thì bỏ.

Không có hàng đợi, không thử lại tự động. Không có email nào khác: thông báo cho học sinh, phụ huynh và mọi sự kiện còn lại chỉ nằm trong ứng dụng.

---

## 4. Luồng nghiệp vụ

Mọi tính năng phục vụ một trong bốn luồng. Tính năng không nằm trong luồng nào thì không đưa vào hệ thống.

### Luồng 0 — Thiết lập đầu năm

1. **School Admin** khai phân hiệu, tổ chuyên môn, môn học (kèm viết tắt), khung giờ tiết.
2. **School Admin** tạo năm học, hai học kỳ, ngày nghỉ cấp trường hoặc cấp phân hiệu.
3. **School Admin** tạo lớp của năm học cho từng phân hiệu — năm sau dùng kết chuyển (SCH-06).
4. **School Admin** import cán bộ, rồi import học sinh. Một lần import học sinh tạo hồ sơ học sinh, xếp lớp, hồ sơ phụ huynh và liên kết (BR-11).
5. **School Admin** chọn nhiều dòng trên danh bạ để tạo tài khoản hàng loạt, nhận danh sách mật khẩu tạm và bàn giao (BR-04).
6. **Principal** gán trách nhiệm: Vice Principal – phân hiệu, tổ trưởng – tổ, chủ nhiệm – lớp.

### Luồng A — Vận hành giảng dạy

1. **Vice Principal** tải file mẫu thời khóa biểu — hệ thống sinh theo cấu hình và điền sẵn bản phát hành gần nhất — sửa rồi import theo học kỳ; file chỉ cần gồm các lớp muốn đổi. Hệ thống đối chiếu viết tắt, báo ô lỗi; file hợp lệ thành bản Nháp (BR-12).
2. **Vice Principal** sửa bản Nháp ngay trên lưới — kéo tiết, thêm tiết, đổi giáo viên, xóa tiết — và xem các xung đột lịch toàn trường được đánh dấu tại ô (BR-12, BR-13).
3. **Vice Principal** xem trước kết quả, xác nhận cảnh báo về tiết sẽ bị xóa, rồi Phát hành. Hệ thống sinh tiết học tới hết học kỳ cho các lớp trong bản Nháp, trừ ngày nghỉ (BR-14).
4. Sau khi Phát hành, phụ trách chuyên môn của giáo viên tự có theo thời khóa biểu; **Vice Principal** xem ở màn hình Phụ trách chuyên môn, không nhập tay (BR-14, SCD-04).
5. Khi có việc phát sinh, **Vice Principal** đổi giáo viên, hủy tiết hoặc thêm tiết bù (BR-15).
6. Giáo viên vắng: **tổ trưởng** khai báo cho giáo viên của tổ, hệ thống đưa các tiết bị ảnh hưởng (ở mọi phân hiệu) về Chờ xử lý và gợi ý giáo viên rảnh trong tổ; tổ trưởng quyết định từng tiết bằng một đề xuất (dạy thay hoặc hủy) rồi **gửi duyệt**; **Vice Principal** của mỗi phân hiệu có tiết duyệt một lần, hoặc trả lại kèm lý do để tổ trưởng sửa và gửi lại (BR-16).
7. **Giáo viên của tiết** điểm danh trong ngày của tiết. Sau ngày đó chỉ **School Admin** nhập bù hoặc sửa (BR-17).

### Luồng B — Học tập

Đây là **bài tập rèn luyện**, không phải sổ điểm chính thức.

1. **Teacher** soạn câu hỏi trắc nghiệm 4 lựa chọn, ít nhất 1 đáp án đúng — thủ công hoặc từ nháp AI — vào ngân hàng câu hỏi (BR-20).
2. **Teacher** giao bài cho lớp + môn mình phụ trách: chọn câu hỏi từ ngân hàng, hạn nộp, thời lượng làm bài tùy chọn (BR-21).
3. **Pupil** làm bài, tự lưu nháp, nộp **một lượt** trước hạn; Backend chấm ngay (BR-21).
4. Nộp xong, **Pupil** thấy ngay điểm, đáp án đúng và giải thích; **Parent** xem được kết quả của con; **Teacher** đang phụ trách xem điểm từng học sinh (BR-22).
5. **MSMS AI** phân tích bài làm — điểm mạnh, điểm yếu, giải thích lỗi sai, gợi ý kiến thức — hiện cùng kết quả cho học sinh, phụ huynh và giáo viên phụ trách (BR-23).
6. **Tổ trưởng** theo dõi việc giao bài và tỷ lệ nộp của thầy cô trong tổ ở Tình hình tổ chuyên môn (ADM-01, mục 5.1).

### Luồng C — Dịch vụ phụ huynh và truyền thông

1. **Parent** gửi đơn xin nghỉ; **chủ nhiệm** duyệt hoặc từ chối (BR-19). Đơn duyệt không tự sinh vắng — nó quyết định nhãn có phép khi học sinh bị ghi vắng (BR-18).
2. **Parent** và **Pupil** xem **báo cáo tuần**, tính trực tiếp từ dữ liệu (BR-24).
3. **Parent** nhắn tin 1–1 với chủ nhiệm hoặc giáo viên đang dạy con (BR-25).
4. Mọi sự kiện đáng chú ý tạo **thông báo trong ứng dụng** (BR-26).
5. **Principal, Vice Principal, Teacher** đăng bài bảng tin, chọn phạm vi Nội bộ hoặc Toàn trường; School Admin chỉ đọc (BR-27).
6. **Principal**, **Vice Principal** theo dõi Bảng điều khiển và hỏi MSMS AI để báo cáo, so sánh, xếp hạng số liệu (BR-29); **School Admin** tra Audit Log (BR-28, ADM-01, ADM-02).

---

## 5. Danh mục Module – Feature

**9 Module / 33 Feature.** Feature có ID theo tiền tố module (ví dụ `SCH-02`); Task đánh số `4.<module>.<số thứ tự>` trong WBS (ví dụ `4.2.9`). Phân rã Task ở file WBS.

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
| IAM-04 | Hồ sơ cá nhân | Xem hồ sơ, trách nhiệm đang giữ và trạng thái tài khoản; tự sửa số điện thoại, email nhận OTP, địa chỉ (phụ huynh không tự sửa số điện thoại) và ảnh đại diện; đổi mật khẩu | chính chủ 📱 | chính chủ 📱 | BR-04 |

### SCH — School Setup (7) — chỉ SA ghi

SA là người duy nhất ghi. HT có các màn hình xem Thông tin trường, Phân hiệu, Tổ chuyên môn & Môn học, Năm học & Học kỳ, Lớp và Xếp lớp kèm thống kê cấu trúc. Ngày nghỉ, Khung giờ tiết và Kết chuyển năm học chỉ có màn hình cho SA. HP xem các màn chỉ xem của HT, trừ Phân hiệu: thống kê tổ, môn là toàn trường, còn Lớp và Xếp lớp chỉ có lớp của phân hiệu mình phụ trách. CN có màn hình Danh sách học sinh chỉ xem học sinh của lớp mình chủ nhiệm, trên cả web và mobile; bấm tên một em mở Hồ sơ học sinh (PPL-02). Mọi màn khác của SCH chỉ có trên web. Ngoài các màn đó, cột Đọc thêm là dữ liệu HP và GV dùng ở màn hình khác (HP chỉ đọc dữ liệu của phân hiệu mình phụ trách).

| ID | Feature | Nội dung chính | Đọc thêm | BR |
| --- | --- | --- | --- | --- |
| SCH-01 | Phân hiệu | Mã, tên, địa chỉ, số điện thoại, ngừng hoạt động; kèm số lớp, học sinh, cán bộ và hiệu phó phụ trách | HT, HP | BR-07 |
| SCH-02 | Tổ chuyên môn & Môn học | Tổ, môn thuộc tổ, viết tắt môn; kèm tổ trưởng, số giáo viên của tổ và số lớp, giáo viên đang dạy từng môn | HT, HP, GV | BR-07 |
| SCH-03 | Năm học, Học kỳ & Ngày nghỉ | Năm học hiện hành, hai học kỳ, ngày nghỉ cấp trường hoặc phân hiệu; khai ngày nghỉ hủy các tiết chưa khóa của ngày đó | HT, HP, GV | BR-08 |
| SCH-04 | Khung giờ tiết | Khung giờ dùng chung toàn trường, khóa là cặp (Buổi, Tiết) | HT, HP, GV, HS, PH | BR-07 |
| SCH-05 | Lớp & Xếp lớp | Lớp theo phân hiệu và năm học; xếp lớp có hiệu lực (một hoặc nhiều học sinh một lượt); chuyển lớp giữa năm (cùng khối); cho rời lớp | HT, HP, CN 📱 | BR-07, 09 |
| SCH-06 | Kết chuyển năm học | Sao chép danh sách lớp sang năm mới (gợi ý tên và khối tăng 1, sửa được trước khi tạo); chuyển nguyên lớp hoặc từng học sinh sang lớp đích do người dùng chọn; cho học sinh khối 9 tốt nghiệp. Không tự xét lên lớp hay lưu ban, không sao chép thời khóa biểu hay trách nhiệm | — | BR-09 |
| SCH-07 | Thông tin trường | Tên trường, địa chỉ, điện thoại, email, website và logo. Đúng một bản; tên trường hiện ở thanh đường dẫn đầu trang và tiêu đề tab trình duyệt, logo hiện ở Trang chủ; ai cũng đọc được, kể cả chưa đăng nhập. **Chỉ SA ghi**; HT, HP xem trên màn Cơ cấu nhà trường | HP, GV | BR-27 |

### PPL — People & Responsibility (4)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| PPL-01 | Danh bạ cán bộ | Hồ sơ cán bộ, import Excel, gán viết tắt giáo viên; trang Hồ sơ cán bộ chỉ xem gồm thông tin cá nhân, trách nhiệm đang giữ và đã kết thúc, giảng dạy học kỳ hiện hành | SA | HT, HP | BR-03, 10, 11 |
| PPL-02 | Danh bạ học sinh | Hồ sơ học sinh, import kèm xếp lớp và phụ huynh, chuyển trường, tốt nghiệp (khối cuối cấp); trang Hồ sơ học sinh chỉ xem gồm thông tin cá nhân, ghi chú, phụ huynh đại diện, chuyên cần năm học hiện hành và lịch sử lớp | SA | HT, HP, CN 📱 (hồ sơ học sinh lớp mình) | BR-03, 10, 11 |
| PPL-03 | Danh bạ phụ huynh & Liên kết | Hồ sơ phụ huynh; mỗi học sinh một phụ huynh đại diện, một phụ huynh nhiều con; đổi phụ huynh đại diện | SA | HT, HP | BR-10 |
| PPL-04 | Trách nhiệm | Gán và thay phụ trách phân hiệu, tổ trưởng, chủ nhiệm; cảnh báo lớp chưa có chủ nhiệm | HT | HP, cán bộ (phần của mình) | BR-09 |

### SCD — Timetable & Teaching Schedule (4)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| SCD-01 | Import & Phát hành thời khóa biểu | Tải mẫu điền sẵn, import theo học kỳ (file gồm các lớp cần đổi), đối chiếu viết tắt, bảng lỗi theo ô, sửa bản Nháp bằng kéo thả, kiểm xung đột toàn trường, xem trước, Phát hành và Phát hành lại | HP | lưới TKB và lịch dạy: HT, HP · lịch cá nhân: GV 📱, HS 📱, PH 📱 | BR-06, 12, 13, 14 |
| SCD-02 | Điều chỉnh lịch dạy | Đổi giáo viên của tiết, hủy tiết, thêm tiết bù | HP | GV 📱, HS 📱, PH 📱 | BR-13, 15 |
| SCD-03 | Xử lý giáo viên vắng | Tổ trưởng khai báo vắng theo ngày hoặc theo tiết, xóa khai báo của giáo viên tổ mình, quyết định từng tiết bằng đề xuất (dạy thay hoặc hủy) rồi gửi duyệt; danh sách tiết Chờ xử lý, gợi ý giáo viên rảnh; HP duyệt hoặc trả lại khai báo đã gửi, xử lý riêng từng tiết; HT xem được danh sách khai báo và tiết Chờ xử lý toàn trường, không thao tác | GV (tổ trưởng), HP | HT, HP, GV (tổ trưởng) | BR-13, 16 |
| SCD-04 | Phụ trách chuyên môn | Màn hình chỉ đọc, tính từ thời khóa biểu đã phát hành: mỗi giáo viên một dòng, liệt kê các mục môn – lớp kèm số tiết mỗi tuần và tổng tiết; lọc theo phân hiệu và môn; xem toàn trường | — | HT, HP | BR-14 |

### ATT — Attendance (1)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| ATT-01 | Điểm danh theo tiết | Giáo viên của tiết điểm danh và sửa trong ngày của tiết; School Admin nhập bù và sửa tiết có ngày ≤ hôm nay trên cùng màn hình, mỗi lần lưu ghi Audit kèm giá trị trước và sau; nhãn có phép suy từ đơn nghỉ đã duyệt | GV của tiết 📱, SA | HS 📱, PH 📱, CN 📱, HP, HT, SA | BR-17, 18 |

### LRN — Learning (5)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| LRN-01 | Ngân hàng câu hỏi | Câu hỏi dùng chung theo môn, khối 6–9, nháp AI | GV (của mình) | mọi GV | BR-20 |
| LRN-02 | Giao bài & Làm bài | Giao bài cho (lớp, môn) mình phụ trách, thời lượng tùy chọn; học sinh bắt đầu, tự lưu nháp, nộp một lượt | GV giao · HS làm | HS 📱 | BR-21 |
| LRN-03 | Kết quả bài làm | Chấm ngay khi nộp; điểm, đáp án đúng và giải thích hiện ngay; giáo viên xem điểm từng học sinh | — | GV phụ trách · HS 📱 · PH 📱 | BR-22 |
| LRN-04 | Phân tích bài làm bằng AI | MSMS AI phân tích từng bài làm khi nộp: điểm mạnh, điểm yếu, giải thích lỗi sai, gợi ý kiến thức | — | GV phụ trách · HS 📱 · PH 📱 | BR-23 |
| LRN-06 | Dựng đề & Bản in đề | Phân loại độ khó cho câu cũ; dựng đề theo ma trận bài × độ khó; thang điểm theo phần; tạo bản in đề PDF hoặc Word (kèm đáp án tùy chọn) từ đề đang soạn hoặc từ bài đã giao | GV | GV phụ trách (bài đã giao) | BR-20, BR-21 |

### PAR — Parent Services (3)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| PAR-01 | Đơn xin nghỉ | Gửi (cả ngày, buổi hoặc tiết lẻ), rút, duyệt, từ chối | PH 📱, CN 📱 | PH 📱, CN 📱 | BR-19 |
| PAR-02 | Báo cáo tuần | Chuyên cần, bài tập có hạn trong tuần và điểm — tính trực tiếp, chỉ đọc | — | HS 📱, PH 📱, CN 📱 | BR-24 |
| PAR-03 | Hộp thư phụ huynh – giáo viên | Hội thoại 1–1 về một học sinh, chỉ văn bản | PH 📱, GV 📱 tạo và gửi | — | BR-25 |

### COM — Communication (2)

| ID | Feature | Nội dung chính | Ghi | Đọc | BR |
| --- | --- | --- | --- | --- | --- |
| COM-01 | Trung tâm thông báo | Thông báo trong ứng dụng, đã đọc / chưa đọc, kiểm quyền khi mở | hệ thống | mọi loại 📱 | BR-26 |
| COM-02 | Bảng tin | Bài đăng phạm vi Nội bộ hoặc Toàn trường, kèm tối đa 5 ảnh | HT, HP, GV (web) | Nội bộ: cán bộ 📱 · Toàn trường: mọi người 📱 | BR-27 |

### ADM — Administration & Oversight (2)

| ID | Feature | Nội dung chính | Đọc | BR |
| --- | --- | --- | --- | --- |
| ADM-01 | Bảng điều khiển | Mỗi vai trò một màn Tình hình (toàn trường, phân hiệu, tổ chuyên môn, lớp chủ nhiệm) gồm cả vận hành lẫn bài tập rèn luyện; Thống kê hệ thống; MSMS AI hỏi đáp số liệu cho Hiệu trưởng, Hiệu phó; mỗi vai trò thấy đúng phạm vi mình chịu trách nhiệm (mục 5.1) | HT, HP, GV (tổ trưởng, chủ nhiệm), SA | BR-29 |
| ADM-02 | Audit Log | Năm nhóm sự kiện, ghi bằng tiếng Việt, chỉ ghi thêm, lọc theo nhóm, người và thời gian | SA | BR-28 |

### 5.1. Bảng điều khiển (ADM-01)

**Mỗi vai trò thấy đúng phạm vi mình chịu trách nhiệm.** Bảng điều khiển là một nhóm menu, mỗi vai trò có **một màn Tình hình** gồm cả vận hành lẫn bài tập rèn luyện, trả lời câu hỏi chính của vai trò đó:

| Mục | Ai thấy | Phạm vi | Câu hỏi chính |
| --- | --- | --- | --- |
| Tình hình toàn trường | Principal | Toàn trường, lọc được theo phân hiệu và khối | Phân hiệu nào, lớp nào đáng lo? |
| Tình hình phân hiệu | Vice Principal | Phân hiệu đang phụ trách (nhiều phân hiệu thì lọc được trong số đó), lọc theo khối | Hôm nay phân hiệu phải xử lý gì? |
| Tình hình tổ chuyên môn | Giáo viên đang là tổ trưởng | Tổ mình | Tổ dạy đủ không, giao bài đều không? |
| Tình hình lớp chủ nhiệm | Giáo viên đang chủ nhiệm | Lớp mình | Hôm nay cần gọi phụ huynh em nào, duyệt gì? |
| Thống kê hệ thống | School Admin | Toàn trường, chỉ dữ liệu tổ chức, tài khoản và hoạt động hệ thống | Hệ thống còn thiếu dữ liệu gì? |

Giáo viên không giữ trách nhiệm nào không có Bảng điều khiển; giáo viên vừa là tổ trưởng vừa chủ nhiệm thấy cả hai màn. Một tiết, đơn hay bài thuộc phân hiệu nào là theo **phân hiệu của lớp** liên quan; cán bộ tính theo **phân hiệu chính**. Mọi số liệu tính lúc đọc, không lưu số liệu suy ra; chỉ màn lớp chủ nhiệm có tên học sinh, các màn khác chỉ có số tổng hợp. Số liệu theo từng lớp, học sinh, giáo viên của Principal và Vice Principal nằm ở MSMS AI (BR-29).

**Trang chủ không phải Bảng điều khiển.** Trang chủ là màn đầu tiên của mọi loại tài khoản, mang tính chào đón. Trên web **mọi loại tài khoản thấy cùng một nội dung**: lời chào theo buổi kèm logo trường (tên trường hiện ở thanh đường dẫn đầu trang), một đồng hồ của ngày, **Hôm nay** (khung giờ tiết của trường, đánh dấu tiết đang diễn ra theo giờ thực; giáo viên có tiết thấy thêm lớp và môn mình dạy ở từng tiết; ngày nghỉ cấp trường hoặc Chủ nhật thì báo trường nghỉ), **Năm học** (học kỳ hiện tại, đang ở tuần thứ mấy) và **Ngày nghỉ sắp tới** (ba ngày gần nhất, ngày đầu có đếm ngược và lịch tháng). Trang chủ web không có số liệu thống kê, lối tắt hay bảng tin; số liệu chỉ nằm ở Bảng điều khiển. Trên mobile, Trang chủ là điểm vào các việc trong ngày nên khác theo loại tài khoản: tiết sắp tới hoặc báo cáo tuần này, bốn ô đếm việc của chính người dùng (tiết chưa điểm danh, đơn chờ duyệt, bài chưa nộp, tin nhắn chưa đọc…) và lối vào các màn hình mobile; đó là việc cần làm của người dùng, không phải số liệu tổng hợp (MSMS_Design_Mobile §4).

**Cách tính dùng chung.** Mọi chỉ số so **4 tuần gần nhất** (28 ngày tính tới hôm nay) với **4 tuần liền trước**, kèm đường 8 tuần (tuần bắt đầu thứ Hai, ghi theo tuần của năm học: "Tuần 12"). Chưa có dữ liệu thì nói rõ, không hiện 0%.

| Chỉ số | Cách tính |
| --- | --- |
| Chuyên cần | (Có mặt + Muộn) trên số lượt đã điểm danh của các tiết Đã xếp lịch; vắng tách có phép và không phép theo BR-18 (cùng quy tắc với màn Điểm danh, spec 6.2). Tiết chưa có bản ghi nào không làm giảm tỷ lệ mà được đếm riêng là **tiết chưa điểm danh** |
| Vắng không phép | Lượt vắng không phép trên số lượt đã điểm danh |
| Học sinh vắng liên tiếp | Vắng **mọi tiết** đã điểm danh trong ngày, từ 3 ngày học liên tiếp trở lên tính tới hôm qua (có phép hay không đều tính; ngày học là ngày lớp có điểm danh) |
| Tiết bị ảnh hưởng | Tiết của giáo viên vắng (đã dạy thay, đã hủy hoặc còn Chờ xử lý) cộng tiết đã hủy vì lý do khác; không tính tiết hủy rơi vào ngày nghỉ của trường hoặc của phân hiệu (lịch nghỉ có kế hoạch, và số không được phụ thuộc việc ngày nghỉ khai trước hay sau khi phát hành) |
| Tiết chưa có người dạy | Tiết Chờ xử lý chưa diễn ra |
| Tỷ lệ nộp bài | Bài đã nộp trên người nhận, chỉ tính bài rèn luyện đã hết hạn nộp trong kỳ |

Ngưỡng cảnh báo cố định, không lưu và không cấu hình: chuyên cần dưới 90%, tỷ lệ nộp bài dưới 50%. Mọi màn Tình hình là **bảng điều khiển tổng quan**, cùng một khung: **bốn thẻ số** (mỗi thẻ kèm đường 8 tuần và mức thay đổi so với 4 tuần trước; việc tồn đọng hiện ngay trên thẻ liên quan bằng một dòng cảnh báo, bấm mở màn hình gốc), rồi hai nhóm khối **Chuyên cần** (**Giảng dạy** ở màn tổ) và **Rèn luyện**. Biểu đồ theo tuần chỉ vẽ một đường cho phạm vi đang xem (màn lớp thêm một đường xám là trung bình khối); so sánh nhiều phân hiệu, khối hay môn dùng thanh ngang một màu, dưới ngưỡng tô đỏ, nên thêm bao nhiêu nhóm cũng không phải thêm màu. Không có danh sách hay bảng so sánh chi tiết; phần đó dành cho MSMS AI (BR-29), mở từ avatar nổi trên màn Tình hình của Principal và Vice Principal. Thiếu dữ liệu thì ẩn mức thay đổi hoặc đường xu hướng; phân hiệu, khối không có học sinh không được vẽ.

**Tình hình toàn trường (Principal):** thẻ chuyên cần (cảnh báo học sinh vắng liên tiếp), vắng không phép, tiết bị ảnh hưởng (cảnh báo tiết chưa có người dạy), tỷ lệ nộp bài. **Chuyên cần**: đường 8 tuần của phạm vi đang xem cạnh cơ cấu điểm danh 4 tuần (có mặt, đi muộn, vắng có phép, vắng không phép); chuyên cần theo phân hiệu (khi xem cả trường), theo khối, theo thứ trong tuần. **Rèn luyện**: tỷ lệ nộp 8 tuần cạnh số bài đang mở, số bài nộp dưới 50% và tỷ lệ nộp theo khối. Principal không đọc đơn nghỉ nên không có số đơn chờ duyệt; lớp chưa có chủ nhiệm nằm ở Thống kê hệ thống.

**Tình hình phân hiệu (Vice Principal):** cùng khung với Principal, thiên về điều hành trong ngày. Thẻ tiết hôm nay bị ảnh hưởng (cảnh báo tiết chưa có người dạy), chuyên cần (cảnh báo học sinh vắng liên tiếp), tiết chưa điểm danh, tỷ lệ nộp bài. **Chuyên cần**: đường 8 tuần cạnh danh sách **Hôm nay** (các tiết hôm nay bị ảnh hưởng, chưa xử lý lên đầu); cơ cấu điểm danh, theo khối, theo thứ. **Rèn luyện** như Principal. Vice Principal không phân công chủ nhiệm nên không có mục lớp chưa có chủ nhiệm.

**Tình hình tổ chuyên môn (tổ trưởng):** thẻ tiết bị ảnh hưởng (cảnh báo thầy cô vắng hôm nay), tiết chưa điểm danh, tỷ lệ nộp bài môn của tổ (cảnh báo số thầy cô chưa giao bài trong 4 tuần), bài đang mở. **Giảng dạy**: tiết của tổ bị ảnh hưởng theo tuần (dạy thay, hủy, chưa xử lý, kèm số giáo viên vắng) cạnh tải dạy từng thầy cô (tiết mỗi tuần theo thời khóa biểu đã phát hành, vạch trung bình tổ, không có định mức). **Rèn luyện**: tỷ lệ nộp 8 tuần cạnh số bài đang mở, số bài nộp dưới 50% và tỷ lệ nộp theo môn. Thầy cô tính là thành viên khi thuộc tổ hoặc đang dạy môn của tổ. Chỉ đọc; không xem bài làm hay điểm của từng học sinh qua màn này.

**Tình hình lớp chủ nhiệm (chủ nhiệm):** thẻ sĩ số có mặt hôm nay (học sinh có ít nhất một lượt Có mặt hoặc Muộn), chuyên cần kèm trung bình khối (cảnh báo học sinh vắng liên tiếp, mở Danh sách học sinh), đơn nghỉ chờ duyệt, tỷ lệ nộp bài. **Chuyên cần**: đường 8 tuần của lớp và đường xám trung bình khối, cạnh cơ cấu điểm danh. **Rèn luyện**: tỷ lệ nộp 8 tuần cạnh số bài đang mở, số bài nộp dưới 50% và tỷ lệ nộp theo môn. Không có bài nộp hay điểm của từng em.

**Thống kê hệ thống (School Admin):** tài khoản theo loại (đang hoạt động, chờ đổi mật khẩu, đang khóa, chưa đăng nhập), quy mô trường, dữ liệu cần bổ sung (học sinh chưa xếp lớp hoặc chưa liên kết phụ huynh, cán bộ chưa có tài khoản, giáo viên chưa thuộc tổ; chỉ hiện mục lớn hơn 0, mỗi dòng mở màn hình sửa; không có mục về trách nhiệm vì School Admin không gán trách nhiệm), hoạt động nhật ký 7 ngày gần nhất, tiến độ phát hành thời khóa biểu theo phân hiệu. Không có số liệu giảng dạy đứng tên người khác (BR-02).

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
| Phân tích bằng AI | Đang phân tích / Xong / Lỗi | Lỗi để trống, không thử lại |
| Phiên bản thời khóa biểu | Nháp / Đã phát hành | Phiên bản Đã phát hành là hồ sơ của một lần import, đọc lại được như lịch sử; bản Nháp chưa phát hành xóa được (BR-12) |
| Bài đăng | Đã đăng / Đã ẩn | Không có trạng thái nháp |
| Học sinh | Đang học / Đã chuyển trường / Đã tốt nghiệp | |

### 6.2. Trạng thái suy ra — không lưu

**Mọi truy vấn nghiệp vụ đi qua cùng một cách tính, không tự viết lại điều kiện.** "Hôm nay" luôn theo giờ Việt Nam.

| Trạng thái | Điều kiện |
| --- | --- |
| Tiết **đã diễn ra** | Đã xếp lịch **và** (đã có ít nhất một bản ghi điểm danh **hoặc** ngày của tiết < hôm nay) |
| Tiết **bị khóa** | Đã diễn ra, hoặc **đã kết thúc**: ngày của tiết < hôm nay, hoặc là hôm nay và đã quá giờ kết thúc của khung giờ. Tiết đang diễn ra chưa bị khóa. Tiết bị khóa không đổi giáo viên, không hủy |
| Điểm danh **có phép** | Vắng **và** có đơn Đã duyệt của học sinh phủ ngày của tiết, cùng buổi hoặc cả ngày |
| Điểm danh **không phép** | Vắng và không có đơn như trên |
| Bài **đã đóng** | Thời điểm hiện tại > hạn nộp |
| Xếp lớp **hiệu lực tại ngày D** | Ngày bắt đầu ≤ D **và** (ngày kết thúc để trống **hoặc** ≥ D); để trống nghĩa là tới hết năm học |

Nhãn cuối của điểm danh có bốn giá trị: **Có mặt / Muộn / Vắng có phép / Vắng không phép**. Đơn duyệt muộn tự đổi nhãn các buổi vắng đã ghi, không cần chạy lại gì.

---

## 7. Quy tắc nghiệp vụ

**29 quy tắc.** Những ràng buộc cố tình không đặt nằm ở mục 7.8.

### 7.1. Danh tính và quyền

**BR-01 — Loại tài khoản.** Sáu loại ở mục 2.1, mỗi tài khoản mang đúng một loại. **Một hồ sơ có đúng một tài khoản.** Cấp sai loại thì đổi loại tài khoản — một thao tác, ghi Audit, cấp lại mật khẩu tạm và đưa tài khoản về Chờ đổi mật khẩu; hồ sơ và mọi lịch sử nghiệp vụ giữ nguyên vì chúng gắn với hồ sơ, không gắn với tài khoản. Quyền = loại tài khoản + trách nhiệm hoặc phụ trách chuyên môn đang hiệu lực + đối tượng + trạng thái đối tượng. Không có bảng quyền động, không đa vai trò, không chọn vai trò sau đăng nhập.

**BR-02 — School Admin.** Vượt phạm vi: thao tác mọi phân hiệu, lớp, môn mà không cần phụ trách chuyên môn. Không làm việc đứng tên người khác: không làm hành vi ở mục 2.4, không giả đăng nhập, không duyệt đơn nghỉ, không đọc hội thoại, không làm thời khóa biểu. Riêng bảng tin, School Admin chỉ đọc, không đăng hay ẩn bài (BR-27). Không vượt điều kiện nghiệp vụ: vẫn bị chặn khi sửa tiết bị khóa, khi dữ liệu sai mẫu.

**BR-03 — Mã hồ sơ và tên đăng nhập.** Hệ thống sinh **mã hồ sơ** khi tạo hồ sơ, không nhập tay, không sửa: tiền tố `HS` / `CB` / `PH` + 6 chữ số lấy từ **một bộ đếm chung cho cả ba loại**, nên không có hai hồ sơ nào trùng phần số. **Tên đăng nhập** = tên + chữ cái đầu họ và đệm + tiền tố + phần số, bỏ dấu, chữ thường — Trần Đức Kiên mã `HS186194` → `kientdhs186194`. Phần số duy nhất nên tên đăng nhập không trùng, không có cơ chế hậu tố. Người vừa là cán bộ vừa là phụ huynh có hai hồ sơ, hai mã, hai tên đăng nhập.

**BR-04 — Mật khẩu.** School Admin tạo tài khoản đơn lẻ cho hồ sơ chưa có tài khoản, hoặc chọn nhiều dòng trên danh bạ để tạo hàng loạt: danh bạ học sinh → Pupil, phụ huynh → Parent, cán bộ → chọn một loại cho cả lô. Mỗi tài khoản nhận **mật khẩu tạm** sinh ngẫu nhiên, hiển thị **một lần** trong kết quả tạo kèm file Excel tải ngay. Tài khoản ở Chờ đổi mật khẩu; lần đăng nhập đầu bắt buộc đổi, sau đó thành Đang hoạt động. Mật khẩu mới tối thiểu 8 ký tự và khác mật khẩu hiện tại. Mật khẩu chỉ lưu dạng băm.

**Quên mật khẩu:** nhập tên đăng nhập → nếu tài khoản Đang hoạt động và hồ sơ có email thì gửi **OTP 6 chữ số** tới email đó → nhập đúng thì đặt mật khẩu mới (mã chỉ dùng một lần; đặt xong mọi phiên của tài khoản bị thu hồi). OTP hết hạn sau 10 phút, sai 5 lần thì mất hiệu lực, gửi lại sau tối thiểu 60 giây; mỗi tài khoản chỉ có một OTP còn hiệu lực. Màn hình luôn trả cùng một thông điệp, không tiết lộ tài khoản có tồn tại hay có email không. **Hồ sơ không có email:** School Admin cấp lại mật khẩu tạm. Người dùng sửa email của mình ở hồ sơ cá nhân như một trường thông tin bình thường.

**BR-05 — Khóa tài khoản.** Phiên đăng nhập dùng access token sống 15 phút và refresh token sống 7 ngày. Web: refresh token trong cookie HttpOnly, access token chỉ giữ trong bộ nhớ trang; Mobile: nhận cả hai token trong body. Tài khoản Đã khóa không đăng nhập được; phiên đang mở bị từ chối ở request kế tiếp, vì Backend đọc trạng thái tài khoản mỗi request. School Admin khóa và mở tay; mở khóa đưa tài khoản chưa từng đổi mật khẩu lần đầu về Chờ đổi mật khẩu, còn lại về Đang hoạt động. Đăng xuất **thu hồi** phiên trên **thiết bị hiện tại**: token của phiên đó mất hiệu lực ngay ở Backend, không chỉ bị client bỏ đi; người dùng không có "đăng xuất mọi thiết bị". Khóa tài khoản, School Admin reset mật khẩu hoặc đổi loại tài khoản thì Backend thu hồi mọi phiên của tài khoản đó; người dùng tự đổi mật khẩu thì các thiết bị khác phải đăng nhập lại. Hồ sơ và lịch sử giữ nguyên.

**Khóa tài khoản không bị chặn bởi lịch dạy.** Trong cùng thao tác khóa (một transaction), với tài khoản loại Teacher: mọi tiết Đã xếp lịch chưa bị khóa của giáo viên đó từ hôm nay trở đi, ở bất kỳ phân hiệu nào, chuyển sang **Chờ xử lý** (Chỉnh tay, **để trống giáo viên**) và Vice Principal của phân hiệu chứa tiết nhận thông báo để xử lý theo BR-16; với mọi tài khoản cán bộ, mọi trách nhiệm đang hiệu lực của người đó (phụ trách phân hiệu, tổ trưởng, chủ nhiệm) được **đặt ngày kết thúc là ngày khóa** và **quyền tương ứng mất ngay lúc khóa** (BR-09). Mở khóa lại không khôi phục các trách nhiệm đã đóng; muốn có lại thì gán mới. Trước khi khóa, màn hình cảnh báo số tiết và phân hiệu bị ảnh hưởng, cùng danh sách trách nhiệm sẽ đóng. Đổi loại tài khoản cũng đóng trách nhiệm, và chuyển tiết nếu loại cũ là Teacher, theo cùng cách.

Học sinh chuyển trường hoặc tốt nghiệp thì tài khoản học sinh tự khóa trong cùng thao tác.

**BR-06 — Ai được dạy.** Người làm giáo viên của tiết hoặc xuất hiện trong file thời khóa biểu phải có **tài khoản loại Teacher không ở trạng thái Đã khóa** — Chờ đổi mật khẩu vẫn hợp lệ, vì đầu năm giáo viên chưa kịp đăng nhập lần đầu. Điều kiện này kiểm **khi gán** giáo viên cho tiết hoặc khi import file; tiết Chờ xử lý để trống giáo viên nên không có gì để kiểm, và giáo viên được chọn cho tiết đó phải qua kiểm tra này. Principal, Vice Principal, School Admin không dạy.

### 7.2. Danh mục, hồ sơ, khoảng hiệu lực

**BR-07 — Danh mục nền.** Phân hiệu có mã và tên duy nhất — tên là thứ các file import tra cứu. Tổ chuyên môn có tên duy nhất. Môn học thuộc một tổ chuyên môn đang hoạt động, có tên duy nhất và có **viết tắt duy nhất toàn trường, chỉ gồm chữ và số** (vì dấu `-` là dấu tách trong ô `MÔN-GV`). Khung giờ tiết **dùng chung toàn trường**, khóa là cặp **(Buổi, Tiết)** — Sáng tiết 1 và Chiều tiết 1 là hai khung khác nhau; số khung mỗi buổi do trường tự khai, không cố định; giờ của các khung đang dùng không được chồng nhau và phải đúng thứ tự (tiết trước kết thúc trước khi tiết sau bắt đầu, buổi Sáng trước buổi Chiều); mọi tiết học kể cả tiết bù phải nằm đúng một khung. Số tiết mỗi buổi đánh từ 1 đến 20. Khung giờ đang có tiết học hoặc thời khóa biểu tham chiếu thì **không ngừng dùng được**, chỉ sửa giờ. Lớp thuộc một phân hiệu và một năm học, tên lớp duy nhất trong phạm vi đó, khối 6–9. Danh mục đã có dữ liệu tham chiếu thì **ngừng hoạt động**, không xóa.

**BR-08 — Năm học, học kỳ, ngày nghỉ.** Đúng một năm học được đánh dấu hiện hành. Hai năm học không được chồng khoảng thời gian. Tên năm học duy nhất; một năm học dài tối đa 13 tháng. Năm học chỉ xóa được khi chưa có lớp, ngày nghỉ hay bản thời khóa biểu nào, và không phải năm hiện hành (trừ khi đó là năm học duy nhất). Mỗi năm có đúng hai học kỳ, nằm trong năm học, không chồng nhau, kỳ 1 trước kỳ 2; học kỳ 1 kết thúc muộn nhất ngày 18/01 và học kỳ 2 muộn nhất ngày 31/05 của năm kết thúc năm học. Học kỳ chỉ là mốc lọc và mốc Phát hành; không có quy trình chuyển kỳ. Ngày nghỉ gắn năm học, cấp **toàn trường** hoặc **một phân hiệu**. Ngày nghỉ khai trước khi Phát hành thì Phát hành bỏ qua. Ngày nghỉ khai sau khi tiết đã sinh thì **hệ thống hủy luôn mọi tiết Đã xếp lịch hoặc Chờ xử lý chưa bị khóa** (mục 6.2) của ngày đó, thuộc toàn trường hoặc đúng phân hiệu của ngày nghỉ, trong cùng thao tác khai: tiết chuyển Đã hủy (Chỉnh tay, giữ nguyên giáo viên để truy vết), giáo viên liên quan và học sinh của lớp nhận thông báo (BR-26), ghi một bản Audit (BR-28). Tiết đã bị khóa giữ nguyên. Đây là hệ quả của việc khai ngày nghỉ chứ không phải School Admin thao tác lịch dạy (BR-02). Xóa ngày nghỉ không khôi phục các tiết đã hủy; muốn dạy lại thì Vice Principal thêm tiết bù (BR-15).

**BR-09 — Khoảng hiệu lực.** Trách nhiệm và xếp lớp đều có ngày bắt đầu và ngày kết thúc; **để trống ngày kết thúc nghĩa là còn hiệu lực** (với xếp lớp là tới hết năm học). Mỗi đích có **tối đa một bản đang hiệu lực**:

- một phân hiệu — một Vice Principal phụ trách
- một tổ chuyên môn — một tổ trưởng
- một lớp — một chủ nhiệm
- một học sinh — một lượt xếp lớp mở trong một năm học

**Chuyển lớp = một thao tác, một transaction:** đặt ngày kết thúc của lượt xếp lớp cũ là **ngày liền trước** ngày hiệu lực của lượt mới, rồi mở lượt mới; chỉ chuyển sang lớp **cùng khối** và cùng năm học — hai khoảng không được chạm nhau một ngày nào.

**Thay người đảm nhiệm trách nhiệm = một thao tác, một transaction, có hiệu lực ngay hôm nay:** bản của người cũ kết thúc hôm nay và người cũ mất quyền ngay lúc thay, bản của người mới bắt đầu từ hôm nay. Không hẹn thay người vào một ngày sau; muốn đổi người vào ngày sau thì đặt ngày kết thúc cho người hiện tại là ngày đó rồi gán người mới bắt đầu từ ngày đó.

**Chủ nhiệm gắn với lớp của một năm học:** bản chủ nhiệm có ngày kết thúc là **ngày cuối năm học của lớp**, đặt sẵn lúc gán, nên hết năm học thì chủ nhiệm tự hết hiệu lực, không cần thao tác nào và không phụ thuộc kết chuyển. Ngày hiệu lực không được sau ngày cuối năm học của lớp. Lớp của năm sau chưa có chủ nhiệm cho tới khi được gán. Phụ trách phân hiệu và tổ trưởng không gắn với năm học nên để trống ngày kết thúc cho tới khi thay người hoặc kết thúc.

Bản cũ giữ nguyên làm lịch sử; dữ liệu đã phát sinh không bị đụng tới.

**Kết thúc không do thay người** (khóa tài khoản, đổi loại tài khoản, bấm kết thúc trách nhiệm): ngày kết thúc là **ngày thao tác** — riêng bấm kết thúc trách nhiệm cho chọn ngày, mặc định hôm nay — và với ngày kết thúc là hôm nay quyền **mất ngay tại thời điểm thao tác**, không đợi hết ngày. Khoảng hiệu lực chỉ dùng để tra cứu theo ngày; kiểm quyền tại thời điểm xử lý dựa trên việc bản ghi đã có ngày kết thúc từ hôm nay trở về trước hay chưa. Mở khóa tài khoản không mở lại bản cũ; gán lại trong cùng ngày thì tạo **bản mới** bắt đầu từ hôm nay, không sửa bản cũ; bản cũ và bản mới có thể cùng nằm trong một ngày nhưng chỉ có một bản đang hiệu lực tại mọi thời điểm. **Lịch hẹn** (bản có ngày bắt đầu sau hôm nay) chưa từng có hiệu lực nên **hủy được** (xóa hẳn, không để lại lịch sử); hủy xong thì gán được người khác cho đích từ hôm nay. Khóa tài khoản, đổi loại tài khoản và ngừng hoạt động hồ sơ cán bộ tự hủy mọi lịch hẹn của người đó, để tới ngày hẹn đích không rơi vào người không còn làm việc.

**Phân hiệu đang hoạt động không bị bỏ trống Hiệu phó.** Chỉ Hiệu phó đang phụ trách mới thao tác thời khóa biểu, lịch dạy và khai báo giáo viên vắng của phân hiệu (Hiệu trưởng chỉ đọc). Vì vậy bấm kết thúc phụ trách phân hiệu **có hiệu lực hôm nay** bị chặn khi phân hiệu còn lớp hoạt động trong năm học hiện hành và chưa có người kế nhiệm: dùng Thay người để giao cho Hiệu phó khác. Kết thúc vào **ngày sau** (bàn giao có hẹn) vẫn được. Phân hiệu không còn lớp hoạt động thì kết thúc tự do. Khóa hoặc đổi loại tài khoản (BR-05) vẫn đóng trách nhiệm như đã nêu, không bị chặn bởi quy tắc này.

Phụ trách phân hiệu chỉ gán cho Vice Principal; tổ trưởng và chủ nhiệm chỉ gán cho Teacher.

**BR-10 — Hồ sơ người.** Ba loại hồ sơ: học sinh, cán bộ, phụ huynh. **Họ tên, ngày sinh và số định danh cá nhân (12 chữ số) bắt buộc** với hồ sơ học sinh và cán bộ; số định danh **duy nhất toàn trường** và là khóa nhận diện khi import. Ngày sinh phải cho tuổi tròn tính đến hôm nay từ 18 đến 75 với cán bộ, từ 5 đến 25 với học sinh. Số định danh không sửa được sau khi tạo. Cán bộ bắt buộc có **phân hiệu chính**, có thể có **viết tắt giáo viên duy nhất toàn trường, chỉ gồm chữ và số**; không có viết tắt thì không xuất hiện được trong file thời khóa biểu. Hồ sơ cán bộ ngừng hoạt động chứ không xóa, và bị chặn khi người đó còn giữ trách nhiệm hoặc còn tiết Đã xếp lịch chưa kết thúc. Phụ huynh bắt buộc có **số điện thoại** — đó là khóa nhận diện của phụ huynh.

**Mỗi học sinh có đúng một phụ huynh đại diện; một phụ huynh liên kết được nhiều học sinh.** Đổi phụ huynh đại diện làm trên giao diện.

### 7.3. Import Excel

**BR-11 — Import Excel.** Ba file hồ sơ: cán bộ, học sinh và phụ huynh (file thời khóa biểu theo BR-12). **Import một bước:** người dùng gửi file, hệ thống validate **toàn bộ file** rồi ghi, tất cả trong một transaction. Có bất kỳ lỗi nào thì **không ghi gì**, trả bảng lỗi kèm số dòng, cột, giá trị gốc; người dùng sửa file và gửi lại.

**Nhận diện người đã tồn tại:** học sinh và cán bộ theo **Số định danh cá nhân** (cột bắt buộc, duy nhất; hai người trùng họ tên và ngày sinh vẫn là hai người nếu khác số định danh); phụ huynh theo **số điện thoại**. Hai dòng trong cùng file trùng khóa nhận diện là lỗi. Người đã tồn tại thì **bỏ qua cả dòng** và báo ở mục "Đã có, bỏ qua" — import **không cập nhật** hồ sơ cũ; sửa hồ sơ làm trên giao diện. Nhờ vậy chạy lại cùng một file không tạo bản trùng.

**File học sinh** có hai ô meta bắt buộc là Năm học và Phân hiệu, áp dụng cho mọi dòng. Một dòng = một học sinh kèm phụ huynh đại diện, tạo tối đa bốn thứ: hồ sơ học sinh, xếp lớp (chỉ khi cột Tên lớp có giá trị; ngày hiệu lực = ngày bắt đầu năm học), hồ sơ phụ huynh, và liên kết. Các dòng cùng số điện thoại dùng chung một hồ sơ phụ huynh và liên kết nhiều con. Lỗi: tên lớp không khớp lớp của (phân hiệu, năm học); cùng số điện thoại mà họ tên phụ huynh khác nhau. Hệ thống **không tự tạo lớp**.

**File phụ huynh** chỉ tạo hồ sơ phụ huynh (họ tên và số điện thoại bắt buộc), không tạo tài khoản và không liên kết học sinh; liên kết làm ở file học sinh hoặc ở thao tác đổi phụ huynh đại diện.

Danh sách cột bắt buộc và tùy chọn của từng file nằm trong `MSMS_Excel_Import_Templates.xlsx`.

### 7.4. Thời khóa biểu và lịch dạy

**BR-12 — File thời khóa biểu.** Một bố cục Excel lưới duy nhất: hàng là Thứ × Buổi × Tiết (Thứ 2–7), cột là lớp. Ba ô meta bắt buộc: Phân hiệu, Năm học, Học kỳ. Mỗi ô ghi **`MÔN-GV`** (viết tắt môn – viết tắt giáo viên); ô trống là tiết trống. Đối chiếu không phân biệt hoa thường, bỏ khoảng trắng thừa; dấu `-` đầu tiên là dấu tách.

**File mẫu do hệ thống sinh, không phải file tĩnh.** Vice Principal chọn phân hiệu + học kỳ và bấm "Tải mẫu"; Backend dựng file ngay lúc đó: số hàng theo **khung giờ đã khai**, số cột theo **lớp của (phân hiệu, năm học)**, ba ô meta điền sẵn, và **nội dung điền sẵn theo phiên bản Đã phát hành gần nhất** của (phân hiệu, học kỳ) đó — chưa có phiên bản nào thì để trống. Nhờ vậy sửa lịch vài lớp là tải về, sửa vài ô, xóa các cột lớp không đổi, tải lên.

**File chỉ cần gồm các lớp muốn đổi.** Đọc các cột lớp từ cột D sang phải tới ô tiêu đề trống đầu tiên. Mỗi lớp có trong file được ghi đè hoàn toàn khi Phát hành — cột lớp để trống toàn bộ nghĩa là lớp không có tiết nào; lớp không có trong file giữ nguyên lịch hiện hành.

Lỗi kèm địa chỉ ô: viết tắt môn hoặc viết tắt giáo viên không khớp; giáo viên không đủ điều kiện dạy (BR-06); tên lớp không thuộc (phân hiệu, năm học); hai cột cùng một tên lớp; hàng tiêu đề bị sửa; hàng có (Buổi, Tiết) không nằm trong khung giờ đã khai; hai hàng cùng (Thứ, Buổi, Tiết); một giáo viên xuất hiện ở hai lớp trong cùng (Thứ, Buổi, Tiết); ba ô meta sai hoặc phân hiệu đã ngừng hoạt động; file không có cột lớp nào. **Có một lỗi là từ chối cả file**, không import một phần; bảng lỗi hiện tối đa 300 lỗi đầu tiên kèm tổng số lỗi. File hợp lệ được lưu thành một phiên bản **Nháp** chỉ chứa các lớp có trong file.

**Sửa bản Nháp.** Trước khi Phát hành, Vice Principal sửa bản Nháp ngay trên lưới, không cần import lại. Chỉ sửa được phiên bản đang Nháp, và chỉ trong các lớp của phiên bản đó. Bốn thao tác:

- **Kéo tiết** sang ô khác trong cùng cột lớp: thả vào ô trống thì chuyển tiết, thả vào ô có tiết thì đổi chỗ hai tiết. Không kéo sang lớp khác.
- **Thêm tiết** vào ô trống: chọn một môn đang dùng của trường và một giáo viên.
- **Đổi giáo viên** của tiết.
- **Xóa tiết.**

Giáo viên được chọn phải đủ điều kiện như file import (BR-06, BR-12). Thao tác gây xung đột (BR-13) **vẫn được lưu** và ô xung đột được đánh dấu tại ô, để Vice Principal đi qua trạng thái trung gian khi đổi nhiều tiết; còn xung đột thì Phát hành bị chặn (BR-14). Mỗi thao tác lưu ngay vào bản Nháp; ô khác với nội dung file import được đánh dấu, và khôi phục được toàn bộ bản Nháp về nội dung file import. Bản Nháp không dùng nữa thì Vice Principal phụ trách phân hiệu xóa hẳn được (hỏi xác nhận); phiên bản Đã phát hành không xóa được. Sửa hay xóa bản Nháp không đụng tới lịch dạy, không tạo thông báo và không ghi Audit.

**BR-13 — Xung đột lịch.** Trùng lịch = cùng **ngày** và cùng **khung giờ**. Trong toàn trường, mỗi lớp có tối đa một tiết Đã xếp lịch hoặc Chờ xử lý, và mỗi giáo viên có tối đa một tiết Đã xếp lịch, tại một (ngày, khung giờ). Tiết Chờ xử lý để trống giáo viên nên không chiếm chỗ của ai (BR-16). Áp dụng cho bản Nháp (BR-12), Phát hành, mọi điều chỉnh tay, và lọc gợi ý giáo viên rảnh. Giáo viên dạy nhiều phân hiệu được kiểm chung. Khi kiểm bản Nháp, các lớp không có trong phiên bản vẫn được tính theo lịch hiện hành, vì chúng không bị ghi đè.

**BR-14 — Phát hành và Phát hành lại.** Vice Principal Phát hành một phiên bản Nháp. Khi bấm Phát hành, hệ thống chạy **xem trước** trong hộp xác nhận và hiển thị: số tiết sẽ xóa và sẽ tạo, nhóm theo lớp và ngày; **cảnh báo số tiết Chỉnh tay, tiết bù và tiết Chờ xử lý sẽ bị xóa**, người dùng phải xác nhận; **danh sách lớp của (phân hiệu, năm học) vẫn không có tiết nào** trong học kỳ sau Phát hành (cảnh báo, không chặn); và danh sách xung đột giáo viên. Mỗi dòng xung đột ghi rõ **lớp và phân hiệu đang chiếm chỗ giáo viên**; Vice Principal xử lý bằng cách đổi sang giáo viên khác cho ô trùng trong bản Nháp.

**Còn xung đột giáo viên là chặn Phát hành.** Phát hành cũng bị chặn khi bản Nháp có lớp đã ngừng hoạt động hoặc giáo viên không còn đủ điều kiện dạy (BR-06); thông báo nêu rõ lớp hoặc giáo viên để xử lý.

Phát hành chạy trong **một transaction**, hai bước:

1. **Xóa** mọi tiết của **các lớp có trong phiên bản**, trong học kỳ đó, **từ ngày mai trở đi** — **ghi đè sạch**, không giữ lại tiết đã chỉnh tay. Lớp không có trong phiên bản không bị đụng tới.
2. **Sinh lại** từ phiên bản, khoảng từ **max(ngày mai, ngày bắt đầu học kỳ)** đến hết học kỳ, chỉ các ngày không phải ngày nghỉ của toàn trường hoặc của phân hiệu đó.

**Tiết đã qua và tiết đã có điểm danh không bao giờ bị đụng tới.**

Xem trước **không được lưu lại**; lúc bấm Phát hành hệ thống tính lại và thực hiện, nên con số cuối cùng mới là con số ghi vào phiên bản. Phát hành thành công: phiên bản thành Đã phát hành; **phiên bản trước không bị đổi trạng thái** — mỗi phiên bản đã phát hành là hồ sơ của một lần import, đọc lại được như lịch sử. Hệ thống gửi **một thông báo chung** (BR-26) và ghi Audit (BR-28). **Phát hành lại** là import file mới cho cùng (phân hiệu, học kỳ) rồi Phát hành theo đúng hai bước trên.

**Nguồn sự thật của thời khóa biểu hiện hành là lịch dạy**, không phải phiên bản — vì chỉ lịch dạy mới phản ánh các điều chỉnh tay ở BR-15 và BR-16. Mọi màn hình xem lịch đều đọc từ lịch dạy.

**Phụ trách chuyên môn** (giáo viên – môn – lớp) không nhập tay và không lưu: hệ thống tính từ thời khóa biểu mỗi khi cần. Với mỗi lớp, lấy phiên bản **Đã phát hành mới nhất của lớp** trong học kỳ hiện hành — lớp không có trong một lần Phát hành sau vẫn theo phiên bản trước đó của chính nó. Giáo viên có tiết của một môn trong phiên bản ấy **đang phụ trách chuyên môn** môn đó ở lớp đó; số tiết mỗi tuần là số ô của (lớp, môn, giáo viên) trong phiên bản. Một (lớp, môn) có thể có nhiều giáo viên. Chưa Phát hành thì chưa có phụ trách chuyên môn. Điều chỉnh lịch và dạy thay (BR-15, BR-16) chỉ đổi lịch dạy nên không đổi phụ trách chuyên môn; muốn đổi thì sửa thời khóa biểu rồi Phát hành lại.

Quyền với bài tập của một (lớp, môn) **luôn theo phụ trách chuyên môn đang hiệu lực**, không theo người tạo: Phát hành đổi giáo viên thì người mới tiếp quản, người cũ mất quyền ngay.

Phát hành ghi Audit (BR-28) kèm số tiết xóa và tạo, và gửi **một thông báo chung** "đã có thời khóa biểu mới" cho giáo viên có tiết trong phiên bản và học sinh của các lớp trong phiên bản (BR-26); hệ thống không so sánh lịch cũ với lịch mới. Học kỳ đã kết thúc thì không còn ngày nào để sinh tiết và không Phát hành được.

**BR-15 — Điều chỉnh lịch.** Vice Principal của phân hiệu thao tác trên tiết **chưa bị khóa** (mục 6.2). Mọi thao tác đặt nguồn tiết thành **Chỉnh tay**, kiểm BR-13, tạo thông báo cho giáo viên liên quan và học sinh của lớp, và ghi Audit. Giáo viên được phân công hoặc mất tiết (đổi giáo viên, hủy, tiết bù) còn nhận email (mục 3.2).

- **Đổi giáo viên:** thay giáo viên của tiết. Không đổi phụ trách chuyên môn (BR-14).
- **Hủy:** tiết → Đã hủy. Tiết Đã hủy không dạy, không điểm danh, không tính chuyên cần.
- **Thêm tiết bù:** tạo tiết mới ở một (ngày, khung giờ) trống của lớp, chọn môn và giáo viên; ngày phải là ngày học của phân hiệu (không Chủ nhật, không ngày nghỉ), ≥ hôm nay, trong một học kỳ của năm học. Tiết bù là một tiết thêm mới, không liên kết với tiết đã hủy nào.

**Đổi giờ một tiết = hủy tiết cũ + thêm tiết bù**; không có thao tác "dời" riêng.

**BR-16 — Giáo viên vắng.** **Tổ trưởng** khai báo giáo viên **thuộc tổ mình** (theo tổ ghi trong hồ sơ) vắng, theo một trong hai cách: **theo ngày** (từ ngày – đến ngày, vắng cả ngày) hoặc **theo tiết** (một ngày, chọn các tiết vắng trong ngày, liền hay rời đều được), kèm lý do (tùy chọn). Chỉ khai từ hôm nay trở đi, một khai báo tối đa 90 ngày. Vice Principal không khai báo. Đây **không phải đơn xin nghỉ của cán bộ** — MSMS chỉ ghi nhận hệ quả lên lịch dạy.

Khai báo **không tự đổi lịch dạy**: mọi tiết Đã xếp lịch chưa bị khóa của giáo viên đó trong phạm vi khai báo, **ở mọi phân hiệu**, chuyển sang **Chờ xử lý** (Chỉnh tay) và **để trống giáo viên**. Tiết trống không chiếm chỗ của giáo viên nào khi kiểm xung đột (BR-13). Mỗi tiết ghi lại khai báo đã đưa nó về Chờ xử lý. Khóa hoặc đổi loại tài khoản Teacher (BR-05) cũng đưa tiết về Chờ xử lý và để trống giáo viên theo cách này, nhưng không gắn với khai báo nào; Vice Principal của phân hiệu chứa tiết nhận thông báo trong trường hợp đó và xử lý riêng, tổ trưởng không thấy các tiết này.

**Tổ trưởng quyết định từng tiết bằng một đề xuất**, cho tiết của khai báo thuộc môn của tổ mình, một trong hai cách:

1. **Giáo viên dạy thay** → lấy từ danh sách gợi ý bên dưới.
2. **Hủy tiết** → muốn dạy lại thì thêm tiết bù sau khi duyệt (BR-15).

Tổ trưởng đổi hoặc bỏ đề xuất được cho tới khi **gửi duyệt**. Đề xuất **chưa đổi lịch**: tiết vẫn Chờ xử lý, để trống giáo viên. Tiết thuộc môn của tổ khác tổ trưởng không quyết định được; Vice Principal xử lý riêng.

**Tổ trưởng gửi duyệt.** Khi đã quyết định **mọi tiết thuộc môn của tổ mình** còn chờ của khai báo (tiết môn của tổ khác không chặn việc gửi), tổ trưởng bấm **Gửi duyệt** cho cả khai báo. Từ lúc gửi, đề xuất bị **khóa** và khai báo không xóa được cho tới khi Vice Principal duyệt hoặc trả lại; Vice Principal của mỗi phân hiệu có tiết nhận thông báo (BR-26) và chỉ từ lúc này mới thấy khai báo để duyệt.

**Vice Principal duyệt.** Chỉ duyệt được khai báo tổ trưởng **đã gửi**. Vice Principal mỗi phân hiệu duyệt một lần cho phần của mình: mọi đề xuất cho tiết ở phân hiệu mình được áp cùng lúc — tiết thành **Đã xếp lịch** với giáo viên dạy thay, hoặc **Đã hủy** — kèm thông báo và email như xử lý riêng (BR-15, BR-26). Duyệt theo kiểu **tất cả hoặc không**: nếu một đề xuất không còn hợp lệ (giáo viên đã có tiết cùng khung giờ, đang vắng, không còn đủ điều kiện dạy, hoặc được đề xuất dạy hai lớp cùng khung giờ) thì không tiết nào được áp, hệ thống nêu tiết lỗi, và Vice Principal xử lý riêng tiết đó rồi duyệt lại, hoặc trả khai báo về cho tổ trưởng. Tiết chưa có đề xuất (môn của tổ khác) giữ Chờ xử lý cho Vice Principal xử lý riêng, không chặn việc duyệt phần còn lại. Tiết ở phân hiệu khác do Vice Principal phân hiệu đó duyệt.

**Vice Principal trả lại.** Vice Principal của phân hiệu có tiết chờ của khai báo đã gửi có thể **trả lại** cho tổ trưởng kèm **lý do bắt buộc** (tối đa 300 ký tự): đề xuất mở khóa để tổ trưởng sửa rồi gửi lại, tổ trưởng nhận thông báo (BR-26), lý do hiện cho tổ trưởng cho tới khi gửi lại. Thông báo gửi cho **tổ trưởng đương nhiệm** của tổ giáo viên vắng (tổ đã đổi tổ trưởng thì người mới nhận); tổ không còn tổ trưởng thì gửi cho người đã khai. Trả lại không đổi lịch và áp cho cả khai báo.

Vice Principal **xử lý riêng** từng tiết Chờ xử lý ngay trên lưới Lịch dạy ở chế độ Điều chỉnh (bấm vào tiết, chọn giáo viên dạy thay từ gợi ý, hoặc hủy), kể cả tiết lỗi lúc duyệt hay tiết môn của tổ khác. Riêng tiết **đang chờ tổ trưởng** — thuộc khai báo chưa gửi duyệt (đang soạn hoặc bị trả lại), môn thuộc tổ của giáo viên vắng, và tổ đang có tổ trưởng — thì Vice Principal **chưa xử lý được**: lưới hiện tiết là chờ tổ trưởng gửi duyệt. Tổ không còn tổ trưởng đương nhiệm thì không ai gửi duyệt được nên Vice Principal xử lý riêng như thường. Xử lý riêng và duyệt đều **xóa đề xuất** của tiết.

Tiết Chờ xử lý **không điểm danh được**. Tiết Chờ xử lý cũng bị khóa khi đã kết thúc (mục 6.2): không còn trong danh sách của SCD-03, không đề xuất, không duyệt, không dạy thay, không hủy được nữa. Khai báo **Đã qua** khi mọi tiết đã kết thúc, hoặc khi không còn tiết nào chờ mà có tiết đã trôi qua không ai xử lý: chỉ để tra cứu, không gửi duyệt, không duyệt, không trả lại được.

**Xóa khai báo vắng** theo kiểu tất cả hoặc không: **tổ trưởng đương nhiệm** của tổ giáo viên vắng xóa được (kể cả khai báo do tổ trưởng trước khai), khi **chưa có tiết nào** của khai báo bị xử lý (dạy thay, hủy), giáo viên **vẫn đủ điều kiện dạy** (BR-06) và **không trùng lịch** ở tiết nào (BR-13). Khi đó mọi tiết của khai báo chưa kết thúc trở về giáo viên, trạng thái Đã xếp lịch, các đề xuất bị bỏ; tiết đã kết thúc bị khóa nên giữ nguyên. Không đạt thì báo lỗi nêu rõ nguyên nhân và không xóa gì; xử lý các tiết còn lại rồi xóa lại. Tiết trống do khóa hoặc đổi loại tài khoản không thuộc khai báo nào nên không tự khôi phục.

**Gợi ý giáo viên rảnh:** Teacher đủ điều kiện dạy (BR-06), cùng tổ chuyên môn với môn của tiết, không có tiết Đã xếp lịch tại (ngày, khung giờ) đó ở bất kỳ phân hiệu nào (BR-13), không có khai báo vắng phủ ngày và tiết đó. Trả danh sách sắp theo tên, tối đa 10 người, kèm số tiết Đã xếp lịch của giáo viên trong ngày đó; **không xếp hạng ưu tiên** — người chọn tự quyết.

**BR-17 — Điểm danh.** Chỉ **giáo viên của tiết** điểm danh, chỉ trên tiết Đã xếp lịch, chỉ **từ 10 phút trước giờ bắt đầu tiết tới hết ngày của tiết** (23:59). Điểm danh dựa vào tiết, không dựa vào phụ trách chuyên môn — giáo viên dạy thay điểm danh được. Danh sách là học sinh đang thuộc lớp vào ngày của tiết. Giáo viên gửi một trong **ba giá trị**: Có mặt, Muộn, Vắng; kèm ghi chú tùy chọn. Mỗi học sinh có đúng một bản ghi cho mỗi tiết; lưu lại là cập nhật bản ghi đó, có hiệu lực ngay, không có bước chốt.

Sau ngày của tiết giáo viên không sửa được nữa. **Chỉ School Admin** nhập bù hoặc sửa, trên **cùng màn hình điểm danh**, cho tiết Đã xếp lịch có ngày ≤ hôm nay. Bản ghi lưu **người nhập, thời điểm, cờ nhập bù**. Vice Principal thấy số liệu nhưng không sửa. School Admin tìm tiết cần nhập bù bằng cách chọn lớp và khoảng ngày trên màn hình điểm danh.

**BR-18 — Có phép / không phép.** **Không lưu, luôn suy ra** (mục 6.2): Vắng + có đơn Đã duyệt của học sinh phủ ngày của tiết và đúng buổi ("sáng" = tiết khung Sáng, "chiều" = tiết khung Chiều, "cả ngày" = mọi tiết; đơn theo tiết lẻ chỉ phủ đúng các tiết đã chọn của buổi đó) → **có phép**; còn lại → **không phép**. Đơn duyệt muộn tự đổi nhãn các buổi vắng đã ghi, không cần chạy lại gì. **Đơn không tự sinh vắng:** học sinh có đơn mà vẫn đi học thì ghi Có mặt. Vì BR-19 không cho xin nghỉ cho ngày đã qua, buổi vắng mà phụ huynh báo muộn luôn là **không phép** — giới hạn này được chấp nhận.

**BR-19 — Đơn xin nghỉ.** Chỉ Parent gửi, chỉ cho con được liên kết đang học ở một lớp. Đơn chọn từ ngày – đến ngày và buổi (cả ngày / sáng / chiều) hoặc một số **tiết lẻ** (tiết 1–5) của buổi sáng hay chiều, áp dụng cho mọi ngày trong khoảng; kèm lý do. Phụ huynh cũng có thể gửi nhiều ngày rời nhau trong một lượt, mỗi ngày một buổi hoặc một số tiết riêng; mỗi ngày thành một đơn riêng và cả lượt chỉ được nhận khi mọi ngày hợp lệ. **Ngày bắt đầu phải ≥ hôm nay** — nghỉ đã xảy ra rồi thì không xin bù đơn; giáo viên đã ghi vắng và School Admin sửa bằng BR-17.

**Hai đơn của cùng một học sinh xung đột khi chồng ngày *và* chồng buổi *và* chồng tiết.** Một học sinh không được có hai đơn Chờ duyệt hoặc Đã duyệt xung đột với nhau. Hai đơn cùng buổi thì chồng; một đơn "cả ngày" chồng với mọi đơn khác; đơn nghỉ cả buổi chồng mọi tiết của buổi đó, còn hai đơn nghỉ theo tiết chỉ chồng khi có tiết chung. Nhờ vậy xin nghỉ **sáng** và **chiều** cùng một ngày bằng hai đơn riêng là hợp lệ.

Parent rút được khi còn Chờ duyệt. Khi gửi, đơn ghi lại **lớp học sinh đang học**. Người duyệt là **chủ nhiệm đang hiệu lực của lớp đó tại lúc duyệt** — đổi chủ nhiệm khi đơn còn chờ thì người mới thấy đơn; nhờ quy tắc ngày liền trước ở BR-09, tại mọi thời điểm chỉ có đúng một người như vậy. Lớp chưa có chủ nhiệm: đơn nằm chờ tới khi có chủ nhiệm được gán (PPL-04). Quyết định là cuối cùng; từ chối bắt buộc kèm lý do và phụ huynh thấy lý do đó; bị từ chối thì gửi đơn mới.

### 7.6. Bài tập và phân tích

**BR-20 — Câu hỏi.** Câu hỏi thuộc một môn, có khối (6–9) và chủ đề (bắt buộc), gồm 4 lựa chọn (A–D) với **từ 1 đến 3 đáp án đúng**; giải thích tùy chọn. Mỗi câu có **độ khó** bắt buộc, một trong bốn mức nhận thức: Nhận biết, Thông hiểu, Vận dụng, Vận dụng cao. Câu có từ trước khi có độ khó là **Chưa phân loại**: người tạo chọn độ khó sau (kể cả khi câu đã nằm trong bài tập), và câu chưa phân loại chưa đưa vào bài mới được. Giáo viên chỉ soạn câu (kể cả nhờ AI) cho các môn thuộc **tổ chuyên môn của mình**. Soạn thủ công **hoặc từ nháp AI**: giáo viên chọn môn, khối, bài trong sách giáo khoa và số câu ở từng độ khó (tổng 1 đến 10 mỗi lần) → MSMS AI đọc các trang của bài và trả về các câu Nháp gắn cờ AI → giáo viên đối chiếu với trang sách, sửa và xác nhận thành **Sẵn dùng**. AI chỉ soạn từ các trang của bài đã chọn trong bộ sách giáo khoa Kết nối tri thức với cuộc sống (bộ dùng thống nhất toàn quốc theo Quyết định 3588/QĐ-BGDĐT); bộ sách không phải lựa chọn của người dùng. Câu do AI soạn lưu bài nguồn, chủ đề của câu là tên bài. Chỉ câu Sẵn dùng mới đưa vào bài tập được.

Ngân hàng câu hỏi **dùng chung toàn trường**: mọi Teacher xem và dùng câu Sẵn dùng của mọi môn; câu Nháp và câu đã ẩn chỉ người tạo thấy; **chỉ người tạo sửa** được. Câu hỏi đã nằm trong một bài tập thì không sửa được nữa — chỉ còn **ẩn khỏi ngân hàng**; ẩn không ảnh hưởng bài cũ và người tạo bỏ ẩn được để đưa câu trở lại ngân hàng chung; muốn phiên bản khác thì soạn câu mới. Câu Nháp không ẩn mà xóa.

**BR-21 — Giao bài và làm bài.** Teacher **đang phụ trách chuyên môn** ở (lớp, môn) giao bài cho lớp đó: đặt tên bài (bắt buộc) và lời dặn tùy chọn, chọn các câu Sẵn dùng **cùng môn và cùng khối với lớp** từ ngân hàng (chọn tay hoặc dựng theo **ma trận đề** bài × độ khó), thứ tự các câu, **thang điểm theo phần**, thời điểm mở (mặc định ngay), hạn nộp (> hiện tại và sau thời điểm mở) và **thời lượng làm bài** (1 đến 600 phút, tùy chọn — để trống là không giới hạn). Bài **giao ngay**, không có bước nháp: hệ thống kiểm bài có từ 1 đến 50 câu và lớp có học sinh đang học, lưu danh sách được giao, tạo thông báo cho học sinh của lớp; từ đó bài **không sửa, không xóa, không gia hạn được nữa**.

**Danh sách được giao:** lúc giao, hệ thống lưu lại danh sách học sinh đang thuộc lớp tại thời điểm đó. Danh sách này không đổi về sau: học sinh chuyển lớp hoặc vào lớp sau thời điểm giao không làm đổi người nhận bài, mẫu số tỉ lệ nộp, nhãn "Không nộp" và số liệu báo cáo tuần (BR-24). Học sinh vào lớp sau khi giao không được giao bài đó.

Học sinh trong danh sách được giao mở bài từ thời điểm mở tới hạn nộp; bài tự lưu nháp. **Bài có thời lượng:** học sinh bấm **Bắt đầu**, đồng hồ tính từ lúc đó theo giờ Backend và vẫn chạy khi học sinh thoát ra rồi vào lại. **Hạn thực tế** của học sinh là mốc sớm hơn giữa hạn nộp và (giờ bắt đầu + thời lượng); bài không có thời lượng thì hạn thực tế là hạn nộp. **Tối đa một lần nộp**; Backend từ chối lưu nháp hoặc nộp sau hạn thực tế, và từ chối nộp lại; bài gửi lên trong **10 giây** sau hạn thực tế vẫn được nhận để bù độ trễ mạng. **Hết hạn thực tế mà học sinh chưa nộp:** nếu bản nháp đã có ít nhất một câu trả lời thì Backend **tự nộp và chấm bản nháp đó**, lúc bài được mở sau hạn thực tế hoặc ở lượt quét định kỳ kế tiếp (mặc định 2 phút một lần), tùy việc nào tới trước; bài nộp ghi giờ là hạn thực tế; bản nháp rỗng hoặc chưa từng mở bài thì không phải 0 điểm — hiển thị "Không nộp".

Chấm ngay khi nộp: một câu chỉ được điểm khi học sinh chọn **đúng và đủ** các đáp án đúng, không chọn thừa; điểm = tổng điểm các câu đúng / tổng điểm tối đa của bài × 10, làm tròn 2 chữ số thập phân. **Thang điểm theo phần:** mỗi độ khó trong bài là một phần; giáo viên đặt tổng điểm cho từng phần, các phần cộng lại đúng 10 điểm (phần có câu thì có điểm lớn hơn 0 với tối đa 2 chữ số thập phân), hệ thống chia đều cho các câu trong phần theo đơn vị 0,01 và lưu điểm từng câu lúc giao nên không đổi về sau. Bài giao trước khi có thang điểm theo phần được chia đều 10 điểm cho các câu. Giáo viên cũng tạo được **bản in đề** (file PDF hoặc Word có đề và, tùy chọn, đáp án ở trang riêng) để tự in hoặc chỉnh sửa, từ đề đang soạn chưa giao (không cần lớp hay hạn nộp) hoặc từ bài đã giao. Đề dựng một lần ở màn Soạn đề rồi mới chọn giao cho lớp (một hay nhiều lớp cùng môn, mỗi lớp là một bài giao riêng) hoặc tải file.

**BR-22 — Kết quả bài làm.** Không có bước công bố: ngay khi nộp, **Pupil thấy điểm, lựa chọn của mình, đáp án đúng và giải thích của từng câu**, kèm khung phân tích AI (BR-23). Pupil chỉ xem kết quả bài **mình đã nộp**. **Parent** xem được cùng nội dung của từng con được liên kết; **Teacher** đang phụ trách chuyên môn xem điểm và kết quả từng học sinh nộp bài của (lớp, môn) mình. Tổ trưởng không xem bài làm hay điểm từng học sinh; việc giao bài của tổ chỉ có số tổng hợp ở Tình hình tổ chuyên môn (ADM-01). Điểm chỉ là điểm bài tập rèn luyện, không vào sổ điểm chính thức.

**BR-23 — Phân tích bằng AI.** Khi học sinh nộp bài (kể cả bị tự nộp), Backend tạo **một phân tích cho bài nộp đó** và gửi MSMS AI **một lần** (mục 3.1): nội dung câu hỏi, các lựa chọn, đáp án đúng, lựa chọn của học sinh ở từng câu và điểm, không kèm tên hay mã học sinh. Kết quả gồm **điểm mạnh, điểm yếu, giải thích lỗi sai và gợi ý kiến thức cần ôn**. Phân tích **tự động, không có bước duyệt hay công bố**: lưu kèm bài nộp rồi hiện cho Pupil, Parent và Teacher đang phụ trách chuyên môn. AI chạy nền sau khi bài đã chấm nên học sinh thấy điểm ngay; trong lúc AI chạy, khung phân tích ở trạng thái Đang phân tích, và nếu kẹt quá 5 phút kể từ lúc gửi phân tích (tiến trình dừng giữa chừng) thì coi là Lỗi; mốc là lúc gửi chứ không phải giờ nộp, vì bài tự nộp ghi giờ nộp lùi về hạn. AI lỗi hoặc trả kết quả rỗng → **Lỗi**, khung phân tích để trống, **không thử lại**; không ảnh hưởng chấm và nộp bài.


### 7.7. Dịch vụ phụ huynh, truyền thông, giám sát

**BR-24 — Báo cáo tuần.** **Tính trực tiếp khi mở, không lưu, không có job sinh báo cáo, không ai viết tay.** Một báo cáo = một học sinh + một tuần (thứ Hai – Chủ nhật), xem được cho mọi tuần của năm học hiện hành mà học sinh học ở một lớp. Gồm:

- **Chuyên cần:** số tiết có mặt, muộn, vắng có phép, vắng không phép, chưa điểm danh — chỉ tiết Đã xếp lịch đã diễn ra.
- **Bài tập có hạn nộp trong tuần:** số được giao, đã nộp, chưa nộp (còn hạn), không nộp (đã quá hạn mà chưa nộp).
- **Điểm** của các bài đó. Phân tích AI của từng bài xem ở màn Kết quả bài tập (BR-23), không nằm trong báo cáo tuần.

Pupil xem báo cáo của chính mình, Parent xem báo cáo của từng con được liên kết, chủ nhiệm xem báo cáo của học sinh lớp mình đang chủ nhiệm; School Admin không xem báo cáo. Không xếp hạng, không điểm trung bình lớp, không hạnh kiểm. Mục không có dữ liệu để trống. Số liệu luôn theo dữ liệu hiện tại — nhập bù điểm danh hay đơn duyệt muộn đều được phản ánh ngay lần mở sau.

**BR-25 — Hộp thư.** Cả Parent và Teacher đều có thể **tạo** hội thoại về một học sinh: Phụ huynh tạo với **chủ nhiệm** lớp con đang học hoặc **giáo viên đang phụ trách chuyên môn** ở lớp đó; Giáo viên (chủ nhiệm hoặc đang phụ trách chuyên môn) tạo với phụ huynh của học sinh thuộc lớp mình đang dạy. Mỗi bộ (phụ huynh, cán bộ, học sinh) có đúng một hội thoại. Hai bên nhắn văn bản tối đa 2.000 ký tự; không file, không gọi, không nhóm. Quan hệ được kiểm **mỗi lần gửi**: quan hệ đã kết thúc thì hội thoại thành **chỉ đọc**, lịch sử giữ nguyên. School Admin **không đọc và không nhắn** trong hội thoại. Tin nhắn mới hiện ngay ở bên nhận đang mở ứng dụng (web và mobile, qua SignalR), kèm chỉ báo **đang nhập**, **đã xem** (khi người nhận mở hội thoại) và **online/offline** của bên kia trong cùng hội thoại; School Admin không tham gia hội thoại nên không có trạng thái này. Realtime chỉ là kênh đẩy: việc gửi và kiểm quan hệ vẫn qua Backend như trên, mất kết nối thì tải lại phần đã lỡ.

**BR-26 — Thông báo.** Chỉ trong ứng dụng. Email chỉ có hai ngoại lệ, đều nằm ngoài danh sách thông báo: OTP quên mật khẩu (BR-04) và email báo giáo viên khi lịch dạy của họ đổi (mục 3.2) — email này đi kèm chứ không thay thông báo trong ứng dụng. Thông báo mới hiện ngay trên web và mobile đang mở (SignalR); không có push khi app đóng. Có thông báo **không đồng nghĩa có quyền** — Backend kiểm lại khi người dùng bấm vào. Danh sách sự kiện là **danh sách đóng**:

| Sự kiện | Người nhận |
| --- | --- |
| Phát hành thời khóa biểu | Giáo viên có tiết trong phiên bản và học sinh của các lớp trong phiên bản — một thông báo chung "đã có thời khóa biểu mới" mỗi người |
| Tiết bị đổi giáo viên, hủy, thêm bù, có giáo viên dạy thay, trả lại giáo viên khi xóa khai báo vắng | Giáo viên liên quan; học sinh của lớp |
| Tiết chuyển Chờ xử lý do khóa hoặc đổi loại tài khoản giáo viên | Vice Principal của phân hiệu chứa tiết |
| Tổ trưởng gửi duyệt một khai báo vắng | Vice Principal của từng phân hiệu có tiết chờ duyệt |
| Vice Principal trả một khai báo vắng về cho tổ trưởng (kèm lý do) | Tổ trưởng đương nhiệm của tổ giáo viên vắng (không còn ai thì người đã khai) |
| Bài mới được giao | Học sinh của lớp |
| Tin nhắn mới | Người nhận trong hội thoại — hiện ở hộp thư (số tin chưa đọc), không liệt kê ở chuông thông báo |

**BR-27 — Bảng tin.** **Principal, Vice Principal và Teacher đăng được**, kể cả khi đang giữ trách nhiệm chủ nhiệm hay tổ trưởng; **School Admin chỉ đọc**, không đăng và không ẩn bài. Khi đăng, chọn một trong hai phạm vi:

| Phạm vi | Ai đọc |
| --- | --- |
| **Nội bộ** | Chỉ cán bộ |
| **Toàn trường** | Mọi người, gồm học sinh và phụ huynh |

Bài kèm tối đa **5 ảnh** (JPG hoặc PNG, mỗi ảnh ≤ 2 MB); ảnh đầu là ảnh bìa hiện trên danh sách. Trạng thái **Đã đăng ↔ Đã ẩn**, không có nháp. Người đăng sửa và ẩn bài của mình. **Chỉ Principal ẩn được bài của người khác** (kể cả bài cũ của School Admin), Vice Principal và Teacher chỉ ẩn bài của mình; bắt buộc nhập lý do; bài giữ nguyên tác giả và nội dung, ghi lại người ẩn, thời điểm và lý do để truy vết. Người ẩn (hoặc tác giả, với bài tự ẩn) hiện lại được bài. Ẩn bài giữ ảnh; xóa bài xóa luôn file. Bảng tin **không tạo thông báo**.

Ảnh đại diện người dùng (IAM-04) dùng chung cách lưu này. **Logo trường** (SCH-07) cũng dùng chung cách lưu: một ảnh duy nhất, JPG hoặc PNG, ≤ 2 MB, kiểm theo nội dung file; đổi logo thì file cũ bị xóa.

**BR-28 — Audit Log.** Ghi đúng **5 nhóm**:

1. **Tài khoản:** tạo (đơn lẻ, hàng loạt), khóa, mở, đổi loại tài khoản, School Admin reset mật khẩu. Không ghi mật khẩu.
2. **Phát hành thời khóa biểu:** phiên bản, số tiết xóa và tạo.
3. **Điều chỉnh lịch:** đổi giáo viên, hủy, tiết bù, tổ trưởng khai báo, xóa khai báo, đề xuất xử lý tiết và gửi duyệt, Vice Principal duyệt hoặc trả lại khai báo và xử lý riêng giáo viên vắng; hủy tiết do khai ngày nghỉ (BR-08, một bản ghi kèm danh sách ID tiết).
4. **Trách nhiệm:** gán, thay người, kết thúc và hủy lịch hẹn, kèm loại trách nhiệm, đối tượng, người đảm nhiệm và ngày hiệu lực. Bản hẹn bị hủy bị xóa hẳn khỏi bảng trách nhiệm nên nhật ký là dấu vết duy nhất của nó.
5. **Điểm danh:** chỉ khi School Admin nhập bù hoặc sửa (BR-17), kèm số bản ghi nhập bù, số bản ghi sửa và từng thay đổi trước → sau, vì bản ghi bị ghi đè khi sửa. Điểm danh của giáo viên trong ngày của tiết không ghi.

Nhóm, hành động, tên trường nội dung và giá trị của mọi bản ghi đều ghi **bằng tiếng Việt**. Thao tác hàng loạt ghi **một bản ghi kèm danh sách ID**. Audit **chỉ ghi thêm** — không có API sửa hay xóa, quyền CSDL chặn UPDATE và DELETE. Việc khác (đăng nhập, nộp bài, điểm danh của giáo viên, kết chuyển) **không vào Audit** — người thực hiện và thời điểm đã lưu ngay trên bản ghi nghiệp vụ.

**BR-29 — MSMS AI của quản lý.** Principal và Vice Principal hỏi MSMS AI bằng câu tự do để **báo cáo, so sánh, xếp hạng** số liệu **chuyên cần, bài tập rèn luyện, giáo viên vắng và tải dạy**; câu hỏi khác bị từ chối nhẹ nhàng. **Chỉ đọc**, cùng phạm vi màn Tình hình (Principal toàn trường, Vice Principal các phân hiệu đang phụ trách); phạm vi do Backend chặn ở từng truy vấn, không phụ thuộc lời nhắc gửi AI, hỏi ngoài phạm vi thì truy vấn bị từ chối. AI không truy vấn dữ liệu: nó chọn trong một bộ truy vấn chỉ đọc Backend khai báo sẵn, Backend tính mọi con số theo cách tính chung ở mục 5.1 (kỳ mặc định 4 tuần, so với kỳ liền trước cùng độ dài), AI chỉ diễn giải. Biểu đồ trong câu trả lời do Backend dựng từ đúng số của truy vấn. Xếp hạng điểm trung bình hay tỷ lệ nộp chỉ tính học sinh có từ 3 bài đã hết hạn; xếp hạng chuyên cần chỉ tính học sinh được điểm danh từ một nửa số tiết thường gặp (trung vị) của phạm vi; cùng số là đồng hạng và AI nói rõ còn bao nhiêu người cùng số với dòng cuối; tải dạy của một giáo viên chỉ so với trung bình tổ khi tổ có từ 3 giáo viên trong phạm vi đang hỏi; mọi danh sách người tối đa 10 dòng và chỉ gồm học sinh đang học ở lớp trong phạm vi (hiện theo lớp hiện tại). Số liệu của một học sinh tính theo chính em ở mọi lớp trong phạm vi, nên em chuyển lớp giữa kỳ vẫn đủ phần ở lớp cũ. Giáo viên có thể dạy môn của tổ khác, nên khi thống kê giáo viên vắng theo tổ, tiết của giáo viên vắng tính cho tổ của người vắng, tiết hủy không gắn ai vắng tính cho tổ của môn. Hỏi kỳ chưa tới thì AI trả lời chưa có số liệu vì kỳ chưa tới. Tiến độ giao bài của giáo viên tính theo **người giao bài**: số bài giao trong kỳ (kể cả bài chưa tới hạn), số bài đang mở, còn tỷ lệ nộp và điểm trung bình chỉ tính bài đã hết hạn. Hệ thống không có dữ liệu nguyên nhân nên AI không đoán nguyên nhân.

- **Danh sách trắng:** AI chỉ nhận số tổng hợp, tên phân hiệu, khối, lớp, tổ, môn, giới tính của học sinh và người ở dạng **mã giả** (HS#, GV#). Không bao giờ nhận họ tên, mã học sinh hay cán bộ, ngày sinh, CCCD, liên lạc, địa chỉ hay phụ huynh. Tên hoặc mã thật người dùng gõ (hoặc chọn bằng **@**) được đổi thành mã giả trước khi gửi; mã giả trong câu trả lời được đổi lại thành tên trước khi hiển thị, chỉ với người trong phạm vi.
- **Không lưu hội thoại, không vào Audit:** hội thoại chỉ nằm trong tab trình duyệt; mỗi câu gửi kèm tối đa 20 lượt hỏi đáp gần nhất để hiểu câu hỏi nối tiếp; đóng tab là mất.
- **Giới hạn:** mỗi tài khoản tối đa 6 câu hỏi một phút; mỗi câu tối đa 4 vòng gọi truy vấn. AI hỏng hoặc hết hạn mức thì báo lỗi, không đoán số.

### 7.8. Những ràng buộc cố tình không đặt

Mỗi điều dưới đây nếu thêm vào sẽ làm lược đồ khó thiết kế mà chặn được rất ít tình huống thật:

- **Không chặn học sinh lệch tuổi so với khối** khi xếp lớp: học sinh đi học muộn hoặc học lại sau thời gian nghỉ là chuyện có thật; khoảng tuổi hợp lệ chỉ kiểm ở hồ sơ (BR-10). Màn Xếp lớp chỉ **cảnh báo** em lệch từ 3 tuổi trở lên so với tuổi thường gặp của khối (khối N → N + 5 tuổi, tính theo năm sinh) và cho xếp sau khi School Admin xác nhận.
- Giáo viên **không bắt buộc** dạy môn thuộc tổ chuyên môn của mình.
- Giáo viên **không bắt buộc** thuộc phân hiệu của lớp — đây là hệ thống đa phân hiệu, dạy liên phân hiệu là chuyện thường.
- **Dạy thay không thành phụ trách chuyên môn:** phụ trách chuyên môn chỉ tính từ thời khóa biểu đã phát hành, không tính từ tiết điều chỉnh (BR-14).
- **Không giới hạn sĩ số lớp** (màn Xếp lớp chỉ cảnh báo khi xếp hoặc chuyển thêm làm lớp có hơn 45 học sinh, vẫn cho làm), không quy định số tiết mỗi môn mỗi tuần.
- **Không kiểm lại lớp khi nộp bài** — chuyển lớp giữa chừng không làm mất bài đã nộp.
- **Không sửa, không xóa, không gia hạn bài sau khi giao** — giao nhầm thì chấp nhận, muốn bài khác thì giao bài mới.
- **Khớp loại tài khoản với loại hồ sơ kiểm ở tầng dịch vụ**, không ở cơ sở dữ liệu.

---

## 8. Mobile và việc còn để mở

### 8.1. Phạm vi mobile

**Web là nền tảng đầy đủ: mọi chức năng có giao diện đều có trên web, cho mọi loại tài khoản. Mobile chỉ là một phần trong đó.**

> **Mobile chỉ có việc làm được bằng một tay, giữa hai tiết, tại lớp. Mọi màn hình dạng bảng, lưới, import, cấu hình chỉ có trên web.**

| Actor | Mobile |
| --- | --- |
| **Parent** | Toàn bộ chức năng của mình |
| **Pupil** | Toàn bộ chức năng của mình trừ làm bài tập rèn luyện: trên mobile xem bài được giao và hạn nộp, kết quả và phân tích AI; làm bài trên web |
| **Teacher** | Trang chủ · lịch dạy của mình · điểm danh và sửa trong ngày · hộp thư · thông báo · bảng tin (đọc) · hồ sơ cá nhân · nếu chủ nhiệm: chuyên cần lớp, danh sách và hồ sơ học sinh lớp mình, hộp đơn nghỉ, duyệt và từ chối, báo cáo tuần lớp mình |
| **School Admin, Principal, Vice Principal** | Không có mobile, dùng web |

Chỉ có trên web: soạn câu hỏi, giao bài, dựng đề và bản in đề, làm bài tập rèn luyện, xem kết quả bài tập theo lớp, soạn bài đăng; toàn bộ SCH và PPL (trừ danh sách và hồ sơ học sinh lớp chủ nhiệm), SCD (trừ màn hình xem lịch cá nhân) và ADM.

### 8.2. Ký hiệu Platform trong WBS

| Giá trị | Nghĩa |
| --- | --- |
| `BE` | Chỉ Backend: lược đồ, service dùng chung, quy tắc không có giao diện riêng |
| `BE + Web` | Có giao diện web, không có mobile |
| `BE + Web + Mobile` | Có cả hai |
| `QA` | Kiểm thử phân quyền và ràng buộc trên môi trường kiểm thử (mục 2.5) |

Mọi Task có giao diện đều mang `Web`; `Mobile` chỉ thêm vào khi Task thuộc phạm vi mục 8.1. Task chỉ làm phần máy khách của một Task Backend đã có (kết nối realtime, giao diện MSMS AI) ghi đúng nền tảng của nó: `Web`, `Mobile` hoặc `Web + Mobile`; phần mobile luôn có phần web tương ứng trong WBS.

### 8.3. Việc kỹ thuật còn để mở

Thuộc Feature Spec / OpenAPI / hạ tầng, không suy diễn từ tài liệu này: lược đồ CSDL chi tiết; danh sách API, mã lỗi, wireframe, test case chi tiết; nơi lưu ảnh và quy ước đường dẫn; cấu hình Gmail SMTP và hạn mức gửi; nhà cung cấp AI và hạn mức gọi; yêu cầu phi chức năng (quy mô, hiệu năng giờ cao điểm nộp bài, sao lưu); rà soát pháp lý cho phân tích bằng AI.

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
