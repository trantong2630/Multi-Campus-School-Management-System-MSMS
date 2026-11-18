import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/format.dart';
import '../../core/http/api_client.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';
import 'notification_api.dart';

/// Đường dẫn web Backend trả khi mở thông báo → màn tương ứng trên mobile.
/// Null: nội dung chỉ có trên web.
String? mobileRouteFor(String webRoute) {
  final uri = Uri.parse(webRoute);
  switch (uri.path) {
    case '/scd/my-class':
    case '/scd/schedule':
      return '/schedule';
    case '/lrn/todo':
      return '/third';
    case '/lrn/children':
      return '/results';
    case '/par/inbox':
      final id = uri.queryParameters['conversationId'];
      return id == null ? '/third' : '/inbox/$id';
  }
  return null;
}

const _typeIcon = {
  'TimetablePublished': (LucideIcons.calendarDays, Accent.green),
  'SessionPending': (LucideIcons.calendarClock, Accent.amber),
  'ScheduleChanged': (LucideIcons.calendarSync, Accent.sky),
  'AssignmentAssigned': (LucideIcons.bookOpen, Accent.violet),
  'MessageReceived': (LucideIcons.messageCircle, Accent.teal),
};

/// Trung tâm thông báo (COM-01): bấm để mở, Backend kiểm lại quyền tại lúc mở (BR-26).
class NotificationsPage extends ConsumerStatefulWidget {
  const NotificationsPage({super.key});

  @override
  ConsumerState<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends ConsumerState<NotificationsPage> {
  final List<NotificationItem> _items = [];
  int _page = 0;
  int _total = 0;
  int _unread = 0;
  bool _loading = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  Future<void> _reload() async {
    _items.clear();
    _page = 0;
    await _more();
  }

  Future<void> _more() async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final p = await ref.read(notificationApiProvider).notifications(page: _page + 1);
      setState(() {
        _page++;
        _items.addAll(p.items);
        _total = p.total;
        _unread = p.unread;
      });
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(NotificationItem n) async {
    try {
      final r = await ref.read(notificationApiProvider).open(n.id);
      ref.invalidate(unreadNotificationsProvider);
      if (!mounted) return;
      if (!r.canAccess) {
        toast(context, r.reason ?? 'Bạn không còn quyền xem nội dung này.');
      } else {
        final route = r.targetRoute == null ? null : mobileRouteFor(r.targetRoute!);
        if (route == null) {
          toast(context, 'Nội dung này xem trên web.');
        } else if (route == '/schedule' || route == '/third') {
          context.go(route);
        } else {
          context.push(route);
        }
      }
      _reload();
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    }
  }

  Future<void> _readAll() async {
    try {
      await ref.read(notificationApiProvider).markAllRead();
      ref.invalidate(unreadNotificationsProvider);
      _reload();
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    }
  }

  @override
  Widget build(BuildContext context) {
    return PageScaffold(
      title: 'Thông báo',
      actions: [if (_unread > 0) TextButton(onPressed: _readAll, child: const Text('Đọc hết'))],
      body: _error != null && _items.isEmpty
          ? ErrorState(_error!, onRetry: _reload)
          : _loading && _items.isEmpty
          ? const SkeletonList(count: 6, height: 76)
          : Refreshable(
              onRefresh: _reload,
              child: _items.isEmpty
                  ? ListView(children: const [EmptyState('Chưa có thông báo nào.')])
                  : ListView(
                      padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 32),
                      children: [
                        for (final n in _items)
                          Padding(padding: const EdgeInsets.only(bottom: 10), child: _NotificationTile(n, () => _open(n))),
                        if (_items.length < _total)
                          Center(
                            child: TextButton(onPressed: _loading ? null : _more, child: Text(_loading ? 'Đang tải…' : 'Xem thêm')),
                          ),
                      ],
                    ),
            ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile(this.n, this.onTap);
  final NotificationItem n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final (icon, accent) = _typeIcon[n.type] ?? (LucideIcons.bell, Accent.green);
    return AppCard(
      onTap: onTap,
      color: n.isRead ? null : AppColor.green50,
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(9),
            decoration: BoxDecoration(color: accent.bg, borderRadius: BorderRadius.circular(12)),
            child: Icon(icon, size: 20, color: accent.fg),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(n.title, style: TextStyle(fontSize: 14.5, fontWeight: n.isRead ? FontWeight.w500 : FontWeight.w700)),
                if ((n.body ?? '').isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(
                      n.body!,
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 13, color: AppColor.text2, height: 1.4),
                    ),
                  ),
                const SizedBox(height: 4),
                Text(
                  '${notificationTypeLabel[n.type] ?? 'Thông báo'} · ${Fmt.ago(n.createdAt)}',
                  style: const TextStyle(fontSize: 11.5, color: AppColor.text3),
                ),
              ],
            ),
          ),
          if (!n.isRead)
            Container(
              margin: const EdgeInsets.only(left: 8, top: 4),
              width: 9,
              height: 9,
              decoration: const BoxDecoration(color: AppColor.coralFg, shape: BoxShape.circle),
            ),
        ],
      ),
    );
  }
}
