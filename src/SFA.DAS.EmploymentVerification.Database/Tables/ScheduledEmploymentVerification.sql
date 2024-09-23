CREATE TABLE [dbo].[ScheduledEmploymentVerification]
(
  ScheduledEmploymentVerificationId BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_ScheduledEmploymentVerification PRIMARY KEY,
  CommitmentId BIGINT,
  ApprenticeshipId BIGINT CONSTRAINT UC_ScheduledEmploymentVerification_ApprenticeshipId UNIQUE,
  ULN BIGINT NOT NULL,
  UKPRN BIGINT,
  EmployerAccountId BIGINT NOT NULL,
  CommitmentStartDate DATE NOT NULL,
  CommitmentStatusId SMALLINT NOT NULL,
  PaymentStatusId SMALLINT NOT NULL,
  Approvals SMALLINT NOT NULL,
  EmployerAndProviderApprovedOn DATETIME2(7),
  TransferApprovalActionedOn DATETIME2(7),
  EmploymentCheckCount SMALLINT CONSTRAINT DF_ScheduledEmploymentVerification_EmploymentCheckCount DEFAULT 0,
  CreatedOn DATETIME2(7) CONSTRAINT DF_ScheduledEmploymentVerification_CreatedOn DEFAULT GETDATE(),
  LastUpdatedOn DATETIME2(7) CONSTRAINT DF_ScheduledEmploymentVerification_LastUpdatedOn DEFAULT GETDATE(),
  CONSTRAINT FK_ScheduledEmploymentVerification_CommitmentStatusId FOREIGN KEY (CommitmentStatusId) REFERENCES dbo.CommitmentStatus(CommitmentStatusId),
  CONSTRAINT FK_ScheduledEmploymentVerification_PaymentStatusId FOREIGN KEY (PaymentStatusId) REFERENCES dbo.PaymentStatus(PaymentStatusId)      
);
GO