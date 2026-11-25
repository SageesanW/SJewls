import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/chitu_models.dart';
import 'chitu_provider.dart';

class ChituInvestmentDetailScreen extends ConsumerWidget {
  final String planId;

  const ChituInvestmentDetailScreen({super.key, required this.planId});

  Future<void> _openPaymentFlow(
    BuildContext context,
    WidgetRef ref, {
    String? slotId,
  }) async {
    final uri = slotId != null && slotId.isNotEmpty
        ? '/chittu/investments/$planId/pay?slotId=$slotId'
        : '/chittu/investments/$planId/pay';
    await context.push(uri);
    ref.invalidate(chituInvestmentPlanDetailProvider(planId));
    ref.invalidate(myChituInvestmentsProvider);
    ref.invalidate(unifiedCustomerPaymentsProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final investmentAsync = ref.watch(
      chituInvestmentPlanDetailProvider(planId),
    );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('My Chittu Portfolio'),
        actions: [
          IconButton(
            tooltip: 'Plan Terms & Schedule',
            icon: const Icon(Icons.info_outline_rounded),
            onPressed: () => context.push('/chittu/plans/$planId'),
          ),
        ],
      ),
      body: investmentAsync.when(
        loading: () => const SJewlsLoadingView(
          message: 'Loading your Chittu slots and payment status...',
        ),
        error: (err, _) => SJewlsErrorView(
          title: 'Unable to Load Portfolio',
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () =>
              ref.invalidate(chituInvestmentPlanDetailProvider(planId)),
        ),
        data: (plan) {
          final hasArrears = plan.actualOutstandingDueThroughCurrentRound > 0;

          return Column(
            children: [
              Expanded(
                child: RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () async {
                    ref.invalidate(chituInvestmentPlanDetailProvider(planId));
                    await ref.read(
                      chituInvestmentPlanDetailProvider(planId).future,
                    );
                  },
                  child: ListView(
                    padding: const EdgeInsets.all(18),
                    children: [
                      // Hero Summary Card
                      Container(
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
                                if (plan.code != null && plan.code!.isNotEmpty)
                                  Text(
                                    plan.code!,
                                    style: const TextStyle(
                                      color: AppColors.gold,
                                      fontSize: 12.5,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                SJewlsStatusBadge.fromStatus(plan.planStatus),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Text(
                              plan.name,
                              style: const TextStyle(
                                color: AppColors.white,
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 16),
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
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          hasArrears
                                              ? 'Outstanding Due'
                                              : 'Monthly Commitment',
                                          style: TextStyle(
                                            color: AppColors.white.withValues(
                                              alpha: 0.75,
                                            ),
                                            fontSize: 12,
                                          ),
                                        ),
                                        const SizedBox(height: 4),
                                        Text(
                                          Formatters.currency(
                                            hasArrears
                                                ? plan.actualOutstandingDueThroughCurrentRound
                                                : plan.normalMonthlyCommitment,
                                            currencyCode: plan.currency,
                                          ),
                                          style: const TextStyle(
                                            color: AppColors.gold,
                                            fontSize: 16.5,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  Container(
                                    width: 1,
                                    height: 34,
                                    color: AppColors.white.withValues(
                                      alpha: 0.2,
                                    ),
                                  ),
                                  const SizedBox(width: 14),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          'Confirmed Paid',
                                          style: TextStyle(
                                            color: AppColors.white.withValues(
                                              alpha: 0.75,
                                            ),
                                            fontSize: 12,
                                          ),
                                        ),
                                        const SizedBox(height: 4),
                                        Text(
                                          Formatters.currency(
                                            plan.totalConfirmedContributions,
                                            currencyCode: plan.currency,
                                          ),
                                          style: const TextStyle(
                                            color: AppColors.white,
                                            fontSize: 16.5,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 14),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Flexible(
                                  child: Text(
                                    'Round ${plan.currentRoundIndex} of ${plan.durationMonths}',
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      color: AppColors.white,
                                      fontSize: 12.5,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                                if (plan.nextRaffleDate != null &&
                                    plan.nextRaffleDate!.isNotEmpty) ...[
                                  const SizedBox(width: 8),
                                  Flexible(
                                    child: Text(
                                      'Next Draw: ${Formatters.date(plan.nextRaffleDate)}',
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                        color: AppColors.gold,
                                        fontSize: 12.5,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ),
                                ],
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 20),

                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Expanded(
                            child: Text(
                              'Your Slots (${plan.slots.length})',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: AppColors.dark,
                                fontSize: 16,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          if (plan.canPurchaseAdditionalSlots)
                            TextButton.icon(
                              onPressed: () =>
                                  context.push('/chittu/plans/${plan.planId}'),
                              icon: const Icon(Icons.add, size: 16),
                              label: const Text('Add Slots'),
                            ),
                        ],
                      ),
                      const SizedBox(height: 8),

                      ...plan.slots.map(
                        (slot) => Padding(
                          padding: const EdgeInsets.only(bottom: 12),
                          child: _buildSlotCard(context, ref, plan, slot),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              if (plan.canPayExistingSlots)
                Container(
                  padding: const EdgeInsets.fromLTRB(18, 12, 18, 22),
                  decoration: BoxDecoration(
                    color: AppColors.white,
                    boxShadow: [
                      BoxShadow(
                        color: AppColors.dark.withValues(alpha: 0.06),
                        blurRadius: 10,
                        offset: const Offset(0, -3),
                      ),
                    ],
                  ),
                  child: SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.gold,
                        foregroundColor: AppColors.dark,
                        padding: const EdgeInsets.symmetric(vertical: 15),
                      ),
                      onPressed: () => _openPaymentFlow(context, ref),
                      icon: const Icon(Icons.payment_rounded),
                      label: const Text(
                        'Pay Instalment',
                        style: TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 15,
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  Widget _buildSlotCard(
    BuildContext context,
    WidgetRef ref,
    CustomerChituInvestmentPlanDto plan,
    CustomerChituSlotSummaryDto slot,
  ) {
    final progress = (slot.progressPercentage / 100).clamp(0.0, 1.0);

    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => context.push('/chittu/slots/${slot.slotId}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 5,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          slot.slotDisplayCode,
                          style: const TextStyle(
                            color: AppColors.primary,
                            fontWeight: FontWeight.bold,
                            fontSize: 14,
                          ),
                        ),
                      ),
                      if (slot.isWinner) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.gold,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Row(
                            children: [
                              Icon(
                                Icons.emoji_events_rounded,
                                size: 13,
                                color: AppColors.dark,
                              ),
                              SizedBox(width: 4),
                              Text(
                                'Winner',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 11.5,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  ),
                  SJewlsStatusBadge.fromStatus(slot.slotStatus),
                ],
              ),
              const SizedBox(height: 12),
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
              SJewlsInfoRow(
                label: 'Instalments Paid',
                value:
                    '${slot.instalmentsPaidCount} / ${slot.durationMonths} Months',
                emphasizeValue: true,
              ),
              SJewlsInfoRow(
                label: 'Monthly Instalment',
                value: Formatters.currency(
                  slot.monthlySlotPrice,
                  currencyCode: plan.currency,
                ),
              ),
              if (slot.arrearsAmount > 0)
                SJewlsInfoRow(
                  label: 'Arrears (${slot.arrearsMonths} mo)',
                  value: Formatters.currency(
                    slot.arrearsAmount,
                    currencyCode: plan.currency,
                  ),
                  emphasizeValue: true,
                  goldValue: true,
                ),
              if (slot.advancePaidMonths > 0)
                SJewlsInfoRow(
                  label: 'Advance Paid (${slot.advancePaidMonths} mo)',
                  value: Formatters.currency(
                    slot.advancePaidAmount,
                    currencyCode: plan.currency,
                  ),
                ),
              SJewlsInfoRow(
                label: 'Draw Eligibility',
                value: slot.isRaffleEligible
                    ? 'Eligible for Draw'
                    : (slot.raffleEligibilityReason ?? 'Not Eligible'),
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () =>
                          context.push('/chittu/slots/${slot.slotId}'),
                      child: const Text('View Schedule'),
                    ),
                  ),
                  if (slot.canPayInstalment && plan.canPayExistingSlots) ...[
                    const SizedBox(width: 10),
                    Expanded(
                      child: ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.gold,
                          foregroundColor: AppColors.dark,
                        ),
                        onPressed: () =>
                            _openPaymentFlow(context, ref, slotId: slot.slotId),
                        child: const Text(
                          'Pay Instalment',
                          style: TextStyle(fontWeight: FontWeight.bold),
                        ),
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
  }
}
