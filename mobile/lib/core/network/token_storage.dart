import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  static const String _accessTokenKey = 'sjewls_access_token';
  static const String _refreshTokenKey = 'sjewls_refresh_token';
  static const String _customerCacheKey = 'sjewls_customer_summary';

  final FlutterSecureStorage _secureStorage;

  // In-memory cache & fallback for test environments
  static bool _inMemoryOnly = false;
  static String? _cachedAccessToken;
  static String? _cachedRefreshToken;
  static Map<String, dynamic>? _cachedCustomer;

  const TokenStorage([this._secureStorage = const FlutterSecureStorage()]);

  static void useInMemoryOnly([bool value = true]) {
    _inMemoryOnly = value;
  }

  String? get cachedAccessToken => _cachedAccessToken;
  String? get cachedRefreshToken => _cachedRefreshToken;
  Map<String, dynamic>? get cachedCustomer => _cachedCustomer;

  Future<void> saveSession({
    required String accessToken,
    required String refreshToken,
    Map<String, dynamic>? customer,
  }) async {
    _cachedAccessToken = accessToken;
    _cachedRefreshToken = refreshToken;
    if (customer != null) {
      _cachedCustomer = Map<String, dynamic>.from(customer);
    }
    if (_inMemoryOnly) return;
    try {
      await _secureStorage.write(key: _accessTokenKey, value: accessToken);
      await _secureStorage.write(key: _refreshTokenKey, value: refreshToken);
      if (customer != null) {
        await _secureStorage.write(
          key: _customerCacheKey,
          value: jsonEncode(customer),
        );
      }
    } catch (_) {
      // Fallback to in-memory cache in widget test environments
    }
  }

  Future<String?> getAccessToken() async {
    if (_cachedAccessToken != null && _cachedAccessToken!.isNotEmpty) {
      return _cachedAccessToken;
    }
    if (_inMemoryOnly) return _cachedAccessToken;
    try {
      _cachedAccessToken = await _secureStorage.read(key: _accessTokenKey);
    } catch (_) {}
    return _cachedAccessToken;
  }

  Future<String?> getRefreshToken() async {
    if (_cachedRefreshToken != null && _cachedRefreshToken!.isNotEmpty) {
      return _cachedRefreshToken;
    }
    if (_inMemoryOnly) return _cachedRefreshToken;
    try {
      _cachedRefreshToken = await _secureStorage.read(key: _refreshTokenKey);
    } catch (_) {}
    return _cachedRefreshToken;
  }

  Future<Map<String, dynamic>?> getCustomerSummary() async {
    if (_cachedCustomer != null) return _cachedCustomer;
    if (_inMemoryOnly) return _cachedCustomer;
    try {
      final raw = await _secureStorage.read(key: _customerCacheKey);
      if (raw != null && raw.isNotEmpty) {
        final decoded = jsonDecode(raw);
        if (decoded is Map<String, dynamic>) {
          _cachedCustomer = decoded;
        }
      }
    } catch (_) {}
    return _cachedCustomer;
  }

  Future<void> clearSession() async {
    _cachedAccessToken = null;
    _cachedRefreshToken = null;
    _cachedCustomer = null;
    if (_inMemoryOnly) return;
    try {
      await _secureStorage.delete(key: _accessTokenKey);
      await _secureStorage.delete(key: _refreshTokenKey);
      await _secureStorage.delete(key: _customerCacheKey);
    } catch (_) {}
  }
}
