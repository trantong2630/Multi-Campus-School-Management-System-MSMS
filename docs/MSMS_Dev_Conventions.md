# MSMS — Quy ước phát triển

20/10/2026 · V1.1 · Trần Đức Kiên

Tài liệu kỹ thuật đi kèm Business Specification V3. Chốt bốn thứ: tech stack, cách chia folder, quy ước hiển thị tiếng Việt, và từ điển hiển thị trên UI.

---

## 1. Tech stack

### Backend

| Thành phần | Công nghệ |
| --- | --- |
| Runtime | .NET 10 (ASP.NET Core) |
| CSDL | SQL Server |
| ORM | EF Core |
| Đăng nhập | JWT Bearer (access token 15 phút) + refresh token 7 ngày xoay vòng; phiên theo thiết bị lưu ở bảng `AccountSession` (chỉ lưu bản băm refresh token) để thu hồi được. Web giữ refresh token trong cookie HttpOnly `msms_rt` (Path `/api/auth`, SameSite=Lax, Secure khi HTTPS, không đặt hạn nên đóng trình duyệt là hết), access token chỉ giữ trong bộ nhớ trang; mobile nhận refresh token trong body và lưu bằng `flutter_secure_storage`. Web và API phải cùng domain khi deploy |
| Phân quyền | Authorization Policy |
| Excel | ClosedXML |
| Realtime | ASP.NET Core SignalR, một hub cho thông báo và chat. Server → client: `NotificationCreated`, `MessageReceived`, `MessageRead`, `Typing`, `PresenceChanged`; client → server chỉ `Typing` và `JoinConversation`, gửi tin vẫn qua REST. Web dùng `@microsoft/signalr`, mobile dùng `signalr_netcore` (chỉ khi app mở, có polling dự phòng). Token truyền qua query `access_token`, hub đi qua cùng kiểm tài khoản và phiên như REST. Đẩy sau `SaveChanges`. Đang nhập và online chỉ ở bộ nhớ, không thêm bảng; không backplane khi chạy một instance |
| Email OTP | Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) bằng App Password của hộp thư chung của trường; tài khoản và mật khẩu ứng dụng đặt trong cấu hình môi trường, không commit (spec mục 3.2) |
| Gọi AI | HttpClient + REST của nhà cung cấp |
| Log | Serilog, ghi ra file |
| Test | xUnit |

Chọn .NET 10 vì .NET 8 và .NET 9 đều hết hỗ trợ ngày 10/11/2026.

### Web

React + TypeScript + Vite · Ant Design · TanStack Query + axios · React Router

Ant Design vì MSMS toàn bảng, lưới và form phức tạp — có sẵn Table, DatePicker, Upload và locale tiếng Việt.

### Mobile

Flutter · dio · go_router · Riverpod · flutter_secure_storage

Chỉ các màn hình thuộc phạm vi mobile (spec mục 8.1).

### Môi trường

Giai đoạn này **chạy local**: `dotnet run` hoặc IIS Express, SQL Server cài trên máy, web `npm run dev`, mobile chạy emulator Android.

Triển khai thật (VPS Windows + IIS, Docker, CI/CD) **có thể làm sau**, chưa đụng tới ở giai đoạn coding.

### Không dùng

| Bỏ | Lý do |
| --- | --- |
| Hangfire / Quartz | Spec không có job chạy nền nào; trạng thái suy lúc đọc |
| ASP.NET Core Identity | Đã có bảng Account riêng, 6 loại tài khoản cố định |
| Redis | Chưa có nhu cầu cache thật |
| Thư viện i18n | Chỉ một ngôn ngữ |

---

## 2. Chia folder

### Nguyên tắc

1. Chia theo **module nghiệp vụ**, không chia theo loại file. Trong module mới chia theo kỹ thuật.
2. Thứ dùng chung nằm ở `Common`, và chỉ có **một bản duy nhất**.
3. Chiều phụ thuộc một chiều: `Modules → Common → Data`.
4. Một quy tắc nghiệp vụ = một chỗ. Xuất hiện ở hai file trở lên là sai cấu trúc.

### Repo

```
msms/
  backend/
  web/
  mobile/
  docs/
```

### Backend

```
MSMS.sln
  MSMS.Api/
  MSMS.Tests/
```

```
MSMS.Api/
  Program.cs

  Common/
    Auth/            CurrentUser, ScopeResolver, Policies
    Time/            IClock — "hôm nay" theo giờ VN
    Rules/           SessionRules, AttendanceRules, AssignmentRules,
                     EnrollmentRules, EffectivePeriod, ScheduleConflict
    Audit/           AuditWriter
    Excel/           ExcelReader, ExcelWriter, CellError
    Notifications/   NotificationSender
    Text/            chuẩn hóa chuỗi, bỏ dấu
    Errors/          ApiException, ErrorCodes, ExceptionMiddleware

  Data/
    MsmsDbContext.cs
    Entities/
    Configurations/

  Modules/
    Iam/  Sch/  Ppl/  Scd/  Att/  Lrn/  Par/  Com/  Adm/
      Controllers/
      Services/
      Dtos/

  Migrations/        EF Migrations (code-first); DB dựng bằng `dotnet ef database update`, không có file .sql
```

Entities để chung ở `Data/`, không rải vào module — Class, Person, TeachingSession bị gần như mọi module dùng.

### Web

```
src/
  app/        router, apiClient, AuthProvider
  shared/     components, lib (labels, format, text)
  features/
    iam/  sch/  ppl/  scd/  att/  lrn/  par/  com/  adm/
      api.ts
      pages/
      components/
      hooks/
```

Tên feature trùng tên module backend. Gọi API chỉ qua `features/<module>/api.ts`.

### Mobile

```
lib/
  core/       http, auth_store, theme, format
  features/
    iam/  scd/  att/  lrn/  par/  com/
  main.dart
```

Không có `sch/`, `ppl/`, `adm/` — mobile không có màn hình nào thuộc ba module đó.

### Test

```
MSMS.Tests/
  Rules/           test thuần hàm, không cần DB
  Modules/
  Authorization/   mỗi loại tài khoản và mỗi trách nhiệm; KHÔNG dùng School Admin (spec mục 2.5)
```

### Luật khi code

1. Controller chỉ nhận request, kiểm quyền, gọi service, trả DTO. Không `if` nghiệp vụ.
2. Mọi trạng thái suy ra gọi `Common/Rules`. Viết lại trong module là reject PR.
3. Service tiêm `IClock`. Không gọi `DateTime.Now` ở bất kỳ đâu.
4. Module cần dữ liệu module khác thì gọi service của nó, không query bảng của nó.
5. Thao tác nhiều bước (Phát hành, import, thay người) bọc trong một transaction.

### Đặt tên để map vào WBS

Task `SCD-02-BE-03` → `Modules/Scd/Services/ScheduleAdjustService.cs`
Branch `feat/SCD-02-doi-giao-vien` · Commit mở đầu bằng Feature ID.

---

## 3. Hiển thị tiếng Việt

### Năm quy ước

**1. Code tiếng Anh, hiển thị tiếng Việt.**
Bảng, cột, class, enum, biến giữ tiếng Anh. Chỉ nhãn người dùng nhìn thấy mới tiếng Việt.

**2. Một file nhãn chung.**
Thư mục `shared/lib/labels/` (mỗi module một file, `index.ts` chỉ re-export) và bản Flutter tương ứng, dịch enum một chỗ: `Present → "Có mặt"`, `Pending → "Chờ duyệt"`, `Scheduled → "Đã xếp lịch"`. Không để mỗi màn hình tự dịch.

**3. Backend trả message tiếng Việt kèm mã lỗi.**
```json
{ "code": "SESSION_LOCKED", "message": "Tiết đã qua, không sửa được." }
```
FE chỉ hiện `message`, dùng `code` khi cần xử lý riêng.

**4. Chuẩn hóa chuỗi trước khi lưu và trước khi so sánh.**
Trim, gộp khoảng trắng thừa, chuẩn hóa NFC. Áp cho mọi tên người, tên lớp, viết tắt. Thiếu bước này thì import đối chiếu tên người, tên lớp, viết tắt sẽ trượt, vì hai máy gõ dấu ra hai chuỗi khác nhau dù nhìn y hệt. **Đây là chỗ duy nhất sẽ sinh bug khó tìm.**

**5. Một hàm bỏ dấu chung.**
Dùng cho sinh tên đăng nhập (BR-03). Tìm kiếm trên danh sách **phân biệt dấu** (collation `Vietnamese_CI_AS`): gõ có dấu mới khớp, không phân biệt hoa thường. Nhớ xử lý `đ → d`, `Đ → D` — bỏ dấu kiểu Unicode thường sót chữ này.

### Cài đặt

| Chỗ | Làm gì |
| --- | --- |
| Web | `ConfigProvider locale={viVN}` + `dayjs.locale('vi')` |
| Mobile | `intl` + `Locale('vi')` |
| Ngày giờ | `dd/MM/yyyy`, giờ 24h `HH:mm`, tuần `Thứ 2 … Thứ 7, CN` |
| Điểm | một chữ số thập phân |
| Sắp xếp tên | làm ở SQL — collation `Vietnamese_CI_AS` đã đúng; FE không sort lại |
| Font | Inter hoặc Be Vietnam Pro — một số font phổ biến hiển thị dấu bị lệch. Ngoại lệ duy nhất: logo chữ `msms.` dùng Montserrat vì không có dấu tiếng Việt (Design_Web §3.1) |
| File Excel tải về | tên file bỏ dấu, tránh lỗi mã hóa khi download |

---

## 4. Từ điển hiển thị

Chốt chữ nào hiện trên màn hình. Mọi màn hình, mọi file xuất Excel dùng đúng các từ dưới đây.

### 4.1. Tên menu

| Module | Menu hiển thị |
| --- | --- |
| SCH | Thiết lập |
| PPL | Hồ sơ |
| SCD | Thời khóa biểu · Lịch dạy |
| ATT | Điểm danh |
| LRN | Học tập |
| PAR | Phụ huynh |
| COM | Thông báo · Bảng tin |
| ADM | Dashboard · Quản trị (Tài khoản · Nhật ký · Xuất dữ liệu) |
| IAM | Tài khoản (góc phải) |

### 4.2. Tên thực thể

| Trong code | Hiển thị |
| --- | --- |
| SchoolProfile | Thông tin trường |
| Campus | Phân hiệu |
| SubjectDepartment | Tổ chuyên môn |
| Subject | Môn học |
| AcademicYear | Năm học |
| Term | Học kỳ |
| PeriodSlot | Khung giờ tiết |
| Class | Lớp |
| Staff | Cán bộ |
| Enrollment | Ghi danh |
| AccountSession | Phiên đăng nhập (theo thiết bị) |
| **Responsibility** | **Trách nhiệm** |
| TimetableVersion | Phiên bản thời khóa biểu |
| TeachingSession | Tiết học |
| Attendance | Điểm danh |
| LeaveRequest | Đơn xin nghỉ |
| Question | Câu hỏi |
| Exam | Đề |
| Assignment | Bài tập |
| Attempt | Lượt làm bài |
| AiComment | Phân tích AI |
| WeeklyReport | Sổ liên lạc tuần |
| Conversation | Hộp thư |
| Notification | Thông báo |
| Post | Bài đăng |
| AuditLog | Nhật ký |

Dòng in đậm là quy ước riêng của MSMS, không dịch sát nghĩa:

- **Trách nhiệm** = gán cho tài khoản Teacher hoặc Vice Principal: chủ nhiệm lớp, tổ trưởng, phụ trách phân hiệu (spec mục 2.2).

**Phụ trách chuyên môn** (giáo viên dạy một môn ở một lớp) không có thực thể riêng: tính từ `TimetableEntry` của phiên bản Đã phát hành mới nhất của lớp (spec BR-14). UI hiển thị **"Phụ trách chuyên môn"**.

### 4.3. Cặp từ dễ gọi lẫn — chốt cứng

| Chốt | Nghĩa |
| --- | --- |
| **Thời khóa biểu** | Bản mẫu tuần, có phiên bản, phát hành |
| **Lịch dạy** | Tiết thực tế theo ngày, có điều chỉnh tay |
| **Cán bộ** | Hồ sơ nhân sự, gồm cả Hiệu trưởng và Hiệu phó |
| **Giáo viên** | Chỉ loại tài khoản Teacher |
| **Tiết học** | Một buổi dạy cụ thể |
| **Khung giờ tiết** | Tiết 1, Tiết 2… trong ngày |

Hai cặp đầu nếu để lẫn thì cả màn hình SCD lẫn file xuất Excel sẽ hiểu sai.

### 4.4. Nhãn trạng thái

Lấy nguyên từ spec mục 6.1 và 6.2, không chế thêm giá trị mới.

| Đối tượng | Nhãn |
| --- | --- |
| Tài khoản | Chờ đổi mật khẩu · Đang hoạt động · Đã khóa |
| Tiết học | Đã xếp lịch · Chờ xử lý · Đã hủy |
| Nguồn tiết | Sinh tự động · Chỉnh tay |
| Điểm danh | Có mặt · Muộn · Vắng có phép · Vắng không phép · Chưa điểm danh |
| Đơn xin nghỉ | Chờ duyệt · Đã duyệt · Đã từ chối · Đã rút |
| Câu hỏi | Nháp · Sẵn dùng |
| Bài tập | Nháp · Đã giao · Đã đóng |
| Bài làm của học sinh | Không nộp (bài đã đóng mà chưa nộp; không phải 0 điểm) |
| Danh mục và hồ sơ (phân hiệu, tổ, môn, lớp, khung giờ, cán bộ) | Đang hoạt động · Ngừng hoạt động |
| Phân tích AI | Nháp · Đã duyệt · Đã công bố · Lỗi |
| Phiên bản TKB | Nháp · Đã phát hành |
| Bài đăng | Đã đăng · Đã ẩn |
| Học sinh | Đang học · Đã chuyển trường · Đã tốt nghiệp |

Màu badge: xanh lá = đang hiệu lực · vàng = chờ · xám = nháp hoặc đã ẩn · đỏ = hủy, khóa, lỗi.

### 4.5. Nhãn nút

**Chung:** Lưu · Đóng · Thêm · Sửa · Xóa · Tìm kiếm · Tải file mẫu · Tải lên · Xuất Excel

**Nghiệp vụ:** Phát hành · Xem trước · Đổi giáo viên · Hủy tiết · Thêm tiết bù · Gửi đơn · Rút đơn · Duyệt · Từ chối · Giao bài · Công bố điểm · Công bố phân tích · Nhân bản · Ẩn bài · Khóa tài khoản · Cấp lại mật khẩu

**Quy tắc:** nút đóng hộp thoại luôn là **"Đóng"**, không bao giờ "Hủy" — vì "Hủy" trong MSMS nghĩa là hủy tiết. Nhầm chỗ này là bấm nhầm thật.

### 4.6. Giọng văn

- Không xưng "bạn". Viết câu không chủ ngữ: *"Tiết đã qua, không sửa được."*
- Lỗi phải nêu nguyên nhân kèm cách xử lý, không chỉ báo sai: *"Trùng lịch với tiết Toán 7A1 (Thứ 3, Tiết 2). Chọn khung giờ khác."*
- Hỏi xác nhận trước hành động không lùi được: hủy tiết, phát hành thời khóa biểu, khóa tài khoản.
- Nhãn cột bảng viết ngắn: Lớp · Môn · Giáo viên · Ngày · Tiết · Trạng thái.

### 4.7. Menu cấp 2

Sidebar web có menu con, **tối đa 2 cấp**. Tên mục con hiển thị tiếng Việt:

| Cấp 1 | Cấp 2 |
| --- | --- |
| Trang chủ | — |
| Dashboard | — |
| Thiết lập | Thông tin trường · Phân hiệu · Tổ chuyên môn & Môn học · Năm học & Học kỳ · Ngày nghỉ · Khung giờ tiết · Lớp & Ghi danh · Kết chuyển năm học |
| Hồ sơ | Cán bộ · Học sinh · Phụ huynh · Trách nhiệm |
| Thời khóa biểu | Thời khóa biểu · Phụ trách chuyên môn · Lịch dạy · Giáo viên vắng |
| Điểm danh | — |
| Học tập | Ngân hàng câu hỏi · Đề · Bài tập · Phân tích AI · Giám sát tổ |
| Phụ huynh | Đơn xin nghỉ · Sổ liên lạc · Hộp thư |
| Bảng tin | — |
| Quản trị | Tài khoản · Nhật ký · Xuất dữ liệu |

- Nhóm chỉ có một màn hình thì không có cấp 2. Áp dụng cả sau khi lọc theo quyền: nhóm chỉ còn một mục con hiển thị cho loại tài khoản đó thì hiện thẳng thành mục cấp 1 (ví dụ giáo viên chỉ thấy **Lịch dạy**; chủ nhiệm chỉ có quyền xuất điểm danh thì nhóm Quản trị hiện thẳng thành **Xuất dữ liệu**). School Admin không thấy Dashboard, Thời khóa biểu và Xuất dữ liệu; nhóm Quản trị của School Admin gồm **Tài khoản** và **Nhật ký**.
- **Trang chủ** là mục cấp 1 đầu tiên, mọi loại tài khoản đều thấy. Trang chủ chỉ chào và hiện thông tin chung (hôm nay, năm học, liên hệ trường — Business Spec §5.1); không đặt số liệu, lối tắt hay bảng tin ở Trang chủ (số liệu ở **Dashboard**, chỉ Principal và Vice Principal).
- Mục cha **không điều hướng**, chỉ xổ ra; vào trang con thì cha tự xổ và sáng lên.
- Icon chỉ ở cấp 1; mục con thụt vào, không icon, không chấm đầu dòng.
- **Ẩn hẳn mục không có quyền**, không hiện rồi khóa.
- Thông báo, hồ sơ cá nhân, đổi mật khẩu, đăng xuất nằm ở **góc phải header**, không vào sidebar.

**Ai thấy mục nào.** Một mục chỉ hiện cho loại tài khoản **có việc làm trên màn hình đó**. Quyền đọc dữ liệu (cột Đọc của Business Spec §5) không tự sinh ra màn hình: Hiệu phó và giáo viên đọc môn học, khung giờ tiết, lớp, ngày nghỉ ngay trong lưới thời khóa biểu, lịch dạy và trang chủ, nên không có màn hình Thiết lập. Gõ thẳng đường dẫn của mục không được thấy thì hiện trang 403. Nguồn duy nhất ở web là `app/menu.tsx`.

| Mục | Ai thấy |
| --- | --- |
| Trang chủ · Bảng tin | Mọi loại |
| Dashboard | HT, HP |
| Thiết lập › Thông tin trường | SA, HT |
| Thiết lập › mọi mục còn lại | SA |
| Hồ sơ › Cán bộ · Học sinh · Phụ huynh | SA, HT, HP |
| Hồ sơ › Trách nhiệm | SA, HT |
| Thời khóa biểu › Thời khóa biểu · Phụ trách chuyên môn · Giáo viên vắng | HP |
| Thời khóa biểu › Lịch dạy | HT, HP, GV |
| Điểm danh | SA, HT, HP, GV, HS, PH |
| Học tập › Ngân hàng câu hỏi · Đề · Phân tích AI · Giám sát tổ | GV |
| Học tập › Bài tập | GV, HS |
| Phụ huynh › Đơn xin nghỉ | HP, GV, PH |
| Phụ huynh › Sổ liên lạc | GV, HS, PH |
| Phụ huynh › Hộp thư | GV, PH |
| Quản trị › Tài khoản · Nhật ký | SA |
| Quản trị › Xuất dữ liệu | HP, GV |

Ký hiệu như Business Spec §5. Kết quả với các màn đã có: **SA** Trang chủ · Thiết lập · Hồ sơ (Cán bộ, Trách nhiệm) · Tài khoản — **HT** Trang chủ · Dashboard · Thông tin trường · Hồ sơ (Cán bộ, Trách nhiệm) · Lịch dạy — **HP** Trang chủ · Dashboard · Cán bộ · Thời khóa biểu (Thời khóa biểu, Lịch dạy) — **GV** Trang chủ · Lịch dạy — **HS, PH** Trang chủ.
