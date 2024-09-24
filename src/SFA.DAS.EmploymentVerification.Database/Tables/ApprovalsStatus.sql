CREATE TABLE [dbo].[ApprovalsStatus]
(
  ApprovalsStatusId SMALLINT NOT NULL Identity(1,1) CONSTRAINT PK_ApprovalsStatus PRIMARY KEY,
  Approvals VARCHAR(100)
)