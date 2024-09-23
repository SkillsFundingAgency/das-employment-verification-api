CREATE TABLE [dbo].[LogError]
(
  LogErrorId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_LogError PRIMARY KEY,
  PipelineId UNIQUEIDENTIFIER,
  ActivityName VARCHAR(255),
  ErrorMessage NVARCHAR(4000),
  CreatedOn DATETIME2(7) CONSTRAINT DF_LogError_CreatedOn DEFAULT GETDATE(),
  CONSTRAINT FK_LogError_PipelineId FOREIGN KEY (PipelineId)
        REFERENCES dbo.LogETLJob(PipelineId)
);
GO