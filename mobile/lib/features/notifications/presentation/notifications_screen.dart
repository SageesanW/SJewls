import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../data/notification_models.dart';
import 'notification_provider.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  bool _isMarkingAll = false;
  final Set<String> _markingIds = {};

  Future<void> _markAllAsRead() async {
    setState(() => _isMarkingAll = true);
    try {
      await ref.read(notificationRepositoryProvider).markAllAsRead();
      ref.invalidate(notificationsProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('All notifications marked as read.'),
            backgroundColor: AppColors.primary,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(e.toString().replaceFirst('Exception: ', '')),
            backgroundColor: AppColors.dark,
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isMarkingAll = false);
      }
    }
  }

  Future<void> _markSingleAsRead(CustomerNotificationDto item) async {
    if (item.isRead || _markingIds.contains(item.id)) return;
    setState(() => _markingIds.add(item.id));
    try {
      await ref.read(notificationRepositoryProvider).markAsRead(item.id);
      ref.invalidate(notificationsProvider);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(e.toString().replaceFirst('Exception: ', '')),
            backgroundColor: AppColors.dark,
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _markingIds.remove(item.id));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final notificationsAsync = ref.watch(notificationsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          notificationsAsync.maybeWhen(
            data: (paged) => paged.unreadCount > 0
                ? TextButton.icon(
                    onPressed: _isMarkingAll ? null : _markAllAsRead,
                    icon: _isMarkingAll
                        ? const SizedBox(
                            width: 14,
                            height: 14,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: AppColors.gold,
                            ),
                          )
                        : const Icon(
                            Icons.done_all_rounded,
                            color: AppColors.gold,
                            size: 18,
                          ),
                    label: const Text(
                      'Mark all read',
                      style: TextStyle(
                        color: AppColors.gold,
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  )
                : const SizedBox.shrink(),
            orElse: () => const SizedBox.shrink(),
          ),
        ],
      ),
      body: notificationsAsync.when(
        loading: () =>
            const SJewlsLoadingView(message: 'Loading your notifications...'),
        error: (err, _) => SJewlsErrorView(
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () => ref.invalidate(notificationsProvider),
        ),
        data: (paged) {
          if (paged.items.isEmpty) {
            return SJewlsEmptyView(
              icon: Icons.notifications_none_rounded,
              title: 'No Notifications Yet',
              subtitle:
                  'Plan activations, gold contributions, and branch updates will appear here.',
              actionLabel: 'Refresh',
              onAction: () => ref.invalidate(notificationsProvider),
            );
          }

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async {
              ref.invalidate(notificationsProvider);
              await ref.read(notificationsProvider.future);
            },
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: paged.items.length + 1,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                if (index == 0) {
                  return Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 14,
                      vertical: 10,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.06),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Total: ${paged.totalCount}',
                          style: const TextStyle(
                            color: AppColors.dark,
                            fontWeight: FontWeight.w600,
                            fontSize: 13,
                          ),
                        ),
                        SJewlsStatusBadge(
                          label: '${paged.unreadCount} Unread',
                          isHighlighted: paged.unreadCount > 0,
                        ),
                      ],
                    ),
                  );
                }

                final item = paged.items[index - 1];
                final isUnread = !item.isRead;
                final isBusy = _markingIds.contains(item.id);

                return InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: () => _markSingleAsRead(item),
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: AppColors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(
                        color: isUnread
                            ? AppColors.gold
                            : AppColors.dark.withValues(alpha: 0.08),
                        width: isUnread ? 1.5 : 1,
                      ),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: isUnread
                                ? AppColors.primary
                                : AppColors.primary.withValues(alpha: 0.08),
                            shape: BoxShape.circle,
                          ),
                          child: Icon(
                            _iconForType(item.type),
                            color: isUnread
                                ? AppColors.gold
                                : AppColors.primary,
                            size: 20,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      item.title,
                                      style: TextStyle(
                                        color: AppColors.dark,
                                        fontSize: 15,
                                        fontWeight: isUnread
                                            ? FontWeight.bold
                                            : FontWeight.w600,
                                      ),
                                    ),
                                  ),
                                  if (isBusy)
                                    const SizedBox(
                                      width: 14,
                                      height: 14,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: AppColors.primary,
                                      ),
                                    )
                                  else if (isUnread)
                                    Container(
                                      width: 9,
                                      height: 9,
                                      decoration: const BoxDecoration(
                                        color: AppColors.gold,
                                        shape: BoxShape.circle,
                                      ),
                                    ),
                                ],
                              ),
                              const SizedBox(height: 6),
                              Text(
                                item.message,
                                style: TextStyle(
                                  color: AppColors.dark.withValues(alpha: 0.78),
                                  fontSize: 13.5,
                                  height: 1.4,
                                ),
                              ),
                              const SizedBox(height: 8),
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Flexible(
                                    child: Text(
                                      Formatters.dateTime(item.createdAtUtc),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: TextStyle(
                                        color: AppColors.dark.withValues(
                                          alpha: 0.52,
                                        ),
                                        fontSize: 12,
                                      ),
                                    ),
                                  ),
                                  if (isUnread) ...[
                                    const SizedBox(width: 8),
                                    const Text(
                                      'Tap to mark read',
                                      style: TextStyle(
                                        color: AppColors.primary,
                                        fontSize: 11.5,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ],
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }

  IconData _iconForType(String? type) {
    final lower = (type ?? '').toLowerCase();
    if (lower.contains('pay') || lower.contains('contrib')) {
      return Icons.payments_outlined;
    }
    if (lower.contains('complete') || lower.contains('claim')) {
      return Icons.workspace_premium_outlined;
    }
    if (lower.contains('plan') || lower.contains('enrol')) {
      return Icons.diamond_outlined;
    }
    return Icons.notifications_outlined;
  }
}
