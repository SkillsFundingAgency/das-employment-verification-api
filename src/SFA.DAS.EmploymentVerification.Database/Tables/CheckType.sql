CREATE TABLE [dbo].[CheckType]
(
  CheckTypeId INT NOT NULL Identity(1,1) Constraint PK_CheckType PRIMARY KEY,
  CheckType VARCHAR(20),
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO
