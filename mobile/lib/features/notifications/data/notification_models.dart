class CustomerNotificationDto {
  final String id;
  final String title;
  final String message;
  final String? type;
  final bool isRead;
  final String? readAtUtc;
  final String? createdAtUtc;

  const CustomerNotificationDto({
    required this.id,
    required this.title,
    required this.message,
    this.type,
    required this.isRead,
    this.readAtUtc,
    this.createdAtUtc,
  });

  factory CustomerNotificationDto.fromJson(Map<String, dynamic> json) {
    return CustomerNotificationDto(
      id: (json['id'] ?? '').toString(),
      title: (json['title'] as String?) ?? 'SJewls Notification',
      message: (json['message'] as String?) ?? '',
      type: json['type'] as String?,
      isRead: json['isRead'] == true,
      readAtUtc: json['readAtUtc'] as String?,
      createdAtUtc: json['createdAtUtc'] as String?,
    );
  }
}

class CustomerNotificationsPagedResponse {
  final List<CustomerNotificationDto> items;
  final int totalCount;
  final int unreadCount;
  final int page;
  final int pageSize;
  final int totalPages;

  const CustomerNotificationsPagedResponse({
    required this.items,
    required this.totalCount,
    required this.unreadCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  factory CustomerNotificationsPagedResponse.fromJson(
    Map<String, dynamic> json,
  ) {
    final rawItems = (json['items'] as List?) ?? const [];
    return CustomerNotificationsPagedResponse(
      items: rawItems
          .whereType<Map>()
          .map(
            (e) =>
                CustomerNotificationDto.fromJson(Map<String, dynamic>.from(e)),
          )
          .toList(),
      totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
      unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    );
  }
}
