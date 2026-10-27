using SmaComplaints.Models;
namespace SmaComplaints.Services;
public static class NotificationRouting
{
 public static List<NotificationDelivery> Complaint(Complaint c,AuditEntry e,Complaint? before=null)
 {
  var result=new List<NotificationDelivery>();var prefix=$"complaint:{c.Id}:{e.Id}";
  void Add(Guid? id,string kind,string title,string tab,string? role=null)=>result.Add(new(id,role,e.ActorId,prefix+":"+kind,title,$"{c.Reference} · {e.ActorName} · {Ui.StageName(c.Stage)}",$"/Complaints/Details/{c.Id}?tab={tab}&from=inbox"+(kind=="handoff"?"#workflow-task":"#complaint-form"),e.At));
  if(e.FromStage!=e.ToStage){var closed=c.Stage==Stage.Closed;var target=closed?c.CreatedBy:new Workflow().Assignee(c);Add(target,"handoff",closed?"Complaint closed":e.Action.StartsWith("Returned",StringComparison.OrdinalIgnoreCase)?"Returned for correction":"New workflow task",c.Stage==Stage.Draft?"notification":c.Stage is Stage.Effectiveness or Stage.ClosureQm or Stage.ClosureCm or Stage.Closed?"effectiveness":"processing",c.Stage==Stage.Evaluation&&target==null?Roles.Qm:null);}
  if(e.Action is "Extension requested" or "Response delay approval requested")Add(c.HodId,"exception",e.Action,"processing");
  if(e.Action.StartsWith("extension ")||e.Action.StartsWith("delay "))Add(c.OwnerId,"exception-decision",e.Action,"processing");
  if(e.Action=="Reporting review confirmed")Add(c.OwnerId,"reporting-done","QM reporting review completed","processing");
  bool Ready(Complaint? value)=>value is not null&&value.Stage==Stage.Investigation&&!value.ReportingReviewed&&(value.Category=="Non-Quality"||(value.Category=="Quality"&&new[]{value.HealthHazard,value.ManufacturerClaim,value.LegislativeRequirement,value.IntendedPurpose}.All(x=>x.HasValue)));
  if(Ready(c)&&!Ready(before))Add(c.QmId,"reporting-ready","Reporting review ready","processing");
  return result;
 }
 public static List<NotificationDelivery> Form(FormEntry e)
 {
  var h=e.History.LastOrDefault();if(h is null||h.Action is not("Forwarded" or "Returned for correction" or "Completed"))return [];
  var recipient=e.State=="Completed"||e.Step==0?e.CreatedBy:e.Assignees.GetValueOrDefault(e.Step);
  return [new(recipient,null,h.ActorId,$"form:{e.Id}:{h.Id}",h.Action=="Completed"?"Form record completed":h.Action=="Forwarded"?"New form task":"Form returned for correction",$"{e.Reference} · {e.Schema.Name} · {h.Actor}",$"/Forms/Entry/{e.Id}",h.At)];
 }
}
