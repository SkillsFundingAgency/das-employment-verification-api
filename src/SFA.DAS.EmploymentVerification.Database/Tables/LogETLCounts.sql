CREATE TABLE [dbo].[LogETLCounts]
(
  LogETLCountsId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_LogETLCounts PRIMARY KEY,
  PipelineId UNIQUEIDENTIFIER,
  ActivityName VARCHAR(255),
  ETLRowsAffected BIGINT,
  ETLActivityName VARCHAR(255),
  CreatedOn DATETIME2(7) CONSTRAINT DF_LogETLCounts_CreatedOn DEFAULT GETDATE(),
  CONSTRAINT FK_LogETLCounts_PipelineId FOREIGN KEY (PipelineId)
        REFERENCES dbo.LogETLJob(PipelineId)
);
GO