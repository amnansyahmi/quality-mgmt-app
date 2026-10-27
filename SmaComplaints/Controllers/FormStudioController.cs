using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Controllers;
[Authorize(Roles=Roles.Admin)]
public sealed class FormStudioController(IComplaintStore people,Workflow flow,IFormStore forms,DocumentFormImporter importer):AppController(people,flow)
{
 public async Task<IActionResult> Index(){ViewData["ActiveNav"]="studio";try{return View(new StudioVm{Forms=await forms.SummariesAsync(true)});}catch(WorkflowException ex){return View(new StudioVm{Error=ex.Message});}}
 [HttpPost,RequestSizeLimit(22_000_000)]
 public async Task<IActionResult> Import(IFormFile? source)
 {
  try{FormRuntime.Require(source is not null&&source.Length is >0 and <=20_971_520,"Choose a DOCX, PDF, PNG or JPG file up to 20 MB.");using var memory=new MemoryStream();await source!.CopyToAsync(memory);var bytes=memory.ToArray();var d=await importer.ImportAsync(source.FileName,bytes,(await Actor()).Id,HttpContext.RequestAborted);await forms.SaveDefinitionAsync(d,0,bytes);TempData["Success"]="Import draft created. Review every field, repeating table and workflow before publishing.";return RedirectToAction("Edit",new{id=d.Id});}
  catch(Exception ex)when(ex is WorkflowException or InvalidDataException or System.Xml.XmlException or IOException or Tesseract.TesseractException or DllNotFoundException or BadImageFormatException or UglyToad.PdfPig.Core.PdfDocumentFormatException or TypeInitializationException){TempData["Error"]=ex is WorkflowException?ex.Message:"The document could not be read by the local extraction/OCR engine. Check the file and OCR runtime/language data, then try again.";return RedirectToAction("Index");}
 }
 public async Task<IActionResult> Edit(Guid id){ViewData["ActiveNav"]="studio";var d=await forms.DefinitionAsync(id);return d is null?NotFound():View(new StudioVm{Definition=d});}
 [HttpPost,RequestFormLimits(ValueCountLimit=10000),RequestSizeLimit(3_000_000)]
 public async Task<IActionResult> Save(Guid id,int revision,FormDefinition input,string command,bool reviewed)
 {
  var d=await forms.DefinitionAsync(id);if(d is null)return NotFound();var originalState=d.State;var evidence=d.Fields.ToDictionary(x=>x.Key,x=>x.Evidence);
  try{
   FormRuntime.Require(d.Revision==revision,"This design changed. Reload it before saving.");FormRuntime.Require(d.State=="Draft","Published definitions are locked; upload a new form for a separate reviewed module.");FormRuntime.Require(ModelState.IsValid,"Check all field and workflow values.");
   d.Name=(input.Name??"").Trim();d.SubmitRoles=input.SubmitRoles??[];d.Fields=input.Fields??[];d.Steps=input.Steps??[];
   foreach(var f in d.Fields){if(string.IsNullOrWhiteSpace(f.Key))f.Key="f_"+Guid.NewGuid().ToString("N")[..10];f.Evidence=evidence.GetValueOrDefault(f.Key,"Added manually");f.Label=(f.Label??"").Trim();f.Section=f.Section??"General";f.Options=f.Options??"";f.Columns=f.Columns??"";}
   foreach(var step in d.Steps){step.Name=step.Name??"";step.Role=step.Role??"";step.Kind=step.Kind??"";step.ReturnTo=step.ReturnTo??"";}
   FormRuntime.ValidateDefinition(d);
   if(command=="publish"){FormRuntime.Require(reviewed,"Confirm that you have reviewed the extracted fields, tables, permissions and workflow.");FormRuntime.Require(!string.IsNullOrWhiteSpace(d.SourceText),"A readable source is required before publishing.");FormRuntime.Require(!(await forms.SummariesAsync()).Any(x=>x.Id!=d.Id&&x.Name.Equals(d.Name,StringComparison.OrdinalIgnoreCase)),"A published module already has this name. Choose a distinct name.");d.State="Published";var publisher=await Actor();d.PublishedBy=publisher.Id;d.PublishedByName=publisher.Name;d.PublishedAt=DateTimeOffset.UtcNow;}
   else FormRuntime.Require(command=="save","Unknown design command.");
   await forms.SaveDefinitionAsync(d,revision);TempData["Success"]=d.State=="Published"?$"{d.Name} is published. Its menu and record workspace are now available.":"Design saved.";
   return d.State=="Published"?RedirectToAction("Module","Forms",new{id=d.Id}):RedirectToAction("Edit",new{id});
  }catch(Exception ex)when(ex is WorkflowException or ConcurrencyConflictException){d.State=originalState;d.Revision=revision;ViewData["ActiveNav"]="studio";return View("Edit",new StudioVm{Definition=d,Error=ex.Message});}
 }
 public async Task<IActionResult> Source(Guid id){var d=await forms.DefinitionAsync(id);if(d is null)return NotFound();var b=await forms.SourceAsync(id);return b is null?NotFound():File(b,"application/octet-stream",d.SourceName);}
 public async Task<IActionResult> Preview(Guid id){var d=await forms.DefinitionAsync(id);if(d is null)return NotFound();ViewData["ActiveNav"]="studio";return View("~/Views/Forms/Entry.cshtml",new FormModuleVm{Definition=d,Entry=new(){DefinitionId=d.Id,Schema=d,CreatedBy=(await Actor()).Id,Reference="Design preview"},User=await Actor(),Users=await Store.UsersAsync(),Preview=true});}
}
