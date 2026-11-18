import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/format.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/msms_ai.dart';
import '../../core/widgets/states.dart';
import 'lrn_api.dart';

/// Kết quả một bài (LRN-03) kèm phân tích của MSMS AI (LRN-04); học sinh và phụ huynh dùng chung.
class ResultPage extends ConsumerWidget {
  const ResultPage({super.key, required this.assignmentId, this.studentId});
  final int assignmentId;
  final int? studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = (assignmentId: assignmentId, studentId: studentId);
    final value = ref.watch(resultProvider(key));
    return PageScaffold(
      title: 'Kết quả bài làm',
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(resultProvider(key)),
        loading: const SkeletonList(count: 4, height: 120),
        data: (r) => Refreshable(
          onRefresh: () => ref.refresh(resultProvider(key).future),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 40),
            children: [
              _ScoreCard(r),
              if (r.submitted) ...[
                const SectionTitle('Nhận xét của MSMS AI'),
                _AnalysisBox(r, studentId),
                SectionTitle('Từng câu (${r.questions.length})'),
                for (final q in r.questions)
                  Padding(
                    padding: const EdgeInsets.only(bottom: AppSpacing.md),
                    child: _QuestionCard(q),
                  ),
              ] else
                const Padding(
                  padding: EdgeInsets.only(top: AppSpacing.lg),
                  child: AiEmptyState('Bài không được nộp trước hạn nên không có đáp án và phân tích.'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ScoreCard extends StatelessWidget {
  const _ScoreCard(this.r);
  final PracticeResult r;

  @override
  Widget build(BuildContext context) {
    final total = r.questions.length;
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Row(
        children: [
          if (r.submitted)
            RatioRing(value: (r.score ?? 0) / 10, size: 84, label: Fmt.score(r.score), color: _scoreColor(r.score))
          else
            Container(
              width: 84,
              height: 84,
              alignment: Alignment.center,
              decoration: const BoxDecoration(color: AppColor.neutralBg, shape: BoxShape.circle),
              child: const Text(
                'Không\nnộp',
                textAlign: TextAlign.center,
                style: TextStyle(fontWeight: FontWeight.w700, color: AppColor.text2),
              ),
            ),
          const SizedBox(width: AppSpacing.lg),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(r.title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
                const SizedBox(height: 2),
                Text('${r.subjectName} · Lớp ${r.className}', style: const TextStyle(fontSize: 13, color: AppColor.text2)),
                const SizedBox(height: 8),
                if (r.submitted) ...[
                  Text('Đúng ${r.correctCount ?? 0}/$total câu', style: const TextStyle(fontWeight: FontWeight.w600)),
                  if (r.submittedAt != null)
                    Text('Nộp lúc ${Fmt.dateTime(r.submittedAt!)}', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                ] else
                  Text('Hạn nộp ${Fmt.dateTime(r.dueAt)}', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Color _scoreColor(double? s) {
    final v = s ?? 0;
    if (v >= 8) return AppColor.green600;
    if (v >= 5) return AppColor.amber;
    return AppColor.coralFg;
  }
}

class _AnalysisBox extends StatelessWidget {
  const _AnalysisBox(this.r, this.studentId);
  final PracticeResult r;
  final int? studentId;

  @override
  Widget build(BuildContext context) {
    final a = r.analysis;
    if (a == null || a.status == 'Failed') {
      return const AppCard(child: SizedBox(height: 24));
    }
    if (a.status == 'Pending') {
      return const MsmsAiBubble(thinking: true, message: 'MSMS AI đang phân tích bài làm');
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (a.summary != null && a.summary!.isNotEmpty) MsmsAiBubble(message: a.summary),
        const SizedBox(height: AppSpacing.md),
        if (a.strengths.isNotEmpty) _Bullets('Điểm mạnh', a.strengths, Accent.green, LucideIcons.thumbsUp),
        if (a.weaknesses.isNotEmpty) _Bullets('Cần cố gắng', a.weaknesses, Accent.amber, LucideIcons.target),
        if (a.mistakes.isNotEmpty)
          _Bullets('Vì sao sai', [for (final m in a.mistakes) 'Câu ${m.no}: ${m.why}'], Accent.coral, LucideIcons.circleX),
        if (a.suggestions.isNotEmpty) _Bullets('Gợi ý ôn tập', a.suggestions, Accent.sky, LucideIcons.lightbulb),
        if (a.studyRefs.isNotEmpty)
          AppCard(
            padding: const EdgeInsets.fromLTRB(14, 12, 14, 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Icon(LucideIcons.bookOpen, size: 18, color: AppColor.violetFg),
                    SizedBox(width: 8),
                    Text(
                      'Xem lại trong sách',
                      style: TextStyle(fontWeight: FontWeight.w700, color: AppColor.violetFg),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                for (final s in a.studyRefs)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(s.lessonTitle, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14.5)),
                    subtitle: Text(
                      '${s.bookTitle} · ${s.pagesText} · câu ${s.questionNos.join(', ')}',
                      style: const TextStyle(fontSize: 12.5, color: AppColor.text2),
                    ),
                    trailing: const Icon(LucideIcons.chevronRight, size: 18, color: AppColor.text3),
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => _BookPagesView(assignmentId: r.assignmentId, ref: s, studentId: studentId),
                      ),
                    ),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}

class _Bullets extends StatelessWidget {
  const _Bullets(this.title, this.items, this.accent, this.icon);
  final String title;
  final List<String> items;
  final Accent accent;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(color: accent.bg, borderRadius: BorderRadius.circular(AppRadius.card)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 18, color: accent.fg),
                const SizedBox(width: 8),
                Text(
                  title,
                  style: TextStyle(fontWeight: FontWeight.w700, color: accent.fg),
                ),
              ],
            ),
            const SizedBox(height: 6),
            for (final s in items)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.only(top: 8, right: 8),
                      child: Container(
                        width: 5,
                        height: 5,
                        decoration: BoxDecoration(color: accent.fg, shape: BoxShape.circle),
                      ),
                    ),
                    Expanded(child: Text(s, style: const TextStyle(fontSize: 14, height: 1.45))),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _QuestionCard extends StatelessWidget {
  const _QuestionCard(this.q);
  final ResultQuestion q;

  @override
  Widget build(BuildContext context) {
    final ok = q.isCorrect == true;
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text('Câu ${q.orderNo}', style: const TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(width: 8),
              if (q.difficulty != null) Pill(difficultyLabel[q.difficulty] ?? q.difficulty!, group: BadgeGroup.neutral, dense: true),
              const Spacer(),
              Pill(ok ? 'Đúng · ${Fmt.score(q.points)}đ' : 'Sai · 0đ', group: ok ? BadgeGroup.active : BadgeGroup.danger, dense: true),
            ],
          ),
          const SizedBox(height: 8),
          Text(q.content, style: const TextStyle(fontSize: 14.5, height: 1.5)),
          if (q.isMultiple)
            const Padding(
              padding: EdgeInsets.only(top: 4),
              child: Text(
                'Chọn nhiều đáp án',
                style: TextStyle(fontSize: 12, color: AppColor.text3, fontStyle: FontStyle.italic),
              ),
            ),
          const SizedBox(height: 8),
          for (final o in q.options) _OptionLine(o),
          if (q.explanation != null && q.explanation!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: AppColor.sunken, borderRadius: BorderRadius.circular(AppRadius.md)),
              child: Text.rich(
                TextSpan(
                  children: [
                    const TextSpan(
                      text: 'Giải thích: ',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    TextSpan(text: q.explanation),
                  ],
                ),
                style: const TextStyle(fontSize: 13.5, height: 1.45, color: AppColor.text2),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _OptionLine extends StatelessWidget {
  const _OptionLine(this.o);
  final ResultOption o;

  @override
  Widget build(BuildContext context) {
    final wrongPick = o.selected && !o.isCorrect;
    final Color bg = o.isCorrect ? AppColor.green50 : (wrongPick ? AppColor.coralBg : AppColor.surface);
    final Color border = o.isCorrect ? AppColor.green300 : (wrongPick ? AppColor.coralFg.withValues(alpha: .5) : AppColor.border);
    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: bg,
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${o.label}.', style: const TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(width: 8),
          Expanded(child: Text(o.content, style: const TextStyle(fontSize: 14, height: 1.4))),
          if (o.isCorrect)
            const Padding(
              padding: EdgeInsets.only(left: 6),
              child: Icon(LucideIcons.check, size: 18, color: AppColor.green700),
            ),
          if (wrongPick)
            const Padding(
              padding: EdgeInsets.only(left: 6),
              child: Icon(LucideIcons.x, size: 18, color: AppColor.coralFg),
            ),
          if (o.selected)
            const Padding(
              padding: EdgeInsets.only(left: 6),
              child: Text('Đã chọn', style: TextStyle(fontSize: 11.5, color: AppColor.text2)),
            ),
        ],
      ),
    );
  }
}

/// Ảnh các trang sách của bài nguồn (chỉ trang của bài đã nộp, Backend kiểm).
class _BookPagesView extends StatelessWidget {
  const _BookPagesView({required this.assignmentId, required this.ref, this.studentId});
  final int assignmentId;
  final StudyRef ref;
  final int? studentId;

  @override
  Widget build(BuildContext context) {
    final pages = [for (var p = ref.startPage; p <= ref.endPage; p++) p];
    return PageScaffold(
      title: ref.lessonTitle,
      body: PageView.builder(
        itemCount: pages.length,
        itemBuilder: (_, i) => Column(
          children: [
            Expanded(
              child: InteractiveViewer(
                maxScale: 4,
                child: AuthImage(
                  '/api/lrn/results/$assignmentId/pages/${ref.lessonId}/${pages[i]}${studentId == null ? '' : '?studentId=$studentId'}',
                  fit: BoxFit.contain,
                  placeholder: const Center(child: SkeletonBox(height: 400, width: 280)),
                  error: const Center(
                    child: Text('Không tải được trang sách.', style: TextStyle(color: AppColor.text2)),
                  ),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Text('Trang ${pages[i]} · ${i + 1}/${pages.length}', style: const TextStyle(color: AppColor.text2)),
            ),
          ],
        ),
      ),
    );
  }
}
