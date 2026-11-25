class CustomerJewelleryCategoryDto {
  final String id;
  final String name;
  final String? description;
  final String? imageUrl;
  final int availablePlansCount;

  const CustomerJewelleryCategoryDto({
    required this.id,
    required this.name,
    this.description,
    this.imageUrl,
    required this.availablePlansCount,
  });

  factory CustomerJewelleryCategoryDto.fromJson(Map<String, dynamic> json) {
    return CustomerJewelleryCategoryDto(
      id: (json['id'] ?? '').toString(),
      name: (json['name'] as String?) ?? 'Jewellery',
      description: json['description'] as String?,
      imageUrl: json['imageUrl'] as String?,
      availablePlansCount: (json['availablePlansCount'] as num?)?.toInt() ?? 0,
    );
  }
}

class CustomerJewelleryPlanDto {
  final String id;
  final String name;
  final String? categoryId;
  final String? categoryName;
  final String? imageUrl;
  final double targetGrams;
  final List<int> offeredDurations;
  final double premiumPercentage;
  final String? startDate;
  final String? description;

  const CustomerJewelleryPlanDto({
    required this.id,
    required this.name,
    this.categoryId,
    this.categoryName,
    this.imageUrl,
    required this.targetGrams,
    required this.offeredDurations,
    required this.premiumPercentage,
    this.startDate,
    this.description,
  });

  factory CustomerJewelleryPlanDto.fromJson(Map<String, dynamic> json) {
    final rawDurations =
        (json['offeredDurations'] as List?) ??
        (json['allowedDurationsMonths'] as List?) ??
        (json['durations'] as List?) ??
        (json['durationsMonths'] as List?) ??
        const [];
    return CustomerJewelleryPlanDto(
      id: (json['id'] ?? json['planId'] ?? '').toString(),
      name:
          (json['name'] as String?) ??
          (json['title'] as String?) ??
          (json['planName'] as String?) ??
          'Jewellery Plan',
      categoryId: (json['categoryId'] ?? json['category']?['id'])?.toString(),
      categoryName:
          (json['categoryName'] ?? json['category']?['name']) as String?,
      imageUrl: (json['imageUrl'] ?? json['image']) as String?,
      targetGrams:
          ((json['targetGrams'] ??
                      json['targetProductGoldWeightGrams'] ??
                      json['targetWeightGrams'] ??
                      json['targetWeight'] ??
                      json['goldWeight'])
                  as num?)
              ?.toDouble() ??
          0.0,
      offeredDurations: rawDurations
          .whereType<num>()
          .map((e) => e.toInt())
          .toList(),
      premiumPercentage:
          ((json['premiumPercentage'] ?? json['premium']) as num?)
              ?.toDouble() ??
          0.0,
      startDate: (json['startDate'] ?? json['createdAtUtc']) as String?,
      description: json['description'] as String?,
    );
  }
}

class EnrolmentPreviewResponse {
  final String planId;
  final String planName;
  final String? categoryId;
  final String? categoryName;
  final String? imageUrl;
  final double targetGrams;
  final int selectedDurationMonths;
  final double premiumPercentage;
  final String? proposedJoiningDate;
  final String? proposedDeadline;
  final String currency;
  final bool hasPreviouslyJoined;
  final int totalEnrolmentCount;
  final int activeEnrolmentCount;
  final bool requiresRepeatJoinConfirmation;

  const EnrolmentPreviewResponse({
    required this.planId,
    required this.planName,
    this.categoryId,
    this.categoryName,
    this.imageUrl,
    required this.targetGrams,
    required this.selectedDurationMonths,
    required this.premiumPercentage,
    this.proposedJoiningDate,
    this.proposedDeadline,
    required this.currency,
    required this.hasPreviouslyJoined,
    required this.totalEnrolmentCount,
    required this.activeEnrolmentCount,
    required this.requiresRepeatJoinConfirmation,
  });

  factory EnrolmentPreviewResponse.fromJson(Map<String, dynamic> json) {
    return EnrolmentPreviewResponse(
      planId: (json['planId'] ?? '').toString(),
      planName: (json['planName'] as String?) ?? 'Jewellery Plan',
      categoryId: json['categoryId'] as String?,
      categoryName: json['categoryName'] as String?,
      imageUrl: json['imageUrl'] as String?,
      targetGrams: (json['targetGrams'] as num?)?.toDouble() ?? 0.0,
      selectedDurationMonths:
          (json['selectedDurationMonths'] as num?)?.toInt() ?? 6,
      premiumPercentage: (json['premiumPercentage'] as num?)?.toDouble() ?? 0.0,
      proposedJoiningDate: json['proposedJoiningDate'] as String?,
      proposedDeadline: json['proposedDeadline'] as String?,
      currency: (json['currency'] as String?) ?? 'LKR',
      hasPreviouslyJoined: json['hasPreviouslyJoined'] == true,
      totalEnrolmentCount: (json['totalEnrolmentCount'] as num?)?.toInt() ?? 0,
      activeEnrolmentCount:
          (json['activeEnrolmentCount'] as num?)?.toInt() ?? 0,
      requiresRepeatJoinConfirmation:
          json['requiresRepeatJoinConfirmation'] == true,
    );
  }
}

class CustomerEnrolmentDto {
  final String enrolmentId;
  final String planId;
  final String planName;
  final String? categoryName;
  final String? imageUrl;
  final double targetGrams;
  final int selectedDurationMonths;
  final String? joiningDate;
  final String? deadline;
  final double premiumPercentage;
  final String currency;
  final double totalConfirmedMoneyPaid;
  final double netAccumulatedGoldGrams;
  final double remainingGrams;
  final double individualProgressPercentage;
  final String enrolmentStatus;
  final bool isContributionAllowed;
  final String? contributionBlockedReason;
  final String? createdAtUtc;

  const CustomerEnrolmentDto({
    required this.enrolmentId,
    required this.planId,
    required this.planName,
    this.categoryName,
    this.imageUrl,
    required this.targetGrams,
    required this.selectedDurationMonths,
    this.joiningDate,
    this.deadline,
    required this.premiumPercentage,
    required this.currency,
    required this.totalConfirmedMoneyPaid,
    required this.netAccumulatedGoldGrams,
    required this.remainingGrams,
    required this.individualProgressPercentage,
    required this.enrolmentStatus,
    required this.isContributionAllowed,
    this.contributionBlockedReason,
    this.createdAtUtc,
  });

  factory CustomerEnrolmentDto.fromJson(Map<String, dynamic> json) {
    return CustomerEnrolmentDto(
      enrolmentId: (json['enrolmentId'] ?? json['id'] ?? '').toString(),
      planId: (json['planId'] ?? '').toString(),
      planName: (json['planName'] as String?) ?? 'Jewellery Plan',
      categoryName: json['categoryName'] as String?,
      imageUrl: json['imageUrl'] as String?,
      targetGrams: (json['targetGrams'] as num?)?.toDouble() ?? 0.0,
      selectedDurationMonths:
          (json['selectedDurationMonths'] as num?)?.toInt() ?? 0,
      joiningDate: json['joiningDate'] as String?,
      deadline: json['deadline'] as String?,
      premiumPercentage: (json['premiumPercentage'] as num?)?.toDouble() ?? 0.0,
      currency: (json['currency'] as String?) ?? 'LKR',
      totalConfirmedMoneyPaid:
          (json['totalConfirmedMoneyPaid'] as num?)?.toDouble() ?? 0.0,
      netAccumulatedGoldGrams:
          (json['netAccumulatedGoldGrams'] as num?)?.toDouble() ?? 0.0,
      remainingGrams: (json['remainingGrams'] as num?)?.toDouble() ?? 0.0,
      individualProgressPercentage:
          (json['individualProgressPercentage'] as num?)?.toDouble() ?? 0.0,
      enrolmentStatus: (json['enrolmentStatus'] as String?) ?? 'Active',
      isContributionAllowed: json['isContributionAllowed'] == true,
      contributionBlockedReason: json['contributionBlockedReason'] as String?,
      createdAtUtc: json['createdAtUtc'] as String?,
    );
  }

  bool get isCompletedOrEligible {
    final s = enrolmentStatus.toLowerCase();
    return s.contains('complete') ||
        s.contains('eligib') ||
        (targetGrams > 0 && remainingGrams <= 0);
  }
}

class CustomerEnrolmentsPagedResponse {
  final List<CustomerEnrolmentDto> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;

  const CustomerEnrolmentsPagedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  factory CustomerEnrolmentsPagedResponse.fromJson(Map<String, dynamic> json) {
    final rawItems = (json['items'] as List?) ?? const [];
    return CustomerEnrolmentsPagedResponse(
      items: rawItems
          .whereType<Map>()
          .map(
            (e) => CustomerEnrolmentDto.fromJson(Map<String, dynamic>.from(e)),
          )
          .toList(),
      totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    );
  }
}

class CustomerEnrolmentPaymentDto {
  final String paymentId;
  final String contributionId;
  final double currencyAmount;
  final String currency;
  final double goldGramsCredited;
  final double appliedRatePerGram;
  final String? confirmedAtUtc;
  final String status;
  final String? paymentMethod;
  final String? paymentReference;
  final String? enrolmentId;
  final String? planName;

  const CustomerEnrolmentPaymentDto({
    required this.paymentId,
    required this.contributionId,
    required this.currencyAmount,
    required this.currency,
    required this.goldGramsCredited,
    required this.appliedRatePerGram,
    this.confirmedAtUtc,
    required this.status,
    this.paymentMethod,
    this.paymentReference,
    this.enrolmentId,
    this.planName,
  });

  factory CustomerEnrolmentPaymentDto.fromJson(
    Map<String, dynamic> json, {
    String? enrolmentId,
    String? planName,
  }) {
    return CustomerEnrolmentPaymentDto(
      paymentId: (json['paymentId'] ?? json['id'] ?? '').toString(),
      contributionId: (json['contributionId'] ?? '').toString(),
      currencyAmount: (json['currencyAmount'] as num?)?.toDouble() ?? 0.0,
      currency: (json['currency'] as String?) ?? 'LKR',
      goldGramsCredited:
          (json['goldGramsCredited'] ?? json['goldGrams'] as num?)
              ?.toDouble() ??
          0.0,
      appliedRatePerGram:
          (json['appliedRatePerGram'] as num?)?.toDouble() ?? 0.0,
      confirmedAtUtc: json['confirmedAtUtc'] as String?,
      status: (json['status'] as String?) ?? 'Succeeded',
      paymentMethod: json['paymentMethod'] as String?,
      paymentReference: json['paymentReference'] as String?,
      enrolmentId: enrolmentId ?? (json['enrolmentId'] as String?),
      planName: planName ?? (json['planName'] as String?),
    );
  }
}

class JewelleryCalculationPreviewResponse {
  final String inputMode;
  final double? enteredAmount;
  final double? requestedGrams;
  final String currency;
  final double ratePerGram;
  final double premiumPercentage;
  final String? rateAsOf;
  final String? rateSource;
  final double baseGoldGrams;
  final double premiumGrams;
  final double? chargeableGoldGrams;
  final double totalGoldCredit;
  final double? baseAmount;
  final double? premiumAmount;
  final double amountToPay;
  final double remainingGramsAfterPayment;
  final double remainingTargetGrams;
  final bool willExceedTarget;
  final double? maximumPayableAmountForTarget;

  const JewelleryCalculationPreviewResponse({
    required this.inputMode,
    this.enteredAmount,
    this.requestedGrams,
    required this.currency,
    required this.ratePerGram,
    required this.premiumPercentage,
    this.rateAsOf,
    this.rateSource,
    required this.baseGoldGrams,
    required this.premiumGrams,
    this.chargeableGoldGrams,
    required this.totalGoldCredit,
    this.baseAmount,
    this.premiumAmount,
    required this.amountToPay,
    required this.remainingGramsAfterPayment,
    required this.remainingTargetGrams,
    required this.willExceedTarget,
    this.maximumPayableAmountForTarget,
  });

  factory JewelleryCalculationPreviewResponse.fromJson(
    Map<String, dynamic> json,
  ) {
    return JewelleryCalculationPreviewResponse(
      inputMode: (json['inputMode'] as String?) ?? 'Amount',
      enteredAmount: (json['enteredAmount'] as num?)?.toDouble(),
      requestedGrams: (json['requestedGrams'] as num?)?.toDouble(),
      currency: (json['currency'] as String?) ?? 'LKR',
      ratePerGram: (json['ratePerGram'] as num?)?.toDouble() ?? 0.0,
      premiumPercentage: (json['premiumPercentage'] as num?)?.toDouble() ?? 0.0,
      rateAsOf: json['rateAsOf'] as String?,
      rateSource: json['rateSource'] as String?,
      baseGoldGrams: (json['baseGoldGrams'] as num?)?.toDouble() ?? 0.0,
      premiumGrams: (json['premiumGrams'] as num?)?.toDouble() ?? 0.0,
      chargeableGoldGrams: (json['chargeableGoldGrams'] as num?)?.toDouble(),
      totalGoldCredit: (json['totalGoldCredit'] as num?)?.toDouble() ?? 0.0,
      baseAmount: (json['baseAmount'] as num?)?.toDouble(),
      premiumAmount: (json['premiumAmount'] as num?)?.toDouble(),
      amountToPay: (json['amountToPay'] as num?)?.toDouble() ?? 0.0,
      remainingGramsAfterPayment:
          (json['remainingGramsAfterPayment'] as num?)?.toDouble() ?? 0.0,
      remainingTargetGrams:
          (json['remainingTargetGrams'] as num?)?.toDouble() ?? 0.0,
      willExceedTarget: json['willExceedTarget'] == true,
      maximumPayableAmountForTarget:
          (json['maximumPayableAmountForTarget'] as num?)?.toDouble(),
    );
  }
}

class CustomerPaymentQuoteDto {
  final String quoteId;
  final String enrolmentId;
  final String inputMode;
  final double? enteredAmount;
  final double? requestedGrams;
  final String currency;
  final double ratePerGram;
  final double premiumPercentage;
  final String? rateAsOf;
  final String? rateSource;
  final double baseGoldGrams;
  final double premiumGrams;
  final double? chargeableGoldGrams;
  final double totalGoldCredit;
  final double? baseAmount;
  final double? premiumAmount;
  final double amountToPay;
  final double remainingGramsAfterPayment;
  final String? createdAtUtc;
  final String? expiresAtUtc;
  final String status;
  final bool isExpired;

  const CustomerPaymentQuoteDto({
    required this.quoteId,
    required this.enrolmentId,
    required this.inputMode,
    this.enteredAmount,
    this.requestedGrams,
    required this.currency,
    required this.ratePerGram,
    required this.premiumPercentage,
    this.rateAsOf,
    this.rateSource,
    required this.baseGoldGrams,
    required this.premiumGrams,
    this.chargeableGoldGrams,
    required this.totalGoldCredit,
    this.baseAmount,
    this.premiumAmount,
    required this.amountToPay,
    required this.remainingGramsAfterPayment,
    this.createdAtUtc,
    this.expiresAtUtc,
    required this.status,
    required this.isExpired,
  });

  factory CustomerPaymentQuoteDto.fromJson(Map<String, dynamic> json) {
    return CustomerPaymentQuoteDto(
      quoteId: (json['quoteId'] ?? '').toString(),
      enrolmentId: (json['enrolmentId'] ?? '').toString(),
      inputMode: (json['inputMode'] as String?) ?? 'Amount',
      enteredAmount: (json['enteredAmount'] as num?)?.toDouble(),
      requestedGrams: (json['requestedGrams'] as num?)?.toDouble(),
      currency: (json['currency'] as String?) ?? 'LKR',
      ratePerGram: (json['ratePerGram'] as num?)?.toDouble() ?? 0.0,
      premiumPercentage: (json['premiumPercentage'] as num?)?.toDouble() ?? 0.0,
      rateAsOf: json['rateAsOf'] as String?,
      rateSource: json['rateSource'] as String?,
      baseGoldGrams: (json['baseGoldGrams'] as num?)?.toDouble() ?? 0.0,
      premiumGrams: (json['premiumGrams'] as num?)?.toDouble() ?? 0.0,
      chargeableGoldGrams: (json['chargeableGoldGrams'] as num?)?.toDouble(),
      totalGoldCredit: (json['totalGoldCredit'] as num?)?.toDouble() ?? 0.0,
      baseAmount: (json['baseAmount'] as num?)?.toDouble(),
      premiumAmount: (json['premiumAmount'] as num?)?.toDouble(),
      amountToPay: (json['amountToPay'] as num?)?.toDouble() ?? 0.0,
      remainingGramsAfterPayment:
          (json['remainingGramsAfterPayment'] as num?)?.toDouble() ?? 0.0,
      createdAtUtc: json['createdAtUtc'] as String?,
      expiresAtUtc: json['expiresAtUtc'] as String?,
      status: (json['status'] as String?) ?? 'Active',
      isExpired: json['isExpired'] == true,
    );
  }
}

class PaymentInitiationResponse {
  final String? code;
  final String? message;
  final String quoteId;
  final double amountToPay;
  final String currency;
  final bool isConfigured;
  final int httpStatusCode;
  final bool isSuccess;
  final double goldGramsCredited;
  final String? transactionId;
  final String? paidAtUtc;

  const PaymentInitiationResponse({
    this.code,
    this.message,
    required this.quoteId,
    required this.amountToPay,
    required this.currency,
    required this.isConfigured,
    required this.httpStatusCode,
    this.isSuccess = false,
    this.goldGramsCredited = 0.0,
    this.transactionId,
    this.paidAtUtc,
  });

  factory PaymentInitiationResponse.fromJson(
    Map<String, dynamic> json, {
    int httpStatusCode = 501,
    bool isSuccess = false,
    double goldGramsCredited = 0.0,
    String? transactionId,
  }) {
    return PaymentInitiationResponse(
      code: json['code'] as String?,
      message: (json['message'] ?? json['detail']) as String?,
      quoteId: (json['quoteId'] ?? '').toString(),
      amountToPay: (json['amountToPay'] as num?)?.toDouble() ?? 0.0,
      currency: (json['currency'] as String?) ?? 'LKR',
      isConfigured: json['isConfigured'] == true,
      httpStatusCode: httpStatusCode,
      isSuccess: isSuccess || (json['isSuccess'] == true),
      goldGramsCredited:
          (json['goldGramsCredited'] as num?)?.toDouble() ?? goldGramsCredited,
      transactionId: (json['transactionId'] ?? transactionId)?.toString(),
      paidAtUtc: json['paidAtUtc']?.toString(),
    );
  }
}

class CustomerGoldRateDto {
  final double rate24KPerGram;
  final double rate22KPerGram;
  final String currency;
  final String? rateAsOf;
  final String? rateSource;
  final bool isLiveFromBackend;
  final bool isOverridden;
  final String? branchId;
  final String? branchName;

  const CustomerGoldRateDto({
    required this.rate24KPerGram,
    required this.rate22KPerGram,
    this.currency = 'LKR',
    this.rateAsOf,
    this.rateSource,
    this.isLiveFromBackend = true,
    this.isOverridden = false,
    this.branchId,
    this.branchName,
  });

  factory CustomerGoldRateDto.fromJson(Map<String, dynamic> json) {
    final rate24k =
        (json['rate24kPerGram'] as num?)?.toDouble() ??
        (json['rate24KPerGram'] as num?)?.toDouble() ??
        (json['ratePerGram'] as num?)?.toDouble() ??
        0.0;

    final rate22k =
        (json['rate22kPerGram'] as num?)?.toDouble() ??
        (json['rate22KPerGram'] as num?)?.toDouble() ??
        (rate24k > 0 ? (rate24k * (22.0 / 24.0)) : 0.0);

    return CustomerGoldRateDto(
      rate24KPerGram: rate24k,
      rate22KPerGram: rate22k,
      currency: (json['currency'] as String?) ?? 'LKR',
      rateAsOf:
          (json['effectiveFromUtc'] ?? json['rateAsOf'] ?? json['createdAtUtc'])
              ?.toString(),
      rateSource:
          (json['source'] ?? json['rateSource'])?.toString() ??
          (json['isOverridden'] == true
              ? 'Store Override'
              : 'Saranga Jewels Rate'),
      isLiveFromBackend: true,
      isOverridden: json['isOverridden'] == true,
      branchId: json['branchId']?.toString(),
      branchName: json['branchName']?.toString(),
    );
  }

  double get sovereign22KRate => rate22KPerGram * 8.0;
  double get sovereign24KRate => rate24KPerGram * 8.0;
}
