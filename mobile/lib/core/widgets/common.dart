import 'dart:math' as math;

import 'package:material_ui/material_ui.dart';

import '../labels.dart';
import '../theme/app_color.dart';
import '../theme/app_spacing.dart';

/// Thẻ nền trắng, bo 18, bóng rất nhẹ (Design_Mobile §2). Dùng Container thay vì Card.
class AppCard extends StatelessWidget {
  const AppCard({super.key, required this.child, this.onTap, this.padding = const EdgeInsets.all(AppSpacing.lg), this.color, this.border});

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final Color? color;
  final Color? border;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(AppRadius.card);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: color ?? AppColor.surface,
        borderRadius: radius,
        boxShadow: AppShadow.card,
        border: border == null ? null : Border.all(color: border!),
      ),
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          borderRadius: radius,
          onTap: onTap,
          child: Padding(padding: padding, child: child),
        ),
      ),
    );
  }
}

/// Viên trạng thái: chấm màu + chữ đậm cùng tông trên nền nhạt. Luôn có chữ, không chỉ dựa vào màu (§9.4).
class Pill extends StatelessWidget {
  const Pill(this.label, {super.key, this.group = BadgeGroup.neutral, this.dense = false});

  final String label;
  final BadgeGroup group;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    final c = group.color;
    return Container(
      padding: EdgeInsets.symmetric(horizontal: dense ? 8 : 10, vertical: dense ? 3 : 4),
      decoration: BoxDecoration(color: c.withValues(alpha: .12), borderRadius: BorderRadius.circular(AppRadius.pill)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(color: c, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(fontSize: dense ? 11.5 : 12.5, fontWeight: FontWeight.w600, color: c, height: 1.2),
          ),
        ],
      ),
    );
  }
}

class StatusPill extends StatelessWidget {
  const StatusPill(this.domain, this.value, {super.key, this.dense = false});

  final StatusDomain domain;
  final String value;
  final bool dense;

  @override
  Widget build(BuildContext context) => Pill(statusLabel(domain, value), group: statusGroup(domain, value), dense: dense);
}

/// Avatar chữ cái cuối của tên trên nền pastel theo tên (cùng màu với web).
class PersonAvatar extends StatelessWidget {
  const PersonAvatar(this.name, {super.key, this.size = 40, this.image});

  final String name;
  final double size;
  final Widget? image;

  @override
  Widget build(BuildContext context) {
    final parts = name.trim().split(RegExp(r'\s+'));
    final initial = parts.isEmpty || parts.last.isEmpty ? '?' : parts.last.characters.first.toUpperCase();
    final tone = accentFor(name);
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(color: tone.bg, shape: BoxShape.circle),
      child:
          image ??
          Text(
            initial,
            style: TextStyle(color: tone.fg, fontWeight: FontWeight.w700, fontSize: math.max(12, size * .38)),
          ),
    );
  }
}

/// Dòng người dạng thẻ: avatar, tên, một dòng phụ, phần cuối tùy chọn (Design_Mobile §5.1b).
class PersonRow extends StatelessWidget {
  const PersonRow({super.key, required this.name, this.subtitle, this.trailing, this.onTap, this.leading});

  final String name;
  final String? subtitle;
  final Widget? trailing;
  final Widget? leading;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: [
          leading ?? PersonAvatar(name),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500),
                ),
                if (subtitle != null)
                  Text(
                    subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 12, color: AppColor.text3),
                  ),
              ],
            ),
          ),
          if (trailing != null) ...[const SizedBox(width: 8), trailing!],
        ],
      ),
    );
  }
}

class SectionTitle extends StatelessWidget {
  const SectionTitle(this.title, {super.key, this.action, this.onAction});

  final String title;
  final String? action;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.xl, bottom: AppSpacing.sm),
      child: Row(
        children: [
          Expanded(
            child: Text(title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
          ),
          if (action != null) TextButton(onPressed: onAction, child: Text(action!)),
        ],
      ),
    );
  }
}

/// Ô số liệu trên trang chủ: nền đặc theo màu nhấn, tên ô chữ trắng góc trái, số trong vòng tròn viền trắng góc phải,
/// một dòng gợi ý bên dưới (Design_Mobile §2.2).
class StatTile extends StatelessWidget {
  const StatTile({super.key, required this.value, required this.label, required this.accent, this.onTap, this.hint});

  final String value;
  final String label;
  final Accent accent;
  final String? hint;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return _SolidTile(
      accent: accent,
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  label,
                  maxLines: 2,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Colors.white, height: 1.25),
                ),
              ),
              const SizedBox(width: 8),
              Container(
                constraints: const BoxConstraints(minWidth: 44),
                height: 44,
                padding: const EdgeInsets.symmetric(horizontal: 6),
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.white, width: 2.5),
                  borderRadius: BorderRadius.circular(AppRadius.pill),
                ),
                child: Text(
                  value,
                  style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: Colors.white),
                ),
              ),
            ],
          ),
          const Spacer(),
          if (hint != null)
            Text(
              hint!,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12, color: Colors.white.withValues(alpha: .92), fontWeight: FontWeight.w500),
            ),
        ],
      ),
    );
  }
}

/// Ô hành động (không có số): biểu tượng trong ô trắng mờ, tên và gợi ý chữ trắng, cùng khung với [StatTile].
class ActionTile extends StatelessWidget {
  const ActionTile({super.key, required this.icon, required this.label, required this.hint, required this.accent, required this.onTap});

  final IconData icon;
  final String label;
  final String hint;
  final Accent accent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return _SolidTile(
      accent: accent,
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(6),
            decoration: BoxDecoration(color: Colors.white.withValues(alpha: .24), borderRadius: BorderRadius.circular(12)),
            child: Icon(icon, size: 20, color: Colors.white),
          ),
          const Spacer(),
          Text(
            label,
            style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Colors.white),
          ),
          Text(
            hint,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 12, color: Colors.white.withValues(alpha: .92), fontWeight: FontWeight.w500),
          ),
        ],
      ),
    );
  }
}

class _SolidTile extends StatelessWidget {
  const _SolidTile({required this.accent, required this.child, this.onTap});
  final Accent accent;
  final Widget child;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(22);
    return Material(
      color: accent.solid,
      borderRadius: radius,
      child: InkWell(
        borderRadius: radius,
        onTap: onTap,
        child: SizedBox(
          height: 104,
          child: Padding(padding: const EdgeInsets.fromLTRB(16, 14, 14, 14), child: child),
        ),
      ),
    );
  }
}

/// Khung trắng bo tròn bọc lưới ô màu, như một bảng điều khiển nhỏ.
class TilePanel extends StatelessWidget {
  const TilePanel({super.key, required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) => AppCard(padding: const EdgeInsets.all(12), child: child);
}

/// Lưới 2 cột tự co theo nội dung (không ép tỉ lệ cố định để chữ dài không bị cắt ở 360dp).
class TwoColumnGrid extends StatelessWidget {
  const TwoColumnGrid({super.key, required this.children, this.gap = AppSpacing.md});

  final List<Widget> children;
  final double gap;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < children.length; i += 2) {
      rows.add(
        IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(child: children[i]),
              SizedBox(width: gap),
              Expanded(child: i + 1 < children.length ? children[i + 1] : const SizedBox()),
            ],
          ),
        ),
      );
      if (i + 2 < children.length) rows.add(SizedBox(height: gap));
    }
    return Column(children: rows);
  }
}

/// Băng thông báo nhạt trong trang (không dùng toast cho lỗi biểu mẫu).
class InlineAlert extends StatelessWidget {
  const InlineAlert(this.message, {super.key, this.tone = Accent.coral, this.icon});

  final String message;
  final Accent tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(color: tone.bg, borderRadius: BorderRadius.circular(AppRadius.md)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (icon != null) ...[Icon(icon, size: 18, color: tone.fg), const SizedBox(width: 10)],
          Expanded(
            child: Text(
              message,
              style: TextStyle(color: tone.fg, fontSize: 13.5, fontWeight: FontWeight.w500, height: 1.4),
            ),
          ),
        ],
      ),
    );
  }
}

/// Vòng tỉ lệ (chuyên cần): phần trăm ở giữa.
class RatioRing extends StatelessWidget {
  const RatioRing({super.key, required this.value, this.size = 64, this.color = AppColor.green600, this.label});

  final double value;
  final double size;
  final Color color;
  final String? label;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(
        painter: _RingPainter(value.clamp(0, 1), color),
        child: Center(
          child: Text(
            label ?? '${(value * 100).round()}%',
            style: TextStyle(fontSize: size * .24, fontWeight: FontWeight.w700, color: AppColor.ink),
          ),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter(this.v, this.color);
  final double v;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final stroke = size.width * .11;
    final rect = Offset(stroke / 2, stroke / 2) & Size(size.width - stroke, size.height - stroke);
    final base = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..color = AppColor.green100;
    canvas.drawArc(rect, 0, math.pi * 2, false, base);
    canvas.drawArc(
      rect,
      -math.pi / 2,
      math.pi * 2 * v,
      false,
      base
        ..color = color
        ..strokeCap = StrokeCap.round,
    );
  }

  @override
  bool shouldRepaint(_RingPainter old) => old.v != v || old.color != color;
}

/// Ô chữ cái môn học (thay icon): nền pastel theo tên môn.
class SubjectBadge extends StatelessWidget {
  const SubjectBadge(this.subject, {super.key, this.abbr, this.size = 44});

  final String subject;
  final String? abbr;
  final double size;

  @override
  Widget build(BuildContext context) {
    final tone = accentFor(subject);
    final text = (abbr != null && abbr!.isNotEmpty) ? abbr! : subject.characters.first.toUpperCase();
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(color: tone.bg, borderRadius: BorderRadius.circular(size * .32)),
      child: Text(
        text.length > 4 ? text.substring(0, 4) : text,
        style: TextStyle(color: tone.fg, fontWeight: FontWeight.w700, fontSize: text.length > 2 ? size * .26 : size * .34),
      ),
    );
  }
}

/// Hiện SnackBar ngắn.
void toast(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

/// Hộp xác nhận hai nút.
Future<bool> confirm(
  BuildContext context, {
  required String title,
  required String message,
  String ok = 'Đồng ý',
  bool danger = false,
}) async {
  final r = await showDialog<bool>(
    context: context,
    builder: (c) => AlertDialog(
      backgroundColor: AppColor.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.card)),
      title: Text(title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
      content: Text(message, style: const TextStyle(fontSize: 14.5, color: AppColor.text2, height: 1.45)),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(c, false),
          child: const Text('Hủy', style: TextStyle(color: AppColor.text2)),
        ),
        TextButton(
          onPressed: () => Navigator.pop(c, true),
          child: Text(ok, style: TextStyle(color: danger ? AppColor.danger : AppColor.green700)),
        ),
      ],
    ),
  );
  return r ?? false;
}
