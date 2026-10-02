# NotificationForwarder

Receives notifications over HTTP. For anything at **warning level or higher**, asks an LLM (OpenAI) what kind of problem it is and to write the alert, then posts it to a **Discord** webhook. At most **10 alerts per rolling minute** leave the service.

```text
 client ──POST /apis/notification-forwarder/v1/notifications──▶ Api ──(level >= warning?)──▶ queue ──▶ worker ──▶ OpenAI ──▶ Discord
                                                                 │                                       │
                                                            202 Accepted                        10 per 60 s, in order
```

## Run

Requires the .NET 10 SDK.

```sh
dotnet run --project src/NotificationForwarder.Api
```

Both providers are off by default, so no secret is needed: alerts come from a template and go to the console log.

```sh
curl -i http://localhost:5080/apis/notification-forwarder/v1/notifications \
  -H 'Content-Type: application/json' \
  -d '{"level":"error","message":"Npgsql: connection pool exhausted after 30s","source":"payments-api"}'
```

You get `202 Accepted` with `{"id":"<guid>"}` and, a moment later, an `ERROR: Database problem` alert in the console. An `info` notification gets `204 No Content`. To use OpenAI and Discord for real, set the secrets in [docs/configuration.md](docs/configuration.md).

## Test

```sh
dotnet test NotificationForwarder.slnx
```

No network, no secrets. Unit tests cover the rules, the handlers, the rate gate with a fake clock, and the OpenAI and Discord adapters against a fake HTTP handler. Integration tests are component tests: they boot the real host and drive it through HTTP, with only the AI provider and the Discord sender replaced by stubs, so validation, the backlog, the worker, the rate limit and the AI fallback run as in production. One wiring test keeps the real OpenAI and Discord adapters and fakes only the network, proving the two hops connect.

## How it works

- **Intake** validates the payload. Below warning: 204 and done. Warning or higher: queued in memory and acknowledged with 202. Queue full: 503 with `Retry-After`.
- **A single worker** drains the queue in order. For each notification it asks OpenAI for the alert, waits for a send turn, and posts to Discord. If OpenAI fails for any reason, a template alert is sent instead.
- **The model** decides the `kind` in its own words, writes a `title` and a factual `message` with at most one sentence of marked inference. Code checks the shape, prefixes the title with the notification's own level, and lays it out: content line, then kind and message in one embed, mentions suppressed.

## Repository

```text
src/
  NotificationForwarder.Domain/          entities and pure rules
  NotificationForwarder.Application/     use cases, interfaces, RollingSendGate, template and fallback generators
  NotificationForwarder.Infrastructure/  channel queue, worker, OpenAI client, Discord client
  NotificationForwarder.Api/             endpoint, health checks, Program.cs
tests/Common/                            FakeHttpMessageHandler, linked into both test projects
tests/NotificationForwarder.UnitTests/   mirrors src; adapter tests with a fake HTTP handler; the architecture test
tests/NotificationForwarder.IntegrationTests/  component tests through the real host (TestWebApplicationFactory)
specs/notification-forwarder.md          requirements as WHEN/THEN scenarios, each naming the tests that prove it
docs/architecture.md                     layers, conventions, request flow, failure handling
docs/decisions.md                        why things are the way they are
docs/configuration.md                    settings and the HTTP contract
docs/llm-scenarios.md                    recorded live runs against gpt-5.4
```

## Limitations

- Queue and rate-limit state are in memory: a restart loses queued alerts and resets the count. Run one instance.
- No retry on a Discord failure; it is logged and the worker moves on.
- No authentication on the intake endpoint.
- Classification quality is not covered by automated tests; see the recorded live runs.
