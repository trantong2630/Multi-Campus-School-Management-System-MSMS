/// Địa chỉ API truyền lúc build: `flutter build apk --dart-define=API_URL=https://...`.
/// Mặc định trỏ về máy chạy emulator (10.0.2.2 là localhost của máy tính).
abstract final class AppConfig {
  static const apiUrl = String.fromEnvironment('API_URL', defaultValue: 'http://10.0.2.2:5063');
}
