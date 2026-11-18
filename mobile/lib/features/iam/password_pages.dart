import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/http/api_client.dart';
import '../../core/json.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/auth_scaffold.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';

const minPasswordLength = 8;

String? _newPasswordRule(String? v) {
  if ((v ?? '').isEmpty) return 'Nhập mật khẩu mới.';
  if (v!.length < minPasswordLength) return 'Mật khẩu mới cần ít nhất $minPasswordLength ký tự.';
  return null;
}

/// Ba ô: mật khẩu hiện tại (tùy chọn), mật khẩu mới, nhập lại.
class _PasswordForm extends StatefulWidget {
  const _PasswordForm({required this.askCurrent, required this.submitLabel, required this.onSubmit});

  final bool askCurrent;
  final String submitLabel;
  final Future<void> Function(String current, String next) onSubmit;

  @override
  State<_PasswordForm> createState() => _PasswordFormState();
}

class _PasswordFormState extends State<_PasswordForm> {
  final _form = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _again = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _again.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy || !_form.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onSubmit(_current.text, _next.text);
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (widget.askCurrent)
            PasswordField(
              controller: _current,
              label: 'Mật khẩu hiện tại',
              enabled: !_busy,
              autofill: const [AutofillHints.password],
              validator: (v) => (v ?? '').isEmpty ? 'Nhập mật khẩu hiện tại.' : null,
            ),
          PasswordField(
            controller: _next,
            label: 'Mật khẩu mới',
            hint: 'Ít nhất $minPasswordLength ký tự',
            enabled: !_busy,
            autofill: const [AutofillHints.newPassword],
            validator: (v) {
              final r = _newPasswordRule(v);
              if (r != null) return r;
              if (widget.askCurrent && v == _current.text) return 'Mật khẩu mới phải khác mật khẩu hiện tại.';
              return null;
            },
          ),
          PasswordField(
            controller: _again,
            label: 'Nhập lại mật khẩu mới',
            enabled: !_busy,
            action: TextInputAction.done,
            onSubmitted: (_) => _submit(),
            validator: (v) {
              if ((v ?? '').isEmpty) return 'Nhập lại mật khẩu mới để xác nhận.';
              if (v != _next.text) return 'Hai mật khẩu chưa giống nhau. Nhập lại cho khớp.';
              return null;
            },
          ),
          if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
          BusyButton(label: widget.submitLabel, busy: _busy, onPressed: _submit),
        ],
      ),
    );
  }
}

/// Lần đăng nhập đầu bắt buộc đổi mật khẩu tạm (BR-04).
class ForcedChangePasswordPage extends ConsumerWidget {
  const ForcedChangePasswordPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return AuthScaffold(
      title: 'Đổi mật khẩu',
      subtitle: 'Đặt mật khẩu mới trước khi tiếp tục',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _PasswordForm(
            askCurrent: true,
            submitLabel: 'Đổi mật khẩu',
            onSubmit: (current, next) async {
              await ref.read(apiProvider).post<void>('/api/auth/change-password', data: {'currentPassword': current, 'newPassword': next});
              await ref.read(authProvider.notifier).reloadMe();
            },
          ),
          const SizedBox(height: 8),
          TextButton(onPressed: () => ref.read(authProvider.notifier).logout(), child: const Text('Đăng xuất')),
        ],
      ),
    );
  }
}

/// Quên mật khẩu: tên đăng nhập → mã 6 số qua email → mật khẩu mới (BR-04).
class ForgotPasswordPage extends ConsumerStatefulWidget {
  const ForgotPasswordPage({super.key, this.username});
  final String? username;

  @override
  ConsumerState<ForgotPasswordPage> createState() => _ForgotPasswordPageState();
}

enum _Step { username, code, reset }

class _ForgotPasswordPageState extends ConsumerState<ForgotPasswordPage> {
  _Step _step = _Step.username;
  final _form = GlobalKey<FormState>();
  late final _username = TextEditingController(text: widget.username);
  final _code = TextEditingController();
  String? _resetToken;
  bool _busy = false;
  String? _error;
  int _cooldown = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    _username.dispose();
    _code.dispose();
    super.dispose();
  }

  ApiClient get _api => ref.read(apiProvider);

  void _startCooldown() {
    _timer?.cancel();
    setState(() => _cooldown = 60);
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (!mounted) return t.cancel();
      setState(() => _cooldown--);
      if (_cooldown <= 0) t.cancel();
    });
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _requestCode({bool resend = false}) => _run(() async {
    await _api.post<void>('/api/auth/forgot-password', data: {'username': _username.text.trim()});
    _startCooldown();
    if (resend) {
      if (mounted) toast(context, 'Yêu cầu gửi lại mã đã được tiếp nhận. Hãy kiểm tra email mới nhất.');
    } else {
      setState(() => _step = _Step.code);
    }
  });

  Future<void> _verify() => _run(() async {
    final j = await _api.post<Json>('/api/auth/verify-reset-code', data: {'username': _username.text.trim(), 'code': _code.text.trim()});
    setState(() {
      _resetToken = j.str('resetToken');
      _step = _Step.reset;
    });
  });

  /// Quay lại bước nhập tên đăng nhập; ở bước đầu thì về màn đăng nhập.
  void back() {
    if (_busy) return;
    if (_step == _Step.username) {
      context.pop();
    } else {
      setState(() {
        _error = null;
        _step = _Step.username;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    switch (_step) {
      case _Step.username:
        return AuthScaffold(
          title: 'Quên mật khẩu',
          subtitle: 'Nhập tên đăng nhập để nhận mã xác nhận qua email trong hồ sơ',
          onBack: back,
          child: Form(
            key: _form,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FieldLabel(
                  'Tên đăng nhập',
                  child: TextFormField(
                    controller: _username,
                    enabled: !_busy,
                    autocorrect: false,
                    textInputAction: TextInputAction.done,
                    onFieldSubmitted: (_) => _form.currentState!.validate() ? _requestCode() : null,
                    validator: (v) => (v ?? '').trim().isEmpty ? 'Nhập tên đăng nhập để nhận mã.' : null,
                  ),
                ),
                if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
                BusyButton(label: 'Gửi mã xác nhận', busy: _busy, onPressed: () => _form.currentState!.validate() ? _requestCode() : null),
              ],
            ),
          ),
        );
      case _Step.code:
        return AuthScaffold(
          title: 'Xác minh mã',
          subtitle:
              'Nếu tài khoản ${_username.text.trim()} hợp lệ và hồ sơ có email, mã xác nhận sẽ được gửi tới email đó. Hãy mở email mới nhất.',
          onBack: back,
          child: Form(
            key: _form,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FieldLabel(
                  'Mã xác nhận',
                  child: TextFormField(
                    controller: _code,
                    enabled: !_busy,
                    keyboardType: TextInputType.number,
                    maxLength: 6,
                    textAlign: TextAlign.center,
                    autofillHints: const [AutofillHints.oneTimeCode],
                    style: const TextStyle(fontSize: 24, letterSpacing: 10, fontWeight: FontWeight.w700),
                    decoration: const InputDecoration(counterText: '', hintText: '••••••'),
                    onFieldSubmitted: (_) => _form.currentState!.validate() ? _verify() : null,
                    validator: (v) {
                      final s = (v ?? '').trim();
                      if (s.isEmpty) return 'Nhập mã 6 chữ số trong email.';
                      if (!RegExp(r'^\d{6}$').hasMatch(s)) return 'Mã gồm đủ 6 chữ số.';
                      return null;
                    },
                  ),
                ),
                if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
                BusyButton(label: 'Xác minh', busy: _busy, onPressed: () => _form.currentState!.validate() ? _verify() : null),
                const SizedBox(height: 4),
                TextButton(
                  onPressed: _cooldown > 0 || _busy ? null : () => _requestCode(resend: true),
                  child: Text(_cooldown > 0 ? 'Gửi lại mã sau ${_cooldown}s' : 'Gửi lại mã'),
                ),
              ],
            ),
          ),
        );
      case _Step.reset:
        return AuthScaffold(
          title: 'Đặt lại mật khẩu',
          subtitle: 'Mã đã được xác minh. Đặt mật khẩu mới khác mật khẩu hiện tại.',
          onBack: back,
          child: _PasswordForm(
            askCurrent: false,
            submitLabel: 'Đặt lại mật khẩu',
            onSubmit: (_, next) async {
              await _api.post<void>(
                '/api/auth/reset-password',
                data: {'username': _username.text.trim(), 'resetToken': _resetToken, 'newPassword': next},
              );
              if (!context.mounted) return;
              toast(context, 'Đã đặt lại mật khẩu. Đăng nhập bằng mật khẩu mới.');
              context.go('/login', extra: _username.text.trim());
            },
          ),
        );
    }
  }
}

/// Đổi mật khẩu khi đang dùng (IAM-03): các thiết bị khác phải đăng nhập lại, máy này giữ phiên.
class ChangePasswordPage extends ConsumerWidget {
  const ChangePasswordPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return PageScaffold(
      title: 'Đổi mật khẩu',
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.screen),
        children: [
          AppCard(
            padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xl, AppSpacing.lg, AppSpacing.lg),
            child: _PasswordForm(
              askCurrent: true,
              submitLabel: 'Đổi mật khẩu',
              onSubmit: (current, next) async {
                await ref
                    .read(apiProvider)
                    .post<void>('/api/auth/change-password', data: {'currentPassword': current, 'newPassword': next});
                if (!context.mounted) return;
                toast(context, 'Đã đổi mật khẩu. Các thiết bị khác cần đăng nhập lại.');
                context.pop();
              },
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          const _Note(LucideIcons.keyRound, 'Ít nhất $minPasswordLength ký tự', 'Nên kết hợp chữ hoa, chữ thường, số và ký tự đặc biệt.'),
          const _Note(
            LucideIcons.laptopMinimal,
            'Thiết bị khác đăng xuất',
            'Sau khi đổi, các thiết bị khác phải đăng nhập lại; máy này vẫn giữ phiên.',
          ),
          const _Note(LucideIcons.mail, 'Quên mật khẩu?', 'Dùng "Quên mật khẩu" ở màn đăng nhập để nhận mã qua email trong hồ sơ cá nhân.'),
        ],
      ),
    );
  }
}

class _Note extends StatelessWidget {
  const _Note(this.icon, this.title, this.text);
  final IconData icon;
  final String title;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: AppColor.green700),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
                Text(text, style: const TextStyle(color: AppColor.text2, fontSize: 13, height: 1.4)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
