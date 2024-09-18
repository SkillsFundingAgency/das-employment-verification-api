CREATE TABLE [dbo].[PaymentStatus]
(
  PaymentStatusId SMALLINT NOT NULL Identity(1,1) Constraint PK_PaymentStatus PRIMARY KEY,
  PaymentStatus VARCHAR(20)
);
GO