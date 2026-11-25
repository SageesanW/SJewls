// TODO(mvp): Lottery is temporarily disabled for the current MVP release.
// This screen is preserved so it can be re-enabled in a post-MVP milestone.
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';

class LotteryTabScreen extends StatelessWidget {
  const LotteryTabScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Monthly Draws & Winners'),
        actions: [
          IconButton(
            tooltip: 'Notifications',
            icon: const Icon(Icons.notifications_outlined),
            onPressed: () => context.push('/notifications'),
          ),
          IconButton(
            tooltip: 'Profile',
            icon: const Icon(Icons.person_outline_rounded),
            onPressed: () => context.push('/profile'),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: AppColors.gold.withValues(alpha: 0.45),
                  width: 1.2,
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: AppColors.gold.withValues(alpha: 0.18),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(
                          Icons.emoji_events_outlined,
                          color: AppColors.gold,
                          size: 26,
                        ),
                      ),
                      const SizedBox(width: 12),
                      const Expanded(
                        child: Text(
                          'Chittu Draw & Winner Results',
                          style: TextStyle(
                            color: AppColors.white,
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Text(
                    'Monthly Chittu draws are conducted physically at Saranga Jewels branches. Active slots paid through the current draw month are eligible.',
                    style: TextStyle(
                      color: AppColors.white.withValues(alpha: 0.88),
                      fontSize: 13.5,
                      height: 1.45,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(
                          Icons.storefront_outlined,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 10),
                        const Expanded(
                          child: Text(
                            'Physical Store Collection Policy',
                            style: TextStyle(
                              color: AppColors.dark,
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      '• Winning slots are announced in-app as soon as branch staff record the monthly draw.\n'
                      '• Winners receive jewellery equal to the Chittu benefit value (not cash).\n'
                      '• Visit your primary Saranga Jewels branch with your registered NIC to claim your jewellery benefit.',
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.75),
                        fontSize: 13.5,
                        height: 1.55,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),
            OutlinedButton.icon(
              onPressed: () => context.push('/notifications'),
              icon: const Icon(Icons.notifications_active_outlined),
              label: const Text('Check In-App Notifications'),
            ),
          ],
        ),
      ),
    );
  }
}
