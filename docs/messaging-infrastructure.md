# EVS messaging infrastructure

PV2-4341 uses two independent Azure Service Bus namespaces. Entity names are preserved.

| Message | Direction | Namespace | Topic setting | Queue / subscription settings |
| --- | --- | --- | --- | --- |
| EmploymentCheckCompletedEvent | Incoming | DAS Shared | DasSharedServiceBusTopicName | EmploymentCheckCompletedQueueName / EmploymentCheckCompletedSubscriptionName |
| VerifyNotEmployed | Incoming | Payments/PV2 | PaymentsServiceBusTopicName | VerifyNotEmployedQueueName / VerifyNotEmployedSubscriptionName |
| VerifyNotEmployedCompleted | Outgoing | Payments/PV2 | VerifyNotEmployedCompletedTopicName | Not applicable |

## Configuration

Use these flat names inside Functions local.settings.json Values, and as Function App settings in Azure:

```json
{
  "DasSharedServiceBusConnection": "",
  "PaymentsServiceBusConnection": "",
  "DasSharedServiceBusTopicName": "",
  "PaymentsServiceBusTopicName": "bundle-1",
  "VerifyNotEmployedQueueName": "sfa-das-evs-verify-not-employed",
  "VerifyNotEmployedSubscriptionName": "sfa-das-evs-verify-not-employed",
  "EmploymentCheckCompletedQueueName": "sfa-das-evs-employment-check-completed",
  "EmploymentCheckCompletedSubscriptionName": "sfa-das-evs-employment-check-completed",
  "VerifyNotEmployedCompletedTopicName": "bundle-1",
  "ServiceBusSetupEnabled": "false",
  "ServiceBusFiltersReviewed": "false"
}
```

Empty values must be supplied from verified environment configuration; they are not usable connections or topic defaults. The existing local PV2 connection is retained as PaymentsServiceBusConnection. DAS Shared connection and topic require confirmation and supply. Both namespaces may contain bundle-1, but matching topic names do not identify the same resource. Do not assume the DAS Shared topic name from the Payments topic name.

Optional DasSharedServiceBusAdministrationConnection and PaymentsServiceBusAdministrationConnection override the corresponding runtime connection for provisioning. Null, empty and whitespace values fall back only to the same namespace's runtime connection. Runtime and administration namespace hostnames must agree. Do not supply entity-scoped connections when one connection needs access to multiple entities.

Retired settings are ServiceBusConnection, ServiceBusAdministrationConnection and ServiceBusTopicName; there is no compatibility fallback. Preserve unrelated configuration. DevOps owns deployment templates, pipelines, secrets and Azure settings; those files are unchanged by this implementation. ConfigNames alone does not establish an Azure Table configuration provider. Trigger connections and queue settings must be visible to the Functions host.

## Receiving and publishing

EmploymentCheckCompletedServiceBusTrigger selects DasSharedServiceBusConnection; VerifyNotEmployedServiceBusTrigger selects PaymentsServiceBusConnection. Existing queue-name expressions and processing logic are unchanged.

The outgoing ServiceBusClient uses PaymentsServiceBusConnection and creates its sender from VerifyNotEmployedCompletedTopicName. EmploymentVerificationPublisher only serializes and sends through that sender; its implementation and message metadata are unchanged.

## Route definitions and filters

EvsRoutes defines each route's namespace identity, resource settings and existing SQL rule. Resource names are environment configuration; rule names and filters remain in code.

The EmploymentCheckCompletedEvent rule retains:

```sql
[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'
```

The VerifyNotEmployed rule retains this TEMPORARY LOCAL-TESTING filter:

```sql
sys.Label = 'VerifyNotEmployed'
```

This is not a production-approved DataLocks filter. Final publisher metadata and filter verification are a separate task. Keep ServiceBusSetupEnabled=false and ServiceBusFiltersReviewed=false. A non-empty filter and review flag do not establish that publisher metadata has been verified. No NServiceBus dependency is introduced; the completion filter only inspects an existing message property.

## Provisioning and validation

One SetupMessagingInfrastructure hosted service selects a namespace-specific administration client for each route. All configured runtime/administration namespace pairs and route definitions are validated before infrastructure calls. Disabled provisioning does not require administration credentials, route definitions or filter review. Runtime publishing still requires its Payments connection and outgoing topic.

Enabled provisioning requires filter review, required route settings, valid namespace identities, non-empty SQL filters, and named rules other than $Default. Duplicate queues or topic/subscription pairs are rejected within a namespace; identical names in different namespaces are allowed.

For each route, EVS checks that its topic exists, creates its queue if missing, then creates its subscription with ForwardTo and the SQL rule together. Shared topics are never created. Each subscription and its forwarding queue belong to the same namespace; cross-namespace forwarding is never used.

Existing subscriptions must have the expected forwarding destination and exact named SQL filter with no rule action. Unexpected rules, missing rules or incorrect forwarding fail startup. The $Default catch-all is removed only after validation succeeds. Existing resources are not renamed or repaired.

ForwardTo accepts a matching relative queue name or an equivalent http, https or sb URI. Absolute URIs must match the current route's namespace hostname, full queue path and accepted port, with no user info, query or fragment. A URI for the other namespace is rejected even if its queue name matches.

New queues retain seven-day TTL, expiry dead-lettering, five-minute locks, maximum delivery count 50 and capacity 5,120 MB. New subscriptions retain seven-day TTL, expiry dead-lettering, five-minute locks and maximum delivery count 50. Existing queue settings are left unchanged.

Administration requires Manage rights covering each route's source and destination. Runtime requires Listen on each incoming queue and Send on the Payments outgoing topic. Separate administration credentials avoid granting runtime credentials management rights. Both namespaces must be reachable from the Function App.

Cancellation flows to administration calls. Permission failures and concurrent creation/deletion conflicts fail startup. Provisioning is not transactional across routes or namespaces. Coordinate configuration and publisher cutover and drain old queues where necessary; creating the same entity name in another namespace does not move its messages.

## Tests

Focused NUnit/Moq tests cover route assignment, separate topics, namespace-scoped duplicates, trigger settings, forwarding and administration/runtime consistency. Existing publisher tests retain serialization coverage. The three Reqnroll Specs are unchanged: they call application services and capture outgoing messages with a mocked sender, so they do not verify live namespace routing or provisioning.
