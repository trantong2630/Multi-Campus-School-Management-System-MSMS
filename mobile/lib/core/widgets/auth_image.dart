import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../auth/auth_controller.dart';
import '../theme/app_color.dart';

/// Ảnh cần đăng nhập (ảnh đại diện, ảnh bảng tin, trang sách): tải qua Dio để dùng chung token và refresh.
/// Giữ tối đa 60 ảnh gần nhất trong bộ nhớ.
class AuthImage extends ConsumerStatefulWidget {
  const AuthImage(this.path, {super.key, this.fit = BoxFit.cover, this.placeholder, this.error});

  final String path;
  final BoxFit fit;
  final Widget? placeholder;
  final Widget? error;

  static final _cache = <String, Uint8List>{};

  static void evict(String prefix) => _cache.removeWhere((k, _) => k.startsWith(prefix));

  @override
  ConsumerState<AuthImage> createState() => _AuthImageState();
}

class _AuthImageState extends ConsumerState<AuthImage> {
  Future<Uint8List>? _future;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(AuthImage old) {
    super.didUpdateWidget(old);
    if (old.path != widget.path) _load();
  }

  void _load() {
    final cached = AuthImage._cache[widget.path];
    if (cached != null) {
      _future = Future.value(cached);
      return;
    }
    _future = ref.read(apiProvider).dio.get<List<int>>(widget.path, options: Options(responseType: ResponseType.bytes)).then((r) {
      final bytes = Uint8List.fromList(r.data!);
      AuthImage._cache[widget.path] = bytes;
      if (AuthImage._cache.length > 60) AuthImage._cache.remove(AuthImage._cache.keys.first);
      return bytes;
    });
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Uint8List>(
      future: _future,
      builder: (context, snap) {
        if (snap.hasData) return Image.memory(snap.data!, fit: widget.fit, gaplessPlayback: true);
        if (snap.hasError) return widget.error ?? const ColoredBox(color: AppColor.neutralBg);
        return widget.placeholder ?? const ColoredBox(color: AppColor.neutralBg);
      },
    );
  }
}
