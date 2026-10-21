# MSMS — Kế hoạch triển khai Backend + Web

Oct 20, 2026 · @Trần Đức Kiên

Kế hoạch code từ đầu đến hết dự án cho team 5 người. Phạm vi: toàn bộ Backend + Web. Mobile làm sau.

## 1. Nguyên tắc chống xung đột

Xung đột code không đến từ chia việc không đều, nó đến từ nhiều người sửa chung một file. Repo đã chia theo module nghiệp vụ nên mỗi người một thư mục là gần như không đụng nhau. Chỉ còn vài file dùng chung phải chốt cứng.

| Nguyên tắc | Nghĩa là |
| --- | --- |
| Một module một chủ | Không ai sửa `Modules/Xxx/` của người khác. Cần thì nhắn chủ module |
| File dùng chung có chủ riêng | `Data/Entities/`, `Program.cs`, `router.tsx` chỉ một người được sửa |
| Chốt API trước khi code | FE gọi mock theo OpenAPI, BE làm song song, không ai chặn ai |
| Nhánh sống ngắn | Tối đa 3 ngày. Nhánh sống 3 tuần là nguồn conflict lớn nhất |
| Không format dạo | Chỉ format file mình sửa. Format toàn repo tạo conflict giả hàng trăm dòng |

Một quyết định quan trọng về schema: **code-first — entity và `MsmsDbContext` là nguồn sự thật của schema, DB dựng và đổi bằng EF Migrations** (`dotnet ef migrations add <Tên>`, `dotnet ef database update`). Không còn file `.sql` dựng DB. Migration đầu tiên là `CreateInitialSchema`; collation `Vietnamese_CI_AS`, sequence `seq_PersonCode` và các giá trị mặc định ngày giờ khai báo trong `MsmsDbContext`.

Giá phải trả: nhiều người sinh migration song song sẽ đụng file `MsmsDbContextModelSnapshot.cs`. Giảm rủi ro bằng luật cũ: mọi thay đổi schema do một người (A) làm trong một PR riêng, merge đầu giờ sáng, những người khác pull trước khi tạo migration của mình.

## 2. Chia người

Chia theo module, lệch có chủ ý. Mỗi người làm trọn cả Backend lẫn Web của module mình để không phải bàn giao qua lại.

| Dev | Module sở hữu | Khối lượng & độ khó | Vì sao gom như vậy |
| --- | --- | --- | --- |
| **A** | Nền tảng M0, `Common/Rules`, SCD-01, SCD-02, SCD-03 | Ít feature, khó nhất | Ba feature SCD dùng chung `ScheduleConflict` và logic sinh tiết. Tách ra là hai người viết lại cùng một thứ |
| **B** | IAM-02/03/04, ATT-01, PAR-01, ADM-01 | Vừa feature, khó vừa | BR-18 (nhãn có phép) nằm vắt giữa ATT và PAR-01, phải cùng một người |
| **C** | SCH-01 đến SCH-07, ADM-03 | Nhiều feature, mỗi cái dễ | CRUD và danh mục, lặp lại một khuôn. Đổi lại phải xong sớm nhất vì chặn mọi người |
| **D** | PPL-01 đến PPL-05, COM-01, COM-02 | Vừa feature, khó vừa | Import Excel hồ sơ và khoảng hiệu lực BR-09 là phần nặng của D |
| **E** | LRN-01 đến LRN-05, PAR-02, PAR-03 | Nhiều feature, khó vừa | Làm bài, chấm, gọi AI là một mạch khép kín, ít dính module khác |

Cân bằng ở đây là đánh đổi giữa số feature và độ khó, không phải chia đều số dòng code. A ít feature nhưng phần khó nhất nên không nhận thêm module phụ. C nhiều feature nhưng cùng một khuôn, đổi lại phải xong trước mọi người.

Giá phải trả của mô hình một người một module: ai nghỉ hoặc chậm thì phần đó đứng im, không ai đỡ được vì không đọc code. Giảm rủi ro bằng cách bắt buộc review chéo giữa các module, để mỗi người vẫn biết đại khái code người khác.

## 3. Sơ đồ phụ thuộc

&#91;embedded content: Phụ thuộc giữa các mốc · 10 mốc, 1 nhánh tách\]

M0 chặn cả team, M1 chặn bốn người, M4 chặn toàn bộ phần vận hành. M6 Học tập tách nhánh ngay sau M3 nên E chạy song song với A và B, không phải chờ thời khóa biểu.

## 4. Các mốc

Mười mốc (M0–M9). Mốc sau chỉ bắt đầu khi mốc trước đạt điều kiện xong, trừ chỗ ghi rõ là chạy song song.

### M0 — Nền tảng

Cả team bị chặn ở đây. Không ai tách nhánh feature trước khi M0 xong.

**A + D làm:**

- Chạy được `dotnet ef database update` + seed dev: 2 phân hiệu, 8 lớp, 20 cán bộ, 200 học sinh, khung giờ tiết, năm học 2026-2027
- `Data/`: toàn bộ entity + `MsmsDbContext` + configuration
- `Common/`: `IClock`, `CurrentUser`, `ScopeResolver`, `ApiException`, `ErrorCodes`, `ExceptionMiddleware`, `AuditWriter`, khung `ExcelReader`/`ExcelWriter`
- IAM-01: đăng nhập JWT, policy phân quyền, `IdentityActionGuard` (mục 2.4 spec)
- Web: `app/` (router, apiClient, AuthProvider), layout + sidebar 2 cấp, trang đăng nhập
- Một trang CRUD mẫu hoàn chỉnh (SCH-01 Phân hiệu) làm khuôn cho 4 người còn lại copy

**B, C, E làm song song:** cài môi trường, chạy được DB local, viết OpenAPI nháp cho module của mình.

**Xong khi:** mọi người `git clone` về, chạy một script dựng DB, `dotnet run` + `npm run dev`, đăng nhập được bằng tài khoản seed, mở được trang Phân hiệu.

### M1 — Thiết lập trường

**C làm một mình.** SCH-02 đến SCH-05: Tổ chuyên môn & Môn học, Năm học & Học kỳ & Ngày nghỉ, Khung giờ tiết, Lớp & Ghi danh.

SCH-07 (thông tin trường: tên, địa chỉ, logo) làm cùng mốc này vì màn hình đăng nhập và thanh bên dùng nó để nhận diện trường.

SCH-06 (kết chuyển năm học) để sau, không ai chờ nó.

**Xong khi:** khai được đủ dữ liệu nền cho một phân hiệu thật — tổ, môn có viết tắt, năm học hiện hành, hai học kỳ, khung giờ tiết, danh sách lớp.

**Mở khóa:** M2 (D cần có lớp để ghi danh), M4 (A cần khung giờ tiết và lớp để dựng file mẫu).

### M2 — Hồ sơ và tài khoản

**D làm:** PPL-01 danh bạ cán bộ (gồm viết tắt giáo viên), PPL-02 danh bạ học sinh, PPL-03 phụ huynh và liên kết. Cả ba kèm import Excel theo BR-11.

**B làm song song:** IAM-02 tạo tài khoản đơn lẻ và hàng loạt, IAM-03 mật khẩu và OTP, IAM-04 hồ sơ cá nhân.

Hai người đụng nhau một chỗ: nút "Tạo tài khoản" nằm trên màn hình danh bạ của D nhưng gọi API của B. Chốt trước: D làm màn hình và nút, B làm API và hộp thoại kết quả, hai bên thống nhất OpenAPI ở đầu mốc.

**Xong khi:** import được file cán bộ và file học sinh mẫu, chọn nhiều dòng tạo tài khoản hàng loạt, tải được file mật khẩu tạm, đăng nhập lần đầu buộc đổi mật khẩu.

**Mở khóa:** M3, và mọi thứ cần đăng nhập đúng loại tài khoản.

### M3 — Trách nhiệm và phụ trách chuyên môn

**D làm.** PPL-04 trách nhiệm (phân hiệu, tổ trưởng, chủ nhiệm), PPL-05 phụ trách chuyên môn. Cốt lõi là BR-09: khoảng hiệu lực và quy tắc ngày liền trước khi thay người.

Logic khoảng hiệu lực viết vào `Common/Rules/EffectivePeriod.cs` — **A review bắt buộc**, vì SCD và LRN đều gọi lại.

**Xong khi:** gán và thay được chủ nhiệm, tổ trưởng, phụ trách phân hiệu, phụ trách chuyên môn; thay người tạo đúng hai bản ghi không chạm ngày nào.

**Mở khóa:** M5 (duyệt đơn nghỉ cần chủ nhiệm), M6 (giao bài cần phụ trách chuyên môn).

### M4 — Thời khóa biểu

**A làm một mình.** Phần khó nhất của cả dự án, chia thành bốn nhánh nhỏ làm tuần tự:

1. Sinh file mẫu theo cấu hình, điền sẵn bản đã phát hành gần nhất (BR-12)
2. Đọc file lưới, đối chiếu viết tắt, bảng lỗi theo địa chỉ ô
3. Xem trước: số tiết xóa và tạo, cảnh báo tiết chỉnh tay, danh sách xung đột giáo viên (BR-13)
4. Phát hành trong một transaction, sinh tiết tới hết học kỳ, trừ ngày nghỉ (BR-14)

**Xong khi:** tải mẫu, sửa vài ô, tải lên, xem trước, phát hành, nhìn thấy tiết học sinh ra đúng trên lịch dạy; phát hành lại không đụng tiết đã qua.

**Mở khóa:** M5 toàn bộ.

### M5 — Vận hành lịch và điểm danh

**A làm:** SCD-02 điều chỉnh lịch (đổi giáo viên, hủy, tiết bù, ngày nghỉ đột xuất), SCD-03 xử lý giáo viên vắng.

**B làm song song:** ATT-01 điểm danh theo tiết, PAR-01 đơn xin nghỉ. Hai cái này đi cùng nhau vì BR-18 suy nhãn có phép từ đơn đã duyệt.

**Xong khi:** hủy một tiết thì nó biến khỏi lịch học sinh; khai giáo viên vắng thì tiết chuyển Chờ xử lý ở mọi phân hiệu; giáo viên điểm danh trong ngày, hôm sau chỉ School Admin sửa được; đơn duyệt muộn tự đổi nhãn buổi vắng đã ghi.

### M6 — Học tập

**E làm một mình**, chia ba nhánh nhỏ:

1. LRN-01 ngân hàng câu hỏi và đề (gồm nhân bản, khóa đề khi giao lần đầu)
2. LRN-02 giao bài, học sinh làm bài, tự lưu nháp, nộp một lượt, chấm ngay
3. LRN-03 công bố điểm và thống kê câu sai nhiều nhất

LRN-04 (AI) và LRN-05 (giám sát tổ) tách riêng, làm ở M8 nếu còn thời gian.

**Xong khi:** giáo viên soạn đề, giao cho lớp mình phụ trách, học sinh nộp được, điểm chấm đúng, công bố xong học sinh thấy điểm.

### M7 — Phụ huynh và truyền thông

**E làm:** PAR-02 sổ liên lạc tuần (tính trực tiếp, không lưu), PAR-03 hộp thư.

**D làm song song:** COM-01 trung tâm thông báo, COM-02 bảng tin.

COM-01 có một ràng buộc: mọi module trước đó đã gọi `NotificationSender` theo danh sách đóng BR-26. D chốt chữ ký hàm này ngay ở M0 để A, B, E gọi được trước khi màn hình thông báo có thật.

**Xong khi:** mọi sự kiện trong BR-26 sinh đúng thông báo, bấm vào điều hướng đúng chỗ, phụ huynh mở được sổ liên lạc của con.

### M8 — Giám sát và hoàn thiện

- **B:** ADM-01 Dashboard sáu chỉ số, mỗi số mở ra danh sách gốc
- **A:** ADM-02 Audit Log
- **C:** ADM-03 xuất Excel ba mục, và SCH-06 kết chuyển năm học
- **E:** LRN-04 phân tích AI, LRN-05 giám sát cấp tổ
- **D:** dọn nợ kỹ thuật của PPL và COM

**Xong khi:** Dashboard ra số khớp với dữ liệu thật, Audit Log ghi đủ ba nhóm, ba file Excel tải về mở được.

### M9 — Kiểm thử phân quyền và demo

**Cả team.** Theo mục 2.5 spec: mỗi loại tài khoản và mỗi trách nhiệm có kịch bản riêng, **không dùng School Admin để kiểm thử phạm vi** của loại khác.

Chia kiểm thử chéo — không ai test module mình viết:

| Người test | Test module của |
| --- | --- |
| A | B |
| B | C |
| C | D |
| D | E |
| E | A |

**Xong khi:** chạy hết kịch bản phân quyền không rò rỉ dữ liệu; dựng được kịch bản demo liền mạch từ thiết lập đầu năm đến sổ liên lạc tuần.

## 5. Chia folder và sở hữu file

Folder đã chốt ở `MSMS_Dev_Conventions.md` mục 2. Phần dưới chỉ gán chủ sở hữu.

### Vùng riêng — không ai đụng của ai

| Vùng | Chủ |
| --- | --- |
| `MSMS.Api/Modules/Scd/` · `web/src/features/scd/` | A |
| `Modules/Iam/`, `Modules/Att/` · `features/iam/`, `features/att/` | B |
| `Modules/Sch/` · `features/sch/` | C |
| `Modules/Ppl/`, `Modules/Com/` · `features/ppl/`, `features/com/` | D |
| `Modules/Lrn/`, `Modules/Par/` · `features/lrn/`, `features/par/` | E |

PAR-01 (đơn nghỉ) thuộc B nhưng nằm trong `Modules/Par/` của E. Tách file rõ: `Par/Services/LeaveRequestService.cs` là của B, phần còn lại của E. Hai file khác nhau thì không conflict.

### Vùng chung — có chủ riêng, người khác phải xin

| File | Chủ | Luật |
| --- | --- | --- |
| `Data/Entities/`, `MsmsDbContext`, `Migrations/` | **A** | Cần thêm cột thì nhắn A. A sửa trong một PR riêng, merge trong ngày |
| `Common/Rules/` | **A** | Ai viết quy tắc mới thì mở PR, A review. Viết lại quy tắc trong module là reject PR |
| `Common/Notifications/` | **D** | Chữ ký `NotificationSender` chốt ở M0, sau đó chỉ thêm loại thông báo |
| `Common/Excel/` | **D** | A và C dùng lại cho file TKB và file xuất |
| `Program.cs` | **A** | Viết một lần ở M0, sau đó gần như không sửa |
| `web/src/app/router.tsx` | **A** | Viết một lần ở M0 |
| `shared/lib/labels/` | mỗi người một file | `labels/scd.ts`, `labels/lrn.ts`… `index.ts` chỉ re-export, viết một lần |
| `package.json`, `.csproj` | ai cũng được | Nhưng phải báo nhóm chat và merge trong ngày |

### Hai mẹo bỏ hẳn xung đột ở file khởi động

Thay vì mọi người sửa chung `Program.cs`, mỗi module tự khai phần của mình:

```csharp
// Modules/Scd/ScdModule.cs — file của A, không ai khác đụng
public static class ScdModule
{
    public static IServiceCollection AddScdModule(this IServiceCollection s)
        => s.AddScoped<IScheduleAdjustService, ScheduleAdjustService>()
            .AddScoped<ITimetableImportService, TimetableImportService>();
}
```

`Program.cs` chỉ còn 9 dòng gọi `AddXxxModule()`, viết một lần ở M0 rồi thôi.

Web làm tương tự: mỗi feature export `routes.tsx` của mình, `router.tsx` chỉ import 9 dòng.

### Quy ước đặt tên để map vào WBS

Task `SCD-02-BE-03` → file `Modules/Scd/Services/ScheduleAdjustService.cs` → nhánh `feat/SCD-02-doi-giao-vien` → commit `SCD-02: doi giao vien cua tiet`.

## 6. Quy ước Git

### Nhánh

```
main        chỉ merge khi demo được, tag theo mốc: v-M4, v-M6...
develop     nhánh chính, mọi người rebase từ đây
feat/<ID>-<viec>    feat/SCD-01-doc-file-tkb
fix/<ID>-<loi>      fix/ATT-01-sai-mui-gio
```

Feature to thì tách nhánh nhỏ, đừng để một nhánh ôm cả feature. SCD-01 tách thành `feat/SCD-01-sinh-mau`, `feat/SCD-01-doc-file`, `feat/SCD-01-xem-truoc`, `feat/SCD-01-phat-hanh`.

### Bảy luật

1. **Nhánh sống tối đa 3 ngày.** Quá 3 ngày thì chia nhỏ việc, đừng chia nhỏ thời gian.
2. **Sáng nào cũng rebase:** `git pull --rebase origin develop` trước khi gõ dòng code đầu tiên.
3. **PR cần một người review**, người đó không cùng module với mình.
4. **Merge bằng squash**, một PR thành một commit trên `develop`. Lịch sử sạch, revert dễ.
5. **Không format toàn file.** `.editorconfig` + Prettier bật từ M0, chỉ format file mình sửa.
6. **Không commit file sinh ra:** `bin/`, `obj/`, `node_modules/`, `.vs/`, `appsettings.Development.json`. Chốt `.gitignore` ở M0.
7. **Không bao giờ `push --force` lên `develop` hay `main`.** Chỉ force được lên nhánh feature của chính mình.

### Khi nào phải báo nhóm

Ba việc này làm xong phải nhắn nhóm ngay, người khác pull về luôn trong ngày:

- Đổi schema hoặc entity
- Thêm thư viện (`package.json`, `.csproj`)
- Sửa file trong `Common/`

Để hai ngày mới báo là lúc đó năm người đã code lệch nhau quá xa.

### Mẫu tên commit

```
SCD-01: doc o meta va cot lop tu file TKB
SCD-01: bang loi theo dia chi o
ATT-01: chan diem danh sau ngay cua tiet
```

Viết không dấu cho chắc. Mở đầu bằng Feature ID để sau này tra ngược từ WBS ra commit.

## 7. Rủi ro và thứ tự cắt giảm

| Rủi ro | Dấu hiệu sớm | Cách gỡ |
| --- | --- | --- |
| C chậm ở M1 | Cuối M1 chưa khai được lớp | A và D nhảy vào làm SCH-04, SCH-05; C giữ phần còn lại |
| M4 vỡ kế hoạch | Hết nửa mốc mà chưa đọc nổi file lưới | Bỏ phần sinh file mẫu điền sẵn, dùng file mẫu tĩnh. Mất tiện lợi, không mất nghiệp vụ |
| Một người nghỉ | Không push 3 ngày | Vì mỗi người một module nên không ai đỡ ngay được. Người gần nhất về domain tiếp quản, chấp nhận chậm |
| Conflict `Data/Entities/` | PR nào cũng đụng file entity | Dồn mọi thay đổi schema vào một PR mỗi ngày, A merge đầu giờ sáng |
| AI hoặc Gmail không gọi được | Lúc demo | Spec đã nói cả hai là tiện ích. Demo vẫn chạy, chỉ hiện trạng thái Lỗi |

### Nếu không kịp, bỏ theo thứ tự này

1. **LRN-04** phân tích AI — bỏ được, vì chấm và công bố điểm không phụ thuộc nó
2. **PAR-02** sổ liên lạc tuần — chỉ là màn hình tổng hợp từ dữ liệu đã có
3. **ADM-01** Dashboard — mọi số đều xem được ở màn hình gốc
4. **SCH-06** kết chuyển năm học — chỉ dùng vào đầu năm sau
5. **LRN-05** giám sát cấp tổ — màn hình chỉ đọc

Năm cái này bỏ đi không làm vỡ luồng nào khác. **Không bỏ** SCD-01, SCD-02, ATT-01, LRN-02 — đó là xương sống của demo.

### Một cân nhắc

LRN-04 là chỗ duy nhất có "điểm sáng tạo" theo tiêu chí đồ án. Nếu bỏ nó thì nên giữ ít nhất phần sinh nháp câu hỏi bằng AI ở LRN-01, vốn nhẹ hơn nhiều: chỉ một lần gọi API, không cần tính chỉ số ẩn danh.
