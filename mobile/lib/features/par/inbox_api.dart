import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';

class Conversation {
  Conversation.fromJson(Json j)
    : id = j.integer('conversationId'),
      studentId = j.integer('studentId'),
      studentName = j.str('studentName'),
      className = j.str('className'),
      staffId = j.integer('staffId'),
      staffName = j.str('staffName'),
      roleDescription = j.str('roleDescription'),
      parentId = j.integer('parentId'),
      parentName = j.str('parentName'),
      lastMessage = j.strOrNull('lastMessageContent'),
      lastMessageAt = j.dateOrNull('lastMessageAt'),
      unread = j.integer('unreadCount'),
      isReadOnly = j.flag('isReadOnly');

  final int id;
  final int studentId;
  final String studentName;
  final String className;
  final int staffId;
  final String staffName;
  final String roleDescription;
  final int parentId;
  final String parentName;
  final String? lastMessage;
  final DateTime? lastMessageAt;
  final int unread;
  final bool isReadOnly;

  /// Bên kia của hội thoại theo người đang xem.
  String counterpartName(int me) => me == staffId ? parentName : staffName;
  int counterpartId(int me) => me == staffId ? parentId : staffId;
  String counterpartRole(int me) => me == staffId ? 'Phụ huynh' : roleDescription;
}

class Message {
  Message({required this.id, required this.senderId, required this.content, required this.sentAt, required this.readAt});

  factory Message.fromJson(Json j) => Message(
    id: j.integer('messageId'),
    senderId: j.integer('senderPersonId'),
    content: j.str('content'),
    sentAt: j.date('sentAt'),
    readAt: j.dateOrNull('readAt'),
  );

  final int id;
  final int senderId;
  final String content;
  final DateTime sentAt;
  DateTime? readAt;
}

class InboxApi {
  InboxApi(this.ref);
  final Ref ref;

  Future<List<Message>> messages(int conversationId) async {
    final list = await ref.read(apiProvider).get<List<dynamic>>('/api/par/conversations/$conversationId/messages');
    return list.map((e) => Message.fromJson(e as Json)).toList();
  }

  Future<Message> send(int conversationId, String content) async => Message.fromJson(
    await ref.read(apiProvider).post<Json>('/api/par/conversations/$conversationId/messages', data: {'content': content}),
  );

  Future<int> open({required int studentId, int? staffId, int? parentId}) async {
    final r = await ref
        .read(apiProvider)
        .post<dynamic>('/api/par/conversations', data: {'studentId': studentId, 'staffId': staffId, 'parentId': parentId});
    return (r as num).toInt();
  }

  Future<List<Json>> availableTeachers(int studentId) async =>
      (await ref.read(apiProvider).get<List<dynamic>>('/api/par/conversations/available-teachers', query: {'studentId': studentId}))
          .cast<Json>();

  Future<List<Json>> teachingStudents() async =>
      (await ref.read(apiProvider).get<List<dynamic>>('/api/par/conversations/my-teaching-students')).cast<Json>();

  Future<List<Json>> availableParents(int studentId) async =>
      (await ref.read(apiProvider).get<List<dynamic>>('/api/par/conversations/available-parents', query: {'studentId': studentId}))
          .cast<Json>();
}

final inboxApiProvider = Provider<InboxApi>(InboxApi.new);

final conversationsProvider = FutureProvider.autoDispose<List<Conversation>>((ref) async {
  ref.watch(currentUserProvider);
  final list = await ref.read(apiProvider).get<List<dynamic>>('/api/par/conversations');
  return list.map((e) => Conversation.fromJson(e as Json)).toList()
    ..sort((a, b) => (b.lastMessageAt ?? DateTime(2000)).compareTo(a.lastMessageAt ?? DateTime(2000)));
});

/// Tổng tin nhắn chưa đọc (ô Tin nhắn trên trang chủ giáo viên).
final unreadMessagesProvider = FutureProvider.autoDispose<int>((ref) async {
  ref.watch(currentUserProvider);
  final r = await ref.read(apiProvider).get<dynamic>('/api/par/conversations/unread-count');
  return (r as num).toInt();
});
