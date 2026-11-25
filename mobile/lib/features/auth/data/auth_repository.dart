import 'package:dio/dio.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/token_storage.dart';

class AuthRepository {
  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;

  AuthRepository(this._apiClient, {TokenStorage? tokenStorage})
    : _tokenStorage = tokenStorage ?? const TokenStorage();

  // 1. Request OTP
  Future<Map<String, dynamic>> requestOtp(String contact) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/auth/otp/request',
        data: {'contact': contact},
      );
      return response.data as Map<String, dynamic>;
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to send OTP. Please try again.',
        ),
      );
    }
  }

  // 2. Verify OTP
  Future<Map<String, dynamic>> verifyOtp({
    required String contact,
    required String code,
  }) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/auth/otp/verify',
        data: {'contact': contact, 'code': code},
      );
      final data = Map<String, dynamic>.from(response.data as Map);
      await _persistTokensIfPresent(data);
      return data;
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Invalid OTP. Please try again.',
        ),
      );
    }
  }

  // 3. Complete Registration
  Future<Map<String, dynamic>> completeRegistration({
    required String registrationToken,
    required String fullName,
    required String dateOfBirth,
    String? nic,
    String? email,
    String? phoneNumber,
    String? additionalContact,
  }) async {
    try {
      final response = await _apiClient.dio.post(
        '/api/v1/auth/registration/complete',
        data: {
          'registrationToken': registrationToken,
          'fullName': fullName,
          'dateOfBirth': dateOfBirth,
          'nic': nic,
          'email': email,
          'phoneNumber': phoneNumber,
          'additionalContact': additionalContact,
        },
      );
      final data = Map<String, dynamic>.from(response.data as Map);
      await _persistTokensIfPresent(data);
      return data;
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to complete registration.',
        ),
      );
    }
  }

  // 4. Logout
  Future<void> logout() async {
    final refreshToken = await _tokenStorage.getRefreshToken();
    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        await _apiClient.dio.post(
          '/api/v1/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      } catch (_) {
        // Proceed with local token cleanup even if network call fails
      }
    }
    await _tokenStorage.clearSession();
  }

  Future<void> _persistTokensIfPresent(Map<String, dynamic> data) async {
    final accessToken = data['accessToken'];
    if (accessToken is String && accessToken.isNotEmpty) {
      final refreshToken = (data['refreshToken'] as String?) ?? '';
      final customer = data['customer'] is Map
          ? Map<String, dynamic>.from(data['customer'] as Map)
          : null;
      await _tokenStorage.saveSession(
        accessToken: accessToken,
        refreshToken: refreshToken,
        customer: customer,
      );
    }
  }
}
