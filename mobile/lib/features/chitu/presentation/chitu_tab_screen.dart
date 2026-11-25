import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/chitu_models.dart';
import 'chitu_provider.dart';

class ChituTabScreen extends ConsumerStatefulWidget {
  final int initialTabIndex;

  const ChituTabScreen({super.key, this.initialTabIndex = 0});

  @override
  ConsumerState<ChituTabScreen> createState() => _ChituTabScreenState();
}

class _ChituTabScreenState extends ConsumerState<ChituTabScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final TextEditingController _searchController = TextEditingController();
  String _searchQuery = '';

  @override
  void initState() {
    super.initState();
    _tabController = TabController(
      length: 2,
      vsync: this,
      initialIndex: widget.initialTabIndex.clamp(0, 1),
    );
  }

  @override
  void dispose() {
    _tabController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _openPaymentFlow(
    BuildContext context,
    String planId, {
    String? slotId,
  }) async {
    final uri = slotId != null && slotId.isNotEmpty
        ? '/chittu/investments/$planId/pay?slotId=$slotId'
        : '/chittu/investments/$planId/pay';
    await context.push(uri);
    if (!mounted) return;
    ref.invalidate(myChituInvestmentsProvider);
    ref.invalidate(availableChituPlansProvider);
    ref.invalidate(unifiedCustomerPaymentsProvider);
  }

  @override
  Widget build(BuildContext context) {
    final myInvestmentsAsync = ref.watch(myChituInvestmentsProvider);
    final ownedCount = myInvestmentsAsync.maybeWhen(
      data: (res) => res.items.length,
      orElse: () => 0,
    );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Chittu Savings Plans'),
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
        bottom: TabBar(
          controller: _tabController,
          indicatorColor: AppColors.gold,
          indicatorWeight: 3,
          labelColor: AppColors.gold,
          unselectedLabelColor: AppColors.white.withValues(alpha: 0.72),
          labelStyle: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 14,
          ),
          tabs: [
            const Tab(text: 'Available Plans'),
            Tab(
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Text('My Plans & Slots'),
                  if (ownedCount > 0) ...[
                    const SizedBox(width: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 7,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.gold,
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        '$ownedCount',
                        style: const TextStyle(
                          color: AppColors.dark,
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [_buildAvailablePlansTab(), _buildMyInvestmentsTab()],
      ),
    );
  }

  Widget _buildAvailablePlansTab() {
    final plansAsync = ref.watch(availableChituPlansProvider);

    return Column(
      children: [
        Container(
          color: AppColors.white,
          padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
          child: TextField(
            controller: _searchController,
            onChanged: (value) {
              setState(() => _searchQuery = value.trim().toLowerCase());
            },
            decoration: InputDecoration(
              hintText: 'Search Chittu plans by name or code...',
              prefixIcon: const Icon(Icons.search, color: AppColors.primary),
              suffixIcon: _searchQuery.isNotEmpty
                  ? IconButton(
                      icon: const Icon(Icons.clear, size: 18),
                      onPressed: () {
                        _searchController.clear();
                        setState(() => _searchQuery = '');
                      },
                    )
                  : null,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: 14,
                vertical: 12,
              ),
            ),
          ),
        ),
        Expanded(
          child: plansAsync.when(
            loading: () => const SJewlsLoadingView(
              message: 'Loading available Chittu plans...',
            ),
            error: (err, _) => SJewlsErrorView(
              title: 'Unable to Load Chittu Plans',
              message: err.toString().replaceFirst('Exception: ', ''),
              onRetry: () => ref.invalidate(availableChituPlansProvider),
            ),
            data: (paged) {
              final filtered = paged.items.where((plan) {
                if (_searchQuery.isEmpty) return true;
                final nameMatch = plan.name.toLowerCase().contains(
                  _searchQuery,
                );
                final codeMatch = (plan.code ?? '').toLowerCase().contains(
                  _searchQuery,
                );
                final descMatch = (plan.description ?? '')
                    .toLowerCase()
                    .contains(_searchQuery);
                return nameMatch || codeMatch || descMatch;
              }).toList();

              if (filtered.isEmpty) {
                return SJewlsEmptyView(
                  icon: Icons.savings_outlined,
                  title: paged.items.isEmpty
                      ? 'No Active Chittu Plans Available'
                      : 'No Matching Chittu Plans',
                  subtitle: paged.items.isEmpty
                      ? 'There are currently no Chittu plans open for joining in your branch.'
                      : 'Try clearing your search filter to see all available Chittu plans.',
                  actionLabel: _searchQuery.isNotEmpty ? 'Clear Search' : null,
                  onAction: _searchQuery.isNotEmpty
                      ? () {
                          _searchController.clear();
                          setState(() => _searchQuery = '');
                        }
                      : null,
                );
              }

              return RefreshIndicator(
                color: AppColors.primary,
                onRefresh: () async {
                  ref.invalidate(availableChituPlansProvider);
                  await ref.read(availableChituPlansProvider.future);
                },
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: filtered.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 14),
                  itemBuilder: (context, index) {
                    final plan = filtered[index];
                    return _buildAvailablePlanCard(context, plan);
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildAvailablePlanCard(
    BuildContext context,
    CustomerChituPlanSummaryDto plan,
  ) {
    final capacityRatio = plan.totalSlotCapacity > 0
        ? (plan.issuedSlotsCount / plan.totalSlotCapacity).clamp(0.0, 1.0)
        : (plan.capacityPercentage / 100).clamp(0.0, 1.0);

    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => context.push('/chittu/plans/${plan.planId}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(
                      Icons.savings_outlined,
                      color: AppColors.primary,
                      size: 24,
                    ),
                  ),
                  const SizedBox(width: 12),
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
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        Text(
                          plan.name,
                          style: const TextStyle(
                            color: AppColors.dark,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  ),
                  SJewlsStatusBadge(
                    label: plan.isAvailableForJoining ? 'Open' : 'Closed',
                    backgroundColor: plan.isAvailableForJoining
                        ? const Color(0xFFE6F4EA)
                        : const Color(0xFFECEFF1),
                    textColor: plan.isAvailableForJoining
                        ? const Color(0xFF137333)
                        : const Color(0xFF455A64),
                  ),
                ],
              ),
              if (plan.description != null && plan.description!.isNotEmpty) ...[
                const SizedBox(height: 10),
                Text(
                  plan.description!,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: AppColors.dark.withValues(alpha: 0.72),
                    fontSize: 13,
                    height: 1.4,
                  ),
                ),
              ],
              const SizedBox(height: 14),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.04),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Monthly / Slot',
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.65),
                              fontSize: 11.5,
                            ),
                          ),
                          const SizedBox(height: 3),
                          Text(
                            Formatters.currency(
                              plan.monthlySlotPrice,
                              currencyCode: plan.currency,
                            ),
                            style: const TextStyle(
                              color: AppColors.primary,
                              fontSize: 14.5,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Container(
                      width: 1,
                      height: 30,
                      color: AppColors.dark.withValues(alpha: 0.12),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Benefit Value (${plan.redemptionType})',
                            style: TextStyle(
                              color: AppColors.dark.withValues(alpha: 0.65),
                              fontSize: 11.5,
                            ),
                          ),
                          const SizedBox(height: 3),
                          Text(
                            Formatters.currency(
                              plan.winningEntitlementPerSlot,
                              currencyCode: plan.currency,
                            ),
                            style: const TextStyle(
                              color: AppColors.dark,
                              fontSize: 14.5,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Flexible(
                    child: Text(
                      'Duration: ${plan.durationMonths} Months (Round ${plan.currentRoundIndex})',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.dark,
                        fontSize: 12.5,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Flexible(
                    child: Text(
                      '${plan.availableSlotsCount} / ${plan.totalSlotCapacity} slots free',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.primary,
                        fontSize: 12.5,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              ClipRRect(
                borderRadius: BorderRadius.circular(6),
                child: LinearProgressIndicator(
                  value: capacityRatio,
                  minHeight: 7,
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
                  if (plan.nextRaffleDate != null &&
                      plan.nextRaffleDate!.isNotEmpty)
                    Flexible(
                      child: Text(
                        'Next Draw: ${Formatters.date(plan.nextRaffleDate)}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.7),
                          fontSize: 12,
                        ),
                      ),
                    )
                  else
                    Flexible(
                      child: Text(
                        'Starts: ${Formatters.date(plan.startDate)}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.7),
                          fontSize: 12,
                        ),
                      ),
                    ),
                  if (plan.customerOwnedSlotsCount > 0)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.gold.withValues(alpha: 0.2),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        'Owned: ${plan.customerOwnedSlotsCount} slot(s)',
                        style: const TextStyle(
                          color: AppColors.dark,
                          fontSize: 11.5,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    )
                  else
                    const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'View Plan & Terms',
                          style: TextStyle(
                            color: AppColors.primary,
                            fontSize: 12.5,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        SizedBox(width: 4),
                        Icon(
                          Icons.arrow_forward_ios_rounded,
                          size: 12,
                          color: AppColors.primary,
                        ),
                      ],
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildMyInvestmentsTab() {
    final myInvestmentsAsync = ref.watch(myChituInvestmentsProvider);

    return myInvestmentsAsync.when(
      loading: () => const SJewlsLoadingView(
        message: 'Loading your Chittu plans and slots...',
      ),
      error: (err, _) => SJewlsErrorView(
        title: 'Unable to Load Your Chittu Plans',
        message: err.toString().replaceFirst('Exception: ', ''),
        onRetry: () => ref.invalidate(myChituInvestmentsProvider),
      ),
      data: (paged) {
        if (paged.items.isEmpty) {
          return SJewlsEmptyView(
            icon: Icons.savings_outlined,
            title: 'No Enrolled Chittu Plans Yet',
            subtitle:
                'Join an active Chittu savings plan to hold monthly slots and track your instalments here.',
            actionLabel: 'Browse Available Plans',
            onAction: () => _tabController.animateTo(0),
          );
        }

        return RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async {
            ref.invalidate(myChituInvestmentsProvider);
            await ref.read(myChituInvestmentsProvider.future);
          },
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: paged.items.length,
            separatorBuilder: (_, _) => const SizedBox(height: 14),
            itemBuilder: (context, index) {
              final plan = paged.items[index];
              return _buildInvestmentPlanCard(context, plan);
            },
          ),
        );
      },
    );
  }

  Widget _buildInvestmentPlanCard(
    BuildContext context,
    CustomerChituInvestmentPlanDto plan,
  ) {
    final hasArrears = plan.actualOutstandingDueThroughCurrentRound > 0;

    return Card(
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
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  ),
                  SJewlsStatusBadge.fromStatus(plan.planStatus),
                ],
              ),
              const SizedBox(height: 12),
              SJewlsInfoRow(
                label: 'Owned Slots',
                value:
                    '${plan.totalOwnedSlotsCount} Total (${plan.activeSlotsCount} Active${plan.winningSlotsCount > 0 ? ', ${plan.winningSlotsCount} Won' : ''})',
                emphasizeValue: true,
              ),
              SJewlsInfoRow(
                label: 'Current Round',
                value:
                    'Round ${plan.currentRoundIndex} of ${plan.durationMonths}',
              ),
              if (plan.nextRaffleDate != null &&
                  plan.nextRaffleDate!.isNotEmpty)
                SJewlsInfoRow(
                  label: 'Next Draw / Due Date',
                  value: Formatters.date(plan.nextRaffleDate),
                ),
              SJewlsInfoRow(
                label: hasArrears
                    ? 'Outstanding Due Now'
                    : 'Monthly Commitment',
                value: Formatters.currency(
                  hasArrears
                      ? plan.actualOutstandingDueThroughCurrentRound
                      : plan.normalMonthlyCommitment,
                  currencyCode: plan.currency,
                ),
                emphasizeValue: true,
                goldValue: hasArrears,
              ),
              SJewlsInfoRow(
                label: 'Total Confirmed Paid',
                value: Formatters.currency(
                  plan.totalConfirmedContributions,
                  currencyCode: plan.currency,
                ),
              ),
              if (plan.slots.isNotEmpty) ...[
                const Divider(height: 20),
                const Text(
                  'Your Slots (Tap a slot to view schedule)',
                  style: TextStyle(
                    color: AppColors.dark,
                    fontSize: 12.5,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: plan.slots.map((slot) {
                    return ActionChip(
                      backgroundColor: AppColors.primary.withValues(
                        alpha: 0.07,
                      ),
                      side: BorderSide(
                        color: slot.arrearsAmount > 0
                            ? Colors.orange.shade700
                            : AppColors.primary.withValues(alpha: 0.25),
                      ),
                      label: Text(
                        '${slot.slotDisplayCode} (${slot.instalmentsPaidCount}/${slot.durationMonths})',
                        style: const TextStyle(
                          color: AppColors.dark,
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      onPressed: () =>
                          context.push('/chittu/slots/${slot.slotId}'),
                    );
                  }).toList(),
                ),
              ],
              if (plan.canPayExistingSlots) ...[
                const SizedBox(height: 14),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.gold,
                      foregroundColor: AppColors.dark,
                      padding: const EdgeInsets.symmetric(vertical: 12),
                    ),
                    onPressed: () => _openPaymentFlow(context, plan.planId),
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
    );
  }
}
