CREATE PROCEDURE [dbo].[UpsertLogETLJob]
    @PipelineId UNIQUEIDENTIFIER,
    @PipelineName VARCHAR(400),
    @PLStartTime DATETIME2(7),
    @PLEndTime DATETIME2(7)
AS
BEGIN
    -- Update existing record if PipelineId exists
    IF EXISTS (SELECT 1 FROM dbo.LogETLJob WHERE PipelineId = @PipelineId)
    BEGIN
        UPDATE dbo.LogETLJob
        SET 
            PLEndTime = @PLEndTime,
            LastUpdatedOn = GETDATE()
        WHERE PipelineId = @PipelineId;
    END
    ELSE
    BEGIN
        -- Insert a new record if PipelineId does not exist
        INSERT INTO dbo.LogETLJob (PipelineId, PipelineName, PLStartTime, LastUpdatedOn)
        VALUES (@PipelineId, @PipelineName, @PLStartTime, GETDATE());
    END
END;
