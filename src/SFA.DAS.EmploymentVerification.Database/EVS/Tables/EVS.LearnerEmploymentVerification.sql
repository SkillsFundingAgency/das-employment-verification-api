CREATE TABLE [EVS].[LearnerEmploymentVerification]
(
	[EmploymentVerificationId] BIGINT NOT NULL CONSTRAINT PK_LearnerEmploymentVerification PRIMARY KEY CONSTRAINT FK_LearnerEmploymentVerification_EmploymentVerification FOREIGN KEY REFERENCES EVS.EmploymentVerification(Id),
	[ULN] BIGINT NOT NULL,
	[EmployeraccountId] BIGINT NOT NULL,
	[CreationDate] DATETIME2(7) NOT NULL CONSTRAINT DF_LearnerEmploymentVerification_CreationDate DEFAULT GETDATE(),
	Index IX_LearnerEmploymentVerification_ULN_EmployerAccountId (ULN, EmployeraccountId)
);
GO
