import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../../chitu/data/chitu_models.dart';
import '../../chitu/presentation/chitu_provider.dart';
import '../../jewellery/presentation/jewellery_provider.dart';

class PaymentHistoryScreen extends ConsumerStatefulWidget {
  const PaymentHistoryScreen({super.key});

  @override
  ConsumerState<PaymentHistoryScreen> createState() =>
      _PaymentHistoryScreenState();
}

class _PaymentHistoryScreenState extends ConsumerState<PaymentHistoryScreen> {
  String _selectedPlanType = 'All';
  String _selectedStatus = 'All';

  static const List<String> _planTypeFilters = ['All', 'Chitu', 'Jewellery'];
  static const List<String> _statusFilters = [
    'All',
    'Confirmed',
    'Pending',
    'Failed',
  ];

  bool _matchesFilters(CustomerPaymentSummaryDto item) {
    if (_selectedPlanType != 'All') {
      final pt = item.planType.toLowerCase();
      if (_selectedPlanType == 'Chitu' &&
          !pt.contains('chitu') &&
          !pt.contains('chittu')) {
        return false;
      }
      if (_selectedPlanType == 'Jewellery' && !pt.contains('jewel')) {
        return false;
      }
    }

    if (_selectedStatus != 'All') {
      final s = item.status.toLowerCase();
      if (_selectedStatus == 'Confirmed') {
        return s.contains('confirm') ||
            s.contains('succeed') ||
            s.contains('paid') ||
            s.contains('complete');
      }
      if (_selectedStatus == 'Pending') {
        return s.contains('pend');
      }
      if (_selectedStatus == 'Failed') {
        return s.contains('fail') ||
            s.contains('cancel') ||
            s.contains('reject');
      }
    }
    return true;
  }

  void _openReceiptDetailSheet(
    BuildContext context,
    CustomerPaymentSummaryDto summary,
  ) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.background,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (_) => _PaymentReceiptBottomSheet(summary: summary),
    );
  }

  @override
  Widget build(BuildContext context) {
    final paymentsAsync = ref.watch(unifiedCustomerPaymentsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Payment History'),
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
          Container(
            width: double.infinity,
            color: AppColors.white,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: _planTypeFilters.map((type) {
                      final selected = _selectedPlanType == type;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(type == 'Chitu' ? 'Chittu Plans' : type),
                          selected: selected,
                          selectedColor: AppColors.primary,
                          backgroundColor: AppColors.background,
                          labelStyle: TextStyle(
                            color: selected ? AppColors.gold : AppColors.dark,
                            fontWeight: selected
                                ? FontWeight.bold
                                : FontWeight.w500,
                          ),
                          onSelected: (_) {
                            setState(() => _selectedPlanType = type);
                          },
                        ),
                      );
                    }).toList(),
                  ),
                ),
                const SizedBox(height: 6),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: _statusFilters.map((filter) {
                      final selected = _selectedStatus == filter;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: FilterChip(
                          label: Text(filter),
                          selected: selected,
                          selectedColor: AppColors.gold.withValues(alpha: 0.25),
                          backgroundColor: AppColors.background,
                          checkmarkColor: AppColors.primary,
                          labelStyle: TextStyle(
                            color: AppColors.dark,
                            fontSize: 12.5,
                            fontWeight: selected
                                ? FontWeight.bold
                                : FontWeight.w500,
                          ),
                          onSelected: (_) {
                            setState(() => _selectedStatus = filter);
                          },
                        ),
                      );
                    }).toList(),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: paymentsAsync.when(
              loading: () => const SJewlsLoadingView(
                message: 'Loading your payment history...',
              ),
              error: (err, _) => SJewlsErrorView(
                message: err.toString().replaceFirst('Exception: ', ''),
                onRetry: () {
                  ref.invalidate(allCustomerPaymentsProvider);
                  ref.invalidate(unifiedCustomerPaymentsProvider);
                },
              ),
              data: (payments) {
                final filtered = payments.where(_matchesFilters).toList();
                if (filtered.isEmpty) {
                  return SJewlsEmptyView(
                    icon: Icons.receipt_long_outlined,
                    title: payments.isEmpty
                        ? 'No Payment Records Found'
                        : 'No Matching Payments',
                    subtitle: payments.isEmpty
                        ? 'Confirmed Chittu instalments and Jewellery plan contributions will appear here.'
                        : 'No payment records match the selected filters.',
                    actionLabel: payments.isEmpty
                        ? 'Explore Plans'
                        : 'Reset Filters',
                    onAction: () {
                      if (payments.isEmpty) {
                        context.go('/jewelry');
                      } else {
                        setState(() {
                          _selectedPlanType = 'All';
                          _selectedStatus = 'All';
                        });
                      }
                    },
                  );
                }

                final totalConfirmed = filtered.fold<double>(
                  0.0,
                  (sum, p) => sum + p.amount,
                );
                final currency = filtered.first.currency.isNotEmpty
                    ? filtered.first.currency
                    : 'LKR';

                return RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () async {
                    ref.invalidate(allCustomerPaymentsProvider);
                    ref.invalidate(unifiedCustomerPaymentsProvider);
                    await ref.read(unifiedCustomerPaymentsProvider.future);
                  },
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: filtered.length + 1,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      if (index == 0) {
                        return Container(
                          padding: const EdgeInsets.all(18),
                          decoration: BoxDecoration(
                            color: AppColors.primary,
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Total Paid (${filtered.length} Transactions)',
                                      style: TextStyle(
                                        color: AppColors.white.withValues(
                                          alpha: 0.75,
                                        ),
                                        fontSize: 12.5,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      Formatters.currency(
                                        totalConfirmed,
                                        currencyCode: currency,
                                      ),
                                      style: const TextStyle(
                                        color: AppColors.gold,
                                        fontSize: 19,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        );
                      }

                      final payment = filtered[index - 1];
                      final isChitu =
                          payment.planType.toLowerCase().contains('chitu') ||
                          payment.planType.toLowerCase().contains('chittu');

                      return Card(
                        child: InkWell(
                          borderRadius: BorderRadius.circular(14),
                          onTap: () =>
                              _openReceiptDetailSheet(context, payment),
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  mainAxisAlignment:
                                      MainAxisAlignment.spaceBetween,
                                  children: [
                                    Expanded(
                                      child: Row(
                                        children: [
                                          Icon(
                                            isChitu
                                                ? Icons.savings_outlined
                                                : Icons.diamond_outlined,
                                            size: 18,
                                            color: AppColors.primary,
                                          ),
                                          const SizedBox(width: 8),
                                          Expanded(
                                            child: Text(
                                              payment.planName,
                                              style: const TextStyle(
                                                color: AppColors.dark,
                                                fontSize: 15,
                                                fontWeight: FontWeight.bold,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                    SJewlsStatusBadge.fromStatus(
                                      payment.status,
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 10),
                                SJewlsInfoRow(
                                  label: 'Plan Type',
                                  value: isChitu ? 'Chittu' : 'Jewellery',
                                ),
                                SJewlsInfoRow(
                                  label: 'Amount Paid',
                                  value: Formatters.currency(
                                    payment.amount,
                                    currencyCode: payment.currency,
                                  ),
                                  emphasizeValue: true,
                                  goldValue: true,
                                ),
                                if (payment.summaryDescription != null &&
                                    payment.summaryDescription!.isNotEmpty)
                                  SJewlsInfoRow(
                                    label: 'Allocation / Credit',
                                    value: payment.summaryDescription!,
                                    emphasizeValue: true,
                                  ),
                                if (payment.paymentMethod != null &&
                                    payment.paymentMethod!.isNotEmpty)
                                  SJewlsInfoRow(
                                    label: 'Payment Method',
                                    value: payment.paymentMethod!,
                                  ),
                                SJewlsInfoRow(
                                  label: 'Date & Time',
                                  value: Formatters.dateTime(
                                    payment.confirmedAtUtc ??
                                        payment.createdAtUtc,
                                  ),
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
            ),
          ),
        ],
      ),
    );
  }
}

class _PaymentReceiptBottomSheet extends ConsumerStatefulWidget {
  final CustomerPaymentSummaryDto summary;

  const _PaymentReceiptBottomSheet({required this.summary});

  @override
  ConsumerState<_PaymentReceiptBottomSheet> createState() =>
      _PaymentReceiptBottomSheetState();
}

class _PaymentReceiptBottomSheetState
    extends ConsumerState<_PaymentReceiptBottomSheet> {
  bool _loading = false;
  CustomerPaymentDetailDto? _detail;

  @override
  void initState() {
    super.initState();
    if (widget.summary.hasDetails && widget.summary.paymentId.isNotEmpty) {
      _loadDetail();
    }
  }

  Future<void> _loadDetail() async {
    setState(() => _loading = true);
    try {
      final repo = ref.read(chituRepositoryProvider);
      final detail = await repo.getUnifiedPaymentDetail(
        widget.summary.paymentId,
      );
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = _detail ?? widget.summary;
    final isChitu =
        s.planType.toLowerCase().contains('chitu') ||
        s.planType.toLowerCase().contains('chittu');

    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Expanded(
                child: Text(
                  'Transaction Receipt',
                  style: TextStyle(
                    color: AppColors.dark,
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              IconButton(
                onPressed: () => Navigator.of(context).pop(),
                icon: const Icon(Icons.close),
              ),
            ],
          ),
          const Divider(height: 20),
          SJewlsInfoRow(label: 'Plan', value: s.planName, emphasizeValue: true),
          SJewlsInfoRow(
            label: 'Amount',
            value: Formatters.currency(s.amount, currencyCode: s.currency),
            emphasizeValue: true,
            goldValue: true,
          ),
          SJewlsInfoRow(label: 'Status', value: s.status),
          if (s.paymentMethod != null && s.paymentMethod!.isNotEmpty)
            SJewlsInfoRow(label: 'Method', value: s.paymentMethod!),
          if (s.providerReference != null && s.providerReference!.isNotEmpty)
            SJewlsInfoRow(label: 'Reference', value: s.providerReference!),
          SJewlsInfoRow(
            label: 'Timestamp',
            value: Formatters.dateTime(s.confirmedAtUtc ?? s.createdAtUtc),
          ),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Center(
                child: CircularProgressIndicator(color: AppColors.primary),
              ),
            )
          else if (_detail?.chituDetails != null) ...[
            const Divider(height: 20),
            const Text(
              'Chittu Slot Allocations',
              style: TextStyle(
                color: AppColors.dark,
                fontWeight: FontWeight.bold,
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 8),
            ..._detail!.chituDetails!.slotAllocations.map(
              (alloc) => SJewlsInfoRow(
                label:
                    '${alloc.slotDisplayCode} (Rd: ${alloc.instalmentRoundsCovered.join(', ')})',
                value: Formatters.currency(
                  alloc.allocatedAmount,
                  currencyCode: s.currency,
                ),
                emphasizeValue: true,
              ),
            ),
          ] else if (_detail?.jewelleryDetails != null) ...[
            const Divider(height: 20),
            SJewlsInfoRow(
              label: 'Gold Credited',
              value:
                  '+${Formatters.grams(_detail!.jewelleryDetails!.creditedGrams)}',
              emphasizeValue: true,
            ),
            SJewlsInfoRow(
              label: 'Applied Rate / g',
              value: Formatters.currency(
                _detail!.jewelleryDetails!.appliedGoldRatePerGram,
                currencyCode: s.currency,
              ),
            ),
          ],
          const SizedBox(height: 18),
          if (s.planId != null && s.planId!.isNotEmpty)
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: AppColors.white,
              ),
              onPressed: () {
                Navigator.of(context).pop();
                if (isChitu) {
                  context.push('/chittu/investments/${s.planId}');
                } else {
                  context.push('/jewelry/enrolments/${s.planId}');
                }
              },
              child: Text(
                isChitu ? 'Open Chittu Plan' : 'Open Jewellery Enrolment',
              ),
            ),
        ],
      ),
    );
  }
}
