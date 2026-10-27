using System.Text.Json;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Data;

// Development preview only. Production always uses SqlComplaintStore.
public sealed class DemoStore : IComplaintStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid,Complaint> complaints=[];
    private readonly Dictionary<Guid,AppUser> users=[];
    private readonly Dictionary<Guid,StoredAttachment> attachments=[];
    private static T Copy<T>(T x)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x))!;
    private readonly DemoNotificationStore notifications;
    public DemoStore(DemoNotificationStore? notifications=null)
    {
        this.notifications=notifications??new DemoNotificationStore();
        var names=new[]{"Aina Rahman","Daniel Tan","Nur Aisyah","Sarah Lim","Amir Hassan","Workspace Admin"};
        for(var i=0;i<Roles.All.Length;i++) { var u=new AppUser { Id=Guid.Parse($"00000000-0000-0000-0000-{i+1:000000000000}"),Name=names[i],Email=$"{Roles.All[i].ToLowerInvariant()}@example.test",Role=Roles.All[i],Department=i==0?"Customer Service":"Quality Management"}; users.Add(u.Id,u); }
        var subjects=new[]{"Reagent control outside expected range","Instrument display intermittently unresponsive","Incorrect item delivered against purchase order","Delayed delivery of calibration material","Damaged packaging on arrival","Lot documentation missing from shipment","Replacement product verification","New customer complaint draft"};
        var stages=new[]{Stage.Investigation,Stage.PlanHod,Stage.Actions,Stage.Evaluation,Stage.CompletionQm,Stage.Effectiveness,Stage.Closed,Stage.Draft};
        for(var i=0;i<subjects.Length;i++)
        {
            var c=new Complaint { Reference=$"SMA-{Ui.Today.Year}-{i+1:0000}",Subject=subjects[i],CustomerName=$"Sample customer {i+1}",Hospital=new[]{"Sample Central Laboratory","Sample Medical Centre","Sample Diagnostic Lab"}[i%3],Contact="03-0000 0000",SubmittedBy=names[0],CreatedBy=users.Values.First(x=>x.Role==Roles.Requestor).Id,OwnerId=users.Values.First(x=>x.Role==Roles.Owner).Id,HodId=users.Values.First(x=>x.Role==Roles.Hod).Id,QmId=users.Values.First(x=>x.Role==Roles.Qm).Id,CmId=users.Values.First(x=>x.Role==Roles.Cm).Id,Stage=stages[i],Revision=1,Department="Quality Management",CreatedAt=DateTimeOffset.UtcNow.AddDays(-i-1),UpdatedAt=DateTimeOffset.UtcNow.AddHours(-i),ComplaintDate=Ui.Today.AddDays(-i-1),DueAt=i==6?null:DateTimeOffset.UtcNow.AddDays(i%3-1),ProductCategory="Instrument",ProductCode=$"DEMO-{100+i}",PartDescription="Sample laboratory product",SerialLot=$"LOT-DEMO-{i+1}",Invoice=$"PO-SAMPLE-{i+1:000}",Problem="Synthetic example for exploring the complaint workflow. Replace with verified details when creating a real complaint.",Category=i==2?"Non-Quality":"Quality",Seriousness="Not Critical",Hazard="Not Hazardous / Low",Urgency="Within 1 week",HealthHazard=false,ManufacturerClaim=false,LegislativeRequirement=false,IntendedPurpose=false,ReportingReviewed=true,Reportable=false,ReportingNote="Sample review only.",ReceivedDate=Ui.Today.AddDays(-i-1),ReceivedBy=names[1],RootCause="Sample investigation finding for demonstration.",InvestigationDate=Ui.Today,Investigator=names[1],Corrections=["Process replacement"],Actions=[new(){Description="Verify affected lot and document corrective action",Responsible=names[1],DueDate=Ui.Today.AddDays(2),CompletedDate=i>=4?Ui.Today:null,Evidence=i>=4?"Sample evidence reference DEMO-01":""}],Effectiveness=i>=5?[new(){Description="Review follow-up results",Responsible=names[3],DueDate=Ui.Today.AddDays(7)}]:[] };
            c.History.Add(new(){ActorId=c.CreatedBy,ActorName=names[0],ActorRole=Roles.Requestor,Action="Sample complaint created",At=c.CreatedAt,ToStage=c.Stage.ToString(),Note="Synthetic demonstration record"});
            complaints[c.Id]=c;
        }
    }
    public Task<List<Complaint>> ListAsync(AppUser user) { lock(gate) return Task.FromResult(complaints.Values.Where(c=>new Workflow().CanView(c,user)).OrderByDescending(c=>c.UpdatedAt).Select(Copy).ToList()); }
    public Task<Complaint?> GetAsync(Guid id) { lock(gate) return Task.FromResult(complaints.TryGetValue(id,out var c)?Copy(c):null); }
    public Task SaveAsync(Complaint c,int expected,AuditEntry e,StoredAttachment? attachment=null)
    {
        lock(gate)
        {
            if(complaints.TryGetValue(c.Id,out var old)?old.Revision!=expected:expected!=0) throw new ConcurrencyConflictException();
            notifications.Publish(NotificationRouting.Complaint(c,e,old),users.Values);
            c.Revision=expected+1;c.UpdatedAt=DateTimeOffset.UtcNow;c.History.Add(e);complaints[c.Id]=Copy(c);if(attachment!=null)attachments[attachment.Info.Id]=Copy(attachment);return Task.CompletedTask;
        }
    }
    public Task<StoredAttachment?> AttachmentAsync(Guid id) { lock(gate)return Task.FromResult(attachments.TryGetValue(id,out var x)?Copy(x):null); }
    public Task<List<AppUser>> UsersAsync() {lock(gate)return Task.FromResult(users.Values.Select(Copy).ToList());}
    public Task<AppUser?> UserAsync(Guid id){lock(gate)return Task.FromResult(users.TryGetValue(id,out var u)?Copy(u):null);}
    public Task<AppUser?> UserByEmailAsync(string email){lock(gate)return Task.FromResult(users.Values.Where(x=>x.Email.Equals(email,StringComparison.OrdinalIgnoreCase)).Select(Copy).FirstOrDefault());}
    public Task SaveUserAsync(AppUser u){lock(gate){users[u.Id]=Copy(u);return Task.CompletedTask;}}
}
