CREATE TABLE [dbo].[AdhocEmploymentVerification]
(
  AdhocEmploymentVerificationId BIGINT NOT NULL Identity(1,1) Constraint PK_AdhocEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL,
  ULN BIGINT NOT NULL,
  EmployerAccountId BIGINT NOT NULL,
  MinDate DATE,
  MaxDate DATE,
  AapprenticeshipId BIGINT,
  CheckType SMALLINT,
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  --LastUpdatedOn DATETIME Default getdate(),
  CONSTRAINT UC_Adhoc_CorrelationId UNIQUE (CorrelationId)
);
GO