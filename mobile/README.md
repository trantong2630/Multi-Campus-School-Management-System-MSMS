# MSMS Mobile

App Flutter cho **Giáo viên, Học sinh, Phụ huynh** (spec §8.1). Thiết kế: `docs/MSMS_Design_Mobile.md`; cấu trúc code: `docs/MSMS_Dev_Conventions.md` mục Mobile.

## Chạy khi dev

1. Chạy backend: `cd backend/MSMS.Api && dotnet run --launch-profile http` (API ở `http://localhost:5063`).
2. Mở emulator Android (Pixel_5), rồi trong thư mục `mobile/`:

```
flutter pub get
flutter run
```

Emulator gọi máy tính qua `10.0.2.2`, nên không cần cấu hình gì thêm. Tài khoản mẫu do `DevSeeder` tạo, mật khẩu chung `Msms@123`.

## Build APK

```
flutter build apk --release --dart-define=API_URL=https://api.ten-mien.com
```

File ra ở `build/app/outputs/flutter-apk/app-release.apk`. Thiếu `API_URL` thì app trỏ về `http://10.0.2.2:5063`, chỉ dùng được trên emulator.

- Điện thoại thật cùng Wi-Fi: chạy backend với `--urls http://0.0.0.0:5063`, mở cổng 5063 trên Windows Firewall, build với `API_URL=http://<IP máy tính>:5063`.
- Backend đã deploy: dùng domain HTTPS của server.

## Lưu ý

- Project nằm trong OneDrive nên `android/gradle.properties` tắt `kotlin.incremental`; bật lại sẽ gặp lỗi "Could not close incremental caches".
- Material import từ `package:material_ui/material_ui.dart` (Flutter 3.50), không dùng `package:flutter/material.dart`.
- Không ghi mã màu trong widget; lấy từ `lib/core/theme/`.
