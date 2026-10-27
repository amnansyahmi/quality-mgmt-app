using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Controllers;

[Authorize]
public abstract class AppController(IComplaintStore store, Workflow workflow) : Controller
{
    protected IComplaintStore Store=>store;
    protected Workflow Flow=>workflow;
    protected async Task<AppUser> Actor()=>await Store.UserAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))??throw new WorkflowException("Your account is unavailable.");
    protected async Task<Complaint> Load(Guid id)
    {
        var c=await Store.GetAsync(id)??throw new WorkflowException("Complaint not found.");
        Flow.Demand(Flow.CanView(c,await Actor()),"You do not have access to this complaint.");return c;
    }
    protected async Task Save(Complaint c,int revision,AppUser u,string action,string note="",Stage? from=null,StoredAttachment? attachment=null)
    {
        await Store.SaveAsync(c,revision,new(){ActorId=u.Id,ActorName=u.Name,ActorRole=u.Role,Action=action,Note=note,FromStage=(from??c.Stage).ToString(),ToStage=c.Stage.ToString()},attachment);
    }
    protected IActionResult Back(Guid id,string tab="notification")=>RedirectToAction("Details","Complaints",new{id,tab});
    protected async Task<IActionResult> Mutate(Guid id,int revision,string tab,Func<Complaint,AppUser,Task> action,string? successView=null)
    {
        try {var c=await Load(id);Flow.Demand(c.Revision==revision,"This record has changed. Reload it and try again.");await action(c,await Actor());TempData["Success"]??="Changes saved.";if(successView is not null)return RedirectToAction("Index","Complaints",new{view=successView});}
        catch(Exception ex)when(ex is WorkflowException or ConcurrencyConflictException){TempData["Error"]=ex.Message;}
        return Back(id,tab);
    }
}
