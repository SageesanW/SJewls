import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/chitu_models.dart';
import 'chitu_provider.dart';

class ChituPlanDetailScreen extends ConsumerWidget {
  final String planId;

  const ChituPlanDetailScreen({super.key, required this.planId});

  void _openJoinBottomSheet(
    BuildContext context,
    WidgetRef ref,
    CustomerChituPlanDetailDto plan,
  ) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.background,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (_) => _ChituJoinBottomSheet(
        plan: plan,
        onJoinConfirmed: (response) {
          ref.invalidate(chituPlanDetailProvider(planId));
          ref.invalidate(availableChituPlansProvider);
          ref.invalidate(myChituInvestmentsProvider);
          ref.invalidate(unifiedCustomerPaymentsProvider);
          if (context.mounted && !response.isGatewayDeferred) {
            context.push('/chittu/investments/${plan.planId}');
          }
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final planAsync = ref.watch(chituPlanDetailProvider(planId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Chittu Plan Details')),
      body: planAsync.when(
        loading: () => const SJewlsLoadingView(
          message: 'Loading Chittu plan details and schedule...',
        ),
        error: (err, _) => SJewlsErrorView(
          title: 'Unable to Load Plan Details',
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () => ref.invalidate(chituPlanDetailProvider(planId)),
        ),
        data: (plan) {
          final capacityRatio = plan.totalSlotCapacity > 0
              ? (plan.issuedSlotsCount / plan.totalSlotCapacity).clamp(0.0, 1.0)
              : (plan.capacityPercentage / 100).clamp(0.0, 1.0);
          final canJoin =
              plan.isAvailableForJoining && plan.availableSlotsCount > 0;

          return Column(
            children: [
              Expanded(
                child: RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () async {
                    ref.invalidate(chituPlanDetailProvider(planId));
                    await ref.read(chituPlanDetailProvider(planId).future);
                  },
                  child: ListView(
                    padding: const EdgeInsets.all(18),
                    children: [
                      // Hero Plan Summary Card
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
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 10,
                                      vertical: 4,
                                    ),
                                    decoration: BoxDecoration(
                                      color: AppColors.gold.withValues(
                                        alpha: 0.2,
                                      ),
                                      borderRadius: BorderRadius.circular(14),
                                    ),
                                    child: Text(
                                      plan.code!,
                                      style: const TextStyle(
                                        color: AppColors.gold,
                                        fontSize: 12,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                SJewlsStatusBadge(
                                  label: canJoin ? 'Open to Join' : 'Closed',
                                  backgroundColor: canJoin
                                      ? AppColors.gold
                                      : AppColors.white.withValues(alpha: 0.2),
                                  textColor: canJoin
                                      ? AppColors.dark
                                      : AppColors.white,
                                ),
                              ],
                            ),
                            const SizedBox(height: 12),
                            Text(
                              plan.name,
                              style: const TextStyle(
                                color: AppColors.white,
                                fontSize: 21,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            if (plan.description != null &&
                                plan.description!.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                plan.description!,
                                style: TextStyle(
                                  color: AppColors.white.withValues(
                                    alpha: 0.85,
                                  ),
                                  fontSize: 13.5,
                                  height: 1.45,
                                ),
                              ),
                            ],
                            const SizedBox(height: 18),
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
                                          'Monthly Instalment',
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
                                            plan.monthlySlotPrice,
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
                                          'Winning Benefit (${plan.redemptionType})',
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
                                            plan.winningEntitlementPerSlot,
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
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),

                      if (plan.customerOwnedSlotsCount > 0) ...[
                        Card(
                          color: AppColors.gold.withValues(alpha: 0.14),
                          child: ListTile(
                            leading: const Icon(
                              Icons.verified_rounded,
                              color: AppColors.primary,
                            ),
                            title: Text(
                              'You hold ${plan.customerOwnedSlotsCount} slot(s) in this plan',
                              style: const TextStyle(
                                color: AppColors.dark,
                                fontWeight: FontWeight.bold,
                                fontSize: 14,
                              ),
                            ),
                            subtitle: const Text(
                              'Tap to view your slot schedule or pay instalments.',
                              style: TextStyle(fontSize: 12.5),
                            ),
                            trailing: const Icon(
                              Icons.arrow_forward_ios_rounded,
                              size: 15,
                              color: AppColors.primary,
                            ),
                            onTap: () => context.push(
                              '/chittu/investments/${plan.planId}',
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                      ],

                      // Plan Specifications & Slot Capacity Card
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Plan Overview & Capacity',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 12),
                              SJewlsInfoRow(
                                label: 'Duration',
                                value: '${plan.durationMonths} Months',
                                emphasizeValue: true,
                              ),
                              SJewlsInfoRow(
                                label: 'Current Round',
                                value:
                                    'Round ${plan.currentRoundIndex} of ${plan.durationMonths}',
                              ),
                              SJewlsInfoRow(
                                label: 'Start Date',
                                value: Formatters.date(plan.startDate),
                              ),
                              SJewlsInfoRow(
                                label: 'End Date',
                                value: Formatters.date(plan.endDate),
                              ),
                              if (plan.nextRaffleDate != null &&
                                  plan.nextRaffleDate!.isNotEmpty)
                                SJewlsInfoRow(
                                  label: 'Next Scheduled Draw',
                                  value: Formatters.date(plan.nextRaffleDate),
                                  emphasizeValue: true,
                                  goldValue: true,
                                ),
                              if (plan.branchName != null &&
                                  plan.branchName!.isNotEmpty)
                                SJewlsInfoRow(
                                  label: 'Branch',
                                  value: plan.branchName!,
                                ),
                              const Divider(height: 22),
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Text(
                                    'Slots Issued: ${plan.issuedSlotsCount} / ${plan.totalSlotCapacity}',
                                    style: const TextStyle(
                                      color: AppColors.dark,
                                      fontSize: 13,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  Text(
                                    '${plan.availableSlotsCount} Available',
                                    style: const TextStyle(
                                      color: AppColors.primary,
                                      fontSize: 13,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 8),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(6),
                                child: LinearProgressIndicator(
                                  value: capacityRatio,
                                  minHeight: 8,
                                  backgroundColor: AppColors.primary.withValues(
                                    alpha: 0.1,
                                  ),
                                  valueColor:
                                      const AlwaysStoppedAnimation<Color>(
                                        AppColors.gold,
                                      ),
                                ),
                              ),
                              if (!canJoin &&
                                  plan.joiningAvailabilityReason != null &&
                                  plan
                                      .joiningAvailabilityReason!
                                      .isNotEmpty) ...[
                                const SizedBox(height: 10),
                                Text(
                                  plan.joiningAvailabilityReason!,
                                  style: TextStyle(
                                    color: Colors.red.shade800,
                                    fontSize: 12.5,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Monthly Round Schedule
                      if (plan.schedule.isNotEmpty) ...[
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Text(
                                  'Monthly Round & Draw Schedule',
                                  style: TextStyle(
                                    color: AppColors.dark,
                                    fontSize: 16,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                const SizedBox(height: 10),
                                ...plan.schedule.map((round) {
                                  return Padding(
                                    padding: const EdgeInsets.symmetric(
                                      vertical: 6,
                                    ),
                                    child: Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        Row(
                                          children: [
                                            Container(
                                              width: 28,
                                              height: 28,
                                              alignment: Alignment.center,
                                              decoration: BoxDecoration(
                                                color: round.hasRecordedDraw
                                                    ? AppColors.primary
                                                    : AppColors.primary
                                                          .withValues(
                                                            alpha: 0.1,
                                                          ),
                                                shape: BoxShape.circle,
                                              ),
                                              child: Text(
                                                '${round.roundIndex}',
                                                style: TextStyle(
                                                  color: round.hasRecordedDraw
                                                      ? AppColors.gold
                                                      : AppColors.primary,
                                                  fontSize: 12,
                                                  fontWeight: FontWeight.bold,
                                                ),
                                              ),
                                            ),
                                            const SizedBox(width: 10),
                                            Column(
                                              crossAxisAlignment:
                                                  CrossAxisAlignment.start,
                                              children: [
                                                Text(
                                                  'Round ${round.roundIndex}',
                                                  style: const TextStyle(
                                                    color: AppColors.dark,
                                                    fontWeight: FontWeight.w600,
                                                    fontSize: 13.5,
                                                  ),
                                                ),
                                                Text(
                                                  Formatters.date(
                                                    round.scheduledDate,
                                                  ),
                                                  style: TextStyle(
                                                    color: AppColors.dark
                                                        .withValues(alpha: 0.6),
                                                    fontSize: 12,
                                                  ),
                                                ),
                                              ],
                                            ),
                                          ],
                                        ),
                                        SJewlsStatusBadge.fromStatus(
                                          round.hasRecordedDraw
                                              ? 'Draw Recorded'
                                              : round.status,
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
                      ],

                      // Terms & Conditions
                      if (plan.terms.isNotEmpty)
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Text(
                                  'Plan Terms & Conditions',
                                  style: TextStyle(
                                    color: AppColors.dark,
                                    fontSize: 16,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                const SizedBox(height: 10),
                                ...plan.terms.map(
                                  (term) => Padding(
                                    padding: const EdgeInsets.only(bottom: 8),
                                    child: Row(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        const Text(
                                          '• ',
                                          style: TextStyle(
                                            color: AppColors.primary,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                        Expanded(
                                          child: Text(
                                            term,
                                            style: TextStyle(
                                              color: AppColors.dark.withValues(
                                                alpha: 0.8,
                                              ),
                                              fontSize: 13,
                                              height: 1.4,
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                    ],
                  ),
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
                      backgroundColor: canJoin
                          ? AppColors.gold
                          : AppColors.dark.withValues(alpha: 0.2),
                      foregroundColor: AppColors.dark,
                      padding: const EdgeInsets.symmetric(vertical: 15),
                    ),
                    onPressed: canJoin
                        ? () => _openJoinBottomSheet(context, ref, plan)
                        : null,
                    icon: const Icon(Icons.add_card_rounded),
                    label: Text(
                      canJoin
                          ? 'Join Plan / Purchase Slots'
                          : (plan.joiningAvailabilityReason ??
                                'Not Open for Joining'),
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
}

class _ChituJoinBottomSheet extends ConsumerStatefulWidget {
  final CustomerChituPlanDetailDto plan;
  final ValueChanged<CustomerChituJoinResponse> onJoinConfirmed;

  const _ChituJoinBottomSheet({
    required this.plan,
    required this.onJoinConfirmed,
  });

  @override
  ConsumerState<_ChituJoinBottomSheet> createState() =>
      _ChituJoinBottomSheetState();
}

class _ChituJoinBottomSheetState extends ConsumerState<_ChituJoinBottomSheet> {
  int _quantity = 1;
  bool _isLoadingPreview = false;
  bool _isSubmitting = false;
  String? _errorMessage;
  CustomerChituJoinPreviewResponse? _preview;
  CustomerChituJoinResponse? _joinResult;

  @override
  void initState() {
    super.initState();
    _loadPreview();
  }

  Future<void> _loadPreview() async {
    setState(() {
      _isLoadingPreview = true;
      _errorMessage = null;
    });
    try {
      final repo = ref.read(chituRepositoryProvider);
      final preview = await repo.previewJoin(
        planId: widget.plan.planId,
        quantity: _quantity,
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

  Future<void> _confirmJoin() async {
    if (_isSubmitting) return;
    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });
    try {
      final repo = ref.read(chituRepositoryProvider);
      final result = await repo.confirmJoin(
        planId: widget.plan.planId,
        quantity: _quantity,
        planName: widget.plan.name,
        previewReference: _preview?.previewReference,
        expectedTotalInitialAmount: _preview?.totalInitialAmountPayable,
      );
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _joinResult = result;
      });
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
    final maxSlots = widget.plan.availableSlotsCount.clamp(1, 20);

    if (_joinResult != null) {
      final res = _joinResult!;
      return Padding(
        padding: const EdgeInsets.fromLTRB(20, 24, 20, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Icon(
              res.isGatewayDeferred
                  ? Icons.storefront_outlined
                  : Icons.check_circle_rounded,
              color: res.isGatewayDeferred ? AppColors.gold : AppColors.primary,
              size: 52,
            ),
            const SizedBox(height: 12),
            Text(
              res.isGatewayDeferred
                  ? 'In-Branch Payment Required'
                  : 'Chittu Slots Activated',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.dark,
                fontSize: 19,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              res.message ??
                  (res.isGatewayDeferred
                      ? 'Online payment gateway is not configured yet. Please visit your Saranga Jewels branch to complete your initial slot payment.'
                      : 'Successfully enrolled in ${res.planName} with ${res.slotsCount} slot(s).'),
              textAlign: TextAlign.center,
              style: TextStyle(
                color: AppColors.dark.withValues(alpha: 0.78),
                fontSize: 13.5,
              ),
            ),
            if (res.assignedSlots.isNotEmpty) ...[
              const SizedBox(height: 16),
              Wrap(
                alignment: WrapAlignment.center,
                spacing: 8,
                runSpacing: 8,
                children: res.assignedSlots.map((s) {
                  return Chip(
                    backgroundColor: AppColors.primary.withValues(alpha: 0.1),
                    label: Text(
                      s.slotDisplayCode,
                      style: const TextStyle(
                        color: AppColors.primary,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  );
                }).toList(),
              ),
            ],
            const SizedBox(height: 20),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: AppColors.white,
              ),
              onPressed: () {
                Navigator.of(context).pop();
                widget.onJoinConfirmed(res);
              },
              child: Text(res.isGatewayDeferred ? 'Done' : 'View My Slots'),
            ),
          ],
        ),
      );
    }

    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + 24,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Expanded(
                child: Text(
                  'Join Chittu Plan',
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
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Expanded(
                child: Text(
                  'Select Slot Quantity',
                  style: TextStyle(
                    color: AppColors.dark,
                    fontWeight: FontWeight.w600,
                    fontSize: 14.5,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(
                    onPressed: _quantity > 1 && !_isLoadingPreview
                        ? () {
                            setState(() => _quantity--);
                            _loadPreview();
                          }
                        : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  Text(
                    '$_quantity',
                    style: const TextStyle(
                      color: AppColors.dark,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  IconButton(
                    onPressed: _quantity < maxSlots && !_isLoadingPreview
                        ? () {
                            setState(() => _quantity++);
                            _loadPreview();
                          }
                        : null,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              ),
            ],
          ),
          const Divider(height: 20),
          if (_isLoadingPreview)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Center(
                child: CircularProgressIndicator(color: AppColors.primary),
              ),
            )
          else if (_preview != null) ...[
            SJewlsInfoRow(
              label: 'Monthly Price per Slot',
              value: Formatters.currency(
                _preview!.monthlySlotPrice,
                currencyCode: _preview!.currency,
              ),
            ),
            SJewlsInfoRow(
              label: 'Initial Rounds Required per Slot',
              value: '${_preview!.requiredInitialInstalmentsPerSlot} Round(s)',
            ),
            SJewlsInfoRow(
              label: 'Initial Payable per Slot',
              value: Formatters.currency(
                _preview!.requiredInitialAmountPerSlot,
                currencyCode: _preview!.currency,
              ),
            ),
            SJewlsInfoRow(
              label: 'Total Initial Amount Payable',
              value: Formatters.currency(
                _preview!.totalInitialAmountPayable,
                currencyCode: _preview!.currency,
              ),
              emphasizeValue: true,
              goldValue: true,
            ),
          ],
          if (_errorMessage != null) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.red.shade50,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                _errorMessage!,
                style: TextStyle(color: Colors.red.shade800, fontSize: 13),
              ),
            ),
          ],
          const SizedBox(height: 18),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.gold,
              foregroundColor: AppColors.dark,
              padding: const EdgeInsets.symmetric(vertical: 15),
            ),
            onPressed: (_isLoadingPreview || _isSubmitting || _preview == null)
                ? null
                : _confirmJoin,
            child: _isSubmitting
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2.4,
                      color: AppColors.dark,
                    ),
                  )
                : Text(
                    _preview != null
                        ? 'Confirm & Pay ${Formatters.currency(_preview!.totalInitialAmountPayable, currencyCode: _preview!.currency)}'
                        : 'Confirm & Join',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
          ),
        ],
      ),
    );
  }
}
