import 'package:dio/dio.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/token_storage.dart';
import 'profile_models.dart';

class ProfileRepository {
  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;

  ProfileRepository(this._apiClient, {TokenStorage? tokenStorage})
    : _tokenStorage = tokenStorage ?? const TokenStorage();

  /// GET /api/v1/customers/me
  Future<CustomerProfileDto> getProfile() async {
    try {
      final response = await _apiClient.dio.get('/api/v1/customers/me');
      return CustomerProfileDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load customer profile.',
        ),
      );
    }
  }

  /// POST /api/v1/customers/me/contacts/otp/request
  Future<RequestSecondaryOtpResult> requestSecondaryContactOtp(
    String contact,
  ) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customers/me/contacts/otp/request',
        data: {'contact': contact},
      );
      return RequestSecondaryOtpResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to send verification code to contact.',
        ),
      );
    }
  }

  /// POST /api/v1/customers/me/contacts/otp/verify
  Future<void> verifySecondaryContactOtp({
    required String contact,
    required String code,
  }) async {
    try {
      await _apiClient.dio.post(
        '/api/v1/customers/me/contacts/otp/verify',
        data: {'contact': contact, 'code': code},
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to verify secondary contact.',
        ),
      );
    }
  }

  /// POST /api/v1/customers/me/account-closure/otp/request
  Future<RequestClosureOtpResult> requestAccountClosureOtp() async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customers/me/account-closure/otp/request',
      );
      return RequestClosureOtpResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to request account closure OTP.',
        ),
      );
    }
  }

  /// POST /api/v1/customers/me/account-closure/confirm
  Future<AccountClosureResult> confirmAccountClosure({
    required String code,
    String? reason,
  }) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/customers/me/account-closure/confirm',
        data: {
          'code': code,
          if (reason != null && reason.trim().isNotEmpty)
            'reason': reason.trim(),
        },
      );
      final result = AccountClosureResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      await _tokenStorage.clearSession();
      return result;
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to confirm account closure.',
        ),
      );
    }
  }
}
