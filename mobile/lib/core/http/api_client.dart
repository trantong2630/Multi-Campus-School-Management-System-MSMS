import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../auth/user.dart';
import '../config.dart';
import '../json.dart';

/// Phiên hết hạn (refresh token bị từ chối): người dùng phải đăng nhập lại.
class SessionExpiredException implements Exception {
  const SessionExpiredException();
  @override
  String toString() => 'Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục.';
}

/// Giữ access token trong bộ nhớ, refresh token trong secure storage (Dev_Conventions §1).
class TokenStore {
  TokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  static const _refreshKey = 'msms_rt';
  final FlutterSecureStorage _storage;

  String? accessToken;
  DateTime? expiresAt;

  Future<String?> readRefresh() => _storage.read(key: _refreshKey);

  Future<void> save(LoginResult r) async {
    accessToken = r.token;
    expiresAt = r.expiresAt.toUtc();
    if (r.refreshToken != null) await _storage.write(key: _refreshKey, value: r.refreshToken);
  }

  Future<void> clear() async {
    accessToken = null;
    expiresAt = null;
    await _storage.delete(key: _refreshKey);
  }
}

/// Một Dio dùng chung: gắn Bearer, gặp 401 thì refresh một lần (gộp các request cùng lúc) rồi gửi lại.
class ApiClient {
  ApiClient(this.tokens) {
    final options = BaseOptions(
      baseUrl: AppConfig.apiUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 30),
      contentType: Headers.jsonContentType,
    );
    _raw = Dio(options);
    dio = Dio(options);
    dio.interceptors.add(InterceptorsWrapper(onRequest: _onRequest, onError: _onError));
  }

  final TokenStore tokens;
  late final Dio dio;
  late final Dio _raw;

  /// Gọi khi refresh token bị từ chối; AuthController đưa người dùng về màn đăng nhập.
  void Function()? onSessionExpired;

  /// Gọi sau mỗi lần refresh thành công để cập nhật thông tin người dùng.
  void Function(UserInfo user)? onRefreshed;

  Future<LoginResult?>? _refreshing;

  void _onRequest(RequestOptions o, RequestInterceptorHandler h) {
    final t = tokens.accessToken;
    if (t != null) o.headers['Authorization'] = 'Bearer $t';
    h.next(o);
  }

  Future<void> _onError(DioException e, ErrorInterceptorHandler h) async {
    final o = e.requestOptions;
    final isAuthCall = o.path.startsWith('/api/auth/login') || o.path.startsWith('/api/auth/refresh');
    if (e.response?.statusCode != 401 || isAuthCall || o.extra['retried'] == true) return h.next(e);
    try {
      final r = await refresh();
      if (r == null) {
        onSessionExpired?.call();
        return h.next(e);
      }
      o.extra['retried'] = true;
      o.headers['Authorization'] = 'Bearer ${r.token}';
      h.resolve(await dio.fetch(o));
    } on DioException catch (err) {
      h.next(err);
    }
  }

  /// Đổi refresh token lấy access token mới. Trả null khi phiên đã hết (401); lỗi mạng thì ném ra.
  Future<LoginResult?> refresh() => _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<LoginResult?> _doRefresh() async {
    final rt = await tokens.readRefresh();
    if (rt == null) return null;
    try {
      final res = await _raw.post<Json>('/api/auth/refresh', data: {'refreshToken': rt});
      final r = LoginResult.fromJson(res.data!);
      await tokens.save(r);
      onRefreshed?.call(r.user);
      return r;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) {
        await tokens.clear();
        return null;
      }
      rethrow;
    }
  }

  /// Token còn hạn ít nhất 30 giây (dùng cho kết nối SignalR, vốn không đi qua interceptor).
  Future<String?> freshToken() async {
    final exp = tokens.expiresAt;
    if (tokens.accessToken != null && exp != null && exp.isAfter(DateTime.now().toUtc().add(const Duration(seconds: 30)))) {
      return tokens.accessToken;
    }
    final r = await refresh();
    if (r == null) onSessionExpired?.call();
    return r?.token;
  }

  Future<LoginResult> login(String username, String password) async {
    final res = await _raw.post<Json>('/api/auth/login', data: {'username': username.trim(), 'password': password, 'clientType': 'mobile'});
    final r = LoginResult.fromJson(res.data!);
    await tokens.save(r);
    return r;
  }

  Future<T> get<T>(String path, {Map<String, dynamic>? query}) async => (await dio.get<T>(path, queryParameters: _clean(query))).data as T;

  Future<T> post<T>(String path, {Object? data}) async => (await dio.post<T>(path, data: data)).data as T;

  Future<T> put<T>(String path, {Object? data}) async => (await dio.put<T>(path, data: data)).data as T;

  Future<T> delete<T>(String path) async => (await dio.delete<T>(path)).data as T;

  static Map<String, dynamic>? _clean(Map<String, dynamic>? q) => q == null ? null : (Map.of(q)..removeWhere((_, v) => v == null));
}

/// Backend trả `{ code, message }` tiếng Việt; app chỉ hiện message (Dev_Conventions §3).
String errorMessage(Object error) {
  if (error is SessionExpiredException) return error.toString();
  if (error is DioException) {
    final data = error.response?.data;
    if (data is Map && data['message'] is String) return data['message'] as String;
    if (data is Map && data['errors'] is Map) {
      final first = (data['errors'] as Map).values.expand((v) => v is List ? v : [v]).firstOrNull;
      if (first != null) return first.toString();
    }
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.sendTimeout:
        return 'Máy chủ phản hồi quá lâu. Vui lòng thử lại.';
      case DioExceptionType.connectionError:
        return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.';
      default:
        break;
    }
    if (error.response == null) return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.';
    if (error.response?.statusCode == 403) return 'Không có quyền xem nội dung này.';
    if (error.response?.statusCode == 404) return 'Nội dung này không còn tồn tại.';
  }
  return 'Có lỗi xảy ra. Thử lại sau, nếu vẫn lỗi thì liên hệ quản trị.';
}

bool isForbidden(Object error) => error is DioException && error.response?.statusCode == 403;
