CREATE TABLE [dbo].[stg_EmploymentVerification]
(
  EmploymentVerificationId BIGINT NOT NULL Identity(1,1),
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_stg_EmploymentVerification_CorrelationId UNIQUE,
  Employed BIT,
  EmploymentCheckDate DATETIME2(3),
  EmploymentCheckRequestDate DATETIME2(3),
  RequestCompletionStatus SMALLINT,
  ErrorType VARCHAR(200),
  MessageSentDate DATE,
  CheckType SMALLINT NOT NULL,
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO