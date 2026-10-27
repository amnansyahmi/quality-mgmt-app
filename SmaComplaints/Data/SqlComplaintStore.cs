using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using SmaComplaints.Models;
namespace SmaComplaints.Data;

public sealed class SqlComplaintStore(IConfiguration configuration) : IComplaintStore
{
    private SqlConnection Connection() => new(configuration.GetConnectionString("Complaints") ?? throw new InvalidOperationException("Set ConnectionStrings__Complaints to your SQL Server connection string."));
    private static void Param(SqlCommand cmd, string name, object? value) => cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static string Payload(Complaint c)
    {
        var json = JsonSerializer.SerializeToNode(c)!;
        json[nameof(Complaint.History)] = null;
        json[nameof(Complaint.Attachments)] = null;
        return json.ToJsonString();
    }
    public async Task<List<Complaint>> ListAsync(AppUser user)
    {
        await using var db = Connection(); await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT TOP (500) c.Payload, c.Revision, a.Payload FROM dbo.Complaints c OUTER APPLY (SELECT TOP (1) e.Payload FROM dbo.AuditEvents e WHERE e.ComplaintId=c.Id AND JSON_VALUE(e.Payload,'$.FromStage')<>JSON_VALUE(e.Payload,'$.ToStage') ORDER BY e.At DESC) a WHERE @Role='Admin' OR CreatedBy=@UserId OR (Stage<>'Draft' AND (@Role='QM' OR OwnerId=@UserId OR HodId=@UserId OR CmId=@UserId)) ORDER BY c.UpdatedAt DESC";
        Param(cmd,"@Role",user.Role); Param(cmd,"@UserId",user.Id);
        var result = new List<Complaint>(); await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { var c = JsonSerializer.Deserialize<Complaint>(reader.GetString(0))!; c.Revision = reader.GetInt32(1); c.History = reader.IsDBNull(2)?[]:[JsonSerializer.Deserialize<AuditEntry>(reader.GetString(2))!]; c.Attachments = []; result.Add(c); }
        return result;
    }
    public async Task<Complaint?> GetAsync(Guid id)
    {
        await using var db = Connection(); await db.OpenAsync();
        await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT Payload, Revision FROM dbo.Complaints WHERE Id=@Id; SELECT Payload FROM dbo.AuditEvents WHERE ComplaintId=@Id ORDER BY At; SELECT Id,Name,Size,ContentType,UploadedBy,UploadedAt FROM dbo.Attachments WHERE ComplaintId=@Id ORDER BY UploadedAt"; Param(cmd,"@Id",id);
        await using var r = await cmd.ExecuteReaderAsync(); if (!await r.ReadAsync()) return null;
        var c = JsonSerializer.Deserialize<Complaint>(r.GetString(0))!; c.Revision = r.GetInt32(1); c.History = []; c.Attachments = [];
        await r.NextResultAsync(); while (await r.ReadAsync()) c.History.Add(JsonSerializer.Deserialize<AuditEntry>(r.GetString(0))!);
        await r.NextResultAsync(); while (await r.ReadAsync()) c.Attachments.Add(new AttachmentInfo { Id=r.GetGuid(0),Name=r.GetString(1),Size=r.GetInt64(2),ContentType=r.GetString(3),UploadedBy=r.GetGuid(4),UploadedAt=r.GetFieldValue<DateTimeOffset>(5) });
        return c;
    }
    public async Task SaveAsync(Complaint c, int expectedRevision, AuditEntry entry, StoredAttachment? attachment = null)
    {
        await using var db = Connection(); await db.OpenAsync(); await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
        Complaint? before=null;
        if(expectedRevision>0){await using var previous=db.CreateCommand();previous.Transaction=tx;previous.CommandText="SELECT Payload FROM dbo.Complaints WHERE Id=@Id";Param(previous,"@Id",c.Id);var saved=await previous.ExecuteScalarAsync() as string;if(saved is not null)before=JsonSerializer.Deserialize<Complaint>(saved);}
        c.Revision = expectedRevision + 1; c.UpdatedAt = DateTimeOffset.UtcNow;
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = expectedRevision == 0
            ? "INSERT INTO dbo.Complaints(Id,Reference,Subject,CustomerName,Stage,CreatedBy,OwnerId,HodId,QmId,CmId,DueAt,CreatedAt,UpdatedAt,Revision,Payload) VALUES(@Id,@Ref,@Subject,@Customer,@Stage,@By,@Owner,@Hod,@Qm,@Cm,@Due,@Created,@Updated,@Revision,@Payload)"
            : "UPDATE dbo.Complaints SET Subject=@Subject,CustomerName=@Customer,Stage=@Stage,OwnerId=@Owner,HodId=@Hod,QmId=@Qm,CmId=@Cm,DueAt=@Due,UpdatedAt=@Updated,Revision=@Revision,Payload=@Payload WHERE Id=@Id AND Revision=@Expected";
        Param(cmd,"@Id",c.Id); Param(cmd,"@Ref",c.Reference); Param(cmd,"@Subject",c.Subject); Param(cmd,"@Customer",c.CustomerName); Param(cmd,"@Stage",c.Stage.ToString()); Param(cmd,"@By",c.CreatedBy);
        Param(cmd,"@Owner",c.OwnerId); Param(cmd,"@Hod",c.HodId); Param(cmd,"@Qm",c.QmId); Param(cmd,"@Cm",c.CmId); Param(cmd,"@Due",c.DueAt); Param(cmd,"@Created",c.CreatedAt); Param(cmd,"@Updated",c.UpdatedAt); Param(cmd,"@Revision",c.Revision); Param(cmd,"@Payload",Payload(c)); Param(cmd,"@Expected",expectedRevision);
        if (await cmd.ExecuteNonQueryAsync() != 1) throw new ConcurrencyConflictException();
        await using var audit = db.CreateCommand(); audit.Transaction = tx; audit.CommandText="INSERT INTO dbo.AuditEvents(Id,ComplaintId,At,ActorId,Action,Payload) VALUES(@Id,@Complaint,@At,@Actor,@Action,@Payload)";
        Param(audit,"@Id",entry.Id); Param(audit,"@Complaint",c.Id); Param(audit,"@At",entry.At); Param(audit,"@Actor",entry.ActorId); Param(audit,"@Action",entry.Action); Param(audit,"@Payload",JsonSerializer.Serialize(entry)); await audit.ExecuteNonQueryAsync();
        if(entry.FromStage!=entry.ToStage || entry.Action.Contains("request",StringComparison.OrdinalIgnoreCase) || entry.Action=="Reporting review confirmed" || entry.Action.Contains("approved",StringComparison.OrdinalIgnoreCase) || entry.Action.Contains("declined",StringComparison.OrdinalIgnoreCase))
        {
            var target = entry.Action.Contains("requested",StringComparison.OrdinalIgnoreCase)?c.HodId:
                entry.Action=="Reporting review confirmed" || entry.Action.StartsWith("delay ") || entry.Action.StartsWith("extension ")?c.OwnerId:
                c.Stage==Stage.Closed?c.CreatedBy:new SmaComplaints.Services.Workflow().Assignee(c);
            await using var mail=db.CreateCommand();mail.Transaction=tx;
            mail.CommandText="INSERT INTO dbo.MailOutbox(Id,DedupKey,Recipient,Subject,Body,CreatedAt,AvailableAt,Attempts) SELECT NEWID(),CONCAT(@Event,'-',CONVERT(varchar(36),Id)),Email,@Subject,@Body,SYSUTCDATETIME(),SYSUTCDATETIME(),0 FROM dbo.AppUsers WHERE Active=1 AND (Id=@Target OR (@AllQm=1 AND Role='QM'))";
            Param(mail,"@Event",entry.Id.ToString());Param(mail,"@Target",target);Param(mail,"@AllQm",c.Stage==Stage.Evaluation&&target==null);Param(mail,"@Subject",$"{c.Reference}: {entry.Action}");Param(mail,"@Body",$"Complaint: {c.Reference}\nActivity: {entry.Action}\nCurrent stage: {Ui.StageName(c.Stage)}\nOpen the complaint workspace to review the record.\nRecord ID: {c.Id}");await mail.ExecuteNonQueryAsync();
        }
        if (attachment != null)
        {
            await using var upload = db.CreateCommand(); upload.Transaction = tx; upload.CommandText="INSERT INTO dbo.Attachments(Id,ComplaintId,Name,Size,ContentType,UploadedBy,UploadedAt,Content) VALUES(@Id,@Complaint,@Name,@Size,@Type,@By,@At,@Content)";
            var a=attachment.Info; Param(upload,"@Id",a.Id); Param(upload,"@Complaint",c.Id); Param(upload,"@Name",a.Name); Param(upload,"@Size",a.Size); Param(upload,"@Type",a.ContentType); Param(upload,"@By",a.UploadedBy); Param(upload,"@At",a.UploadedAt); upload.Parameters.Add("@Content",SqlDbType.VarBinary,-1).Value=attachment.Bytes; await upload.ExecuteNonQueryAsync();
        }
        await SqlNotificationStore.InsertAsync(db,tx,SmaComplaints.Services.NotificationRouting.Complaint(c,entry,before));
        await tx.CommitAsync();
    }
    public async Task<StoredAttachment?> AttachmentAsync(Guid id)
    {
        await using var db=Connection(); await db.OpenAsync(); await using var cmd=db.CreateCommand(); cmd.CommandText="SELECT ComplaintId,Name,Size,ContentType,UploadedBy,UploadedAt,Content FROM dbo.Attachments WHERE Id=@Id"; Param(cmd,"@Id",id);
        await using var r=await cmd.ExecuteReaderAsync(); return !await r.ReadAsync()?null:new StoredAttachment(r.GetGuid(0),new AttachmentInfo{Id=id,Name=r.GetString(1),Size=r.GetInt64(2),ContentType=r.GetString(3),UploadedBy=r.GetGuid(4),UploadedAt=r.GetFieldValue<DateTimeOffset>(5)},(byte[])r[6]);
    }
    private static AppUser ReadUser(SqlDataReader r)=>new(){Id=r.GetGuid(0),Name=r.GetString(1),Email=r.GetString(2),Role=r.GetString(3),Department=r.GetString(4),PasswordHash=r.GetString(5),Active=r.GetBoolean(6)};
    public async Task<List<AppUser>> UsersAsync()
    {
        await using var db=Connection();await db.OpenAsync();await using var cmd=db.CreateCommand();cmd.CommandText="SELECT Id,Name,Email,Role,Department,PasswordHash,Active FROM dbo.AppUsers ORDER BY Name";await using var r=await cmd.ExecuteReaderAsync();var result=new List<AppUser>();while(await r.ReadAsync()) result.Add(ReadUser(r));return result;
    }
    public async Task<AppUser?> UserAsync(Guid id)=>await FindUser("Id",id);
    public async Task<AppUser?> UserByEmailAsync(string email)=>await FindUser("Email",email.Trim().ToLowerInvariant());
    private async Task<AppUser?> FindUser(string column, object value)
    {
        await using var db=Connection();await db.OpenAsync();await using var cmd=db.CreateCommand();cmd.CommandText=$"SELECT Id,Name,Email,Role,Department,PasswordHash,Active FROM dbo.AppUsers WHERE {column}=@Value";Param(cmd,"@Value",value);await using var r=await cmd.ExecuteReaderAsync();return await r.ReadAsync()?ReadUser(r):null;
    }
    public async Task SaveUserAsync(AppUser u)
    {
        await using var db=Connection();await db.OpenAsync();await using var cmd=db.CreateCommand();cmd.CommandText="UPDATE dbo.AppUsers SET Name=@Name,Email=@Email,Role=@Role,Department=@Dept,PasswordHash=@Hash,Active=@Active WHERE Id=@Id; IF @@ROWCOUNT=0 INSERT INTO dbo.AppUsers(Id,Name,Email,Role,Department,PasswordHash,Active) VALUES(@Id,@Name,@Email,@Role,@Dept,@Hash,@Active);";
        Param(cmd,"@Id",u.Id);Param(cmd,"@Name",u.Name);Param(cmd,"@Email",u.Email.Trim().ToLowerInvariant());Param(cmd,"@Role",u.Role);Param(cmd,"@Dept",u.Department);Param(cmd,"@Hash",u.PasswordHash);Param(cmd,"@Active",u.Active);await cmd.ExecuteNonQueryAsync();
    }
}
