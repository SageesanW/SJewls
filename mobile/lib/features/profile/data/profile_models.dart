class CustomerProfileDto {
  final String id;
  final String? fullName;
  final String? dateOfBirth;
  final String? nic;
  final String? phoneNumber;
  final String? email;
  final bool isPhoneVerified;
  final bool isEmailVerified;
  final String? phoneVerifiedAtUtc;
  final String? emailVerifiedAtUtc;
  final String primaryBranchId;
  final String? primaryBranchCode;
  final String? primaryBranchName;
  final bool isProfileComplete;
  final String? createdAtUtc;

  const CustomerProfileDto({
    required this.id,
    this.fullName,
    this.dateOfBirth,
    this.nic,
    this.phoneNumber,
    this.email,
    required this.isPhoneVerified,
    required this.isEmailVerified,
    this.phoneVerifiedAtUtc,
    this.emailVerifiedAtUtc,
    required this.primaryBranchId,
    this.primaryBranchCode,
    this.primaryBranchName,
    required this.isProfileComplete,
    this.createdAtUtc,
  });

  factory CustomerProfileDto.fromJson(Map<String, dynamic> json) {
    return CustomerProfileDto(
      id: (json['id'] ?? '').toString(),
      fullName: json['fullName'] as String?,
      dateOfBirth: json['dateOfBirth'] as String?,
      nic: json['nic'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      email: json['email'] as String?,
      isPhoneVerified: json['isPhoneVerified'] == true,
      isEmailVerified: json['isEmailVerified'] == true,
      phoneVerifiedAtUtc: json['phoneVerifiedAtUtc'] as String?,
      emailVerifiedAtUtc: json['emailVerifiedAtUtc'] as String?,
      primaryBranchId: (json['primaryBranchId'] ?? '').toString(),
      primaryBranchCode: json['primaryBranchCode'] as String?,
      primaryBranchName: json['primaryBranchName'] as String?,
      isProfileComplete: json['isProfileComplete'] == true,
      createdAtUtc: json['createdAtUtc'] as String?,
    );
  }

  bool get hasUnverifiedPhone =>
      phoneNumber != null && phoneNumber!.trim().isNotEmpty && !isPhoneVerified;

  bool get hasUnverifiedEmail =>
      email != null && email!.trim().isNotEmpty && !isEmailVerified;
}

class RequestSecondaryOtpResult {
  final bool success;
  final String? message;
  final String? normalizedContact;
  final int expiresInSeconds;
  final int cooldownSeconds;
  final String? devOtp;

  const RequestSecondaryOtpResult({
    required this.success,
    this.message,
    this.normalizedContact,
    required this.expiresInSeconds,
    required this.cooldownSeconds,
    this.devOtp,
  });

  factory RequestSecondaryOtpResult.fromJson(Map<String, dynamic> json) {
    return RequestSecondaryOtpResult(
      success: json['success'] ?? true,
      message: json['message'] as String?,
      normalizedContact: json['normalizedContact'] as String?,
      expiresInSeconds: (json['expiresInSeconds'] as num?)?.toInt() ?? 300,
      cooldownSeconds: (json['cooldownSeconds'] as num?)?.toInt() ?? 60,
      devOtp: json['devOtp'] as String?,
    );
  }
}

class RequestClosureOtpResult {
  final bool success;
  final String? message;
  final String? deliveryChannel;
  final String? maskedContact;
  final int expiresInSeconds;
  final int cooldownSeconds;
  final String? devOtp;

  const RequestClosureOtpResult({
    required this.success,
    this.message,
    this.deliveryChannel,
    this.maskedContact,
    required this.expiresInSeconds,
    required this.cooldownSeconds,
    this.devOtp,
  });

  factory RequestClosureOtpResult.fromJson(Map<String, dynamic> json) {
    return RequestClosureOtpResult(
      success: json['success'] ?? true,
      message: json['message'] as String?,
      deliveryChannel: json['deliveryChannel'] as String?,
      maskedContact: json['maskedContact'] as String?,
      expiresInSeconds: (json['expiresInSeconds'] as num?)?.toInt() ?? 300,
      cooldownSeconds: (json['cooldownSeconds'] as num?)?.toInt() ?? 60,
      devOtp: json['devOtp'] as String?,
    );
  }
}

class AccountClosureResult {
  final bool success;
  final String? message;
  final String? closedAtUtc;
  final String? status;

  const AccountClosureResult({
    required this.success,
    this.message,
    this.closedAtUtc,
    this.status,
  });

  factory AccountClosureResult.fromJson(Map<String, dynamic> json) {
    return AccountClosureResult(
      success: json['success'] ?? true,
      message: json['message'] as String?,
      closedAtUtc: json['closedAtUtc'] as String?,
      status: json['status'] as String?,
    );
  }
}
