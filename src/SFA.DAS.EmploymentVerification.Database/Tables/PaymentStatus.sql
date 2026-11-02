CREATE TABLE [dbo].[PaymentStatus]
(
  PaymentStatusId SMALLINT NOT NULL CONSTRAINT PK_PaymentStatus PRIMARY KEY,
  PaymentStatus VARCHAR(50)
);
GO