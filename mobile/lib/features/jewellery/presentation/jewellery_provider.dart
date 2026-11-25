import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/presentation/auth_provider.dart';
import '../../profile/presentation/profile_provider.dart';
import '../data/jewellery_models.dart';
import '../data/jewellery_repository.dart';

final jewelleryRepositoryProvider = Provider<JewelleryRepository>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  return JewelleryRepository(apiClient);
});

final jewelleryCategoriesProvider =
    FutureProvider.autoDispose<List<CustomerJewelleryCategoryDto>>((ref) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      return repo.getCategories();
    });

final selectedJewelleryCategoryIdProvider = StateProvider.autoDispose<String?>(
  (ref) => null,
);

final jewelleryPlansProvider =
    FutureProvider.autoDispose<List<CustomerJewelleryPlanDto>>((ref) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      final selectedCategoryId = ref.watch(selectedJewelleryCategoryIdProvider);
      if (selectedCategoryId != null && selectedCategoryId.isNotEmpty) {
        return repo.getPlansByCategory(selectedCategoryId);
      }
      return repo.getPlans();
    });

final jewelleryPlanDetailProvider = FutureProvider.autoDispose
    .family<CustomerJewelleryPlanDto, String>((ref, planId) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      return repo.getPlanDetails(planId);
    });

final customerEnrolmentsProvider =
    FutureProvider.autoDispose<CustomerEnrolmentsPagedResponse>((ref) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      return repo.getEnrolments(page: 1, pageSize: 50);
    });

final enrolmentDetailProvider = FutureProvider.autoDispose
    .family<CustomerEnrolmentDto, String>((ref, enrolmentId) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      return repo.getEnrolmentDetails(enrolmentId);
    });

final enrolmentPaymentsProvider = FutureProvider.autoDispose
    .family<List<CustomerEnrolmentPaymentDto>, String>((
      ref,
      enrolmentId,
    ) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      return repo.getEnrolmentPayments(enrolmentId);
    });

final allCustomerPaymentsProvider =
    FutureProvider.autoDispose<List<CustomerEnrolmentPaymentDto>>((ref) async {
      final repo = ref.watch(jewelleryRepositoryProvider);
      final enrolmentsRes = await repo.getEnrolments(page: 1, pageSize: 50);
      if (enrolmentsRes.items.isEmpty) {
        return const [];
      }

      final paymentLists = await Future.wait(
        enrolmentsRes.items.map(
          (enrolment) => repo.getEnrolmentPayments(
            enrolment.enrolmentId,
            planName: enrolment.planName,
          ),
        ),
      );

      final allPayments = paymentLists.expand((list) => list).toList();
      allPayments.sort((a, b) {
        final dateA =
            DateTime.tryParse(a.confirmedAtUtc ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0);
        final dateB =
            DateTime.tryParse(b.confirmedAtUtc ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0);
        return dateB.compareTo(dateA);
      });
      return allPayments;
    });

final liveGoldRateProvider = FutureProvider.autoDispose<CustomerGoldRateDto>((
  ref,
) async {
  final repo = ref.watch(jewelleryRepositoryProvider);
  final profileAsync = ref.watch(customerProfileProvider);
  final enrolmentsAsync = ref.watch(customerEnrolmentsProvider);

  final branchId = profileAsync.maybeWhen(
    data: (p) => p.primaryBranchId,
    orElse: () => null,
  );

  final enrolments = enrolmentsAsync.maybeWhen(
    data: (res) => res.items,
    orElse: () => const <CustomerEnrolmentDto>[],
  );

  String? activeEnrolmentId;
  String currency = 'LKR';
  for (final item in enrolments) {
    if (item.currency.isNotEmpty) {
      currency = item.currency;
    }
    if (item.isContributionAllowed && item.enrolmentId.isNotEmpty) {
      activeEnrolmentId = item.enrolmentId;
      break;
    }
  }
  activeEnrolmentId ??= enrolments.isNotEmpty
      ? enrolments.first.enrolmentId
      : null;

  return repo.getLiveGoldRate(
    activeEnrolmentId: activeEnrolmentId,
    branchId: branchId,
    currency: currency,
  );
});
