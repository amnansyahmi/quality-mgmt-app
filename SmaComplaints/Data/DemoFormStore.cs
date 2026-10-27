using System.Text.Json;
using SmaComplaints.Models;
namespace SmaComplaints.Data;
public sealed class DemoFormStore(DemoNotificationStore? notifications=null,IComplaintStore? people=null):IFormStore
{
 readonly object gate=new();readonly Dictionary<Guid,FormDefinition> definitions=[];readonly Dictionary<Guid,byte[]> sources=[];readonly Dictionary<Guid,FormEntry> entries=[];readonly Dictionary<Guid,byte[]> files=[];
 static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
 public Task<List<FormSummary>> SummariesAsync(bool drafts=false){lock(gate)return Task.FromResult(definitions.Values.Where(x=>drafts||x.State=="Published").OrderBy(x=>x.Name).Select(x=>new FormSummary(x.Id,x.Name,x.State,x.Revision,x.Fields.Count,x.Steps.Count,x.SourceName)).ToList());}
 public Task<FormDefinition?> DefinitionAsync(Guid id){lock(gate)return Task.FromResult(definitions.TryGetValue(id,out var value)?Copy(value):null);}
 public Task SaveDefinitionAsync(FormDefinition d,int expected,byte[]? source=null){lock(gate){if(definitions.TryGetValue(d.Id,out var old)?old.Revision!=expected:expected!=0)throw new ConcurrencyConflictException();if(d.State=="Published"&&definitions.Values.Any(x=>x.Id!=d.Id&&x.State=="Published"&&x.Name.Equals(d.Name,StringComparison.OrdinalIgnoreCase)))throw new WorkflowException("A published module already has this name.");d.Revision=expected+1;definitions[d.Id]=Copy(d);if(source is not null)sources[d.Id]=source.ToArray();return Task.CompletedTask;}}
 public Task<byte[]?> SourceAsync(Guid id){lock(gate)return Task.FromResult(sources.TryGetValue(id,out var b)?b.ToArray():null);}
 public Task<List<FormEntry>> EntriesAsync(Guid id,AppUser user){lock(gate)return Task.FromResult(entries.Values.Where(x=>x.DefinitionId==id&&Services.FormRuntime.CanView(x,user)).OrderByDescending(x=>x.UpdatedAt).Take(500).Select(Copy).ToList());}
 public Task<FormEntry?> EntryAsync(Guid id){lock(gate)return Task.FromResult(entries.TryGetValue(id,out var value)?Copy(value):null);}
 public async Task SaveEntryAsync(FormEntry e,int expected,IReadOnlyList<ModuleBlob>? uploads=null){var users=people is null?new List<AppUser>():await people.UsersAsync();lock(gate){if(entries.TryGetValue(e.Id,out var old)?old.Revision!=expected:expected!=0)throw new ConcurrencyConflictException();notifications?.Publish(Services.NotificationRouting.Form(e),users);e.Revision=expected+1;e.UpdatedAt=DateTimeOffset.UtcNow;entries[e.Id]=Copy(e);foreach(var f in uploads??[])files[f.Id]=f.Bytes.ToArray();}}
 public Task<List<FormEntry>> TasksAsync(AppUser user){lock(gate)return Task.FromResult(entries.Values.Where(x=>Services.FormRuntime.CanAct(x,user)).OrderByDescending(x=>x.UpdatedAt).Select(Copy).ToList());}
 public Task<byte[]?> FileAsync(Guid id){lock(gate)return Task.FromResult(files.TryGetValue(id,out var b)?b.ToArray():null);}
}
