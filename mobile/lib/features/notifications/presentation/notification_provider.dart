import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/presentation/auth_provider.dart';
import '../data/notification_models.dart';
import '../data/notification_repository.dart';

final notificationRepositoryProvider = Provider<NotificationRepository>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  return NotificationRepository(apiClient);
});

final notificationsProvider =
    FutureProvider.autoDispose<CustomerNotificationsPagedResponse>((ref) async {
      final repo = ref.watch(notificationRepositoryProvider);
      return repo.getNotifications(page: 1, pageSize: 50);
    });

final unreadNotificationCountProvider = Provider.autoDispose<int>((ref) {
  final asyncNotifications = ref.watch(notificationsProvider);
  return asyncNotifications.maybeWhen(
    data: (res) => res.unreadCount,
    orElse: () => 0,
  );
});
