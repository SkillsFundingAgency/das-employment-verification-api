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
--Insert data into CheckType table
INSERT INTO [dbo].[CheckType] (CheckTypeId, CheckType)
VALUES 
    ('1', 'Scheduled'),
    ('2', 'AdHoc');

--Insert data into CommitmentStatus table
INSERT INTO [dbo].[CommitmentStatus] (CommitmentStatusId, CommitmentStatus)
VALUES 
    ('1', 'Active'),
    ('2', 'Paused'),
    ('3', 'Stopped'),
    ('4', 'Inactive');

--Insert data into PaymentStatus table
INSERT INTO [dbo].[PaymentStatus] (PaymentStatusId, PaymentStatus)
VALUES 
    ('0', 'Pending Approval'),
    ('1', 'Active'),
    ('2', 'Paused'),
    ('3', 'Cancelled'),
    ('4', 'Completed'),
    ('5', 'Deleted');

--Insert data into PaymentStatus table
INSERT INTO [dbo].[ApprovalsStatus] (ApprovalsStatusId, ApprovalsStatus)
VALUES 
    ('0', 'None'),
    ('1', 'Employer'),
    ('2', 'Provider'),
    ('3', 'Employer and Provider'),
    ('4', 'Transfer Sender'),
    ('5', 'All (Employer and Provider and TransferSender)');