import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/http/api_client.dart';
import '../../core/theme/app_color.dart';
import '../../core/widgets/auth_scaffold.dart';
import '../../core/widgets/common.dart';

/// Lời chào theo buổi trong ngày, như web.
String greeting() {
  final h = DateTime.now().hour;
  if (h < 12) return 'Chào buổi sáng';
  if (h < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key, this.username});

  /// Trang quên mật khẩu gửi về để không phải gõ lại.
  final String? username;

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _form = GlobalKey<FormState>();
  late final _username = TextEditingController(text: widget.username);
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;
  final _title = greeting();

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
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
      await ref.read(authProvider.notifier).login(_username.text, _password.text);
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final notice = ref.watch(authProvider).notice;
    return AuthScaffold(
      title: _title,
      subtitle: 'Đăng nhập để tiếp tục',
      child: Form(
        key: _form,
        onChanged: () {
          if (_error != null) setState(() => _error = null);
        },
        child: AutofillGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (notice != null && _error == null) ...[
                InlineAlert(notice, tone: Accent.amber, icon: LucideIcons.info),
                const SizedBox(height: 16),
              ],
              FieldLabel(
                'Tên đăng nhập',
                child: TextFormField(
                  controller: _username,
                  enabled: !_busy,
                  autofillHints: const [AutofillHints.username],
                  textInputAction: TextInputAction.next,
                  autocorrect: false,
                  decoration: const InputDecoration(hintText: 'vd. kientdhs186194'),
                  validator: (v) => (v ?? '').trim().isEmpty ? 'Nhập tên đăng nhập để tiếp tục.' : null,
                ),
              ),
              PasswordField(
                controller: _password,
                label: 'Mật khẩu',
                hint: 'Nhập mật khẩu',
                enabled: !_busy,
                autofill: const [AutofillHints.password],
                action: TextInputAction.done,
                onSubmitted: (_) => _submit(),
                validator: (v) => (v ?? '').isEmpty ? 'Nhập mật khẩu để tiếp tục.' : null,
              ),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: _busy ? null : () => context.push('/forgot-password', extra: _username.text.trim()),
                  child: const Text('Quên mật khẩu?'),
                ),
              ),
              const SizedBox(height: 8),
              if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
              BusyButton(label: 'Đăng nhập', busy: _busy, onPressed: _submit),
            ],
          ),
        ),
      ),
    );
  }
}
