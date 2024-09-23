CREATE TABLE [dbo].[AdhocEmploymentVerification]
(
  AdhocEmploymentVerificationId BIGINT NOT NULL Identity(1,1) Constraint PK_AdhocEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AdHocEmploymentVerification_CorrelationId DEFAULT NEWID(),
  ApprenticeshipId BIGINT,
  ULN BIGINT NOT NULL,
  EmployerAccountId BIGINT NOT NULL,
  MinDate DATE,
  MaxDate DATE,
  CreatedOn DATETIME2(7) DEFAULT GETDATE(),
  CONSTRAINT UC_AdhocEmploymentVerification_CorrelationId UNIQUE (CorrelationId)
);
GO