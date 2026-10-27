using SmaComplaints.Models;
namespace SmaComplaints.Services;

public sealed class Workflow
{
    public bool CanView(Complaint c, AppUser u) => u.Active && (u.Role == Roles.Admin || (u.Role == Roles.Qm && c.Stage != Stage.Draft) || c.CreatedBy == u.Id || (c.Stage!=Stage.Draft && (c.OwnerId == u.Id || c.HodId == u.Id || c.CmId == u.Id)));
    public bool IsAssigned(Complaint c, AppUser u) => c.Stage switch
    {
        Stage.Draft => c.CreatedBy == u.Id,
        Stage.Evaluation => u.Role == Roles.Qm && (c.QmId == null || c.QmId == u.Id),
        Stage.Investigation or Stage.Actions => c.OwnerId == u.Id && u.Role == Roles.Owner,
        Stage.PlanHod or Stage.CompletionHod => c.HodId == u.Id && u.Role == Roles.Hod,
        Stage.PlanQm or Stage.CompletionQm or Stage.Effectiveness or Stage.ClosureQm => c.QmId == u.Id && u.Role == Roles.Qm,
        Stage.ClosureCm => c.CmId == u.Id && u.Role == Roles.Cm,
        _ => false
    };
    public bool CanEditA(Complaint c, AppUser u) => c.Stage == Stage.Draft && c.CreatedBy == u.Id;
    public bool CanEditB(Complaint c, AppUser u) => c.Stage is Stage.Investigation or Stage.Actions && IsAssigned(c, u);
    public bool CanEditC(Complaint c, AppUser u) => c.Stage == Stage.Effectiveness && IsAssigned(c, u);
    public bool CanUpload(Complaint c, AppUser u) => CanEditA(c, u) || CanEditB(c, u) || CanEditC(c, u);
    public static bool IsApprovalStage(Stage stage)=>stage is Stage.PlanHod or Stage.PlanQm or Stage.CompletionHod or Stage.CompletionQm or Stage.ClosureQm or Stage.ClosureCm;
    public static Stage? NextStage(Stage stage)=>stage switch {Stage.Draft=>Stage.Evaluation,Stage.Evaluation=>Stage.Investigation,Stage.Investigation=>Stage.PlanHod,Stage.PlanHod=>Stage.PlanQm,Stage.PlanQm=>Stage.Actions,Stage.Actions=>Stage.CompletionHod,Stage.CompletionHod=>Stage.CompletionQm,Stage.CompletionQm=>Stage.Effectiveness,Stage.Effectiveness=>Stage.ClosureQm,Stage.ClosureQm=>Stage.ClosureCm,Stage.ClosureCm=>Stage.Closed,_=>null};
    public static Guid? AssigneeAt(Complaint c,Stage stage)=>stage switch {Stage.Draft=>c.CreatedBy,Stage.Evaluation or Stage.PlanQm or Stage.CompletionQm or Stage.Effectiveness or Stage.ClosureQm=>c.QmId,Stage.Investigation or Stage.Actions=>c.OwnerId,Stage.PlanHod or Stage.CompletionHod=>c.HodId,Stage.ClosureCm=>c.CmId,_=>null};
    public static Stage[] ReturnTargets(Stage stage)=>stage switch {Stage.Evaluation=>[Stage.Draft],Stage.PlanHod=>[Stage.Investigation],Stage.PlanQm=>[Stage.Investigation,Stage.PlanHod],Stage.CompletionHod=>[Stage.Actions],Stage.CompletionQm=>[Stage.Actions,Stage.CompletionHod],Stage.ClosureQm=>[Stage.Effectiveness],Stage.ClosureCm=>[Stage.Effectiveness,Stage.ClosureQm],_=>[]};
    public static AuditEntry? LatestReturn(Complaint c){var transition=c.History.Where(x=>x.FromStage!=x.ToStage).OrderByDescending(x=>x.At).FirstOrDefault();return transition is not null && transition.Action.StartsWith("Returned",StringComparison.OrdinalIgnoreCase) && transition.ToStage==c.Stage.ToString()?transition:null;}
    public static string SubmitLabel(Complaint c)=>LatestReturn(c) is null?AdvanceLabel(c.Stage):c.Stage switch{Stage.Draft=>"Resubmit complaint",Stage.Investigation=>"Resubmit action plan",Stage.Actions=>"Resubmit completed actions",Stage.Effectiveness=>"Resubmit effectiveness review",_=>AdvanceLabel(c.Stage)};
    public bool HasWork(Complaint c,AppUser u)=>IsAssigned(c,u)||(c.Stage==Stage.Investigation && c.QmId==u.Id && u.Role==Roles.Qm && !c.ReportingReviewed)||(c.Stage is not(Stage.Draft or Stage.Closed) && c.HodId==u.Id && u.Role==Roles.Hod && (c.ExtensionPending||c.ResponseDelayPending));
    public void ApproveAndForward(Complaint c,AppUser u,AppUser? recipient)
    {
        Demand(u.Active && IsAssigned(c,u) && IsApprovalStage(c.Stage));
        var next=NextStage(c.Stage)!.Value;
        if(next==Stage.Closed)Demand(recipient is null,"Final closure does not have a next recipient.");
        else Demand(recipient is not null && recipient.Active && recipient.Role==Ui.TaskRole(next),$"Choose an active {Ui.TaskRole(next)} for the next task.");
        Advance(c,u);
        if(recipient is not null)switch(Ui.TaskRole(next)){case Roles.Owner:c.OwnerId=recipient.Id;break;case Roles.Hod:c.HodId=recipient.Id;break;case Roles.Qm:c.QmId=recipient.Id;break;case Roles.Cm:c.CmId=recipient.Id;break;}
    }
    public List<ComplaintTask> TasksFor(IEnumerable<Complaint> records,AppUser u)
    {
        var tasks=new List<ComplaintTask>();
        foreach(var c in records.Where(c=>CanView(c,u)))
        {
            var returned=LatestReturn(c);
            if(IsAssigned(c,u))tasks.Add(new(c,IsApprovalStage(c.Stage)?"approval":"form",IsApprovalStage(c.Stage)?Ui.StageName(c.Stage):c.Stage switch{Stage.Draft=>returned is null?"Complete notification":"Correct returned notification",Stage.Evaluation=>"Evaluate and assign complaint",Stage.Investigation=>returned is null?"Prepare investigation and action plan":"Correct returned action plan",Stage.Actions=>returned is null?"Complete approved actions":"Correct action completion evidence",Stage.Effectiveness=>returned is null?"Record effectiveness checks":"Correct effectiveness review",_=>Ui.StageName(c.Stage)},IsApprovalStage(c.Stage)?"Review & decide":c.Stage==Stage.Evaluation?"Assign complaint":returned is not null?"Make corrections":"Open task",c.Stage==Stage.Draft?"notification":c.Stage is Stage.Effectiveness or Stage.ClosureQm or Stage.ClosureCm?"effectiveness":"processing",false));
            if(c.Stage==Stage.Investigation && c.QmId==u.Id && u.Role==Roles.Qm && !c.ReportingReviewed)tasks.Add(new(c,"reporting","Confirm reportability","Review reporting","processing",c.Category is not("Quality" or "Non-Quality") || (c.Category=="Quality" && new[]{c.HealthHazard,c.ManufacturerClaim,c.LegislativeRequirement,c.IntendedPurpose}.Any(x=>!x.HasValue))));
            if(c.Stage is not(Stage.Draft or Stage.Closed) && c.HodId==u.Id && u.Role==Roles.Hod){if(c.ExtensionPending)tasks.Add(new(c,"extension","Decide deadline extension","Review extension","processing",false));if(c.ResponseDelayPending)tasks.Add(new(c,"delay","Decide response delay","Review delay","processing",false));}
        }
        return tasks.OrderBy(x=>x.Waiting).ThenByDescending(x=>IsOverdue(x.Complaint)).ThenBy(x=>x.Complaint.DueAt??DateTimeOffset.MaxValue).ToList();
    }
    public void Demand(bool condition, string message = "This task is assigned to another role or is no longer editable.") { if (!condition) throw new WorkflowException(message); }
    public static bool IsOverdue(Complaint c) => c.Stage is not (Stage.Draft or Stage.Closed) && c.DueAt < DateTimeOffset.UtcNow;
    public static bool HasPendingDelay(Complaint c) => c.Stage != Stage.Draft && c.CreatedAt.AddDays(7) < DateTimeOffset.UtcNow && !c.ResponseDelayApproved;
    public Guid? Assignee(Complaint c) => c.Stage switch
    {
        Stage.Draft => c.CreatedBy, Stage.Evaluation or Stage.PlanQm or Stage.CompletionQm or Stage.Effectiveness or Stage.ClosureQm => c.QmId,
        Stage.Investigation or Stage.Actions => c.OwnerId, Stage.PlanHod or Stage.CompletionHod => c.HodId, Stage.ClosureCm => c.CmId, _ => null
    };
    public void Advance(Complaint c, AppUser u)
    {
        Demand(IsAssigned(c, u));
        switch (c.Stage)
        {
            case Stage.Draft:
                Demand(!string.IsNullOrWhiteSpace(c.CustomerName) && !string.IsNullOrWhiteSpace(c.Hospital) && !string.IsNullOrWhiteSpace(c.Contact), "Complete all compulsory customer details.");
                Demand(!string.IsNullOrWhiteSpace(c.Subject) && !string.IsNullOrWhiteSpace(c.Problem), "Enter a subject and detailed problem description.");
                Demand(c.OutOfStock || !string.IsNullOrWhiteSpace(c.Invoice), "Provide an invoice / PO number, or mark out of stock.");
                Demand(!new[] { "Reagent", "QC", "Calibrator" }.Contains(c.ProductCategory) || c.ExpiryDate != null, "Expiry date is required for reagent, QC, and calibrator complaints.");
                Demand(c.Corrections.Count > 0, "Select the immediate correction, including None if no correction was taken.");
                Demand(!c.Corrections.Contains("None") || c.Corrections.Count == 1, "None cannot be combined with another correction.");
                Demand(!c.Corrections.Contains("Other") || !string.IsNullOrWhiteSpace(c.CorrectionOther), "Describe the other correction.");
                Demand(c.Attachments.Count > 0, "Attach supporting evidence before submission.");
                c.Stage = Stage.Evaluation; c.DueAt = DateTimeOffset.UtcNow.AddHours(24); break;
            case Stage.Evaluation:
                Demand(c.OwnerId != null && c.HodId != null && c.QmId != null && c.CmId != null && !string.IsNullOrWhiteSpace(c.Department), "Assign a department, complaint owner, HOD, QM, and CM.");
                c.Stage = Stage.Investigation; break;
            case Stage.Investigation:
                Demand(c.Category is "Quality" or "Non-Quality", "Evaluate the complaint category.");
                Demand(c.ReceivedDate != null && !string.IsNullOrWhiteSpace(c.ReceivedBy), "Record when and by whom the complaint was received.");
                Demand(!string.IsNullOrWhiteSpace(c.RootCause) && c.InvestigationDate != null && !string.IsNullOrWhiteSpace(c.Investigator), "Complete the root cause, investigator, and investigation date.");
                Demand(!string.IsNullOrWhiteSpace(c.Seriousness) && !string.IsNullOrWhiteSpace(c.Hazard) && !string.IsNullOrWhiteSpace(c.Urgency), "Complete seriousness, hazard, and urgency.");
                Demand(c.Category != "Quality" || new[] { c.HealthHazard, c.ManufacturerClaim, c.LegislativeRequirement, c.IntendedPurpose }.All(x => x.HasValue), "Answer every reportability question.");
                Demand(c.ReportingReviewed, "QM must confirm the reporting review before the plan is submitted.");
                Demand(c.Actions.Count > 0 && c.Actions.All(ValidPlan), "Add at least one action with a responsible person and due date.");
                c.Stage = Stage.PlanHod; break;
            case Stage.PlanHod: c.Stage = Stage.PlanQm; break;
            case Stage.PlanQm: c.Stage = Stage.Actions; break;
            case Stage.Actions:
                Demand(c.Actions.Count > 0 && c.Actions.All(Completed), "Each action needs completion date and objective evidence.");
                Demand(!HasPendingDelay(c), "For a response exceeding one week, obtain HOD approval of the delay reason first.");
                Demand(!c.ExtensionPending, "Resolve the pending extension request before submitting completion.");
                Demand(!c.ResponseDelayPending, "Resolve the pending response delay request before submitting completion.");
                c.Stage = Stage.CompletionHod; break;
            case Stage.CompletionHod: c.Stage = Stage.CompletionQm; break;
            case Stage.CompletionQm: c.Stage = Stage.Effectiveness; break;
            case Stage.Effectiveness:
                Demand(c.Effectiveness.Count > 0 && c.Effectiveness.All(x => ValidPlan(x) && Completed(x)), "Complete at least one effectiveness check, including responsibility, dates, and evidence.");
                c.Stage = Stage.ClosureQm; break;
            case Stage.ClosureQm: c.Stage = Stage.ClosureCm; break;
            case Stage.ClosureCm: c.Stage = Stage.Closed; c.DueAt = null; break;
            default: throw new WorkflowException("This complaint is closed.");
        }
        if (c.Stage == Stage.Actions && c.Actions.Count > 0)
        {
            c.DueAt = Ui.EndOfDay(c.Actions.Min(x => x.DueDate!.Value));
            if(c.ApprovedExtensionDueDate is DateOnly extended && Ui.EndOfDay(extended)>c.DueAt)c.DueAt=Ui.EndOfDay(extended);
        }
    }
    public void Return(Complaint c, AppUser u, string note, Stage? target = null)
    {
        Demand(IsAssigned(c, u)); Demand(!string.IsNullOrWhiteSpace(note), "Give a reason for returning the task.");
        var targets=ReturnTargets(c.Stage);Demand(targets.Length>0,"This stage cannot be returned.");
        var destination=target??targets[0];Demand(targets.Contains(destination),"Choose an allowed correction or previous-review stage. Approval stages cannot be skipped.");
        c.Stage=destination;
    }
    public static bool ValidPlan(ActionItem x) => !string.IsNullOrWhiteSpace(x.Description) && !string.IsNullOrWhiteSpace(x.Responsible) && x.DueDate.HasValue;
    public static bool Completed(ActionItem x) => x.CompletedDate.HasValue && x.CompletedDate <= Ui.Today && !string.IsNullOrWhiteSpace(x.Evidence);
    public static string AdvanceLabel(Stage stage) => stage switch
    {
        Stage.Draft => "Submit complaint", Stage.Evaluation => "Assign & begin investigation", Stage.Investigation => "Submit action plan",
        Stage.PlanHod or Stage.PlanQm => "Approve action plan", Stage.Actions => "Submit completed actions", Stage.CompletionHod or Stage.CompletionQm => "Approve completed actions",
        Stage.Effectiveness => "Submit effectiveness review", Stage.ClosureQm => "Confirm closure review", Stage.ClosureCm => "Approve & close complaint", _ => "Closed"
    };
}
