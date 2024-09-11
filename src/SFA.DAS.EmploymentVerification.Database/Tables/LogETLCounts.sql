CREATE TABLE [dbo].[LogETLCounts]
(
  LogETLCountsId BIGINT NOT NULL Identity(1,1) Constraint PK_LogETLCounts PRIMARY KEY,
  PipelineId UNIQUEIDENTIFIER,
  ActivityName VARCHAR(200),
  ETLRowsAffected BIGINT,
  ETLActivityName VARCHAR(200),
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO