import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/presentation/auth_provider.dart';
import '../../jewellery/presentation/jewellery_provider.dart';
import '../data/chitu_models.dart';
import '../data/chitu_repository.dart';

final chituRepositoryProvider = Provider<ChituRepository>((ref) {
  return ChituRepository(ref.watch(apiClientProvider));
});

/// Available Chittu plans for the customer's branch
final availableChituPlansProvider =
    FutureProvider<CustomerChituPlansPagedResponse>((ref) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getAvailablePlans(page: 1, pageSize: 50);
    });

/// Customer's enrolled Chittu plans and slots (My Plans)
final myChituInvestmentsProvider =
    FutureProvider<CustomerChituInvestmentsPagedResponse>((ref) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getMyInvestments(page: 1, pageSize: 50);
    });

/// Single Chittu plan details with schedule & terms
final chituPlanDetailProvider =
    FutureProvider.family<CustomerChituPlanDetailDto, String>((
      ref,
      planId,
    ) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getPlanDetails(planId);
    });

/// Customer's single Chittu plan portfolio breakdown
final chituInvestmentPlanDetailProvider =
    FutureProvider.family<CustomerChituInvestmentPlanDto, String>((
      ref,
      planId,
    ) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getInvestmentPlanDetails(planId);
    });

/// Detailed slot information including instalment schedule and winner claim
final chituSlotDetailProvider =
    FutureProvider.family<CustomerChituSlotDetailDto, String>((
      ref,
      slotId,
    ) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getSlotDetails(slotId);
    });

/// Payment history for a specific Chittu slot
final chituSlotPaymentsProvider =
    FutureProvider.family<List<CustomerPaymentSummaryDto>, String>((
      ref,
      slotId,
    ) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getSlotPayments(slotId);
    });

/// Payable slots for a specific Chittu plan
final chituPayableSlotsProvider =
    FutureProvider.family<CustomerPayableSlotsDto, String>((ref, planId) async {
      final repo = ref.watch(chituRepositoryProvider);
      return repo.getPayableSlots(planId);
    });

/// Unified payment history across Chittu and Jewellery plans, with fallback to
/// Jewellery enrolment payments if the unified endpoint is not yet deployed on the host.
final unifiedCustomerPaymentsProvider =
    FutureProvider<List<CustomerPaymentSummaryDto>>((ref) async {
      final chituRepo = ref.watch(chituRepositoryProvider);
      try {
        final paged = await chituRepo.getUnifiedPayments(page: 1, pageSize: 50);
        return paged.items;
      } catch (_) {
        // Fallback to aggregating Jewellery enrolment payments if unified endpoint returns 404
        final jewelleryPayments = await ref.watch(
          allCustomerPaymentsProvider.future,
        );
        return jewelleryPayments
            .map(
              (p) => CustomerPaymentSummaryDto(
                paymentId: p.paymentId.isNotEmpty
                    ? p.paymentId
                    : p.contributionId,
                planType: 'Jewellery',
                planId: p.enrolmentId,
                planName: p.planName ?? 'Jewellery Plan Contribution',
                currency: p.currency,
                amount: p.currencyAmount,
                status: p.status,
                paymentMethod: p.paymentMethod,
                providerReference: p.paymentReference,
                createdAtUtc: p.confirmedAtUtc ?? '',
                confirmedAtUtc: p.confirmedAtUtc,
                isSimulated: false,
                allocationsCount: 1,
                hasDetails: false,
                summaryDescription:
                    '+${p.goldGramsCredited.toStringAsFixed(4)} g @ ${p.currency} ${p.appliedRatePerGram.toStringAsFixed(2)}/g',
              ),
            )
            .toList();
      }
    });
