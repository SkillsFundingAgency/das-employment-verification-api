# PV2-4341 database table names

The SQL project defines `[EVS].[EmploymentVerificationRequest]` and
`[EVS].[EmploymentVerificationResponse]`, matching the ticket. EF maps the existing
`EmploymentCheckRequest` and `EmploymentCheckResponse` C# entities to these tables.
Repository and service class names and DbSet names are unchanged.

All columns, types, identity settings and constraints are unchanged. In particular,
the primary keys remain `PK_EmploymentCheckRequest` and `PK_EmploymentCheckResponse`;
EF explicitly preserves those constraint names. The request's `AccountId` column
still stores the application's `EmployerAccountIdentifier`.

## Existing databases: rename before publishing

No database migration has been executed. This project has no SSDT refactor log, so
renaming the SQL definitions alone does not instruct DACPAC publishing to rename
existing tables. A database containing the old names needs a data-preserving
migration before the new application or DACPAC is deployed.

An offline SqlPackage Script comparison of the previous and renamed DACPACs
confirmed the following (no live database was contacted):

- Default settings create the new tables without renaming or copying the old
  tables. The preserved primary-key names would conflict with the old constraints
  in the same schema, so this is not a usable migration.
- `DropObjectsNotInSource=True` generates DROP TABLE for both old tables and
  CREATE TABLE for both new tables. The default data-loss guards abort if rows
  exist; disabling those guards would risk losing the existing data.

The actual local database's deployment plan has not been inspected. After the
manual rename, review a fresh script against that database before publishing.

The simplest local approach is a separately reviewed, one-time table rename using
`sys.sp_rename`. Back up the local database, verify that both old tables exist and
neither new table exists, and run the following only against the intended database
when explicitly authorized:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'EVS.EmploymentCheckRequest', N'U') IS NULL
   OR OBJECT_ID(N'EVS.EmploymentCheckResponse', N'U') IS NULL
   OR OBJECT_ID(N'EVS.EmploymentVerificationRequest', N'U') IS NOT NULL
   OR OBJECT_ID(N'EVS.EmploymentVerificationResponse', N'U') IS NOT NULL
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50000, 'Expected old tables and no new tables. Review the target database before renaming.', 1;
END;

EXEC sys.sp_rename N'EVS.EmploymentCheckRequest', N'EmploymentVerificationRequest', N'OBJECT';
EXEC sys.sp_rename N'EVS.EmploymentCheckResponse', N'EmploymentVerificationResponse', N'OBJECT';

COMMIT TRANSACTION;
```

Renaming the tables preserves their rows, identity state and existing constraints.
Afterward, verify row counts and generate/review a fresh DACPAC deployment script
before publishing. Do not override data-loss protection to force deployment.
Do not add these operations to a post-deployment script: the schema update would
already have been planned and executed by then.

For databases already using the new names, no rename is needed. For fresh databases,
the DACPAC creates the new names directly. Other dependent applications or ad hoc
SQL using the old names must be updated before migration. No such SQL references
were found in this repository beyond the preserved constraint names and this
migration guidance.
