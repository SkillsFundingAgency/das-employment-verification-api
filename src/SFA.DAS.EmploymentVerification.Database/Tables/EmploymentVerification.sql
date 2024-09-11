CREATE TABLE [apprenticeship].[EmploymentVerification]
(
  EmploymentVerificationId BIGINT NOT NULL Identity(1,1) Constraint PK_EmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_EmploymentVerification_CorrelationId UNIQUE,
  ULN BIGINT,
  Employed BIT,
  EmploymentCheckDate DATETIME2(3),
  EmploymentCheckRequestDate DATETIME2(3),
  RequestCompletionStatus SMALLINT,
  ErrorType VARCHAR(200),
  MessageSentDate DATE,
  CheckType SMALLINT NOT NULL,
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  LastUpdatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO