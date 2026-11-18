import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';

class Child {
  Child.fromJson(Json j)
    : studentId = j.integer('studentId'),
      studentCode = j.str('studentCode'),
      fullName = j.str('fullName'),
      className = j.strOrNull('className'),
      classId = j.intOrNull('classId');

  final int studentId;
  final String studentCode;
  final String fullName;
  final String? className;
  final int? classId;

  String get givenName => fullName.trim().split(RegExp(r'\s+')).last;
}

/// Các con được liên kết của phụ huynh đang đăng nhập.
final myChildrenProvider = FutureProvider.autoDispose<List<Child>>((ref) async {
  ref.watch(currentUserProvider);
  final list = await ref.read(apiProvider).get<List<dynamic>>('/api/par/leave-requests/my-children');
  return list.map((e) => Child.fromJson(e as Json)).toList();
});

/// Con đang chọn áp dụng cho cả app; không chọn lại ở từng màn (Design_Mobile §3.3).
final selectedChildIdProvider = NotifierProvider<SelectedChild, int?>(SelectedChild.new);

class SelectedChild extends Notifier<int?> {
  @override
  int? build() {
    ref.watch(currentUserProvider);
    return null;
  }

  void select(int id) => state = id;
}

/// Con hiện hành: con đã chọn, hoặc con đầu tiên nếu chưa chọn.
final currentChildProvider = FutureProvider.autoDispose<Child?>((ref) async {
  final children = await ref.watch(myChildrenProvider.future);
  if (children.isEmpty) return null;
  final id = ref.watch(selectedChildIdProvider);
  return children.firstWhere((c) => c.studentId == id, orElse: () => children.first);
});

/// Chip avatar + tên con ở header; bấm mở bottom sheet chọn con.
class ChildChip extends ConsumerWidget {
  const ChildChip({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final child = ref.watch(currentChildProvider).value;
    final children = ref.watch(myChildrenProvider).value ?? const [];
    if (child == null) return const SizedBox(height: 48);
    final many = children.length > 1;
    return Material(
      color: AppColor.surface.withValues(alpha: .85),
      borderRadius: BorderRadius.circular(AppRadius.pill),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        onTap: many ? () => pickChild(context, ref) : null,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(4, 4, 14, 4),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              PersonAvatar(child.fullName, size: 36),
              const SizedBox(width: 8),
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 170),
                child: Text(
                  child.fullName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w600, color: AppColor.green800, fontSize: 14),
                ),
              ),
              if (many) ...[const SizedBox(width: 4), const Icon(LucideIcons.chevronDown, size: 18, color: AppColor.green800)],
            ],
          ),
        ),
      ),
    );
  }
}

Future<void> pickChild(BuildContext context, WidgetRef ref) async {
  final children = await ref.read(myChildrenProvider.future);
  final current = ref.read(currentChildProvider).value;
  if (!context.mounted) return;
  final id = await showModalBottomSheet<int>(
    context: context,
    builder: (c) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Padding(
              padding: EdgeInsets.only(bottom: AppSpacing.md),
              child: Text('Chọn con', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            ),
            for (final ch in children)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: PersonRow(
                  name: ch.fullName,
                  subtitle: [ch.studentCode, if (ch.className != null) 'Lớp ${ch.className}'].join(' · '),
                  trailing: ch.studentId == current?.studentId ? const Icon(LucideIcons.circleCheck, color: AppColor.green600) : null,
                  onTap: () => Navigator.pop(c, ch.studentId),
                ),
              ),
          ],
        ),
      ),
    ),
  );
  if (id != null) ref.read(selectedChildIdProvider.notifier).select(id);
}
