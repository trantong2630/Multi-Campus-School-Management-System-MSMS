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
| Realtime | ASP.NET Core SignalR, một hub cho thông báo và chat. Server → client: `NotificationCreated`, `MessageReceived`, `MessagesRead`, `UserTyping`, `PresenceChanged`; client → server chỉ `SendTyping` và `JoinConversation`, gửi tin vẫn qua REST. Web dùng `@microsoft/signalr`, mobile dùng `signalr_netcore` (chỉ khi app mở, có polling dự phòng). Token truyền qua query `access_token`, hub đi qua cùng kiểm tài khoản và phiên như REST. Đẩy sau `SaveChanges`. Đang nhập và online chỉ ở bộ nhớ, không thêm bảng; không backplane khi chạy một instance |
| Email OTP | Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) bằng App Password của hộp thư chung của trường; tài khoản và mật khẩu ứng dụng đặt trong cấu hình môi trường, không commit (spec mục 3.2) |
| Gọi AI | HttpClient + REST của nhà cung cấp (Gemini); khóa và model đặt ở mục `Ai` trong `appsettings.json` (`Ai:ApiKey`, `Ai:Model`). Yêu cầu có thể kèm ảnh (`IAiClient`, `AiImage`). MSMS AI của quản lý (BR-29) gọi `IAiClient.ChatAsync` (hội thoại nhiều lượt, khai báo tool) với model riêng `Ai:ChatModel` (bỏ trống thì dùng `Ai:Model`) để không tranh hạn mức với soạn câu hỏi và phân tích bài làm; `Ai:ChatHistoryTurns` (20), `Ai:ChatMaxToolRounds` (4), `Ai:ChatPerUserPerMinute` (6) |
| Sách giáo khoa | Ảnh từng trang nằm trên Cloudinary qua `IImageStorage`, thư mục `msms/textbooks`, tên `<mã sách>-p-<trang PDF>.jpg`; API lấy ảnh rồi trả lại qua endpoint có kiểm quyền, ảnh không đi thẳng từ Cloudinary tới người dùng. Mục lục (bài, trang in bắt đầu và kết thúc, độ lệch số trang của cuốn) nằm trong mã nguồn ở `backend/MSMS.Api/Data/Textbooks/<mã sách>.json` và `TextbookSeeder` nạp vào CSDL mỗi lần API khởi động, ở mọi môi trường. Nạp một cuốn mới bằng `python -I tools/textbook-import/import_textbook.py` (cần thư viện `pymupdf`; khóa Gemini và Cloudinary lấy từ biến môi trường hoặc user-secrets); chữ do Gemini đọc từ mục lục phải được người rà lại trước khi commit file JSON |
| Lưu ảnh | Cloudinary (gói Free) qua `CloudinaryDotNet`, cho ảnh đại diện, logo trường và ảnh bài đăng (BR-27). Code chỉ làm việc với `IImageStorage` (`Common/Storage`). CSDL chỉ giữ tên file; ảnh nằm ở thư mục `msms/avatars`, `msms/school`, `msms/posts` và vẫn đi qua API có kiểm quyền, API lấy từ Cloudinary rồi trả lại trình duyệt. Khóa `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret` đặt trong user-secrets hoặc biến môi trường, không commit; thiếu khóa thì API không khởi động |
| Log | Serilog, ghi ra file |
| Test | xUnit |

Chọn .NET 10 vì .NET 8 và .NET 9 đều hết hỗ trợ ngày 10/11/2026.

### Web

React + TypeScript + Vite · Ant Design · TanStack Query + axios · React Router

Ant Design vì MSMS toàn bảng, lưới và form phức tạp — có sẵn Table, DatePicker, Upload và locale tiếng Việt.

### Mobile

Flutter 3.50 (Material qua gói `material_ui`) · dio · go_router · Riverpod 3 · flutter_secure_storage · signalr_netcore · image_picker · lucide_icons_flutter

Chỉ các màn hình thuộc phạm vi mobile (spec mục 8.1). Địa chỉ API truyền lúc build: `--dart-define=API_URL=...`, mặc định `http://10.0.2.2:5063` (máy chạy emulator). Project nằm trong OneDrive nên `android/gradle.properties` tắt `kotlin.incremental` (OneDrive khóa cache tăng dần của Kotlin làm build lỗi).

### Môi trường

Giai đoạn này **chạy local**: `dotnet run` hoặc IIS Express, SQL Server cài trên máy, web `npm run dev`, mobile chạy emulator Android.

Ảnh lưu trên Cloudinary nên mỗi máy phải đặt khóa một lần: trong `backend/MSMS.Api` chạy `dotnet user-secrets set "Cloudinary:CloudName" "..."`, tương tự cho `Cloudinary:ApiKey` và `Cloudinary:ApiSecret`. Test dùng bản lưu vào thư mục tạm, không gọi Cloudinary.

Triển khai thật (VPS Windows + IIS, Docker, CI/CD) **có thể làm sau**, chưa đụng tới ở giai đoạn coding.

### Không dùng

| Bỏ | Lý do |
| --- | --- |
| Hangfire / Quartz | Job nền duy nhất (tự nộp bản nháp quá hạn, BR-21) chỉ là một vòng lặp định kỳ nên dùng `BackgroundService` có sẵn của ASP.NET Core; các trạng thái khác vẫn suy lúc đọc |
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
    Rules/           SessionLockRules, AttendanceRules, AssignmentRules,
                     EnrollmentRules, LeaveRequestRules, SchoolCalendarRules,
                     SemesterRules, RolloverRules, QuizRules, ExportRules,
                     EffectivePeriod, PublishedTimetable
    Audit/           AuditWriter
    Excel/           ExcelHelpers (đọc và ghi file Excel)
    Notifications/   NotificationSender
    Text/            TextNormalizer (chuẩn hóa chuỗi, bỏ dấu), UsernameGenerator
    Errors/          ApiException, ErrorCodes, ExceptionMiddleware, Guard,
                     PagedResult
    Storage/         IImageStorage, CloudinaryImageStorage
    Ai/              IAiClient, GeminiClient
    Email/           SmtpEmailSender

  Data/
    MsmsDbContext.cs
    Entities/        mỗi file một entity, tên số ít (Campus.cs, TeachingSession.cs)
    DevSeeder.cs     dữ liệu mẫu cho môi trường dev
    TextbookSeeder.cs, Textbooks/   mục lục sách giáo khoa (JSON)
    PersonCodes.cs   sinh mã người

  Modules/
    Iam/  Sch/  Ppl/  Scd/  Att/  Lrn/  Par/  Com/  Adm/
      Controllers/
      Services/
      Dtos/
      <Module>Module.cs   đăng ký DI của module (IamModule, SchModule…)

  Migrations/        EF Migrations (code-first); DB dựng bằng `dotnet ef database update`, không có file .sql
```

Entities để chung ở `Data/`, không rải vào module — Class, Person, TeachingSession bị gần như mọi module dùng. Cấu hình EF viết trong `MsmsDbContext`, không tách thư mục `Configurations/`.

Thứ chỉ một module dùng và gắn với một cơ chế riêng thì nằm cạnh `Controllers/Services/Dtos` của module đó, không đưa lên `Common`: `Com` giữ `CommunicationHub`, `PresenceTracker`, `NotificationPushInterceptor` (SignalR); `Adm/Ai/` giữ MSMS AI của quản lý: `AiScope` (phạm vi, kỳ, bộ lọc), `AiTools` (các truy vấn chỉ đọc khai báo cho AI), `PersonDirectory` (đổi tên, mã thật sang mã giả HS#, GV# và ngược lại), `AdmAiChatService` (vòng gọi tool, biểu đồ).

### Web

```
src/
  app/        router, apiClient, AuthProvider
  shared/     components, lib (labels, format, text)
  features/
    iam/  sch/  ppl/  scd/  att/  lrn/  par/  com/  adm/
      api.ts          gọi API, một đối tượng <module>Api (scdApi, schApi)
      types.ts        kiểu dữ liệu trả về từ API
      routes.tsx      route của feature
      pages/          <Tên>Page.tsx
      components/     component dùng trong feature, kèm file .css của feature
      hooks/          useXxx.ts bọc TanStack Query (riêng lrn dùng một file hooks.ts)
      labels.ts       nhãn chỉ dùng trong feature đó (nếu có)
```

Tên feature trùng tên module backend. Gọi API chỉ qua `features/<module>/api.ts`. File phụ trợ riêng của feature (ví dụ `semester.ts`, `subjectColor.ts`) đặt ngay trong thư mục feature.

### Mobile

```
lib/
  core/
    auth/         user.dart (UserInfo, AccountScope), auth_controller.dart (phiên, apiProvider)
    http/         api_client.dart: Dio dùng chung, gắn Bearer, gặp 401 thì refresh một lần rồi gửi lại
    theme/        app_color.dart, app_spacing.dart, app_theme.dart
    widgets/      widget dùng chung: AuthScaffold, HeroHeader, AppCard, Pill, PersonRow, StatTile, MsmsAiBubble, AsyncView…
    config.dart  json.dart  format.dart  labels.dart  realtime.dart  router.dart
  features/
    home/         trang chủ theo loại tài khoản (ghép dữ liệu nhiều module)
    iam/  scd/  att/  lrn/  par/  com/
                  <module>_api.dart: model + hàm gọi API + provider; các file *_page(s).dart là màn hình
  main.dart
```

Gọi API chỉ qua `features/<module>/<module>_api.dart` (riêng hộp thư ở `par/inbox_api.dart`; COM tách theo tính năng: `com/notification_api.dart` cho thông báo, `com/feed_api.dart` cho bảng tin). Nhãn enum dịch một chỗ trong `core/labels.dart`. Provider đọc dữ liệu dùng `autoDispose` và theo dõi `currentUserProvider` để đổi tài khoản không lẫn dữ liệu cũ. Không có `sch/`, `ppl/`, `adm/` — mobile không có màn hình nào thuộc ba module đó.

### Test

```
MSMS.Tests/
  Rules/           test hàm thuần, không cần DB (AssignmentRulesTests, LeaveRequestRulesTests…)
  Modules/<Module>/ test theo module, tên file <Chủ đề>Tests.cs
  Authorization/   chính sách và phạm vi theo loại tài khoản (hiện mới có Iam); KHÔNG dùng School Admin (spec mục 2.5)
```

### Luật khi code

1. Controller chỉ nhận request, kiểm quyền, gọi service, trả DTO. Không `if` nghiệp vụ.
2. Mọi trạng thái suy ra gọi `Common/Rules`. Viết lại trong module là reject PR.
3. Service tiêm `IClock`. Không gọi `DateTime.Now` ở bất kỳ đâu.
4. Module cần dữ liệu module khác thì gọi service của nó, không query bảng của nó.
5. Thao tác nhiều bước (Phát hành, import, thay người) bọc trong một transaction.

### Đặt tên

**Backend (C#)**

| Thứ | Quy tắc | Ví dụ |
| --- | --- | --- |
| Tên file | trùng tên kiểu chính trong file, PascalCase | `TimetablePublishService.cs` |
| Entity | danh từ số ít, một file một entity | `TeachingSession.cs` |
| Service | `<Việc>Service`, hàm async hậu tố `Async` | `ScheduleAdjustmentService.ChangeTeacherAsync` |
| Controller | `<Việc>Controller`, route `api/<module>/<việc>` | `AdjustmentController` → `api/scd/adjust` |
| DTO | `record`, hậu tố `Dto`; request hậu tố `Request`; kết quả thao tác hậu tố `Result`. Mỗi module gom vào một file `<Module>Dtos.cs` | `GridCellDto`, `ChangeSessionTeacherRequest`, `TimetableImportResult` |
| Quy tắc nghiệp vụ | `Common/Rules/<Chủ đề>Rules.cs` | `SessionLockRules` |
| Test | `<Chủ đề>Tests.cs` | `MakeUpDayRulesTests.cs` |
| Migration | do EF sinh: `<yyyyMMddHHmmss>_<Việc>` | `20261022062221_AddAccountSession` |

**Web (TypeScript)**

| Thứ | Quy tắc | Ví dụ |
| --- | --- | --- |
| Trang | `<Tên>Page.tsx` trong `pages/` | `SchedulePage.tsx` |
| Component | PascalCase, tên file = tên component | `TimetableGrid.tsx` |
| Hook | `useXxx`, file cùng tên hoặc nhóm trong một file | `useCatalogs.ts` |
| Đối tượng gọi API | `<module>Api` | `scdApi` |
| File tiện ích | camelCase | `subjectColor.ts` |
| CSS | một file `<module>.css` cho cả feature, đặt trong `components/` | `scd.css` |

**Map vào WBS.** Task `SCD-02-BE-03` nằm ở `Modules/Scd/Services/ScheduleAdjustmentService.cs`. Mỗi module một branch `feat/<MODULE>` (`feat/SCD`, `feat/LRN`…), nhánh chính để merge là `dev`. Commit tiếng Anh, mở đầu bằng Feature ID, mỗi ID một commit: `LRN-01: <nội dung>`.

---

## 3. Hiển thị tiếng Việt

### Năm quy ước

**1. Code tiếng Anh, hiển thị tiếng Việt.**
Bảng, cột, class, enum, biến giữ tiếng Anh. Chỉ nhãn người dùng nhìn thấy mới tiếng Việt.

**2. Một file nhãn chung.**
Thư mục `shared/lib/labels/` (nhãn dùng chung theo nhóm: `account`, `audit`, `auth`, `enrollment`, `notification`, `post`, `status`; `index.ts` chỉ re-export). Nhãn chỉ một feature dùng thì để ở `features/<module>/labels.ts` (hiện có `lrn`, `scd`) và bản Flutter tương ứng, dịch enum một chỗ: `Present → "Có mặt"`, `Pending → "Chờ duyệt"`, `Scheduled → "Đã xếp lịch"`. Không để mỗi màn hình tự dịch.

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
| SCH | Cơ cấu nhà trường |
| PPL | Hồ sơ |
| SCD | Thời khóa biểu · Lịch dạy |
| ATT | Điểm danh |
| LRN | Bài tập rèn luyện |
| PAR | Phụ huynh |
| COM | Thông báo · Bảng tin |
| ADM | Bảng điều khiển · Quản trị (Tài khoản · Nhật ký · Xuất dữ liệu) |
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
| TextbookBook | Sách giáo khoa đã nạp |
| TextbookLesson | Bài trong mục lục sách |
| AssignmentQuestion | Câu hỏi của bài tập |
| Assignment | Bài tập |
| Submission | Lượt làm bài |
| AiFeedback | Phân tích bằng AI |
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
| Bài tập | Đã giao · Đã đóng |
| Bài làm của học sinh | Đã nộp · Không nộp (bài đã đóng mà chưa nộp; không phải 0 điểm) |
| Danh mục và hồ sơ (phân hiệu, tổ, môn, lớp, khung giờ, cán bộ) | Đang hoạt động · Ngừng hoạt động |
| Phân tích bằng AI | Đang phân tích · Xong · Lỗi |
| Phiên bản TKB | Nháp · Đã phát hành |
| Bài đăng | Đã đăng · Đã ẩn |
| Học sinh | Đang học · Đã chuyển trường · Đã tốt nghiệp |

Màu badge: xanh lá = đang hiệu lực · vàng = chờ · xám = nháp hoặc đã ẩn · đỏ = hủy, khóa, lỗi.

### 4.5. Nhãn nút

**Chung:** Lưu · Đóng · Thêm · Sửa · Xóa · Tìm kiếm · Tải file mẫu · Tải lên · Xuất Excel

**Nghiệp vụ:** Phát hành · Xem trước · Đổi giáo viên · Hủy tiết · Thêm tiết bù · Gửi đơn · Rút đơn · Duyệt · Từ chối · Giao bài · Công bố điểm · Công bố phân tích · Nhân bản · Ẩn bài · Khóa tài khoản · Cấp lại mật khẩu · Thêm câu hỏi · Ẩn khỏi ngân hàng · Sinh câu hỏi nháp · Xác nhận câu hỏi · Lưu nháp · Nộp bài · Chạy phân tích · Tạo lại

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
| Bảng điều khiển | Tình hình toàn trường · Tình hình phân hiệu · Tình hình tổ chuyên môn · Tình hình lớp chủ nhiệm · Thống kê hệ thống |
| Danh sách học sinh | — |
| Cơ cấu nhà trường | Thông tin trường · Phân hiệu · Tổ chuyên môn & Môn học · Năm học & Học kỳ · Ngày nghỉ · Khung giờ tiết · Lớp & Ghi danh · Kết chuyển năm học |
| Hồ sơ | Cán bộ · Học sinh · Phụ huynh · Trách nhiệm |
| Thời khóa biểu | Thời khóa biểu · Phụ trách chuyên môn · Lịch dạy · Giáo viên vắng |
| Điểm danh | — |
| Bài tập rèn luyện | Kho câu hỏi · Giao bài tập · Bài tập đã giao · Bài tập được giao · Danh sách bài tập · Kết quả của con |
| Phụ huynh | Đơn xin nghỉ · Sổ liên lạc · Hộp thư |
| Bảng tin | Đăng tin · Bảng tin |
| Quản trị | Tài khoản · Nhật ký · Xuất dữ liệu |

- Nhóm chỉ có một màn hình thì không có cấp 2. Áp dụng cả sau khi lọc theo quyền: nhóm chỉ còn một mục con hiển thị cho loại tài khoản đó thì hiện thẳng thành mục cấp 1 (ví dụ giáo viên chỉ thấy **Lịch dạy**; chủ nhiệm chỉ có quyền xuất điểm danh thì nhóm Quản trị hiện thẳng thành **Xuất dữ liệu**). School Admin không thấy Thời khóa biểu và Xuất dữ liệu; nhóm Quản trị của School Admin gồm **Tài khoản** và **Nhật ký**. **Bảng điều khiển** là ngoại lệ có chủ ý: luôn giữ nhóm cha dù chỉ còn một mục con (School Admin chỉ có **Thống kê hệ thống**), cờ `keepGroup` trong `app/menu.tsx`.
- **Trang chủ** là mục cấp 1 đầu tiên, mọi loại tài khoản đều thấy; ngay dưới là **Bảng điều khiển** (cùng cụm Tổng quan) cho mọi cán bộ có việc theo dõi. Giáo viên không giữ trách nhiệm nào thì không có Bảng điều khiển; lịch dạy hôm nay đã nằm ở Trang chủ. Trang chủ chỉ chào và hiện thông tin chung (hôm nay, năm học, ngày nghỉ sắp tới, lịch dạy của giáo viên — Business Spec §5.1); không đặt số liệu thống kê, lối tắt hay bảng tin ở Trang chủ (số liệu ở **Bảng điều khiển**). `/dashboard` không phải màn hình, chuyển tới mục đầu tiên người dùng được mở.
- Mục cha **không điều hướng**, chỉ xổ ra; vào trang con thì cha tự xổ và sáng lên.
- Icon chỉ ở cấp 1; mục con thụt vào, không icon, không chấm đầu dòng.
- **Ẩn hẳn mục không có quyền**, không hiện rồi khóa.
- Thông báo, hồ sơ cá nhân, đổi mật khẩu, đăng xuất nằm ở **góc phải header**, không vào sidebar.

**Ai thấy mục nào.** Một mục chỉ hiện cho loại tài khoản **có việc làm trên màn hình đó**. Quyền đọc dữ liệu (cột Đọc của Business Spec §5) không tự sinh ra màn hình: Hiệu phó và giáo viên đọc môn học, khung giờ tiết, lớp, ngày nghỉ ngay trong lưới thời khóa biểu, lịch dạy và trang chủ, nên giáo viên không có màn hình Cơ cấu nhà trường; Hiệu phó xem Thông tin trường, Tổ chuyên môn & Môn học, Năm học & Học kỳ và Lớp & Ghi danh ở chế độ chỉ đọc như Hiệu trưởng, trừ Phân hiệu (Lớp & Ghi danh chỉ có lớp của phân hiệu mình phụ trách); chủ nhiệm xem học sinh lớp mình ở mục cấp 1 **Danh sách học sinh**, mục đầu tiên của cụm Quản lý; Hiệu trưởng xem các màn này ở chế độ chỉ đọc, mọi thao tác sửa thuộc School Admin. Gõ thẳng đường dẫn của mục không được thấy thì hiện trang 403. Nguồn duy nhất ở web là `app/menu.tsx`.

| Mục | Ai thấy |
| --- | --- |
| Trang chủ · Bảng tin | Mọi loại |
| Bảng điều khiển › Tình hình toàn trường | HT |
| Bảng điều khiển › Tình hình phân hiệu | HP |
| Bảng điều khiển › Tình hình tổ chuyên môn | GV (chỉ tổ trưởng) |
| Bảng điều khiển › Tình hình lớp chủ nhiệm | GV (chỉ chủ nhiệm) |
| Bảng điều khiển › Thống kê hệ thống | SA |
| Danh sách học sinh | GV (chỉ chủ nhiệm) |
| Cơ cấu nhà trường › Phân hiệu | SA (sửa), HT (chỉ xem) |
| Cơ cấu nhà trường › Thông tin trường · Tổ chuyên môn & Môn học · Năm học & Học kỳ · Lớp & Ghi danh | SA (sửa), HT, HP (chỉ xem; Lớp & Ghi danh của HP chỉ có lớp thuộc phân hiệu mình phụ trách) |
| Cơ cấu nhà trường › Ngày nghỉ · Khung giờ tiết · Kết chuyển năm học | SA |
| Hồ sơ › Cán bộ · Học sinh · Phụ huynh | SA, HT, HP |
| Hồ sơ › Trách nhiệm | SA, HT |
| Thời khóa biểu › Thời khóa biểu · Phụ trách chuyên môn | HT (chỉ xem), HP |
| Thời khóa biểu › Giáo viên vắng | HT (chỉ xem), HP |
| Thời khóa biểu › Lịch dạy | HT, HP, GV |
| Điểm danh | SA, HT, HP, GV, HS, PH |
| Bài tập rèn luyện › Kho câu hỏi · Giao bài tập · Bài tập đã giao | GV |
| Bài tập rèn luyện › Bài tập được giao · Danh sách bài tập | HS |
| Bài tập rèn luyện › Kết quả của con | PH |
| Phụ huynh › Đơn xin nghỉ | GV (chỉ chủ nhiệm), PH |
| Phụ huynh › Sổ liên lạc | GV, HS, PH |
| Phụ huynh › Hộp thư | GV, PH |
| Quản trị › Tài khoản · Nhật ký | SA |
| Quản trị › Xuất dữ liệu | HT, HP, GV |

Ký hiệu như Business Spec §5. Kết quả với các màn đã có: **SA** Trang chủ · Bảng điều khiển (Thống kê hệ thống) · Cơ cấu nhà trường (đủ 8 mục) · Hồ sơ (Cán bộ, Trách nhiệm) · Tài khoản — **HT** Trang chủ · Bảng điều khiển (Tình hình toàn trường) · Cơ cấu nhà trường (5 mục chỉ xem) · Hồ sơ (Cán bộ, Trách nhiệm) · Lịch dạy · Quản trị (Xuất dữ liệu) — **HP** Trang chủ · Bảng điều khiển (Tình hình phân hiệu) · Cơ cấu nhà trường (4 mục chỉ xem, không có Phân hiệu) · Cán bộ · Thời khóa biểu (Thời khóa biểu, Lịch dạy) — **GV** Trang chủ · Lịch dạy (chủ nhiệm thêm Danh sách học sinh; chủ nhiệm hoặc tổ trưởng thêm Bảng điều khiển) — **HS, PH** Trang chủ.
