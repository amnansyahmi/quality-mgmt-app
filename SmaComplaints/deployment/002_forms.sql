SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.FormDefinitions',N'U') IS NULL
CREATE TABLE dbo.FormDefinitions(Id uniqueidentifier NOT NULL PRIMARY KEY,Name nvarchar(150) NOT NULL,State varchar(20) NOT NULL,Revision int NOT NULL,Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1),SourceName nvarchar(255) NOT NULL,SourceBytes varbinary(max) NULL);
IF OBJECT_ID(N'dbo.FormEntries',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.FormEntries(Id uniqueidentifier NOT NULL PRIMARY KEY,DefinitionId uniqueidentifier NOT NULL REFERENCES dbo.FormDefinitions(Id),CreatedBy uniqueidentifier NOT NULL REFERENCES dbo.AppUsers(Id),Revision int NOT NULL,UpdatedAt datetimeoffset NOT NULL,Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1));
 CREATE INDEX IX_FormEntries_Module ON dbo.FormEntries(DefinitionId,UpdatedAt);
END;
IF OBJECT_ID(N'dbo.FormFiles',N'U') IS NULL
CREATE TABLE dbo.FormFiles(Id uniqueidentifier NOT NULL PRIMARY KEY,EntryId uniqueidentifier NOT NULL REFERENCES dbo.FormEntries(Id),Content varbinary(max) NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.FormDefinitions') AND name='UX_FormDefinitions_PublishedName')
CREATE UNIQUE INDEX UX_FormDefinitions_PublishedName ON dbo.FormDefinitions(Name) WHERE State='Published';
COMMIT TRANSACTION;
