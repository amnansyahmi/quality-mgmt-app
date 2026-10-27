using System.ComponentModel.DataAnnotations;

namespace SmaComplaints.Models;

public enum Stage { Draft, Evaluation, Investigation, PlanHod, PlanQm, Actions, CompletionHod, CompletionQm, Effectiveness, ClosureQm, ClosureCm, Closed }
public static class Roles
{
    public const string Requestor = "Requestor", Owner = "Owner", Hod = "HOD", Qm = "QM", Cm = "CM", Admin = "Admin";
    public static readonly string[] All = [Requestor, Owner, Hod, Qm, Cm, Admin];
}
public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = Roles.Requestor;
    public string Department { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool Active { get; set; } = true;
}
public sealed class ActionItem
{
    public string Description { get; set; } = "";
    public string Responsible { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public string Evidence { get; set; } = "";
}
public sealed class Complaint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = "";
    public int Revision { get; set; }
    public Stage Stage { get; set; } = Stage.Draft;
    public Guid CreatedBy { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? HodId { get; set; }
    public Guid? QmId { get; set; }
    public Guid? CmId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public string SubmittedBy { get; set; } = "";
    public DateOnly ComplaintDate { get; set; } = Ui.Today;
    public string CustomerName { get; set; } = "";
    public string Hospital { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Invoice { get; set; } = "";
    public bool OutOfStock { get; set; }
    public string Subject { get; set; } = "";
    public string PartDescription { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductCategory { get; set; } = "";
    public string SerialLot { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public string Frequency { get; set; } = "Unknown";
    public string Problem { get; set; } = "";
    public List<string> Corrections { get; set; } = [];
    public string CorrectionOther { get; set; } = "";
    public List<ActionItem> ImmediateActions { get; set; } = [];
    public string Department { get; set; } = "";
    public DateOnly? ReceivedDate { get; set; }
    public string ReceivedBy { get; set; } = "";
    public string ReceivedVia { get; set; } = "Email / letter";
    public string Category { get; set; } = "";
    public string Seriousness { get; set; } = "";
    public string Hazard { get; set; } = "";
    public string Urgency { get; set; } = "";
    public bool? HealthHazard { get; set; }
    public bool? ManufacturerClaim { get; set; }
    public bool? LegislativeRequirement { get; set; }
    public bool? IntendedPurpose { get; set; }
    public bool ReportingReviewed { get; set; }
    public bool? Reportable { get; set; }
    public DateOnly? ReportingDueDate { get; set; }
    public string ReportingNote { get; set; } = "";
    public string RootCause { get; set; } = "";
    public DateOnly? InvestigationDate { get; set; }
    public string Investigator { get; set; } = "";
    public List<ActionItem> Actions { get; set; } = [];
    public List<ActionItem> Effectiveness { get; set; } = [];
    public string ResponseDelayReason { get; set; } = "";
    public string ExtensionReason { get; set; } = "";
    public DateOnly? RequestedDueDate { get; set; }
    public DateOnly? ApprovedExtensionDueDate { get; set; }
    public bool ExtensionPending { get; set; }
    public bool ResponseDelayPending { get; set; }
    public bool ResponseDelayApproved { get; set; }
    public List<AuditEntry> History { get; set; } = [];
    public List<AttachmentInfo> Attachments { get; set; } = [];
    public bool HasReportingFlag => new[] { HealthHazard, ManufacturerClaim, LegislativeRequirement, IntendedPurpose }.Any(x => x == true);
}
public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public string FromStage { get; set; } = "";
    public string ToStage { get; set; } = "";
}
public sealed class AttachmentInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public Guid UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed record StoredAttachment(Guid ComplaintId, AttachmentInfo Info, byte[] Bytes);
public sealed class ConcurrencyConflictException : Exception { public ConcurrencyConflictException() : base("This complaint changed since you opened it. Reload the page before trying again.") { } }
public sealed class WorkflowException(string message) : Exception(message);

public static class Ui
{
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);
    public static DateTimeOffset EndOfDay(DateOnly date) => new(date.ToDateTime(new TimeOnly(17, 0)), TimeSpan.FromHours(8));
    public static string Date(DateTimeOffset? date) => date is null ? "Not set" : TimeZoneInfo.ConvertTime(date.Value, Zone).ToString("dd MMM yyyy, HH:mm");
    public static string StageName(Stage stage) => stage switch
    {
        Stage.Draft => "Draft", Stage.Evaluation => "Pending evaluation", Stage.Investigation => "Investigation & action plan",
        Stage.PlanHod => "Action plan · HOD approval", Stage.PlanQm => "Action plan · QM approval", Stage.Actions => "Actions in progress",
        Stage.CompletionHod => "Completed actions · HOD review", Stage.CompletionQm => "Completed actions · QM review",
        Stage.Effectiveness => "Effectiveness monitoring", Stage.ClosureQm => "Closure · QM review", Stage.ClosureCm => "Closure · CM approval", _ => "Closed"
    };
    public static string TaskRole(Stage stage) => stage switch
    {
        Stage.Draft => Roles.Requestor, Stage.Evaluation or Stage.PlanQm or Stage.CompletionQm or Stage.Effectiveness or Stage.ClosureQm => Roles.Qm,
        Stage.Investigation or Stage.Actions => Roles.Owner, Stage.PlanHod or Stage.CompletionHod => Roles.Hod, Stage.ClosureCm => Roles.Cm, _ => ""
    };
    public static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])).ToUpperInvariant();
}
