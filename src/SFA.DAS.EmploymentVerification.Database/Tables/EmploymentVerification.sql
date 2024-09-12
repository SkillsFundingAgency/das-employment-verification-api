CREATE TABLE [dbo].[EmploymentVerification]
(
  EmploymentVerificationId BIGINT NOT NULL Identity(1,1) Constraint PK_EmploymentVerification PRIMARY KEY,
  CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_EmploymentVerification_CorrelationId UNIQUE,
  Employed BIT,
  EmploymentCheckDate DATETIME2(3),
  EmploymentCheckRequestDate DATETIME2(3),
  RequestCompletionStatus SMALLINT,
  ErrorType VARCHAR(200),
  MessageSentDate DATE,
  CheckTypeId SMALLINT NOT NULL,
  CreatedOn DATETIME2(3) DEFAULT GETDATE(),
  LastUpdatedOn DATETIME2(3) DEFAULT GETDATE(),
  CONSTRAINT FK_EmploymentVerification_CheckTypeId FOREIGN KEY (CheckTypeId)
        REFERENCES dbo.CheckType(CheckTypeId)
);
GO