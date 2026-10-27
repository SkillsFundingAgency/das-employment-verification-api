CREATE PROCEDURE dbo.usp_LogETLCounts
(
    @PipelineId NVARCHAR(100),
    @ActivityName NVARCHAR(255),
    @ETLRowsAffected INT,
    @ETLActivityName NVARCHAR(255)
)
AS
BEGIN
    INSERT INTO [dbo].[LogETLCounts]
           ([PipelineId]
           ,[ActivityName]
           ,[ETLRowsAffected]
           ,[ETLActivityName])
    VALUES
           (@PipelineId
           ,@ActivityName
           ,@ETLRowsAffected
           ,@ETLActivityName);
    RETURN 0;
END
