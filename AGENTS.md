# Development instructions

Read `specs/notification-forwarder.md` for behaviour and `docs/architecture.md` for structure and conventions before changing anything. Do not duplicate what they say here.

## Adding a feature

1. Command, result, handler interface and a stub handler in `Application/Notifications/<UseCase>/`.
2. `AddX()` in `NotificationsDependencyInjection.cs`, called from `AddNotifications()`.
3. Endpoint, mapper, request and response under `Api/Notifications/<UseCase>/V1/`.
4. Validator, then the real handler.
5. Tests mirroring the paths: `XCommandHandlerShould`, `XEndpointShould` (WebApplicationFactory with the handler substituted, `Arg.Do` to capture the command).
6. Add or change the spec's requirement scenarios, naming the tests that verify each, and update `docs/configuration.md` if the contract changes.

## Rules

- Warnings are errors. Shared namespaces go in each project's `GlobalUsings.cs`; no unused usings.
- `TimeProvider`, never `DateTime.UtcNow`. `[LoggerMessage]` for logs. `IValidateOptions<T>` with `ValidateOnStart()` for options.
- Never log or forward provider error bodies, credentials or webhook URLs.
- Tests: xUnit v3, Shouldly, NSubstitute, `FakeTimeProvider`; always pass `TestContext.Current.CancellationToken`. No test may touch the network or need a real key.
- Integration tests are outside-in component tests: use `TestWebApplicationFactory`, drive the app through HTTP, and stub only the AI provider and the alert sender through the factory's `AlertGenerator` and `AlertSender` fields; the real fallback decorator wraps the AI stub. Adapter wire contracts belong in unit tests with `FakeHttpMessageHandler`.

## Before handing over

```sh
dotnet format NotificationForwarder.slnx --verify-no-changes
dotnet build NotificationForwarder.slnx
dotnet test NotificationForwarder.slnx
```
