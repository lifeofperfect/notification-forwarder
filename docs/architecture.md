# Architecture

## Layers

Four projects. Each depends only on the ones above it; `CleanArchitectureTests` enforces this.

| Project | Contains |
| --- | --- |
| `Domain` | `NotificationEntity`, `NotificationLevel`, `NotificationMessage`, `GeneratedAlert` (kind, title, message; shape rules; strips any severity word from the title), `DeliveryRateLimit` (pure arithmetic). No I/O. |
| `Application` | Use cases `ReceiveNotification` and `ProcessPendingNotification`; the interfaces they need (`INotificationBacklog`, `IAlertGenerator`, `IAlertSender`); `RollingSendGate`; `TemplateAlertGenerator` and the `FallbackAlertGenerator` decorator; per-feature DI. |
| `Infrastructure` | Channel-backed backlog, the worker that drains it, OpenAI client, Discord client, options and validators. |
| `Api` | One controller per endpoint, health checks, `Program.cs`. |

## Conventions

House style of the M-KOPA sales services, scaled down.

- One folder per use case: `XCommand`, `IXCommandHandler`, `XCommandHandler`, `XCommandValidator` (FluentValidation), `XCommandMapper`. Handlers return `Ardalis.Result`.
- DI by feature: `AddReceiveNotification()` and `AddProcessPendingNotification()` under `AddNotifications()`; each Infrastructure area has its own `XDependencyInjection.cs`; hosts call `AddApplication()` and `AddInfrastructure()`.
- Endpoints are single-action controllers named `XEndpoint` with an internal `XEndpointMapper`; request and response records sit beside them under `V1/`; routes come from `Routing.cs`.
- Interfaces live in `Application/Notifications/Common/Services/`; implementations in `Infrastructure/<Area>/Services/Implementation/`. `Infrastructure/Processing/` holds the worker, which implements no Application interface.
- Options are validated on start with `IValidateOptions<T>`.
- Unit tests mirror the source path under a layer folder and are named `XShould`. Integration tests are component tests: `TestWebApplicationFactory` boots the real host, replaces only the AI provider (wrapped by the real fallback), the alert sender and the clock, and tests drive it through HTTP (`<Endpoint>Tests`, `<Behaviour>Tests`). One wiring test opts into the real OpenAI and Discord adapters and fakes only their HTTP handlers. Tests wait on observable state (calls received, requests recorded), never on a fixed sleep.

## Request flow

1. `ReceiveNotificationEndpoint` maps the request to a command and calls `IReceiveNotificationCommandHandler`.
2. The handler validates. Invalid → 400. Below warning → 204. Otherwise it creates a `NotificationEntity` and enqueues it: full → 503, queued → 202.
3. `NotificationProcessingWorker`, the backlog's single reader, resolves `IProcessPendingNotificationCommandHandler` in a scope per item.
4. The handler is three calls: `IAlertGenerator.GenerateAsync`, `RollingSendGate.WaitForTurnAsync`, `IAlertSender.SendAsync`. In production the generator is `FallbackAlertGenerator` around the OpenAI client; an `AlertGenerationException` degrades to the template. A rejected send surfaces as `AlertDeliveryException`, which the worker logs before moving on.

## Rate limit

`DeliveryRateLimit.GetDelay` computes, from the ages of recent sends, how long until another send is allowed. `RollingSendGate` keeps the monotonic timestamp of each send under a lock, so a wall-clock adjustment cannot unlock or lock capacity, and waits with `Task.Delay(delay, TimeProvider)`, so tests advance a fake clock. The turn is taken after generation, directly before the send, so the gate counts exactly the messages leaving for Discord. A rejected send still consumes its turn.

## Failure handling

| Failure | Effect |
| --- | --- |
| OpenAI error, timeout, refusal, truncated or contract-breaking output | Template alert sent instead; reason logged; provider bodies never logged. |
| Discord non-2xx, timeout or transport error | Logged with reason, status and notification id; no retry; worker continues. |
| Any other exception while processing | Logged with the notification id; worker continues. |
| Queue full | 503 with `Retry-After`. |
| Restart | Queued notifications and rate-limit history are lost. |
