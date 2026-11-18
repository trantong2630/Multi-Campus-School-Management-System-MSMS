import 'dart:math' as math;

import 'package:material_ui/material_ui.dart';

import '../theme/app_color.dart';
import '../theme/app_spacing.dart';

/// Linh vật MSMS AI (logo/chatbot.png) kèm bong bóng chat (WBS 4.6.21). Lời nhắn soạn sẵn, không gọi AI.
/// Avatar nhún nhẹ; khi [thinking] bật, bong bóng hiện ba chấm đang gõ.
class MsmsAiBubble extends StatefulWidget {
  const MsmsAiBubble({super.key, this.message, this.thinking = false, this.size = 56});

  final String? message;
  final bool thinking;
  final double size;

  @override
  State<MsmsAiBubble> createState() => _MsmsAiBubbleState();
}

class _MsmsAiBubbleState extends State<MsmsAiBubble> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1600))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        AnimatedBuilder(
          animation: _c,
          builder: (_, child) => Transform.translate(offset: Offset(0, -3 * math.sin(_c.value * 2 * math.pi).abs()), child: child),
          child: Image.asset('assets/images/msms_ai.png', width: widget.size, height: widget.size, semanticLabel: 'MSMS AI'),
        ),
        const SizedBox(width: AppSpacing.sm),
        Flexible(
          child: Container(
            margin: EdgeInsets.only(bottom: widget.size * .35),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: const BoxDecoration(
              color: AppColor.surface,
              boxShadow: AppShadow.card,
              borderRadius: BorderRadius.only(
                topLeft: Radius.circular(16),
                topRight: Radius.circular(16),
                bottomRight: Radius.circular(16),
                bottomLeft: Radius.circular(4),
              ),
            ),
            child: widget.thinking
                ? Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (widget.message != null) ...[
                        Flexible(
                          child: Text(widget.message!, style: const TextStyle(fontSize: 14, color: AppColor.text1)),
                        ),
                        const SizedBox(width: 8),
                      ],
                      _Dots(_c),
                    ],
                  )
                : Text(widget.message ?? '', style: const TextStyle(fontSize: 14, color: AppColor.text1, height: 1.4)),
          ),
        ),
      ],
    );
  }
}

class _Dots extends StatelessWidget {
  const _Dots(this.c);
  final AnimationController c;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: c,
      builder: (_, _) => Row(
        mainAxisSize: MainAxisSize.min,
        children: List.generate(3, (i) {
          final phase = (c.value * 3 - i).clamp(0.0, 1.0);
          final lift = math.sin(phase * math.pi);
          return Container(
            margin: const EdgeInsets.symmetric(horizontal: 2),
            width: 6,
            height: 6,
            transform: Matrix4.translationValues(0, -3 * lift, 0),
            decoration: BoxDecoration(
              color: AppColor.green500.withValues(alpha: .5 + .5 * lift),
              shape: BoxShape.circle,
            ),
          );
        }),
      ),
    );
  }
}
