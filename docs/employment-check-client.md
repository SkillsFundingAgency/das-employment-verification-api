# Employment Check HTTP client

Infrastructure registers `EmploymentCheckClient` as the typed HttpClient for the
Application `IEmploymentCheckClient` interface. The temporary mock is no longer
registered. The Application request uses `EmployerAccountIdentifier` (long), mapped
to the existing database `AccountId` column. Request date calculations, persistence ordering,
Service Bus infrastructure and Function triggers are unchanged.

## Contract confirmed from the local Outer API source

`C:/Dev/das-apim-endpoints/src/EmploymentCheck` contains the controller, request DTO,
RegisterCheck command/response and Startup authentication registration.

The endpoint is `POST /api/EmploymentCheck/RegisterCheck`. The client maps:

| HTTP field | EVS value |
| --- | --- |
| `CorrelationId` | `CorrelationId` |
| `CheckType` | `VerifyNotEmployed` |
| `Uln` | `ULN` |
| `ApprenticeshipAccountId` | Checked conversion of `EmployerAccountIdentifier` from long to int |
| `ApprenticeshipId` | `ApprenticeshipId` |
| `MinDate` | `StartDate` |
| `MaxDate` | `EndDate` |

The response is an acknowledgement with `ErrorType` and `ErrorMessage`, not an
employment result. A successful status with either nonempty error field fails the
request. Non-success statuses raise HttpRequestException with the status code.
Transport errors, malformed/null acknowledgements, timeouts and cancellation
propagate. No POST retry policy is installed; HttpClient uses its default redirect
behavior. Error response bodies and error
field contents are not logged because they may contain learner data. The Function
trigger remains responsible for logging exceptions.

## Configuration

The client reads `EmploymentCheckApiUrl` and `EmploymentCheckApiScope` directly from
IConfiguration. There is no options class, authentication-mode setting, custom URL
validation or custom HTTP handler. HttpClient uses its standard 100-second timeout.

For local testing, add these entries manually to the existing local `Values`:

```json
"EmploymentCheckApiUrl": "https://localhost:5221/api/EmploymentCheck/RegisterCheck"
```

The client skips token acquisition only when IHostEnvironment.IsDevelopment() is
true. Azure Functions Core Tools normally uses this environment for local runs;
confirm the effective host environment. No scope is needed for that bypass.
Outside Development, including Production and Staging, token acquisition is
mandatory. Missing scope or unavailable credentials never cause an unauthenticated
fallback. This change does not edit
local.settings.json. Retain the existing SQL, storage and Service Bus settings and
keep Service Bus setup disabled. Missing API settings fail when sending a request.

The inspected Outer API launch profile uses HTTPS port 5221. Its Startup bypasses
authentication and the global authorization filter when `IsLocalOrDev()` is true;
its checked-in appsettings selects `Environment = LOCAL`. Confirm the effective
Outer API configuration still selects this bypass before testing. Trust the local
development HTTPS certificate normally; TLS validation is never disabled.

Configure the complete API endpoint URL. EVS sends directly to this URL without
appending a path or applying custom URL validation.
The local bypass follows the environment-based approach seen in the DfE Outer
API, using the standard host Development environment instead of its separate
`Environment = LOCAL` convention. The client and target API each need their local
environment configured when testing without authentication. Deployed hosts must
not use Development.

For deployment, set:

| Key | Value |
| --- | --- |
| `EmploymentCheckApiUrl` | Complete deployed RegisterCheck HTTPS endpoint URL |
| `EmploymentCheckApiScope` | Actual target API audience followed by `/.default` |

The actual deployed audience must be confirmed from the target API's effective
`AzureAd:Identifier`/application registration; it is not inferred from the URL or
an inner API identifier. No deployed scope has been configured or verified by a
live token request. The shared bearer authentication policy requires the `APIM`
role and validates the configured tenant and audience. Assign that application
role to the EVS system-assigned Managed Identity on the target API application.
If routing through APIM, separately confirm gateway route and authentication
requirements before use.

DI registers a singleton DefaultAzureCredential as TokenCredential. The client
requests the configured scope with the caller's cancellation
token, and sets Authorization on each individual request. Tokens are never logged.
DefaultAzureCredential uses the standard Azure credential chain, including Managed
Identity in Azure and developer credentials where available. It has no hardcoded
identity IDs or secrets. The credential SDK handles token caching; no HTTP POST
retry policy is installed.

The existing persist-before-send and correlation-ID deduplication workflow remains
unchanged: an HTTP failure after persistence is not repaired by this client. That
workflow's retry/idempotency behavior requires separate review.
