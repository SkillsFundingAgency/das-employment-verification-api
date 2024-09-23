CREATE TABLE [dbo].[CheckType]
(
  CheckTypeId SMALLINT NOT NULL Identity(1,1) CONSTRAINT PK_CheckType PRIMARY KEY,
  CheckType VARCHAR(20)
);
GO
