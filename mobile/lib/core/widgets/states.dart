import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../http/api_client.dart';
import '../theme/app_color.dart';
import '../theme/app_spacing.dart';
import 'msms_ai.dart';

/// Khối xương nhấp nháy nhẹ, dựng theo hình thẻ thật (Design_Mobile §6: không dùng spinner).
class SkeletonBox extends StatefulWidget {
  const SkeletonBox({super.key, this.height = 68, this.width, this.radius = AppRadius.card});

  final double height;
  final double? width;
  final double radius;

  @override
  State<SkeletonBox> createState() => _SkeletonBoxState();
}

class _SkeletonBoxState extends State<SkeletonBox> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 900))..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: Tween(begin: .55, end: 1.0).animate(_c),
      child: Container(
        height: widget.height,
        width: widget.width,
        decoration: BoxDecoration(color: AppColor.neutralBg, borderRadius: BorderRadius.circular(widget.radius)),
      ),
    );
  }
}

/// Danh sách xương: [count] thẻ cao [height].
class SkeletonList extends StatelessWidget {
  const SkeletonList({super.key, this.count = 5, this.height = 68, this.padding = const EdgeInsets.all(AppSpacing.screen)});

  final int count;
  final double height;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: padding,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: count,
      separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
      itemBuilder: (_, _) => SkeletonBox(height: height),
    );
  }
}

/// Trạng thái rỗng: minh họa nhỏ tông xanh nhạt, một câu, nút hành động nếu có.
class EmptyState extends StatelessWidget {
  const EmptyState(this.message, {super.key, this.actionLabel, this.onAction, this.compact = false});

  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.symmetric(vertical: compact ? AppSpacing.lg : AppSpacing.xxl, horizontal: AppSpacing.xl),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: compact ? 72 : 120,
            height: compact ? 56 : 92,
            child: const CustomPaint(painter: _EmptyArt()),
          ),
          const SizedBox(height: AppSpacing.md),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColor.text2, fontSize: 14.5, height: 1.45),
          ),
          if (actionLabel != null) ...[
            const SizedBox(height: AppSpacing.md),
            OutlinedButton(
              onPressed: onAction,
              style: OutlinedButton.styleFrom(minimumSize: const Size(160, 48)),
              child: Text(actionLabel!),
            ),
          ],
        ],
      ),
    );
  }
}

/// Đồi và mây nhỏ tông green200 — cùng họ với tranh đăng nhập.
class _EmptyArt extends CustomPainter {
  const _EmptyArt();

  @override
  void paint(Canvas canvas, Size s) {
    final p = Paint()..color = AppColor.green200;
    final light = Paint()..color = AppColor.green100;
    canvas.drawCircle(Offset(s.width * .72, s.height * .28), s.height * .16, light);
    canvas.drawPath(
      Path()
        ..moveTo(0, s.height * .78)
        ..quadraticBezierTo(s.width * .3, s.height * .45, s.width * .6, s.height * .72)
        ..quadraticBezierTo(s.width * .82, s.height * .9, s.width, s.height * .7)
        ..lineTo(s.width, s.height)
        ..lineTo(0, s.height)
        ..close(),
      p,
    );
    final cloud = Paint()..color = AppColor.green100;
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(s.width * .12, s.height * .26, s.width * .3, s.height * .12), const Radius.circular(20)),
      cloud,
    );
    canvas.drawCircle(Offset(s.width * .24, s.height * .26), s.height * .1, cloud);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Lỗi mạng: thẻ xám giữa màn + nút Thử lại.
class ErrorState extends StatelessWidget {
  const ErrorState(this.error, {super.key, this.onRetry});

  final Object error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.xl),
          decoration: BoxDecoration(color: AppColor.neutralBg, borderRadius: BorderRadius.circular(AppRadius.card)),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(LucideIcons.cloudOff, color: AppColor.text3, size: 32),
              const SizedBox(height: AppSpacing.md),
              Text(
                errorMessage(error),
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColor.text2, height: 1.45),
              ),
              if (onRetry != null) ...[
                const SizedBox(height: AppSpacing.lg),
                OutlinedButton(
                  onPressed: onRetry,
                  style: OutlinedButton.styleFrom(minimumSize: const Size(140, 48)),
                  child: const Text('Thử lại'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Bọc AsyncValue: lần đầu hiện xương, lỗi hiện thẻ Thử lại; khi làm mới vẫn giữ dữ liệu cũ (Design_Mobile §6).
class AsyncView<T> extends StatelessWidget {
  const AsyncView({super.key, required this.value, required this.data, this.loading, this.onRetry});

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final Widget? loading;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    if (value.hasValue) return data(value.requireValue);
    if (value.hasError) return ErrorState(value.error!, onRetry: onRetry);
    return loading ?? const SkeletonList();
  }
}

/// Khung màn con (mở từ trang chủ hoặc danh sách): tiêu đề trái, nút quay lại, nền bg.
class PageScaffold extends StatelessWidget {
  const PageScaffold({super.key, required this.title, required this.body, this.actions, this.bottom, this.floating});

  final String title;
  final Widget body;
  final List<Widget>? actions;
  final Widget? bottom;
  final Widget? floating;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title), actions: actions),
      body: body,
      bottomNavigationBar: bottom,
      floatingActionButton: floating,
    );
  }
}

/// Bao danh sách có kéo xuống để làm mới.
class Refreshable extends StatelessWidget {
  const Refreshable({super.key, required this.onRefresh, required this.child});

  final Future<void> Function() onRefresh;
  final Widget child;

  @override
  Widget build(BuildContext context) =>
      RefreshIndicator(color: AppColor.green600, backgroundColor: AppColor.surface, onRefresh: onRefresh, child: child);
}

/// Dòng trạng thái rỗng do MSMS AI nói, dùng ở các màn của học sinh và phụ huynh.
class AiEmptyState extends StatelessWidget {
  const AiEmptyState(this.message, {super.key});
  final String message;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: AppSpacing.xl),
    child: MsmsAiBubble(message: message),
  );
}
