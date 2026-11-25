// Data models for Customer Chitu Plans, Slots, Instalments, and Unified Payments (API v1.2)

class CustomerChituPlansPagedResponse {
  final List<CustomerChituPlanSummaryDto> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;

  const CustomerChituPlansPagedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  factory CustomerChituPlansPagedResponse.fromJson(Map<String, dynamic> json) {
    final rawItems = (json['items'] as List?) ?? const [];
    return CustomerChituPlansPagedResponse(
      items: rawItems
          .whereType<Map>()
          .map(
            (e) => CustomerChituPlanSummaryDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      totalCount: (json['totalCount'] as num?)?.toInt() ?? rawItems.length,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    );
  }
}

class CustomerChituPlanSummaryDto {
  final String planId;
  final String branchId;
  final String? branchName;
  final String? code;
  final String name;
  final String? description;
  final String redemptionType;
  final String currency;
  final int durationMonths;
  final String startDate;
  final String endDate;
  final double monthlySlotPrice;
  final double winningEntitlementPerSlot;
  final int totalSlotCapacity;
  final int issuedSlotsCount;
  final int availableSlotsCount;
  final double capacityPercentage;
  final int currentRoundIndex;
  final int? nextRaffleRoundIndex;
  final String? nextRaffleDate;
  final int customerOwnedSlotsCount;
  final bool isAvailableForJoining;
  final String? joiningAvailabilityReason;

  const CustomerChituPlanSummaryDto({
    required this.planId,
    required this.branchId,
    this.branchName,
    this.code,
    required this.name,
    this.description,
    required this.redemptionType,
    required this.currency,
    required this.durationMonths,
    required this.startDate,
    required this.endDate,
    required this.monthlySlotPrice,
    required this.winningEntitlementPerSlot,
    required this.totalSlotCapacity,
    required this.issuedSlotsCount,
    required this.availableSlotsCount,
    required this.capacityPercentage,
    required this.currentRoundIndex,
    this.nextRaffleRoundIndex,
    this.nextRaffleDate,
    required this.customerOwnedSlotsCount,
    required this.isAvailableForJoining,
    this.joiningAvailabilityReason,
  });

  factory CustomerChituPlanSummaryDto.fromJson(Map<String, dynamic> json) {
    return CustomerChituPlanSummaryDto(
      planId: (json['planId'] ?? json['id'] ?? '').toString(),
      branchId: (json['branchId'] ?? '').toString(),
      branchName: json['branchName']?.toString(),
      code: json['code']?.toString(),
      name: (json['name'] ?? 'Chittu Plan').toString(),
      description: json['description']?.toString(),
      redemptionType: (json['redemptionType'] ?? 'Gold').toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      durationMonths: (json['durationMonths'] as num?)?.toInt() ?? 12,
      startDate: (json['startDate'] ?? '').toString(),
      endDate: (json['endDate'] ?? '').toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      winningEntitlementPerSlot:
          (json['winningEntitlementPerSlot'] as num?)?.toDouble() ?? 0.0,
      totalSlotCapacity: (json['totalSlotCapacity'] as num?)?.toInt() ?? 0,
      issuedSlotsCount: (json['issuedSlotsCount'] as num?)?.toInt() ?? 0,
      availableSlotsCount: (json['availableSlotsCount'] as num?)?.toInt() ?? 0,
      capacityPercentage:
          (json['capacityPercentage'] as num?)?.toDouble() ?? 0.0,
      currentRoundIndex: (json['currentRoundIndex'] as num?)?.toInt() ?? 1,
      nextRaffleRoundIndex: (json['nextRaffleRoundIndex'] as num?)?.toInt(),
      nextRaffleDate: json['nextRaffleDate']?.toString(),
      customerOwnedSlotsCount:
          (json['customerOwnedSlotsCount'] as num?)?.toInt() ?? 0,
      isAvailableForJoining: json['isAvailableForJoining'] as bool? ?? true,
      joiningAvailabilityReason: json['joiningAvailabilityReason']?.toString(),
    );
  }
}

class CustomerChituRoundScheduleDto {
  final int roundIndex;
  final String scheduledDate;
  final String status;
  final bool hasRecordedDraw;

  const CustomerChituRoundScheduleDto({
    required this.roundIndex,
    required this.scheduledDate,
    required this.status,
    required this.hasRecordedDraw,
  });

  factory CustomerChituRoundScheduleDto.fromJson(Map<String, dynamic> json) {
    return CustomerChituRoundScheduleDto(
      roundIndex: (json['roundIndex'] as num?)?.toInt() ?? 1,
      scheduledDate: (json['scheduledDate'] ?? '').toString(),
      status: (json['status'] ?? 'Scheduled').toString(),
      hasRecordedDraw: json['hasRecordedDraw'] as bool? ?? false,
    );
  }
}

class CustomerChituPlanDetailDto extends CustomerChituPlanSummaryDto {
  final List<CustomerChituRoundScheduleDto> schedule;
  final List<String> terms;

  const CustomerChituPlanDetailDto({
    required super.planId,
    required super.branchId,
    super.branchName,
    super.code,
    required super.name,
    super.description,
    required super.redemptionType,
    required super.currency,
    required super.durationMonths,
    required super.startDate,
    required super.endDate,
    required super.monthlySlotPrice,
    required super.winningEntitlementPerSlot,
    required super.totalSlotCapacity,
    required super.issuedSlotsCount,
    required super.availableSlotsCount,
    required super.capacityPercentage,
    required super.currentRoundIndex,
    super.nextRaffleRoundIndex,
    super.nextRaffleDate,
    required super.customerOwnedSlotsCount,
    required super.isAvailableForJoining,
    super.joiningAvailabilityReason,
    required this.schedule,
    required this.terms,
  });

  factory CustomerChituPlanDetailDto.fromJson(Map<String, dynamic> json) {
    final base = CustomerChituPlanSummaryDto.fromJson(json);
    final rawSchedule = (json['schedule'] as List?) ?? const [];
    final rawTerms = (json['terms'] as List?) ?? const [];
    return CustomerChituPlanDetailDto(
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
      monthlySlotPrice: base.monthlySlotPrice,
      winningEntitlementPerSlot: base.winningEntitlementPerSlot,
      totalSlotCapacity: base.totalSlotCapacity,
      issuedSlotsCount: base.issuedSlotsCount,
      availableSlotsCount: base.availableSlotsCount,
      capacityPercentage: base.capacityPercentage,
      currentRoundIndex: base.currentRoundIndex,
      nextRaffleRoundIndex: base.nextRaffleRoundIndex,
      nextRaffleDate: base.nextRaffleDate,
      customerOwnedSlotsCount: base.customerOwnedSlotsCount,
      isAvailableForJoining: base.isAvailableForJoining,
      joiningAvailabilityReason: base.joiningAvailabilityReason,
      schedule: rawSchedule
          .whereType<Map>()
          .map(
            (e) => CustomerChituRoundScheduleDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      terms: rawTerms
          .map((e) => e.toString())
          .where((e) => e.isNotEmpty)
          .toList(),
    );
  }
}

class CustomerChituJoinPreviewResponse {
  final String planId;
  final String? planCode;
  final String planName;
  final String currency;
  final double monthlySlotPrice;
  final int requestedQuantity;
  final double totalOrdinaryMonthlyPayment;
  final int requiredInitialInstalmentsPerSlot;
  final double requiredInitialAmountPerSlot;
  final double totalInitialAmountPayable;
  final int availableSlotsCount;
  final double winningEntitlementPerSlot;
  final String startDate;
  final String endDate;
  final int currentRoundIndex;
  final int? nextRaffleRoundIndex;
  final String? nextRaffleDate;
  final int existingOwnedSlotsCount;
  final String? previewReference;
  final String? previewCreatedAtUtc;
  final String? previewExpiresAtUtc;

  const CustomerChituJoinPreviewResponse({
    required this.planId,
    this.planCode,
    required this.planName,
    required this.currency,
    required this.monthlySlotPrice,
    required this.requestedQuantity,
    required this.totalOrdinaryMonthlyPayment,
    required this.requiredInitialInstalmentsPerSlot,
    required this.requiredInitialAmountPerSlot,
    required this.totalInitialAmountPayable,
    required this.availableSlotsCount,
    required this.winningEntitlementPerSlot,
    required this.startDate,
    required this.endDate,
    required this.currentRoundIndex,
    this.nextRaffleRoundIndex,
    this.nextRaffleDate,
    required this.existingOwnedSlotsCount,
    this.previewReference,
    this.previewCreatedAtUtc,
    this.previewExpiresAtUtc,
  });

  factory CustomerChituJoinPreviewResponse.fromJson(Map<String, dynamic> json) {
    return CustomerChituJoinPreviewResponse(
      planId: (json['planId'] ?? '').toString(),
      planCode: json['planCode']?.toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      requestedQuantity: (json['requestedQuantity'] as num?)?.toInt() ?? 1,
      totalOrdinaryMonthlyPayment:
          (json['totalOrdinaryMonthlyPayment'] as num?)?.toDouble() ?? 0.0,
      requiredInitialInstalmentsPerSlot:
          (json['requiredInitialInstalmentsPerSlot'] as num?)?.toInt() ?? 1,
      requiredInitialAmountPerSlot:
          (json['requiredInitialAmountPerSlot'] as num?)?.toDouble() ?? 0.0,
      totalInitialAmountPayable:
          (json['totalInitialAmountPayable'] as num?)?.toDouble() ?? 0.0,
      availableSlotsCount: (json['availableSlotsCount'] as num?)?.toInt() ?? 0,
      winningEntitlementPerSlot:
          (json['winningEntitlementPerSlot'] as num?)?.toDouble() ?? 0.0,
      startDate: (json['startDate'] ?? '').toString(),
      endDate: (json['endDate'] ?? '').toString(),
      currentRoundIndex: (json['currentRoundIndex'] as num?)?.toInt() ?? 1,
      nextRaffleRoundIndex: (json['nextRaffleRoundIndex'] as num?)?.toInt(),
      nextRaffleDate: json['nextRaffleDate']?.toString(),
      existingOwnedSlotsCount:
          (json['existingOwnedSlotsCount'] as num?)?.toInt() ?? 0,
      previewReference: json['previewReference']?.toString(),
      previewCreatedAtUtc: json['previewCreatedAtUtc']?.toString(),
      previewExpiresAtUtc: json['previewExpiresAtUtc']?.toString(),
    );
  }
}

class CustomerAssignedSlotDto {
  final String slotId;
  final int slotNumber;
  final String slotDisplayCode;
  final int initialMonthsPaid;
  final String activatedAtUtc;

  const CustomerAssignedSlotDto({
    required this.slotId,
    required this.slotNumber,
    required this.slotDisplayCode,
    required this.initialMonthsPaid,
    required this.activatedAtUtc,
  });

  factory CustomerAssignedSlotDto.fromJson(Map<String, dynamic> json) {
    final numVal = (json['slotNumber'] as num?)?.toInt() ?? 0;
    return CustomerAssignedSlotDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotNumber: numVal,
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot #$numVal').toString(),
      initialMonthsPaid: (json['initialMonthsPaid'] as num?)?.toInt() ?? 1,
      activatedAtUtc: (json['activatedAtUtc'] ?? '').toString(),
    );
  }
}

class CustomerChituJoinResponse {
  final String paymentId;
  final String planId;
  final String? planCode;
  final String planName;
  final int slotsCount;
  final double totalAmountPaid;
  final String currency;
  final bool isSimulated;
  final List<CustomerAssignedSlotDto> assignedSlots;
  final String? message;
  final bool isGatewayDeferred;

  const CustomerChituJoinResponse({
    required this.paymentId,
    required this.planId,
    this.planCode,
    required this.planName,
    required this.slotsCount,
    required this.totalAmountPaid,
    required this.currency,
    required this.isSimulated,
    required this.assignedSlots,
    this.message,
    this.isGatewayDeferred = false,
  });

  factory CustomerChituJoinResponse.fromJson(
    Map<String, dynamic> json, {
    bool isGatewayDeferred = false,
  }) {
    final rawSlots = (json['assignedSlots'] as List?) ?? const [];
    return CustomerChituJoinResponse(
      paymentId: (json['paymentId'] ?? '').toString(),
      planId: (json['planId'] ?? '').toString(),
      planCode: json['planCode']?.toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      slotsCount: (json['slotsCount'] as num?)?.toInt() ?? rawSlots.length,
      totalAmountPaid:
          (json['totalAmountPaid'] as num?)?.toDouble() ??
          (json['amountToPay'] as num?)?.toDouble() ??
          0.0,
      currency: (json['currency'] ?? 'LKR').toString(),
      isSimulated: json['isSimulated'] as bool? ?? false,
      assignedSlots: rawSlots
          .whereType<Map>()
          .map(
            (e) =>
                CustomerAssignedSlotDto.fromJson(Map<String, dynamic>.from(e)),
          )
          .toList(),
      message: (json['message'] ?? json['detail'] ?? json['title'])?.toString(),
      isGatewayDeferred: isGatewayDeferred,
    );
  }
}

class CustomerChituInvestmentsPagedResponse {
  final List<CustomerChituInvestmentPlanDto> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;

  const CustomerChituInvestmentsPagedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  factory CustomerChituInvestmentsPagedResponse.fromJson(
    Map<String, dynamic> json,
  ) {
    final rawItems = (json['items'] as List?) ?? const [];
    return CustomerChituInvestmentsPagedResponse(
      items: rawItems
          .whereType<Map>()
          .map(
            (e) => CustomerChituInvestmentPlanDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      totalCount: (json['totalCount'] as num?)?.toInt() ?? rawItems.length,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    );
  }
}

class CustomerChituInvestmentPlanDto {
  final String planId;
  final String branchId;
  final String? branchName;
  final String? code;
  final String name;
  final String? description;
  final String redemptionType;
  final String currency;
  final int durationMonths;
  final String startDate;
  final String endDate;
  final int currentRoundIndex;
  final int? nextRaffleRoundIndex;
  final String? nextRaffleDate;
  final String planStatus;
  final int totalOwnedSlotsCount;
  final int activeSlotsCount;
  final int winningSlotsCount;
  final double monthlySlotPrice;
  final double normalMonthlyCommitment;
  final double actualOutstandingDueThroughCurrentRound;
  final int currentRoundPaidSlotsCount;
  final int currentRoundUnpaidSlotsCount;
  final double totalConfirmedContributions;
  final bool canPurchaseAdditionalSlots;
  final bool canPayExistingSlots;
  final bool isSimulated;
  final List<CustomerChituSlotSummaryDto> slots;

  const CustomerChituInvestmentPlanDto({
    required this.planId,
    required this.branchId,
    this.branchName,
    this.code,
    required this.name,
    this.description,
    required this.redemptionType,
    required this.currency,
    required this.durationMonths,
    required this.startDate,
    required this.endDate,
    required this.currentRoundIndex,
    this.nextRaffleRoundIndex,
    this.nextRaffleDate,
    required this.planStatus,
    required this.totalOwnedSlotsCount,
    required this.activeSlotsCount,
    required this.winningSlotsCount,
    required this.monthlySlotPrice,
    required this.normalMonthlyCommitment,
    required this.actualOutstandingDueThroughCurrentRound,
    required this.currentRoundPaidSlotsCount,
    required this.currentRoundUnpaidSlotsCount,
    required this.totalConfirmedContributions,
    required this.canPurchaseAdditionalSlots,
    required this.canPayExistingSlots,
    required this.isSimulated,
    required this.slots,
  });

  factory CustomerChituInvestmentPlanDto.fromJson(Map<String, dynamic> json) {
    final rawSlots = (json['slots'] as List?) ?? const [];
    return CustomerChituInvestmentPlanDto(
      planId: (json['planId'] ?? '').toString(),
      branchId: (json['branchId'] ?? '').toString(),
      branchName: json['branchName']?.toString(),
      code: json['code']?.toString(),
      name: (json['name'] ?? 'Chittu Plan').toString(),
      description: json['description']?.toString(),
      redemptionType: (json['redemptionType'] ?? 'Gold').toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      durationMonths: (json['durationMonths'] as num?)?.toInt() ?? 12,
      startDate: (json['startDate'] ?? '').toString(),
      endDate: (json['endDate'] ?? '').toString(),
      currentRoundIndex: (json['currentRoundIndex'] as num?)?.toInt() ?? 1,
      nextRaffleRoundIndex: (json['nextRaffleRoundIndex'] as num?)?.toInt(),
      nextRaffleDate: json['nextRaffleDate']?.toString(),
      planStatus: (json['planStatus'] ?? 'Active').toString(),
      totalOwnedSlotsCount:
          (json['totalOwnedSlotsCount'] as num?)?.toInt() ?? rawSlots.length,
      activeSlotsCount: (json['activeSlotsCount'] as num?)?.toInt() ?? 0,
      winningSlotsCount: (json['winningSlotsCount'] as num?)?.toInt() ?? 0,
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      normalMonthlyCommitment:
          (json['normalMonthlyCommitment'] as num?)?.toDouble() ?? 0.0,
      actualOutstandingDueThroughCurrentRound:
          (json['actualOutstandingDueThroughCurrentRound'] as num?)
              ?.toDouble() ??
          0.0,
      currentRoundPaidSlotsCount:
          (json['currentRoundPaidSlotsCount'] as num?)?.toInt() ?? 0,
      currentRoundUnpaidSlotsCount:
          (json['currentRoundUnpaidSlotsCount'] as num?)?.toInt() ?? 0,
      totalConfirmedContributions:
          (json['totalConfirmedContributions'] as num?)?.toDouble() ?? 0.0,
      canPurchaseAdditionalSlots:
          json['canPurchaseAdditionalSlots'] as bool? ?? false,
      canPayExistingSlots: json['canPayExistingSlots'] as bool? ?? false,
      isSimulated: json['isSimulated'] as bool? ?? false,
      slots: rawSlots
          .whereType<Map>()
          .map(
            (e) => CustomerChituSlotSummaryDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
    );
  }
}

class CustomerChituSlotSummaryDto {
  final String slotId;
  final int slotNumber;
  final String slotDisplayCode;
  final String joinedAtUtc;
  final double monthlySlotPrice;
  final int durationMonths;
  final int instalmentsPaidCount;
  final int paidThroughRound;
  final int arrearsMonths;
  final double arrearsAmount;
  final int advancePaidMonths;
  final double advancePaidAmount;
  final bool isCurrentRoundPaid;
  final double totalConfirmedContributions;
  final String slotStatus;
  final bool isWinner;
  final int? winningRoundIndex;
  final String? winningDrawDate;
  final double? winningPrizeAmount;
  final bool winningEntitlementClaimed;
  final bool isRaffleEligible;
  final String? raffleEligibilityReason;

  const CustomerChituSlotSummaryDto({
    required this.slotId,
    required this.slotNumber,
    required this.slotDisplayCode,
    required this.joinedAtUtc,
    required this.monthlySlotPrice,
    required this.durationMonths,
    required this.instalmentsPaidCount,
    required this.paidThroughRound,
    required this.arrearsMonths,
    required this.arrearsAmount,
    required this.advancePaidMonths,
    required this.advancePaidAmount,
    required this.isCurrentRoundPaid,
    required this.totalConfirmedContributions,
    required this.slotStatus,
    required this.isWinner,
    this.winningRoundIndex,
    this.winningDrawDate,
    this.winningPrizeAmount,
    required this.winningEntitlementClaimed,
    required this.isRaffleEligible,
    this.raffleEligibilityReason,
  });

  bool get canPayInstalment {
    final s = slotStatus.toLowerCase();
    return s == 'active' && instalmentsPaidCount < durationMonths;
  }

  double get progressPercentage {
    if (durationMonths <= 0) return 0.0;
    return ((instalmentsPaidCount / durationMonths) * 100).clamp(0.0, 100.0);
  }

  factory CustomerChituSlotSummaryDto.fromJson(Map<String, dynamic> json) {
    final slotNum = (json['slotNumber'] as num?)?.toInt() ?? 0;
    return CustomerChituSlotSummaryDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotNumber: slotNum,
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot #$slotNum').toString(),
      joinedAtUtc: (json['joinedAtUtc'] ?? '').toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      durationMonths: (json['durationMonths'] as num?)?.toInt() ?? 12,
      instalmentsPaidCount:
          (json['instalmentsPaidCount'] as num?)?.toInt() ?? 0,
      paidThroughRound: (json['paidThroughRound'] as num?)?.toInt() ?? 0,
      arrearsMonths: (json['arrearsMonths'] as num?)?.toInt() ?? 0,
      arrearsAmount: (json['arrearsAmount'] as num?)?.toDouble() ?? 0.0,
      advancePaidMonths: (json['advancePaidMonths'] as num?)?.toInt() ?? 0,
      advancePaidAmount: (json['advancePaidAmount'] as num?)?.toDouble() ?? 0.0,
      isCurrentRoundPaid: json['isCurrentRoundPaid'] as bool? ?? false,
      totalConfirmedContributions:
          (json['totalConfirmedContributions'] as num?)?.toDouble() ?? 0.0,
      slotStatus: (json['slotStatus'] ?? 'Active').toString(),
      isWinner: json['isWinner'] as bool? ?? false,
      winningRoundIndex: (json['winningRoundIndex'] as num?)?.toInt(),
      winningDrawDate: json['winningDrawDate']?.toString(),
      winningPrizeAmount: (json['winningPrizeAmount'] as num?)?.toDouble(),
      winningEntitlementClaimed:
          json['winningEntitlementClaimed'] as bool? ?? false,
      isRaffleEligible: json['isRaffleEligible'] as bool? ?? false,
      raffleEligibilityReason: json['raffleEligibilityReason']?.toString(),
    );
  }
}

class CustomerChituSlotInstalmentDto {
  final String id;
  final int monthIndex;
  final String dueDate;
  final double amountDue;
  final String status;
  final String? paidAtUtc;
  final String? paymentId;

  const CustomerChituSlotInstalmentDto({
    required this.id,
    required this.monthIndex,
    required this.dueDate,
    required this.amountDue,
    required this.status,
    this.paidAtUtc,
    this.paymentId,
  });

  factory CustomerChituSlotInstalmentDto.fromJson(Map<String, dynamic> json) {
    return CustomerChituSlotInstalmentDto(
      id: (json['id'] ?? '').toString(),
      monthIndex: (json['monthIndex'] as num?)?.toInt() ?? 1,
      dueDate: (json['dueDate'] ?? '').toString(),
      amountDue: (json['amountDue'] as num?)?.toDouble() ?? 0.0,
      status: (json['status'] ?? 'Pending').toString(),
      paidAtUtc: json['paidAtUtc']?.toString(),
      paymentId: json['paymentId']?.toString(),
    );
  }
}

class CustomerWinnerBenefitDto {
  final String id;
  final String drawId;
  final String slotId;
  final String? slotDisplayCode;
  final double chituBenefitValue;
  final double advanceExcessAmount;
  final String redemptionType;
  final String status;
  final String? claimedAtUtc;
  final String? claimRemarks;

  const CustomerWinnerBenefitDto({
    required this.id,
    required this.drawId,
    required this.slotId,
    this.slotDisplayCode,
    required this.chituBenefitValue,
    required this.advanceExcessAmount,
    required this.redemptionType,
    required this.status,
    this.claimedAtUtc,
    this.claimRemarks,
  });

  factory CustomerWinnerBenefitDto.fromJson(Map<String, dynamic> json) {
    return CustomerWinnerBenefitDto(
      id: (json['id'] ?? '').toString(),
      drawId: (json['drawId'] ?? '').toString(),
      slotId: (json['slotId'] ?? '').toString(),
      slotDisplayCode: json['slotDisplayCode']?.toString(),
      chituBenefitValue: (json['chituBenefitValue'] as num?)?.toDouble() ?? 0.0,
      advanceExcessAmount:
          (json['advanceExcessAmount'] as num?)?.toDouble() ?? 0.0,
      redemptionType: (json['redemptionType'] ?? 'Gold').toString(),
      status: (json['status'] ?? 'Unclaimed').toString(),
      claimedAtUtc: json['claimedAtUtc']?.toString(),
      claimRemarks: json['claimRemarks']?.toString(),
    );
  }
}

class CustomerChituSlotDetailDto extends CustomerChituSlotSummaryDto {
  final String planId;
  final String planName;
  final String? planCode;
  final String currency;
  final List<CustomerChituSlotInstalmentDto> instalments;
  final CustomerWinnerBenefitDto? entitlementClaimInfo;

  const CustomerChituSlotDetailDto({
    required super.slotId,
    required super.slotNumber,
    required super.slotDisplayCode,
    required super.joinedAtUtc,
    required super.monthlySlotPrice,
    required super.durationMonths,
    required super.instalmentsPaidCount,
    required super.paidThroughRound,
    required super.arrearsMonths,
    required super.arrearsAmount,
    required super.advancePaidMonths,
    required super.advancePaidAmount,
    required super.isCurrentRoundPaid,
    required super.totalConfirmedContributions,
    required super.slotStatus,
    required super.isWinner,
    super.winningRoundIndex,
    super.winningDrawDate,
    super.winningPrizeAmount,
    required super.winningEntitlementClaimed,
    required super.isRaffleEligible,
    super.raffleEligibilityReason,
    required this.planId,
    required this.planName,
    this.planCode,
    required this.currency,
    required this.instalments,
    this.entitlementClaimInfo,
  });

  factory CustomerChituSlotDetailDto.fromJson(Map<String, dynamic> json) {
    final base = CustomerChituSlotSummaryDto.fromJson(json);
    final rawInstalments = (json['instalments'] as List?) ?? const [];
    final rawClaim = json['entitlementClaimInfo'];
    return CustomerChituSlotDetailDto(
      slotId: base.slotId,
      slotNumber: base.slotNumber,
      slotDisplayCode: base.slotDisplayCode,
      joinedAtUtc: base.joinedAtUtc,
      monthlySlotPrice: base.monthlySlotPrice,
      durationMonths: base.durationMonths,
      instalmentsPaidCount: base.instalmentsPaidCount,
      paidThroughRound: base.paidThroughRound,
      arrearsMonths: base.arrearsMonths,
      arrearsAmount: base.arrearsAmount,
      advancePaidMonths: base.advancePaidMonths,
      advancePaidAmount: base.advancePaidAmount,
      isCurrentRoundPaid: base.isCurrentRoundPaid,
      totalConfirmedContributions: base.totalConfirmedContributions,
      slotStatus: base.slotStatus,
      isWinner: base.isWinner,
      winningRoundIndex: base.winningRoundIndex,
      winningDrawDate: base.winningDrawDate,
      winningPrizeAmount: base.winningPrizeAmount,
      winningEntitlementClaimed: base.winningEntitlementClaimed,
      isRaffleEligible: base.isRaffleEligible,
      raffleEligibilityReason: base.raffleEligibilityReason,
      planId: (json['planId'] ?? '').toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      planCode: json['planCode']?.toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      instalments: rawInstalments
          .whereType<Map>()
          .map(
            (e) => CustomerChituSlotInstalmentDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      entitlementClaimInfo: rawClaim is Map
          ? CustomerWinnerBenefitDto.fromJson(
              Map<String, dynamic>.from(rawClaim),
            )
          : null,
    );
  }
}

class CustomerPayableSlotItemDto {
  final String slotId;
  final int slotNumber;
  final String slotDisplayCode;
  final String status;
  final bool isEligibleForPayment;
  final String? ineligibilityReason;
  final double monthlyInstalmentPrice;
  final List<int> paidInstalmentRounds;
  final List<int> unpaidInstalmentRounds;
  final double outstandingDueAmount;
  final int remainingPayableMonths;
  final bool isCurrentRoundPaid;
  final int instalmentsPaidCount;
  final double totalConfirmedPaid;

  const CustomerPayableSlotItemDto({
    required this.slotId,
    required this.slotNumber,
    required this.slotDisplayCode,
    required this.status,
    required this.isEligibleForPayment,
    this.ineligibilityReason,
    required this.monthlyInstalmentPrice,
    required this.paidInstalmentRounds,
    required this.unpaidInstalmentRounds,
    required this.outstandingDueAmount,
    required this.remainingPayableMonths,
    required this.isCurrentRoundPaid,
    required this.instalmentsPaidCount,
    required this.totalConfirmedPaid,
  });

  factory CustomerPayableSlotItemDto.fromJson(Map<String, dynamic> json) {
    final slotNum = (json['slotNumber'] as num?)?.toInt() ?? 0;
    final paidList = (json['paidInstalmentRounds'] as List?) ?? const [];
    final unpaidList = (json['unpaidInstalmentRounds'] as List?) ?? const [];
    return CustomerPayableSlotItemDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotNumber: slotNum,
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot #$slotNum').toString(),
      status: (json['status'] ?? 'Active').toString(),
      isEligibleForPayment: json['isEligibleForPayment'] as bool? ?? false,
      ineligibilityReason: json['ineligibilityReason']?.toString(),
      monthlyInstalmentPrice:
          (json['monthlyInstalmentPrice'] as num?)?.toDouble() ?? 0.0,
      paidInstalmentRounds: paidList
          .whereType<num>()
          .map((e) => e.toInt())
          .toList(),
      unpaidInstalmentRounds: unpaidList
          .whereType<num>()
          .map((e) => e.toInt())
          .toList(),
      outstandingDueAmount:
          (json['outstandingDueAmount'] as num?)?.toDouble() ?? 0.0,
      remainingPayableMonths:
          (json['remainingPayableMonths'] as num?)?.toInt() ?? 0,
      isCurrentRoundPaid: json['isCurrentRoundPaid'] as bool? ?? false,
      instalmentsPaidCount:
          (json['instalmentsPaidCount'] as num?)?.toInt() ?? 0,
      totalConfirmedPaid:
          (json['totalConfirmedPaid'] as num?)?.toDouble() ?? 0.0,
    );
  }
}

class CustomerPayableSlotsDto {
  final String planId;
  final String? planCode;
  final String planName;
  final double monthlySlotPrice;
  final String currency;
  final int currentRoundIndex;
  final List<CustomerPayableSlotItemDto> slots;

  const CustomerPayableSlotsDto({
    required this.planId,
    this.planCode,
    required this.planName,
    required this.monthlySlotPrice,
    required this.currency,
    required this.currentRoundIndex,
    required this.slots,
  });

  factory CustomerPayableSlotsDto.fromJson(Map<String, dynamic> json) {
    final rawSlots = (json['slots'] as List?) ?? const [];
    return CustomerPayableSlotsDto(
      planId: (json['planId'] ?? '').toString(),
      planCode: json['planCode']?.toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      currency: (json['currency'] ?? 'LKR').toString(),
      currentRoundIndex: (json['currentRoundIndex'] as num?)?.toInt() ?? 1,
      slots: rawSlots
          .whereType<Map>()
          .map(
            (e) => CustomerPayableSlotItemDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
    );
  }
}

class CustomerSlotPaymentSelectionDto {
  final String slotId;
  final int instalmentsToPayCount;

  const CustomerSlotPaymentSelectionDto({
    required this.slotId,
    required this.instalmentsToPayCount,
  });

  Map<String, dynamic> toJson() => {
    'slotId': slotId,
    'instalmentsToPayCount': instalmentsToPayCount,
  };
}

class CustomerSlotPaymentPreviewItemDto {
  final String slotId;
  final String slotDisplayCode;
  final int instalmentsToPayCount;
  final double amountAllocated;
  final List<int> clearedRounds;
  final double remainingArrearsAfterPayment;
  final int advanceCoverageMonthsAfterPayment;
  final bool resultingCurrentRoundCoverage;
  final bool resultingRaffleEligible;

  const CustomerSlotPaymentPreviewItemDto({
    required this.slotId,
    required this.slotDisplayCode,
    required this.instalmentsToPayCount,
    required this.amountAllocated,
    required this.clearedRounds,
    required this.remainingArrearsAfterPayment,
    required this.advanceCoverageMonthsAfterPayment,
    required this.resultingCurrentRoundCoverage,
    required this.resultingRaffleEligible,
  });

  factory CustomerSlotPaymentPreviewItemDto.fromJson(
    Map<String, dynamic> json,
  ) {
    final rounds = (json['clearedRounds'] as List?) ?? const [];
    return CustomerSlotPaymentPreviewItemDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot').toString(),
      instalmentsToPayCount:
          (json['instalmentsToPayCount'] as num?)?.toInt() ?? 1,
      amountAllocated: (json['amountAllocated'] as num?)?.toDouble() ?? 0.0,
      clearedRounds: rounds.whereType<num>().map((e) => e.toInt()).toList(),
      remainingArrearsAfterPayment:
          (json['remainingArrearsAfterPayment'] as num?)?.toDouble() ?? 0.0,
      advanceCoverageMonthsAfterPayment:
          (json['advanceCoverageMonthsAfterPayment'] as num?)?.toInt() ?? 0,
      resultingCurrentRoundCoverage:
          json['resultingCurrentRoundCoverage'] as bool? ?? false,
      resultingRaffleEligible:
          json['resultingRaffleEligible'] as bool? ?? false,
    );
  }
}

class CustomerChituPaymentPreviewResponse {
  final String planId;
  final String? planCode;
  final String planName;
  final String currency;
  final double monthlySlotPrice;
  final double totalPayableAmount;
  final List<CustomerSlotPaymentPreviewItemDto> slots;
  final String? previewReference;
  final String? previewCreatedAtUtc;
  final String? previewExpiresAtUtc;

  const CustomerChituPaymentPreviewResponse({
    required this.planId,
    this.planCode,
    required this.planName,
    required this.currency,
    required this.monthlySlotPrice,
    required this.totalPayableAmount,
    required this.slots,
    this.previewReference,
    this.previewCreatedAtUtc,
    this.previewExpiresAtUtc,
  });

  factory CustomerChituPaymentPreviewResponse.fromJson(
    Map<String, dynamic> json,
  ) {
    final rawSlots = (json['slots'] as List?) ?? const [];
    return CustomerChituPaymentPreviewResponse(
      planId: (json['planId'] ?? '').toString(),
      planCode: json['planCode']?.toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      totalPayableAmount:
          (json['totalPayableAmount'] as num?)?.toDouble() ?? 0.0,
      slots: rawSlots
          .whereType<Map>()
          .map(
            (e) => CustomerSlotPaymentPreviewItemDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      previewReference: json['previewReference']?.toString(),
      previewCreatedAtUtc: json['previewCreatedAtUtc']?.toString(),
      previewExpiresAtUtc: json['previewExpiresAtUtc']?.toString(),
    );
  }
}

class CustomerSlotPaymentReceiptDto {
  final String slotId;
  final String slotDisplayCode;
  final double amountAllocated;
  final int monthsCovered;
  final List<int> clearedMonthIndices;

  const CustomerSlotPaymentReceiptDto({
    required this.slotId,
    required this.slotDisplayCode,
    required this.amountAllocated,
    required this.monthsCovered,
    required this.clearedMonthIndices,
  });

  factory CustomerSlotPaymentReceiptDto.fromJson(Map<String, dynamic> json) {
    final indices = (json['clearedMonthIndices'] as List?) ?? const [];
    return CustomerSlotPaymentReceiptDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot').toString(),
      amountAllocated: (json['amountAllocated'] as num?)?.toDouble() ?? 0.0,
      monthsCovered: (json['monthsCovered'] as num?)?.toInt() ?? 1,
      clearedMonthIndices: indices
          .whereType<num>()
          .map((e) => e.toInt())
          .toList(),
    );
  }
}

class CustomerChituPaymentConfirmResponse {
  final String paymentId;
  final String planId;
  final String? planCode;
  final String planName;
  final double totalAmount;
  final String currency;
  final String paidAtUtc;
  final String? paymentMethod;
  final bool isSimulated;
  final List<CustomerSlotPaymentReceiptDto> receipts;
  final String? message;
  final bool isGatewayDeferred;

  const CustomerChituPaymentConfirmResponse({
    required this.paymentId,
    required this.planId,
    this.planCode,
    required this.planName,
    required this.totalAmount,
    required this.currency,
    required this.paidAtUtc,
    this.paymentMethod,
    required this.isSimulated,
    required this.receipts,
    this.message,
    this.isGatewayDeferred = false,
  });

  factory CustomerChituPaymentConfirmResponse.fromJson(
    Map<String, dynamic> json, {
    bool isGatewayDeferred = false,
  }) {
    final rawReceipts = (json['receipts'] as List?) ?? const [];
    return CustomerChituPaymentConfirmResponse(
      paymentId: (json['paymentId'] ?? '').toString(),
      planId: (json['planId'] ?? '').toString(),
      planCode: json['planCode']?.toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      totalAmount:
          (json['totalAmount'] as num?)?.toDouble() ??
          (json['amountToPay'] as num?)?.toDouble() ??
          0.0,
      currency: (json['currency'] ?? 'LKR').toString(),
      paidAtUtc: (json['paidAtUtc'] ?? '').toString(),
      paymentMethod: json['paymentMethod']?.toString(),
      isSimulated: json['isSimulated'] as bool? ?? false,
      receipts: rawReceipts
          .whereType<Map>()
          .map(
            (e) => CustomerSlotPaymentReceiptDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      message: (json['message'] ?? json['detail'] ?? json['title'])?.toString(),
      isGatewayDeferred: isGatewayDeferred,
    );
  }
}

class CustomerPaymentSummaryDto {
  final String paymentId;
  final String planType;
  final String? planId;
  final String planName;
  final String? planCode;
  final String currency;
  final double amount;
  final String status;
  final String? paymentMethod;
  final String? provider;
  final String? providerReference;
  final String createdAtUtc;
  final String? confirmedAtUtc;
  final bool isSimulated;
  final int allocationsCount;
  final int? slotsCoveredCount;
  final bool hasDetails;
  final String? summaryDescription;

  const CustomerPaymentSummaryDto({
    required this.paymentId,
    required this.planType,
    this.planId,
    required this.planName,
    this.planCode,
    required this.currency,
    required this.amount,
    required this.status,
    this.paymentMethod,
    this.provider,
    this.providerReference,
    required this.createdAtUtc,
    this.confirmedAtUtc,
    required this.isSimulated,
    required this.allocationsCount,
    this.slotsCoveredCount,
    required this.hasDetails,
    this.summaryDescription,
  });

  factory CustomerPaymentSummaryDto.fromJson(Map<String, dynamic> json) {
    return CustomerPaymentSummaryDto(
      paymentId: (json['paymentId'] ?? json['id'] ?? '').toString(),
      planType: (json['planType'] ?? 'Chitu').toString(),
      planId: json['planId']?.toString(),
      planName: (json['planName'] ?? 'Plan Payment').toString(),
      planCode: json['planCode']?.toString(),
      currency: (json['currency'] ?? 'LKR').toString(),
      amount: (json['amount'] as num?)?.toDouble() ?? 0.0,
      status: (json['status'] ?? 'Confirmed').toString(),
      paymentMethod: json['paymentMethod']?.toString(),
      provider: json['provider']?.toString(),
      providerReference: json['providerReference']?.toString(),
      createdAtUtc: (json['createdAtUtc'] ?? '').toString(),
      confirmedAtUtc: json['confirmedAtUtc']?.toString(),
      isSimulated: json['isSimulated'] as bool? ?? false,
      allocationsCount: (json['allocationsCount'] as num?)?.toInt() ?? 1,
      slotsCoveredCount: (json['slotsCoveredCount'] as num?)?.toInt(),
      hasDetails: json['hasDetails'] as bool? ?? false,
      summaryDescription: json['summaryDescription']?.toString(),
    );
  }
}

class CustomerPaymentsPagedResponse {
  final List<CustomerPaymentSummaryDto> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;

  const CustomerPaymentsPagedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  factory CustomerPaymentsPagedResponse.fromJson(Map<String, dynamic> json) {
    final rawItems = (json['items'] as List?) ?? const [];
    return CustomerPaymentsPagedResponse(
      items: rawItems
          .whereType<Map>()
          .map(
            (e) => CustomerPaymentSummaryDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
      totalCount: (json['totalCount'] as num?)?.toInt() ?? rawItems.length,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    );
  }
}

class CustomerChituPaymentSlotAllocationDto {
  final String slotId;
  final int slotNumber;
  final String slotDisplayCode;
  final double allocatedAmount;
  final List<int> instalmentRoundsCovered;
  final bool isJoiningPayment;

  const CustomerChituPaymentSlotAllocationDto({
    required this.slotId,
    required this.slotNumber,
    required this.slotDisplayCode,
    required this.allocatedAmount,
    required this.instalmentRoundsCovered,
    required this.isJoiningPayment,
  });

  factory CustomerChituPaymentSlotAllocationDto.fromJson(
    Map<String, dynamic> json,
  ) {
    final slotNum = (json['slotNumber'] as num?)?.toInt() ?? 0;
    final rounds = (json['instalmentRoundsCovered'] as List?) ?? const [];
    return CustomerChituPaymentSlotAllocationDto(
      slotId: (json['slotId'] ?? '').toString(),
      slotNumber: slotNum,
      slotDisplayCode: (json['slotDisplayCode'] ?? 'Slot #$slotNum').toString(),
      allocatedAmount: (json['allocatedAmount'] as num?)?.toDouble() ?? 0.0,
      instalmentRoundsCovered: rounds
          .whereType<num>()
          .map((e) => e.toInt())
          .toList(),
      isJoiningPayment: json['isJoiningPayment'] as bool? ?? false,
    );
  }
}

class CustomerChituPaymentDetailBreakdownDto {
  final String planId;
  final String planName;
  final String? planCode;
  final double monthlySlotPrice;
  final bool isJoiningPayment;
  final List<CustomerChituPaymentSlotAllocationDto> slotAllocations;

  const CustomerChituPaymentDetailBreakdownDto({
    required this.planId,
    required this.planName,
    this.planCode,
    required this.monthlySlotPrice,
    required this.isJoiningPayment,
    required this.slotAllocations,
  });

  factory CustomerChituPaymentDetailBreakdownDto.fromJson(
    Map<String, dynamic> json,
  ) {
    final rawAllocations = (json['slotAllocations'] as List?) ?? const [];
    return CustomerChituPaymentDetailBreakdownDto(
      planId: (json['planId'] ?? '').toString(),
      planName: (json['planName'] ?? 'Chittu Plan').toString(),
      planCode: json['planCode']?.toString(),
      monthlySlotPrice: (json['monthlySlotPrice'] as num?)?.toDouble() ?? 0.0,
      isJoiningPayment: json['isJoiningPayment'] as bool? ?? false,
      slotAllocations: rawAllocations
          .whereType<Map>()
          .map(
            (e) => CustomerChituPaymentSlotAllocationDto.fromJson(
              Map<String, dynamic>.from(e),
            ),
          )
          .toList(),
    );
  }
}

class CustomerJewelleryPaymentDetailBreakdownDto {
  final String enrolmentId;
  final String? enrolmentDisplayNumber;
  final double appliedGoldRatePerGram;
  final double appliedPremiumPercentage;
  final double creditedGrams;
  final int? instalmentMonthIndex;
  final String? paymentType;

  const CustomerJewelleryPaymentDetailBreakdownDto({
    required this.enrolmentId,
    this.enrolmentDisplayNumber,
    required this.appliedGoldRatePerGram,
    required this.appliedPremiumPercentage,
    required this.creditedGrams,
    this.instalmentMonthIndex,
    this.paymentType,
  });

  factory CustomerJewelleryPaymentDetailBreakdownDto.fromJson(
    Map<String, dynamic> json,
  ) {
    return CustomerJewelleryPaymentDetailBreakdownDto(
      enrolmentId: (json['enrolmentId'] ?? '').toString(),
      enrolmentDisplayNumber: json['enrolmentDisplayNumber']?.toString(),
      appliedGoldRatePerGram:
          (json['appliedGoldRatePerGram'] as num?)?.toDouble() ?? 0.0,
      appliedPremiumPercentage:
          (json['appliedPremiumPercentage'] as num?)?.toDouble() ?? 0.0,
      creditedGrams: (json['creditedGrams'] as num?)?.toDouble() ?? 0.0,
      instalmentMonthIndex: (json['instalmentMonthIndex'] as num?)?.toInt(),
      paymentType: json['paymentType']?.toString(),
    );
  }
}

class CustomerPaymentDetailDto extends CustomerPaymentSummaryDto {
  final String? remarks;
  final CustomerChituPaymentDetailBreakdownDto? chituDetails;
  final CustomerJewelleryPaymentDetailBreakdownDto? jewelleryDetails;

  const CustomerPaymentDetailDto({
    required super.paymentId,
    required super.planType,
    super.planId,
    required super.planName,
    super.planCode,
    required super.currency,
    required super.amount,
    required super.status,
    super.paymentMethod,
    super.provider,
    super.providerReference,
    required super.createdAtUtc,
    super.confirmedAtUtc,
    required super.isSimulated,
    required super.allocationsCount,
    super.slotsCoveredCount,
    required super.hasDetails,
    super.summaryDescription,
    this.remarks,
    this.chituDetails,
    this.jewelleryDetails,
  });

  factory CustomerPaymentDetailDto.fromJson(Map<String, dynamic> json) {
    final base = CustomerPaymentSummaryDto.fromJson(json);
    final rawChitu = json['chituDetails'];
    final rawJewellery = json['jewelleryDetails'];
    return CustomerPaymentDetailDto(
      paymentId: base.paymentId,
      planType: base.planType,
      planId: base.planId,
      planName: base.planName,
      planCode: base.planCode,
      currency: base.currency,
      amount: base.amount,
      status: base.status,
      paymentMethod: base.paymentMethod,
      provider: base.provider,
      providerReference: base.providerReference,
      createdAtUtc: base.createdAtUtc,
      confirmedAtUtc: base.confirmedAtUtc,
      isSimulated: base.isSimulated,
      allocationsCount: base.allocationsCount,
      slotsCoveredCount: base.slotsCoveredCount,
      hasDetails: base.hasDetails,
      summaryDescription: base.summaryDescription,
      remarks: json['remarks']?.toString(),
      chituDetails: rawChitu is Map
          ? CustomerChituPaymentDetailBreakdownDto.fromJson(
              Map<String, dynamic>.from(rawChitu),
            )
          : null,
      jewelleryDetails: rawJewellery is Map
          ? CustomerJewelleryPaymentDetailBreakdownDto.fromJson(
              Map<String, dynamic>.from(rawJewellery),
            )
          : null,
    );
  }
}
