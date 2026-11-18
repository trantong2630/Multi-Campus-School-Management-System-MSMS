import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';
import 'children.dart';
import 'par_api.dart';

/// Sổ liên lạc tuần (PAR-02): chuyên cần, bài tập, điểm và phân tích — chỉ đọc.
/// Học sinh xem của mình, phụ huynh xem của con đang chọn, chủ nhiệm mở từ danh sách lớp ([studentId]).
class WeeklyReportPage extends ConsumerStatefulWidget {
  const WeeklyReportPage({super.key, this.studentId});
  final int? studentId;

  @override
  ConsumerState<WeeklyReportPage> createState() => _WeeklyReportPageState();
}

class _WeeklyReportPageState extends ConsumerState<WeeklyReportPage> {
  int _index = -1; // vị trí tuần trong danh sách; -1 = tuần hiện tại

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final int? sid = widget.studentId ?? (user.isStudent ? user.personId : ref.watch(currentChildProvider).value?.studentId);
    return PageScaffold(
      title: 'Sổ liên lạc',
      body: sid == null
          ? (user.isParent && ref.watch(currentChildProvider).isLoading
                ? const SkeletonList(count: 3, height: 140)
                : const EmptyState('Chưa có học sinh nào được liên kết với tài khoản.'))
          : _content(user.isParent && widget.studentId == null, sid),
    );
  }

  Widget _content(bool showChild, int sid) {
    final weeks = ref.watch(reportWeeksProvider(sid));
    return AsyncView(
      value: weeks,
      onRetry: () => ref.invalidate(reportWeeksProvider(sid)),
      loading: const SkeletonList(count: 3, height: 140),
      data: (list) {
        if (list.isEmpty) return const EmptyState('Chưa có tuần học nào để xem.');
        final current = list.indexWhere((w) => w.isCurrent);
        final idx = (_index >= 0 ? _index : (current >= 0 ? current : list.length - 1)).clamp(0, list.length - 1);
        final week = list[idx];
        final key = (studentId: sid, week: week.start);
        final report = ref.watch(weeklyReportProvider(key));
        return Column(
          children: [
            if (showChild)
              const Padding(
                padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
                child: Align(alignment: Alignment.centerLeft, child: ChildChip()),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Row(
                children: [
                  IconButton(
                    tooltip: 'Tuần trước',
                    onPressed: idx > 0 ? () => setState(() => _index = idx - 1) : null,
                    icon: const Icon(LucideIcons.chevronLeft),
                  ),
                  Expanded(
                    child: Column(
                      children: [
                        Text(
                          'Tuần ${week.number}${week.isCurrent ? ' · Tuần này' : ''}',
                          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                        ),
                        Text(
                          '${Fmt.shortDate(week.start)} – ${Fmt.date(week.end)}',
                          style: const TextStyle(fontSize: 12.5, color: AppColor.text3),
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    tooltip: 'Tuần sau',
                    onPressed: idx < list.length - 1 ? () => setState(() => _index = idx + 1) : null,
                    icon: const Icon(LucideIcons.chevronRight),
                  ),
                ],
              ),
            ),
            Expanded(
              child: AsyncView(
                value: report,
                onRetry: () => ref.invalidate(weeklyReportProvider(key)),
                loading: const SkeletonList(count: 3, height: 140),
                data: (r) => Refreshable(
                  onRefresh: () => ref.refresh(weeklyReportProvider(key).future),
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 40),
                    children: [
                      Text('${r.studentName} · Lớp ${r.className}', style: const TextStyle(color: AppColor.text2, fontSize: 13.5)),
                      const SizedBox(height: AppSpacing.md),
                      _AttendanceCard(r),
                      const SizedBox(height: AppSpacing.md),
                      _AssignmentCard(r),
                      const SizedBox(height: AppSpacing.md),
                      _ScoresCard(r),
                    ],
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _CardTitle extends StatelessWidget {
  const _CardTitle(this.icon, this.title, this.accent);
  final IconData icon;
  final String title;
  final Accent accent;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      Container(
        padding: const EdgeInsets.all(7),
        decoration: BoxDecoration(color: accent.bg, borderRadius: BorderRadius.circular(10)),
        child: Icon(icon, size: 18, color: accent.fg),
      ),
      const SizedBox(width: 10),
      Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
    ],
  );
}

/// Không vẽ số 0 cho tuần chưa có dữ liệu: một dòng xám (Design_Mobile §5.3).
class _NoData extends StatelessWidget {
  const _NoData(this.text);
  final String text;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 12),
    child: Text(text, style: const TextStyle(color: AppColor.text3, fontSize: 13.5)),
  );
}

class _Figure extends StatelessWidget {
  const _Figure(this.value, this.label, this.color);
  final int value;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
    child: Column(
      children: [
        Text(
          '$value',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: color),
        ),
        Text(
          label,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 11.5, color: AppColor.text2, height: 1.2),
        ),
      ],
    ),
  );
}

class _AttendanceCard extends StatelessWidget {
  const _AttendanceCard(this.r);
  final WeeklyReport r;

  @override
  Widget build(BuildContext context) {
    final happened = r.total - r.unmarked;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardTitle(LucideIcons.calendarCheck, 'Chuyên cần', Accent.green),
          if (r.total == 0)
            const _NoData('Tuần này chưa có tiết nào đã diễn ra.')
          else ...[
            const SizedBox(height: 14),
            Row(
              children: [
                RatioRing(value: happened == 0 ? 0 : r.present / happened, size: 64, label: happened == 0 ? '—' : null),
                const SizedBox(width: 12),
                _Figure(r.present, 'Có mặt', AppColor.green700),
                _Figure(r.late, 'Muộn', AppColor.skyFg),
                _Figure(r.excused, 'Vắng\ncó phép', AppColor.tealFg),
                _Figure(r.unexcused, 'Vắng\nkhông phép', AppColor.coralFg),
              ],
            ),
            if (r.unmarked > 0)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Text('${r.unmarked} tiết chưa điểm danh', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
              ),
          ],
        ],
      ),
    );
  }
}

class _AssignmentCard extends StatelessWidget {
  const _AssignmentCard(this.r);
  final WeeklyReport r;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardTitle(LucideIcons.listChecks, 'Bài tập', Accent.amber),
          if (r.assigned == 0)
            const _NoData('Không có bài tập nào có hạn nộp trong tuần.')
          else ...[
            const SizedBox(height: 14),
            Row(
              children: [
                _Figure(r.assigned, 'Được giao', AppColor.ink),
                _Figure(r.submitted, 'Đã nộp', AppColor.green700),
                _Figure(r.unsubmitted, 'Chưa nộp', AppColor.amberFg),
                _Figure(r.missed, 'Không nộp', AppColor.coralFg),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _ScoresCard extends StatelessWidget {
  const _ScoresCard(this.r);
  final WeeklyReport r;

  @override
  Widget build(BuildContext context) {
    final scored = r.assignments.where((a) => a.status == 'Submitted').toList();
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardTitle(LucideIcons.sparkles, 'Điểm và phân tích', Accent.violet),
          if (scored.isEmpty)
            const _NoData('Chưa có bài nào được chấm trong tuần.')
          else
            for (final a in scored)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SubjectBadge(a.subjectName, size: 36),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(a.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                          Text(a.subjectName, style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                          if ((a.aiFeedback ?? '').isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: Text(a.aiFeedback!, style: const TextStyle(fontSize: 13, color: AppColor.text2, height: 1.4)),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      Fmt.score(a.score),
                      style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColor.ink),
                    ),
                  ],
                ),
              ),
        ],
      ),
    );
  }
}

/// Danh sách học sinh lớp chủ nhiệm, bấm để xem sổ liên lạc từng em.
class HomeroomStudentsPage extends ConsumerStatefulWidget {
  const HomeroomStudentsPage({super.key});

  @override
  ConsumerState<HomeroomStudentsPage> createState() => _HomeroomStudentsPageState();
}

class _HomeroomStudentsPageState extends ConsumerState<HomeroomStudentsPage> {
  String _q = '';

  @override
  Widget build(BuildContext context) {
    final value = ref.watch(homeroomStudentsProvider);
    return PageScaffold(
      title: 'Sổ liên lạc lớp',
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(homeroomStudentsProvider),
        data: (list) {
          if (list.isEmpty) return const EmptyState('Hiện không có lớp chủ nhiệm.');
          final q = _q.trim().toLowerCase();
          final shown = q.isEmpty ? list : list.where((s) => s.name.toLowerCase().contains(q) || s.code.toLowerCase().contains(q)).toList();
          return Column(
            children: [
              if (list.length > 12)
                Padding(
                  padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
                  child: TextField(
                    onChanged: (v) => setState(() => _q = v),
                    decoration: const InputDecoration(prefixIcon: Icon(LucideIcons.search, size: 18), hintText: 'Tìm theo tên hoặc mã'),
                  ),
                ),
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 32),
                  itemCount: shown.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 10),
                  itemBuilder: (_, i) {
                    final s = shown[i];
                    return PersonRow(
                      name: s.name,
                      subtitle: '${s.code} · Lớp ${s.className}',
                      trailing: const Icon(LucideIcons.chevronRight, size: 18, color: AppColor.text3),
                      onTap: () => context.push('/report?studentId=${s.id}'),
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
