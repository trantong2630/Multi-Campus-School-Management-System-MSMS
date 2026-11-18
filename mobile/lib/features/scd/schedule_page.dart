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
import '../../core/widgets/states.dart';
import '../par/children.dart';
import 'scd_api.dart';

/// Lịch dạy (giáo viên) / Lịch học (học sinh, phụ huynh): dải 7 ngày cuộn ngang, dòng thời gian bên dưới (Design_Mobile §5.2).
class SchedulePage extends ConsumerStatefulWidget {
  const SchedulePage({super.key, this.initialDate});
  final DateTime? initialDate;

  @override
  ConsumerState<SchedulePage> createState() => _SchedulePageState();
}

class _SchedulePageState extends ConsumerState<SchedulePage> {
  late DateTime _day = dateOnly(widget.initialDate ?? DateTime.now());

  DateTime get _monday => weekStart(_day);

  void _shiftWeek(int weeks) => setState(() => _day = _day.add(Duration(days: 7 * weeks)));

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final week = ref.watch(weekScheduleProvider(_monday));
    final today = dateOnly(DateTime.now());
    final subtitle = user.isTeacher ? null : ref.watch(myClassProvider).value;
    return Scaffold(
      appBar: AppBar(
        title: Text(user.isTeacher ? 'Lịch dạy' : 'Lịch học'),
        actions: [
          if (!isSameDay(_day, today)) TextButton(onPressed: () => setState(() => _day = today), child: const Text('Hôm nay')),
          const Padding(padding: EdgeInsets.only(right: 8), child: NotificationBell(onLight: true)),
        ],
      ),
      body: Column(
        children: [
          if (user.isParent)
            const Padding(
              padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
              child: Align(alignment: Alignment.centerLeft, child: ChildChip()),
            ),
          if (subtitle != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text('Lớp $subtitle', style: const TextStyle(color: AppColor.text2, fontSize: 13.5)),
              ),
            ),
          _WeekStrip(
            monday: _monday,
            selected: _day,
            sessions: week.value ?? const [],
            onSelect: (d) => setState(() => _day = d),
            onShift: _shiftWeek,
          ),
          const SizedBox(height: AppSpacing.sm),
          Expanded(
            child: AsyncView(
              value: week,
              onRetry: () => ref.invalidate(weekScheduleProvider(_monday)),
              loading: const SkeletonList(count: 4, height: 84),
              data: (all) {
                final items = all.where((s) => isSameDay(s.date, _day)).toList();
                return Refreshable(
                  onRefresh: () => ref.refresh(weekScheduleProvider(_monday).future),
                  child: items.isEmpty
                      ? ListView(
                          children: [
                            EmptyState(
                              _day.weekday == DateTime.sunday
                                  ? 'Chủ nhật, không có tiết nào.'
                                  : user.isTeacher
                                  ? 'Không có tiết dạy trong ngày này.'
                                  : 'Không có tiết học trong ngày này.',
                            ),
                          ],
                        )
                      : ListView.builder(
                          padding: const EdgeInsets.fromLTRB(AppSpacing.screen, AppSpacing.sm, AppSpacing.screen, 120),
                          itemCount: items.length,
                          itemBuilder: (_, i) => _TimelineRow(
                            session: items[i],
                            showTeacher: !user.isTeacher,
                            isLast: i == items.length - 1,
                            onTap: user.isTeacher && items[i].status == 'Scheduled'
                                ? () => context.push('/attendance/${items[i].id}')
                                : null,
                          ),
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

class _WeekStrip extends StatelessWidget {
  const _WeekStrip({required this.monday, required this.selected, required this.sessions, required this.onSelect, required this.onShift});

  final DateTime monday;
  final DateTime selected;
  final List<Session> sessions;
  final ValueChanged<DateTime> onSelect;
  final ValueChanged<int> onShift;

  @override
  Widget build(BuildContext context) {
    final today = dateOnly(DateTime.now());
    final sunday = monday.add(const Duration(days: 6));
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4),
          child: Row(
            children: [
              IconButton(tooltip: 'Tuần trước', onPressed: () => onShift(-1), icon: const Icon(LucideIcons.chevronLeft, size: 20)),
              Expanded(
                child: Text(
                  '${Fmt.shortDate(monday)} – ${Fmt.date(sunday)}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
                ),
              ),
              IconButton(tooltip: 'Tuần sau', onPressed: () => onShift(1), icon: const Icon(LucideIcons.chevronRight, size: 20)),
            ],
          ),
        ),
        SizedBox(
          height: 76,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.screen),
            itemCount: 7,
            separatorBuilder: (_, _) => const SizedBox(width: 8),
            itemBuilder: (_, i) {
              final d = monday.add(Duration(days: i));
              final isToday = isSameDay(d, today);
              final isSel = isSameDay(d, selected);
              final count = sessions.where((s) => isSameDay(s.date, d) && !s.isCancelled).length;
              final fg = isToday ? Colors.white : AppColor.text1;
              return Semantics(
                selected: isSel,
                button: true,
                label: '${Fmt.weekday(d)} ${Fmt.date(d)}, $count tiết',
                child: GestureDetector(
                  onTap: () => onSelect(d),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    width: 48,
                    decoration: BoxDecoration(
                      color: isToday ? AppColor.green600 : AppColor.surface,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: isSel ? AppColor.green700 : AppColor.border, width: isSel ? 2 : 1),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Text(Fmt.weekdayShort(d), style: TextStyle(fontSize: 12, color: isToday ? Colors.white : AppColor.text3)),
                        const SizedBox(height: 2),
                        Text(
                          '${d.day}',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: fg),
                        ),
                        const SizedBox(height: 3),
                        Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            for (var k = 0; k < count.clamp(0, 4); k++)
                              Container(
                                margin: const EdgeInsets.symmetric(horizontal: 1),
                                width: 4,
                                height: 4,
                                decoration: BoxDecoration(color: isToday ? Colors.white : AppColor.green500, shape: BoxShape.circle),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _TimelineRow extends StatelessWidget {
  const _TimelineRow({required this.session, required this.showTeacher, required this.isLast, this.onTap});

  final Session session;
  final bool showTeacher;
  final bool isLast;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final s = session;
    final now = DateTime.now();
    final live = !s.isCancelled && now.isAfter(s.startsAt) && now.isBefore(s.endsAt);
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            width: 52,
            child: Column(
              children: [
                Text(
                  s.start,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: live ? AppColor.amberInk : AppColor.text1),
                ),
                Text(s.end, style: const TextStyle(fontSize: 12, color: AppColor.text3)),
                const SizedBox(height: 4),
                if (!isLast) const Expanded(child: VerticalDivider(width: 1, thickness: 1, color: AppColor.border)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.md),
              child: SessionCard(s, showTeacher: showTeacher, live: live, onTap: onTap),
            ),
          ),
        ],
      ),
    );
  }
}

/// Thẻ tiết: viền trái 4px theo trạng thái; Đã hủy gạch ngang nền xám; Chờ xử lý tông hổ phách.
class SessionCard extends StatelessWidget {
  const SessionCard(this.s, {super.key, this.showTeacher = false, this.live = false, this.onTap, this.trailing});

  final Session s;
  final bool showTeacher;
  final bool live;
  final VoidCallback? onTap;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final edge = statusGroup(StatusDomain.session, s.status).color;
    final bg = s.isCancelled ? AppColor.neutralBg : (s.isPending ? AppColor.amberBg : AppColor.surface);
    final strike = s.isCancelled ? TextDecoration.lineThrough : null;
    final radius = BorderRadius.circular(AppRadius.card);
    return Container(
      decoration: BoxDecoration(color: bg, borderRadius: radius, boxShadow: s.isCancelled ? null : AppShadow.card),
      clipBehavior: Clip.antiAlias,
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          onTap: s.isCancelled ? null : onTap,
          child: Container(
            decoration: BoxDecoration(
              border: Border(left: BorderSide(color: live ? AppColor.amber : edge, width: 4)),
            ),
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 12),
            child: Row(
              children: [
                SubjectBadge(s.subjectName, abbr: s.subjectAbbr, size: 40),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        s.subjectName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          decoration: strike,
                          color: s.isCancelled ? AppColor.text3 : AppColor.text1,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        [
                          '${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo}',
                          if (!showTeacher) 'Lớp ${s.className}',
                          if (showTeacher && s.teacherName != null) s.teacherName!,
                        ].join(' · '),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 13, color: AppColor.text2, decoration: strike),
                      ),
                      if (s.isPending)
                        const Padding(
                          padding: EdgeInsets.only(top: 2),
                          child: Text(
                            'Chờ phân công giáo viên',
                            style: TextStyle(fontSize: 12.5, color: AppColor.amberInk, fontWeight: FontWeight.w500),
                          ),
                        ),
                    ],
                  ),
                ),
                // Tiết bình thường (Đã xếp lịch) không cần pill; chỉ báo tiết đang diễn ra, chờ xử lý hoặc đã hủy.
                if (trailing != null) ...[
                  const SizedBox(width: 8),
                  trailing!,
                ] else if (live) ...[
                  const SizedBox(width: 8),
                  const Pill('Đang diễn ra', group: BadgeGroup.pending, dense: true),
                ] else if (s.status != 'Scheduled') ...[
                  const SizedBox(width: 8),
                  StatusPill(StatusDomain.session, s.status, dense: true),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
