using System.Globalization;
using System.Text.RegularExpressions;
using SmaComplaints.Models;
namespace SmaComplaints.Services;
public sealed class FormRuntime
{
 public static readonly string[] Types=["Text","Multiline","Number","Date","Choice","MultiChoice","YesNo","Table","Attachment"];
 public static bool CanView(FormEntry e,AppUser u)=>u.Active&&(u.Role==Roles.Admin||e.CreatedBy==u.Id||e.Assignees.Values.Contains(u.Id));
 public static bool CanAct(FormEntry e,AppUser u)=>u.Active&&e.State!="Completed"&&(e.Step==0?e.CreatedBy==u.Id&&e.Schema.SubmitRoles.Contains(u.Role):e.Step<=e.Schema.Steps.Count&&e.Assignees.GetValueOrDefault(e.Step)==u.Id&&e.Schema.Steps[e.Step-1].Role==u.Role);
 public static string StepName(FormEntry e)=>e.State=="Completed"?"Completed":e.Step==0?"Prepare & submit":e.Schema.Steps[e.Step-1].Name;
 public static Guid? Assignee(FormEntry e)=>e.State=="Completed"?null:e.Step==0?e.CreatedBy:e.Assignees.GetValueOrDefault(e.Step);
 public static string[] Choices(FormField f)=>f.Options.Split('|',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
 public static (string Label,string Type,string Key)[] Columns(FormField f)=>f.Columns.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select((line,i)=>{var bits=line.Split('|',StringSplitOptions.TrimEntries);return(bits[0],bits.Length>1&&bits[1].Equals("date",StringComparison.OrdinalIgnoreCase)?"date":bits.Length>1&&bits[1].Equals("number",StringComparison.OrdinalIgnoreCase)?"number":"text","c"+i);}).ToArray();
 public static int[] ReturnTargets(FormEntry e)=>e.Step==0?[]:e.Schema.Steps[e.Step-1].ReturnTo.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x,out var n)?n:-1).Where(n=>n>=0&&n<e.Step).Distinct().ToArray();
 public static void ValidateDefinition(FormDefinition d)
 {
  Require(d.Name.Trim().Length is >0 and <=150,"Enter a module name up to 150 characters.");Require(d.Fields.Count is >0 and <=150,"Define between 1 and 150 fields.");Require(d.Steps.Count<=20,"Use at most 20 workflow steps.");Require(d.SubmitRoles.Count>0&&d.SubmitRoles.All(Roles.All.Contains),"Choose who may submit this form.");
  var keys=new HashSet<string>();foreach(var f in d.Fields){Require(Regex.IsMatch(f.Key,@"^f_[a-zA-Z0-9]{1,30}$")&&keys.Add(f.Key),"Every field needs a unique valid key.");Require(f.Label.Trim().Length is >0 and <=150,"Field labels must be 1–150 characters.");Require(Types.Contains(f.Type),"Choose a supported field type.");Require(f.Stage>=0&&f.Stage<=d.Steps.Count,"Assign each field to the submitter (0) or an existing step.");Require(f.Options.Length<=5000&&f.Columns.Length<=5000&&f.Section.Length<=150,"Field settings are too long.");if(f.Type is "Choice" or "MultiChoice")Require(Choices(f).Length is >0 and <=50,"Choice fields need 1–50 options separated by |.");if(f.Type=="Table")Require(Columns(f).Length is >0 and <=20,"A repeating table needs 1–20 columns, one label|type per line.");}
  for(var i=0;i<d.Steps.Count;i++){var s=d.Steps[i];Require(s.Name.Trim().Length is >0 and <=150&&Roles.All.Contains(s.Role)&&new[]{"Approval","Form"}.Contains(s.Kind),"Give each step a name, existing role and task type.");Require(s.ReturnTo.Length<=200,"The return-step list is too long.");Require(s.ReturnTo.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).All(x=>int.TryParse(x,out var n)&&n>=0&&n<i+1),"Return destinations must be earlier step numbers; 0 is the submitter.");}
 }
 public void Advance(FormEntry e,AppUser actor,AppUser? next,string note)
 {
  Require(CanAct(e,actor),"This task is not assigned to you.");ValidateValues(e);var from=e.Step;
  if(e.Step<e.Schema.Steps.Count){var step=e.Schema.Steps[e.Step];Require(next is not null&&next.Active&&next.Role==step.Role,$"Choose an active {step.Role} for the next task.");e.Step++;e.Assignees[e.Step]=next!.Id;e.State="Active";Record(e,actor,"Forwarded",$"To {next.Name} ({next.Role}). "+note,from);}
  else{Require(next is null,"The last step does not have a next recipient.");e.State="Completed";Record(e,actor,"Completed",note,from);}
 }
 public void Return(FormEntry e,AppUser actor,int target,string note)
 {
  Require(CanAct(e,actor)&&e.Step>0,"Only the current task holder may return this record.");Require(!string.IsNullOrWhiteSpace(note)&&note.Length<=4000,"Give a correction reason up to 4,000 characters.");Require(ReturnTargets(e).Contains(target),"Choose a return destination allowed by this workflow.");var from=e.Step;e.Step=target;e.State="Returned";Record(e,actor,"Returned for correction",note,from);
 }
 public static void ValidateValues(FormEntry e)
 {
  foreach(var f in e.Schema.Fields.Where(x=>x.Stage==e.Step))
  {
   var value=e.Values.GetValueOrDefault(f.Key,"");if(f.Type=="Attachment"){Require(!f.Required||e.Files.Any(x=>x.FieldKey==f.Key),$"Attach evidence for {f.Label}.");continue;}
   if(f.Type=="Table"){var rows=e.Tables.GetValueOrDefault(f.Key,[]);Require(!f.Required||rows.Count>0,$"Add at least one row for {f.Label}.");foreach(var row in rows)foreach(var col in Columns(f))CheckType(row.GetValueOrDefault(col.Key,""),col.Type,col.Label);continue;}
   Require(!f.Required||!string.IsNullOrWhiteSpace(value),$"Complete {f.Label}.");if(string.IsNullOrEmpty(value))continue;CheckType(value,f.Type,f.Label);
   if(f.Type=="Choice")Require(Choices(f).Contains(value),$"Choose an allowed option for {f.Label}.");if(f.Type=="MultiChoice")Require(value.Split('|').All(Choices(f).Contains),$"Choose allowed options for {f.Label}.");if(f.Type=="YesNo")Require(new[]{"Yes","No","Not applicable"}.Contains(value),$"Answer {f.Label}.");
  }
 }
 static void CheckType(string value,string type,string label){if(value=="")return;if(type.Equals("Date",StringComparison.OrdinalIgnoreCase))Require(DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),$"Enter a valid date for {label}.");if(type.Equals("Number",StringComparison.OrdinalIgnoreCase))Require(decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out _),$"Enter a valid number for {label}.");}
 public static void Record(FormEntry e,AppUser actor,string action,string note,int? from=null)=>e.History.Add(new(){ActorId=actor.Id,Actor=actor.Name,Action=action,Note=note,From=from??e.Step,To=e.Step});
 public static void Require(bool ok,string message){if(!ok)throw new WorkflowException(message);}
}
