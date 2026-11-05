# EVS Specs (local execution only)

Reqnroll.NUnit covers request registration and the two successful employment
outcomes through the real Application services, EF Core
repository, HTTP client and outgoing publisher. Existing unit tests retain
unknown-correlation and other edge-case coverage.

## Configuration

Following the Payments EarningEvents Specs convention, `TestRunBindings` loads
`appSettings.json` from the test output directory, followed by optional
`appSettings.Development.json`. The Functions `local.settings.json` is not loaded.
Specs use the existing EVS local development database.

The default `ConnectionStrings:EmploymentVerificationConnectionString` is:

```text
Server=localhost;Database=SFA.DAS.EmploymentVerification.Database;Integrated Security=true;TrustServerCertificate=true
```

Both JSON files are copied to the output when present. The connection string is
used directly, without hardcoded server/database validation or a separate SQL
connection probe. Keep it pointed at the local development database. The default
uses Windows Integrated authentication and trusts the local SQL Server certificate.
No development override is required.

## Prerequisites and build

The local development database must already exist with the current EVS schema,
including `EVS.EmploymentVerificationRequest` and `EVS.EmploymentVerificationResponse`.
Specs never create/drop databases, deploy DACPACs, run EF migrations or call
EnsureCreated. Connection/schema errors surface from the normal EF operations.

Prerequisites: Windows, .NET 10 SDK, a running local SQL Server, Visual Studio
MSBuild/SSDT to build the SQL project. The configured SQL account needs SELECT, INSERT, UPDATE and
DELETE permissions on the EVS request/response tables for routine Specs runs.
No Azure credentials, Functions Core Tools, Docker or NServiceBus are required.

From the repository root in a Visual Studio Developer PowerShell:

```powershell
dotnet restore tests/SFA.DAS.EmploymentVerificationService.Specs/SFA.DAS.EmploymentVerificationService.Specs.csproj
MSBuild.exe SFA.DAS.EmploymentVerificationService.sln /t:Build /p:Configuration=Debug
```

## Execution and cleanup

```powershell
dotnet test tests/SFA.DAS.EmploymentVerificationService.Specs/SFA.DAS.EmploymentVerificationService.Specs.csproj --no-build --no-restore
```

Every scenario uses a new correlation ID. Database assertions select only that
ID using the existing `EmploymentVerificationDbContext`, constructed once per scenario
with `DbContextOptionsBuilder<EmploymentVerificationDbContext>` and `UseSqlServer`.
Entity queries use `AsNoTracking` so assertions
read the saved SQL state rather than application-tracked entities. No test-specific
DbContext, entity copies or duplicate mappings are needed.

`TestRunBindings.AfterScenario` deletes its responses first and then its requests in a
transaction, even when a step fails. No whole-table deletes or truncation occur;
pre-existing development records remain intact. Normal inserts advance identity
counters even after the scenario rows are deleted. Cleanup errors fail the scenario. A forcibly
terminated test process cannot run hooks and may leave its own scenario rows.

WireMock.Net listens on a dynamically allocated local HTTP port. `TestSession`
directly constructs the existing EF context, repository, request factory, real
EmploymentCheckClient, publisher and both Application services. The services share
the scenario context and use `NullLogger<T>` and `TimeProvider.System`.
A mocked ServiceBusSender captures outgoing messages. The HTTP client receives a
Development environment and skips token acquisition; its required credential is
the standard DefaultAzureCredential. No authentication configuration is needed.
Lifecycle hooks perform scenario-specific SQL cleanup and then dispose the context,
HttpClient and WireMock in a finally block. Steps call the services directly.

There is no Functions worker startup. Steps invoke the Application
services directly. Function-trigger forwarding, event-to-Application mapping and
worker deserialization remain covered by the separate trigger unit tests. The
Specs no longer cover trigger invocation or the complete Functions/Infrastructure
DI wiring; the existing DI unit tests cover registration. HTTP JSON, SQL persistence,
completion mapping and actual publisher serialization are still verified here.

No additional test DbContext or database project is used. Reqnroll code-behind
files are generated under `obj`. No pipeline changes are included; Specs are
intended for explicit local execution.
