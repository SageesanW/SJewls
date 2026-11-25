import 'package:dio/dio.dart';
import '../../../core/network/api_client.dart';
import 'notification_models.dart';

class NotificationRepository {
  final ApiClient _apiClient;

  NotificationRepository(this._apiClient);

  /// GET /api/v1/customer/notifications
  Future<CustomerNotificationsPagedResponse> getNotifications({
    int page = 1,
    int pageSize = 30,
  }) async {
    try {
      final response = await _apiClient.dio.get(
        '/api/v1/customer/notifications',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return CustomerNotificationsPagedResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to load notifications.',
        ),
      );
    }
  }

  /// POST /api/v1/customer/notifications/{notificationId}/read
  Future<void> markAsRead(String notificationId) async {
    try {
      await _apiClient.dio.post(
        '/api/v1/customer/notifications/$notificationId/read',
      );
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to mark notification as read.',
        ),
      );
    }
  }

  /// POST /api/v1/customer/notifications/read-all
  Future<void> markAllAsRead() async {
    try {
      await _apiClient.dio.post('/api/v1/customer/notifications/read-all');
    } on DioException catch (e) {
      throw Exception(
        ApiClient.extractErrorMessage(
          e,
          fallback: 'Failed to mark all notifications as read.',
        ),
      );
    }
  }
}
