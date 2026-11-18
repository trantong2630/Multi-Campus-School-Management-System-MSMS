import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/format.dart';
import '../../core/http/api_client.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';
import 'att_api.dart';

/// Ba lựa chọn: Có mặt / Muộn / Vắng; nhãn có phép do Backend suy từ đơn nghỉ (BR-18).
enum Mark {
  present('Present', 'C', 'Có mặt', Accent.green),
  late('Late', 'M', 'Muộn', Accent.sky),
  absent('Absent', 'V', 'Vắng', Accent.coral);

  const Mark(this.value, this.short, this.label, this.accent);
  final String value;
  final String short;
  final String label;
  final Accent accent;

  static Mark? parse(String? v) => values.where((m) => m.value == v).firstOrNull;
}

/// Điểm danh một tiết (ATT-01): mục tiêu một lớp dưới 60 giây, một tay (Design_Mobile §5.1).
class AttendanceSheetPage extends ConsumerStatefulWidget {
  const AttendanceSheetPage({super.key, required this.sessionId});
  final int sessionId;

  @override
  ConsumerState<AttendanceSheetPage> createState() => _AttendanceSheetPageState();
}

class _AttendanceSheetPageState extends ConsumerState<AttendanceSheetPage> {
  /// Thay đổi chưa lưu, theo học sinh.
  final Map<int, Mark> _marks = {};
  final Map<int, String?> _notes = {};
  bool _saving = false;

  bool get _dirty => _marks.isNotEmpty || _notes.isNotEmpty;

  Mark? _markOf(SheetRow r) => _marks[r.studentId] ?? Mark.parse(r.status);
  String? _noteOf(SheetRow r) => _notes.containsKey(r.studentId) ? _notes[r.studentId] : r.note;

  void _set(SheetRow r, Mark m) {
    HapticFeedback.selectionClick();
    setState(() => _marks[r.studentId] = m);
  }

  void _allPresent(List<SheetRow> rows) {
    HapticFeedback.lightImpact();
    setState(() {
      for (final r in rows) {
        if (_markOf(r) == null) _marks[r.studentId] = Mark.present;
      }
    });
  }

  Future<void> _editNote(SheetRow r) async {
    final c = TextEditingController(text: _noteOf(r));
    final result = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, MediaQuery.viewInsetsOf(ctx).bottom + AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Ghi chú · ${r.fullName}', style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: c,
              autofocus: true,
              maxLength: 255,
              maxLines: 3,
              minLines: 2,
              decoration: const InputDecoration(hintText: 'vd. Đến muộn 10 phút'),
            ),
            const SizedBox(height: AppSpacing.sm),
            FilledButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('Xong')),
          ],
        ),
      ),
    );
    if (result != null) setState(() => _notes[r.studentId] = result.isEmpty ? null : result);
  }

  Future<void> _save(AttendanceSheet sheet) async {
    final items = <Map<String, dynamic>>[];
    for (final r in sheet.rows) {
      final m = _markOf(r);
      if (m == null) continue;
      items.add({'studentId': r.studentId, 'status': m.value, 'note': _noteOf(r)});
    }
    if (items.isEmpty) {
      toast(context, 'Chưa có học sinh nào được điểm danh. Chọn trạng thái cho học sinh rồi lưu.');
      return;
    }
    setState(() => _saving = true);
    try {
      await ref.read(attApiProvider).save(widget.sessionId, items);
      _marks.clear();
      _notes.clear();
      ref.invalidate(sheetProvider(widget.sessionId));
      ref.invalidate(todayAttendanceProvider);
      if (mounted) {
        final left = sheet.rows.length - items.length;
        toast(context, left == 0 ? 'Đã lưu điểm danh cả lớp.' : 'Đã lưu. Còn $left học sinh chưa điểm danh.');
      }
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final value = ref.watch(sheetProvider(widget.sessionId));
    return PopScope(
      canPop: !_dirty,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        final leave = await confirm(
          context,
          title: 'Chưa lưu điểm danh',
          message: 'Các thay đổi chưa lưu sẽ mất. Thoát mà không lưu?',
          ok: 'Thoát',
          danger: true,
        );
        if (leave && context.mounted) {
          setState(() {
            _marks.clear();
            _notes.clear();
          });
          Navigator.of(context).pop();
        }
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('Điểm danh')),
        body: AsyncView(
          value: value,
          onRetry: () => ref.invalidate(sheetProvider(widget.sessionId)),
          loading: const SkeletonList(count: 7),
          data: (sheet) => _body(sheet),
        ),
        bottomNavigationBar: value.hasValue && value.requireValue.readOnlyReason == null && value.requireValue.session.canWrite
            ? _saveBar(value.requireValue)
            : null,
      ),
    );
  }

  Widget _body(AttendanceSheet sheet) {
    final s = sheet.session;
    final readOnly = sheet.readOnlyReason != null || !s.canWrite;
    var present = 0, late = 0, absent = 0, none = 0;
    for (final r in sheet.rows) {
      switch (_markOf(r)) {
        case Mark.present:
          present++;
        case Mark.late:
          late++;
        case Mark.absent:
          absent++;
        case null:
          none++;
      }
    }
    return CustomScrollView(
      slivers: [
        SliverPersistentHeader(
          pinned: true,
          delegate: _StickyHeader(
            height: 150,
            child: Container(
              color: AppColor.bg,
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 8),
              child: AppCard(
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${s.subjectName} · Lớp ${s.className}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                    ),
                    Text(
                      '${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo} (${s.start}–${s.end}) · ${Fmt.longDate(s.date)}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 12.5, color: AppColor.text2),
                    ),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        _Count('Có mặt', present, Accent.green),
                        _Count('Muộn', late, Accent.sky),
                        _Count('Vắng', absent, Accent.coral),
                        _Count('Chưa', none, null),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
        if (readOnly)
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 8),
              child: Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: AppColor.neutralBg, borderRadius: BorderRadius.circular(AppRadius.md)),
                child: Row(
                  children: [
                    const Icon(LucideIcons.lock, size: 18, color: AppColor.text2),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        sheet.readOnlyReason ?? 'Tiết đã qua, không sửa được. Liên hệ quản trị để nhập bù.',
                        style: const TextStyle(color: AppColor.text2, fontSize: 13.5),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          )
        else if (none > 0)
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 8),
              child: OutlinedButton.icon(
                onPressed: () => _allPresent(sheet.rows),
                icon: const Icon(LucideIcons.checkCheck, size: 20),
                label: Text(none == sheet.rows.length ? 'Tất cả có mặt' : 'Còn lại có mặt ($none)'),
                style: OutlinedButton.styleFrom(
                  backgroundColor: AppColor.green50,
                  side: const BorderSide(color: AppColor.green200),
                ),
              ),
            ),
          ),
        if (sheet.rows.isEmpty)
          const SliverToBoxAdapter(child: EmptyState('Lớp chưa có học sinh nào vào ngày của tiết.'))
        else
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 24),
            sliver: SliverList.separated(
              itemCount: sheet.rows.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (_, i) {
                final r = sheet.rows[i];
                return _StudentRow(
                  row: r,
                  index: i + 1,
                  mark: _markOf(r),
                  note: _noteOf(r),
                  readOnly: readOnly,
                  onMark: (m) => _set(r, m),
                  onNote: () => _editNote(r),
                );
              },
            ),
          ),
      ],
    );
  }

  Widget _saveBar(AttendanceSheet sheet) {
    final marked = sheet.rows.where((r) => _markOf(r) != null).length;
    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 10, AppSpacing.screen, 10),
        decoration: const BoxDecoration(color: AppColor.surface, boxShadow: AppShadow.pop),
        child: FilledButton(
          onPressed: _saving ? null : () => _save(sheet),
          child: _saving
              ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2.4, color: Colors.white))
              : Text('Lưu · $marked/${sheet.rows.length}'),
        ),
      ),
    );
  }
}

class _Count extends StatelessWidget {
  const _Count(this.label, this.value, this.accent);
  final String label;
  final int value;
  final Accent? accent;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(
            '$value',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: accent?.fg ?? AppColor.text3),
          ),
          Text(label, style: const TextStyle(fontSize: 11.5, color: AppColor.text2)),
        ],
      ),
    );
  }
}

class _StudentRow extends StatelessWidget {
  const _StudentRow({
    required this.row,
    required this.index,
    required this.mark,
    required this.note,
    required this.readOnly,
    required this.onMark,
    required this.onNote,
  });

  final SheetRow row;
  final int index;
  final Mark? mark;
  final String? note;
  final bool readOnly;
  final ValueChanged<Mark> onMark;
  final VoidCallback onNote;

  @override
  Widget build(BuildContext context) {
    final excused = row.label == 'ExcusedAbsent';
    // Màn hẹp (360dp) bỏ avatar để tên dài còn chỗ hiển thị.
    final narrow = MediaQuery.sizeOf(context).width < 380;
    return Container(
      constraints: const BoxConstraints(minHeight: 68),
      padding: const EdgeInsets.fromLTRB(12, 8, 0, 8),
      decoration: BoxDecoration(color: AppColor.surface, borderRadius: BorderRadius.circular(AppRadius.card), boxShadow: AppShadow.card),
      child: Row(
        children: [
          if (!narrow) ...[PersonAvatar(row.fullName, size: 40), const SizedBox(width: 10)],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  row.fullName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500),
                ),
                Text(
                  [row.studentCode, if (excused) 'Có đơn nghỉ đã duyệt', if (note != null && note!.isNotEmpty) note!].join(' · '),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: excused ? AppColor.skyFg : AppColor.text3),
                ),
              ],
            ),
          ),
          const SizedBox(width: 6),
          Semantics(
            label: 'Trạng thái của ${row.fullName}',
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                for (final m in Mark.values) _MarkButton(mark: m, selected: mark == m, enabled: !readOnly, onTap: () => onMark(m)),
              ],
            ),
          ),
          if (!readOnly)
            IconButton(
              tooltip: 'Ghi chú',
              onPressed: onNote,
              icon: Icon(LucideIcons.pencil, size: 18, color: (note ?? '').isEmpty ? AppColor.text3 : AppColor.green700),
            )
          else
            const SizedBox(width: 8),
        ],
      ),
    );
  }
}

class _MarkButton extends StatelessWidget {
  const _MarkButton({required this.mark, required this.selected, required this.enabled, required this.onTap});
  final Mark mark;
  final bool selected;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: Semantics(
        button: true,
        selected: selected,
        label: mark.label,
        child: InkWell(
          onTap: enabled ? onTap : null,
          borderRadius: BorderRadius.circular(12),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 140),
            width: 40,
            height: 40,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: selected ? mark.accent.fg : (enabled ? mark.accent.bg : AppColor.neutralBg),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              mark.short,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 15,
                color: selected ? Colors.white : (enabled ? mark.accent.fg : AppColor.text3),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _StickyHeader extends SliverPersistentHeaderDelegate {
  _StickyHeader({required this.height, required this.child});
  final double height;
  final Widget child;

  @override
  double get minExtent => height;
  @override
  double get maxExtent => height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) => SizedBox(height: height, child: child);

  @override
  bool shouldRebuild(_StickyHeader old) => true;
}
