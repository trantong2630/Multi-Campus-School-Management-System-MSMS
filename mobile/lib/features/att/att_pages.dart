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
import '../../core/widgets/states.dart';
import '../par/children.dart';
import 'att_api.dart';

/// Thẻ một tiết kèm tiến độ điểm danh (dùng ở danh sách tiết hôm nay của giáo viên).
class AttSessionCard extends StatelessWidget {
  const AttSessionCard(this.s, {super.key, this.onTap});
  final AttSession s;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final cancelled = s.status == 'Cancelled';
    final progress = s.rosterCount == 0 ? 0.0 : s.markedCount / s.rosterCount;
    return AppCard(
      onTap: cancelled ? null : onTap,
      color: cancelled ? AppColor.neutralBg : null,
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          SizedBox(
            width: 52,
            child: Column(
              children: [
                Text(s.start, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                Text('Tiết ${s.periodNo}', style: const TextStyle(fontSize: 11.5, color: AppColor.text3)),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${s.subjectName} · ${s.className}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, decoration: cancelled ? TextDecoration.lineThrough : null),
                ),
                const SizedBox(height: 6),
                if (cancelled)
                  const Text('Tiết đã hủy', style: TextStyle(fontSize: 12.5, color: AppColor.text3))
                else
                  Row(
                    children: [
                      Expanded(
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(4),
                          child: LinearProgressIndicator(
                            value: progress,
                            minHeight: 6,
                            backgroundColor: AppColor.neutralBg,
                            color: s.done ? AppColor.green600 : AppColor.amber,
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Text('${s.markedCount}/${s.rosterCount}', style: const TextStyle(fontSize: 12.5, color: AppColor.text2)),
                    ],
                  ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          if (!cancelled)
            s.done
                ? const Pill('Đã xong', group: BadgeGroup.active, dense: true)
                : (s.canWrite
                      ? const Pill('Chưa xong', group: BadgeGroup.pending, dense: true)
                      : const Pill('Đã khóa', group: BadgeGroup.neutral, dense: true)),
        ],
      ),
    );
  }
}

/// Điểm danh hôm nay: các tiết của giáo viên, bấm để điểm danh.
class TodayAttendancePage extends ConsumerWidget {
  const TodayAttendancePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(todayAttendanceProvider);
    return PageScaffold(
      title: 'Điểm danh hôm nay',
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(todayAttendanceProvider),
        data: (list) => Refreshable(
          onRefresh: () => ref.refresh(todayAttendanceProvider.future),
          child: list.isEmpty
              ? ListView(children: const [EmptyState('Hôm nay không có tiết dạy nào.')])
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 32),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                  itemBuilder: (_, i) => AttSessionCard(list[i], onTap: () => context.push('/attendance/${list[i].id}')),
                ),
        ),
      ),
    );
  }
}

/// Chuyên cần của học sinh (học sinh tự xem, phụ huynh xem của con): bốn con số và danh sách theo ngày.
class StudentAttendancePage extends ConsumerStatefulWidget {
  const StudentAttendancePage({super.key});

  @override
  ConsumerState<StudentAttendancePage> createState() => _StudentAttendancePageState();
}

class _StudentAttendancePageState extends ConsumerState<StudentAttendancePage> {
  int _range = 0; // 0: tuần này, 1: tháng này, 2: học kỳ gần đây (90 ngày)

  AttRange get _r {
    final today = dateOnly(DateTime.now());
    return switch (_range) {
      0 => (from: weekStart(today), to: today),
      1 => (from: DateTime(today.year, today.month, 1), to: today),
      _ => (from: today.subtract(const Duration(days: 90)), to: today),
    };
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final r = _r;
    final value = ref.watch(myAttendanceProvider(r));
    return PageScaffold(
      title: 'Chuyên cần',
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
            child: Row(
              children: [
                if (user.isParent) ...[const ChildChip(), const Spacer()],
                Wrap(
                  spacing: 8,
                  children: [
                    for (final (i, l) in ['Tuần này', 'Tháng này', '3 tháng'].indexed)
                      ChoiceChip(
                        label: Text(l),
                        selected: _range == i,
                        showCheckmark: false,
                        selectedColor: AppColor.green100,
                        labelStyle: TextStyle(color: _range == i ? AppColor.green800 : AppColor.text2, fontWeight: FontWeight.w600),
                        side: const BorderSide(color: AppColor.border),
                        onSelected: (_) => setState(() => _range = i),
                      ),
                  ],
                ),
              ],
            ),
          ),
          Expanded(
            child: AsyncView(
              value: value,
              onRetry: () => ref.invalidate(myAttendanceProvider(r)),
              data: (data) {
                if (data == null) return const EmptyState('Chưa có học sinh nào được liên kết với tài khoản.');
                final c = data.counts;
                final absentItems = data.items.where((i) => i.label != 'Present').toList();
                return Refreshable(
                  onRefresh: () => ref.refresh(myAttendanceProvider(r).future),
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 40),
                    children: [
                      AppCard(
                        child: Row(
                          children: [
                            RatioRing(value: c.total == 0 ? 0 : c.present / c.total, size: 72, label: c.total == 0 ? '—' : null),
                            const SizedBox(width: AppSpacing.lg),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    c.total == 0 ? 'Chưa có tiết nào đã diễn ra' : '${c.present}/${c.total} tiết có mặt',
                                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    '${Fmt.shortDate(r.from)} – ${Fmt.date(r.to)}',
                                    style: const TextStyle(fontSize: 12.5, color: AppColor.text3),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      TwoColumnGrid(
                        children: [
                          StatTile(value: '${c.late}', label: 'Muộn', accent: Accent.sky),
                          StatTile(value: '${c.excused}', label: 'Vắng có phép', accent: Accent.teal),
                          StatTile(value: '${c.unexcused}', label: 'Vắng không phép', accent: Accent.coral),
                          StatTile(value: '${c.notMarked}', label: 'Chưa điểm danh', accent: Accent.violet),
                        ],
                      ),
                      SectionTitle(absentItems.isEmpty ? 'Các tiết' : 'Tiết vắng, muộn hoặc chưa điểm danh'),
                      if (data.items.isEmpty)
                        const EmptyState('Không có tiết nào trong khoảng này.', compact: true)
                      else if (absentItems.isEmpty)
                        const AiEmptyState('Có mặt đủ tất cả các tiết. Giữ vững nhé!')
                      else
                        for (final it in absentItems)
                          Padding(
                            padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                            child: AppCard(
                              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                              child: Row(
                                children: [
                                  SubjectBadge(it.subjectName, size: 38),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(it.subjectName, style: const TextStyle(fontWeight: FontWeight.w600)),
                                        Text(
                                          '${Fmt.weekday(it.date)} ${Fmt.shortDate(it.date)} · ${sessionOfDayLabel(it.sessionOfDay)} tiết ${it.periodNo}'
                                          '${it.note == null ? '' : ' · ${it.note}'}',
                                          maxLines: 2,
                                          style: const TextStyle(fontSize: 12.5, color: AppColor.text2),
                                        ),
                                      ],
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  StatusPill(StatusDomain.attendance, it.label, dense: true),
                                ],
                              ),
                            ),
                          ),
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// Chuyên cần lớp chủ nhiệm theo ngày: mỗi tiết kèm số vắng và tên học sinh vắng.
class HomeroomAttendancePage extends ConsumerStatefulWidget {
  const HomeroomAttendancePage({super.key});

  @override
  ConsumerState<HomeroomAttendancePage> createState() => _HomeroomAttendancePageState();
}

class _HomeroomAttendancePageState extends ConsumerState<HomeroomAttendancePage> {
  DateTime _day = dateOnly(DateTime.now());
  int? _classId;

  @override
  Widget build(BuildContext context) {
    final classes = ref.watch(homeroomClassesProvider);
    return PageScaffold(
      title: 'Chuyên cần lớp chủ nhiệm',
      body: AsyncView(
        value: classes,
        onRetry: () => ref.invalidate(homeroomClassesProvider),
        data: (list) {
          if (list.isEmpty) return const EmptyState('Hiện không có lớp chủ nhiệm.');
          final classId = _classId ?? list.first.classId;
          final key = (classId: classId, day: _day);
          final day = ref.watch(classDayProvider(key));
          return Column(
            children: [
              if (list.length > 1)
                SizedBox(
                  height: 48,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(horizontal: AppSpacing.screen),
                    children: [
                      for (final c in list)
                        Padding(
                          padding: const EdgeInsets.only(right: 8),
                          child: ChoiceChip(
                            label: Text('Lớp ${c.name}'),
                            selected: c.classId == classId,
                            onSelected: (_) => setState(() => _classId = c.classId),
                          ),
                        ),
                    ],
                  ),
                ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4),
                child: Row(
                  children: [
                    IconButton(
                      tooltip: 'Ngày trước',
                      onPressed: () => setState(() => _day = _day.subtract(const Duration(days: 1))),
                      icon: const Icon(LucideIcons.chevronLeft),
                    ),
                    Expanded(
                      child: Text(
                        '${Fmt.longDate(_day)}${isSameDay(_day, DateTime.now()) ? ' · Hôm nay' : ''}',
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                    IconButton(
                      tooltip: 'Ngày sau',
                      onPressed: isSameDay(_day, DateTime.now()) ? null : () => setState(() => _day = _day.add(const Duration(days: 1))),
                      icon: const Icon(LucideIcons.chevronRight),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: AsyncView(
                  value: day,
                  onRetry: () => ref.invalidate(classDayProvider(key)),
                  data: (d) {
                    final sessions = d.sessions.where((s) => s.status != 'Cancelled').toList()..sort((a, b) => a.start.compareTo(b.start));
                    if (sessions.isEmpty) {
                      return ListView(children: const [EmptyState('Lớp không có tiết nào trong ngày này.')]);
                    }
                    return Refreshable(
                      onRefresh: () => ref.refresh(classDayProvider(key).future),
                      child: ListView.separated(
                        padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 32),
                        itemCount: sessions.length,
                        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                        itemBuilder: (_, i) {
                          final s = sessions[i];
                          final missing = d.students
                              .where((st) => st.labels.containsKey(s.id) && st.labels[s.id] != 'Present')
                              .map((st) => (name: st.name, label: st.labels[s.id]!))
                              .toList();
                          return AppCard(
                            onTap: () => context.push('/attendance/${s.id}'),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        '${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo} · ${s.subjectName}',
                                        style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                                      ),
                                    ),
                                    Text('${s.markedCount}/${s.rosterCount}', style: const TextStyle(color: AppColor.text2, fontSize: 13)),
                                  ],
                                ),
                                if (s.teacherName != null)
                                  Text(s.teacherName!, style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                                const SizedBox(height: 8),
                                if (s.markedCount == 0)
                                  const Text('Chưa điểm danh', style: TextStyle(color: AppColor.amberInk, fontSize: 13))
                                else if (missing.isEmpty)
                                  const Text(
                                    'Có mặt đủ',
                                    style: TextStyle(color: AppColor.green700, fontSize: 13, fontWeight: FontWeight.w600),
                                  )
                                else
                                  Wrap(
                                    spacing: 6,
                                    runSpacing: 6,
                                    children: [
                                      for (final m in missing)
                                        Pill(
                                          '${m.name.split(' ').last} · ${statusLabel(StatusDomain.attendance, m.label)}',
                                          group: statusGroup(StatusDomain.attendance, m.label),
                                          dense: true,
                                        ),
                                    ],
                                  ),
                              ],
                            ),
                          );
                        },
                      ),
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
