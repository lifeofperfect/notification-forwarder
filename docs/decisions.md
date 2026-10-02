# Decisions

## D-01: Four projects with an enforced dependency direction

A single project would do for this size, but the layering is the convention of the services this code sits beside, and the architecture test keeps it cheap to maintain.

## D-02: In-memory queue and rate-limit state, no database

The brief asks for a simple application and says nothing about durability. A bounded `Channel<T>` and ten timestamps give the same behaviour while the process runs, in about 100 lines, with millisecond tests. The cost is explicit: a restart loses queued alerts and resets the count, and only one instance may run. A durable store would go behind `INotificationBacklog` and `RollingSendGate` without touching the use cases. An earlier SQLite revision is in git history; it consumed most of the time budget.

## D-03: Hand-written sliding-log gate instead of `System.Threading.RateLimiting`

The framework limiter is segmented, so a strict rolling count can briefly reach twice the limit, and it cannot be driven by a `TimeProvider`, so window-expiry tests must wait in real time. `RollingSendGate` is exact, about 40 lines, measures with the monotonic timestamp so wall-clock changes do not affect it, and is tested with a fake clock.

## D-04: The send turn is taken after generation

The brief caps messages sent, so the gate sits directly before the send and counts exactly that. An AI failure never consumes a turn. Under a burst an alert may be generated and then wait up to a minute; it does not go stale, since it describes a message that was already written.

## D-05: AI failure falls back to a template

The LLM improves the message; it is not what makes the alert important, so an AI outage must not silence an error. `FallbackAlertGenerator` wraps the OpenAI client and switches to a template alert that names a coarse kind, quotes the source and text with credential-looking values redacted, and says the assistant was unavailable. The template is built so it cannot fail for any accepted notification. The fallback is logged.

## D-06: The model decides the kind and writes the message; code validates shape only

The brief does not define the kinds, so neither does the design. The model returns `kind` as its own short label, `title` as a headline without severity, and `message` with the facts plus at most one sentence of inference, worded as such. The severity prefix on the channel line is built by code from the notification's level, never taken from generated text, and a severity word the model adds anyway is stripped. The prompt forbids suggested actions and conclusions drawn from what the message does not say, so every sentence can be traced to the input. An earlier revision used a fixed list of six categories and forbade all inference, which made the AI a classifier plus a paraphraser. Free-text kinds cannot be grouped reliably; a closed list can be reintroduced in the schema if that is ever needed. Because the text derives from untrusted input, Discord mentions are suppressed in the payload and the request is sent with `store: false`, which keeps the exchange out of OpenAI's dashboard and training; it is not zero retention, since OpenAI's abuse-monitoring retention still applies.

## D-07: Providers are opt-in

`Ai:Enabled` and `Discord:Enabled` default to false, so a fresh clone runs without secrets and the test suite never calls a paid API. The switch itself is pinned by a DI test; the adapters are tested against a fake HTTP handler, in isolation and once through the whole host.

## D-08: Not included

Authentication, idempotency keys, a status endpoint, retries, Docker, CI and deployment pipelines. None is asked for by the brief and each adds surface a reviewer must read. The three commands in `AGENTS.md` are the whole verification.

## D-09: One spec file in requirement and scenario form

The spec borrows the OpenSpec format used in the M-KOPA services: SHALL requirements, WHEN/THEN scenarios, behaviour only. It does not borrow the change workflow (a proposal, design, delta spec and task list per change, archived by date). There is one delivery here and git is its history; the workflow would be more text than code. Each scenario names its tests instead, which that workflow keeps in the task list.
