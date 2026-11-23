import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../features/att/att_pages.dart';
import '../features/att/attendance_sheet_page.dart';
import '../features/com/feed_pages.dart';
import '../features/com/notifications_page.dart';
import '../features/home/home_pages.dart';
import '../features/iam/login_page.dart';
import '../features/iam/password_pages.dart';
import '../features/iam/profile.dart';
import '../features/lrn/assignments_page.dart';
import '../features/lrn/result_page.dart';
import '../features/par/inbox_api.dart';
import '../features/par/inbox_pages.dart';
import '../features/par/leave_pages.dart';
import '../features/par/par_api.dart';
import '../features/par/report_pages.dart';
import '../features/ppl/student_profile_page.dart';
import '../features/scd/schedule_page.dart';
import 'auth/auth_controller.dart';
import 'realtime.dart';
import 'theme/app_color.dart';
import 'theme/app_spacing.dart';
import 'widgets/brand.dart';

final _rootKey = GlobalKey<NavigatorState>();

const _publicPaths = {'/login', '/forgot-password'};

final routerProvider = Provider<GoRouter>((ref) {
  final auth = ValueNotifier<AuthState>(ref.read(authProvider));
  ref.listen(authProvider, (_, next) => auth.value = next);
  ref.onDispose(auth.dispose);

  return GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/splash',
    refreshListenable: auth,
    redirect: (context, state) {
      final a = auth.value;
      final path = state.matchedLocation;
      switch (a.status) {
        case AuthStatus.checking:
        case AuthStatus.unreachable:
          return path == '/splash' ? null : '/splash';
        case AuthStatus.signedOut:
          return _publicPaths.contains(path) ? null : '/login';
        case AuthStatus.signedIn:
          if (a.user!.mustChangePassword) return path == '/forced-change' ? null : '/forced-change';
          if (path == '/splash' || path == '/forced-change' || _publicPaths.contains(path)) return '/home';
          return null;
      }
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashPage()),
      GoRoute(
        path: '/login',
        builder: (_, s) => LoginPage(username: s.extra as String?),
      ),
      GoRoute(
        path: '/forgot-password',
        builder: (_, s) => ForgotPasswordPage(username: s.extra as String?),
      ),
      GoRoute(path: '/forced-change', builder: (_, _) => const ForcedChangePasswordPage()),
      StatefulShellRoute.indexedStack(
        builder: (_, _, shell) => RealtimeScope(child: AppShell(shell: shell)),
        branches: [
          StatefulShellBranch(
            routes: [GoRoute(path: '/home', builder: (_, _) => const HomePage())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/schedule', builder: (_, _) => const SchedulePage())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/news', builder: (_, _) => const FeedPage())],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/third',
                builder: (_, s) => ThirdTab(initialTab: int.tryParse(s.uri.queryParameters['tab'] ?? '') ?? 0),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/me', builder: (_, _) => const ProfilePage())],
          ),
        ],
      ),
      GoRoute(
        path: '/me/edit',
        builder: (_, s) => EditProfilePage(profile: s.extra! as OwnProfile),
      ),
      GoRoute(path: '/me/password', builder: (_, _) => const ChangePasswordPage()),
      GoRoute(path: '/notifications', builder: (_, _) => const NotificationsPage()),
      GoRoute(
        path: '/feed/:id',
        builder: (_, s) => PostDetailPage(postId: int.parse(s.pathParameters['id']!)),
      ),
      GoRoute(path: '/attendance-today', builder: (_, _) => const TodayAttendancePage()),
      GoRoute(
        path: '/attendance/:id',
        builder: (_, s) => AttendanceSheetPage(sessionId: int.parse(s.pathParameters['id']!)),
      ),
      GoRoute(path: '/attendance-me', builder: (_, _) => const StudentAttendancePage()),
      GoRoute(path: '/homeroom-attendance', builder: (_, _) => const HomeroomAttendancePage()),
      GoRoute(path: '/homeroom-students', builder: (_, _) => const HomeroomStudentsPage()),
      GoRoute(
        path: '/students/:id',
        builder: (_, s) => StudentProfilePage(studentId: int.parse(s.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/report',
        builder: (_, s) => WeeklyReportPage(studentId: int.tryParse(s.uri.queryParameters['studentId'] ?? '')),
      ),
      GoRoute(path: '/leave', builder: (_, _) => const LeaveListPage()),
      GoRoute(path: '/leave/new', builder: (_, _) => const LeaveFormPage()),
      GoRoute(
        path: '/leave/detail',
        builder: (_, s) => LeaveDetailPage(leave: s.extra! as LeaveItem),
      ),
      GoRoute(path: '/results', builder: (_, _) => const AssignmentsPage()),
      GoRoute(
        path: '/results/:id',
        builder: (_, s) =>
            ResultPage(assignmentId: int.parse(s.pathParameters['id']!), studentId: int.tryParse(s.uri.queryParameters['studentId'] ?? '')),
      ),
      GoRoute(path: '/new-message', builder: (_, _) => const NewConversationPage()),
      GoRoute(
        path: '/inbox/:id',
        builder: (_, s) => ChatPage(conversationId: int.parse(s.pathParameters['id']!)),
      ),
    ],
  );
});

/// Tab thứ tư: Bài tập với học sinh, Hộp thư với giáo viên và phụ huynh.
class ThirdTab extends ConsumerWidget {
  const ThirdTab({super.key, this.initialTab = 0});
  final int initialTab;

  @override
  Widget build(BuildContext context, WidgetRef ref) =>
      ref.watch(currentUserProvider).isStudent ? AssignmentsPage(key: ValueKey(initialTab), initialTab: initialTab) : const InboxPage();
}

class _Tab {
  const _Tab(this.label, this.icon);
  final String label;
  final IconData icon;
}

/// Khung sau đăng nhập: bottom nav nổi nền xanh, bo tròn dạng viên, cách đáy 12, 5 tab theo loại tài khoản (Design_Mobile §3.1).
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.shell});
  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(currentUserProvider);
    final tabs = [
      const _Tab('Trang chủ', LucideIcons.house),
      _Tab(user.isTeacher ? 'Lịch dạy' : 'Lịch học', LucideIcons.calendarDays),
      const _Tab('Bảng tin', LucideIcons.newspaper),
      user.isStudent ? const _Tab('Bài tập', LucideIcons.bookOpen) : const _Tab('Hộp thư', LucideIcons.messageCircle),
      const _Tab('Cá nhân', LucideIcons.user),
    ];
    final unread = user.isStudent ? 0 : (ref.watch(unreadMessagesProvider).value ?? 0);
    final bottom = MediaQuery.paddingOf(context).bottom;
    return Scaffold(
      extendBody: true,
      body: shell,
      bottomNavigationBar: Padding(
        padding: EdgeInsets.fromLTRB(AppSpacing.screen, 0, AppSpacing.screen, 12 + bottom),
        child: Container(
          height: 68,
          padding: const EdgeInsets.all(6),
          decoration: BoxDecoration(
            gradient: const LinearGradient(colors: [AppColor.green600, AppColor.green500]),
            borderRadius: BorderRadius.circular(AppRadius.pill),
            boxShadow: const [BoxShadow(color: Color(0x401D8E55), blurRadius: 24, offset: Offset(0, 10))],
          ),
          child: Row(
            children: [
              for (final (i, t) in tabs.indexed)
                Expanded(
                  child: _NavItem(
                    tab: t,
                    selected: shell.currentIndex == i,
                    badge: i == 3 ? unread : 0,
                    onTap: () => shell.goBranch(i, initialLocation: i == shell.currentIndex),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({required this.tab, required this.selected, required this.badge, required this.onTap});
  final _Tab tab;
  final bool selected;
  final int badge;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Trên nền xanh: tab đang chọn chữ và icon trắng đậm trên viên trắng mờ, tab khác trắng nhạt.
    final color = selected ? Colors.white : Colors.white.withValues(alpha: .72);
    return Semantics(
      selected: selected,
      button: true,
      label: badge > 0 ? '${tab.label}, $badge chưa đọc' : tab.label,
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        onTap: onTap,
        child: ExcludeSemantics(
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 220),
            curve: Curves.easeOutCubic,
            margin: const EdgeInsets.symmetric(horizontal: 2),
            decoration: BoxDecoration(
              color: selected ? Colors.white.withValues(alpha: .22) : Colors.transparent,
              borderRadius: BorderRadius.circular(AppRadius.pill),
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Icon(tab.icon, size: 22, color: color),
                    if (badge > 0)
                      Positioned(
                        right: -10,
                        top: -6,
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                          decoration: BoxDecoration(
                            color: AppColor.coralFg,
                            borderRadius: BorderRadius.circular(AppRadius.pill),
                            border: Border.all(color: AppColor.green600, width: 1.5),
                          ),
                          child: Text(
                            badge > 99 ? '99+' : '$badge',
                            style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.w700),
                          ),
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 3),
                Text(
                  tab.label,
                  maxLines: 1,
                  overflow: TextOverflow.fade,
                  softWrap: false,
                  style: TextStyle(fontSize: 11, fontWeight: selected ? FontWeight.w700 : FontWeight.w500, color: color),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Màn chờ khi mở app (đang khôi phục phiên) hoặc khi không kết nối được máy chủ.
class SplashPage extends ConsumerWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unreachable = ref.watch(authProvider).status == AuthStatus.unreachable;
    return Scaffold(
      body: Stack(
        children: [
          const Positioned.fill(child: LandscapeArt()),
          Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const BrandLogo(tone: LogoTone.white, size: 64),
                if (unreachable) ...[
                  const SizedBox(height: AppSpacing.xl),
                  Container(
                    margin: const EdgeInsets.symmetric(horizontal: AppSpacing.xl),
                    padding: const EdgeInsets.all(AppSpacing.lg),
                    decoration: BoxDecoration(
                      color: AppColor.surface,
                      borderRadius: BorderRadius.circular(AppRadius.card),
                      boxShadow: AppShadow.pop,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Text(
                          'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: AppColor.text2, height: 1.45),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        OutlinedButton(
                          onPressed: () => ref.read(authProvider.notifier).restore(),
                          style: OutlinedButton.styleFrom(minimumSize: const Size(140, 48)),
                          child: const Text('Thử lại'),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
