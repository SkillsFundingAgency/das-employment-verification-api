CREATE TABLE [EVS].[EmploymentCheck]
(
	[Id] INT NOT NULL IDENTITY(1,1) CONSTRAINT PK_EmploymentCheck PRIMARY KEY,
	[CheckCorrelationId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT UC_EmploymentCheck_CheckCorrelationId UNIQUE,
	[StartDate] DATE NOT NULL,
	[EndDate] DATE NOT NULL,
	[Reference] VARCHAR(200) NULL,
	[RequestTime] DATETIME2(7) NOT NULL ,
	[ResponseTime] DATETIME2(7) NULL,
	[Status] TINYINT NOT NULL CONSTRAINT FK_EmploymentCheck_EmploymentCheckStatus FOREIGN KEY REFERENCES EVS.EmploymentCheckStatus(Id),
	[FailureReason] VARCHAR(200) NULL,
	[CreationTime] DATETIME2(7) NOT NULL CONSTRAINT DF_EmploymentCheck_CreationTime DEFAULT GETDATE(),
);
GO