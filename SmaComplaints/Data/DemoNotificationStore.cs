using SmaComplaints.Models;
namespace SmaComplaints.Data;
public sealed class DemoNotificationStore:INotificationStore
{
 readonly object gate=new();readonly List<AppNotification> items=[];
 public void Publish(IEnumerable<NotificationDelivery> deliveries,IEnumerable<AppUser> users)
 {
  lock(gate)foreach(var d in deliveries)foreach(var user in users.Where(u=>u.Active&&(u.Id==d.RecipientId||(d.RecipientRole!=null&&u.Role==d.RecipientRole))))
  {if(items.Any(n=>n.RecipientId==user.Id&&n.EventKey==d.EventKey))continue;items.Add(new(){RecipientId=user.Id,EventKey=d.EventKey,Title=d.Title,Body=d.Body,Url=d.Url,At=d.At});}
 }
 static AppNotification Copy(AppNotification n)=>new(){Id=n.Id,RecipientId=n.RecipientId,EventKey=n.EventKey,Title=n.Title,Body=n.Body,Url=n.Url,At=n.At,ReadAt=n.ReadAt};
 public Task<NotificationFeed> FeedAsync(Guid userId){lock(gate){var mine=items.Where(n=>n.RecipientId==userId);return Task.FromResult(new NotificationFeed(mine.Count(n=>n.ReadAt==null),mine.OrderBy(n=>n.ReadAt!=null).ThenByDescending(n=>n.At).Take(50).Select(Copy).ToList()));}}
 public Task<AppNotification?> FindAsync(Guid userId,Guid id){lock(gate)return Task.FromResult(items.Where(n=>n.Id==id&&n.RecipientId==userId).Select(Copy).FirstOrDefault());}
 public Task ReadAsync(Guid userId,Guid? id,DateTimeOffset through){lock(gate){foreach(var n in items.Where(n=>n.RecipientId==userId&&n.ReadAt==null&&n.At<=through&&(!id.HasValue||n.Id==id)))n.ReadAt=DateTimeOffset.UtcNow;return Task.CompletedTask;}}
}
