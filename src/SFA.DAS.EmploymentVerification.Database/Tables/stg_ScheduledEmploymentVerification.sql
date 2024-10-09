CREATE TABLE [dbo].[stg_ScheduledEmploymentVerification]
(
  ScheduledEmploymentVerificationId BIGINT NOT NULL Identity(1,1),
  CommitmentId BIGINT,
  ApprenticeshipId BIGINT,
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATE NOT NULL,
  CommitmentStatusId SMALLINT NOT NULL,
  PaymentStatusId SMALLINT NOT NULL,
  ApprovalsStatusId SMALLINT NOT NULL,
  EmployerAndProviderApprovedOn DATETIME2(7),
  TransferApprovalActionedOn DATETIME2(7),
  CreatedOn DATETIME2(7) DEFAULT GETDATE()
  );
GO