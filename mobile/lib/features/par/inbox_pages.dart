import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/http/api_client.dart';
import '../../core/json.dart';
import '../../core/realtime.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/hero_header.dart';
import '../../core/widgets/states.dart';
import 'children.dart';
import 'inbox_api.dart';

/// Hộp thư (PAR-03): hội thoại 1–1 giữa phụ huynh và giáo viên về một học sinh.
class InboxPage extends ConsumerStatefulWidget {
  const InboxPage({super.key});

  @override
  ConsumerState<InboxPage> createState() => _InboxPageState();
}

class _InboxPageState extends ConsumerState<InboxPage> {
  StreamSubscription<RealtimeEvent>? _sub;

  @override
  void initState() {
    super.initState();
    _sub = ref.read(realtimeProvider).events.listen((e) {
      if (e is MessageEvent || e is ReadEvent || e is ResyncEvent) {
        ref.invalidate(conversationsProvider);
        ref.invalidate(unreadMessagesProvider);
      }
    });
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final me = ref.watch(currentUserProvider);
    final value = ref.watch(conversationsProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hộp thư'),
        actions: const [Padding(padding: EdgeInsets.only(right: 8), child: NotificationBell(onLight: true))],
      ),
      floatingActionButton: Padding(
        padding: const EdgeInsets.only(bottom: 96),
        child: FloatingActionButton.extended(
          backgroundColor: AppColor.green600,
          foregroundColor: Colors.white,
          onPressed: () => context.push('/new-message'),
          icon: const Icon(LucideIcons.squarePen),
          label: const Text('Tin nhắn mới'),
        ),
      ),
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(conversationsProvider),
        loading: const SkeletonList(count: 6, height: 76),
        data: (list) => Refreshable(
          onRefresh: () => ref.refresh(conversationsProvider.future),
          child: list.isEmpty
              ? ListView(
                  children: [
                    EmptyState(
                      me.isParent
                          ? 'Chưa có hội thoại nào. Nhắn cho giáo viên chủ nhiệm hoặc giáo viên bộ môn của con.'
                          : 'Chưa có hội thoại nào với phụ huynh.',
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 180),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 10),
                  itemBuilder: (_, i) => _ConversationTile(list[i], me.personId),
                ),
        ),
      ),
    );
  }
}

class _ConversationTile extends StatelessWidget {
  const _ConversationTile(this.c, this.me);
  final Conversation c;
  final int me;

  @override
  Widget build(BuildContext context) {
    final name = c.counterpartName(me);
    final unread = c.unread > 0;
    return AppCard(
      onTap: () => context.push('/inbox/${c.id}'),
      padding: const EdgeInsets.fromLTRB(12, 12, 14, 12),
      child: Row(
        children: [
          PersonAvatar(name, size: 46),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 15, fontWeight: unread ? FontWeight.w700 : FontWeight.w600),
                      ),
                    ),
                    if (c.lastMessageAt != null)
                      Text(Fmt.ago(c.lastMessageAt!), style: TextStyle(fontSize: 11.5, color: unread ? AppColor.green700 : AppColor.text3)),
                  ],
                ),
                Text(
                  'về ${c.studentName} · ${c.counterpartRole(me)}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 12, color: AppColor.text2),
                ),
                const SizedBox(height: 2),
                Row(
                  children: [
                    if (c.isReadOnly)
                      const Padding(
                        padding: EdgeInsets.only(right: 4),
                        child: Icon(LucideIcons.lock, size: 12, color: AppColor.text3),
                      ),
                    Expanded(
                      child: Text(
                        c.lastMessage ?? 'Chưa có tin nhắn',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 13.5,
                          color: unread ? AppColor.text1 : AppColor.text3,
                          fontWeight: unread ? FontWeight.w600 : null,
                        ),
                      ),
                    ),
                    if (unread)
                      Container(
                        margin: const EdgeInsets.only(left: 8),
                        width: 10,
                        height: 10,
                        decoration: const BoxDecoration(color: AppColor.green500, shape: BoxShape.circle),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Màn chat: bong bóng bo 16, của mình nền green600 chữ trắng; đếm ký tự khi quá 1.800/2.000 (Design_Mobile §5.5).
class ChatPage extends ConsumerStatefulWidget {
  const ChatPage({super.key, required this.conversationId});
  final int conversationId;

  @override
  ConsumerState<ChatPage> createState() => _ChatPageState();
}

class _ChatPageState extends ConsumerState<ChatPage> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  List<Message>? _messages;
  Object? _error;
  bool _sending = false;
  bool _partnerTyping = false;
  bool? _partnerOnline;
  StreamSubscription<RealtimeEvent>? _sub;
  Timer? _typingOff;
  Timer? _partnerTypingOff;
  Timer? _poll;
  bool _typingSent = false;

  int get _id => widget.conversationId;

  @override
  void initState() {
    super.initState();
    _load();
    final rt = ref.read(realtimeProvider);
    _join();
    _sub = rt.events.listen(_onEvent);
    // Dự phòng khi realtime không dựng được: hỏi lại mỗi 8 giây.
    _poll = Timer.periodic(const Duration(seconds: 8), (_) {
      if (!ref.read(realtimeProvider).connected) _load(silent: true);
    });
    _input.addListener(_onInput);
  }

  Future<void> _join() async {
    final online = await ref.read(realtimeProvider).joinConversation(_id);
    if (mounted && online != null) setState(() => _partnerOnline = online);
  }

  @override
  void dispose() {
    if (_typingSent) ref.read(realtimeProvider).sendTyping(_id, false);
    _sub?.cancel();
    _typingOff?.cancel();
    _partnerTypingOff?.cancel();
    _poll?.cancel();
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    try {
      final list = await ref.read(inboxApiProvider).messages(_id);
      if (!mounted) return;
      setState(() {
        _messages = list;
        _error = null;
      });
      ref.invalidate(unreadMessagesProvider);
      ref.invalidate(conversationsProvider);
    } catch (e) {
      if (!silent && mounted) setState(() => _error = e);
    }
  }

  void _onEvent(RealtimeEvent e) {
    final me = ref.read(currentUserProvider).personId;
    switch (e) {
      case MessageEvent(:final data) when e.conversationId == _id:
        final m = Message.fromJson(data);
        if (_messages?.any((x) => x.id == m.id) ?? false) return;
        setState(() {
          _messages = [...?_messages, m];
          if (m.senderId != me) _partnerTyping = false;
        });
        // Đang mở hội thoại: tải lại để Backend ghi đã xem và báo cho bên gửi.
        if (m.senderId != me) _load(silent: true);
      case ReadEvent(:final conversationId, :final readAt) when conversationId == _id:
        setState(() {
          for (final m in _messages ?? const <Message>[]) {
            if (m.senderId == me && m.readAt == null) m.readAt = readAt;
          }
        });
      case TypingEvent(:final conversationId, :final personId, :final isTyping) when conversationId == _id && personId != me:
        _partnerTypingOff?.cancel();
        setState(() => _partnerTyping = isTyping);
        if (isTyping) _partnerTypingOff = Timer(const Duration(seconds: 6), () => mounted ? setState(() => _partnerTyping = false) : null);
      case PresenceEvent(:final isOnline):
        setState(() => _partnerOnline = isOnline);
      case ResyncEvent():
        _load(silent: true);
        _join();
      default:
        break;
    }
  }

  void _onInput() {
    setState(() {});
    final rt = ref.read(realtimeProvider);
    if (_input.text.isNotEmpty && !_typingSent) {
      _typingSent = true;
      rt.sendTyping(_id, true);
    }
    _typingOff?.cancel();
    _typingOff = Timer(const Duration(seconds: 3), () {
      if (_typingSent) {
        _typingSent = false;
        rt.sendTyping(_id, false);
      }
    });
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      final m = await ref.read(inboxApiProvider).send(_id, text);
      _input.clear();
      _typingSent = false;
      ref.read(realtimeProvider).sendTyping(_id, false);
      setState(() {
        if (!(_messages?.any((x) => x.id == m.id) ?? false)) _messages = [...?_messages, m];
      });
      ref.invalidate(conversationsProvider);
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final me = ref.watch(currentUserProvider).personId;
    final conv = ref.watch(conversationsProvider).value?.where((c) => c.id == _id).firstOrNull;
    final name = conv?.counterpartName(me) ?? 'Hội thoại';
    return Scaffold(
      appBar: AppBar(
        titleSpacing: 0,
        title: Row(
          children: [
            PersonAvatar(name, size: 38),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                  ),
                  Text(
                    _partnerTyping
                        ? 'Đang nhập…'
                        : [if (conv != null) 'về ${conv.studentName}', if (_partnerOnline == true) 'Đang trực tuyến'].join(' · '),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 12, color: _partnerTyping || _partnerOnline == true ? AppColor.green700 : AppColor.text3),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          Expanded(child: _list(me)),
          if (conv?.isReadOnly ?? false)
            SafeArea(
              top: false,
              child: Container(
                width: double.infinity,
                margin: const EdgeInsets.all(AppSpacing.screen),
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(color: AppColor.neutralBg, borderRadius: BorderRadius.circular(AppRadius.md)),
                child: const Text(
                  'Hội thoại đã kết thúc, chỉ xem lại.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: AppColor.text2),
                ),
              ),
            )
          else
            _composer(),
        ],
      ),
    );
  }

  Widget _list(int me) {
    if (_messages == null) {
      return _error != null ? ErrorState(_error!, onRetry: _load) : const SkeletonList(count: 6, height: 52);
    }
    final msgs = _messages!;
    if (msgs.isEmpty) return const EmptyState('Chưa có tin nhắn. Gửi lời chào để bắt đầu.');
    final lastMineRead = msgs.lastIndexWhere((m) => m.senderId == me && m.readAt != null);
    return ListView.builder(
      controller: _scroll,
      reverse: true,
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
      itemCount: msgs.length + (_partnerTyping ? 1 : 0),
      itemBuilder: (_, i) {
        if (_partnerTyping && i == 0) return const _TypingBubble();
        final idx = msgs.length - 1 - (i - (_partnerTyping ? 1 : 0));
        final m = msgs[idx];
        final mine = m.senderId == me;
        final prev = idx > 0 ? msgs[idx - 1] : null;
        final newDay = prev == null || !isSameDay(prev.sentAt, m.sentAt);
        return Column(
          children: [
            if (newDay)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 10),
                child: Text(
                  isSameDay(m.sentAt, DateTime.now()) ? 'Hôm nay' : Fmt.longDate(m.sentAt),
                  style: const TextStyle(fontSize: 12, color: AppColor.text3),
                ),
              ),
            _Bubble(m, mine: mine, showSeen: mine && idx == lastMineRead),
          ],
        );
      },
    );
  }

  Widget _composer() {
    final len = _input.text.length;
    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.fromLTRB(12, 8, 8, 8),
        decoration: const BoxDecoration(
          color: AppColor.surface,
          border: Border(top: BorderSide(color: AppColor.border)),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  TextField(
                    controller: _input,
                    minLines: 1,
                    maxLines: 5,
                    maxLength: 2000,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: InputDecoration(
                      hintText: 'Nhắn tin…',
                      counterText: '',
                      filled: true,
                      fillColor: AppColor.sunken,
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(22), borderSide: BorderSide.none),
                      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(22), borderSide: BorderSide.none),
                      focusedBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(22),
                        borderSide: const BorderSide(color: AppColor.green300),
                      ),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                    ),
                  ),
                  if (len > 1800)
                    Padding(
                      padding: const EdgeInsets.only(top: 2, right: 8),
                      child: Text('$len/2000', style: TextStyle(fontSize: 11.5, color: len >= 2000 ? AppColor.danger : AppColor.text3)),
                    ),
                ],
              ),
            ),
            const SizedBox(width: 6),
            IconButton.filled(
              tooltip: 'Gửi',
              onPressed: _input.text.trim().isEmpty || _sending ? null : _send,
              style: IconButton.styleFrom(
                backgroundColor: AppColor.green600,
                disabledBackgroundColor: AppColor.green200,
                minimumSize: const Size(48, 48),
              ),
              icon: const Icon(LucideIcons.sendHorizontal, color: Colors.white, size: 20),
            ),
          ],
        ),
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble(this.m, {required this.mine, required this.showSeen});
  final Message m;
  final bool mine;
  final bool showSeen;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * .78),
        child: Column(
          crossAxisAlignment: mine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
          children: [
            Container(
              margin: const EdgeInsets.symmetric(vertical: 3),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: mine ? AppColor.green600 : AppColor.bubbleOther,
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(16),
                  topRight: const Radius.circular(16),
                  bottomLeft: Radius.circular(mine ? 16 : 4),
                  bottomRight: Radius.circular(mine ? 4 : 16),
                ),
              ),
              child: Text(m.content, style: TextStyle(color: mine ? Colors.white : AppColor.text1, fontSize: 15, height: 1.4)),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(
                showSeen ? '${Fmt.time(m.sentAt)} · Đã xem' : Fmt.time(m.sentAt),
                style: const TextStyle(fontSize: 11, color: AppColor.text3),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TypingBubble extends StatelessWidget {
  const _TypingBubble();

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(color: AppColor.bubbleOther, borderRadius: BorderRadius.circular(16)),
        child: const Text(
          '• • •',
          style: TextStyle(color: AppColor.text3, fontWeight: FontWeight.w700, letterSpacing: 2),
        ),
      ),
    );
  }
}

/// Tạo hội thoại mới: phụ huynh chọn giáo viên của con; giáo viên chọn học sinh rồi phụ huynh (BR-25).
class NewConversationPage extends ConsumerStatefulWidget {
  const NewConversationPage({super.key});

  @override
  ConsumerState<NewConversationPage> createState() => _NewConversationPageState();
}

class _NewConversationPageState extends ConsumerState<NewConversationPage> {
  Json? _student; // giáo viên: học sinh đang chọn
  String _q = '';
  bool _busy = false;

  // Giữ future để gõ tìm kiếm không gọi lại API.
  final Map<int, Future<List<Json>>> _teachers = {};
  final Map<int, Future<List<Json>>> _parents = {};
  Future<List<Json>>? _students;

  Future<void> _open({required int studentId, int? staffId, int? parentId}) async {
    setState(() => _busy = true);
    try {
      final id = await ref.read(inboxApiProvider).open(studentId: studentId, staffId: staffId, parentId: parentId);
      ref.invalidate(conversationsProvider);
      if (mounted) context.pushReplacement('/inbox/$id');
    } catch (e) {
      if (mounted) toast(context, errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    return PageScaffold(
      title: 'Tin nhắn mới',
      body: AbsorbPointer(absorbing: _busy, child: user.isParent ? _parent() : _teacher()),
    );
  }

  Widget _parent() {
    final child = ref.watch(currentChildProvider).value;
    if (child == null) return const SkeletonList(count: 4);
    return FutureBuilder<List<Json>>(
      key: ValueKey(child.studentId),
      future: _teachers[child.studentId] ??= ref.read(inboxApiProvider).availableTeachers(child.studentId),
      builder: (_, snap) => ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, 32),
        children: [
          const Align(alignment: Alignment.centerLeft, child: ChildChip()),
          const SectionTitle('Nhắn cho giáo viên'),
          if (snap.hasError)
            ErrorState(snap.error!)
          else if (!snap.hasData)
            const SkeletonBox()
          else if (snap.data!.isEmpty)
            const EmptyState('Chưa có giáo viên nào đang dạy lớp của con.', compact: true)
          else
            for (final t in snap.data!)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: PersonRow(
                  name: t.str('fullName'),
                  subtitle: t.str('roleDescription'),
                  trailing: const Icon(LucideIcons.messageCircle, size: 20, color: AppColor.green700),
                  onTap: () => _open(studentId: child.studentId, staffId: t.integer('staffId')),
                ),
              ),
        ],
      ),
    );
  }

  Widget _teacher() {
    final s = _student;
    if (s != null) {
      return FutureBuilder<List<Json>>(
        key: ValueKey(s.integer('studentId')),
        future: _parents[s.integer('studentId')] ??= ref.read(inboxApiProvider).availableParents(s.integer('studentId')),
        builder: (_, snap) => ListView(
          padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, 32),
          children: [
            PersonRow(
              name: s.str('fullName'),
              subtitle: '${s.str('studentCode')} · Lớp ${s.str('className')}',
              trailing: TextButton(onPressed: () => setState(() => _student = null), child: const Text('Đổi')),
            ),
            const SectionTitle('Phụ huynh'),
            if (snap.hasError)
              ErrorState(snap.error!)
            else if (!snap.hasData)
              const SkeletonBox()
            else if (snap.data!.isEmpty)
              const EmptyState('Học sinh này chưa có phụ huynh được liên kết.', compact: true)
            else
              for (final p in snap.data!)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: PersonRow(
                    name: p.str('fullName'),
                    subtitle: p.strOrNull('relationship') ?? 'Phụ huynh',
                    trailing: const Icon(LucideIcons.messageCircle, size: 20, color: AppColor.green700),
                    onTap: () => _open(studentId: s.integer('studentId'), parentId: p.integer('parentId')),
                  ),
                ),
          ],
        ),
      );
    }
    return FutureBuilder<List<Json>>(
      future: _students ??= ref.read(inboxApiProvider).teachingStudents(),
      builder: (_, snap) {
        if (snap.hasError) return ErrorState(snap.error!);
        if (!snap.hasData) return const SkeletonList();
        final q = _q.trim().toLowerCase();
        final list = snap.data!
            .where((x) => q.isEmpty || x.str('fullName').toLowerCase().contains(q) || x.str('className').toLowerCase().contains(q))
            .toList();
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, AppSpacing.sm),
              child: TextField(
                onChanged: (v) => setState(() => _q = v),
                decoration: const InputDecoration(prefixIcon: Icon(LucideIcons.search, size: 18), hintText: 'Tìm học sinh hoặc lớp'),
              ),
            ),
            Expanded(
              child: list.isEmpty
                  ? const EmptyState('Không tìm thấy học sinh.')
                  : ListView.separated(
                      padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 32),
                      itemCount: list.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 10),
                      itemBuilder: (_, i) => PersonRow(
                        name: list[i].str('fullName'),
                        subtitle: '${list[i].str('studentCode')} · Lớp ${list[i].str('className')}',
                        trailing: const Icon(LucideIcons.chevronRight, size: 18, color: AppColor.text3),
                        onTap: () => setState(() => _student = list[i]),
                      ),
                    ),
            ),
          ],
        );
      },
    );
  }
}
