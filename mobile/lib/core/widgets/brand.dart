import 'dart:math' as math;

import 'package:material_ui/material_ui.dart';

import '../theme/app_color.dart';

enum LogoTone { green, white }

/// Logo chữ "msms.": hai chữ đầu đậm, hai chữ sau nhạt, dấu chấm vàng (Design_Web §3.1). Không dùng ảnh.
class BrandLogo extends StatelessWidget {
  const BrandLogo({super.key, this.tone = LogoTone.green, this.size = 32});

  final LogoTone tone;
  final double size;

  @override
  Widget build(BuildContext context) {
    final color = tone == LogoTone.white ? AppColor.surface : AppColor.green500;
    final base = TextStyle(fontFamily: 'Montserrat', fontSize: size, height: 1, color: color, fontWeight: FontWeight.w300);
    return Semantics(
      label: 'MSMS',
      child: ExcludeSemantics(
        child: Text.rich(
          TextSpan(
            style: base,
            children: [
              const TextSpan(
                text: 'ms',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
              const TextSpan(text: 'ms'),
              TextSpan(
                text: '.',
                style: base.copyWith(fontWeight: FontWeight.w700, color: AppColor.logoDot),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Phong cảnh của màn đăng nhập web (trời aqua, mặt trời, mây, ba lớp đồi, hai cái cây), vẽ lại cho mobile.
/// Mây trôi chậm khi [animate] bật.
class LandscapeArt extends StatefulWidget {
  const LandscapeArt({super.key, this.animate = true, this.alignBottom = false, this.opacity = 1});

  final bool animate;

  /// Giữ đáy tranh (đồi, cây) khi khung thấp hơn tỉ lệ gốc — dùng cho header.
  final bool alignBottom;
  final double opacity;

  @override
  State<LandscapeArt> createState() => _LandscapeArtState();
}

class _LandscapeArtState extends State<LandscapeArt> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(seconds: 18));

  @override
  void initState() {
    super.initState();
    if (widget.animate) _c.repeat();
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    if (reduce && _c.isAnimating) _c.stop();
    return RepaintBoundary(
      child: AnimatedBuilder(
        animation: _c,
        builder: (_, _) => CustomPaint(
          painter: _LandscapePainter(t: _c.value, alignBottom: widget.alignBottom, opacity: widget.opacity),
          size: Size.infinite,
        ),
      ),
    );
  }
}

class _LandscapePainter extends CustomPainter {
  _LandscapePainter({required this.t, required this.alignBottom, required this.opacity});

  final double t;
  final bool alignBottom;
  final double opacity;

  Color _c(Color c) => c.withValues(alpha: c.a * opacity);

  @override
  void paint(Canvas canvas, Size size) {
    const w = 800.0, h = 720.0;
    final s = math.max(size.width / w, size.height / h);
    final dx = (size.width - w * s) / 2;
    final dy = alignBottom ? size.height - h * s : (size.height - h * s) / 2;
    canvas.save();
    canvas.clipRect(Offset.zero & size);
    canvas.translate(dx, dy);
    canvas.scale(s);

    final sky = Paint()
      ..shader = LinearGradient(
        begin: Alignment.topCenter,
        end: Alignment.bottomCenter,
        colors: [_c(AppColor.aqua), _c(AppColor.limeSoft)],
      ).createShader(const Rect.fromLTWH(0, 0, w, h));
    canvas.drawRect(const Rect.fromLTWH(0, 0, w, h), sky);

    final white = AppColor.surface;
    canvas.drawCircle(const Offset(620, 130), 92, Paint()..color = _c(white.withValues(alpha: .25)));
    canvas.drawCircle(const Offset(620, 130), 58, Paint()..color = _c(white.withValues(alpha: .8)));

    // Mây trôi qua lại một quãng ngắn quanh vị trí gốc.
    final drift = math.sin(t * 2 * math.pi);
    _cloud(canvas, 150 + drift * 26, 150, 1, _c(white.withValues(alpha: .92)));
    _cloud(canvas, 470 - drift * 18, 270, .68, _c(white.withValues(alpha: .7)));

    canvas.drawPath(
      Path()
        ..moveTo(0, 430)
        ..cubicTo(160, 380, 320, 410, 480, 440)
        ..cubicTo(640, 470, 700, 470, 800, 420)
        ..lineTo(800, 720)
        ..lineTo(0, 720)
        ..close(),
      Paint()..color = _c(AppColor.meadow1),
    );
    canvas.drawPath(
      Path()
        ..moveTo(0, 520)
        ..cubicTo(170, 470, 330, 540, 510, 525)
        ..cubicTo(690, 510, 720, 480, 800, 495)
        ..lineTo(800, 720)
        ..lineTo(0, 720)
        ..close(),
      Paint()..color = _c(AppColor.meadow2),
    );

    final trunk = Paint()
      ..color = _c(AppColor.meadow3)
      ..strokeWidth = 6
      ..strokeCap = StrokeCap.round;
    final crown = Paint()..color = _c(AppColor.meadow3);
    canvas.drawLine(const Offset(150, 545), const Offset(150, 488), trunk);
    canvas.drawCircle(const Offset(150, 462), 40, crown);
    canvas.drawLine(const Offset(240, 552), const Offset(240, 515), trunk);
    canvas.drawCircle(const Offset(240, 498), 26, crown);

    canvas.drawPath(
      Path()
        ..moveTo(0, 665)
        ..cubicTo(200, 645, 380, 605, 560, 575)
        ..cubicTo(740, 545, 740, 545, 800, 535)
        ..lineTo(800, 720)
        ..lineTo(0, 720)
        ..close(),
      Paint()..color = _c(AppColor.meadow3),
    );
    final bush = Paint()..color = _c(AppColor.meadow2);
    canvas.drawCircle(const Offset(80, 672), 26, bush);
    canvas.drawCircle(const Offset(116, 680), 20, bush);
    canvas.restore();
  }

  void _cloud(Canvas canvas, double x, double y, double k, Color color) {
    final p = Paint()..color = color;
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(x, y, 130 * k, 34 * k), Radius.circular(17 * k)), p);
    canvas.drawCircle(Offset(x + 45 * k, y - 2 * k), 30 * k, p);
    canvas.drawCircle(Offset(x + 86 * k, y - 14 * k), 38 * k, p);
  }

  @override
  bool shouldRepaint(_LandscapePainter old) => old.t != t || old.opacity != opacity || old.alignBottom != alignBottom;
}
