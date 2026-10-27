# Form Studio

An administrator opens Form Studio, uploads DOCX/PDF/PNG/JPG (up to 20 MB; PDF up to 20 pages), reviews the generated draft, and publishes a module. The proposed name comes from the filename and is editable. Publishing adds a named menu entry and record workspace.

## Import and review

DOCX text/tables and embedded PDF text are read locally. Image-only PDF pages and PNG/JPG scans use local Tesseract English OCR; language data is included. No document content is sent to an external AI/OCR service. Handwriting, marked checkboxes, complex tables and poor scans require manual verification. The original source and extracted text remain available to administrators.

Proposals use local rules, not an LLM. They may omit or split labels, and approval mentions can include optional branches. Review labels, types, required flags, repeating columns, stage ownership and the sequence. Conditional branches, calculated fields, regulatory/business rules and qualified electronic signatures are not automatically generated. The existing complaint module retains its separately implemented reportability and approval rules.

## Fields and workflow

Supported fields: text, multiline text, number, date, choice, multiple choice, yes/no, repeating table and evidence attachment. Choices use | separators. Table columns use one Label|text, Label|number or Label|date per line. Step 0 is the submitter. Later fields are editable only by the assigned task holder at that step.

Add Form steps for data preparation and Approval steps for decisions. Each step uses an existing application role and a configured list of earlier return destinations. Select an active named person in the next required role when forwarding. Tasks appear in the shared inbox. Return requires a reason; resubmission repeats the configured sequence. Required/type validation applies when forwarding; progress can be saved. Evidence limits: 10 MB per file, 20 files and 50 MB per record.

Only configured submitter roles can start records. Other users see records only as creator, named participant or administrator. Records capture a schema snapshot. Published definitions are locked. This version creates separate modules for revised forms instead of migrating in-flight records. Publishing records the administrator and time.

## SQL Server and runtime

The development demo uses an in-memory store that resets on restart. Production uses the existing configurable SQL Server connection. Run dotnet SmaComplaints.dll --initialize-database with DemoMode disabled to apply 001_schema.sql and 002_forms.sql. Existing installations need 002_forms.sql before using Form Studio. SQL stores definitions, sources, records and attachments; no executable code or per-upload SQL table is generated.

PDF/OCR was exercised on Windows with DOCX, text PDF, PNG and image-only PDF fixtures. NuGet restores PDFium/SkiaSharp and Tesseract assets. On Linux/macOS, install a compatible native Tesseract 5/Leptonica runtime and verify OCR there. Generic-module routing creates in-app tasks; the SMTP worker remains complaint-specific. Live SQL Server integration remains unverified in this session.
