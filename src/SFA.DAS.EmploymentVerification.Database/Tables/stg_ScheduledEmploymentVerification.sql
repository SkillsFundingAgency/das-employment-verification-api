CREATE TABLE [dbo].[stg_ScheduledEmploymentVerification]
(
  ScheduledEmploymentVerificationId BIGINT Identity(1,1),
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAndProviderApprovedOn DATETIME2(7),
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATE,
  CommitmentId BIGINT,
  AapprenticeshipId BIGINT,
  CommitmentStatusId SMALLINT,
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO