CREATE TABLE [dbo].[CommitmentStatus]
(
  Id INT NOT NULL Identity(1,1) Constraint PK_CommitmentStatus PRIMARY KEY,
  CommitmentStatus VARCHAR(20),
  CreatedOn DATETIME2(3) DEFAULT GETDATE()
);
GO