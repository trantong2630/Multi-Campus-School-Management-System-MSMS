import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../theme/app_color.dart';
import '../theme/app_spacing.dart';
import 'brand.dart';

/// Khung chung cho mọi màn chưa đăng nhập, cùng tông với web (Design_Web §6.0): nửa trên là tranh phong cảnh
/// với wordmark trắng, nửa dưới là thẻ form trắng chồm lên mép tranh và trồi lên khi mở màn.
class AuthScaffold extends StatefulWidget {
  const AuthScaffold({super.key, required this.title, this.subtitle, required this.child, this.onBack});

  final String title;
  final String? subtitle;
  final Widget child;
  final VoidCallback? onBack;

  @override
  State<AuthScaffold> createState() => _AuthScaffoldState();
}

class _AuthScaffoldState extends State<AuthScaffold> with SingleTickerProviderStateMixin {
  late final AnimationController _rise = AnimationController(vsync: this, duration: const Duration(milliseconds: 480))..forward();

  @override
  void dispose() {
    _rise.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final artHeight = (media.size.height * .36).clamp(220.0, 340.0);
    final curve = CurvedAnimation(parent: _rise, curve: const Cubic(.2, .8, .2, 1));
    return Scaffold(
      backgroundColor: AppColor.surface,
      body: SingleChildScrollView(
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: media.size.height),
          child: Column(
            children: [
              SizedBox(
                height: artHeight,
                child: Stack(
                  children: [
                    const Positioned.fill(child: LandscapeArt()),
                    // Logo nằm trên vùng trời, phía trên đồi và cây.
                    Positioned(
                      left: 0,
                      right: 0,
                      top: media.padding.top + (artHeight - media.padding.top) * .26,
                      child: const Center(child: BrandLogo(tone: LogoTone.white, size: 64)),
                    ),
                    if (widget.onBack != null)
                      Positioned(
                        top: media.padding.top + 4,
                        left: 4,
                        child: IconButton(
                          onPressed: widget.onBack,
                          tooltip: 'Quay lại',
                          icon: const Icon(LucideIcons.arrowLeft, color: AppColor.green800),
                        ),
                      ),
                  ],
                ),
              ),
              Transform.translate(
                offset: const Offset(0, -28),
                child: AnimatedBuilder(
                  animation: curve,
                  builder: (_, child) => Opacity(
                    opacity: curve.value,
                    child: Transform.translate(offset: Offset(0, 24 * (1 - curve.value)), child: child),
                  ),
                  child: Container(
                    width: double.infinity,
                    decoration: const BoxDecoration(
                      color: AppColor.surface,
                      borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
                    ),
                    padding: const EdgeInsets.fromLTRB(24, 28, 24, 16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          widget.title,
                          style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w700, color: AppColor.text1),
                        ),
                        if (widget.subtitle != null) ...[
                          const SizedBox(height: 6),
                          Text(widget.subtitle!, style: const TextStyle(fontSize: 14.5, color: AppColor.text2, height: 1.45)),
                        ],
                        const SizedBox(height: AppSpacing.xl),
                        widget.child,
                      ],
                    ),
                  ),
                ),
              ),
              Padding(
                padding: EdgeInsets.only(bottom: media.padding.bottom + 16),
                child: Text(
                  '© ${DateTime.now().year} · Hệ thống quản lý trường học đa phân hiệu',
                  style: const TextStyle(fontSize: 12, color: AppColor.text3),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Ô mật khẩu có nút hiện/ẩn.
class PasswordField extends StatefulWidget {
  const PasswordField({
    super.key,
    required this.controller,
    required this.label,
    this.hint,
    this.validator,
    this.autofill,
    this.onSubmitted,
    this.enabled = true,
    this.action = TextInputAction.next,
  });

  final TextEditingController controller;
  final String label;
  final String? hint;
  final String? Function(String?)? validator;
  final Iterable<String>? autofill;
  final void Function(String)? onSubmitted;
  final bool enabled;
  final TextInputAction action;

  @override
  State<PasswordField> createState() => _PasswordFieldState();
}

class _PasswordFieldState extends State<PasswordField> {
  bool _hidden = true;

  @override
  Widget build(BuildContext context) {
    return FieldLabel(
      widget.label,
      child: TextFormField(
        controller: widget.controller,
        obscureText: _hidden,
        enabled: widget.enabled,
        autofillHints: widget.autofill,
        textInputAction: widget.action,
        onFieldSubmitted: widget.onSubmitted,
        validator: widget.validator,
        decoration: InputDecoration(
          hintText: widget.hint,
          suffixIcon: IconButton(
            tooltip: _hidden ? 'Hiện mật khẩu' : 'Ẩn mật khẩu',
            onPressed: () => setState(() => _hidden = !_hidden),
            icon: Icon(_hidden ? LucideIcons.eye : LucideIcons.eyeOff, size: 20, color: AppColor.text3),
          ),
        ),
      ),
    );
  }
}

/// Nhãn đặt trên ô nhập.
class FieldLabel extends StatelessWidget {
  const FieldLabel(this.label, {super.key, required this.child});
  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: AppColor.text2),
          ),
          const SizedBox(height: 6),
          child,
        ],
      ),
    );
  }
}

/// Nút chính có trạng thái đang gửi.
class BusyButton extends StatelessWidget {
  const BusyButton({super.key, required this.label, required this.busy, required this.onPressed});

  final String label;
  final bool busy;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: busy ? null : onPressed,
      style: busy ? FilledButton.styleFrom(disabledBackgroundColor: AppColor.green500) : null,
      child: busy
          ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2.4, color: Colors.white))
          : Text(label),
    );
  }
}
