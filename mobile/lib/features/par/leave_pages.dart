import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/http/api_client.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/auth_scaffold.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';
import 'children.dart';
import 'par_api.dart';

String leaveDates(LeaveItem l) => isSameDay(l.fromDate, l.toDate)
    ? '${Fmt.weekday(l.fromDate)}, ${Fmt.date(l.fromDate)}'
    : '${Fmt.date(l.fromDate)} – ${Fmt.date(l.toDate)}';

/// Đơn xin nghỉ (PAR-01): phụ huynh xem lịch sử đơn của con; chủ nhiệm xem hộp đơn của lớp.
/// Danh sách phân trang bằng nút "Xem thêm" (Design_Mobile §6).
class LeaveListPage extends ConsumerStatefulWidget {
  const LeaveListPage({super.key});

  @override
  ConsumerState<LeaveListPage> createState() => _LeaveListPageState();
}

class _LeaveListPageState extends ConsumerState<LeaveListPage> {
  final List<LeaveItem> _items = [];
  int _page = 0;
  int _total = 0;
  bool _loading = false;
  Object? _error;
  bool _pendingOnly = false;
  int? _childId;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  Future<void> _reload() async {
    _items.clear();
    _page = 0;
    _total = 0;
    await _more();
  }

  Future<void> _more() async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final user = ref.read(currentUserProvider);
      final api = ref.read(parApiProvider);
      final LeavePage p;
      if (user.isParent) {
        final child = await ref.read(currentChildProvider.future);
        if (child == null) {
          setState(() => _loading = false);
          return;
        }
        _childId = child.studentId;
        p = await api.childLeaves(child.studentId, _page + 1);
      } else {
        p = await api.homeroomLeaves(_page + 1);
      }
      setState(() {
        _page++;
        _total = p.total;
        _items.addAll(p.items);
      });
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(LeaveItem l) async {
    final changed = await context.push<bool>('/leave/detail', extra: l);
    if (changed == true) {
      ref.invalidate(pendingHomeroomLeavesProvider);
      _reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    if (user.isParent) {
      // Đổi con thì tải lại danh sách đơn của con đó.
      ref.listen(currentChildProvider, (_, next) {
        final id = next.value?.studentId;
        if (id != null && id != _childId) _reload();
      });
    }
    final items = _pendingOnly ? _items.where((i) => i.isPending).toList() : _items;
    return PageScaffold(
      title: 'Đơn xin nghỉ',
      floating: user.isParent
          ? FloatingActionButton.extended(
              backgroundColor: AppColor.green600,
              foregroundColor: Colors.white,
              onPressed: () async {
                final ok = await context.push<bool>('/leave/new');
                if (ok == true) _reload();
              },
              icon: const Icon(LucideIcons.plus),
              label: const Text('Xin nghỉ'),
            )
          : null,
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
            child: Row(
              children: [
                if (user.isParent) const ChildChip(),
                const Spacer(),
                FilterChip(
                  label: const Text('Chờ duyệt'),
                  selected: _pendingOnly,
                  showCheckmark: false,
                  selectedColor: AppColor.amberBg,
                  side: const BorderSide(color: AppColor.border),
                  onSelected: (v) => setState(() => _pendingOnly = v),
                ),
              ],
            ),
          ),
          Expanded(
            child: _error != null && _items.isEmpty
                ? ErrorState(_error!, onRetry: _reload)
                : _loading && _items.isEmpty
                ? const SkeletonList(count: 5, height: 96)
                : Refreshable(
                    onRefresh: _reload,
                    child: items.isEmpty
                        ? ListView(
                            children: [
                              EmptyState(
                                _pendingOnly
                                    ? 'Không có đơn nào đang chờ duyệt.'
                                    : (user.isParent ? 'Chưa có đơn xin nghỉ nào.' : 'Lớp chủ nhiệm chưa có đơn xin nghỉ nào.'),
                                actionLabel: _pendingOnly && _items.length < _total ? 'Xem thêm đơn cũ' : null,
                                onAction: _more,
                              ),
                            ],
                          )
                        : ListView(
                            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 96),
                            children: [
                              for (final l in items)
                                Padding(
                                  padding: const EdgeInsets.only(bottom: AppSpacing.md),
                                  child: _LeaveCard(l, showStudent: !user.isParent, onTap: () => _open(l)),
                                ),
                              if (_items.length < _total)
                                Center(
                                  child: TextButton(onPressed: _loading ? null : _more, child: Text(_loading ? 'Đang tải…' : 'Xem thêm')),
                                ),
                            ],
                          ),
                  ),
          ),
        ],
      ),
    );
  }
}

class _LeaveCard extends StatelessWidget {
  const _LeaveCard(this.l, {required this.showStudent, required this.onTap});
  final LeaveItem l;
  final bool showStudent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (showStudent) ...[PersonAvatar(l.studentName, size: 40), const SizedBox(width: 12)],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (showStudent)
                  Text(
                    l.studentName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                  ),
                Text(
                  leaveDates(l),
                  style: TextStyle(fontWeight: showStudent ? FontWeight.w400 : FontWeight.w600, fontSize: showStudent ? 13.5 : 15),
                ),
                Text(leaveScopeLabel(l.sessionOfDay, l.slotIds), style: const TextStyle(fontSize: 12.5, color: AppColor.text2)),
                if ((l.reason ?? '').isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      '“${l.reason}”',
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 13, color: AppColor.text2),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          StatusPill(StatusDomain.leave, l.status, dense: true),
        ],
      ),
    );
  }
}

/// Chi tiết đơn: phụ huynh rút đơn đang chờ; chủ nhiệm duyệt hoặc từ chối (từ chối phải ghi lý do).
class LeaveDetailPage extends ConsumerStatefulWidget {
  const LeaveDetailPage({super.key, required this.leave});
  final LeaveItem leave;

  @override
  ConsumerState<LeaveDetailPage> createState() => _LeaveDetailPageState();
}

class _LeaveDetailPageState extends ConsumerState<LeaveDetailPage> {
  bool _busy = false;

  Future<void> _act(Future<void> Function() action, String done) async {
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      toast(context, done);
      context.pop(true);
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reject() async {
    final c = TextEditingController();
    final note = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, MediaQuery.viewInsetsOf(ctx).bottom + AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Lý do từ chối', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            const Text('Phụ huynh sẽ thấy lý do này trong lịch sử đơn.', style: TextStyle(color: AppColor.text2, fontSize: 13)),
            const SizedBox(height: AppSpacing.md),
            TextField(controller: c, autofocus: true, maxLines: 3, minLines: 2, maxLength: 500),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: AppColor.danger),
              onPressed: () {
                if (c.text.trim().isEmpty) return;
                Navigator.pop(ctx, c.text.trim());
              },
              child: const Text('Từ chối đơn'),
            ),
          ],
        ),
      ),
    );
    if (note == null) return;
    await _act(() => ref.read(parApiProvider).reject(widget.leave.id, note), 'Đã từ chối đơn.');
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final l = widget.leave;
    final canDecide = user.isTeacher && l.isPending;
    final canWithdraw = user.isParent && l.isPending;
    return PageScaffold(
      title: 'Đơn xin nghỉ',
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.screen),
        children: [
          AppCard(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    PersonAvatar(l.studentName, size: 48),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(l.studentName, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
                          Text(
                            ['Lớp ${l.className}', if (l.studentCode != null) l.studentCode!].join(' · '),
                            style: const TextStyle(color: AppColor.text2, fontSize: 13),
                          ),
                        ],
                      ),
                    ),
                    StatusPill(StatusDomain.leave, l.status),
                  ],
                ),
                const Divider(height: 28),
                _Line('Ngày nghỉ', leaveDates(l)),
                _Line('Phạm vi', leaveScopeLabel(l.sessionOfDay, l.slotIds)),
                _Line('Lý do', (l.reason ?? '').isEmpty ? 'Không ghi lý do' : l.reason!),
                if (l.parentName != null) _Line('Người gửi', l.parentName!),
                _Line('Gửi lúc', Fmt.dateTime(l.createdAt)),
                if (l.decidedAt != null)
                  _Line(l.status == 'Withdrawn' ? 'Rút lúc' : 'Xử lý', '${l.decidedBy ?? ''} · ${Fmt.dateTime(l.decidedAt!)}'),
                if ((l.decisionNote ?? '').isNotEmpty) _Line('Lý do từ chối', l.decisionNote!),
              ],
            ),
          ),
          if (canDecide) ...[
            const SizedBox(height: AppSpacing.lg),
            const InlineAlert(
              'Duyệt đơn thì các tiết vắng trong khoảng này tự hiện "Vắng có phép" khi điểm danh.',
              tone: Accent.sky,
              icon: LucideIcons.info,
            ),
          ],
        ],
      ),
      bottom: canDecide || canWithdraw
          ? SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 8, AppSpacing.screen, 12),
                child: canDecide
                    ? Row(
                        children: [
                          Expanded(
                            child: OutlinedButton(
                              onPressed: _busy ? null : _reject,
                              style: OutlinedButton.styleFrom(foregroundColor: AppColor.danger),
                              child: const Text('Từ chối'),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: BusyButton(
                              label: 'Duyệt',
                              busy: _busy,
                              onPressed: () => _act(() => ref.read(parApiProvider).approve(l.id), 'Đã duyệt đơn.'),
                            ),
                          ),
                        ],
                      )
                    : TextButton(
                        onPressed: _busy
                            ? null
                            : () async {
                                final ok = await confirm(
                                  context,
                                  title: 'Rút đơn?',
                                  message: 'Đơn sẽ chuyển sang Đã rút và không được xét duyệt nữa.',
                                  ok: 'Rút đơn',
                                  danger: true,
                                );
                                if (ok) await _act(() => ref.read(parApiProvider).withdraw(l.id), 'Đã rút đơn.');
                              },
                        style: TextButton.styleFrom(foregroundColor: AppColor.danger),
                        child: const Text('Rút đơn'),
                      ),
              ),
            )
          : null,
    );
  }
}

class _Line extends StatelessWidget {
  const _Line(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 104,
            child: Text(label, style: const TextStyle(color: AppColor.text3, fontSize: 13.5)),
          ),
          Expanded(child: Text(value, style: const TextStyle(fontSize: 14.5))),
        ],
      ),
    );
  }
}

/// Gửi đơn xin nghỉ: một màn, không wizard (Design_Mobile §5.4).
class LeaveFormPage extends ConsumerStatefulWidget {
  const LeaveFormPage({super.key});

  @override
  ConsumerState<LeaveFormPage> createState() => _LeaveFormPageState();
}

class _LeaveFormPageState extends ConsumerState<LeaveFormPage> {
  late DateTime _from = dateOnly(DateTime.now());
  late DateTime _to = _from;
  String _session = 'AllDay';
  final _reason = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<DateTime?> _pick(DateTime initial, DateTime first) => showDatePicker(
    context: context,
    initialDate: initial.isBefore(first) ? first : initial,
    firstDate: first,
    lastDate: first.add(const Duration(days: 180)),
    locale: const Locale('vi'),
    helpText: 'Chọn ngày',
    cancelText: 'Hủy',
    confirmText: 'Chọn',
  );

  Future<void> _submit(Child child) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(parApiProvider)
          .submitLeave(studentId: child.studentId, from: _from, to: _to, sessionOfDay: _session, reason: _reason.text);
      if (!mounted) return;
      toast(context, 'Đã gửi đơn. Giáo viên chủ nhiệm sẽ xem và duyệt.');
      context.pop(true);
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final child = ref.watch(currentChildProvider);
    final today = dateOnly(DateTime.now());
    return PageScaffold(
      title: 'Xin nghỉ cho con',
      body: AsyncView(
        value: child,
        onRetry: () => ref.invalidate(currentChildProvider),
        data: (c) {
          if (c == null) return const EmptyState('Chưa có học sinh nào được liên kết với tài khoản.');
          return ListView(
            padding: const EdgeInsets.all(AppSpacing.screen),
            children: [
              const FieldLabel(
                'Con',
                child: Align(alignment: Alignment.centerLeft, child: ChildChip()),
              ),
              Row(
                children: [
                  Expanded(
                    child: FieldLabel(
                      'Từ ngày',
                      child: _DateBox(_from, () async {
                        final d = await _pick(_from, today);
                        if (d == null) return;
                        setState(() {
                          _from = d;
                          if (_to.isBefore(d)) _to = d;
                        });
                      }),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FieldLabel(
                      'Đến ngày',
                      child: _DateBox(_to, () async {
                        final d = await _pick(_to, _from);
                        if (d != null) setState(() => _to = d);
                      }),
                    ),
                  ),
                ],
              ),
              const Padding(
                padding: EdgeInsets.only(bottom: AppSpacing.lg),
                child: Text(
                  'Chỉ xin nghỉ được từ hôm nay trở đi; ngày đã qua không chọn được.',
                  style: TextStyle(fontSize: 12.5, color: AppColor.text3),
                ),
              ),
              FieldLabel(
                'Buổi',
                child: Wrap(
                  spacing: 8,
                  children: [
                    for (final (v, l) in const [('AllDay', 'Cả ngày'), ('Morning', 'Sáng'), ('Afternoon', 'Chiều')])
                      ChoiceChip(
                        label: Text(l),
                        selected: _session == v,
                        showCheckmark: false,
                        selectedColor: AppColor.green100,
                        labelStyle: TextStyle(color: _session == v ? AppColor.green800 : AppColor.text2, fontWeight: FontWeight.w600),
                        side: const BorderSide(color: AppColor.border),
                        onSelected: (_) => setState(() => _session = v),
                      ),
                  ],
                ),
              ),
              FieldLabel(
                'Lý do',
                child: TextField(
                  controller: _reason,
                  maxLines: 4,
                  minLines: 3,
                  maxLength: 500,
                  decoration: const InputDecoration(hintText: 'vd. Con bị sốt, cần nghỉ ở nhà theo dõi'),
                ),
              ),
              if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
              BusyButton(label: 'Gửi đơn', busy: _busy, onPressed: () => _submit(c)),
            ],
          );
        },
      ),
    );
  }
}

class _DateBox extends StatelessWidget {
  const _DateBox(this.date, this.onTap);
  final DateTime date;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppRadius.field),
      child: Container(
        height: 52,
        padding: const EdgeInsets.symmetric(horizontal: 14),
        decoration: BoxDecoration(
          color: AppColor.surface,
          border: Border.all(color: AppColor.fieldBorder),
          borderRadius: BorderRadius.circular(AppRadius.field),
        ),
        child: Row(
          children: [
            const Icon(LucideIcons.calendar, size: 18, color: AppColor.green700),
            const SizedBox(width: 10),
            Expanded(
              child: Text(Fmt.date(date), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500)),
            ),
          ],
        ),
      ),
    );
  }
}
