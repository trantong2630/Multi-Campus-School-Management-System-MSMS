import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/hero_header.dart';
import '../../core/widgets/msms_ai.dart';
import '../../core/widgets/states.dart';
import '../par/children.dart';
import 'lrn_api.dart';

/// Bài tập (học sinh) / Kết quả của con (phụ huynh): theo dõi hạn nộp và mở kết quả.
/// Làm bài chỉ có trên web (spec §8.1), nên bài chưa nộp chỉ mở thẻ thông tin.
class AssignmentsPage extends ConsumerStatefulWidget {
  const AssignmentsPage({super.key, this.initialTab = 0});
  final int initialTab;

  @override
  ConsumerState<AssignmentsPage> createState() => _AssignmentsPageState();
}

class _AssignmentsPageState extends ConsumerState<AssignmentsPage> {
  late int _tab = widget.initialTab;

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final value = ref.watch(assignmentsProvider);
    final isTab = user.isStudent;
    final body = Column(
      children: [
        if (user.isParent)
          const Padding(
            padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
            child: Align(alignment: Alignment.centerLeft, child: ChildChip()),
          ),
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
          child: SizedBox(
            width: double.infinity,
            child: SegmentedButton<int>(
              showSelectedIcon: false,
              style: SegmentedButton.styleFrom(
                selectedBackgroundColor: AppColor.green100,
                selectedForegroundColor: AppColor.green800,
                side: const BorderSide(color: AppColor.border),
              ),
              segments: [
                ButtonSegment(value: 0, label: Text(user.isParent ? 'Chưa nộp' : 'Cần làm')),
                const ButtonSegment(value: 1, label: Text('Đã có kết quả')),
              ],
              selected: {_tab},
              onSelectionChanged: (s) => setState(() => _tab = s.first),
            ),
          ),
        ),
        Expanded(
          child: AsyncView(
            value: value,
            onRetry: () => ref.invalidate(assignmentsProvider),
            loading: const SkeletonList(count: 5, height: 92),
            data: (all) {
              final todo = all.where((a) => a.isTodo).toList()..sort((a, b) => a.dueAt.compareTo(b.dueAt));
              final done = all.where((a) => a.hasResult).toList()..sort((a, b) => b.dueAt.compareTo(a.dueAt));
              final items = _tab == 0 ? todo : done;
              return Refreshable(
                onRefresh: () => ref.refresh(assignmentsProvider.future),
                child: items.isEmpty
                    ? ListView(
                        children: [
                          AiEmptyState(
                            _tab == 0
                                ? (user.isParent ? 'Con không còn bài nào chưa nộp.' : 'Hết bài cần làm rồi. Nghỉ ngơi một chút nhé!')
                                : 'Chưa có bài nào có kết quả.',
                          ),
                        ],
                      )
                    : ListView.separated(
                        padding: EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, isTab ? 120 : 32),
                        itemCount: items.length,
                        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                        itemBuilder: (_, i) => AssignmentCard(items[i]),
                      ),
              );
            },
          ),
        ),
      ],
    );
    return Scaffold(
      appBar: AppBar(
        title: Text(user.isParent ? 'Kết quả của con' : 'Bài tập'),
        actions: [if (isTab) const Padding(padding: EdgeInsets.only(right: 8), child: NotificationBell(onLight: true))],
      ),
      body: body,
    );
  }
}

class AssignmentCard extends ConsumerWidget {
  const AssignmentCard(this.a, {super.key});
  final AssignmentItem a;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = DateTime.now();
    final urgent = a.isTodo && a.dueAt.difference(now).inHours < 24;
    return AppCard(
      onTap: () => openAssignment(context, ref, a),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          SubjectBadge(a.subjectName, size: 44),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  a.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 2),
                Text(a.subjectName, style: const TextStyle(fontSize: 12.5, color: AppColor.text2)),
                const SizedBox(height: 6),
                if (a.isTodo)
                  Row(
                    children: [
                      Icon(LucideIcons.clock, size: 14, color: urgent ? AppColor.coralFg : AppColor.text3),
                      const SizedBox(width: 4),
                      Flexible(
                        child: Text(
                          a.state == 'NotOpen' && a.openAt != null
                              ? 'Mở lúc ${Fmt.dateTime(a.openAt!)}'
                              : '${Fmt.remaining(a.dueAt)} · hạn ${Fmt.dateTime(a.dueAt)}',
                          style: TextStyle(
                            fontSize: 12.5,
                            color: urgent ? AppColor.coralFg : AppColor.text2,
                            fontWeight: urgent ? FontWeight.w600 : null,
                          ),
                        ),
                      ),
                    ],
                  )
                else
                  StatusPill(StatusDomain.attempt, a.state, dense: true),
              ],
            ),
          ),
          const SizedBox(width: 8),
          if (a.state == 'Submitted')
            Column(
              children: [
                Text(
                  Fmt.score(a.score),
                  style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColor.ink),
                ),
                const Text('điểm', style: TextStyle(fontSize: 11, color: AppColor.text3)),
              ],
            )
          else if (a.isTodo)
            StatusPill(StatusDomain.attempt, a.state, dense: true),
        ],
      ),
    );
  }
}

/// Bài đã nộp hoặc quá hạn mở kết quả; bài còn mở hiện thẻ thông tin và nhắc làm trên web.
void openAssignment(BuildContext context, WidgetRef ref, AssignmentItem a) {
  final user = ref.read(currentUserProvider);
  if (a.hasResult) {
    final childId = user.isParent ? ref.read(currentChildProvider).value?.studentId : null;
    context.push('/results/${a.id}${childId == null ? '' : '?studentId=$childId'}');
    return;
  }
  showModalBottomSheet<void>(
    context: context,
    builder: (c) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.xl, 0, AppSpacing.xl, AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                SubjectBadge(a.subjectName, size: 44),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(a.title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
                      Text('${a.subjectName} · Lớp ${a.className}', style: const TextStyle(color: AppColor.text2, fontSize: 13)),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            _InfoLine(LucideIcons.calendarClock, 'Hạn nộp', '${Fmt.dateTime(a.dueAt)} (${Fmt.remaining(a.dueAt)})'),
            if (a.openAt != null && a.state == 'NotOpen') _InfoLine(LucideIcons.lockOpen, 'Mở lúc', Fmt.dateTime(a.openAt!)),
            if (a.durationMinutes != null) _InfoLine(LucideIcons.timer, 'Thời lượng', '${a.durationMinutes} phút'),
            _InfoLine(LucideIcons.listChecks, 'Số câu', '${a.questionCount} câu'),
            const SizedBox(height: AppSpacing.md),
            MsmsAiBubble(
              size: 48,
              message: user.isParent
                  ? 'Con làm bài trên web. Có kết quả, phụ huynh xem ngay tại đây.'
                  : (a.state == 'InProgress' ? 'Làm bài trên web. Bài đang làm dở vẫn được giữ nguyên.' : 'Làm bài trên web.'),
            ),
          ],
        ),
      ),
    ),
  );
}

class _InfoLine extends StatelessWidget {
  const _InfoLine(this.icon, this.label, this.value);
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColor.green700),
          const SizedBox(width: 10),
          SizedBox(
            width: 84,
            child: Text(label, style: const TextStyle(color: AppColor.text3, fontSize: 13.5)),
          ),
          Expanded(
            child: Text(value, style: const TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
          ),
        ],
      ),
    );
  }
}
