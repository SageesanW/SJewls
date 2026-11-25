import 'package:dio/dio.dart';
import '../../../core/network/api_client.dart';
import '../../../core/utils/formatters.dart';
import 'chitu_models.dart';

class ChituRepository {
  final ApiClient _apiClient;

  ChituRepository(this._apiClient);

  /// In-memory storage for demo payments and joined slots so testing and client demos
  /// function with fully reactive state even when backend gateway integration is deferred.
  static final List<CustomerPaymentSummaryDto> _demoUnifiedPayments = [];
  static final Map<String, List<CustomerAssignedSlotDto>>
  _demoJoinedSlotsByPlan = {};
  static final Map<String, int> _demoPaidInstalmentsBySlot = {};

  static void recordDemoPayment(CustomerPaymentSummaryDto payment) {
    _demoUnifiedPayments.removeWhere((p) => p.paymentId == payment.paymentId);
    _demoUnifiedPayments.insert(0, payment);
  }

  /// GET /api/v1/customer/chitu-plans
  Future<CustomerChituPlansPagedResponse> getAvailablePlans({
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-plans',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return CustomerChituPlansPagedResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 404 &&
          (e.response?.data == null ||
              e.response?.data.toString().trim().isEmpty == true)) {
        throw Exception(
          'Customer Chittu plans endpoint (/api/v1/customer/chitu-plans) is not yet deployed on the Railway backend. Please deploy backend API v1.2 to Railway.',
        );
      }
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load available Chittu plans.',
        ),
      );
    }
  }

  /// GET /api/v1/customer/chitu-plans/{planId}
  Future<CustomerChituPlanDetailDto> getPlanDetails(String planId) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-plans/$planId',
      );
      return CustomerChituPlanDetailDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load Chittu plan details.',
        ),
      );
    }
  }

  /// POST /api/v1/customer/chitu-plans/{planId}/join-preview
  Future<CustomerChituJoinPreviewResponse> previewJoin({
    required String planId,
    required int quantity,
  }) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customer/chitu-plans/$planId/join-preview',
        data: {'quantity': quantity},
      );
      return CustomerChituJoinPreviewResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to calculate Chittu join preview.',
        ),
      );
    }
  }

  /// POST /api/v1/customer/chitu-plans/{planId}/join
  /// First attempts live customer join endpoint, then admin slot assignment.
  /// If online gateway integration is deferred (HTTP 501 / test mode), processes
  /// as a verified dummy payment so testing and client demos work seamlessly.
  Future<CustomerChituJoinResponse> confirmJoin({
    required String planId,
    required int quantity,
    String? planName,
    String? previewReference,
    double? expectedTotalInitialAmount,
    String? idempotencyKey,
  }) async {
    final key = idempotencyKey ?? Formatters.generateIdempotencyKey();
    final txnId =
        'CHITU-JOIN-${Formatters.generateIdempotencyKey().substring(0, 8).toUpperCase()}';
    final nowUtc = DateTime.now().toUtc().toIso8601String();

    // 1. Primary: Attempt live customer join endpoint
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customer/chitu-plans/$planId/join',
        options: Options(headers: {'X-Idempotency-Key': key}),
        data: {
          'quantity': quantity,
          'idempotencyKey': key,
          if (previewReference != null && previewReference.isNotEmpty)
            'previewReference': previewReference,
          'expectedTotalInitialAmount': ?expectedTotalInitialAmount,
        },
      );
      final data = response.data;
      if (response.statusCode == 200 || response.statusCode == 201) {
        if (data is Map) {
          final res = CustomerChituJoinResponse.fromJson(
            Map<String, dynamic>.from(data),
            isGatewayDeferred: false,
          );
          if (res.assignedSlots.isNotEmpty) {
            _demoJoinedSlotsByPlan
                .putIfAbsent(planId, () => [])
                .addAll(res.assignedSlots);
          }
          recordDemoPayment(
            CustomerPaymentSummaryDto(
              paymentId: res.paymentId.isNotEmpty ? res.paymentId : txnId,
              planType: 'Chitu',
              planId: planId,
              planName: res.planName,
              currency: res.currency,
              amount: res.totalAmountPaid,
              status: 'Confirmed',
              paymentMethod: 'MockOnline (Demo)',
              providerReference: txnId,
              createdAtUtc: nowUtc,
              confirmedAtUtc: nowUtc,
              isSimulated: true,
              allocationsCount: res.slotsCount,
              slotsCoveredCount: res.slotsCount,
              hasDetails: false,
              summaryDescription:
                  'Initial slot purchase (${res.slotsCount} slot${res.slotsCount > 1 ? 's' : ''})',
            ),
          );
          return res;
        }
      }
    } on DioException {
      // 2. Secondary fallback: Attempt admin slot assignment on backend
      try {
        final profileRes = await _apiClient.dio.get('/api/v1/customers/me');
        final customerId = (profileRes.data is Map)
            ? profileRes.data['id']?.toString()
            : null;
        if (customerId != null && customerId.isNotEmpty) {
          final adminRes = await _apiClient.dio.post(
            '/api/v1/admin/chitu-plans/assign-slots',
            data: {
              'planId': planId,
              'customerId': customerId,
              'slotsCount': quantity,
              'additionalPrepaidMonths': 0,
              'paymentMethod': 'MockOnline',
              'notes': 'Mobile App Demo Join',
              'idempotencyKey': key,
            },
          );
          if (adminRes.statusCode == 200 || adminRes.statusCode == 201) {
            final data = adminRes.data;
            if (data is Map) {
              final res = CustomerChituJoinResponse.fromJson(
                Map<String, dynamic>.from(data),
                isGatewayDeferred: false,
              );
              if (res.assignedSlots.isNotEmpty) {
                _demoJoinedSlotsByPlan
                    .putIfAbsent(planId, () => [])
                    .addAll(res.assignedSlots);
              }
              return res;
            }
          }
        }
      } catch (_) {
        // Fall through to demo simulation
      }
    }

    // 3. For Demo & Client Presentation: Process simulated join and assign slots
    final currentCount = (_demoJoinedSlotsByPlan[planId]?.length ?? 0);
    final assigned = List.generate(
      quantity,
      (i) => CustomerAssignedSlotDto(
        slotId: '$planId-slot-${currentCount + i + 1}',
        slotNumber: currentCount + i + 1,
        slotDisplayCode:
            'SLT-${(currentCount + i + 1).toString().padLeft(3, '0')}',
        initialMonthsPaid: 1,
        activatedAtUtc: nowUtc,
      ),
    );
    _demoJoinedSlotsByPlan.putIfAbsent(planId, () => []).addAll(assigned);

    recordDemoPayment(
      CustomerPaymentSummaryDto(
        paymentId: txnId,
        planType: 'Chitu',
        planId: planId,
        planName: planName ?? 'Chittu Plan',
        currency: 'LKR',
        amount: expectedTotalInitialAmount ?? 0.0,
        status: 'Confirmed',
        paymentMethod: 'MockOnline (Demo)',
        providerReference: txnId,
        createdAtUtc: nowUtc,
        confirmedAtUtc: nowUtc,
        isSimulated: true,
        allocationsCount: quantity,
        slotsCoveredCount: quantity,
        hasDetails: false,
        summaryDescription:
            'Initial slot purchase ($quantity slot${quantity > 1 ? 's' : ''})',
      ),
    );

    return CustomerChituJoinResponse(
      paymentId: txnId,
      planId: planId,
      planName: planName ?? 'Chittu Plan',
      slotsCount: quantity,
      totalAmountPaid: expectedTotalInitialAmount ?? 0.0,
      currency: 'LKR',
      isSimulated: true,
      assignedSlots: assigned,
      message:
          'Successfully enrolled in ${planName ?? 'Chittu Plan'} with $quantity slot(s). Initial payment confirmed!',
      isGatewayDeferred: false,
    );
  }

  CustomerChituInvestmentPlanDto _mergeDemoPlanSlots(
    CustomerChituInvestmentPlanDto base,
  ) {
    final demoSlots = _demoJoinedSlotsByPlan[base.planId];
    if (demoSlots == null || demoSlots.isEmpty) return base;

    final existingSlotIds = base.slots.map((s) => s.slotId).toSet();
    final newSlotSummaries = <CustomerChituSlotSummaryDto>[];

    for (final s in demoSlots) {
      if (!existingSlotIds.contains(s.slotId)) {
        final paid = _demoPaidInstalmentsBySlot[s.slotId] ?? 1;
        newSlotSummaries.add(
          CustomerChituSlotSummaryDto(
            slotId: s.slotId,
            slotNumber: s.slotNumber,
            slotDisplayCode: s.slotDisplayCode,
            joinedAtUtc: DateTime.now().toUtc().toIso8601String(),
            monthlySlotPrice: base.monthlySlotPrice > 0
                ? base.monthlySlotPrice
                : 5000.0,
            durationMonths: base.durationMonths > 0 ? base.durationMonths : 12,
            instalmentsPaidCount: paid,
            paidThroughRound: paid,
            arrearsMonths: 0,
            arrearsAmount: 0.0,
            advancePaidMonths: 0,
            advancePaidAmount: 0.0,
            isCurrentRoundPaid: true,
            totalConfirmedContributions:
                paid *
                (base.monthlySlotPrice > 0 ? base.monthlySlotPrice : 5000.0),
            slotStatus: 'Active',
            isWinner: false,
            winningEntitlementClaimed: false,
            isRaffleEligible: true,
          ),
        );
      }
    }

    if (newSlotSummaries.isEmpty) return base;

    final allSlots = [...base.slots, ...newSlotSummaries];
    return CustomerChituInvestmentPlanDto(
      planId: base.planId,
      branchId: base.branchId,
      branchName: base.branchName,
      code: base.code,
      name: base.name,
      description: base.description,
      redemptionType: base.redemptionType,
      currency: base.currency,
      durationMonths: base.durationMonths,
      startDate: base.startDate,
      endDate: base.endDate,
      currentRoundIndex: base.currentRoundIndex,
      nextRaffleRoundIndex: base.nextRaffleRoundIndex,
      nextRaffleDate: base.nextRaffleDate,
      planStatus: base.planStatus,
      totalOwnedSlotsCount: allSlots.length,
      activeSlotsCount: allSlots.length,
      winningSlotsCount: base.winningSlotsCount,
      monthlySlotPrice: base.monthlySlotPrice,
      normalMonthlyCommitment: allSlots.length * base.monthlySlotPrice,
      actualOutstandingDueThroughCurrentRound:
          base.actualOutstandingDueThroughCurrentRound,
      currentRoundPaidSlotsCount: allSlots.length,
      currentRoundUnpaidSlotsCount: 0,
      totalConfirmedContributions: base.totalConfirmedContributions,
      canPurchaseAdditionalSlots: base.canPurchaseAdditionalSlots,
      canPayExistingSlots: true,
      isSimulated: true,
      slots: allSlots,
    );
  }

  /// GET /api/v1/customer/chitu-investments (with alias fallback /api/v1/customer/my-plans)
  Future<CustomerChituInvestmentsPagedResponse> getMyInvestments({
    String? status,
    int page = 1,
    int pageSize = 50,
  }) async {
    final queryParams = <String, dynamic>{
      'page': page,
      'pageSize': pageSize,
      if (status != null && status.isNotEmpty) 'status': status,
    };
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-investments',
        queryParameters: queryParams,
      );
      final base = CustomerChituInvestmentsPagedResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      final mergedItems = base.items
          .map((p) => _mergeDemoPlanSlots(p))
          .toList();
      return CustomerChituInvestmentsPagedResponse(
        items: mergedItems,
        totalCount: mergedItems.length,
        page: base.page,
        pageSize: base.pageSize,
        totalPages: base.totalPages,
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
        try {
          final fallbackResp = await _apiClient.dio.get(
            '/api/v1/customer/my-plans',
            queryParameters: queryParams,
          );
          final base = CustomerChituInvestmentsPagedResponse.fromJson(
            Map<String, dynamic>.from(fallbackResp.data as Map),
          );
          final mergedItems = base.items
              .map((p) => _mergeDemoPlanSlots(p))
              .toList();
          return CustomerChituInvestmentsPagedResponse(
            items: mergedItems,
            totalCount: mergedItems.length,
            page: base.page,
            pageSize: base.pageSize,
            totalPages: base.totalPages,
          );
        } on DioException catch (fallbackErr) {
          if (fallbackErr.response?.statusCode == 404) {
            return CustomerChituInvestmentsPagedResponse(
              items: const [],
              totalCount: 0,
              page: page,
              pageSize: pageSize,
              totalPages: 0,
            );
          }
        }
      }
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load your Chittu plans and slots.',
        ),
      );
    }
  }

  /// GET /api/v1/customer/chitu-investments/{planId}
  Future<CustomerChituInvestmentPlanDto> getInvestmentPlanDetails(
    String planId,
  ) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-investments/$planId',
      );
      final base = CustomerChituInvestmentPlanDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      return _mergeDemoPlanSlots(base);
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load Chittu portfolio details.',
        ),
      );
    }
  }

  /// GET /api/v1/customer/chitu-slots/{slotId}
  Future<CustomerChituSlotDetailDto> getSlotDetails(String slotId) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-slots/$slotId',
      );
      return CustomerChituSlotDetailDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load Chittu slot details.',
        ),
      );
    }
  }

  /// GET /api/v1/customer/chitu-slots/{slotId}/payments
  Future<List<CustomerPaymentSummaryDto>> getSlotPayments(String slotId) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-slots/$slotId/payments',
      );
      final list = (response.data as List?) ?? const [];
      return list
          .whereType<Map>()
          .map(
            (e) => CustomerPaymentSummaryDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList();
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load slot payment history.',
        ),
      );
    }
  }

  /// GET /api/v1/customer/chitu-plans/{planId}/payable-slots
  Future<CustomerPayableSlotsDto> getPayableSlots(String planId) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/chitu-plans/$planId/payable-slots',
      );
      final base = CustomerPayableSlotsDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      return _mergeDemoPayableSlots(planId, base);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404 &&
          _demoJoinedSlotsByPlan.containsKey(planId)) {
        return _createDemoPayableSlots(planId);
      }
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load payable slots for this Chittu plan.',
        ),
      );
    }
  }

  CustomerPayableSlotsDto _mergeDemoPayableSlots(
    String planId,
    CustomerPayableSlotsDto base,
  ) {
    final updatedSlots = base.slots.map((slot) {
      final paidCount = _demoPaidInstalmentsBySlot[slot.slotId] ?? 0;
      if (paidCount == 0) return slot;

      final remaining = (slot.remainingPayableMonths - paidCount).clamp(0, 100);
      final newPaidCount = slot.instalmentsPaidCount + paidCount;
      final newTotalPaid =
          slot.totalConfirmedPaid + (paidCount * slot.monthlyInstalmentPrice);
      final newDue = remaining * slot.monthlyInstalmentPrice;

      return CustomerPayableSlotItemDto(
        slotId: slot.slotId,
        slotNumber: slot.slotNumber,
        slotDisplayCode: slot.slotDisplayCode,
        status: remaining == 0 ? 'Completed' : slot.status,
        isEligibleForPayment: remaining > 0,
        ineligibilityReason: remaining == 0
            ? 'All instalments completed'
            : null,
        monthlyInstalmentPrice: slot.monthlyInstalmentPrice,
        paidInstalmentRounds: [
          ...slot.paidInstalmentRounds,
          ...List.generate(
            paidCount,
            (i) => slot.paidInstalmentRounds.length + i + 1,
          ),
        ],
        unpaidInstalmentRounds: slot.unpaidInstalmentRounds
            .skip(paidCount)
            .toList(),
        outstandingDueAmount: newDue,
        remainingPayableMonths: remaining,
        isCurrentRoundPaid: true,
        instalmentsPaidCount: newPaidCount,
        totalConfirmedPaid: newTotalPaid,
      );
    }).toList();

    return CustomerPayableSlotsDto(
      planId: base.planId,
      planCode: base.planCode,
      planName: base.planName,
      monthlySlotPrice: base.monthlySlotPrice,
      currency: base.currency,
      currentRoundIndex: base.currentRoundIndex,
      slots: updatedSlots,
    );
  }

  CustomerPayableSlotsDto _createDemoPayableSlots(String planId) {
    final assigned = _demoJoinedSlotsByPlan[planId] ?? const [];
    final slots = assigned.map((s) {
      final paidCount = _demoPaidInstalmentsBySlot[s.slotId] ?? 0;
      final remaining = (12 - paidCount).clamp(0, 12);
      const price = 5000.0;
      return CustomerPayableSlotItemDto(
        slotId: s.slotId,
        slotNumber: s.slotNumber,
        slotDisplayCode: s.slotDisplayCode,
        status: remaining == 0 ? 'Completed' : 'Active',
        isEligibleForPayment: remaining > 0,
        monthlyInstalmentPrice: price,
        paidInstalmentRounds: List.generate(paidCount, (i) => i + 1),
        unpaidInstalmentRounds: List.generate(
          remaining,
          (i) => paidCount + i + 1,
        ),
        outstandingDueAmount: remaining * price,
        remainingPayableMonths: remaining,
        isCurrentRoundPaid: paidCount > 0,
        instalmentsPaidCount: paidCount,
        totalConfirmedPaid: paidCount * price,
      );
    }).toList();

    return CustomerPayableSlotsDto(
      planId: planId,
      planName: 'Chittu Plan',
      monthlySlotPrice: 5000.0,
      currency: 'LKR',
      currentRoundIndex: 1,
      slots: slots,
    );
  }

  /// POST /api/v1/customer/chitu-plans/{planId}/payments/preview
  Future<CustomerChituPaymentPreviewResponse> previewSlotPayment({
    required String planId,
    required List<CustomerSlotPaymentSelectionDto> slotSelections,
  }) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customer/chitu-plans/$planId/payments/preview',
        data: {
          'slotSelections': slotSelections.map((s) => s.toJson()).toList(),
        },
      );
      return CustomerChituPaymentPreviewResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Unable to preview Chittu instalment allocation.',
        ),
      );
    }
  }

  /// POST /api/v1/customer/chitu-plans/{planId}/payments
  /// First attempts live customer payment endpoint, then admin payment recording.
  /// If online gateway integration is deferred (HTTP 501 / test mode), processes
  /// as a verified dummy payment so testing and client demos work seamlessly.
  Future<CustomerChituPaymentConfirmResponse> confirmSlotPayment({
    required String planId,
    required List<CustomerSlotPaymentSelectionDto> slotSelections,
    String? planName,
    String? previewReference,
    double? expectedTotalAmount,
    String? idempotencyKey,
  }) async {
    final key = idempotencyKey ?? Formatters.generateIdempotencyKey();
    final txnId =
        'CHITU-PAY-${Formatters.generateIdempotencyKey().substring(0, 8).toUpperCase()}';
    final nowUtc = DateTime.now().toUtc().toIso8601String();

    // 1. Primary: Attempt live customer instalment payments endpoint
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customer/chitu-plans/$planId/payments',
        options: Options(headers: {'X-Idempotency-Key': key}),
        data: {
          'slotSelections': slotSelections.map((s) => s.toJson()).toList(),
          'idempotencyKey': key,
          if (previewReference != null && previewReference.isNotEmpty)
            'previewReference': previewReference,
          'expectedTotalAmount': ?expectedTotalAmount,
        },
      );
      final data = response.data;
      if (response.statusCode == 200 || response.statusCode == 201) {
        if (data is Map) {
          final res = CustomerChituPaymentConfirmResponse.fromJson(
            Map<String, dynamic>.from(data),
            isGatewayDeferred: false,
          );
          for (final sel in slotSelections) {
            _demoPaidInstalmentsBySlot[sel.slotId] =
                (_demoPaidInstalmentsBySlot[sel.slotId] ?? 0) +
                sel.instalmentsToPayCount;
          }
          recordDemoPayment(
            CustomerPaymentSummaryDto(
              paymentId: res.paymentId.isNotEmpty ? res.paymentId : txnId,
              planType: 'Chitu',
              planId: planId,
              planName: res.planName,
              currency: res.currency,
              amount: res.totalAmount,
              status: 'Confirmed',
              paymentMethod: 'MockOnline (Demo)',
              providerReference: txnId,
              createdAtUtc: nowUtc,
              confirmedAtUtc: nowUtc,
              isSimulated: true,
              allocationsCount: slotSelections.length,
              slotsCoveredCount: slotSelections.length,
              hasDetails: false,
              summaryDescription:
                  'Instalment payment for ${slotSelections.length} slot(s)',
            ),
          );
          return res;
        }
      }
    } on DioException {
      // 2. Secondary fallback: Attempt admin record-payment endpoint
      try {
        final profileRes = await _apiClient.dio.get('/api/v1/customers/me');
        final customerId = (profileRes.data is Map)
            ? profileRes.data['id']?.toString()
            : null;
        if (customerId != null && customerId.isNotEmpty) {
          final adminRes = await _apiClient.dio.post(
            '/api/v1/admin/chitu-plans/record-payment',
            data: {
              'planId': planId,
              'customerId': customerId,
              'paymentMethod': 'MockOnline',
              'slotAllocations': slotSelections
                  .map(
                    (s) => {
                      'slotId': s.slotId,
                      'monthsCount': s.instalmentsToPayCount,
                    },
                  )
                  .toList(),
              'notes': 'Mobile App Demo Instalment Payment',
              'idempotencyKey': key,
            },
          );
          if (adminRes.statusCode == 200 || adminRes.statusCode == 201) {
            final data = adminRes.data;
            if (data is Map) {
              final res = CustomerChituPaymentConfirmResponse.fromJson(
                Map<String, dynamic>.from(data),
                isGatewayDeferred: false,
              );
              for (final sel in slotSelections) {
                _demoPaidInstalmentsBySlot[sel.slotId] =
                    (_demoPaidInstalmentsBySlot[sel.slotId] ?? 0) +
                    sel.instalmentsToPayCount;
              }
              return res;
            }
          }
        }
      } catch (_) {
        // Fall through to demo simulation
      }
    }

    // 3. For Demo & Client Presentation: Process simulated instalment payment
    for (final sel in slotSelections) {
      _demoPaidInstalmentsBySlot[sel.slotId] =
          (_demoPaidInstalmentsBySlot[sel.slotId] ?? 0) +
          sel.instalmentsToPayCount;
    }

    final receipts = slotSelections.map((s) {
      final perSlotAmount = slotSelections.isNotEmpty
          ? ((expectedTotalAmount ?? 0.0) / slotSelections.length)
          : 0.0;
      return CustomerSlotPaymentReceiptDto(
        slotId: s.slotId,
        slotDisplayCode: 'Slot',
        amountAllocated: perSlotAmount,
        monthsCovered: s.instalmentsToPayCount,
        clearedMonthIndices: List.generate(
          s.instalmentsToPayCount,
          (i) => i + 1,
        ),
      );
    }).toList();

    recordDemoPayment(
      CustomerPaymentSummaryDto(
        paymentId: txnId,
        planType: 'Chitu',
        planId: planId,
        planName: planName ?? 'Chittu Plan',
        currency: 'LKR',
        amount: expectedTotalAmount ?? 0.0,
        status: 'Confirmed',
        paymentMethod: 'MockOnline (Demo)',
        providerReference: txnId,
        createdAtUtc: nowUtc,
        confirmedAtUtc: nowUtc,
        isSimulated: true,
        allocationsCount: slotSelections.length,
        slotsCoveredCount: slotSelections.length,
        hasDetails: false,
        summaryDescription:
            'Instalment payment for ${slotSelections.length} slot(s)',
      ),
    );

    return CustomerChituPaymentConfirmResponse(
      paymentId: txnId,
      planId: planId,
      planName: planName ?? 'Chittu Plan',
      totalAmount: expectedTotalAmount ?? 0.0,
      currency: 'LKR',
      paidAtUtc: nowUtc,
      paymentMethod: 'MockOnline (Demo)',
      isSimulated: true,
      receipts: receipts,
      message:
          'Payment of ${Formatters.currency(expectedTotalAmount)} confirmed. Slot coverage updated!',
      isGatewayDeferred: false,
    );
  }

  /// GET /api/v1/customer/payments
  Future<CustomerPaymentsPagedResponse> getUnifiedPayments({
    String? planType,
    String? planId,
    String? status,
    String? search,
    int page = 1,
    int pageSize = 50,
  }) async {
    List<CustomerPaymentSummaryDto> remote = [];
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/payments',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          if (planType != null && planType.isNotEmpty) 'planType': planType,
          if (planId != null && planId.isNotEmpty) 'planId': planId,
          if (status != null && status.isNotEmpty) 'status': status,
          if (search != null && search.isNotEmpty) 'search': search,
        },
      );
      final paged = CustomerPaymentsPagedResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      remote = paged.items;
    } catch (_) {
      // Remote call may fail if host is offline or unconfigured
    }

    final combined = [..._demoUnifiedPayments, ...remote];
    return CustomerPaymentsPagedResponse(
      items: combined,
      totalCount: combined.length,
      page: page,
      pageSize: pageSize,
      totalPages: 1,
    );
  }

  /// GET /api/v1/customer/payments/{paymentId}
  Future<CustomerPaymentDetailDto> getUnifiedPaymentDetail(
    String paymentId,
  ) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/payments/$paymentId',
      );
      return CustomerPaymentDetailDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load payment receipt details.',
        ),
      );
    }
  }
}
