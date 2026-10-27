using System.Net;
using System.Net.Mail;
using Microsoft.Data.SqlClient;
namespace SmaComplaints.Services;

public sealed class MailWorker(IConfiguration config,ILogger<MailWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!config.GetValue<bool>("Mail:Enabled"))return;
        while(!stoppingToken.IsCancellationRequested)
        {
            try {await Deliver(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}catch(Exception ex){log.LogError(ex,"Complaint mail worker failed. Pending notifications remain in the SQL outbox.");}
            await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);
        }
    }
    private async Task Deliver(CancellationToken token)
    {
        await using var db=new SqlConnection(config.GetConnectionString("Complaints"));await db.OpenAsync(token);
        // One reminder per recipient / complaint / Malaysia calendar date. Range locks serialize competing app instances.
        await using(var remind=db.CreateCommand())
        {
            remind.CommandText="""
                INSERT INTO dbo.MailOutbox(Id,DedupKey,Recipient,Subject,Body,CreatedAt,AvailableAt,Attempts)
                SELECT NEWID(),k.DedupKey,u.Email,CONCAT(c.Reference,': overdue task'),CONCAT('Your complaint task is overdue. Reference: ',c.Reference,CHAR(10),'Record ID: ',CONVERT(varchar(36),c.Id)),SYSUTCDATETIME(),SYSUTCDATETIME(),0
                FROM dbo.Complaints c JOIN dbo.AppUsers u ON u.Active=1 AND
                (u.Id=CASE WHEN c.Stage='Draft' THEN c.CreatedBy WHEN c.Stage IN ('Investigation','Actions') THEN c.OwnerId WHEN c.Stage IN ('PlanHod','CompletionHod') THEN c.HodId WHEN c.Stage='ClosureCm' THEN c.CmId ELSE c.QmId END OR (c.Stage='Evaluation' AND c.QmId IS NULL AND u.Role='QM'))
                CROSS APPLY(SELECT CONCAT(CONVERT(varchar(36),c.Id),'-',CONVERT(varchar(36),u.Id),'-due-',CONVERT(char(8),DATEADD(hour,8,SYSUTCDATETIME()),112)) AS DedupKey)k
                WHERE c.Stage NOT IN ('Draft','Closed') AND c.DueAt<SYSUTCDATETIME()
                AND NOT EXISTS(SELECT 1 FROM dbo.MailOutbox o WITH(UPDLOCK,HOLDLOCK) WHERE o.DedupKey=k.DedupKey);
                """;
            await remind.ExecuteNonQueryAsync(token);
        }
        var lease=Guid.NewGuid();var items=new List<(Guid Id,string To,string Subject,string Body)>();
        await using(var claim=db.CreateCommand())
        {
            claim.CommandText="UPDATE TOP(10) dbo.MailOutbox WITH(UPDLOCK,READPAST,ROWLOCK) SET LeaseId=@Lease,LockedUntil=DATEADD(minute,5,SYSUTCDATETIME()),Attempts=Attempts+1 OUTPUT inserted.Id,inserted.Recipient,inserted.Subject,inserted.Body WHERE SentAt IS NULL AND AvailableAt<=SYSUTCDATETIME() AND (LockedUntil IS NULL OR LockedUntil<SYSUTCDATETIME()) AND Attempts<20";
            claim.Parameters.AddWithValue("@Lease",lease);await using var r=await claim.ExecuteReaderAsync(token);while(await r.ReadAsync(token))items.Add((r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3)));
        }
        foreach(var item in items)
        {
            string? error=null;
            try
            {
                using var client=new SmtpClient(config["Mail:Host"],config.GetValue<int?>("Mail:Port")??587){EnableSsl=true,Timeout=30000};
                if(!string.IsNullOrWhiteSpace(config["Mail:Username"]))client.Credentials=new NetworkCredential(config["Mail:Username"],config["Mail:Password"]);
                var link=(config["Mail:BaseUrl"]??"").TrimEnd('/')+"/Complaints?view=inbox";
                using var message=new MailMessage(config["Mail:From"]!,item.To,item.Subject,item.Body+"\n\n"+link);
                await client.SendMailAsync(message,token);
            }
            catch(Exception ex)when(ex is not OperationCanceledException){error=ex.Message[..Math.Min(ex.Message.Length,1000)];log.LogWarning("Could not deliver outbox notification {Id}. It remains queued for retry.",item.Id);}
            await using var result=db.CreateCommand();
            result.CommandText=error==null?"UPDATE dbo.MailOutbox SET SentAt=SYSUTCDATETIME(),LockedUntil=NULL,LeaseId=NULL,LastError=NULL WHERE Id=@Id AND LeaseId=@Lease":"UPDATE dbo.MailOutbox SET AvailableAt=DATEADD(minute,15,SYSUTCDATETIME()),LockedUntil=NULL,LeaseId=NULL,LastError=@Error WHERE Id=@Id AND LeaseId=@Lease";
            result.Parameters.AddWithValue("@Id",item.Id);result.Parameters.AddWithValue("@Lease",lease);if(error!=null)result.Parameters.AddWithValue("@Error",error);await result.ExecuteNonQueryAsync(token);
        }
    }
}
