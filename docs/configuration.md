# Configuration

All settings live in `src/NotificationForwarder.Api/appsettings.json` and can be overridden with environment variables using the `Section__Key` syntax, or with `dotnet user-secrets` in Development. Options are validated when the host starts.

| Key | Default | Meaning |
| --- | --- | --- |
| `Queue:Capacity` | `1000` | Notifications that may wait for processing before intake answers 503. |
| `Ai:Enabled` | `false` | `true` uses OpenAI; `false` uses the template generator and needs no key. |
| `Ai:ApiKey` | empty | OpenAI API key. Required when enabled. Never commit it. Requests are sent with `store: false`, so OpenAI does not keep the exchange for its dashboard or model training. OpenAI's own abuse-monitoring retention still applies. |
| `Ai:Model` | `gpt-5.4` | An OpenAI model with structured-output support. |
| `Ai:BaseUrl` | `https://api.openai.com/v1/` | Must end with `/` and use https (plain http only for loopback). Change it only for a proxy or an OpenAI-compatible endpoint. |
| `Ai:RequestTimeout` | `00:00:15` | Per-request deadline, 100 ms to 60 s. |
| `Discord:Enabled` | `false` | `true` posts to the webhook; `false` writes alerts to the log. |
| `Discord:WebhookUrl` | empty | `https://discord.com/api/webhooks/{id}/{token}`. Required when enabled. Treat as a secret. The app appends `wait=true` so Discord confirms each post with 200 and the created message. |
| `Discord:Username` | `Notification Forwarder` | Display name of the webhook post. |
| `Discord:RequestTimeout` | `00:00:10` | Per-request deadline, 100 ms to 60 s. |

## Setting secrets

```sh
dotnet user-secrets --project src/NotificationForwarder.Api set "Ai:Enabled" "true"
dotnet user-secrets --project src/NotificationForwarder.Api set "Ai:ApiKey" "<your key>"
dotnet user-secrets --project src/NotificationForwarder.Api set "Discord:Enabled" "true"
dotnet user-secrets --project src/NotificationForwarder.Api set "Discord:WebhookUrl" "https://discord.com/api/webhooks/<id>/<token>"
```

User secrets are loaded only in the Development environment, which is what `dotnet run` uses via `Properties/launchSettings.json`.

## HTTP contract

`POST /apis/notification-forwarder/v1/notifications`, `Content-Type: application/json`, body at most 16 KiB.

```json
{ "level": "error", "message": "Npgsql: connection pool exhausted", "source": "payments-api" }
```

| Status | Body | When |
| --- | --- | --- |
| 202 | `{"id":"<guid>"}` | Queued for forwarding. The id is shown in the Discord message. |
| 204 | none | Below warning; ignored. |
| 400 | validation problem details, one entry per field | Invalid level, message or source (blank, too long, control characters, multi-line source); malformed JSON. |
| 503 | problem details, `Retry-After: 5` | Queue full. |
| 500 | problem details, no exception details | Unexpected failure inside the request. |

Health: `GET /health/live` and `GET /health/ready` return 200.
