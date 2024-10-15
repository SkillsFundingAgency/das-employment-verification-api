CREATE PROCEDURE usp_UpsertScheduledEmploymentVerification
	@PipelineId UNIQUEIDENTIFIER,
	@ActivityName VARCHAR(255)
AS
BEGIN
	BEGIN TRY
		--Create table to catch DML actions and count
		CREATE TABLE #MergeOutput (Action NVARCHAR(10));
		BEGIN TRAN
			--perform UPSERT using MERGE as the data is not huge to perform MERGE
			MERGE INTO [dbo].[ScheduledEmploymentVerification] AS target
			USING [dbo].[stg_ScheduledEmploymentVerification] AS source
			ON target.ApprenticeshipId = source.ApprenticeshipId
				AND target.ULN = source.ULN
				AND target.[EmployerAccountId] = source. [EmployerAccountId]
			WHEN MATCHED AND target.[CommitmentStartDate] != source.[CommitmentStartDate] THEN
				UPDATE 
				SET target.[CommitmentStartDate] = source.[CommitmentStartDate],
					target.[EmploymentCheckCount] = 0,
					target.[LastUpdatedOn] = GETDATE()
			WHEN NOT MATCHED BY TARGET THEN
				INSERT ([CommitmentId]
				  ,[ApprenticeshipId]
				  ,[ULN]
				  ,[UKPRN]
				  ,[EmployerAccountId]
				  ,[CommitmentStartDate]
				  ,[CommitmentStatusId]
				  ,[PaymentStatusId]
				  ,[ApprovalsStatusId]
				  ,[EmployerAndProviderApprovedOn]
				  ,[TransferApprovalActionedOn]) 
				VALUES (source.[CommitmentId]
				  ,source.[ApprenticeshipId]
				  ,source.[ULN]
				  ,source.[UKPRN]
				  ,source.[EmployerAccountId]
				  ,source.[CommitmentStartDate]
				  ,source.[CommitmentStatusId]
				  ,source.[PaymentStatusId]
				  ,source.[ApprovalsStatusId]
				  ,source.[EmployerAndProviderApprovedOn]
				  ,source.[TransferApprovalActionedOn])
			OUTPUT $action INTO #MergeOutput;  -- Capture action types (INSERT, UPDATE, DELETE) Get the count of each action type
		COMMIT TRAN
		-- Log DML counts and actions
		INSERT INTO [dbo].[LogETLCounts] (PipelineId,ActivityName,ETLRowsAffected,ETLActivityName)
		SELECT 
			@PipelineId,
			@ActivityName,
			COUNT(*) AS Count,
			Action
		FROM #MergeOutput
		GROUP BY Action;
	END TRY

	BEGIN CATCH
		--Rollback the transaction
		ROLLBACK TRAN
		--Log error details
		INSERT INTO LogError(PipelineId, ActivityName, ErrorMessage)
		SELECT 
			@PipelineId,
			@ActivityName,
			ERROR_MESSAGE() AS ErrorMessage
	END CATCH
END