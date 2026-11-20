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
import '../att/att_api.dart';
import '../att/att_pages.dart';
import '../lrn/lrn_api.dart';
import '../par/children.dart';
import '../par/inbox_api.dart';
import '../par/par_api.dart';
import '../scd/scd_api.dart';
import '../scd/schedule_page.dart';

/// Trang chủ theo loại tài khoản: header gradient → thẻ nổi bật → lưới 2×2 → danh sách ngắn (Design_Mobile §4).
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(currentUserProvider);
    if (user.isTeacher) return const TeacherHome();
    if (user.isStudent) return const StudentHome();
    return const ParentHome();
  }
}

/// Khung cuộn chung: header không có lề, nội dung có lề 16, chừa chỗ cho bottom nav nổi.
class _HomeScroll extends StatelessWidget {
  const _HomeScroll({required this.header, required this.children, required this.onRefresh});
  final Widget header;
  final List<Widget> children;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Refreshable(
        onRefresh: onRefresh,
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            header,
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, AppSpacing.lg, AppSpacing.screen, 120),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: children),
            ),
          ],
        ),
      ),
    );
  }
}

String _count(AsyncValue<int> v) => v.hasValue ? '${v.requireValue}' : '…';

// ───────────────────────────── Giáo viên ─────────────────────────────

class TeacherHome extends ConsumerWidget {
  const TeacherHome({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(currentUserProvider);
    final today = ref.watch(todayAttendanceProvider);
    final now = DateTime.now();
    final sessions = (today.value ?? const <AttSession>[]).where((s) => s.status != 'Cancelled').toList();
    final next = sessions.where((s) => _endOf(s).isAfter(now)).firstOrNull;
    final unmarked = sessions.where((s) => s.canWrite && !s.done && _startOf(s).isBefore(now.add(const Duration(minutes: 10)))).length;
    final unreadMsg = ref.watch(unreadMessagesProvider);
    final pending = user.isHomeroom ? ref.watch(pendingHomeroomLeavesProvider) : null;
    final week = user.isHomeroom ? null : ref.watch(weekScheduleProvider(weekStart(now)));

    return _HomeScroll(
      onRefresh: () async {
        ref.invalidate(todayAttendanceProvider);
        ref.invalidate(unreadMessagesProvider);
        ref.invalidate(pendingHomeroomLeavesProvider);
        await ref.read(todayAttendanceProvider.future);
      },
      header: HeroHeader(
        title: 'Chào, ${user.givenName}',
        subtitle: Fmt.longDate(now),
        actions: [
          HeaderPill(
            label: 'Điểm danh',
            icon: LucideIcons.clipboardCheck,
            onTap: () => next != null && next.canWrite ? context.push('/attendance/${next.id}') : context.push('/attendance-today'),
          ),
          HeaderPill(label: 'Lịch hôm nay', icon: LucideIcons.calendarDays, onTap: () => context.go('/schedule')),
        ],
      ),
      children: [
        if (today.isLoading && !today.hasValue)
          const SkeletonBox(height: 112)
        else if (today.hasError && !today.hasValue)
          ErrorState(today.error!, onRetry: () => ref.invalidate(todayAttendanceProvider))
        else
          _NextSessionCard(next),
        const SizedBox(height: AppSpacing.lg),
        TilePanel(
          child: TwoColumnGrid(
            children: [
              StatTile(
                value: today.hasValue ? '${sessions.length}' : '…',
                label: 'Tiết hôm nay',
                accent: Accent.green,
                onTap: () => context.go('/schedule'),
              ),
              StatTile(
                value: today.hasValue ? '$unmarked' : '…',
                label: 'Chưa điểm danh',
                hint: unmarked == 0 ? 'Đã xong hết' : 'Tiết đã bắt đầu',
                accent: Accent.coral,
                onTap: () => context.push('/attendance-today'),
              ),
              if (pending != null)
                StatTile(value: _count(pending), label: 'Đơn nghỉ chờ duyệt', accent: Accent.amber, onTap: () => context.push('/leave'))
              else
                StatTile(
                  value: week?.hasValue == true ? '${week!.requireValue.where((s) => !s.isCancelled).length}' : '…',
                  label: 'Tiết tuần này',
                  accent: Accent.violet,
                  onTap: () => context.go('/schedule'),
                ),
              StatTile(value: _count(unreadMsg), label: 'Tin nhắn chưa đọc', accent: Accent.sky, onTap: () => context.go('/third')),
            ],
          ),
        ),
        if (user.isHomeroom) ...[
          const SectionTitle('Lớp chủ nhiệm'),
          TilePanel(
            child: TwoColumnGrid(
              children: [
                ActionTile(
                  icon: LucideIcons.userCheck,
                  label: 'Chuyên cần lớp',
                  hint: 'Ai vắng hôm nay',
                  accent: Accent.teal,
                  onTap: () => context.push('/homeroom-attendance'),
                ),
                ActionTile(
                  icon: LucideIcons.users,
                  label: 'Học sinh lớp',
                  hint: 'Hồ sơ, sổ liên lạc',
                  accent: Accent.violet,
                  onTap: () => context.push('/homeroom-students'),
                ),
              ],
            ),
          ),
        ],
        if (sessions.isNotEmpty) ...[
          SectionTitle('Các tiết hôm nay', action: 'Tất cả', onAction: () => context.push('/attendance-today')),
          for (final s in sessions.where((s) => _endOf(s).isAfter(now)).take(3))
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.md),
              child: AttSessionCard(s, onTap: () => context.push('/attendance/${s.id}')),
            ),
          if (sessions.every((s) => !_endOf(s).isAfter(now))) const AiEmptyState('Hết tiết hôm nay rồi. Thầy cô nghỉ ngơi nhé!'),
        ],
      ],
    );
  }

  static DateTime _startOf(AttSession s) => _at(s.date, s.start);
  static DateTime _endOf(AttSession s) => _at(s.date, s.end);
  static DateTime _at(DateTime d, String hm) =>
      DateTime(d.year, d.month, d.day, int.parse(hm.substring(0, 2)), int.parse(hm.substring(3, 5)));
}

class _NextSessionCard extends StatelessWidget {
  const _NextSessionCard(this.s);
  final AttSession? s;

  @override
  Widget build(BuildContext context) {
    final s = this.s;
    if (s == null) {
      return const AppCard(child: MsmsAiBubble(message: 'Hết tiết hôm nay', size: 48));
    }
    final now = DateTime.now();
    final live = TeacherHome._startOf(s).isBefore(now);
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Row(
        children: [
          SubjectBadge(s.subjectName, size: 52),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  live ? 'Đang diễn ra' : 'Tiết tiếp theo',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: live ? AppColor.amberInk : AppColor.green700),
                ),
                Text(
                  '${s.subjectName} · ${s.className}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
                ),
                Text(
                  '${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo} · ${s.start}–${s.end}',
                  style: const TextStyle(fontSize: 13, color: AppColor.text2),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          if (s.canWrite)
            FilledButton(
              onPressed: () => context.push('/attendance/${s.id}'),
              style: FilledButton.styleFrom(minimumSize: const Size(0, 48), padding: const EdgeInsets.symmetric(horizontal: 14)),
              child: Text(s.done ? 'Xem' : 'Điểm danh'),
            ),
        ],
      ),
    );
  }
}

// ───────────────────────────── Học sinh ─────────────────────────────

class StudentHome extends ConsumerWidget {
  const StudentHome({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(currentUserProvider);
    final now = DateTime.now();
    final klass = ref.watch(myClassProvider).value;
    final today = ref.watch(todayScheduleProvider);
    final tasks = ref.watch(assignmentsProvider);
    final week = ref.watch(myAttendanceProvider((from: weekStart(now), to: dateOnly(now))));

    final list = tasks.value ?? const <AssignmentItem>[];
    final todo = list.where((a) => a.isTodo && a.state != 'NotOpen').toList();
    final soon = todo.where((a) => a.dueAt.difference(now).inHours < 48).length;
    final fresh = list.where((a) => a.state == 'Submitted' && a.submittedAt != null && now.difference(a.submittedAt!).inDays < 7).length;
    final c = week.value?.counts;

    return _HomeScroll(
      onRefresh: () async {
        ref.invalidate(assignmentsProvider);
        ref.invalidate(weekScheduleProvider(weekStart(now)));
        ref.invalidate(myAttendanceProvider);
        await ref.read(assignmentsProvider.future);
      },
      header: HeroHeader(
        title: 'Chào, ${user.givenName}',
        subtitle: [if (klass != null) 'Lớp $klass', Fmt.longDate(now)].join(' · '),
        actions: [
          HeaderPill(label: 'Sổ liên lạc', icon: LucideIcons.notebookText, onTap: () => context.push('/report')),
          HeaderPill(label: 'Chuyên cần', icon: LucideIcons.userCheck, onTap: () => context.push('/attendance-me')),
        ],
      ),
      children: [
        _NowCard(today),
        const SizedBox(height: AppSpacing.lg),
        TilePanel(
          child: TwoColumnGrid(
            children: [
              StatTile(
                value: tasks.hasValue ? '${todo.length}' : '…',
                label: 'Bài chưa nộp',
                accent: Accent.coral,
                onTap: () => context.go('/third'),
              ),
              StatTile(
                value: tasks.hasValue ? '$soon' : '…',
                label: 'Sắp hết hạn',
                hint: 'Trong 48 giờ tới',
                accent: Accent.amber,
                onTap: () => context.go('/third'),
              ),
              StatTile(
                value: tasks.hasValue ? '$fresh' : '…',
                label: 'Điểm mới',
                hint: 'Trong 7 ngày qua',
                accent: Accent.violet,
                onTap: () => context.go('/third?tab=1'),
              ),
              StatTile(
                value: c == null ? '…' : (c.total == 0 ? '—' : '${(c.present * 100 / c.total).round()}%'),
                label: 'Chuyên cần tuần',
                hint: c == null || c.total == 0 ? null : '${c.present}/${c.total} tiết có mặt',
                accent: Accent.teal,
                onTap: () => context.push('/attendance-me'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Tiết đang học hoặc tiết kế tiếp hôm nay.
class _NowCard extends StatelessWidget {
  const _NowCard(this.today);
  final AsyncValue<List<Session>> today;

  @override
  Widget build(BuildContext context) {
    if (!today.hasValue) {
      return today.hasError ? ErrorState(today.error!) : const SkeletonBox(height: 96);
    }
    final now = DateTime.now();
    final list = today.requireValue.where((s) => !s.isCancelled).toList();
    final current = list.where((s) => s.startsAt.isBefore(now) && s.endsAt.isAfter(now)).firstOrNull;
    final next = current ?? list.where((s) => s.startsAt.isAfter(now)).firstOrNull;
    if (next == null) {
      return AppCard(
        child: MsmsAiBubble(
          size: 48,
          message: list.isEmpty ? 'Hôm nay không có tiết học nào.' : 'Hết tiết hôm nay rồi. Về nhà nghỉ ngơi nhé!',
        ),
      );
    }
    return SessionCard(
      next,
      showTeacher: true,
      live: current != null,
      onTap: () => context.go('/schedule'),
      trailing: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            next.start,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColor.ink),
          ),
          Text(
            current != null ? 'Đang học' : 'Tiếp theo',
            style: TextStyle(fontSize: 11.5, color: current != null ? AppColor.amberInk : AppColor.text3, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

// ───────────────────────────── Phụ huynh ─────────────────────────────

/// Đơn Chờ duyệt của con đang chọn (trang đầu, đủ cho trang chủ).
final _childPendingLeavesProvider = FutureProvider.autoDispose<int>((ref) async {
  final child = await ref.watch(currentChildProvider.future);
  if (child == null) return 0;
  final p = await ref.read(parApiProvider).childLeaves(child.studentId, 1);
  return p.items.where((i) => i.isPending).length;
});

class ParentHome extends ConsumerWidget {
  const ParentHome({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = DateTime.now();
    final child = ref.watch(currentChildProvider);
    final c = child.value;
    final report = c == null ? null : ref.watch(weeklyReportProvider((studentId: c.studentId, week: null)));
    final pending = ref.watch(_childPendingLeavesProvider);
    final results = ref.watch(assignmentsProvider);
    final fresh = (results.value ?? const <AssignmentItem>[])
        .where((a) => a.state == 'Submitted' && a.submittedAt != null && now.difference(a.submittedAt!).inDays < 7)
        .length;
    final unreadMsg = ref.watch(unreadMessagesProvider);

    return _HomeScroll(
      onRefresh: () async {
        ref.invalidate(myChildrenProvider);
        ref.invalidate(weeklyReportProvider);
        ref.invalidate(_childPendingLeavesProvider);
        ref.invalidate(assignmentsProvider);
        ref.invalidate(unreadMessagesProvider);
        await ref.read(currentChildProvider.future);
      },
      header: HeroHeader(
        leading: const ChildChip(),
        title: c == null ? 'Chào phụ huynh' : 'Hôm nay của ${c.givenName}',
        subtitle: [if (c?.className != null) 'Lớp ${c!.className}', Fmt.longDate(now)].join(' · '),
        actions: [
          HeaderPill(label: 'Lịch học', icon: LucideIcons.calendarDays, onTap: () => context.go('/schedule')),
          HeaderPill(label: 'Chuyên cần', icon: LucideIcons.userCheck, onTap: () => context.push('/attendance-me')),
        ],
      ),
      children: [
        if (child.hasValue && c == null)
          const EmptyState('Chưa có học sinh nào được liên kết với tài khoản. Liên hệ nhà trường để được liên kết.')
        else ...[
          _WeekBookCard(report),
          const SizedBox(height: AppSpacing.lg),
          TilePanel(
            child: TwoColumnGrid(
              children: [
                ActionTile(
                  icon: LucideIcons.filePlus,
                  label: 'Xin nghỉ',
                  hint: 'Gửi đơn cho chủ nhiệm',
                  accent: Accent.green,
                  onTap: () => context.push('/leave/new'),
                ),
                StatTile(value: _count(pending), label: 'Đơn đang chờ', accent: Accent.amber, onTap: () => context.push('/leave')),
                StatTile(
                  value: results.hasValue ? '$fresh' : '…',
                  label: 'Điểm mới',
                  hint: 'Trong 7 ngày qua',
                  accent: Accent.violet,
                  onTap: () => context.push('/results'),
                ),
                StatTile(
                  value: _count(unreadMsg),
                  label: 'Nhắn giáo viên',
                  hint: 'Tin chưa đọc',
                  accent: Accent.sky,
                  onTap: () => context.go('/third'),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }
}

/// Sổ liên lạc tuần này: bốn con số chuyên cần, bấm để mở sổ (Design_Mobile §4.3).
class _WeekBookCard extends StatelessWidget {
  const _WeekBookCard(this.report);
  final AsyncValue<WeeklyReport>? report;

  @override
  Widget build(BuildContext context) {
    final r = report;
    if (r == null || (!r.hasValue && !r.hasError)) return const SkeletonBox(height: 132);
    if (!r.hasValue) return ErrorState(r.error!);
    final w = r.requireValue;
    Widget fig(int v, String l, Color c) => Expanded(
      child: Column(
        children: [
          Text(
            w.total == 0 ? '—' : '$v',
            style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: c),
          ),
          Text(
            l,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 11.5, color: AppColor.text2, height: 1.2),
          ),
        ],
      ),
    );
    return AppCard(
      onTap: () => context.push('/report'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(LucideIcons.notebookText, size: 18, color: AppColor.green700),
              const SizedBox(width: 8),
              const Expanded(
                child: Text('Sổ liên lạc tuần này', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
              ),
              Text(
                '${Fmt.shortDate(w.weekStart)} – ${Fmt.shortDate(w.weekEnd)}',
                style: const TextStyle(fontSize: 12, color: AppColor.text3),
              ),
            ],
          ),
          const SizedBox(height: 14),
          if (w.total == 0)
            const Text('Tuần này chưa có tiết nào đã diễn ra.', style: TextStyle(color: AppColor.text3, fontSize: 13.5))
          else
            Row(
              children: [
                fig(w.present, 'Có mặt', AppColor.green700),
                fig(w.late, 'Muộn', AppColor.skyFg),
                fig(w.excused, 'Vắng\ncó phép', AppColor.tealFg),
                fig(w.unexcused, 'Vắng\nkhông phép', AppColor.coralFg),
              ],
            ),
          if (w.assigned > 0) ...[
            const Divider(height: 24),
            Text(
              'Bài tập: đã nộp ${w.submitted}/${w.assigned}${w.missed > 0 ? ' · ${w.missed} bài không nộp' : ''}',
              style: TextStyle(fontSize: 13, color: w.missed > 0 ? AppColor.coralFg : AppColor.text2),
            ),
          ],
        ],
      ),
    );
  }
}
