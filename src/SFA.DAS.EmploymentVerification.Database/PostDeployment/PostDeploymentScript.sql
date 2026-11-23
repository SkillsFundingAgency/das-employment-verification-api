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

-- Lookup rows required by the initial VerifyNotEmployed request
IF NOT EXISTS (SELECT 1 FROM [EVS].[VerificationType] WHERE [Id] = 1)
    INSERT INTO [EVS].[VerificationType] ([Id], [Description]) VALUES (1, 'NonEmployment');

IF NOT EXISTS (SELECT 1 FROM [EVS].[VerificationStatus] WHERE [Id] = 2)
    INSERT INTO [EVS].[VerificationStatus] ([Id], [Description]) VALUES (2, 'InProgress');

IF NOT EXISTS (SELECT 1 FROM [EVS].[EmploymentCheckStatus] WHERE [Id] = 1)
    INSERT INTO [EVS].[EmploymentCheckStatus] ([Id], [Description]) VALUES (1, 'InProgress');

IF NOT EXISTS (SELECT 1 FROM [EVS].[EmploymentCheckStatus] WHERE [Id] = 2)
    INSERT INTO [EVS].[EmploymentCheckStatus] ([Id], [Description]) VALUES (2, 'Completed');

IF NOT EXISTS (SELECT 1 FROM [EVS].[VerificationStatus] WHERE [Id] = 7)
    INSERT INTO [EVS].[VerificationStatus] ([Id], [Description]) VALUES (7, 'Employed');

IF NOT EXISTS (SELECT 1 FROM [EVS].[VerificationStatus] WHERE [Id] = 8)
    INSERT INTO [EVS].[VerificationStatus] ([Id], [Description]) VALUES (8, 'NotEmployed');
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
