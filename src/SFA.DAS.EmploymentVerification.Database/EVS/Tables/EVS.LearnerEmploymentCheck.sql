CREATE TABLE [EVS].[LearnerEmploymentCheck]
(
	[Id] BIGINT NOT NULL IDENTITY(1,1) CONSTRAINT PK_LearnerEmploymentCheck PRIMARY KEY,
	[LearnerEmploymentVerificationId] BIGINT NOT NULL CONSTRAINT FK_LearnerEmploymentCheck_LearnerEmploymentVerification FOREIGN KEY REFERENCES EVS.LearnerEmploymentVerification(Id),
	[ULN] BIGINT NOT NULL,
	[EmployerAccountId] BIGINT NOT NULL,
	[CreationTime] DATETIME2(7) NOT NULL CONSTRAINT DF_LearnerEmploymentCheck_CreationTime DEFAULT GETDATE(),
	Index IX_LearnerEmploymentCheck_ULN_EmployerAccountId (ULN, EmployeraccountId)
);
GO