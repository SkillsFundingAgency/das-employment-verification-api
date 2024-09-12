CREATE TABLE [dbo].[LogError]
(
  LogErrorId BIGINT NOT NULL Identity(1,1) Constraint PK_LogError PRIMARY KEY,
  PipelineId UNIQUEIDENTIFIER,
  ActivityName VARCHAR(200),
  ErrorMessage NVARCHAR(400),
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  CONSTRAINT FK_LogError_PipelineId FOREIGN KEY (PipelineId)
        REFERENCES dbo.LogETLJob(PipelineId)
);
GO