using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class CustomerNotificationService : ICustomerNotificationService
{
    private readonly SJewlsDbContext _db;
    private readonly ILogger<CustomerNotificationService> _logger;

    public CustomerNotificationService(
        SJewlsDbContext db,
        ILogger<CustomerNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerNotificationsPagedResponse> GetNotificationsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Notifications
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);
        var unreadCount = await _db.Notifications.CountAsync(n => n.CustomerId == customerId && !n.IsRead, ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(n => new CustomerNotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            ReadAtUtc = n.ReadAtUtc,
            CreatedAtUtc = n.CreatedAtUtc
        }).ToList();

        return new CustomerNotificationsPagedResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> MarkAsReadAsync(Guid customerId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.CustomerId == customerId, ct);

        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid customerId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.CustomerId == customerId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task CreateNotificationAsync(
        Guid customerId,
        string title,
        string message,
        string type,
        CancellationToken ct = default)
    {
        // Deduplication window: avoid duplicate notifications created within the last 10 minutes
        var recentCutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        var isDuplicate = await _db.Notifications
            .AnyAsync(n => n.CustomerId == customerId &&
                           n.Title == title &&
                           n.Message == message &&
                           n.CreatedAtUtc >= recentCutoff, ct);

        if (isDuplicate)
        {
            _logger.LogInformation("Duplicate notification prevented for customer {CustomerId}: {Title}", customerId, title);
            return;
        }

        var notification = new Notification
        {
            CustomerId = customerId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
    }
}
