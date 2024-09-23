CREATE TABLE [dbo].[PaymentStatus]
(
  PaymentStatusId SMALLINT NOT NULL Identity(1,1) CONSTRAINT PK_PaymentStatus PRIMARY KEY,
  PaymentStatus VARCHAR(50)
);
GO