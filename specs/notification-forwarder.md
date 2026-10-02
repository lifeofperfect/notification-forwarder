# Specification

Source: the "Notification application with HTTP interface" assignment (~4 h). Deployment is not required; a Git repository is appreciated.

Requirements are written as SHALL statements with WHEN/THEN scenarios. Every scenario names the tests that prove it. The spec describes what can be observed from outside: HTTP responses, provider requests, log outcomes and timing. How the code is arranged is in `docs/architecture.md`; why, in `docs/decisions.md`.

## 1. Brief to design

| ID | The brief asks for | This design |
| --- | --- | --- |
| REQ-01 | C#. | .NET 10, ASP.NET Core. |
| REQ-02 | Receive notifications over HTTP; the payload has a `level`. | `POST /apis/notification-forwarder/v1/notifications` with `level`, `message`, optional `source`. |
| REQ-03 | Forward warning or higher to an external interface, e.g. Discord. | Warning and above are queued and delivered to a Discord webhook. |
| REQ-04 | Use an LLM to determine the kind of warning/error and generate the message. | OpenAI returns `kind`, `title` and `message` under a strict JSON schema. Code validates shape only. A template alert is sent if the call fails. |
| REQ-05 | At most 10 such messages per minute. | A rolling 60-second window directly in front of every Discord send. Excess waits. |
| REQ-06 | Unit tests. | `tests/NotificationForwarder.UnitTests`. |
| REQ-07 | Integration tests. | `tests/NotificationForwarder.IntegrationTests`: the real host driven through HTTP, with only the AI provider and the Discord sender stubbed. |
| REQ-08 | Documentation. | `README.md`, `docs/`, this file. |

## 2. Requirements

### Requirement: Intake accepts a notification and answers without waiting on any provider (REQ-02, REQ-03)

The service SHALL accept `level`, `message` (1 to 2000 characters; tabs and line breaks allowed, no other control characters) and optional `source` (one line of up to 100 characters). `level` SHALL be one of trace, debug, info, warning (alias warn), error (alias err) or critical (alias fatal), case-insensitive. The response SHALL NOT depend on OpenAI or Discord.

#### Scenario: Warning or higher is queued

- **WHEN** a notification with level warning, error or critical is posted
- **THEN** the response is `202` with body `{"id":"<guid>"}`
- **AND** the notification reaches the sender with that id, level, message and source
- Verified by `ReceiveNotificationEndpointTests.PostWhenWarningOrHigherShouldQueueAndHandTheNotificationToTheSender`, `ReceiveNotificationEndpointShould.Return202WithTheIdAndPassEveryFieldToTheHandler`, `ReceiveNotificationCommandHandlerShould.QueueWarningOrHigherWithReceiptTime`

#### Scenario: Below warning is ignored

- **WHEN** a notification with level trace, debug or info is posted
- **THEN** the response is `204` with no body
- **AND** nothing is generated or forwarded
- Verified by `ReceiveNotificationEndpointTests.PostWhenBelowWarningShouldAnswer204AndNotForward`, `ReceiveNotificationCommandHandlerShould.IgnoreLevelsBelowWarningWithoutQueueing`, `NotificationLevelShould.ForwardWarningAndAbove`, `NotificationEntityShould.RefuseLevelsBelowWarning`

#### Scenario: Level names are forgiving

- **WHEN** `level` is `WARNING`, ` warning `, `warn`, `err` or `fatal`
- **THEN** it is read as warning, warning, warning, error and critical
- Verified by `NotificationLevelShould.ParseKnownNamesCaseInsensitively`

#### Scenario: An invalid payload names every bad field

- **WHEN** `level` is unknown, `message` is blank, over 2000 characters or contains a control character, or `source` is over 100 characters or not a single line
- **THEN** the response is `400` problem details with one entry per invalid field
- **AND** nothing is queued
- Verified by `ReceiveNotificationEndpointTests.PostWhenPayloadIsInvalidShouldReturnValidationProblemWithEveryFieldError`, `ReceiveNotificationCommandValidatorShould.ReportEveryInvalidField`, `ReceiveNotificationCommandValidatorShould.RejectMessagesAboveTheLimit`, `ReceiveNotificationCommandValidatorShould.RejectControlCharactersButAllowLineBreaksInTheMessage`, `NotificationLevelShould.RejectUnknownNames`

#### Scenario: An unreadable body is refused before any handling

- **WHEN** the body is empty, the JSON literal `null`, or malformed JSON
- **THEN** the response is `400`
- **AND** the handler is never called
- Verified by `ReceiveNotificationEndpointShould.Return400ForAnUnreadableBodyWithoutCallingTheHandler`

#### Scenario: A burst is accepted at once

- **WHEN** fifteen error notifications are posted within a second
- **THEN** every one is answered `202` immediately, although only ten can be sent in the first minute
- Verified by `NotificationForwardingTests.WorkerWhenMoreThanTenArriveInAMinuteShouldSendTenThenTheRestAfterTheWindowInOrder`

### Requirement: Intake fails safely (REQ-02)

The service SHALL hold at most `Queue:Capacity` (default 1000) notifications awaiting delivery, and SHALL never expose exception detail to the caller.

#### Scenario: The backlog is full

- **WHEN** a notification is posted while the backlog holds its capacity
- **THEN** the response is `503` with `Retry-After: 5`
- Verified by `BacklogCapacityTests.PostWhenTheBacklogIsFullShouldAnswer503WithRetryAfter`, `ReceiveNotificationCommandHandlerShould.ReportUnavailableWhenTheQueueIsFull`

#### Scenario: An unexpected exception

- **WHEN** handling throws
- **THEN** the response is `500` problem details
- **AND** the body carries neither the exception type nor its message
- Verified by `ReceiveNotificationEndpointShould.Return500ProblemDetailsWithoutExceptionDetailsWhenTheHandlerThrows`

#### Scenario: Health probes

- **WHEN** `/health/live` or `/health/ready` is requested
- **THEN** the response is `200`
- Verified by `ReceiveNotificationEndpointTests.GetHealthProbesShouldAnswer200`

### Requirement: The model determines the kind and writes the message (REQ-04)

For each queued notification the service SHALL ask OpenAI for `kind` (what sort of warning or error this is, in the model's own words), `title` (a headline without a severity word) and `message` (the facts, plus at most one sentence of inference worded as such). The prompt SHALL be sent as the system message and the notification as user data. The API key SHALL travel only in the `Authorization` header. The request SHALL ask for a strict JSON schema with exactly those three fields and `store: false`. Output SHALL be accepted only if all three fields are present, single-line where required, within length, and free of control characters.

#### Scenario: The request to OpenAI

- **WHEN** a queued notification is processed with the AI provider enabled
- **THEN** one `POST` goes to `/v1/chat/completions` with the configured model, the prompt as the system message, the level, source and message as user content, `response_format` of type `json_schema` with `strict: true` and required `kind`, `title`, `message`, and `store: false`
- **AND** the key appears in the `Authorization` header and nowhere else
- Verified by `OpenAiAlertGeneratorShould.RequestStrictJsonWithTheInstructionsSeparatedFromTheUntrustedMessage`, `AdapterWiringTests.PostWithRealAdaptersShouldCallOpenAiThenPostTheGeneratedAlertToDiscord`

#### Scenario: Well-formed output is accepted

- **WHEN** the model returns a kind, a title and a message within the limits
- **THEN** the alert carries those three values, trimmed
- Verified by `GeneratedAlertShould.AcceptWellFormedOutputAndTrimIt`

#### Scenario: The provider answers with an error

- **WHEN** OpenAI answers with a non-success status
- **THEN** generation fails with the status only
- **AND** the provider's response body is never logged or surfaced
- Verified by `OpenAiAlertGeneratorShould.ReportProviderErrorsWithoutLeakingTheirBody`

#### Scenario: The reply breaks the contract

- **WHEN** the reply is refused, truncated, empty, not JSON, missing a field, carrying an extra field, multi-line where a single line is required, over length, or containing control characters
- **THEN** generation fails
- Verified by `OpenAiAlertGeneratorShould.RejectIncompleteRefusedOrEmptyReplies`, `OpenAiAlertGeneratorShould.RejectOutputThatBreaksTheContract`, `GeneratedAlertShould.RejectOutputThatBreaksTheContract`, `GeneratedAlertShould.RejectOverlongFields`

#### Scenario: Generation fails

- **WHEN** generation fails for any of the reasons above, or times out
- **THEN** a template alert is sent instead, with a coarse kind chosen by keyword, a title of `<kind> reported by <source>`, and a message that quotes the source and text and says the assistant was unavailable
- **AND** the template never fails itself: the title is cut to the limit for the longest source, and values that look like credentials (`password=`, `token:`, `Bearer ...`) are redacted from the quoted text
- **AND** the failure is logged
- Verified by `NotificationForwardingTests.WorkerWhenTheAiGeneratorFailsShouldSendTheTemplateAlertInstead`, `FallbackAlertGeneratorShould.UseTheTemplateWhenThePrimaryFails`, `TemplateAlertGeneratorShould.PickAKindByKeyword`, `TemplateAlertGeneratorShould.QuoteTheSourceAndMessageAndSayTheAssistantWasUnavailable`, `TemplateAlertGeneratorShould.FitTheTitleForTheLongestAllowedSource`, `TemplateAlertGeneratorShould.RedactCredentialsInTheQuotedText`

#### Scenario: Shutdown during generation

- **WHEN** the service is stopping while a generation is in flight
- **THEN** the cancellation propagates and no template alert is sent
- Verified by `FallbackAlertGeneratorShould.NotSwallowCancellation`

### Requirement: Alerts are delivered to Discord (REQ-03)

The service SHALL post each alert to the configured webhook with `wait=true`. The `content` line SHALL be the notification's level in upper case, a colon, and the generated title; the level SHALL come from the notification, never from generated text. One embed SHALL carry the kind as its title, the message as its description, and fields for level, source and notification id. Mentions SHALL be suppressed.

#### Scenario: The Discord payload

- **WHEN** an alert for a critical notification is sent
- **THEN** `content` is `CRITICAL: <title>`
- **AND** the single embed has the kind as title, the message as description, and fields `Level`, `Source` and `Notification`
- **AND** `allowed_mentions.parse` is empty
- Verified by `DiscordWebhookAlertSenderShould.PostTheTitleAsContentAndTheRestInOneEmbedWithMentionsSuppressed`, `AdapterWiringTests.PostWithRealAdaptersShouldCallOpenAiThenPostTheGeneratedAlertToDiscord`

#### Scenario: The model adds a severity word anyway

- **WHEN** the generated title starts with a severity word such as `ERROR:` or `warning -`
- **THEN** that prefix is removed before the title is used
- Verified by `GeneratedAlertShould.StripASeverityPrefixTheGeneratorAddedAnyway`

#### Scenario: Discord rejects or is unreachable

- **WHEN** Discord answers with a non-success status, or the request fails in transport
- **THEN** delivery fails with the status, or without one for transport errors
- **AND** the failure is logged with the notification id
- **AND** the next notification is still delivered
- Verified by `DiscordWebhookAlertSenderShould.ThrowWithTheStatusWhenDiscordRejectsTheMessage`, `DiscordWebhookAlertSenderShould.ThrowADeliveryExceptionForTransportFailures`, `NotificationForwardingTests.WorkerWhenDiscordRejectsOneAlertShouldStillDeliverTheNext`, `NotificationProcessingWorkerShould.KeepDrainingTheBacklogAfterDeliveryAndUnexpectedFailures`

### Requirement: At most ten messages leave in any rolling minute (REQ-05)

The service SHALL send at most ten alerts in any 60-second window, counted at the moment of sending, in arrival order. An alert that cannot be sent yet SHALL wait, not be dropped. Generation SHALL NOT consume a send turn.

#### Scenario: Fifteen notifications in a burst

- **WHEN** fifteen notifications arrive within a second
- **THEN** ten are sent at once and the eleventh waits
- **AND** nothing more leaves 59 seconds later
- **AND** the remaining five are sent once the window has slid, in arrival order
- Verified by `NotificationForwardingTests.WorkerWhenMoreThanTenArriveInAMinuteShouldSendTenThenTheRestAfterTheWindowInOrder`, `RollingSendGateShould.AllowTenTurnsThenRefuseUntilTheWindowSlides`

#### Scenario: The window slides per send

- **WHEN** one alert was sent at t = 0 and nine more at t = 20 s
- **THEN** the next may leave at t = 60 s and the one after it at t = 80 s
- **AND** sends older than 60 s no longer count
- Verified by `RollingSendGateShould.ReleaseTurnsOneAtATimeAsSendsAge`, `DeliveryRateLimitShould.WaitForTheOldestOfTenToExpire`, `DeliveryRateLimitShould.IgnoreSendsOlderThanTheWindow`, `DeliveryRateLimitShould.AllowSendingWhileFewerThanTenInTheWindow`

#### Scenario: The turn is taken directly before the send

- **WHEN** the eleventh notification in a minute is processed
- **THEN** its alert is generated first and the send waits for a turn
- Verified by `ProcessPendingNotificationCommandHandlerShould.WaitForATurnBeforeTheEleventhSendInAMinute`, `ProcessPendingNotificationCommandHandlerShould.GenerateThenSendTheGeneratedAlert`

#### Scenario: Shutdown while waiting for a turn

- **WHEN** the service stops while an alert is waiting for a turn
- **THEN** the wait is cancelled and the alert is not sent
- Verified by `RollingSendGateShould.HonourCancellationWhileWaiting`

#### Scenario: The wall clock is adjusted

- **WHEN** the system clock jumps forward by more than a minute after ten sends, without a minute of real time passing
- **THEN** the eleventh send still waits, because ages are measured with the monotonic clock
- Verified by `RollingSendGateShould.IgnoreWallClockAdjustments`

### Requirement: Providers are opt-in

`Ai:Enabled` and `Discord:Enabled` SHALL default to false, so a fresh clone runs and the test suite passes without secrets or network. With a provider disabled the service SHALL use the template generator or a sender that only logs. Settings are in `docs/configuration.md`.

#### Scenario: Both providers disabled

- **WHEN** the service starts with the defaults
- **THEN** alerts are generated by the template and written to the log, and no outbound HTTP is made
- Verified by `InfrastructureDependencyInjectionShould.UseTheTemplateGeneratorAndTheLogSenderWhenProvidersAreDisabled`

#### Scenario: Both providers enabled

- **WHEN** the service starts with both enabled and configured
- **THEN** a queued notification travels to OpenAI and then to Discord
- **AND** an OpenAI failure still falls back to the template
- Verified by `InfrastructureDependencyInjectionShould.WrapOpenAiInTheFallbackAndUseDiscordWhenProvidersAreEnabled`, `AdapterWiringTests.PostWithRealAdaptersShouldCallOpenAiThenPostTheGeneratedAlertToDiscord`

## 3. Out of scope

Persistence across restarts, multiple instances, retries, authentication, a status endpoint, CI and deployment. Reasons are in `docs/decisions.md`.
