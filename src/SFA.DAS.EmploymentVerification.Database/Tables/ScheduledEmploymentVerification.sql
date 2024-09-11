CREATE TABLE [dbo].[ScheduledEmploymentVerification]
(
  ScheduledECId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_ScheduledEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_Scheduled_CorrelationId UNIQUE,
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAndProviderApprovedOn DATETIME2(7),
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATE,
  CommitmentId BIGINT,
  AapprenticeshipId BIGINT,
  CommitmentStatus SMALLINT,
  CheckType SMALLINT,
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
  --LastUpdatedOn DATETIME Default getdate()
);
GO