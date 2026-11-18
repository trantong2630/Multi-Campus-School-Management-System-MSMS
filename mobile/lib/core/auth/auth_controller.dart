import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../http/api_client.dart';
import '../json.dart';
import 'user.dart';

final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore());

final apiProvider = Provider<ApiClient>((ref) {
  final api = ApiClient(ref.watch(tokenStoreProvider));
  api.onSessionExpired = () => ref.read(authProvider.notifier).expire();
  api.onRefreshed = (u) => ref.read(authProvider.notifier).updateUser(u);
  return api;
});

enum AuthStatus { checking, unreachable, signedOut, signedIn }

class AuthState {
  const AuthState(this.status, {this.user, this.notice});

  final AuthStatus status;
  final UserInfo? user;

  /// Một dòng hiện ở màn đăng nhập sau khi bị đưa ra (phiên hết hạn, tài khoản chỉ dùng web).
  final String? notice;
}

final authProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);

class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() {
    Future.microtask(restore);
    return const AuthState(AuthStatus.checking);
  }

  ApiClient get _api => ref.read(apiProvider);

  /// Mở app: dùng refresh token đã lưu để vào thẳng, không bắt đăng nhập lại.
  Future<void> restore() async {
    state = const AuthState(AuthStatus.checking);
    try {
      final r = await _api.refresh();
      state = r == null ? const AuthState(AuthStatus.signedOut) : _signedIn(r.user);
    } on DioException {
      state = const AuthState(AuthStatus.unreachable);
    }
  }

  Future<void> login(String username, String password) async {
    final r = await _api.login(username, password);
    state = _signedIn(r.user);
  }

  AuthState _signedIn(UserInfo user) {
    if (!user.hasMobile) {
      // Đăng nhập thành công nhưng loại tài khoản này không có mobile: thu hồi phiên vừa tạo.
      _api.post<void>('/api/auth/logout').catchError((_) {});
      _api.tokens.clear();
      return const AuthState(AuthStatus.signedOut, notice: 'Tài khoản Quản trị, Hiệu trưởng và Hiệu phó dùng MSMS trên web.');
    }
    return AuthState(AuthStatus.signedIn, user: user);
  }

  void updateUser(UserInfo user) {
    if (state.status == AuthStatus.signedIn) state = AuthState(AuthStatus.signedIn, user: user);
  }

  /// Đọc lại thông tin người dùng (sau khi đổi mật khẩu lần đầu).
  Future<void> reloadMe() async {
    final j = await _api.get<Json>('/api/auth/me');
    state = _signedIn(UserInfo.fromJson(j));
  }

  void expire() {
    if (state.status != AuthStatus.signedIn) return;
    _api.tokens.clear();
    state = const AuthState(AuthStatus.signedOut, notice: 'Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục.');
  }

  /// Đăng xuất thu hồi phiên ở Backend (BR-05); mất mạng vẫn xóa token trên máy.
  Future<void> logout() async {
    try {
      await _api.post<void>('/api/auth/logout');
    } catch (_) {}
    await _api.tokens.clear();
    state = const AuthState(AuthStatus.signedOut);
  }
}

UserInfo? _lastUser;

/// Người dùng hiện tại; chỉ dùng trong các màn sau đăng nhập. Giữ người dùng cuối cùng để các màn
/// đang đóng dần sau khi đăng xuất không bị lỗi trước khi router chuyển về màn đăng nhập.
final currentUserProvider = Provider<UserInfo>((ref) {
  final u = ref.watch(authProvider).user;
  if (u != null) _lastUser = u;
  return _lastUser!;
});
