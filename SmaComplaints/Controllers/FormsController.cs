using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Controllers;
public sealed class FormsController(IComplaintStore people,Workflow workflow,IFormStore forms,FormRuntime runtime):AppController(people,workflow)
{
 public async Task<IActionResult> Index(){ViewData["ActiveNav"]="forms";return View(await forms.SummariesAsync());}
 public async Task<IActionResult> Module(Guid id){var d=await forms.DefinitionAsync(id);if(d is null||d.State!="Published")return NotFound();ViewData["ActiveNav"]="form-"+id;var u=await Actor();return View(new FormModuleVm{Definition=d,Entries=await forms.EntriesAsync(id,u),User=u,Users=await Store.UsersAsync()});}
 [HttpPost]
 public async Task<IActionResult> Create(Guid id){var d=await forms.DefinitionAsync(id);var u=await Actor();if(d is null||d.State!="Published")return NotFound();if(!d.SubmitRoles.Contains(u.Role))return Forbid();var snapshot=JsonSerializer.Deserialize<FormDefinition>(JsonSerializer.Serialize(d))!;snapshot.SourceText="";snapshot.Warnings=[];var e=new FormEntry{DefinitionId=id,Schema=snapshot,CreatedBy=u.Id,Reference=$"FRM-{Ui.Today.Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"};FormRuntime.Record(e,u,"Draft created","");await forms.SaveEntryAsync(e,0);return RedirectToAction("Entry",new{id=e.Id});}
 public async Task<IActionResult> Entry(Guid id){var e=await forms.EntryAsync(id);var u=await Actor();if(e is null||!FormRuntime.CanView(e,u))return NotFound();ViewData["ActiveNav"]="form-"+e.DefinitionId;return View(new FormModuleVm{Definition=e.Schema,Entry=e,User=u,Users=await Store.UsersAsync()});}
 [HttpPost,RequestFormLimits(ValueCountLimit=10000),RequestSizeLimit(23_000_000)]
 public async Task<IActionResult> Save(Guid id,int revision,string command,Guid? nextAssigneeId,int? returnTarget,string note)
 {
  var e=await forms.EntryAsync(id);var u=await Actor();if(e is null||!FormRuntime.CanView(e,u))return NotFound();
  try{
   FormRuntime.Require(ModelState.IsValid&&e.Revision==revision,"This record changed or a selected value is invalid. Reload before saving.");FormRuntime.Require(FormRuntime.CanAct(e,u),"This task is not assigned to you.");FormRuntime.Require((note??"").Length<=4000,"Keep decision notes to 4,000 characters.");var users=await Store.UsersAsync();var uploads=new List<ModuleBlob>();
   if(command=="return")
   {
    FormRuntime.Require(returnTarget.HasValue&&FormRuntime.ReturnTargets(e).Contains(returnTarget.Value),"Select a return destination allowed by this workflow.");var recipientId=returnTarget==0?e.CreatedBy:e.Assignees.GetValueOrDefault(returnTarget!.Value);FormRuntime.Require(users.Any(x=>x.Id==recipientId&&x.Active&&(returnTarget==0?e.Schema.SubmitRoles.Contains(x.Role):x.Role==e.Schema.Steps[returnTarget!.Value-1].Role)),"The return recipient must have an active account in the required role.");runtime.Return(e,u,returnTarget!.Value,note??"");
   }
   else
   {
    FormRuntime.Require(command is "save" or "advance","Unknown workflow command.");var data=await Request.ReadFormAsync();
    foreach(var f in e.Schema.Fields.Where(x=>x.Stage==e.Step))
    {
     if(f.Type=="Table"){var rows=new List<Dictionary<string,string>>();for(var row=0;row<20;row++){var cells=FormRuntime.Columns(f).ToDictionary(col=>col.Key,col=>data[$"t_{f.Key}_{row}_{col.Key}"].ToString().Trim());FormRuntime.Require(cells.Values.All(x=>x.Length<=4000),"Table cells must be at most 4,000 characters.");if(cells.Values.Any(x=>x!=""))rows.Add(cells);}e.Tables[f.Key]=rows;}
     else if(f.Type=="Attachment"){foreach(var file in data.Files.Where(x=>x.Name=="file_"+f.Key&&x.Length>0)){FormRuntime.Require(file.Length<=10_485_760&&e.Files.Count<20,"Evidence files are limited to 10 MB each and 20 per record.");FormRuntime.Require(new[]{".pdf",".png",".jpg",".jpeg",".docx",".xlsx",".csv",".txt",".zip"}.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()),"Unsupported evidence file type.");var info=new ModuleFile{FieldKey=f.Key,Name=Path.GetFileName(file.FileName),Size=file.Length};FormRuntime.Require(info.Name.Length<=255,"The filename is too long.");using var memory=new MemoryStream();await file.CopyToAsync(memory);uploads.Add(new(info.Id,memory.ToArray()));e.Files.Add(info);}FormRuntime.Require(e.Files.Sum(x=>x.Size)<=52_428_800,"Evidence is limited to 50 MB per record.");}
     else{var value=f.Type=="MultiChoice"?string.Join('|',data["v_"+f.Key].ToArray()):data["v_"+f.Key].ToString();FormRuntime.Require(value.Length<=8000,"Field values must be at most 8,000 characters.");e.Values[f.Key]=value.Trim();}
    }
    if(command=="advance")
    {
     try{runtime.Advance(e,u,nextAssigneeId.HasValue?users.FirstOrDefault(x=>x.Id==nextAssigneeId):null,note??"");}
     catch(WorkflowException ex){FormRuntime.Record(e,u,"Progress saved",$"Submission blocked: {ex.Message}");await forms.SaveEntryAsync(e,revision,uploads);throw new WorkflowException(ex.Message+" Your progress was saved.");}
    }
    else FormRuntime.Record(e,u,"Progress saved","");
   }
   await forms.SaveEntryAsync(e,revision,uploads);TempData["Success"]=e.State=="Completed"?"Record completed.":command=="return"?"Returned with the correction reason. The reference and history are retained.":command=="advance"?"Sent to the next task holder. The task is available in their inbox.":"Progress saved.";
  }catch(Exception ex)when(ex is WorkflowException or ConcurrencyConflictException){TempData["Error"]=ex.Message;}
  return RedirectToAction("Entry",new{id});
 }
 public async Task<IActionResult> Download(Guid id,Guid fileId){var e=await forms.EntryAsync(id);if(e is null||!FormRuntime.CanView(e,await Actor()))return NotFound();var info=e.Files.FirstOrDefault(x=>x.Id==fileId);if(info is null)return NotFound();var bytes=await forms.FileAsync(fileId);return bytes is null?NotFound():File(bytes,"application/octet-stream",info.Name);}
}
