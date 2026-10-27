using SmaComplaints.Models;
namespace SmaComplaints.Services;
public static class QueueViews
{
    public static List<Complaint> Filter(DashboardVm page,IEnumerable<Complaint> rows)
    {
        if(!string.IsNullOrWhiteSpace(page.Query))rows=rows.Where(c=>$"{c.Reference} {c.Subject} {c.CustomerName} {c.Hospital}".Contains(page.Query,StringComparison.OrdinalIgnoreCase));
        if(Enum.TryParse<Stage>(page.Status,out var stage)&&Enum.IsDefined(stage))rows=rows.Where(c=>c.Stage==stage);
        return rows.ToList();
    }
    public static string Person(IEnumerable<AppUser> users,Guid? id,string fallback="Unassigned")=>users.FirstOrDefault(x=>x.Id==id)?.Name??fallback;
    public static string Deadline(Complaint c)=>!c.DueAt.HasValue?"No active deadline":Workflow.IsOverdue(c)?$"{Math.Max(1,(int)Math.Ceiling((DateTimeOffset.UtcNow-c.DueAt.Value).TotalDays))} day(s) overdue":$"Due {Ui.Date(c.DueAt).Split(',')[0]}";
}
