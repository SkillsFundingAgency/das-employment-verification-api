CREATE TABLE [dbo].[LogError]
(
  LogErrorId BIGINT NOT NULL Identity(1,1) Constraint PK_LogError PRIMARY KEY,
  PipelineId NVARCHAR(100),
  ActivityName VARCHAR(200),
  ErrorMessage NVARCHAR(400),
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO