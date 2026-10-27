using SmaComplaints.Models;
namespace SmaComplaints.Data;
public interface IFormStore
{
 Task<List<FormSummary>> SummariesAsync(bool drafts=false);Task<FormDefinition?> DefinitionAsync(Guid id);Task SaveDefinitionAsync(FormDefinition definition,int expected,byte[]? source=null);Task<byte[]?> SourceAsync(Guid id);Task<List<FormEntry>> EntriesAsync(Guid definitionId,AppUser user);Task<FormEntry?> EntryAsync(Guid id);Task SaveEntryAsync(FormEntry entry,int expected,IReadOnlyList<ModuleBlob>? files=null);Task<byte[]?> FileAsync(Guid id);Task<List<FormEntry>> TasksAsync(AppUser user);
}
