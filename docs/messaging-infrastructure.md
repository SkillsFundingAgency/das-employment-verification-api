# EVS messaging infrastructure

Incoming messages use a shared topic, a dedicated EVS subscription with one SQL
rule, and an auto-forwarding queue. The existing typed Service Bus triggers consume
the queues. EVS never creates the shared topic or changes other services'
subscriptions. Outgoing `VerifyNotEmployedCompleted` messages use the existing
publisher and a separately configured topic.

## Route definitions

`EvsRoutes.For(configuration)` defines the two incoming routes in C#:

| Message | Rule name | SQL filter |
| --- | --- | --- |
| `VerifyNotEmployed` | `VerifyNotEmployed` | Empty placeholder until DataLocks publishing metadata is confirmed |
| `EmploymentCheckCompletedEvent` | `EmploymentCheckCompletedEvent` | Verified NServiceBus type filter below |

The completion filter is:

```sql
[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'
```

The exact command CLR type is
`SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands.VerifyNotEmployed`.
This does not prove DataLocks publishes it through NServiceBus or sets that header.
The command SQL filter is deliberately an empty constant in `EvsRoutes.cs`, with a
TODO to confirm the actual publisher metadata. It is not a wildcard or a working
filter. Enabled provisioning fails with an explicit DataLocks metadata error until
this filter is resolved, even when all settings and the review flag are present.
Do not infer the filter from a package reference, the command namespace or EVS's
outgoing Subject.

SQL filters and rule names live beside their route definitions because they describe
message contracts and publisher metadata, rather than environment-specific Azure
resources. There is no custom SQL parser and no filter app setting. Adding a future
incoming message route requires another definition in `EvsRoutes` and appropriate
resource settings, without changing `SetupMessagingInfrastructure`.

## Application settings

In Functions `local.settings.json`, keep these string settings inside the existing
`Values` object. Supply the same flat names in each Azure Function application's
settings, using that environment's existing resource names:

```json
{
  "ServiceBusSetupEnabled": "false",
  "ServiceBusFiltersReviewed": "false",
  "ServiceBusTopicName": "bundle-1",
  "VerifyNotEmployedQueueName": "sfa-das-evs-verify-not-employed",
  "VerifyNotEmployedSubscriptionName": "sfa-das-evs-verify-not-employed",
  "EmploymentCheckCompletedQueueName": "sfa-das-evs-employment-check-completed",
  "EmploymentCheckCompletedSubscriptionName": "sfa-das-evs-employment-check-completed",
  "VerifyNotEmployedCompletedTopicName": "bundle-1"
}
```

`ServiceBusTopicName` supplies the incoming shared topic for both routes. Each route
reads its queue and subscription from separate settings; there is no assumption
that queue and subscription names match across environments. Trigger bindings
continue using `%VerifyNotEmployedQueueName%` and
`%EmploymentCheckCompletedQueueName%`, so provisioning and processing use the same
queue settings. No physical resource names are hardcoded in C#.

`VerifyNotEmployedCompletedTopicName` independently controls the outgoing sender.
Do not replace it with `ServiceBusTopicName`. Locally, both settings retain `bundle-1`.
The publisher's JSON, Subject, correlation ID and cancellation behaviour are unchanged.

Keep existing `ServiceBusConnection`, optional `ServiceBusAdministrationConnection`,
`AzureWebJobsStorage`, `FUNCTIONS_WORKER_RUNTIME` and `SqlConnectionString` values.
The optional administration connection falls back to `ServiceBusConnection` and
must address the same namespace as the source topic and destination queues.
Connection values are intentionally omitted from the example.

## DevOps migration

For each environment:

- Add `ServiceBusTopicName` using the existing incoming topic. Confirm both publisher
  routes use this topic and namespace; do not change the actual destination.
- Add `VerifyNotEmployedSubscriptionName` and
  `EmploymentCheckCompletedSubscriptionName` using the existing subscriptions.
- Retain both trigger queue settings and the separate outgoing topic setting.
- Retain both Service Bus connection settings where configured.
- Remove the previous indexed route settings. There is no fallback to them, and
  rule names and filters no longer come from application settings.
- Keep `ServiceBusSetupEnabled=false` and `ServiceBusFiltersReviewed=false`.

No resource should be renamed or recreated as part of this settings migration.
Check the Function application's effective settings source: the checked-in ARM
and pipeline templates do not currently supply these messaging values explicitly.
`ConfigNames` alone does not establish that the isolated worker loads Azure Table
configuration. No pipeline changes are part of this implementation.

## Validation and startup

Disabled provisioning returns disabled options with no routes, without requiring
route settings, filter review or resolution of the DataLocks placeholder. The
outgoing sender still requires its connection and outgoing topic independently.

When provisioning is enabled, filter review is required. Every physical resource
setting must be non-empty; missing-setting exceptions identify the setting by name.
Routes require all fields, a named rule other than `$Default`, distinct queues,
distinct topic/subscription pairs and non-empty SQL filters. Enabled provisioning
currently fails because the DataLocks filter remains unresolved. Do not enable it
until the publisher metadata is verified, the correct filter is defined in code,
and all filters and environment resource names have been reviewed.

The hosted service's behaviour is unchanged. It checks that the shared topic exists,
creates missing queues, and creates missing subscriptions with ForwardTo and the SQL
rule in the same call. There is no temporary wildcard filter. New entities retain
seven-day TTL, expiry dead-lettering, five-minute locks and maximum delivery count
50; queues retain capacity 5,120 MB. Existing queue settings are left unchanged.

Existing subscriptions are checked once for the expected ForwardTo destination
and named SQL rule's exact expression, with no rule action. Missing/incorrect rules,
incorrect forwarding and unexpected additional rules fail startup. There is no
repair or reconciliation. `$Default` is removed only after the correct forwarding
and required filter have been confirmed. Validation failures remove no rules.

Cancellation flows to every administration call. Permission failures, concurrent
creation/deletion conflicts and other administration errors fail startup. Startup
is not transactional across routes; correct invalid existing configuration before
restarting. Provisioning requires Manage rights; runtime needs Listen on the queues
and Send on the outgoing topic.
