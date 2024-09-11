CREATE TABLE [dbo].[EmploymentVerification]
(
  ApprenticeshipEmploymentId BIGINT NOT NULL Identity(1,1) Constraint PK_EmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_EV_CorrelationId UNIQUE,
  ULN BIGINT,
  Employed BIT,
  EmploymentCheckDate DATETIME,
  EmploymentCheckRequestDate DATETIME,
  RequestCompletionStatus SMALLINT,
  ErrorType VARCHAR(200),
  MessageSentDate DATETIME,
  CheckType SMALLINT NOT NULL,
  CreatedOn DATETIME DEFAULT GETDATE(),
  LastUpdatedOn DATETIME DEFAULT GETDATE()
);
GO