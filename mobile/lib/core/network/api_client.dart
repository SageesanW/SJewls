import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import '../constants/api_constants.dart';
import 'token_storage.dart';

class ApiClient {
  late final Dio dio;
  final TokenStorage _tokenStorage;
  final VoidCallback? onUnauthorizedSessionExpired;

  ApiClient({TokenStorage? tokenStorage, this.onUnauthorizedSessionExpired})
    : _tokenStorage = tokenStorage ?? const TokenStorage() {
    dio = Dio(
      BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: const Duration(seconds: 30),
        receiveTimeout: const Duration(seconds: 150),
        sendTimeout: const Duration(seconds: 30),
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
      ),
    );

    dio.interceptors.add(
      QueuedInterceptorsWrapper(
        onRequest: (options, handler) async {
          final isPublicAuthPath =
              options.path.startsWith('/api/v1/auth/otp/') ||
              options.path == '/api/v1/auth/check' ||
              options.path == '/api/v1/auth/registration/complete' ||
              options.path == '/api/v1/auth/token/refresh';

          if (!isPublicAuthPath) {
            final token = await _tokenStorage.getAccessToken();
            if (token != null && token.isNotEmpty) {
              options.headers['Authorization'] = 'Bearer $token';
            }
          }
          handler.next(options);
        },
        onError: (DioException error, ErrorInterceptorHandler handler) async {
          final statusCode = error.response?.statusCode;
          final requestOptions = error.requestOptions;
          final isAuthEndpoint = requestOptions.path.startsWith(
            '/api/v1/auth/',
          );

          if (statusCode == 401 &&
              !isAuthEndpoint &&
              requestOptions.extra['retried_after_refresh'] != true) {
            final refreshToken = await _tokenStorage.getRefreshToken();
            if (refreshToken != null && refreshToken.isNotEmpty) {
              try {
                final refreshDio = Dio(
                  BaseOptions(
                    baseUrl: ApiConstants.baseUrl,
                    headers: {
                      'Content-Type': 'application/json',
                      'Accept': 'application/json',
                    },
                  ),
                );
                final refreshResponse = await refreshDio.post(
                  '/api/v1/auth/token/refresh',
                  data: {'refreshToken': refreshToken},
                );
                final data = refreshResponse.data;
                if (data is Map &&
                    data['accessToken'] is String &&
                    (data['accessToken'] as String).isNotEmpty) {
                  final newAccess = data['accessToken'] as String;
                  final newRefresh =
                      (data['refreshToken'] as String?) ?? refreshToken;
                  await _tokenStorage.saveSession(
                    accessToken: newAccess,
                    refreshToken: newRefresh,
                  );

                  requestOptions.headers['Authorization'] = 'Bearer $newAccess';
                  requestOptions.extra['retried_after_refresh'] = true;
                  final retryResponse = await dio.fetch(requestOptions);
                  return handler.resolve(retryResponse);
                }
              } catch (_) {
                await _tokenStorage.clearSession();
                onUnauthorizedSessionExpired?.call();
              }
            } else {
              await _tokenStorage.clearSession();
              onUnauthorizedSessionExpired?.call();
            }
          }

          handler.next(error);
        },
      ),
    );

    if (kDebugMode) {
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            debugPrint('--> ${options.method} ${options.path}');
            handler.next(options);
          },
          onResponse: (response, handler) {
            debugPrint(
              '<-- ${response.statusCode} ${response.requestOptions.path}',
            );
            handler.next(response);
          },
          onError: (error, handler) {
            debugPrint(
              '<-- ERROR ${error.response?.statusCode} ${error.requestOptions.path}: ${error.response?.data}',
            );
            handler.next(error);
          },
        ),
      );
    }
  }

  /// Extracts a clean, user-friendly error message from a DioException or generic error
  static String extractErrorMessage(
    Object error, {
    String fallback = 'Something went wrong. Please try again.',
  }) {
    if (error is DioException) {
      final data = error.response?.data;
      if (data is Map) {
        if (data['message'] is String &&
            (data['message'] as String).trim().isNotEmpty) {
          return (data['message'] as String).trim();
        }
        if (data['detail'] is String &&
            (data['detail'] as String).trim().isNotEmpty) {
          return (data['detail'] as String).trim();
        }
        if (data['errors'] is Map) {
          final errorsMap = data['errors'] as Map;
          for (final entry in errorsMap.values) {
            if (entry is List && entry.isNotEmpty) {
              return entry.first.toString();
            } else if (entry is String && entry.isNotEmpty) {
              return entry;
            }
          }
        }
        if (data['title'] is String &&
            (data['title'] as String).trim().isNotEmpty) {
          return (data['title'] as String).trim();
        }
      }

      if (error.type == DioExceptionType.connectionTimeout ||
          error.type == DioExceptionType.receiveTimeout ||
          error.type == DioExceptionType.sendTimeout) {
        return 'Server is taking too long to respond. Please try again.';
      }
      if (error.type == DioExceptionType.connectionError) {
        return 'Unable to connect to the server. Please check your internet connection.';
      }
      if (error.response?.statusCode == 401) {
        return 'Your session has expired. Please log in again.';
      }
      if (error.response?.statusCode == 403) {
        return 'You do not have permission to perform this action.';
      }
      if (error.response?.statusCode == 404) {
        return 'The requested record could not be found.';
      }
    }

    final text = error.toString().replaceFirst('Exception: ', '').trim();
    return text.isNotEmpty ? text : fallback;
  }
}
