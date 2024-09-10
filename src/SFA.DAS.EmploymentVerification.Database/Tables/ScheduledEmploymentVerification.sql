CREATE TABLE [dbo].[ScheduledEmploymentVerification]
(
  ScheduledECId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_ScheduledEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_Scheduled_CorrelationId UNIQUE,
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAndProviderApprovedOn DATETIME,
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATETIME,
  CommitmentId BIGINT,
  AapprenticeshipId BIGINT,
  CommitmentStatus SMALLINT,
  CheckType SMALLINT,
  CreatedOn DATETIME DEFAULT GETDATE()
  --LastUpdatedOn DATETIME Default getdate()
);
GO