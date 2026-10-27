using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SmaComplaints.Models;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using SkiaSharp;
namespace SmaComplaints.Services;
public sealed class DocumentFormImporter(IWebHostEnvironment environment)
{
 static readonly SemaphoreSlim OcrGate=new(1,1);
 public async Task<FormDefinition> ImportAsync(string name,byte[] bytes,Guid creator,CancellationToken cancellation=default)
 {
  WorkflowCheck(bytes.Length is >0 and <=20_971_520,"Upload a document no larger than 20 MB.");var ext=Path.GetExtension(name).ToLowerInvariant();WorkflowCheck(new[]{".docx",".pdf",".png",".jpg",".jpeg"}.Contains(ext),"Use DOCX, PDF, PNG or JPG.");
  var d=new FormDefinition{CreatedBy=creator,SourceName=Path.GetFileName(name),Name=Regex.Replace(Path.GetFileNameWithoutExtension(name),@"\s*(?:V(?:er)?\s*\d+|\(\d+\))\s*$","",RegexOptions.IgnoreCase).Trim(),SourceDigest=Convert.ToHexString(SHA256.HashData(bytes))};
  if(d.Name.Length>150)d.Name=d.Name[..150];WorkflowCheck(d.SourceName.Length<=255,"The source filename is too long.");var text=new StringBuilder();
  if(ext==".docx")
  {
   using var zip=new ZipArchive(new MemoryStream(bytes));WorkflowCheck(zip.Entries.Sum(x=>x.Length)<=60_000_000,"The expanded document is too large.");var doc=zip.GetEntry("word/document.xml")??throw new WorkflowException("This is not a readable DOCX document.");
   using var xml=XmlReader.Create(doc.Open(),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=20_000_000,XmlResolver=null});var root=XDocument.Load(xml);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
   foreach(var p in root.Descendants(w+"p")){var line=string.Concat(p.Descendants(w+"t").Select(x=>x.Value));if(!string.IsNullOrWhiteSpace(line))text.AppendLine(line);}
   d.Pages=0;d.ExtractionMethod="DOCX text and tables";d.Warnings.Add("DOCX page count depends on Word layout and is not calculated. Compare repeating tables with the original.");
  }
  else if(ext==".pdf")
  {
   if(!OperatingSystem.IsWindows()&&!OperatingSystem.IsLinux()&&!OperatingSystem.IsMacOS())throw new WorkflowException("Local PDF rendering is supported on Windows, Linux and macOS servers.");
   using var pdf=PdfDocument.Open(bytes);WorkflowCheck(pdf.NumberOfPages is >0 and <=20,"Import up to 20 PDF pages at a time.");d.Pages=pdf.NumberOfPages;var ocrPages=0;
   for(var i=1;i<=pdf.NumberOfPages;i++){cancellation.ThrowIfCancellationRequested();var page=pdf.GetPage(i);WorkflowCheck(page.Width*page.Height<4_000_000,"A PDF page is too large to render safely.");var content=ContentOrderTextExtractor.GetText(page);if(content.Count(char.IsLetterOrDigit)<30){using var bitmap=PDFtoImage.Conversion.ToImage(new MemoryStream(bytes),page:i-1,options:new PDFtoImage.RenderOptions(Dpi:150));using var encoded=bitmap.Encode(SKEncodedImageFormat.Png,100);content=await OcrAsync(encoded.ToArray(),cancellation);ocrPages++;}text.AppendLine($"[Page {i}]");text.AppendLine(content);}
   d.ExtractionMethod=ocrPages>0?$"PDF text + local OCR ({ocrPages} pages)":"PDF embedded text";if(ocrPages>0)d.Warnings.Add("OCR was used. Check field labels, dates, checkboxes and table structure against the source.");
  }
  else{d.Pages=1;d.ExtractionMethod="Local image OCR";text.Append(await OcrAsync(bytes,cancellation));d.Warnings.Add("OCR was used. Printed text is supported; handwriting and checkbox marks can be misread.");}
  d.SourceText=text.ToString();WorkflowCheck(d.SourceText.Length<=250_000,"The extracted text is too large. Split the source into smaller forms.");WorkflowCheck(d.SourceText.Count(char.IsLetterOrDigit)>=15,"No readable text was found. Use a clearer printed scan or a text-based document.");
  Propose(d);return d;
 }
 async Task<string> OcrAsync(byte[] bytes,CancellationToken cancellation)
 {
  using var imageData=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(imageData);WorkflowCheck(codec is not null&&codec.Info.Width*(long)codec.Info.Height<=40_000_000,"Use a readable image below 40 megapixels.");
  await OcrGate.WaitAsync(cancellation);try{return await Task.Run(()=>{using var engine=new TesseractEngine(Path.Combine(environment.ContentRootPath,"OcrData"),"eng",EngineMode.LstmOnly);using var image=Pix.LoadFromMemory(bytes);WorkflowCheck(image.Width*(long)image.Height<=40_000_000,"The scan is too large. Use an image below 40 megapixels.");using var page=engine.Process(image,PageSegMode.Auto);return page.GetText();},cancellation);}finally{OcrGate.Release();}
 }
 public static void Propose(FormDefinition d)
 {
  var section="General";var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var lines=d.SourceText.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(x=>Regex.Replace(x,@"\s+"," ").Trim()).Where(x=>x.Length>0).ToList();
  foreach(var line in lines)
  {
   if(Regex.IsMatch(line,@"^(Part\s+[A-Z]|\d+\.\d+\s)",RegexOptions.IgnoreCase)&&line.Length<130){section=line;continue;}
   var roleMatch=Regex.Match(line,@"(?:Approver|Reviewer).*?\(?\b(HOD|QM|CM)\b",RegexOptions.IgnoreCase);
   if(roleMatch.Success){var role=roleMatch.Groups[1].Value.ToUpperInvariant();d.Steps.Add(new(){Name=(section=="General"?"Review":section)+" · "+role,Role=role,Evidence=line});continue;}
   if(line.Contains("Signature",StringComparison.OrdinalIgnoreCase))continue;
   if(line.Contains('☐')||line.Contains('□'))
   {
    var segments=Regex.Split(line,"[☐□]");var label=segments[0].Trim(' ',':');if(label.Length<3&&d.Fields.Count>0)label=d.Fields[^1].Label;var options=segments.Skip(1).Select(x=>x.Trim(' ',':')).Where(x=>x.Length>0&&x.Length<100).ToList();
    if(label.Length>2&&label.Length<130&&options.Count>1){var type=options.All(x=>new[]{"Yes","No","Not Applicable"}.Contains(x,StringComparer.OrdinalIgnoreCase))?"YesNo":label.Contains("correction",StringComparison.OrdinalIgnoreCase)?"MultiChoice":"Choice";var previous=d.Fields.LastOrDefault(x=>x.Label.Equals(label,StringComparison.OrdinalIgnoreCase));if(previous is not null){previous.Type=type;previous.Options=string.Join("|",options);previous.Evidence+=" "+line;}else Add(label,type,string.Join("|",options),line);}
    continue;
   }
   if(Regex.IsMatch(line,@"^(No\.?\s+)?(Action|Check).*Responsible",RegexOptions.IgnoreCase)&&line.Length<220){var before=d.Fields.Count;Add(section+" entries","Table","",line);if(d.Fields.Count>before)d.Fields[^1].Columns="Action|text\nResponsible person|text\nDue date|date\nCompleted date|date\nEvidence|text";continue;}
   var chunks=Regex.Split(line,@"(?<=[:：])");if(chunks.Length>1){for(var i=0;i<chunks.Length-1;i++){var label=Regex.Replace(chunks[i],@"Click (?:here|or tap).*|_{2,}.*","",RegexOptions.IgnoreCase).Trim(' ',':','：');if(label.Length>2&&label.Length<120&&!Regex.IsMatch(label,@"^(SMA|Ref No|Date|By)$",RegexOptions.IgnoreCase))Add(label,Guess(label),"",line);}}
   else if(line.Length is >3 and <100&&!Regex.IsMatch(line,@"^(No\.?\s*\d|Click |\[Page|SMA-|Revision|Page |Note|Please|Whether|The |This )",RegexOptions.IgnoreCase)&&Regex.IsMatch(line,@"Name|Date|Description|Code|Category|Contact|Invoice|Root Cause|Reason|Subject|Expiry|Address|Department|Amount|Quantity|Email",RegexOptions.IgnoreCase))Add(line,Guess(line),"",line);
  }
  d.Warnings.Add("This is a local rule-based proposal, not a guaranteed complete transcription. Add, remove or correct fields and repeating-table columns before publishing.");
  d.Warnings.Add("Approval mentions are proposals, not a confirmed process. Repeated approvals or exception branches may need different steps. The initial runtime uses sequential steps with explicit returns; conditional business rules require separate implementation.");
  if(d.Steps.Count==0)d.Warnings.Add("No approval role was detected. Add workflow steps if review or approval is required.");
  if(d.Fields.Count==0)d.Warnings.Add("No reliable field labels were detected. Add fields manually using the extracted text and original source.");
  void Add(string label,string type,string options,string evidence){label=label.Trim();if(!seen.Add(label)||d.Fields.Count>=150)return;d.Fields.Add(new(){Label=label,Section=section,Type=type,Options=options,Required=label.Contains("compulsory",StringComparison.OrdinalIgnoreCase),Evidence=evidence});}
 }
 static string Guess(string label)=>Regex.IsMatch(label,"date|expiry",RegexOptions.IgnoreCase)?"Date":Regex.IsMatch(label,"description|reason|cause|detail",RegexOptions.IgnoreCase)?"Multiline":Regex.IsMatch(label,"amount|quantity|total",RegexOptions.IgnoreCase)?"Number":"Text";
 static void WorkflowCheck(bool condition,string message){if(!condition)throw new WorkflowException(message);}
}
