/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/
SET NOCOUNT ON;
GO
--Insert data into CheckType table
IF NOT EXISTS (SELECT 1 FROM [dbo].[CheckType])
INSERT INTO [dbo].[CheckType] (CheckTypeId, CheckType)
VALUES 
    ('1', 'Scheduled'),
    ('2', 'AdHoc')
GO

--Insert data into CommitmentStatus table
IF NOT EXISTS (SELECT 1 FROM [dbo].[CommitmentStatus])
INSERT INTO [dbo].[CommitmentStatus] (CommitmentStatusId, CommitmentStatus)
VALUES 
    ('1', 'Active'),
    ('2', 'Paused'),
    ('3', 'Stopped'),
    ('4', 'Inactive')
GO

--Insert data into PaymentStatus table
IF NOT EXISTS (SELECT 1 FROM [dbo].[PaymentStatus])
INSERT INTO [dbo].[PaymentStatus] (PaymentStatusId, PaymentStatus)
VALUES 
    ('0', 'Pending Approval'),
    ('1', 'Active'),
    ('2', 'Paused'),
    ('3', 'Cancelled'),
    ('4', 'Completed'),
    ('5', 'Deleted')
GO

--Insert data into PaymentStatus table
IF NOT EXISTS (SELECT 1 FROM [dbo].[ApprovalsStatus])
INSERT INTO [dbo].[ApprovalsStatus] (ApprovalsStatusId, Status)
VALUES 
    ('0', 'None'),
    ('1', 'Employer'),
    ('2', 'Provider'),
    ('3', 'Employer and Provider'),
    ('4', 'Transfer Sender'),
    ('5', 'All (Employer and Provider and TransferSender)')
GO

--Update ApprovalsStatusId = 7 where ApprovalsStatusId= 5
IF NOT EXISTS (SELECT 1 FROM [dbo].[ApprovalsStatus]  WHERE [ApprovalsStatusId] = 7)
	UPDATE [dbo].[ApprovalsStatus] 
    SET ApprovalsStatusId = 7
    WHERE ApprovalsStatusId = 5
GO