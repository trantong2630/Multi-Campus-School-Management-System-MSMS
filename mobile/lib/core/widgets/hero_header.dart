import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../features/com/notification_api.dart';
import '../theme/app_color.dart';
import '../theme/app_spacing.dart';
import 'brand.dart';

/// Header trang chủ: gradient xanh nhạt, đồi mờ phía dưới, bo tròn đáy, chữ green800 (Design_Mobile §2.1).
class HeroHeader extends StatelessWidget {
  const HeroHeader({super.key, required this.title, this.subtitle, this.leading, this.actions = const [], this.footer});

  final String title;
  final String? subtitle;

  /// Thay chỗ logo ở góc trái (vd. chip chọn con của phụ huynh).
  final Widget? leading;
  final List<HeaderPill> actions;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return ClipRRect(
      borderRadius: const BorderRadius.vertical(bottom: Radius.circular(AppRadius.sheet)),
      child: DecoratedBox(
        decoration: const BoxDecoration(gradient: AppGradient.header),
        child: Stack(
          children: [
            const Positioned.fill(child: LandscapeArt(alignBottom: true, opacity: .2)),
            Padding(
              padding: EdgeInsets.fromLTRB(AppSpacing.screen, top + 10, AppSpacing.screen, 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Align(alignment: Alignment.centerLeft, child: leading ?? const BrandLogo(size: 24)),
                      ),
                      const NotificationBell(),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Text(
                    title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColor.green800, height: 1.2),
                  ),
                  if (subtitle != null) ...[
                    const SizedBox(height: 4),
                    Text(subtitle!, style: TextStyle(fontSize: 14, color: AppColor.green800.withValues(alpha: .85))),
                  ],
                  if (actions.isNotEmpty) ...[const SizedBox(height: 14), Wrap(spacing: 10, runSpacing: 8, children: actions)],
                  if (footer != null) ...[const SizedBox(height: 14), footer!],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Nút trắng dạng viên trong header.
class HeaderPill extends StatelessWidget {
  const HeaderPill({super.key, required this.label, required this.icon, required this.onTap});

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColor.surface.withValues(alpha: .92),
      borderRadius: BorderRadius.circular(AppRadius.pill),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        onTap: onTap,
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 18, color: AppColor.green700),
                const SizedBox(width: 8),
                Text(
                  label,
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColor.green800),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Chuông thông báo ở góc phải header, giống web (thông báo không phải một tab).
class NotificationBell extends ConsumerWidget {
  const NotificationBell({super.key, this.onLight = false});

  final bool onLight;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadNotificationsProvider).value ?? 0;
    return Semantics(
      button: true,
      label: unread > 0 ? 'Thông báo, $unread chưa đọc' : 'Thông báo',
      child: Material(
        color: AppColor.surface.withValues(alpha: onLight ? 1 : .7),
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: () => context.push('/notifications'),
          child: SizedBox(
            width: 48,
            height: 48,
            child: Stack(
              alignment: Alignment.center,
              children: [
                const Icon(LucideIcons.bell, size: 22, color: AppColor.green800),
                if (unread > 0)
                  Positioned(
                    top: 8,
                    right: 7,
                    child: Container(
                      constraints: const BoxConstraints(minWidth: 18),
                      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                      decoration: BoxDecoration(
                        color: AppColor.coralFg,
                        borderRadius: BorderRadius.circular(AppRadius.pill),
                        border: Border.all(color: AppColor.surface, width: 1.5),
                      ),
                      child: Text(
                        unread > 99 ? '99+' : '$unread',
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.white, fontSize: 10.5, fontWeight: FontWeight.w700),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
