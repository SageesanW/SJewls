using SJewls.Application.DTOs;
using SJewls.Domain.Entities;

namespace SJewls.Application.Interfaces;

public interface ICustomerNotificationService
{
    Task<CustomerNotificationsPagedResponse> GetNotificationsAsync(Guid customerId, int page, int pageSize, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid customerId, Guid notificationId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid customerId, CancellationToken ct = default);
    Task CreateNotificationAsync(Guid customerId, string title, string message, string type, CancellationToken ct = default);
    Task<Notification> CreateNotificationAsync(Guid customerId, string title, string message, string type, Guid? referenceId, string? referenceType, Guid? planAlertId, CancellationToken ct = default);
}
