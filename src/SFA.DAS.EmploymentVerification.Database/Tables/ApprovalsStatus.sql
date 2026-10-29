CREATE TABLE [dbo].[ApprovalsStatus]
(
  ApprovalsStatusId SMALLINT NOT NULL CONSTRAINT PK_ApprovalsStatus PRIMARY KEY,
  [Status] VARCHAR(100)
)