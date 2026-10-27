SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.AppUsers',N'U') IS NULL
CREATE TABLE dbo.AppUsers (
 Id uniqueidentifier NOT NULL PRIMARY KEY, Name nvarchar(150) NOT NULL, Email nvarchar(254) NOT NULL,
 Role varchar(20) NOT NULL CHECK(Role IN ('Requestor','Owner','HOD','QM','CM','Admin')),
 Department nvarchar(150) NOT NULL, PasswordHash nvarchar(1000) NOT NULL, Active bit NOT NULL DEFAULT 1,
 CONSTRAINT UQ_AppUsers_Email UNIQUE(Email)
);
IF OBJECT_ID(N'dbo.Complaints',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Complaints (
 Id uniqueidentifier NOT NULL PRIMARY KEY, Reference varchar(40) NOT NULL UNIQUE,
 Subject nvarchar(300) NOT NULL, CustomerName nvarchar(200) NOT NULL, Stage varchar(30) NOT NULL,
 CreatedBy uniqueidentifier NOT NULL REFERENCES dbo.AppUsers(Id),
 OwnerId uniqueidentifier NULL REFERENCES dbo.AppUsers(Id), HodId uniqueidentifier NULL REFERENCES dbo.AppUsers(Id),
 QmId uniqueidentifier NULL REFERENCES dbo.AppUsers(Id), CmId uniqueidentifier NULL REFERENCES dbo.AppUsers(Id),
 DueAt datetimeoffset NULL, CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL,
 Revision int NOT NULL, Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1)
 );
 CREATE INDEX IX_Complaints_StageDue ON dbo.Complaints(Stage,DueAt) INCLUDE(OwnerId,HodId,QmId,CmId);
 CREATE INDEX IX_Complaints_CreatedBy ON dbo.Complaints(CreatedBy,UpdatedAt);
END;
IF OBJECT_ID(N'dbo.AuditEvents',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.AuditEvents (
 Id uniqueidentifier NOT NULL PRIMARY KEY, ComplaintId uniqueidentifier NOT NULL REFERENCES dbo.Complaints(Id),
 At datetimeoffset NOT NULL, ActorId uniqueidentifier NOT NULL REFERENCES dbo.AppUsers(Id),
 Action nvarchar(150) NOT NULL, Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1)
 );
 CREATE INDEX IX_AuditEvents_Complaint ON dbo.AuditEvents(ComplaintId,At);
END;
IF OBJECT_ID(N'dbo.Attachments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Attachments (
 Id uniqueidentifier NOT NULL PRIMARY KEY, ComplaintId uniqueidentifier NOT NULL REFERENCES dbo.Complaints(Id),
 Name nvarchar(255) NOT NULL, Size bigint NOT NULL CHECK(Size BETWEEN 1 AND 10485760), ContentType nvarchar(150) NOT NULL,
 UploadedBy uniqueidentifier NOT NULL REFERENCES dbo.AppUsers(Id), UploadedAt datetimeoffset NOT NULL, Content varbinary(max) NOT NULL
 );
 CREATE INDEX IX_Attachments_Complaint ON dbo.Attachments(ComplaintId);
END;
IF OBJECT_ID(N'dbo.MailOutbox',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MailOutbox(
 Id uniqueidentifier NOT NULL PRIMARY KEY, DedupKey varchar(150) NOT NULL UNIQUE,
 Recipient nvarchar(254) NOT NULL, Subject nvarchar(500) NOT NULL, Body nvarchar(max) NOT NULL,
 CreatedAt datetimeoffset NOT NULL, AvailableAt datetimeoffset NOT NULL, SentAt datetimeoffset NULL,
 LockedUntil datetimeoffset NULL, LeaseId uniqueidentifier NULL, Attempts int NOT NULL DEFAULT 0,
 LastError nvarchar(1000) NULL
 );
 CREATE INDEX IX_MailOutbox_Pending ON dbo.MailOutbox(SentAt,AvailableAt,LockedUntil);
END;
COMMIT TRANSACTION;
