using System.ComponentModel.DataAnnotations;
namespace SmaComplaints.Models;

public sealed class DashboardVm
{
    public AppUser User { get; set; } = new();
    public List<Complaint> Complaints { get; set; } = [];
    public string View { get; set; } = "overview";
    public string Query { get; set; } = "";
    public string Status { get; set; } = "";
    public List<AppUser> Users { get; set; } = [];
    public List<ComplaintTask> Tasks { get; set; } = [];
    public List<FormEntry> FormTasks { get; set; } = [];
}
public sealed class DetailVm
{
    public Complaint Complaint { get; set; } = new();
    public AppUser User { get; set; } = new();
    public List<AppUser> Users { get; set; } = [];
    public string Tab { get; set; } = "notification";
    public string? Error { get; set; }
}
public sealed class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed class UserEditVm
{
    public Guid? Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required] public string Role { get; set; } = Roles.Requestor;
    [Required, StringLength(150)] public string Department { get; set; } = "";
    [MinLength(12), DataType(DataType.Password)] public string? Password { get; set; }
    public bool Active { get; set; } = true;
}

public sealed record ComplaintTask(Complaint Complaint,string Kind,string Title,string ActionLabel,string Tab,bool Waiting);
public sealed record ComplaintListVm(List<Complaint> Rows,DashboardVm Page,bool ShowDeadline=false);
