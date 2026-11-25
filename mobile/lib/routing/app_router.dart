import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../core/network/token_storage.dart';
import '../core/theme/app_colors.dart';
import '../features/auth/presentation/complete_profile_screen.dart';
import '../features/auth/presentation/login_screen.dart';
import '../features/auth/presentation/otp_screen.dart';
import '../features/chitu/presentation/chitu_investment_detail_screen.dart';
import '../features/chitu/presentation/chitu_payment_screen.dart';
import '../features/chitu/presentation/chitu_plan_detail_screen.dart';
import '../features/chitu/presentation/chitu_slot_detail_screen.dart';
import '../features/chitu/presentation/chitu_tab_screen.dart';
import '../features/home/presentation/home_dashboard_screen.dart';
import '../features/jewellery/presentation/buy_gold_screen.dart';
import '../features/jewellery/presentation/enrolment_detail_screen.dart';
import '../features/jewellery/presentation/jewellery_plan_detail_screen.dart';
import '../features/jewellery/presentation/jewellery_tab_screen.dart';
import '../features/notifications/presentation/notifications_screen.dart';
import '../features/payments/presentation/payment_history_screen.dart';
import '../features/profile/presentation/profile_screen.dart';

final GlobalKey<NavigatorState> _rootNavigatorKey = GlobalKey<NavigatorState>();

// TODO(mvp): Lottery is out of scope for the current MVP and temporarily disabled.
// Set to true and restore the Lottery StatefulShellBranch/BottomNavigationBarItem when ready.
const bool kEnableLotteryMvp = false;

// Single source of truth for all navigation
final GoRouter appRouter = GoRouter(
  navigatorKey: _rootNavigatorKey,
  initialLocation: '/',
  redirect: (context, state) {
    final path = state.uri.path;

    // Prevent direct navigation to /lottery while disabled for MVP
    if (!kEnableLotteryMvp && path.startsWith('/lottery')) {
      final token = const TokenStorage().cachedAccessToken;
      return (token != null && token.isNotEmpty) ? '/home' : '/login';
    }

    final isPublicRoute =
        path == '/' ||
        path == '/login' ||
        path == '/otp' ||
        path == '/complete-profile';

    if (isPublicRoute) {
      return null;
    }

    final token = const TokenStorage().cachedAccessToken;
    if (token == null || token.isEmpty) {
      return '/login';
    }
    return null;
  },
  routes: [
    // Splash
    GoRoute(
      path: '/',
      name: 'splash',
      builder: (context, state) => const SplashScreen(),
    ),

    // Login
    GoRoute(
      path: '/login',
      name: 'login',
      builder: (context, state) => const LoginScreen(),
    ),

    // OTP
    GoRoute(
      path: '/otp',
      name: 'otp',
      builder: (context, state) {
        final extra = state.extra as Map<String, dynamic>?;
        return OtpScreen(
          contact: extra?['contact'] ?? '',
          isPhone: extra?['isPhone'] ?? true,
        );
      },
    ),

    // Registration (Complete Profile)
    GoRoute(
      path: '/complete-profile',
      name: 'complete-profile',
      builder: (context, state) {
        final extra = state.extra as Map<String, dynamic>? ?? {};
        return CompleteProfileScreen(
          registrationToken: extra['registrationToken'] ?? '',
          verifiedContact: extra['verifiedContact'] ?? '',
          isPhone: extra['isPhone'] ?? false,
        );
      },
    ),

    // Bottom Navigation Shell (MVP: Home, Chittu, Jewelry, Payments; Lottery disabled)
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) {
        return SJewlsMainShell(navigationShell: navigationShell);
      },
      branches: [
        // 1. Home Tab
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: '/home',
              name: 'home',
              builder: (context, state) => const HomeDashboardScreen(),
            ),
          ],
        ),
        // 2. Chittu Tab
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: '/chittu',
              name: 'chittu',
              builder: (context, state) => const ChituTabScreen(),
              routes: [
                GoRoute(
                  path: 'plans/:planId',
                  name: 'chittu-plan-detail',
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (context, state) => ChituPlanDetailScreen(
                    planId: state.pathParameters['planId'] ?? '',
                  ),
                ),
                GoRoute(
                  path: 'investments/:planId',
                  name: 'chittu-investment-detail',
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (context, state) => ChituInvestmentDetailScreen(
                    planId: state.pathParameters['planId'] ?? '',
                  ),
                  routes: [
                    GoRoute(
                      path: 'pay',
                      name: 'chittu-pay-instalment',
                      parentNavigatorKey: _rootNavigatorKey,
                      builder: (context, state) => ChituPaymentScreen(
                        planId: state.pathParameters['planId'] ?? '',
                        initialSlotId: state.uri.queryParameters['slotId'],
                      ),
                    ),
                  ],
                ),
                GoRoute(
                  path: 'slots/:slotId',
                  name: 'chittu-slot-detail',
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (context, state) => ChituSlotDetailScreen(
                    slotId: state.pathParameters['slotId'] ?? '',
                  ),
                ),
              ],
            ),
          ],
        ),
        // 3. Jewelry Tab
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: '/jewelry',
              name: 'jewelry',
              builder: (context, state) => const JewelleryTabScreen(),
              routes: [
                GoRoute(
                  path: 'plans/:planId',
                  name: 'jewelry-plan-detail',
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (context, state) => JewelleryPlanDetailScreen(
                    planId: state.pathParameters['planId'] ?? '',
                  ),
                ),
                GoRoute(
                  path: 'enrolments/:enrolmentId',
                  name: 'jewelry-enrolment-detail',
                  parentNavigatorKey: _rootNavigatorKey,
                  builder: (context, state) => EnrolmentDetailScreen(
                    enrolmentId: state.pathParameters['enrolmentId'] ?? '',
                  ),
                  routes: [
                    GoRoute(
                      path: 'buy-gold',
                      name: 'jewelry-buy-gold',
                      parentNavigatorKey: _rootNavigatorKey,
                      builder: (context, state) => BuyGoldScreen(
                        enrolmentId: state.pathParameters['enrolmentId'] ?? '',
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
        // TODO(mvp): Lottery StatefulShellBranch temporarily disabled for MVP.
        // 4. Payments Tab
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: '/payments',
              name: 'payments',
              builder: (context, state) => const PaymentHistoryScreen(),
            ),
          ],
        ),
      ],
    ),

    // Notifications Centre
    GoRoute(
      path: '/notifications',
      name: 'notifications',
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const NotificationsScreen(),
    ),

    // Customer Profile & Contact Verification
    GoRoute(
      path: '/profile',
      name: 'profile',
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const ProfileScreen(),
    ),
  ],
);

class SJewlsMainShell extends StatelessWidget {
  final StatefulNavigationShell navigationShell;

  const SJewlsMainShell({super.key, required this.navigationShell});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: navigationShell.currentIndex,
        onTap: (index) {
          navigationShell.goBranch(
            index,
            initialLocation: index == navigationShell.currentIndex,
          );
        },
        type: BottomNavigationBarType.fixed,
        backgroundColor: AppColors.primary,
        selectedItemColor: AppColors.gold,
        unselectedItemColor: AppColors.white.withValues(alpha: 0.65),
        selectedLabelStyle: const TextStyle(
          fontWeight: FontWeight.bold,
          fontSize: 12,
        ),
        unselectedLabelStyle: const TextStyle(
          fontWeight: FontWeight.w500,
          fontSize: 11.5,
        ),
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.home_outlined),
            activeIcon: Icon(Icons.home_rounded),
            label: 'Home',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.savings_outlined),
            activeIcon: Icon(Icons.savings_rounded),
            label: 'Chittu',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.diamond_outlined),
            activeIcon: Icon(Icons.diamond_rounded),
            label: 'Jewelry',
          ),
          // TODO(mvp): Lottery BottomNavigationBarItem temporarily removed for MVP.
          BottomNavigationBarItem(
            icon: Icon(Icons.receipt_long_outlined),
            activeIcon: Icon(Icons.receipt_long_rounded),
            label: 'Payments',
          ),
        ],
      ),
    );
  }
}

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    _checkSessionAndNavigate();
  }

  Future<void> _checkSessionAndNavigate() async {
    await Future.delayed(const Duration(seconds: 2));
    if (!mounted) return;
    final token = await const TokenStorage().getAccessToken();
    if (!mounted) return;
    if (token != null && token.isNotEmpty) {
      context.go('/home');
    } else {
      context.go('/login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.primary,
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.diamond, size: 80, color: AppColors.gold),
            SizedBox(height: 24),
            Text(
              'SARANGA',
              style: TextStyle(
                color: AppColors.gold,
                fontSize: 32,
                fontWeight: FontWeight.bold,
                letterSpacing: 4,
              ),
            ),
            Text(
              'JEWELLERY',
              style: TextStyle(
                color: AppColors.gold,
                fontSize: 14,
                letterSpacing: 6,
              ),
            ),
            SizedBox(height: 48),
            CircularProgressIndicator(color: AppColors.gold),
          ],
        ),
      ),
    );
  }
}
