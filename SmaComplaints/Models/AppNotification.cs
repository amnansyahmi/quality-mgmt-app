namespace SmaComplaints.Models;
public sealed class AppNotification
{
 public Guid Id{get;set;}=Guid.NewGuid(); public Guid RecipientId{get;set;} public string EventKey{get;set;}="";
 public string Title{get;set;}=""; public string Body{get;set;}=""; public string Url{get;set;}="";
 public DateTimeOffset At{get;set;}=DateTimeOffset.UtcNow; public DateTimeOffset? ReadAt{get;set;}
}
public sealed record NotificationFeed(int Unread,List<AppNotification> Items){public DateTimeOffset Through{get;init;}=DateTimeOffset.UtcNow;}
public sealed record NotificationDelivery(Guid? RecipientId,string? RecipientRole,Guid ActorId,string EventKey,string Title,string Body,string Url,DateTimeOffset At);
