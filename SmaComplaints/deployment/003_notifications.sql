IF OBJECT_ID('dbo.AppNotifications','U') IS NULL
BEGIN
 CREATE TABLE dbo.AppNotifications(Id uniqueidentifier NOT NULL PRIMARY KEY,RecipientId uniqueidentifier NOT NULL REFERENCES dbo.AppUsers(Id),EventKey nvarchar(200) NOT NULL,Title nvarchar(200) NOT NULL,Body nvarchar(600) NOT NULL,Url nvarchar(400) NOT NULL,At datetimeoffset NOT NULL,ReadAt datetimeoffset NULL);
 CREATE UNIQUE INDEX UX_NotificationEventRecipient ON dbo.AppNotifications(RecipientId,EventKey);
 CREATE INDEX IX_NotificationsRecipientReadAt ON dbo.AppNotifications(RecipientId,ReadAt,At DESC);
END;
