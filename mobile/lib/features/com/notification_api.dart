import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';

class NotificationItem {
  NotificationItem.fromJson(Json j)
    : id = j.integer('notificationId'),
      type = j.str('notificationType'),
      title = j.str('title'),
      body = j.strOrNull('body'),
      isRead = j.flag('isRead'),
      createdAt = j.date('createdAt');

  final int id;
  final String type;
  final String title;
  final String? body;
  final bool isRead;
  final DateTime createdAt;
}

class NotificationPage {
  NotificationPage(this.items, this.total, this.unread);
  final List<NotificationItem> items;
  final int total;
  final int unread;
}

class OpenResult {
  OpenResult.fromJson(Json j)
    : canAccess = j.flag('canAccess'),
      targetRoute = j.strOrNull('targetRoute'),
      reason = j.strOrNull('reasonNotAllowed');

  final bool canAccess;
  final String? targetRoute;
  final String? reason;
}

class NotificationApi {
  NotificationApi(this.ref);
  final Ref ref;

  Future<NotificationPage> notifications({int page = 1, int pageSize = 20}) async {
    final j = await ref.read(apiProvider).get<Json>('/api/com/notifications', query: {'page': page, 'pageSize': pageSize});
    return NotificationPage(j.list('items', NotificationItem.fromJson), j.integer('total'), j.integer('unreadCount'));
  }

  Future<int> unreadCount() async => (await ref.read(apiProvider).get<Json>('/api/com/notifications/unread-count')).integer('unreadCount');

  /// Mở thông báo: Backend kiểm lại quyền tại lúc mở và đánh dấu đã đọc (BR-26).
  Future<OpenResult> open(int id) async => OpenResult.fromJson(await ref.read(apiProvider).post<Json>('/api/com/notifications/$id/open'));

  Future<void> markAllRead() => ref.read(apiProvider).put<dynamic>('/api/com/notifications/read-all');
}

final notificationApiProvider = Provider<NotificationApi>(NotificationApi.new);

/// Số thông báo chưa đọc trên chuông; realtime làm mới khi có NotificationCreated.
final unreadNotificationsProvider = FutureProvider.autoDispose<int>((ref) => ref.read(notificationApiProvider).unreadCount());
