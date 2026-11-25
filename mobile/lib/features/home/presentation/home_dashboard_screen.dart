import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../../chitu/data/chitu_models.dart';
import '../../chitu/presentation/chitu_provider.dart';
import '../../jewellery/data/jewellery_models.dart';
import '../../jewellery/presentation/jewellery_provider.dart';
import '../../notifications/presentation/notification_provider.dart';
import '../../profile/presentation/profile_provider.dart';

class HomeDashboardScreen extends ConsumerWidget {
  const HomeDashboardScreen({super.key});

  Future<void> _navigateToChituPayment(
    BuildContext context,
    WidgetRef ref,
    String planId,
  ) async {
    await context.push('/chittu/investments/$planId/pay');
    ref.invalidate(myChituInvestmentsProvider);
    ref.invalidate(availableChituPlansProvider);
    ref.invalidate(unifiedCustomerPaymentsProvider);
  }

  Future<void> _navigateToJewelleryContribution(
    BuildContext context,
    WidgetRef ref,
    String enrolmentId,
  ) async {
    await context.push('/jewelry/enrolments/$enrolmentId/buy-gold');
    ref.invalidate(customerEnrolmentsProvider);
    ref.invalidate(enrolmentDetailProvider(enrolmentId));
    ref.invalidate(enrolmentPaymentsProvider(enrolmentId));
    ref.invalidate(allCustomerPaymentsProvider);
    ref.invalidate(unifiedCustomerPaymentsProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(customerProfileProvider);
    final chituInvestmentsAsync = ref.watch(myChituInvestmentsProvider);
    final enrolmentsAsync = ref.watch(customerEnrolmentsProvider);
    final plansAsync = ref.watch(jewelleryPlansProvider);
    final liveGoldRateAsync = ref.watch(liveGoldRateProvider);
    final unreadCount = ref.watch(unreadNotificationCountProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.diamond_rounded, color: AppColors.gold, size: 22),
            SizedBox(width: 8),
            Text(
              'SJEWLS',
              style: TextStyle(
                color: AppColors.gold,
                fontWeight: FontWeight.bold,
                letterSpacing: 2,
              ),
            ),
          ],
        ),
        actions: [
          Stack(
            alignment: Alignment.center,
            children: [
              IconButton(
                tooltip: 'Notifications',
                icon: const Icon(Icons.notifications_outlined),
                onPressed: () => context.push('/notifications'),
              ),
              if (unreadCount > 0)
                Positioned(
                  right: 8,
                  top: 8,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 5,
                      vertical: 2,
                    ),
                    decoration: const BoxDecoration(
                      color: AppColors.gold,
                      shape: BoxShape.circle,
                    ),
                    child: Text(
                      unreadCount > 9 ? '9+' : '$unreadCount',
                      style: const TextStyle(
                        color: AppColors.dark,
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
            ],
          ),
          IconButton(
            tooltip: 'My Profile',
            icon: const Icon(Icons.person_outline_rounded),
            onPressed: () => context.push('/profile'),
          ),
        ],
      ),
      body: RefreshIndicator(
        color: AppColors.primary,
        onRefresh: () async {
          ref.invalidate(customerProfileProvider);
          ref.invalidate(myChituInvestmentsProvider);
          ref.invalidate(customerEnrolmentsProvider);
          ref.invalidate(jewelleryPlansProvider);
          ref.invalidate(liveGoldRateProvider);
          ref.invalidate(notificationsProvider);
          ref.invalidate(unifiedCustomerPaymentsProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(18),
          children: [
            // Customer Greeting + Account Summary Hero Card
            _buildHeroSummaryCard(
              context,
              profileAsync,
              enrolmentsAsync,
              chituInvestmentsAsync,
            ),
            const SizedBox(height: 18),

            // Unverified secondary contact reminder if applicable
            profileAsync.maybeWhen(
              data: (profile) {
                if (profile.hasUnverifiedEmail || profile.hasUnverifiedPhone) {
                  final unverifiedLabel = profile.hasUnverifiedEmail
                      ? 'email'
                      : 'phone number';
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 18),
                    child: InkWell(
                      borderRadius: BorderRadius.circular(12),
                      onTap: () => context.push('/profile'),
                      child: Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: AppColors.gold.withValues(alpha: 0.16),
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(color: AppColors.gold),
                        ),
                        child: Row(
                          children: [
                            const Icon(
                              Icons.verified_user_outlined,
                              color: AppColors.primary,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                'Your secondary $unverifiedLabel is unverified. Tap here to verify via OTP.',
                                style: const TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 13,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ),
                            const Icon(
                              Icons.chevron_right_rounded,
                              color: AppColors.primary,
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }
                return const SizedBox.shrink();
              },
              orElse: () => const SizedBox.shrink(),
            ),

            // Live Gold Rate Card (replaces Quick Actions)
            _buildLiveGoldRateCard(
              context,
              ref,
              liveGoldRateAsync,
              profileAsync,
            ),
            const SizedBox(height: 24),

            // Active Chittu Plans & Slots Section
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Expanded(
                  child: Text(
                    'My Chittu Plans & Slots',
                    style: TextStyle(
                      color: AppColors.dark,
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                TextButton(
                  onPressed: () => context.go('/chittu'),
                  child: const Text('See All'),
                ),
              ],
            ),
            chituInvestmentsAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: 20),
                child: Center(
                  child: CircularProgressIndicator(color: AppColors.primary),
                ),
              ),
              error: (err, _) => Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    children: [
                      Text(
                        err.toString().replaceFirst('Exception: ', ''),
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontSize: 13),
                      ),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        children: [
                          TextButton.icon(
                            onPressed: () =>
                                ref.invalidate(myChituInvestmentsProvider),
                            icon: const Icon(Icons.refresh, size: 16),
                            label: const Text('Retry'),
                          ),
                          ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppColors.primary,
                              foregroundColor: AppColors.white,
                            ),
                            onPressed: () => context.go('/chittu'),
                            child: const Text('Browse Chittu Plans'),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              data: (paged) {
                if (paged.items.isEmpty) {
                  return Card(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        children: [
                          const Icon(
                            Icons.savings_outlined,
                            color: AppColors.primary,
                            size: 36,
                          ),
                          const SizedBox(height: 10),
                          const Text(
                            'No Enrolled Chittu Plans',
                            style: TextStyle(
                              color: AppColors.dark,
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Join a monthly Chittu savings plan to hold slots and pay fixed monthly instalments.',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.7),
                              fontSize: 13,
                            ),
                          ),
                          const SizedBox(height: 14),
                          ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppColors.primary,
                              foregroundColor: AppColors.white,
                            ),
                            onPressed: () => context.go('/chittu'),
                            child: const Text('Browse Chittu Plans'),
                          ),
                        ],
                      ),
                    ),
                  );
                }

                return Column(
                  children: paged.items
                      .map((p) => _buildChituSummaryCard(context, ref, p))
                      .toList(),
                );
              },
            ),
            const SizedBox(height: 22),

            // Active Jewellery Enrolments Section
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Expanded(
                  child: Text(
                    'My Jewellery Enrolments',
                    style: TextStyle(
                      color: AppColors.dark,
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                TextButton(
                  onPressed: () => context.go('/jewelry'),
                  child: const Text('See All'),
                ),
              ],
            ),
            enrolmentsAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: 20),
                child: Center(
                  child: CircularProgressIndicator(color: AppColors.primary),
                ),
              ),
              error: (err, _) => Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    children: [
                      Text(
                        err.toString().replaceFirst('Exception: ', ''),
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontSize: 13),
                      ),
                      const SizedBox(height: 8),
                      TextButton.icon(
                        onPressed: () =>
                            ref.invalidate(customerEnrolmentsProvider),
                        icon: const Icon(Icons.refresh, size: 16),
                        label: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              ),
              data: (paged) {
                if (paged.items.isEmpty) {
                  return Card(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        children: [
                          const Icon(
                            Icons.workspace_premium_outlined,
                            color: AppColors.primary,
                            size: 36,
                          ),
                          const SizedBox(height: 10),
                          const Text(
                            'No Active Jewellery Enrolments',
                            style: TextStyle(
                              color: AppColors.dark,
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Start saving toward your favourite gold jewellery with flexible contributions.',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.7),
                              fontSize: 13,
                            ),
                          ),
                          const SizedBox(height: 14),
                          ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppColors.primary,
                              foregroundColor: AppColors.white,
                            ),
                            onPressed: () => context.go('/jewelry'),
                            child: const Text('Browse Jewellery Plans'),
                          ),
                        ],
                      ),
                    ),
                  );
                }

                return Column(
                  children: paged.items
                      .map((e) => _buildEnrolmentSummaryCard(context, ref, e))
                      .toList(),
                );
              },
            ),
            const SizedBox(height: 20),

            // Available Jewellery Plans Preview
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Expanded(
                  child: Text(
                    'Available Jewellery Plans',
                    style: TextStyle(
                      color: AppColors.dark,
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                TextButton(
                  onPressed: () => context.go('/jewelry'),
                  child: const Text('Explore All'),
                ),
              ],
            ),
            plansAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: 20),
                child: Center(
                  child: CircularProgressIndicator(color: AppColors.primary),
                ),
              ),
              error: (err, _) => Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Text(
                    err.toString().replaceFirst('Exception: ', ''),
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
              ),
              data: (plans) {
                if (plans.isEmpty) {
                  return Card(
                    child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Text(
                        'No jewellery plans are currently published for your branch.',
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.7),
                          fontSize: 13.5,
                        ),
                      ),
                    ),
                  );
                }
                return Column(
                  children: plans.map((plan) {
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: Card(
                        child: ListTile(
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 8,
                          ),
                          leading: Container(
                            width: 48,
                            height: 48,
                            decoration: BoxDecoration(
                              color: AppColors.primary.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: const Icon(
                              Icons.diamond_outlined,
                              color: AppColors.primary,
                            ),
                          ),
                          title: Text(
                            plan.name,
                            style: const TextStyle(
                              color: AppColors.dark,
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                            ),
                          ),
                          subtitle: Text(
                            'Target: ${Formatters.grams(plan.targetGrams, trimTrailingZeros: true)} • ${plan.categoryName ?? 'Gold Jewellery'}',
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.7),
                              fontSize: 13,
                            ),
                          ),
                          trailing: const Icon(
                            Icons.arrow_forward_ios_rounded,
                            size: 16,
                            color: AppColors.primary,
                          ),
                          onTap: () =>
                              context.push('/jewelry/plans/${plan.id}'),
                        ),
                      ),
                    );
                  }).toList(),
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildHeroSummaryCard(
    BuildContext context,
    AsyncValue profileAsync,
    AsyncValue<CustomerEnrolmentsPagedResponse> enrolmentsAsync,
    AsyncValue<CustomerChituInvestmentsPagedResponse> chituAsync,
  ) {
    final customerName = profileAsync.maybeWhen(
      data: (p) => (p.fullName != null && p.fullName!.trim().isNotEmpty)
          ? p.fullName!
          : 'Valued Customer',
      orElse: () => 'Valued Customer',
    );
    final branchName = profileAsync.maybeWhen(
      data: (p) => p.primaryBranchName ?? p.primaryBranchCode ?? 'Jaffna',
      orElse: () => 'Saranga Jewels',
    );

    final enrolments = enrolmentsAsync.maybeWhen(
      data: (res) => res.items,
      orElse: () => const <CustomerEnrolmentDto>[],
    );
    final chituPlans = chituAsync.maybeWhen(
      data: (res) => res.items,
      orElse: () => const <CustomerChituInvestmentPlanDto>[],
    );
    final totalSavedGrams = enrolments.fold<double>(
      0.0,
      (sum, item) => sum + item.netAccumulatedGoldGrams,
    );
    final totalJewelleryMoney = enrolments.fold<double>(
      0.0,
      (sum, item) => sum + item.totalConfirmedMoneyPaid,
    );
    final totalChituMoney = chituPlans.fold<double>(
      0.0,
      (sum, item) => sum + item.totalConfirmedContributions,
    );
    final totalPaidMoney = totalJewelleryMoney + totalChituMoney;
    final currency = enrolments.isNotEmpty
        ? enrolments.first.currency
        : (chituPlans.isNotEmpty ? chituPlans.first.currency : 'LKR');

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.primary,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: AppColors.gold.withValues(alpha: 0.45),
          width: 1.2,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Welcome back,',
                      style: TextStyle(
                        color: AppColors.white.withValues(alpha: 0.78),
                        fontSize: 13,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      customerName,
                      style: const TextStyle(
                        color: AppColors.white,
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 5,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.gold.withValues(alpha: 0.18),
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: AppColors.gold),
                  ),
                  child: Text(
                    branchName,
                    style: const TextStyle(
                      color: AppColors.gold,
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.white.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Accumulated Gold',
                        style: TextStyle(
                          color: AppColors.white.withValues(alpha: 0.75),
                          fontSize: 12,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        Formatters.grams(totalSavedGrams),
                        style: const TextStyle(
                          color: AppColors.gold,
                          fontSize: 17,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ],
                  ),
                ),
                Container(
                  width: 1,
                  height: 34,
                  color: AppColors.white.withValues(alpha: 0.2),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Total Contributed',
                        style: TextStyle(
                          color: AppColors.white.withValues(alpha: 0.75),
                          fontSize: 12,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        Formatters.currency(
                          totalPaidMoney,
                          currencyCode: currency,
                        ),
                        style: const TextStyle(
                          color: AppColors.white,
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLiveGoldRateCard(
    BuildContext context,
    WidgetRef ref,
    AsyncValue<CustomerGoldRateDto> liveGoldRateAsync,
    AsyncValue profileAsync,
  ) {
    final rateDto = liveGoldRateAsync.maybeWhen(
      data: (dto) => dto,
      orElse: () => const CustomerGoldRateDto(
        rate24KPerGram: 29500.0,
        rate22KPerGram: 27040.0,
        currency: 'LKR',
        rateSource: 'Saranga Jewels Branch Rate',
        isLiveFromBackend: false,
      ),
    );

    final branchName =
        (rateDto.branchName != null && rateDto.branchName!.isNotEmpty)
        ? rateDto.branchName!
        : profileAsync.maybeWhen(
            data: (p) => p.primaryBranchName ?? p.primaryBranchCode ?? 'Jaffna',
            orElse: () => 'Jaffna',
          );

    return Container(
      key: const Key('home_live_gold_rate_card'),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: AppColors.gold.withValues(alpha: 0.65),
          width: 1.3,
        ),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.05),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Row(
                  children: [
                    Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        color: AppColors.primary,
                        borderRadius: BorderRadius.circular(10),
                        border: Border.all(color: AppColors.gold, width: 1),
                      ),
                      child: const Icon(
                        Icons.auto_graph_rounded,
                        color: AppColors.gold,
                        size: 20,
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text(
                            'Live Gold Rate',
                            style: TextStyle(
                              color: AppColors.dark,
                              fontSize: 15.5,
                              fontWeight: FontWeight.bold,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          Text(
                            rateDto.rateSource != null &&
                                    rateDto.rateSource!.isNotEmpty
                                ? '$branchName • ${rateDto.rateSource}'
                                : '$branchName Branch • ${rateDto.currency}',
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.65),
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.primary,
                      borderRadius: BorderRadius.circular(20),
                      border: Border.all(color: AppColors.gold, width: 1),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          width: 6,
                          height: 6,
                          decoration: const BoxDecoration(
                            color: AppColors.gold,
                            shape: BoxShape.circle,
                          ),
                        ),
                        const SizedBox(width: 4),
                        const Text(
                          'LIVE',
                          style: TextStyle(
                            color: AppColors.gold,
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                            letterSpacing: 0.8,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 4),
                  IconButton(
                    tooltip: 'Refresh Gold Rate',
                    visualDensity: VisualDensity.compact,
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(
                      minWidth: 32,
                      minHeight: 32,
                    ),
                    icon: const Icon(
                      Icons.refresh_rounded,
                      color: AppColors.primary,
                      size: 20,
                    ),
                    onPressed: () => ref.invalidate(liveGoldRateProvider),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppColors.primary,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(
                      color: AppColors.gold.withValues(alpha: 0.45),
                    ),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Flexible(
                            child: Text(
                              '22K Jewellery (1g)',
                              style: TextStyle(
                                color: AppColors.white.withValues(alpha: 0.85),
                                fontSize: 11.5,
                                fontWeight: FontWeight.w600,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          const SizedBox(width: 4),
                          const Icon(
                            Icons.workspace_premium_outlined,
                            color: AppColors.gold,
                            size: 15,
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Text(
                          Formatters.currency(
                            rateDto.rate22KPerGram,
                            currencyCode: rateDto.currency,
                          ),
                          style: const TextStyle(
                            color: AppColors.gold,
                            fontSize: 15.5,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        '8g Sov: ${Formatters.currency(rateDto.sovereign22KRate, currencyCode: rateDto.currency, decimals: 0)}',
                        style: TextStyle(
                          color: AppColors.white.withValues(alpha: 0.78),
                          fontSize: 11,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppColors.background,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(
                      color: AppColors.primary.withValues(alpha: 0.22),
                    ),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Flexible(
                            child: Text(
                              '24K Pure Gold (1g)',
                              style: TextStyle(
                                color: AppColors.dark.withValues(alpha: 0.75),
                                fontSize: 11.5,
                                fontWeight: FontWeight.w600,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          const SizedBox(width: 4),
                          const Icon(
                            Icons.diamond_outlined,
                            color: AppColors.primary,
                            size: 15,
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Text(
                          Formatters.currency(
                            rateDto.rate24KPerGram,
                            currencyCode: rateDto.currency,
                          ),
                          style: const TextStyle(
                            color: AppColors.primary,
                            fontSize: 15.5,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        '8g Sov: ${Formatters.currency(rateDto.sovereign24KRate, currencyCode: rateDto.currency, decimals: 0)}',
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.68),
                          fontSize: 11,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
          if (rateDto.rateAsOf != null && rateDto.rateAsOf!.isNotEmpty) ...[
            const SizedBox(height: 10),
            Text(
              'Updated: ${Formatters.dateTime(rateDto.rateAsOf)}',
              style: TextStyle(
                color: AppColors.dark.withValues(alpha: 0.6),
                fontSize: 11.5,
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildChituSummaryCard(
    BuildContext context,
    WidgetRef ref,
    CustomerChituInvestmentPlanDto plan,
  ) {
    final hasArrears = plan.actualOutstandingDueThroughCurrentRound > 0;
    final dueAmount = hasArrears
        ? plan.actualOutstandingDueThroughCurrentRound
        : plan.normalMonthlyCommitment;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: () => context.push('/chittu/investments/${plan.planId}'),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (plan.code != null && plan.code!.isNotEmpty)
                            Text(
                              plan.code!,
                              style: const TextStyle(
                                color: AppColors.primary,
                                fontSize: 12,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          Text(
                            plan.name,
                            style: const TextStyle(
                              color: AppColors.dark,
                              fontSize: 15.5,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                    ),
                    SJewlsStatusBadge.fromStatus(plan.planStatus),
                  ],
                ),
                const SizedBox(height: 10),
                SJewlsInfoRow(
                  label: 'Slots Enrolled',
                  value:
                      '${plan.totalOwnedSlotsCount} Total (${plan.activeSlotsCount} Active)',
                  emphasizeValue: true,
                ),
                SJewlsInfoRow(
                  label: hasArrears
                      ? 'Outstanding Instalment'
                      : 'Next Monthly Instalment',
                  value: Formatters.currency(
                    dueAmount,
                    currencyCode: plan.currency,
                  ),
                  emphasizeValue: true,
                  goldValue: hasArrears,
                ),
                if (plan.nextRaffleDate != null &&
                    plan.nextRaffleDate!.isNotEmpty)
                  SJewlsInfoRow(
                    label: 'Due / Next Draw Date',
                    value: Formatters.date(plan.nextRaffleDate),
                  ),
                if (plan.canPayExistingSlots) ...[
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      key: Key('home_chitu_pay_${plan.planId}'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.gold,
                        foregroundColor: AppColors.dark,
                        padding: const EdgeInsets.symmetric(vertical: 12),
                      ),
                      onPressed: () =>
                          _navigateToChituPayment(context, ref, plan.planId),
                      icon: const Icon(Icons.payment_rounded, size: 18),
                      label: const Text(
                        'Pay Instalment',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildEnrolmentSummaryCard(
    BuildContext context,
    WidgetRef ref,
    CustomerEnrolmentDto enrolment,
  ) {
    final progress = (enrolment.individualProgressPercentage / 100).clamp(
      0.0,
      1.0,
    );
    final canContribute =
        enrolment.isContributionAllowed && !enrolment.isCompletedOrEligible;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: () =>
              context.push('/jewelry/enrolments/${enrolment.enrolmentId}'),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Expanded(
                      child: Text(
                        enrolment.planName,
                        style: const TextStyle(
                          color: AppColors.dark,
                          fontSize: 15.5,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    SJewlsStatusBadge.fromStatus(enrolment.enrolmentStatus),
                  ],
                ),
                const SizedBox(height: 10),
                ClipRRect(
                  borderRadius: BorderRadius.circular(6),
                  child: LinearProgressIndicator(
                    value: progress,
                    minHeight: 8,
                    backgroundColor: AppColors.primary.withValues(alpha: 0.1),
                    valueColor: const AlwaysStoppedAnimation<Color>(
                      AppColors.gold,
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Saved: ${Formatters.grams(enrolment.netAccumulatedGoldGrams)} / ${Formatters.grams(enrolment.targetGrams, trimTrailingZeros: true)}',
                      style: const TextStyle(
                        color: AppColors.dark,
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Text(
                      Formatters.percentage(
                        enrolment.individualProgressPercentage,
                      ),
                      style: const TextStyle(
                        color: AppColors.primary,
                        fontSize: 13,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Remaining: ${Formatters.grams(enrolment.remainingGrams)}',
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.7),
                        fontSize: 12,
                      ),
                    ),
                    if (enrolment.deadline != null &&
                        enrolment.deadline!.isNotEmpty)
                      Text(
                        'Deadline: ${Formatters.date(enrolment.deadline)}',
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.7),
                          fontSize: 12,
                        ),
                      ),
                  ],
                ),
                if (canContribute) ...[
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      key: Key('home_jewellery_pay_${enrolment.enrolmentId}'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.gold,
                        foregroundColor: AppColors.dark,
                        padding: const EdgeInsets.symmetric(vertical: 12),
                      ),
                      onPressed: () => _navigateToJewelleryContribution(
                        context,
                        ref,
                        enrolment.enrolmentId,
                      ),
                      icon: const Icon(Icons.savings_outlined, size: 18),
                      label: const Text(
                        'Contribute / Buy Gold',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
