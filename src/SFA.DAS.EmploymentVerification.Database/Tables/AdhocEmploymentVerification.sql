CREATE TABLE [dbo].[AdhocEmploymentVerification]
(
  AdhocEmploymentVerificationId BIGINT NOT NULL Identity(1,1) Constraint PK_AdhocEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
  ULN BIGINT NOT NULL,
  EmployerAccountId BIGINT NOT NULL,
  MinDate DATE,
  MaxDate DATE,
  CommitmentsApprenticeshipId BIGINT,
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  CONSTRAINT UC_AdhocEmploymentVerification_CorrelationId UNIQUE (CorrelationId)
);
GO