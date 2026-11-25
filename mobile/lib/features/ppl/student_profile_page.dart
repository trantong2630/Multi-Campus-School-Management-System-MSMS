import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/format.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';
import 'ppl_api.dart';

/// Hồ sơ học sinh (PPL-02, Design_Mobile §5.8): chủ nhiệm mở từ Học sinh lớp. Chỉ xem; báo cáo tuần mở bằng nút ngay dưới đầu trang.
class StudentProfilePage extends ConsumerWidget {
  const StudentProfilePage({super.key, required this.studentId});

  final int studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(studentProfileProvider(studentId));
    return PageScaffold(
      title: 'Hồ sơ học sinh',
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(studentProfileProvider(studentId)),
        data: (s) => Refreshable(
          onRefresh: () => ref.refresh(studentProfileProvider(studentId).future),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 40),
            children: [
              _Header(s),
              if (s.note != null && s.note!.trim().isNotEmpty) ...[
                const SizedBox(height: AppSpacing.md),
                InlineAlert('Ghi chú: ${s.note}', tone: Accent.amber, icon: LucideIcons.stickyNote),
              ],
              const SizedBox(height: AppSpacing.md),
              FilledButton.icon(
                onPressed: () => context.push('/report?studentId=${s.id}'),
                style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                icon: const Icon(LucideIcons.chartColumn, size: 18),
                label: const Text('Báo cáo tuần'),
              ),
              const SectionTitle('Phụ huynh'),
              _GuardianCard(s.guardian),
              const SectionTitle('Chuyên cần'),
              _AttendanceBlock(s.attendance),
              const SectionTitle('Thông tin cá nhân'),
              _InfoCard(s),
              const SectionTitle('Lịch sử lớp'),
              _History(s.enrollments),
            ],
          ),
        ),
      ),
    );
  }
}

/// Đầu trang: nền pastel theo màu avatar, tên lớn, lớp hiện tại ở một viên riêng để nhìn là thấy.
class _Header extends StatelessWidget {
  const _Header(this.s);
  final StudentProfile s;

  @override
  Widget build(BuildContext context) {
    final tone = accentFor(s.name);
    final dob = s.dateOfBirth;
    final age = dob == null ? null : _age(dob);
    final meta = [s.code, if (age != null) '$age tuổi', if (s.gender != null) genderLabel(s.gender!)].join(' · ');
    return Container(
      padding: const EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [s.isStudying ? tone.bg : AppColor.neutralBg, AppColor.surface],
        ),
        boxShadow: AppShadow.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              PersonAvatar(s.name, size: 60),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      s.name,
                      style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: AppColor.ink, height: 1.2),
                    ),
                    const SizedBox(height: 2),
                    Text(meta, style: const TextStyle(fontSize: 13, color: AppColor.text2)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                decoration: BoxDecoration(color: AppColor.surface, borderRadius: BorderRadius.circular(AppRadius.pill)),
                child: Text.rich(
                  TextSpan(
                    children: s.className == null
                        ? [TextSpan(text: s.isStudying ? 'Chưa xếp lớp' : 'Rời trường ${s.leftDate == null ? '' : Fmt.date(s.leftDate!)}')]
                        : [
                            const TextSpan(text: 'Lớp '),
                            TextSpan(
                              text: s.className,
                              style: const TextStyle(fontWeight: FontWeight.w800, color: AppColor.green700),
                            ),
                            TextSpan(text: ' · ${s.campusName}'),
                          ],
                  ),
                  style: const TextStyle(fontSize: 14, color: AppColor.text1),
                ),
              ),
              StatusPill(StatusDomain.student, s.status),
            ],
          ),
        ],
      ),
    );
  }

  static int _age(DateTime dob) {
    final now = DateTime.now();
    var a = now.year - dob.year;
    if (now.month < dob.month || (now.month == dob.month && now.day < dob.day)) a--;
    return a;
  }
}

class _GuardianCard extends StatelessWidget {
  const _GuardianCard(this.g);
  final StudentGuardian? g;

  @override
  Widget build(BuildContext context) {
    final g = this.g;
    if (g == null) return const EmptyState('Chưa có phụ huynh đại diện.', compact: true);
    return AppCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: [
          PersonAvatar(g.name),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(g.name, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
                Text(g.relationship ?? 'Phụ huynh đại diện', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                if (g.email != null) Text(g.email!, style: const TextStyle(fontSize: 12.5, color: AppColor.text2)),
              ],
            ),
          ),
          // Số điện thoại phụ huynh là thứ chủ nhiệm cần nhất: chữ lớn, giữ để sao chép.
          if (g.phone != null)
            SelectableText(
              g.phone!,
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppColor.green700),
            ),
        ],
      ),
    );
  }
}

class _AttendanceBlock extends StatelessWidget {
  const _AttendanceBlock(this.a);
  final StudentAttOverview? a;

  @override
  Widget build(BuildContext context) {
    final a = this.a;
    if (a == null) return const EmptyState('Chưa có năm học hiện hành nên chưa có số liệu chuyên cần.', compact: true);
    final c = a.counts;
    final rate = a.rate;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        AppCard(
          child: Row(
            children: [
              RatioRing(value: (rate ?? 0) / 100, size: 72, label: rate == null ? '—' : '${rate.round()}%'),
              const SizedBox(width: AppSpacing.lg),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      rate == null ? 'Chưa có lượt điểm danh nào' : 'Có mặt ${c.present + c.late}/${c.marked} lượt',
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 2),
                    Text('Năm học ${a.yearName} · đến ${Fmt.date(a.to)}', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                    if (c.notMarked > 0)
                      Text('${c.notMarked} tiết chưa điểm danh, không tính', style: const TextStyle(fontSize: 12, color: AppColor.text3)),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        TwoColumnGrid(
          children: [
            StatTile(value: '${c.present}', label: 'Có mặt', accent: Accent.green),
            StatTile(value: '${c.late}', label: 'Muộn', accent: Accent.sky),
            StatTile(value: '${c.excused}', label: 'Vắng có phép', accent: Accent.teal),
            StatTile(value: '${c.unexcused}', label: 'Vắng không phép', accent: Accent.coral),
          ],
        ),
        if (a.recent.isNotEmpty) ...[
          const Padding(
            padding: EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.sm),
            child: Text(
              'Muộn và vắng gần đây',
              style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColor.text2),
            ),
          ),
          for (final it in a.recent)
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
      ],
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard(this.s);
  final StudentProfile s;

  @override
  Widget build(BuildContext context) {
    final rows = <(String, String?)>[
      ('Ngày sinh', s.dateOfBirth == null ? null : Fmt.date(s.dateOfBirth!)),
      ('Giới tính', s.gender == null ? null : genderLabel(s.gender!)),
      ('Số định danh', s.nationalId),
      ('Điện thoại', s.phone),
      ('Địa chỉ', s.address),
      ('Ngày nhập học', s.admissionDate == null ? null : Fmt.date(s.admissionDate!)),
      if (s.leftDate != null) ('Ngày rời trường', Fmt.date(s.leftDate!)),
    ];
    return AppCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
      child: Column(
        children: [
          for (var i = 0; i < rows.length; i++)
            Container(
              padding: const EdgeInsets.symmetric(vertical: 10),
              decoration: BoxDecoration(
                border: i == 0 ? null : const Border(top: BorderSide(color: AppColor.border)),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 112,
                    child: Text(rows[i].$1, style: const TextStyle(fontSize: 13, color: AppColor.text3)),
                  ),
                  Expanded(
                    child: Text(
                      rows[i].$2 ?? '—',
                      style: TextStyle(fontSize: 14, color: rows[i].$2 == null ? AppColor.text3 : AppColor.text1),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Lịch sử lớp: dòng thời gian dọc, lớp đang học có chấm xanh đặc.
class _History extends StatelessWidget {
  const _History(this.rows);
  final List<StudentEnrollment> rows;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) return const EmptyState('Chưa có ghi danh nào.', compact: true);
    return AppCard(
      child: Column(
        children: [
          for (var i = 0; i < rows.length; i++)
            IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    width: 18,
                    child: Column(
                      children: [
                        const SizedBox(height: 4),
                        Container(
                          width: 12,
                          height: 12,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: rows[i].isCurrent ? AppColor.green500 : AppColor.surface,
                            border: Border.all(color: rows[i].isCurrent ? AppColor.green500 : AppColor.borderStrong, width: 2),
                          ),
                        ),
                        if (i < rows.length - 1) Expanded(child: Container(width: 2, color: AppColor.border)),
                      ],
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Padding(
                      padding: EdgeInsets.only(bottom: i < rows.length - 1 ? AppSpacing.lg : 0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            rows[i].yearName,
                            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: AppColor.text3),
                          ),
                          Text.rich(
                            TextSpan(
                              children: [
                                TextSpan(
                                  text: rows[i].className,
                                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppColor.ink),
                                ),
                                TextSpan(text: '  ${rows[i].campusName}'),
                              ],
                            ),
                            style: const TextStyle(fontSize: 13, color: AppColor.text2),
                          ),
                          Text(
                            '${Fmt.date(rows[i].start)} – ${Fmt.date(rows[i].end)}'
                            '${rows[i].reason == null ? '' : ' · ${enrollmentCloseReasonLabel[rows[i].reason] ?? rows[i].reason}'}'
                            '${rows[i].isCurrent ? ' · Đang học' : ''}',
                            style: const TextStyle(fontSize: 12, color: AppColor.text3),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
