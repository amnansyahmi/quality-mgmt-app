using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Controllers;

public sealed class ComplaintsController(IComplaintStore store,Workflow flow,IFormStore forms):AppController(store,flow)
{
    public async Task<IActionResult> Index(string view="overview",string q="",string status="")
    {
        view=new[]{"overview","inbox","register","drafts","overdue"}.Contains(view)?view:"overview";
        var u=await Actor();var model=new DashboardVm{User=u,Complaints=await Store.ListAsync(u),Users=await Store.UsersAsync(),View=view,Query=q,Status=status};
        model.Tasks=Flow.TasksFor(model.Complaints,u);if(view=="inbox"){model.FormTasks=await forms.TasksAsync(u);if(!string.IsNullOrWhiteSpace(q))model.FormTasks=model.FormTasks.Where(e=>(e.Reference+" "+e.Schema.Name).Contains(q,StringComparison.OrdinalIgnoreCase)).ToList();}ViewData["ActiveNav"]=view;
        return View(view switch{"inbox"=>"Inbox","register"=>"Register","drafts"=>"Drafts","overdue"=>"Deadlines",_=>"Workflow"},model);
    }
    public IActionResult New(){ViewData["ActiveNav"]="new";return View();}
    [HttpPost,Authorize(Roles=Roles.Requestor)]
    public async Task<IActionResult> Create()
    {
        var u=await Actor();var c=new Complaint{CreatedBy=u.Id,SubmittedBy=u.Name,Reference=$"SMA-{Ui.Today.Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"};
        await Save(c,0,u,"Draft created");return Back(c.Id);
    }
    public async Task<IActionResult> Details(Guid id,string? tab=null,string? from=null)
    {
        ViewData["ActiveNav"]=from=="inbox"?"inbox":"register";
        try {var c=await Load(id);var u=await Actor();tab??=Flow.TasksFor([c],u).FirstOrDefault()?.Tab??(c.Stage==Stage.Draft?"notification":c.Stage is Stage.Effectiveness or Stage.ClosureQm or Stage.ClosureCm or Stage.Closed?"effectiveness":"processing");return View(new DetailVm{Complaint=c,User=u,Users=await Store.UsersAsync(),Tab=tab});}
        catch(WorkflowException){return NotFound();}
    }
    private static List<ActionItem> Rows(List<ActionItem>? rows)=> (rows??[]).Where(x=>!string.IsNullOrWhiteSpace(x.Description)).Take(30).ToList();
    [HttpPost,RequestSizeLimit(200_000)]
    public Task<IActionResult> Edit(Guid id,int revision,string section,string command,Complaint input,bool returnToInbox=false)=>Mutate(id,revision,section,async(c,u)=>
    {
        Flow.Demand(ModelState.IsValid,"Some values could not be read. Check the dates and field values, then save again.");
        var from=c.Stage;
        if(section=="notification")
        {
            Flow.Demand(Flow.CanEditA(c,u));
            Flow.Demand(input.Subject.Length<=300&&input.CustomerName.Length<=200,"Subject must be at most 300 characters and customer name at most 200.");
            c.ComplaintDate=input.ComplaintDate;c.CustomerName=input.CustomerName.Trim();c.Hospital=input.Hospital;c.Contact=input.Contact;c.Invoice=input.Invoice;c.OutOfStock=input.OutOfStock;
            c.Subject=input.Subject.Trim();c.PartDescription=input.PartDescription;c.ProductCode=input.ProductCode;c.ProductCategory=input.ProductCategory;c.SerialLot=input.SerialLot;c.ExpiryDate=input.ExpiryDate;c.Frequency=input.Frequency;c.Problem=input.Problem;
            c.Corrections=input.Corrections;c.CorrectionOther=input.CorrectionOther;c.ImmediateActions=Rows(input.ImmediateActions);
        }
        else if(section=="processing")
        {
            Flow.Demand(Flow.CanEditB(c,u));
            if(c.Stage==Stage.Investigation)
            {
                var before=JsonSerializer.Serialize(new{c.Category,c.HealthHazard,c.ManufacturerClaim,c.LegislativeRequirement,c.IntendedPurpose});var previousUrgency=c.Urgency;
                c.ReceivedDate=input.ReceivedDate;c.ReceivedBy=input.ReceivedBy;c.ReceivedVia=input.ReceivedVia;c.Category=input.Category;c.Seriousness=input.Seriousness;c.Hazard=input.Hazard;c.Urgency=input.Urgency;
                c.HealthHazard=input.HealthHazard;c.ManufacturerClaim=input.ManufacturerClaim;c.LegislativeRequirement=input.LegislativeRequirement;c.IntendedPurpose=input.IntendedPurpose;
                if(c.Category=="Non-Quality"){c.Seriousness="Not Applicable";c.Hazard="Not Applicable";c.Urgency="Not Applicable";c.HealthHazard=null;c.ManufacturerClaim=null;c.LegislativeRequirement=null;c.IntendedPurpose=null;}
                if(before!=JsonSerializer.Serialize(new{c.Category,c.HealthHazard,c.ManufacturerClaim,c.LegislativeRequirement,c.IntendedPurpose})){c.ReportingReviewed=false;c.Reportable=null;c.ReportingDueDate=null;c.ReportingNote="";}
                c.RootCause=input.RootCause;c.Investigator=input.Investigator;c.InvestigationDate=input.InvestigationDate;c.Actions=Rows(input.Actions);
                if(c.Urgency!=previousUrgency&&!c.ApprovedExtensionDueDate.HasValue)c.DueAt=c.Urgency switch{"Within 24 hours"=>c.CreatedAt.AddHours(24),"Within 1 week"=>c.CreatedAt.AddDays(7),"Within 1 month"=>c.CreatedAt.AddMonths(1),"Not Applicable"=>null,_=>c.DueAt};
            }
            else
            {
                Flow.Demand(input.Actions.Count==c.Actions.Count,"The approved action plan cannot be changed at completion.");
                for(var i=0;i<c.Actions.Count;i++){c.Actions[i].Evidence=input.Actions[i].Evidence;c.Actions[i].CompletedDate=input.Actions[i].CompletedDate;}
            }
        }
        else if(section=="effectiveness"){Flow.Demand(Flow.CanEditC(c,u));c.Effectiveness=Rows(input.Effectiveness);}
        else throw new WorkflowException("Unknown form section.");
        if(command=="advance")
        {
            try{Flow.Advance(c,u);}
            catch(WorkflowException ex){await Save(c,revision,u,"Form saved",$"Section: {section}. Submission blocked: {ex.Message}",from);throw new WorkflowException(ex.Message+" Your form entries have been saved.");}
        }
        await Save(c,revision,u,command=="advance"?Workflow.AdvanceLabel(from):"Form saved",$"Section: {section}",from);
        if(command=="advance"){var users=await Store.UsersAsync();var recipient=QueueViews.Person(users,Flow.Assignee(c),c.Stage==Stage.Evaluation?"the QM evaluation queue":Ui.TaskRole(c.Stage));TempData["Success"]=$"{c.Reference} submitted to {recipient} for {Ui.StageName(c.Stage).ToLowerInvariant()}.";}
    },command=="advance"&&returnToInbox?"inbox":null);
    [HttpPost]
    public Task<IActionResult> Assign(Guid id,int revision,Guid ownerId,Guid hodId,Guid qmId,Guid cmId,string department)=>Mutate(id,revision,"processing",async(c,u)=>
    {
        Flow.Demand(c.Stage==Stage.Evaluation&&Flow.IsAssigned(c,u));var users=await Store.UsersAsync();
        foreach(var pair in new[]{(ownerId,Roles.Owner),(hodId,Roles.Hod),(qmId,Roles.Qm),(cmId,Roles.Cm)})Flow.Demand(users.Any(x=>x.Id==pair.Item1&&x.Role==pair.Item2&&x.Active),$"Select an active {pair.Item2}.");
        c.OwnerId=ownerId;c.HodId=hodId;c.QmId=qmId;c.CmId=cmId;c.Department=department;c.ReceivedDate??=Ui.Today;c.ReceivedBy=u.Name;
        var from=c.Stage;Flow.Advance(c,u);await Save(c,revision,u,"Complaint assigned",$"Department: {department}",from);
    });
    [HttpPost]
    public Task<IActionResult> Task(Guid id,int revision,string command,string note,Guid? nextAssigneeId,Stage? returnStage,bool returnToInbox=false)=>Mutate(id,revision,"processing",async(c,u)=>
    {
        Flow.Demand(ModelState.IsValid,"Check the selected recipient and return destination.");
        Flow.Demand((note??"").Length<=4000,"Decision notes must be 4,000 characters or fewer.");
        var from=c.Stage;Flow.Demand(Workflow.IsApprovalStage(c.Stage),"Use the editable form to submit this stage.");
        var users=await Store.UsersAsync();string Person(Guid? personId)=>users.FirstOrDefault(x=>x.Id==personId)?.Name??"Unassigned";
        if(command=="return")
        {
            Flow.Return(c,u,note??"",returnStage);
            var recipient=Flow.Assignee(c);Flow.Demand(users.Any(x=>x.Id==recipient && x.Active && x.Role==Ui.TaskRole(c.Stage)),$"The return recipient must be active and have the {Ui.TaskRole(c.Stage)} role. Ask an administrator to update that account before returning.");
            await Save(c,revision,u,"Returned for correction",$"To: {Person(recipient)} ({Ui.TaskRole(c.Stage)}). {note}",from);
            TempData["Success"]=$"{c.Reference} returned to {Person(recipient)} for {Ui.StageName(c.Stage).ToLowerInvariant()}. The same complaint reference is retained.";
        }
        else if(command=="advance")
        {
            var next=Workflow.NextStage(from)!.Value;var recipient=next==Stage.Closed?null:users.FirstOrDefault(x=>x.Id==(nextAssigneeId??Workflow.AssigneeAt(c,next)));
            Flow.ApproveAndForward(c,u,recipient);
            var route=next==Stage.Closed?"Complaint closed.":$"Sent to: {recipient!.Name} ({recipient.Role}). Next task: {Ui.StageName(next)}.";
            await Save(c,revision,u,Workflow.AdvanceLabel(from),route+(string.IsNullOrWhiteSpace(note)?"":" Decision: "+note),from);
            TempData["Success"]=next==Stage.Closed?$"{c.Reference} approved and closed.":$"{c.Reference} approved and sent to {recipient!.Name}. It is now in their task inbox.";
        }
        else throw new WorkflowException("Unknown workflow command.");
    },returnToInbox?"inbox":null);
    [HttpPost]
    public Task<IActionResult> ReturnToRequestor(Guid id,int revision,string note)=>Mutate(id,revision,"notification",async(c,u)=>{Flow.Demand(c.Stage==Stage.Evaluation);var from=c.Stage;Flow.Return(c,u,note??"");await Save(c,revision,u,"Returned to requestor",note??"",from);});
    [HttpPost]
    public Task<IActionResult> Reporting(Guid id,int revision,bool? reportable,DateOnly? reportingDueDate,string reportingNote)=>Mutate(id,revision,"processing",async(c,u)=>
    {
        Flow.Demand(ModelState.IsValid,"Check the reporting due date.");
        Flow.Demand(u.Role==Roles.Qm&&c.QmId==u.Id&&c.Stage==Stage.Investigation,"Only the assigned QM can confirm the reporting review during investigation.");
        Flow.Demand(c.Category is "Quality" or "Non-Quality","The owner must save the complaint evaluation first.");
        Flow.Demand(c.Category!="Quality"||new[]{c.HealthHazard,c.ManufacturerClaim,c.LegislativeRequirement,c.IntendedPurpose}.All(x=>x.HasValue),"The owner must answer all reporting questions first.");
        Flow.Demand(c.Category!="Quality"||reportable.HasValue,"Select a reporting determination.");
        Flow.Demand(!c.HasReportingFlag||reportable==true,"The supplied form requires reportability when any criterion is Yes.");
        Flow.Demand(reportable!=true||reportingDueDate.HasValue,"Set a reporting due date when reportable.");
        Flow.Demand(!string.IsNullOrWhiteSpace(reportingNote),"Record the basis for the reporting decision.");
        c.ReportingReviewed=true;c.Reportable=c.Category=="Non-Quality"?null:reportable;c.ReportingDueDate=c.Reportable==true?reportingDueDate:null;c.ReportingNote=reportingNote;
        await Save(c,revision,u,"Reporting review confirmed",reportingNote);
    });
    [HttpPost]
    public Task<IActionResult> RequestException(Guid id,int revision,string kind,string reason,DateOnly? requestedDate)=>Mutate(id,revision,"processing",async(c,u)=>
    {
        Flow.Demand(c.OwnerId==u.Id&&u.Role==Roles.Owner&&c.Stage is Stage.Investigation or Stage.Actions);
        Flow.Demand(!string.IsNullOrWhiteSpace(reason),"A reason is required.");
        if(kind=="extension"){Flow.Demand(!c.ExtensionPending,"An extension is already awaiting HOD approval.");Flow.Demand(requestedDate>Ui.Today&&(!c.DueAt.HasValue||Ui.EndOfDay(requestedDate.Value)>c.DueAt),"The new due date must be later than today and the current deadline.");c.ExtensionPending=true;c.ExtensionReason=reason;c.RequestedDueDate=requestedDate;}
        else if(kind=="delay"){Flow.Demand(!c.ResponseDelayPending,"A response delay is already awaiting HOD approval.");c.ResponseDelayPending=true;c.ResponseDelayReason=reason;}
        else throw new WorkflowException("Unknown request type.");await Save(c,revision,u,kind=="extension"?"Extension requested":"Response delay approval requested",reason);
    });
    [HttpPost]
    public Task<IActionResult> DecideException(Guid id,int revision,string kind,bool approve,string note)=>Mutate(id,revision,"processing",async(c,u)=>
    {
        Flow.Demand(u.Role==Roles.Hod&&c.HodId==u.Id);Flow.Demand(!string.IsNullOrWhiteSpace(note),"Record the reason for this decision.");
        if(kind=="extension"){Flow.Demand(c.ExtensionPending);if(approve){c.ApprovedExtensionDueDate=c.RequestedDueDate;c.DueAt=Ui.EndOfDay(c.RequestedDueDate!.Value);}c.ExtensionPending=false;}
        else if(kind=="delay"){Flow.Demand(c.ResponseDelayPending);c.ResponseDelayPending=false;c.ResponseDelayApproved=approve;}
        else throw new WorkflowException("Unknown request type.");await Save(c,revision,u,$"{kind} {(approve?"approved":"declined")}",note);
    });
    [HttpPost,RequestSizeLimit(12_000_000),RequestFormLimits(MultipartBodyLengthLimit=12_000_000)]
    public Task<IActionResult> Upload(Guid id,int revision,IFormFile? file)=>Mutate(id,revision,"evidence",async(c,u)=>
    {
        Flow.Demand(Flow.CanUpload(c,u));Flow.Demand(file is not null&&file.Length is >0 and <=10_485_760,"Choose a file no larger than 10 MB.");
        var name=Path.GetFileName(file!.FileName);Flow.Demand(name.Length is >0 and <=255,"The filename must contain at most 255 characters.");
        Flow.Demand(new[]{".pdf",".png",".jpg",".jpeg",".csv",".txt",".xlsx",".docx",".zip"}.Contains(Path.GetExtension(name).ToLowerInvariant()),"Supported evidence: PDF, PNG, JPG, CSV, TXT, XLSX, DOCX, ZIP.");
        Flow.Demand(c.Attachments.Count<20&&c.Attachments.Sum(x=>x.Size)+file.Length<=52_428_800,"The evidence limit is 20 files / 50 MB per complaint.");
        await using var ms=new MemoryStream();await file.CopyToAsync(ms);var info=new AttachmentInfo{Name=name,Size=ms.Length,UploadedBy=u.Id,ContentType="application/octet-stream"};c.Attachments.Add(info);
        await Save(c,revision,u,"Evidence uploaded",name,attachment:new(c.Id,info,ms.ToArray()));
    });
    public async Task<IActionResult> Download(Guid id)
    {
        var f=await Store.AttachmentAsync(id);if(f==null)return NotFound();try{await Load(f.ComplaintId);}catch(WorkflowException){return NotFound();}return File(f.Bytes,"application/octet-stream",f.Info.Name);
    }
    public async Task<IActionResult> Print(Guid id)
    {
        try{return View(new DetailVm{Complaint=await Load(id),User=await Actor(),Users=await Store.UsersAsync()});}catch(WorkflowException){return NotFound();}
    }
    public async Task<IActionResult> Export()
    {
        var rows=await Store.ListAsync(await Actor());
        static string Csv(string? s){s??="";if(s.TrimStart().Length>0&&new[]{'=','+','-','@'}.Contains(s.TrimStart()[0]))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
        var text="Reference,Subject,Customer,Hospital,Stage,Department,Due date\r\n"+string.Join("\r\n",rows.Select(c=>string.Join(",",new[]{c.Reference,c.Subject,c.CustomerName,c.Hospital,Ui.StageName(c.Stage),c.Department,Ui.Date(c.DueAt)}.Select(Csv))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),"text/csv","complaint-register.csv");
    }
}
