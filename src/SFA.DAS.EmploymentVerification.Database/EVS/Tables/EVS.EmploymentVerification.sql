CREATE TABLE [EVS].[EmploymentVerification]
(
  Id BIGINT NOT NULL Identity(1,1) CONSTRAINT PK_EmploymentVerification PRIMARY KEY,
  ExternalCorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_EmploymentVerification_ExternalCorrelationId UNIQUE,
  [Type] TINYINT NOT NULL CONSTRAINT FK_EmploymentVerification_Type FOREIGN KEY REFERENCES EVS.VerificationType(Id),
  StartDate DATE NOT NULL,
  DurationInDays INT NOT NULL CONSTRAINT DF_EmploymentVerification_DurationInDays DEFAULT 30,
  Reference nvarchar(MAX) NULL, 
  [Status] TINYINT NOT NULL CONSTRAINT FK_EmploymentVerification_Status FOREIGN KEY REFERENCES EVS.VerificationStatus(Id),
  FailureReason NVARCHAR(200) NULL,
  RetryCount TINYINT NOT NULL CONSTRAINT DF_EmploymentVerification_RetryCount DEFAULT 0,
  NextRetryTime DATETIME2(7) NULL,
  CreationTime DATETIME2(7) NOT NULL CONSTRAINT DF_EmploymentVerification_CreationTime DEFAULT GETDATE(),
);
GO