using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface ICustomerNotificationService
{
    Task<CustomerNotificationsPagedResponse> GetNotificationsAsync(Guid customerId, int page, int pageSize, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid customerId, Guid notificationId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid customerId, CancellationToken ct = default);
    Task CreateNotificationAsync(Guid customerId, string title, string message, string type, CancellationToken ct = default);
}
