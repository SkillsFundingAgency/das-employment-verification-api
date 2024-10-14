CREATE PROCEDURE dbo.usp_LogError
(
    @PipelineId NVARCHAR(100),
    @ActivityName NVARCHAR(255),
    @ErrorMessage NVARCHAR(MAX)
)
AS
BEGIN
    INSERT INTO [dbo].[LogError]
        ([PipelineId], [ActivityName], [ErrorMessage])
    VALUES
        (@PipelineId, @ActivityName, @ErrorMessage);
    RETURN 0;
END
