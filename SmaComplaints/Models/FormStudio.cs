namespace SmaComplaints.Models;
public sealed class FormDefinition
{
 public Guid Id{get;set;}=Guid.NewGuid();public int Revision{get;set;}public string Name{get;set;}="Untitled form";public string State{get;set;}="Draft";public Guid? PublishedBy{get;set;}public string PublishedByName{get;set;}="";public DateTimeOffset? PublishedAt{get;set;}public Guid CreatedBy{get;set;}public DateTimeOffset CreatedAt{get;set;}=DateTimeOffset.UtcNow;public string SourceName{get;set;}="";public string SourceText{get;set;}="";public string SourceDigest{get;set;}="";public string ExtractionMethod{get;set;}="";public int Pages{get;set;}public List<string> Warnings{get;set;}=[];public List<string> SubmitRoles{get;set;}=[Roles.Requestor];public List<FormField> Fields{get;set;}=[];public List<FormStep> Steps{get;set;}=[];
}
public sealed class FormField
{
 public string Key{get;set;}="f_"+Guid.NewGuid().ToString("N")[..10];public string Label{get;set;}="";public string Section{get;set;}="General";public string Type{get;set;}="Text";public bool Required{get;set;}public int Stage{get;set;}public string Options{get;set;}="";public string Columns{get;set;}="";public string Evidence{get;set;}="Added manually";
}
public sealed class FormStep
{
 public string Name{get;set;}="Review";public string Role{get;set;}=Roles.Qm;public string Kind{get;set;}="Approval";public string ReturnTo{get;set;}="0";public string Evidence{get;set;}="Proposed; confirm the routing";
}
public sealed record FormSummary(Guid Id,string Name,string State,int Revision,int FieldCount,int StepCount,string SourceName);
public sealed class FormEntry
{
 public Guid Id{get;set;}=Guid.NewGuid();public Guid DefinitionId{get;set;}public string Reference{get;set;}="";public int Revision{get;set;}public FormDefinition Schema{get;set;}=new();public Guid CreatedBy{get;set;}public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public string State{get;set;}="Draft";public int Step{get;set;}public Dictionary<int,Guid> Assignees{get;set;}=[];public Dictionary<string,string> Values{get;set;}=[];public Dictionary<string,List<Dictionary<string,string>>> Tables{get;set;}=[];public List<ModuleFile> Files{get;set;}=[];public List<ModuleEvent> History{get;set;}=[];
}
public sealed class ModuleEvent{public Guid Id{get;set;}=Guid.NewGuid();public DateTimeOffset At{get;set;}=DateTimeOffset.UtcNow;public Guid ActorId{get;set;}public string Actor{get;set;}="";public string Action{get;set;}="";public string Note{get;set;}="";public int From{get;set;}public int To{get;set;}}
public sealed class ModuleFile{public Guid Id{get;set;}=Guid.NewGuid();public string FieldKey{get;set;}="";public string Name{get;set;}="";public long Size{get;set;}}
public sealed record ModuleBlob(Guid Id,byte[] Bytes);
public sealed class StudioVm{public List<FormSummary> Forms{get;set;}=[];public FormDefinition Definition{get;set;}=new();public List<AppUser> Users{get;set;}=[];public string? Error{get;set;}}
public sealed class FormModuleVm{public FormDefinition Definition{get;set;}=new();public FormEntry? Entry{get;set;}public List<FormEntry> Entries{get;set;}=[];public List<AppUser> Users{get;set;}=[];public AppUser User{get;set;}=new();public bool Preview{get;set;}}
