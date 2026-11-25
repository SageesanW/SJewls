import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/jewellery_models.dart';
import 'jewellery_provider.dart';

class EnrolmentDetailScreen extends ConsumerWidget {
  final String enrolmentId;

  const EnrolmentDetailScreen({super.key, required this.enrolmentId});

  void _showInStoreRequestGuidance(BuildContext context, String requestType) {
    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: AppColors.white,
        title: Text(
          requestType,
          style: const TextStyle(
            color: AppColors.dark,
            fontWeight: FontWeight.bold,
          ),
        ),
        content: Text(
          '$requestType requests are processed and recorded by authorised staff at your primary Saranga Jewels branch. Please visit or contact your branch with your enrolment details.',
          style: TextStyle(
            color: AppColors.dark.withValues(alpha: 0.78),
            fontSize: 14,
            height: 1.45,
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Understood'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final enrolmentAsync = ref.watch(enrolmentDetailProvider(enrolmentId));
    final paymentsAsync = ref.watch(enrolmentPaymentsProvider(enrolmentId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Enrolment Progress'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () {
              ref.invalidate(enrolmentDetailProvider(enrolmentId));
              ref.invalidate(enrolmentPaymentsProvider(enrolmentId));
            },
          ),
        ],
      ),
      body: enrolmentAsync.when(
        loading: () =>
            const SJewlsLoadingView(message: 'Loading enrolment progress...'),
        error: (err, _) => SJewlsErrorView(
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () {
            ref.invalidate(enrolmentDetailProvider(enrolmentId));
            ref.invalidate(enrolmentPaymentsProvider(enrolmentId));
          },
        ),
        data: (CustomerEnrolmentDto enrolment) {
          final progress = (enrolment.individualProgressPercentage / 100).clamp(
            0.0,
            1.0,
          );

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async {
              ref.invalidate(enrolmentDetailProvider(enrolmentId));
              ref.invalidate(enrolmentPaymentsProvider(enrolmentId));
              await ref.read(enrolmentDetailProvider(enrolmentId).future);
            },
            child: ListView(
              padding: const EdgeInsets.all(18),
              children: [
                // Top Progress Hero Card
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
                          Expanded(
                            child: Text(
                              enrolment.planName,
                              style: const TextStyle(
                                color: AppColors.white,
                                fontSize: 19,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          SJewlsStatusBadge(
                            label: enrolment.enrolmentStatus,
                            isHighlighted: true,
                          ),
                        ],
                      ),
                      if (enrolment.categoryName != null) ...[
                        const SizedBox(height: 4),
                        Text(
                          enrolment.categoryName!,
                          style: TextStyle(
                            color: AppColors.white.withValues(alpha: 0.78),
                            fontSize: 13,
                          ),
                        ),
                      ],
                      const SizedBox(height: 18),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Flexible(
                            child: Text(
                              'Saved Gold: ${Formatters.grams(enrolment.netAccumulatedGoldGrams)}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: AppColors.gold,
                                fontSize: 15,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            Formatters.percentage(
                              enrolment.individualProgressPercentage,
                            ),
                            style: const TextStyle(
                              color: AppColors.white,
                              fontSize: 15,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      ClipRRect(
                        borderRadius: BorderRadius.circular(6),
                        child: LinearProgressIndicator(
                          value: progress,
                          minHeight: 10,
                          backgroundColor: AppColors.white.withValues(
                            alpha: 0.16,
                          ),
                          valueColor: const AlwaysStoppedAnimation<Color>(
                            AppColors.gold,
                          ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Flexible(
                            child: Text(
                              'Target: ${Formatters.grams(enrolment.targetGrams, trimTrailingZeros: true)}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: AppColors.white.withValues(alpha: 0.85),
                                fontSize: 12.5,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Flexible(
                            child: Text(
                              'Remaining: ${Formatters.grams(enrolment.remainingGrams)}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: AppColors.white.withValues(alpha: 0.85),
                                fontSize: 12.5,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),

                // Physical Store Claim Guidance Banner (when Completed / Eligible)
                if (enrolment.isCompletedOrEligible) ...[
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: AppColors.gold.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: AppColors.gold, width: 1.5),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Icon(
                          Icons.storefront_rounded,
                          color: AppColors.primary,
                          size: 26,
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Ready for Physical Store Collection',
                                style: TextStyle(
                                  color: AppColors.primary,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 15,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                'Your enrolment has reached its target. Please visit your Saranga Jewels branch in person with your registered NIC to complete physical collection.',
                                style: TextStyle(
                                  color: AppColors.dark.withValues(alpha: 0.82),
                                  fontSize: 13,
                                  height: 1.4,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // Blocked Contribution Warning if applicable
                if (!enrolment.isContributionAllowed &&
                    enrolment.contributionBlockedReason != null &&
                    enrolment.contributionBlockedReason!.trim().isNotEmpty) ...[
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: AppColors.dark.withValues(alpha: 0.06),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.info_outline_rounded,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            enrolment.contributionBlockedReason!,
                            style: const TextStyle(
                              color: AppColors.dark,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // Enrolment Snapshot Terms Card
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Enrolment Terms & Balances',
                          style: TextStyle(
                            color: AppColors.dark,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 12),
                        SJewlsInfoRow(
                          label: 'Total Confirmed Paid',
                          value: Formatters.currency(
                            enrolment.totalConfirmedMoneyPaid,
                            currencyCode: enrolment.currency,
                          ),
                          emphasizeValue: true,
                          goldValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Net Accumulated Gold',
                          value: Formatters.grams(
                            enrolment.netAccumulatedGoldGrams,
                          ),
                          emphasizeValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Remaining Gold to Target',
                          value: Formatters.grams(enrolment.remainingGrams),
                        ),
                        SJewlsInfoRow(
                          label: 'Making / Plan Charges',
                          value: Formatters.percentage(
                            enrolment.premiumPercentage,
                          ),
                        ),
                        SJewlsInfoRow(
                          label: 'Selected Duration',
                          value: '${enrolment.selectedDurationMonths} Months',
                        ),
                        SJewlsInfoRow(
                          label: 'Joining Date',
                          value: Formatters.date(enrolment.joiningDate),
                        ),
                        SJewlsInfoRow(
                          label: 'Target Deadline',
                          value: Formatters.date(enrolment.deadline),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Buy Gold Action Button
                if (enrolment.isContributionAllowed) ...[
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: AppColors.white,
                      padding: const EdgeInsets.symmetric(vertical: 15),
                    ),
                    onPressed: () => context.push(
                      '/jewelry/enrolments/${enrolment.enrolmentId}/buy-gold',
                    ),
                    icon: const Icon(
                      Icons.add_circle_outline_rounded,
                      color: AppColors.gold,
                    ),
                    label: const Text('Buy Gold / Make Contribution'),
                  ),
                  const SizedBox(height: 12),
                ],

                // Extension / Early Closure In-Store Support Options
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () => _showInStoreRequestGuidance(
                          context,
                          'Duration Extension',
                        ),
                        child: const Text('Extension Info'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () => _showInStoreRequestGuidance(
                          context,
                          'Early Closure',
                        ),
                        child: const Text('Early Closure Info'),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 22),

                // Contribution History Section
                const Text(
                  'Confirmed Contribution History',
                  style: TextStyle(
                    color: AppColors.dark,
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 10),
                paymentsAsync.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.symmetric(vertical: 20),
                    child: Center(
                      child: CircularProgressIndicator(
                        color: AppColors.primary,
                      ),
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
                  data: (payments) {
                    if (payments.isEmpty) {
                      return Card(
                        child: Padding(
                          padding: const EdgeInsets.all(20),
                          child: Column(
                            children: [
                              const Icon(
                                Icons.history_rounded,
                                color: AppColors.primary,
                                size: 32,
                              ),
                              const SizedBox(height: 8),
                              const Text(
                                'No Confirmed Contributions Yet',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                'Each confirmed gold contribution and its locked historical rate will be recorded here permanently.',
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  color: AppColors.dark.withValues(alpha: 0.7),
                                  fontSize: 13,
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    }

                    return Column(
                      children: payments.map((payment) {
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: Card(
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      Flexible(
                                        child: Text(
                                          Formatters.currency(
                                            payment.currencyAmount,
                                            currencyCode: payment.currency,
                                          ),
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: const TextStyle(
                                            color: AppColors.primary,
                                            fontSize: 16,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ),
                                      const SizedBox(width: 8),
                                      SJewlsStatusBadge.fromStatus(
                                        payment.status,
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: 8),
                                  SJewlsInfoRow(
                                    label: 'Gold Credited',
                                    value:
                                        '+${Formatters.grams(payment.goldGramsCredited)}',
                                    emphasizeValue: true,
                                  ),
                                  SJewlsInfoRow(
                                    label: 'Locked Gold Rate',
                                    value:
                                        '${Formatters.currency(payment.appliedRatePerGram, currencyCode: payment.currency)} / g',
                                  ),
                                  if (payment.paymentMethod != null &&
                                      payment.paymentMethod!.isNotEmpty)
                                    SJewlsInfoRow(
                                      label: 'Method',
                                      value: payment.paymentMethod!,
                                    ),
                                  if (payment.paymentReference != null &&
                                      payment.paymentReference!.isNotEmpty)
                                    SJewlsInfoRow(
                                      label: 'Reference',
                                      value: payment.paymentReference!,
                                    ),
                                  SJewlsInfoRow(
                                    label: 'Confirmed At',
                                    value: Formatters.dateTime(
                                      payment.confirmedAtUtc,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        );
                      }).toList(),
                    );
                  },
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
