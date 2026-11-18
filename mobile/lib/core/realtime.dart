import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../features/com/notification_api.dart';
import 'auth/auth_controller.dart';
import 'config.dart';
import 'json.dart';

sealed class RealtimeEvent {}

class NotificationEvent extends RealtimeEvent {}

class MessageEvent extends RealtimeEvent {
  MessageEvent(this.data);
  final Json data;
  int get conversationId => data.integer('conversationId');
}

class ReadEvent extends RealtimeEvent {
  ReadEvent(this.conversationId, this.readAt);
  final int conversationId;
  final DateTime readAt;
}

class TypingEvent extends RealtimeEvent {
  TypingEvent(this.conversationId, this.personId, this.isTyping);
  final int conversationId;
  final int personId;
  final bool isTyping;
}

class PresenceEvent extends RealtimeEvent {
  PresenceEvent(this.personId, this.isOnline);
  final int personId;
  final bool isOnline;
}

/// Nối lại sau khi mất kết nối hoặc quay lại app: các màn tải lại phần đã lỡ qua REST.
class ResyncEvent extends RealtimeEvent {}

/// Một kết nối SignalR cho phiên hiện tại (WBS 4.8.10): chỉ giữ khi app đang mở, ra nền thì ngắt.
/// Token đi qua query `access_token`; khi token hết hạn server đóng kết nối, client refresh rồi nối lại.
class RealtimeService {
  RealtimeService(this.ref);
  final Ref ref;

  HubConnection? _hub;
  final _events = StreamController<RealtimeEvent>.broadcast();
  Timer? _retry;
  bool _wanted = false;

  Stream<RealtimeEvent> get events => _events.stream;
  bool get connected => _hub?.state == HubConnectionState.Connected;

  HubConnection _build() {
    final api = ref.read(apiProvider);
    final hub = HubConnectionBuilder()
        .withUrl(
          '${AppConfig.apiUrl}/signalr',
          options: HttpConnectionOptions(accessTokenFactory: () async => await api.freshToken() ?? ''),
        )
        .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 20000])
        .build();
    hub.on('NotificationCreated', (_) => _emit(NotificationEvent()));
    hub.on('MessageReceived', (a) {
      if (a != null && a.isNotEmpty && a.first is Map) _emit(MessageEvent(Map<String, dynamic>.from(a.first as Map)));
    });
    hub.on('MessagesRead', (a) {
      if (a == null || a.length < 2) return;
      _emit(ReadEvent((a[0] as num).toInt(), DateTime.tryParse(a[1].toString()) ?? DateTime.now()));
    });
    hub.on('UserTyping', (a) {
      if (a == null || a.length < 3) return;
      _emit(TypingEvent((a[0] as num).toInt(), (a[1] as num).toInt(), a[2] == true));
    });
    hub.on('PresenceChanged', (a) {
      if (a == null || a.length < 2) return;
      _emit(PresenceEvent((a[0] as num).toInt(), a[1] == true));
    });
    hub.onreconnected(({connectionId}) => _emit(ResyncEvent()));
    hub.onclose(({error}) {
      // Hết đường tự nối lại (vd. token hết hạn và refresh lỗi): thử lại sau 10 giây nếu app còn mở.
      if (_wanted) _scheduleRetry();
    });
    return hub;
  }

  void _emit(RealtimeEvent e) {
    if (!_events.isClosed) _events.add(e);
  }

  void _scheduleRetry() {
    _retry?.cancel();
    _retry = Timer(const Duration(seconds: 10), connect);
  }

  Future<void> connect() async {
    _wanted = true;
    _retry?.cancel();
    final hub = _hub ??= _build();
    if (hub.state != HubConnectionState.Disconnected) return;
    try {
      await hub.start();
      _emit(ResyncEvent());
    } catch (_) {
      // Không dựng được kết nối: polling chậm vẫn chạy (RealtimeScope), thử lại sau.
      if (_wanted) _scheduleRetry();
    }
  }

  Future<void> disconnect() async {
    _wanted = false;
    _retry?.cancel();
    final hub = _hub;
    if (hub != null && hub.state != HubConnectionState.Disconnected) {
      try {
        await hub.stop();
      } catch (_) {}
    }
  }

  /// Vào nhóm hội thoại; trả về trạng thái online hiện tại của bên kia (null nếu chưa kết nối).
  Future<bool?> joinConversation(int id) async {
    if (!connected) return null;
    try {
      return (await _hub!.invoke('JoinConversation', args: [id])) == true;
    } catch (_) {
      return null;
    }
  }

  void sendTyping(int conversationId, bool isTyping) {
    if (!connected) return;
    _hub!.invoke('SendTyping', args: [conversationId, isTyping]).catchError((_) => null);
  }

  void dispose() {
    disconnect();
    _hub = null;
    _events.close();
  }
}

final realtimeProvider = Provider<RealtimeService>((ref) {
  ref.watch(currentUserProvider);
  final s = RealtimeService(ref);
  ref.onDispose(s.dispose);
  return s;
});

/// Bọc khung sau đăng nhập: nối khi app mở, ngắt khi ra nền; làm mới chuông khi có thông báo;
/// polling chậm (60 giây) khi kết nối không dựng được.
class RealtimeScope extends ConsumerStatefulWidget {
  const RealtimeScope({super.key, required this.child});
  final Widget child;

  @override
  ConsumerState<RealtimeScope> createState() => _RealtimeScopeState();
}

class _RealtimeScopeState extends ConsumerState<RealtimeScope> {
  late final AppLifecycleListener _life;
  StreamSubscription<RealtimeEvent>? _sub;
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    final rt = ref.read(realtimeProvider);
    rt.connect();
    _sub = rt.events.listen((e) {
      if (e is NotificationEvent || e is ResyncEvent || e is MessageEvent) ref.invalidate(unreadNotificationsProvider);
    });
    _poll = Timer.periodic(const Duration(seconds: 60), (_) {
      if (!ref.read(realtimeProvider).connected) ref.invalidate(unreadNotificationsProvider);
    });
    _life = AppLifecycleListener(
      onResume: () => ref.read(realtimeProvider).connect(),
      onHide: () => ref.read(realtimeProvider).disconnect(),
    );
  }

  @override
  void dispose() {
    _life.dispose();
    _sub?.cancel();
    _poll?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
