CREATE TABLE [dbo].[AdhocEmploymentVerification]
(
  AdhocEVId BIGINT NOT NULL Identity(1,1) Constraint PK_AdhocEmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL,
  ULN BIGINT NOT NULL,
  EmployerAccountId BIGINT NOT NULL,
  MinDate DATETIME,
  MaxDate DATETIME,
  AapprenticeshipId BIGINT,
  CheckType SMALLINT,
  CreatedOn DATETIME DEFAULT GETDATE(),
  --LastUpdatedOn DATETIME Default getdate(),
  CONSTRAINT UC_Adhoc_CorrelationId UNIQUE (CorrelationId)
);
GO