# EVS messaging infrastructure

Both incoming routes use topic → filtered subscription → forwarding queue → existing
typed ServiceBusTrigger. Provisioning uses Azure.Messaging.ServiceBus only. It does
not create shared topics or change trigger bindings or automatic settlement.

| Direction | Message | Route |
| --- | --- | --- |
| Incoming | `VerifyNotEmployed` | Configured topic -> EVS filtered subscription -> command queue -> typed trigger |
| Incoming | `EmploymentCheckCompletedEvent` | Configured topic -> EVS filtered subscription -> completion queue -> typed trigger |
| Outgoing | `VerifyNotEmployedCompleted` | EVS publisher -> configured topic -> downstream consumer subscription |

EVS owns only the two incoming subscriptions and queues. The outgoing event does
not require a third EVS subscription or queue; downstream consumers own their
subscriptions and filters.

Provisioning is disabled by default. Local settings keep setup and filter review
disabled, and leave the unverified command filter empty. Do not enable setup against the shared namespace until both
filters have been reviewed. The review flag records that external review; it does
not verify publisher metadata.

## Configuration

The outgoing publisher settings are required independently of provisioning:

| Key | Meaning |
| --- | --- |
| `ServiceBusConnection` | Existing Service Bus connection string used by the sender and triggers |
| `VerifyNotEmployedCompletedTopicName` | Outgoing topic; configure as `bundle-1` for local development |

The singleton `ServiceBusClient` creates a reusable `ServiceBusSender` for the
configured outgoing topic. `EmploymentVerificationPublisher` retains JSON
serialization, `ContentType = application/json`, `Subject = VerifyNotEmployedCompleted`,
and the event's correlation ID. It passes cancellation to the sender and propagates
send exceptions. It neither creates nor validates the topic remotely during DI
registration. The destination must already exist and the credential needs Send
permission on that topic.

Set the following incoming provisioning settings only after review:

| Key | Meaning |
| --- | --- |
| `ServiceBusSetupEnabled` | Explicit opt-in; defaults to false |
| `ServiceBusFiltersReviewed` | Must explicitly be true when enabled; covers both filters |
| `VerifyNotEmployedTopic` | Command topic; `bundle-1` locally |
| `VerifyNotEmployedSubscription` | Dedicated EVS command subscription |
| `VerifyNotEmployedFilter` | Required reviewed command filter; no default |
| `VerifyNotEmployedQueueName` | Existing command trigger queue setting |
| `EmploymentCheckCompletedTopic` | Completion topic; `bundle-1` locally |
| `EmploymentCheckCompletedSubscription` | Dedicated EVS completion subscription |
| `EmploymentCheckCompletedFilter` | Required completion filter shown below |
| `EmploymentCheckCompletedQueueName` | Existing completion trigger queue setting |
| `ServiceBusAdministrationConnection` | Optional Manage connection string; falls back to `ServiceBusConnection` |

All configuration keys are flat and use the same names in local `Values` and Azure
application settings; no section prefix or double underscores are needed. The existing
`ServiceBusConnection` and `VerifyNotEmployedCompletedTopicName` publisher settings
remain required. The two routes must use distinct queues and distinct subscription
pairs. Forwarding destinations must be in the topic's namespace.

All three topic names are environment-specific configuration; none is hardcoded.
For local development, configure both incoming topics and the outgoing topic as
`bundle-1`. In deployed environments, supply the appropriate topic names, dedicated
EVS subscription/queue names, reviewed filters and credentials through the existing
environment configuration. The sender and triggers use `ServiceBusConnection`;
the optional administration connection must point to the same namespace. Ensure
that configuration and trigger queue names agree. Outgoing publishing works while
incoming provisioning remains disabled. The local settings file contains the values
below and preserves the existing storage, worker runtime, SQL and Service Bus
connection values. Deployed environment settings must be migrated to these flat
names; there is no fallback to the former nested keys or former outgoing setting.

### Local settings

| Key | Local value |
| --- | --- |
| `ServiceBusSetupEnabled` | `false` |
| `ServiceBusFiltersReviewed` | `false` |
| `VerifyNotEmployedTopic` | `bundle-1` |
| `VerifyNotEmployedSubscription` | `sbts-evs-verify-not-employed-dev` |
| `VerifyNotEmployedQueueName` | `sbq-evs-verify-not-employed-dev` |
| `VerifyNotEmployedFilter` | Empty string until publisher metadata is confirmed |
| `EmploymentCheckCompletedTopic` | `bundle-1` |
| `EmploymentCheckCompletedSubscription` | `sbts-evs-employment-check-completed-dev` |
| `EmploymentCheckCompletedQueueName` | `sbq-evs-employment-check-completed-dev` |
| `EmploymentCheckCompletedFilter` | Verified SQL expression below |
| `VerifyNotEmployedCompletedTopicName` | `bundle-1` |

Keep the current `AzureWebJobsStorage`, `FUNCTIONS_WORKER_RUNTIME`,
`SqlConnectionString` and `ServiceBusConnection` values unchanged. Connection
values are intentionally omitted from this document. `ServiceBusAdministrationConnection`
remains optional and falls back to `ServiceBusConnection` when absent.

With setup disabled, provisioning route validation and hosted-service registration
are skipped. The review flag alone never enables setup. Enabling setup requires
explicit review and valid restrictive filters for both routes; the empty command
filter must be resolved first. Existing trigger bindings still use the queue-name
keys above, and no trigger or publisher implementation changes are required.

The verified completion filter is:

```sql
[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'
```

The actual VerifyNotEmployed publisher metadata remains unverified. Do not infer
its filter from the completion publisher or EVS's outgoing Subject header. Obtain
the publisher's actual Subject/application properties before configuring it.

To exclude unrestricted SQL, configuration accepts one property equality or LIKE
comparison with a quoted literal. LIKE patterns must contain a literal letter or
digit. Examples of supported property syntax are `sys.Label`, `[messageType]`, and
`user.[message-type]`. Compound expressions, tautologies and wildcard-only patterns
are rejected. More complex filters require a separate reviewed implementation.

## Startup and existing entities

All route configuration is validated before provisioning starts. The hosted service
checks that each shared topic exists, ensures each queue exists, then creates each
subscription with its required rule atomically. It never creates a subscription
with a default pass-through filter. New queues use the Collection Period reference
settings: seven-day TTL, expiry dead-lettering, five-minute locks, delivery count
50 and 5,120 MB capacity. Existing queue settings are preserved.

On existing EVS subscriptions, it repairs the owned rule's expression/action,
removes `$Default` only after the required rule exists, and repairs ForwardTo.
Owned rule names are `VerifyNotEmployed` and `EmploymentCheckCompletedEvent`.
Unexpected additional rules fail startup without being deleted. Use dedicated
EVS subscriptions, not subscriptions owned by other applications. Final forwarding
and rules are read back and verified. Previously misrouted messages are not repaired.

Concurrent already-exists creates are followed by reconciliation. Concurrent
default-rule deletion is tolerated. Cancellation flows to every administration
call; permission errors and unresolved failures propagate and fail startup.
Startup is not transactional across routes: a failure can leave a route partially
provisioned; rerunning reconciles it. Conflicting deployments with different desired
configuration must be resolved operationally.

The administration credential needs Service Bus Manage rights covering source
topics/subscriptions/rules and destination queues, including queue creation. A
Send/Listen-only credential is insufficient. Runtime still needs Listen on the two
queues and Send on the configured outgoing topic. No NServiceBus dependency is
introduced. HTTP Employment Check client implementation is outside this change.

Tests use an in-memory administration client and make no Service Bus connections.
