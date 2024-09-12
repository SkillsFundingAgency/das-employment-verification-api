CREATE TABLE [dbo].[CommitmentStatus]
(
  CommitmentStatusId SMALLINT NOT NULL Identity(1,1) Constraint PK_CommitmentStatus PRIMARY KEY,
  CommitmentStatus VARCHAR(20)
);
GO