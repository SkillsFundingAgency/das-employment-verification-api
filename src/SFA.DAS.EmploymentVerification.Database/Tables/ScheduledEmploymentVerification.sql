CREATE TABLE [dbo].[ScheduledEmploymentVerification]
(
  ScheduledEmploymentVerificationId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_ScheduledEmploymentVerification PRIMARY KEY,
  ApprenticeshipId BIGINT CONSTRAINT UC_ScheduledEmploymentVerification_ApprenticeshipId UNIQUE,
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAndProviderApprovedOn DATETIME2(7),
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATE,
  CommitmentId BIGINT,
  CommitmentStatusId SMALLINT,
  PaymentStatusId SMALLINT,
  EmploymentCheckCount SMALLINT DEFAULT(0),
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  LastUpdatedOn DATETIME2(3) DEFAULT GETDATE(),
  CONSTRAINT FK_ScheduledEmploymentVerification_CommitmentStatusId FOREIGN KEY (CommitmentStatusId) REFERENCES dbo.CommitmentStatus(CommitmentStatusId),
  CONSTRAINT FK_ScheduledEmploymentVerification_PaymentStatusId FOREIGN KEY (PaymentStatusId) REFERENCES dbo.PaymentStatus(PaymentStatusId)      
);
GO