namespace SmaComplaints.Models;
public sealed record FieldVm(string Name,string Label,string? Value="",string Type="text",string[]? Options=null,bool Required=false,string? Help=null);
