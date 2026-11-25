import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import 'chitu_provider.dart';

class ChituSlotDetailScreen extends ConsumerWidget {
  final String slotId;

  const ChituSlotDetailScreen({super.key, required this.slotId});

  Future<void> _openSlotPayment(
    BuildContext context,
    WidgetRef ref,
    String planId,
  ) async {
    await context.push('/chittu/investments/$planId/pay?slotId=$slotId');
    ref.invalidate(chituSlotDetailProvider(slotId));
    ref.invalidate(chituSlotPaymentsProvider(slotId));
    ref.invalidate(chituInvestmentPlanDetailProvider(planId));
    ref.invalidate(myChituInvestmentsProvider);
    ref.invalidate(unifiedCustomerPaymentsProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final slotAsync = ref.watch(chituSlotDetailProvider(slotId));
    final paymentsAsync = ref.watch(chituSlotPaymentsProvider(slotId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Slot Schedule & Details')),
      body: slotAsync.when(
        loading: () => const SJewlsLoadingView(
          message: 'Loading slot schedule and instalments...',
        ),
        error: (err, _) => SJewlsErrorView(
          title: 'Unable to Load Slot Details',
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () => ref.invalidate(chituSlotDetailProvider(slotId)),
        ),
        data: (slot) {
          final progress = (slot.progressPercentage / 100).clamp(0.0, 1.0);
          final canPay = slot.canPayInstalment && slot.planId.isNotEmpty;

          return Column(
            children: [
              Expanded(
                child: RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () async {
                    ref.invalidate(chituSlotDetailProvider(slotId));
                    ref.invalidate(chituSlotPaymentsProvider(slotId));
                    await ref.read(chituSlotDetailProvider(slotId).future);
                  },
                  child: ListView(
                    padding: const EdgeInsets.all(18),
                    children: [
                      // Slot Hero Card
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
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 10,
                                    vertical: 5,
                                  ),
                                  decoration: BoxDecoration(
                                    color: AppColors.gold.withValues(
                                      alpha: 0.2,
                                    ),
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: Text(
                                    slot.slotDisplayCode,
                                    style: const TextStyle(
                                      color: AppColors.gold,
                                      fontSize: 13.5,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                                SJewlsStatusBadge.fromStatus(slot.slotStatus),
                              ],
                            ),
                            const SizedBox(height: 10),
                            Text(
                              slot.planName,
                              style: const TextStyle(
                                color: AppColors.white,
                                fontSize: 19,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 14),
                            ClipRRect(
                              borderRadius: BorderRadius.circular(6),
                              child: LinearProgressIndicator(
                                value: progress,
                                minHeight: 8,
                                backgroundColor: AppColors.white.withValues(
                                  alpha: 0.16,
                                ),
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
                                  'Paid: ${slot.instalmentsPaidCount} / ${slot.durationMonths} Months',
                                  style: const TextStyle(
                                    color: AppColors.white,
                                    fontSize: 13,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                Text(
                                  Formatters.percentage(
                                    slot.progressPercentage,
                                  ),
                                  style: const TextStyle(
                                    color: AppColors.gold,
                                    fontSize: 13,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Winner Benefit Banner if Slot Won
                      if (slot.isWinner) ...[
                        Card(
                          color: AppColors.gold.withValues(alpha: 0.16),
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Row(
                                  children: [
                                    Icon(
                                      Icons.emoji_events_rounded,
                                      color: AppColors.primary,
                                    ),
                                    SizedBox(width: 8),
                                    Text(
                                      'Winning Slot Entitlement',
                                      style: TextStyle(
                                        color: AppColors.dark,
                                        fontSize: 16,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 10),
                                if (slot.winningRoundIndex != null)
                                  SJewlsInfoRow(
                                    label: 'Winning Round',
                                    value: 'Round ${slot.winningRoundIndex}',
                                    emphasizeValue: true,
                                  ),
                                if (slot.winningDrawDate != null &&
                                    slot.winningDrawDate!.isNotEmpty)
                                  SJewlsInfoRow(
                                    label: 'Draw Date',
                                    value: Formatters.date(
                                      slot.winningDrawDate,
                                    ),
                                  ),
                                if (slot.winningPrizeAmount != null)
                                  SJewlsInfoRow(
                                    label: 'Benefit Value',
                                    value: Formatters.currency(
                                      slot.winningPrizeAmount!,
                                      currencyCode: slot.currency,
                                    ),
                                    emphasizeValue: true,
                                    goldValue: true,
                                  ),
                                SJewlsInfoRow(
                                  label: 'Claim Status',
                                  value:
                                      slot.entitlementClaimInfo?.status ??
                                      (slot.winningEntitlementClaimed
                                          ? 'Claimed'
                                          : 'Unclaimed (Collect at Branch)'),
                                  emphasizeValue: true,
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                      ],

                      // Slot Financial Summary
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Slot Summary',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 10),
                              SJewlsInfoRow(
                                label: 'Monthly Instalment',
                                value: Formatters.currency(
                                  slot.monthlySlotPrice,
                                  currencyCode: slot.currency,
                                ),
                                emphasizeValue: true,
                              ),
                              SJewlsInfoRow(
                                label: 'Total Confirmed Paid',
                                value: Formatters.currency(
                                  slot.totalConfirmedContributions,
                                  currencyCode: slot.currency,
                                ),
                                emphasizeValue: true,
                              ),
                              SJewlsInfoRow(
                                label: 'Arrears (${slot.arrearsMonths} mo)',
                                value: Formatters.currency(
                                  slot.arrearsAmount,
                                  currencyCode: slot.currency,
                                ),
                                emphasizeValue: slot.arrearsAmount > 0,
                                goldValue: slot.arrearsAmount > 0,
                              ),
                              SJewlsInfoRow(
                                label: 'Advance (${slot.advancePaidMonths} mo)',
                                value: Formatters.currency(
                                  slot.advancePaidAmount,
                                  currencyCode: slot.currency,
                                ),
                              ),
                              SJewlsInfoRow(
                                label: 'Draw Eligibility',
                                value: slot.isRaffleEligible
                                    ? 'Eligible'
                                    : (slot.raffleEligibilityReason ??
                                          'Not Eligible'),
                              ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Instalment Schedule
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Instalment Schedule (${slot.instalments.length})',
                                style: const TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 12),
                              if (slot.instalments.isEmpty)
                                Text(
                                  'No instalment rows returned for this slot.',
                                  style: TextStyle(
                                    color: AppColors.dark.withValues(
                                      alpha: 0.7,
                                    ),
                                    fontSize: 13,
                                  ),
                                )
                              else
                                ...slot.instalments.map((inst) {
                                  return Container(
                                    margin: const EdgeInsets.only(bottom: 10),
                                    padding: const EdgeInsets.all(12),
                                    decoration: BoxDecoration(
                                      color: AppColors.background,
                                      borderRadius: BorderRadius.circular(10),
                                      border: Border.all(
                                        color: AppColors.dark.withValues(
                                          alpha: 0.08,
                                        ),
                                      ),
                                    ),
                                    child: Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment:
                                                CrossAxisAlignment.start,
                                            children: [
                                              Text(
                                                'Month ${inst.monthIndex} • ${Formatters.currency(inst.amountDue, currencyCode: slot.currency)}',
                                                style: const TextStyle(
                                                  color: AppColors.dark,
                                                  fontWeight: FontWeight.bold,
                                                  fontSize: 13.5,
                                                ),
                                              ),
                                              const SizedBox(height: 3),
                                              Text(
                                                inst.paidAtUtc != null &&
                                                        inst
                                                            .paidAtUtc!
                                                            .isNotEmpty
                                                    ? 'Paid: ${Formatters.dateTime(inst.paidAtUtc)}'
                                                    : 'Due: ${Formatters.date(inst.dueDate)}',
                                                style: TextStyle(
                                                  color: AppColors.dark
                                                      .withValues(alpha: 0.65),
                                                  fontSize: 12,
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),
                                        SJewlsStatusBadge.fromStatus(
                                          inst.status,
                                        ),
                                      ],
                                    ),
                                  );
                                }),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Slot Payment History
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Slot Payment History',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 10),
                              paymentsAsync.when(
                                loading: () => const Padding(
                                  padding: EdgeInsets.symmetric(vertical: 12),
                                  child: Center(
                                    child: CircularProgressIndicator(
                                      color: AppColors.primary,
                                    ),
                                  ),
                                ),
                                error: (_, _) => Text(
                                  'Unable to load payment history for this slot.',
                                  style: TextStyle(
                                    color: AppColors.dark.withValues(
                                      alpha: 0.7,
                                    ),
                                    fontSize: 13,
                                  ),
                                ),
                                data: (payments) {
                                  if (payments.isEmpty) {
                                    return Text(
                                      'No payment receipts recorded for this slot yet.',
                                      style: TextStyle(
                                        color: AppColors.dark.withValues(
                                          alpha: 0.7,
                                        ),
                                        fontSize: 13,
                                      ),
                                    );
                                  }
                                  return Column(
                                    children: payments.map((p) {
                                      return ListTile(
                                        contentPadding: EdgeInsets.zero,
                                        title: Text(
                                          Formatters.currency(
                                            p.amount,
                                            currencyCode: p.currency,
                                          ),
                                          style: const TextStyle(
                                            fontWeight: FontWeight.bold,
                                            fontSize: 14,
                                          ),
                                        ),
                                        subtitle: Text(
                                          '${Formatters.dateTime(p.confirmedAtUtc ?? p.createdAtUtc)}${p.paymentMethod != null ? ' • ${p.paymentMethod}' : ''}',
                                          style: const TextStyle(fontSize: 12),
                                        ),
                                        trailing: SJewlsStatusBadge.fromStatus(
                                          p.status,
                                        ),
                                      );
                                    }).toList(),
                                  );
                                },
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              if (canPay)
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
                      onPressed: () =>
                          _openSlotPayment(context, ref, slot.planId),
                      icon: const Icon(Icons.payment_rounded),
                      label: const Text(
                        'Pay Instalment for This Slot',
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
}
