import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/jewellery_models.dart';
import 'jewellery_provider.dart';

class JewelleryTabScreen extends ConsumerStatefulWidget {
  const JewelleryTabScreen({super.key});

  @override
  ConsumerState<JewelleryTabScreen> createState() => _JewelleryTabScreenState();
}

class _JewelleryTabScreenState extends ConsumerState<JewelleryTabScreen> {
  bool _showEnrolments = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Jewellery Savings Plans'),
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
      body: Column(
        children: [
          // Top segment toggle: Explore Plans vs My Enrolments
          Container(
            color: AppColors.white,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
            child: Row(
              children: [
                Expanded(
                  child: _buildSegmentButton(
                    label: 'Explore Plans',
                    icon: Icons.diamond_outlined,
                    selected: !_showEnrolments,
                    onTap: () => setState(() => _showEnrolments = false),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: _buildSegmentButton(
                    label: 'My Enrolments',
                    icon: Icons.account_balance_wallet_outlined,
                    selected: _showEnrolments,
                    onTap: () => setState(() => _showEnrolments = true),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: _showEnrolments
                ? _buildMyEnrolmentsView(context)
                : _buildExplorePlansView(context),
          ),
        ],
      ),
    );
  }

  Widget _buildSegmentButton({
    required String label,
    required IconData icon,
    required bool selected,
    required VoidCallback onTap,
  }) {
    return InkWell(
      borderRadius: BorderRadius.circular(10),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 11, horizontal: 12),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : AppColors.background,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: selected
                ? AppColors.primary
                : AppColors.dark.withValues(alpha: 0.15),
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              icon,
              size: 18,
              color: selected ? AppColors.gold : AppColors.dark,
            ),
            const SizedBox(width: 8),
            Flexible(
              child: Text(
                label,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: selected ? AppColors.white : AppColors.dark,
                  fontWeight: FontWeight.w600,
                  fontSize: 13.5,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildExplorePlansView(BuildContext context) {
    final categoriesAsync = ref.watch(jewelleryCategoriesProvider);
    final selectedCategoryId = ref.watch(selectedJewelleryCategoryIdProvider);
    final plansAsync = ref.watch(jewelleryPlansProvider);

    return Column(
      children: [
        // Category chips bar
        categoriesAsync.maybeWhen(
          data: (categories) {
            if (categories.isEmpty) return const SizedBox.shrink();
            return Container(
              width: double.infinity,
              color: AppColors.white,
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: const Text('All Categories'),
                        selected: selectedCategoryId == null,
                        selectedColor: AppColors.primary,
                        backgroundColor: AppColors.background,
                        labelStyle: TextStyle(
                          color: selectedCategoryId == null
                              ? AppColors.gold
                              : AppColors.dark,
                          fontWeight: selectedCategoryId == null
                              ? FontWeight.bold
                              : FontWeight.w500,
                        ),
                        onSelected: (_) {
                          ref
                                  .read(
                                    selectedJewelleryCategoryIdProvider
                                        .notifier,
                                  )
                                  .state =
                              null;
                        },
                      ),
                    ),
                    ...categories.map((cat) {
                      final isSelected = selectedCategoryId == cat.id;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(
                            '${cat.name} (${cat.availablePlansCount})',
                          ),
                          selected: isSelected,
                          selectedColor: AppColors.primary,
                          backgroundColor: AppColors.background,
                          labelStyle: TextStyle(
                            color: isSelected ? AppColors.gold : AppColors.dark,
                            fontWeight: isSelected
                                ? FontWeight.bold
                                : FontWeight.w500,
                          ),
                          onSelected: (_) {
                            ref
                                .read(
                                  selectedJewelleryCategoryIdProvider.notifier,
                                )
                                .state = isSelected
                                ? null
                                : cat.id;
                          },
                        ),
                      );
                    }),
                  ],
                ),
              ),
            );
          },
          orElse: () => const SizedBox.shrink(),
        ),
        Expanded(
          child: plansAsync.when(
            loading: () => const SJewlsLoadingView(
              message: 'Loading available jewellery plans...',
            ),
            error: (err, _) => SJewlsErrorView(
              message: err.toString().replaceFirst('Exception: ', ''),
              onRetry: () {
                ref.invalidate(jewelleryCategoriesProvider);
                ref.invalidate(jewelleryPlansProvider);
              },
            ),
            data: (plans) {
              if (plans.isEmpty) {
                return SJewlsEmptyView(
                  icon: Icons.diamond_outlined,
                  title: 'No Jewellery Plans Available',
                  subtitle: selectedCategoryId != null
                      ? 'No active plans were found in the selected category.'
                      : 'There are currently no active jewellery plans for your branch.',
                  actionLabel: selectedCategoryId != null
                      ? 'Show All Categories'
                      : 'Refresh',
                  onAction: () {
                    if (selectedCategoryId != null) {
                      ref
                              .read(
                                selectedJewelleryCategoryIdProvider.notifier,
                              )
                              .state =
                          null;
                    } else {
                      ref.invalidate(jewelleryPlansProvider);
                    }
                  },
                );
              }

              return RefreshIndicator(
                color: AppColors.primary,
                onRefresh: () async {
                  ref.invalidate(jewelleryCategoriesProvider);
                  ref.invalidate(jewelleryPlansProvider);
                  await ref.read(jewelleryPlansProvider.future);
                },
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: plans.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 14),
                  itemBuilder: (context, index) {
                    final plan = plans[index];
                    return _buildPlanCard(context, plan);
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildPlanCard(BuildContext context, CustomerJewelleryPlanDto plan) {
    final durationsText = plan.offeredDurations.isNotEmpty
        ? '${plan.offeredDurations.join(', ')} months'
        : 'Flexible duration';

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/jewelry/plans/${plan.id}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Product Image Banner
            SizedBox(
              height: 165,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  if (plan.imageUrl != null && plan.imageUrl!.trim().isNotEmpty)
                    Image.network(
                      plan.imageUrl!,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => _buildFallbackBanner(),
                    )
                  else
                    _buildFallbackBanner(),
                  Positioned(
                    top: 12,
                    left: 12,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 5,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.9),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        plan.categoryName ?? 'Gold Jewellery',
                        style: const TextStyle(
                          color: AppColors.gold,
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    plan.name,
                    style: const TextStyle(
                      color: AppColors.dark,
                      fontSize: 17,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  if (plan.description != null &&
                      plan.description!.trim().isNotEmpty) ...[
                    const SizedBox(height: 6),
                    Text(
                      plan.description!,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.72),
                        fontSize: 13.5,
                      ),
                    ),
                  ],
                  const SizedBox(height: 14),
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.05),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Column(
                      children: [
                        SJewlsInfoRow(
                          label: 'Target Gold Weight',
                          value: Formatters.grams(
                            plan.targetGrams,
                            trimTrailingZeros: true,
                          ),
                          emphasizeValue: true,
                          goldValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Offered Durations',
                          value: durationsText,
                        ),
                        SJewlsInfoRow(
                          label: 'Making / Plan Charges',
                          value: Formatters.percentage(plan.premiumPercentage),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 14),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: AppColors.white,
                      ),
                      onPressed: () =>
                          context.push('/jewelry/plans/${plan.id}'),
                      child: const Text('View Plan & Enrol'),
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

  Widget _buildFallbackBanner() {
    return Container(
      color: AppColors.primary,
      alignment: Alignment.center,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.diamond_outlined, color: AppColors.gold, size: 46),
          const SizedBox(height: 6),
          Text(
            'SARANGA JEWELS',
            style: TextStyle(
              color: AppColors.gold.withValues(alpha: 0.85),
              fontSize: 12,
              letterSpacing: 3,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMyEnrolmentsView(BuildContext context) {
    final enrolmentsAsync = ref.watch(customerEnrolmentsProvider);

    return enrolmentsAsync.when(
      loading: () => const SJewlsLoadingView(
        message: 'Loading your jewellery enrolments...',
      ),
      error: (err, _) => SJewlsErrorView(
        message: err.toString().replaceFirst('Exception: ', ''),
        onRetry: () => ref.invalidate(customerEnrolmentsProvider),
      ),
      data: (paged) {
        if (paged.items.isEmpty) {
          return SJewlsEmptyView(
            icon: Icons.account_balance_wallet_outlined,
            title: 'No Enrolments Yet',
            subtitle:
                'You have not joined any jewellery savings plans yet. Explore available plans to start saving in gold.',
            actionLabel: 'Explore Plans',
            onAction: () => setState(() => _showEnrolments = false),
          );
        }

        return RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async {
            ref.invalidate(customerEnrolmentsProvider);
            await ref.read(customerEnrolmentsProvider.future);
          },
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: paged.items.length,
            separatorBuilder: (_, _) => const SizedBox(height: 14),
            itemBuilder: (context, index) {
              final enrolment = paged.items[index];
              final progress = (enrolment.individualProgressPercentage / 100)
                  .clamp(0.0, 1.0);

              return Card(
                child: InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: () => context.push(
                    '/jewelry/enrolments/${enrolment.enrolmentId}',
                  ),
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
                                  Text(
                                    enrolment.planName,
                                    style: const TextStyle(
                                      color: AppColors.dark,
                                      fontSize: 16.5,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                  if (enrolment.categoryName != null)
                                    Text(
                                      enrolment.categoryName!,
                                      style: TextStyle(
                                        color: AppColors.dark.withValues(
                                          alpha: 0.65,
                                        ),
                                        fontSize: 12.5,
                                      ),
                                    ),
                                ],
                              ),
                            ),
                            SJewlsStatusBadge.fromStatus(
                              enrolment.enrolmentStatus,
                            ),
                          ],
                        ),
                        const SizedBox(height: 14),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(6),
                          child: LinearProgressIndicator(
                            value: progress,
                            minHeight: 9,
                            backgroundColor: AppColors.primary.withValues(
                              alpha: 0.1,
                            ),
                            valueColor: const AlwaysStoppedAnimation<Color>(
                              AppColors.gold,
                            ),
                          ),
                        ),
                        const SizedBox(height: 8),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Flexible(
                              child: Text(
                                'Progress: ${Formatters.percentage(enrolment.individualProgressPercentage)}',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: AppColors.primary,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                            const SizedBox(width: 8),
                            Flexible(
                              child: Text(
                                'Deadline: ${Formatters.date(enrolment.deadline)}',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: AppColors.dark.withValues(alpha: 0.68),
                                  fontSize: 12.5,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const Divider(height: 22),
                        SJewlsInfoRow(
                          label: 'Accumulated Gold',
                          value: Formatters.grams(
                            enrolment.netAccumulatedGoldGrams,
                          ),
                          emphasizeValue: true,
                          goldValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Remaining Target',
                          value: Formatters.grams(enrolment.remainingGrams),
                        ),
                        SJewlsInfoRow(
                          label: 'Confirmed Paid',
                          value: Formatters.currency(
                            enrolment.totalConfirmedMoneyPaid,
                            currencyCode: enrolment.currency,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Row(
                          children: [
                            Expanded(
                              child: OutlinedButton(
                                onPressed: () => context.push(
                                  '/jewelry/enrolments/${enrolment.enrolmentId}',
                                ),
                                child: const Text('View Progress'),
                              ),
                            ),
                            if (enrolment.isContributionAllowed) ...[
                              const SizedBox(width: 10),
                              Expanded(
                                child: ElevatedButton(
                                  style: ElevatedButton.styleFrom(
                                    backgroundColor: AppColors.primary,
                                    foregroundColor: AppColors.white,
                                  ),
                                  onPressed: () => context.push(
                                    '/jewelry/enrolments/${enrolment.enrolmentId}/buy-gold',
                                  ),
                                  child: const Text('Buy Gold'),
                                ),
                              ),
                            ],
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }
}
