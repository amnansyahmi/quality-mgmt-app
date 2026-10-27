using SmaComplaints.Models;
namespace SmaComplaints.Data;
public interface INotificationStore
{
 Task<NotificationFeed> FeedAsync(Guid userId);
 Task<AppNotification?> FindAsync(Guid userId,Guid id);
 Task ReadAsync(Guid userId,Guid? id,DateTimeOffset through);
}
