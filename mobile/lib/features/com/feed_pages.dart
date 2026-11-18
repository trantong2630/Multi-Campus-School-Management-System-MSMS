import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/format.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/hero_header.dart';
import '../../core/widgets/states.dart';
import 'feed_api.dart';

/// Bảng tin (COM-02): mobile chỉ đọc; ảnh đầu là ảnh bìa (BR-27).
class FeedPage extends ConsumerStatefulWidget {
  const FeedPage({super.key});

  @override
  ConsumerState<FeedPage> createState() => _FeedPageState();
}

class _FeedPageState extends ConsumerState<FeedPage> {
  final List<PostItem> _items = [];
  int _page = 0;
  int _total = 0;
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
      final (list, total) = await ref.read(feedApiProvider).posts(page: _page + 1);
      setState(() {
        _page++;
        _items.addAll(list);
        _total = total;
      });
    } catch (e) {
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    // Bảng tin là một tab: chuông thông báo ở góc phải như các tab khác, chừa chỗ cho bottom nav nổi.
    return PageScaffold(
      title: 'Bảng tin',
      actions: const [Padding(padding: EdgeInsets.only(right: 8), child: NotificationBell(onLight: true))],
      body: _error != null && _items.isEmpty
          ? ErrorState(_error!, onRetry: _reload)
          : _loading && _items.isEmpty
          ? const SkeletonList(count: 3, height: 260)
          : Refreshable(
              onRefresh: _reload,
              child: _items.isEmpty
                  ? ListView(children: const [EmptyState('Chưa có bài đăng nào.')])
                  : ListView(
                      padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 120),
                      children: [
                        for (final p in _items)
                          Padding(
                            padding: const EdgeInsets.only(bottom: AppSpacing.lg),
                            child: PostCard(p),
                          ),
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

/// Thẻ bài đăng có ảnh bìa (dùng ở bảng tin và trang chủ).
class PostCard extends StatelessWidget {
  const PostCard(this.p, {super.key, this.compact = false});
  final PostItem p;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: () => context.push('/feed/${p.id}'),
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (p.coverPath != null)
            ClipRRect(
              borderRadius: const BorderRadius.vertical(top: Radius.circular(AppRadius.card)),
              child: AspectRatio(
                aspectRatio: compact ? 2.2 : 16 / 9,
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    AuthImage(p.coverPath!),
                    if (p.imageCount > 1)
                      Positioned(
                        right: 10,
                        bottom: 10,
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                          decoration: BoxDecoration(
                            color: Colors.black.withValues(alpha: .5),
                            borderRadius: BorderRadius.circular(AppRadius.pill),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(LucideIcons.images, size: 13, color: Colors.white),
                              const SizedBox(width: 4),
                              Text(
                                '${p.imageCount}',
                                style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w600),
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          Padding(
            padding: const EdgeInsets.fromLTRB(14, 12, 14, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    if (p.visibility == 'Internal') ...[
                      const Pill('Nội bộ', group: BadgeGroup.info, dense: true),
                      const SizedBox(width: 8),
                    ],
                    Expanded(
                      child: Text(
                        '${p.authorName} · ${Fmt.ago(p.createdAt)}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 12, color: AppColor.text3),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  p.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.3),
                ),
                if (!compact && p.excerpt.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      p.excerpt,
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 13.5, color: AppColor.text2, height: 1.45),
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

class PostDetailPage extends ConsumerStatefulWidget {
  const PostDetailPage({super.key, required this.postId});
  final int postId;

  @override
  ConsumerState<PostDetailPage> createState() => _PostDetailPageState();
}

class _PostDetailPageState extends ConsumerState<PostDetailPage> {
  int _image = 0;

  @override
  Widget build(BuildContext context) {
    final value = ref.watch(postDetailProvider(widget.postId));
    return PageScaffold(
      title: 'Bảng tin',
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(postDetailProvider(widget.postId)),
        loading: const SkeletonList(count: 2, height: 220),
        data: (p) => ListView(
          padding: const EdgeInsets.only(bottom: 40),
          children: [
            if (p.imageIds.isNotEmpty)
              Stack(
                children: [
                  AspectRatio(
                    aspectRatio: 4 / 3,
                    child: PageView.builder(
                      itemCount: p.imageIds.length,
                      onPageChanged: (i) => setState(() => _image = i),
                      itemBuilder: (_, i) => InteractiveViewer(child: AuthImage(p.imagePath(p.imageIds[i]))),
                    ),
                  ),
                  if (p.imageIds.length > 1)
                    Positioned(
                      bottom: 10,
                      left: 0,
                      right: 0,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          for (var i = 0; i < p.imageIds.length; i++)
                            AnimatedContainer(
                              duration: const Duration(milliseconds: 200),
                              margin: const EdgeInsets.symmetric(horizontal: 3),
                              width: i == _image ? 18 : 7,
                              height: 7,
                              decoration: BoxDecoration(
                                color: i == _image ? Colors.white : Colors.white.withValues(alpha: .6),
                                borderRadius: BorderRadius.circular(AppRadius.pill),
                              ),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.screen),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (p.visibility == 'Internal')
                    const Padding(
                      padding: EdgeInsets.only(bottom: 8),
                      child: Pill('Nội bộ', group: BadgeGroup.info),
                    ),
                  Text(p.title, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, height: 1.3)),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      PersonAvatar(p.authorName, size: 32),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          '${p.authorName} · ${Fmt.dateTime(p.createdAt)}',
                          style: const TextStyle(fontSize: 13, color: AppColor.text2),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  SelectableText(p.content, style: const TextStyle(fontSize: 15.5, height: 1.6)),
                  if (p.updatedAt != null)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.lg),
                      child: Text(
                        'Sửa lần cuối ${Fmt.dateTime(p.updatedAt!)}',
                        style: const TextStyle(fontSize: 12, color: AppColor.text3),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
