namespace SmaComplaints.Models;

public sealed class WorkflowBoardModel
{
    public string UserName { get; set; }="";
    public string Role { get; set; }="";
    public string InitialView { get; set; }="overview";
    public string Query { get; set; }="";
    public string StageFilter { get; set; }="";
    public string AntiforgeryToken { get; set; }="";
    public List<WorkflowRow> Rows { get; set; }=[];
    public static WorkflowBoardModel From(DashboardVm vm,string token)
    {
        var flow=new Services.Workflow();
        return new(){UserName=vm.User.Name,Role=vm.User.Role,InitialView=vm.View,Query=vm.Query,StageFilter=vm.Status,AntiforgeryToken=token,
            Rows=vm.Complaints.Select(c=>new WorkflowRow{Id=c.Id,Reference=c.Reference,Subject=c.Subject==""?"Untitled complaint":c.Subject,Customer=c.CustomerName==""?"Customer not entered":c.CustomerName,Hospital=c.Hospital,Stage=c.Stage,Owner=vm.Users.FirstOrDefault(x=>x.Id==flow.Assignee(c))?.Name??(c.Stage==Stage.Evaluation?"QM queue":"Unassigned"),TaskRole=Ui.TaskRole(c.Stage),DueAt=c.DueAt,CreatedAt=c.CreatedAt,UpdatedAt=c.UpdatedAt,Department=c.Department,Mine=flow.HasWork(c,vm.User),Overdue=Services.Workflow.IsOverdue(c),ReportingPending=c.Stage==Stage.Investigation&&!c.ReportingReviewed,ExtensionPending=c.ExtensionPending||c.ResponseDelayPending,ReportingFlag=c.HasReportingFlag,Approval=c.Stage is Stage.PlanHod or Stage.PlanQm or Stage.CompletionHod or Stage.CompletionQm or Stage.ClosureQm or Stage.ClosureCm}).ToList()};
    }
}
public sealed class WorkflowRow
{
    public Guid Id { get; set; }
    public string Reference { get; set; }="";
    public string Subject { get; set; }="";
    public string Customer { get; set; }="";
    public string Hospital { get; set; }="";
    public Stage Stage { get; set; }
    public string Owner { get; set; }="";
    public string TaskRole { get; set; }="";
    public string Department { get; set; }="";
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool Mine { get; set; }
    public bool Overdue { get; set; }
    public bool Approval { get; set; }
    public bool ReportingPending { get; set; }
    public bool ReportingFlag { get; set; }
    public bool ExtensionPending { get; set; }
}
