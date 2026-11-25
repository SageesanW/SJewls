import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/chitu_models.dart';
import 'chitu_provider.dart';

class ChituPaymentScreen extends ConsumerStatefulWidget {
  final String planId;
  final String? initialSlotId;

  const ChituPaymentScreen({
    super.key,
    required this.planId,
    this.initialSlotId,
  });

  @override
  ConsumerState<ChituPaymentScreen> createState() => _ChituPaymentScreenState();
}

class _ChituPaymentScreenState extends ConsumerState<ChituPaymentScreen> {
  final Map<String, int> _selectedSlotInstalments = {};
  bool _initializedSelections = false;
  bool _isLoadingPreview = false;
  bool _isSubmitting = false;
  String? _errorMessage;
  CustomerChituPaymentPreviewResponse? _preview;
  CustomerChituPaymentConfirmResponse? _confirmResult;

  void _initializeDefaultSelections(CustomerPayableSlotsDto payable) {
    if (_initializedSelections) return;
    _initializedSelections = true;
    final eligibleSlots = payable.slots
        .where((s) => s.isEligibleForPayment && s.remainingPayableMonths > 0)
        .toList();
    if (eligibleSlots.isEmpty) return;

    if (widget.initialSlotId != null && widget.initialSlotId!.isNotEmpty) {
      final matching = eligibleSlots
          .where((s) => s.slotId == widget.initialSlotId)
          .toList();
      if (matching.isNotEmpty) {
        final slot = matching.first;
        final defaultCount = slot.unpaidInstalmentRounds.isNotEmpty
            ? slot.unpaidInstalmentRounds.length.clamp(
                1,
                slot.remainingPayableMonths,
              )
            : 1;
        _selectedSlotInstalments[slot.slotId] = defaultCount;
        WidgetsBinding.instance.addPostFrameCallback((_) => _fetchPreview());
        return;
      }
    }

    for (final slot in eligibleSlots) {
      final defaultCount = slot.unpaidInstalmentRounds.isNotEmpty
          ? slot.unpaidInstalmentRounds.length.clamp(
              1,
              slot.remainingPayableMonths,
            )
          : 1;
      _selectedSlotInstalments[slot.slotId] = defaultCount;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) => _fetchPreview());
  }

  List<CustomerSlotPaymentSelectionDto> _buildSelections() {
    return _selectedSlotInstalments.entries
        .where((e) => e.value > 0)
        .map(
          (e) => CustomerSlotPaymentSelectionDto(
            slotId: e.key,
            instalmentsToPayCount: e.value,
          ),
        )
        .toList();
  }

  Future<void> _fetchPreview() async {
    final selections = _buildSelections();
    if (selections.isEmpty) {
      setState(() {
        _preview = null;
        _errorMessage = null;
      });
      return;
    }

    setState(() {
      _isLoadingPreview = true;
      _errorMessage = null;
    });

    try {
      final repo = ref.read(chituRepositoryProvider);
      final preview = await repo.previewSlotPayment(
        planId: widget.planId,
        slotSelections: selections,
      );
      if (!mounted) return;
      setState(() {
        _preview = preview;
        _isLoadingPreview = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoadingPreview = false;
        _errorMessage = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  Future<void> _submitPayment() async {
    final selections = _buildSelections();
    if (selections.isEmpty || _isSubmitting) return;

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final repo = ref.read(chituRepositoryProvider);
      final result = await repo.confirmSlotPayment(
        planId: widget.planId,
        slotSelections: selections,
        planName: _preview?.planName,
        previewReference: _preview?.previewReference,
        expectedTotalAmount: _preview?.totalPayableAmount,
      );
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _confirmResult = result;
      });
      ref.invalidate(chituPayableSlotsProvider(widget.planId));
      ref.invalidate(chituInvestmentPlanDetailProvider(widget.planId));
      ref.invalidate(myChituInvestmentsProvider);
      ref.invalidate(unifiedCustomerPaymentsProvider);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_confirmResult != null) {
      return _buildResultScreen(context, _confirmResult!);
    }

    final payableAsync = ref.watch(chituPayableSlotsProvider(widget.planId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Pay Chittu Instalment')),
      body: payableAsync.when(
        loading: () => const SJewlsLoadingView(
          message: 'Loading your payable slots and instalments...',
        ),
        error: (err, _) => SJewlsErrorView(
          title: 'Unable to Load Payable Slots',
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () =>
              ref.invalidate(chituPayableSlotsProvider(widget.planId)),
        ),
        data: (payable) {
          _initializeDefaultSelections(payable);
          final eligibleSlots = payable.slots
              .where(
                (s) => s.isEligibleForPayment && s.remainingPayableMonths > 0,
              )
              .toList();

          if (eligibleSlots.isEmpty) {
            return SJewlsEmptyView(
              icon: Icons.check_circle_outline_rounded,
              title: 'No Payable Instalments Available',
              subtitle:
                  'All slots in ${payable.planName} are either fully paid or not currently eligible for additional payments.',
              actionLabel: 'Back',
              onAction: () => Navigator.of(context).pop(),
            );
          }

          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.all(18),
                  children: [
                    // Plan Header Banner
                    Container(
                      padding: const EdgeInsets.all(18),
                      decoration: BoxDecoration(
                        color: AppColors.primary,
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            payable.planName,
                            style: const TextStyle(
                              color: AppColors.white,
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Current Round: ${payable.currentRoundIndex} • Monthly / Slot: ${Formatters.currency(payable.monthlySlotPrice, currencyCode: payable.currency)}',
                            style: TextStyle(
                              color: AppColors.white.withValues(alpha: 0.85),
                              fontSize: 13,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 18),

                    const Text(
                      'Select Slots & Instalments to Pay',
                      style: TextStyle(
                        color: AppColors.dark,
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 10),

                    ...payable.slots.map((slot) {
                      final isSelected = _selectedSlotInstalments.containsKey(
                        slot.slotId,
                      );
                      final selectedMonths =
                          _selectedSlotInstalments[slot.slotId] ?? 1;
                      final canSelect =
                          slot.isEligibleForPayment &&
                          slot.remainingPayableMonths > 0;

                      return Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: Card(
                          child: Padding(
                            padding: const EdgeInsets.all(14),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Checkbox(
                                      value: isSelected,
                                      activeColor: AppColors.primary,
                                      onChanged: canSelect
                                          ? (checked) {
                                              setState(() {
                                                if (checked == true) {
                                                  _selectedSlotInstalments[slot
                                                          .slotId] =
                                                      slot
                                                          .unpaidInstalmentRounds
                                                          .isNotEmpty
                                                      ? slot
                                                            .unpaidInstalmentRounds
                                                            .length
                                                            .clamp(
                                                              1,
                                                              slot.remainingPayableMonths,
                                                            )
                                                      : 1;
                                                } else {
                                                  _selectedSlotInstalments
                                                      .remove(slot.slotId);
                                                }
                                              });
                                              _fetchPreview();
                                            }
                                          : null,
                                    ),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                            slot.slotDisplayCode,
                                            style: const TextStyle(
                                              color: AppColors.dark,
                                              fontSize: 15,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                          Text(
                                            slot.outstandingDueAmount > 0
                                                ? 'Due now: ${Formatters.currency(slot.outstandingDueAmount, currencyCode: payable.currency)}'
                                                : 'Up to date (${slot.remainingPayableMonths} months remaining)',
                                            style: TextStyle(
                                              color:
                                                  slot.outstandingDueAmount > 0
                                                  ? Colors.orange.shade900
                                                  : AppColors.dark.withValues(
                                                      alpha: 0.7,
                                                    ),
                                              fontSize: 12.5,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                    SJewlsStatusBadge.fromStatus(slot.status),
                                  ],
                                ),
                                if (!canSelect &&
                                    slot.ineligibilityReason != null &&
                                    slot.ineligibilityReason!.isNotEmpty)
                                  Padding(
                                    padding: const EdgeInsets.only(
                                      left: 12,
                                      top: 4,
                                    ),
                                    child: Text(
                                      slot.ineligibilityReason!,
                                      style: TextStyle(
                                        color: Colors.red.shade700,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ),
                                if (isSelected && canSelect) ...[
                                  const Divider(height: 18),
                                  Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      const Expanded(
                                        child: Text(
                                          'Instalments to Pay:',
                                          style: TextStyle(
                                            color: AppColors.dark,
                                            fontSize: 13.5,
                                            fontWeight: FontWeight.w600,
                                          ),
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                      Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          IconButton(
                                            onPressed:
                                                selectedMonths > 1 &&
                                                    !_isLoadingPreview
                                                ? () {
                                                    setState(() {
                                                      _selectedSlotInstalments[slot
                                                              .slotId] =
                                                          selectedMonths - 1;
                                                    });
                                                    _fetchPreview();
                                                  }
                                                : null,
                                            icon: const Icon(
                                              Icons.remove_circle_outline,
                                            ),
                                          ),
                                          Text(
                                            '$selectedMonths mo',
                                            style: const TextStyle(
                                              color: AppColors.dark,
                                              fontWeight: FontWeight.bold,
                                              fontSize: 15,
                                            ),
                                          ),
                                          IconButton(
                                            onPressed:
                                                selectedMonths <
                                                        slot.remainingPayableMonths &&
                                                    !_isLoadingPreview
                                                ? () {
                                                    setState(() {
                                                      _selectedSlotInstalments[slot
                                                              .slotId] =
                                                          selectedMonths + 1;
                                                    });
                                                    _fetchPreview();
                                                  }
                                                : null,
                                            icon: const Icon(
                                              Icons.add_circle_outline,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                ],
                              ],
                            ),
                          ),
                        ),
                      );
                    }),

                    // Allocation Preview Breakdown
                    if (_isLoadingPreview)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 20),
                        child: Center(
                          child: CircularProgressIndicator(
                            color: AppColors.primary,
                          ),
                        ),
                      )
                    else if (_preview != null) ...[
                      const SizedBox(height: 8),
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Payment Allocation Preview',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 12),
                              ..._preview!.slots.map((item) {
                                return Padding(
                                  padding: const EdgeInsets.only(bottom: 10),
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      SJewlsInfoRow(
                                        label:
                                            '${item.slotDisplayCode} (${item.instalmentsToPayCount} mo)',
                                        value: Formatters.currency(
                                          item.amountAllocated,
                                          currencyCode: _preview!.currency,
                                        ),
                                        emphasizeValue: true,
                                      ),
                                      if (item.clearedRounds.isNotEmpty)
                                        Text(
                                          'Covers Round(s): ${item.clearedRounds.join(', ')} • ${item.resultingRaffleEligible ? 'Draw Eligible' : 'Arrears Remain'}',
                                          style: TextStyle(
                                            color: AppColors.dark.withValues(
                                              alpha: 0.68,
                                            ),
                                            fontSize: 12,
                                          ),
                                        ),
                                    ],
                                  ),
                                );
                              }),
                              const Divider(height: 20),
                              SJewlsInfoRow(
                                label: 'Total Amount Payable',
                                value: Formatters.currency(
                                  _preview!.totalPayableAmount,
                                  currencyCode: _preview!.currency,
                                ),
                                emphasizeValue: true,
                                goldValue: true,
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],

                    if (_errorMessage != null) ...[
                      const SizedBox(height: 12),
                      Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.red.shade50,
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(color: Colors.red.shade200),
                        ),
                        child: Text(
                          _errorMessage!,
                          style: TextStyle(
                            color: Colors.red.shade800,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
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
                    onPressed:
                        (_selectedSlotInstalments.isEmpty ||
                            _isLoadingPreview ||
                            _isSubmitting ||
                            _preview == null)
                        ? null
                        : _submitPayment,
                    icon: _isSubmitting
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.2,
                              color: AppColors.dark,
                            ),
                          )
                        : const Icon(Icons.lock_outline_rounded),
                    label: Text(
                      _preview != null
                          ? 'Confirm & Pay ${Formatters.currency(_preview!.totalPayableAmount, currencyCode: _preview!.currency)}'
                          : 'Select Slots to Continue',
                      style: const TextStyle(
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

  Widget _buildResultScreen(
    BuildContext context,
    CustomerChituPaymentConfirmResponse result,
  ) {
    final isDeferred = result.isGatewayDeferred;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(isDeferred ? 'In-Branch Payment Info' : 'Payment Receipt'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(22),
                child: Column(
                  children: [
                    Icon(
                      isDeferred
                          ? Icons.storefront_outlined
                          : Icons.check_circle_rounded,
                      size: 56,
                      color: isDeferred ? AppColors.gold : AppColors.primary,
                    ),
                    const SizedBox(height: 14),
                    Text(
                      isDeferred
                          ? 'Online Gateway Not Configured'
                          : 'Instalment Payment Confirmed',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: AppColors.dark,
                        fontSize: 19,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      result.message ??
                          (isDeferred
                              ? 'Online payment is not currently configured for this branch. Please visit your Saranga Jewels branch to pay your Chittu instalment in store. Your slot balances remain unchanged until confirmed.'
                              : 'Your Chittu instalment payment for ${result.planName} has been confirmed.'),
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.78),
                        fontSize: 13.5,
                        height: 1.45,
                      ),
                    ),
                    const Divider(height: 28),
                    SJewlsInfoRow(
                      label: 'Plan',
                      value: result.planName,
                      emphasizeValue: true,
                    ),
                    SJewlsInfoRow(
                      label: isDeferred ? 'Amount Due' : 'Amount Paid',
                      value: Formatters.currency(
                        result.totalAmount,
                        currencyCode: result.currency,
                      ),
                      emphasizeValue: true,
                      goldValue: true,
                    ),
                    if (!isDeferred && result.paymentId.isNotEmpty)
                      SJewlsInfoRow(
                        label: 'Payment ID',
                        value: result.paymentId,
                      ),
                    if (!isDeferred && result.paidAtUtc.isNotEmpty)
                      SJewlsInfoRow(
                        label: 'Paid At',
                        value: Formatters.dateTime(result.paidAtUtc),
                      ),
                    if (result.receipts.isNotEmpty) ...[
                      const Divider(height: 22),
                      ...result.receipts.map(
                        (r) => SJewlsInfoRow(
                          label:
                              '${r.slotDisplayCode} (${r.monthsCovered} mo${r.clearedMonthIndices.isNotEmpty ? ': Rd ${r.clearedMonthIndices.join(', ')}' : ''})',
                          value: Formatters.currency(
                            r.amountAllocated,
                            currencyCode: result.currency,
                          ),
                          emphasizeValue: true,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            const Spacer(),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: AppColors.white,
                padding: const EdgeInsets.symmetric(vertical: 15),
              ),
              onPressed: () => Navigator.of(context).pop(),
              child: const Text(
                'Return to My Plans',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
