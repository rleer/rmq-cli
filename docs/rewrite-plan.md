# rmq rewrite plan

Working document for the rebuild on branch `rebuild`. Delete or archive this
once the rewrite lands — the durable constraints live in `CLAUDE.md`, and this
file holds only the transition: what exists today, what it becomes, in what
order, and which questions are still open.

**Rule of thumb for what goes where:** `CLAUDE.md` states the prohibition; this
file records the archaeology. "Do not reintroduce batched acks" is a constraint.
"The old `AckHandler` was 75 lines and did this" is history.

## Baseline (measured 2026-08-14, osx-arm64)

| Metric | Value | Note |
|---|---|---|
| Startup, `--help` | **6.7 ms** | 20-run average, AOT binary, warm cache |
| Binary size | **16.4 MB** | AOT, trimmed, self-contained |
| Trim/AOT warnings | **0** | no `IL2xxx`/`IL3xxx` at publish |
| Source | **6,249 LOC** | `src/RmqCli`, 79 files (excl. `obj/`) |
| Tests | **18,216 LOC** | 4 test projects + 1 shared library, ~2.9× source |
| `PackageReference` | **10** | drops to **2** — the other two allowlisted packages are in-box |

Two findings that correct assumptions behind the rewrite:

- **Startup was never the problem.** 6.7 ms already sits far under any
  reasonable budget. The 20 ms figure in `CLAUDE.md` is a *regression guard*, not
  a target to work toward. Nobody should optimize for speed during this rewrite.
- **Tomlyn is not an AOT blocker.** Verified empirically: a TOML value bound
  correctly through the AOT binary (`--host` resolved to `toml-host-marker:5673`
  from a config file). Dropping TOML is justified by "too many configuration
  mechanisms," *not* by AOT incompatibility. If a config file is ever wanted
  again, Tomlyn is not disqualified on those grounds.

## Target

~1,500–2,000 LOC of source, 4 allowlisted dependencies (2 `PackageReference`),
2 test projects. The reduction comes almost entirely from deleting abstraction
layers, not from cutting features — the only features dropped are `Reject` ack
mode, file rotation, `--prefetch-count`, and the `config` command subtree.
`--consumer-priority`, `amqps://`, and `--insecure` are net-new.

## Salvage inventory

### Keep — ~377 LOC, mostly a namespace move

These are pure functions with no DI or Spectre coupling, and they are exactly
the set the thin unit-test slice covers.

| File | LOC | Change |
|---|---|---|
| `Commands/Publish/JsonMessageParser.cs` | 107 | namespace only |
| `Commands/Publish/PropertyMerger.cs` | 74 | namespace only |
| `Commands/Publish/HeaderParser.cs` | 62 | namespace only |
| `Shared/Json/JsonSerializationContext.cs` | 55 | retarget at the new DTOs |
| `Shared/Json/BodyJsonConverter.cs` | 45 | namespace only |
| `Core/Models/MessageProperties.cs` | 34 | drop `ClusterId` + its `HasAnyProperty()` clause |

### Salvage the knowledge, rewrite the code

- **`Infrastructure/RabbitMq/RabbitChannelFactory.cs` (257).** The connection
  setup is thin, but two things inside are worth keeping: the `SslOption` block
  (now driven by the `amqps://` scheme and `--insecure`) and the broker-error mapping in
  `HandleConnectionException` (reply code 530 → vhost-not-found vs. access-denied,
  plus `AuthenticationFailureException` / `ConnectFailureException` /
  `BrokerUnreachableException`). That mapping is the entire basis for **exit code 1**
  and its `--help`-quality error messages. Copy the *table*, not the class — it is
  currently welded to `IStatusOutputService`, `ErrorInfoFactory`, and `ILogger`.
- **`Shared/Factories/RabbitErrorInfoFactory.cs` (68).** Same: the message and
  suggestion strings are good, the factory indirection is not.

### Delete outright — 5,872 LOC (94% of source)

Counted, not estimated; 5,872 deleted + 377 salvaged = 6,249 total.

| Group | Files | LOC | Blocks dropping |
|---|---|---|---|
| Command handlers/services | `Commands/{Publish,Consume,Peek,Purge}/**`, `RootCommandHandler.cs` — less the 243 LOC of salvaged parsers | 2,000 | — |
| Output framework | `Shared/Output/**` (formatters, factories, `StatusOutputService`, `ConsoleOutput`, `FileOutput`) | 1,275 | Spectre.Console |
| Retrieval abstraction | `Commands/MessageRetrieval/**` (incl. `Strategies/`), `Shared/AckHandler.cs` | 657 | — |
| DI wiring | `Shared/Extensions/ServiceCollectionExtensions.cs`, `Shared/Factories/ServiceFactory.cs` | 580 | M.E.DependencyInjection, M.E.Logging* |
| Config subsystem | `Infrastructure/Configuration/**`, `Commands/Config/**`, `rmq-cli.toml` | 452 | Tomlyn, M.E.Configuration* |
| RabbitMQ infrastructure | `Infrastructure/RabbitMq/**` — rewritten, knowledge salvaged | 435 | — |
| Core models | `Core/Models/**` — less `MessageProperties` (34) | 179 | — |
| Misc | validators, error factories, `AnsiConsoleFactory`, `ICommandHandler`, old `Program.cs` | 294 | — |

`Shared/Json/**` (100 LOC) appears in neither row — both its files are salvaged
intact.

Note that `Core/Models/Message.cs` is **replaced, not merely deleted**. The old
DTO is `Body` / `Properties` / `Headers`; the NDJSON schema below is `body` /
`properties` / `routingKey`, with headers nested inside `properties`. Whoever
writes Phase 1 should treat the DTO shape as new work, not a carry-over.

The 94% figure is the honest scale of this: it is closer to a greenfield rewrite
that reuses six files than to a refactor. Plan effort accordingly — the risk is
not in the deleting, it is in the ~1,500 LOC of genuinely new code below.

Test projects deleted whole: `RmqCli.Integration.Tests`,
`RmqCli.Subcutaneous.Tests`, `RmqCli.Tests.Shared` (fold its `RabbitMqFixture` /
`RabbitMqOperations` container plumbing into E2E first). `RmqCli.Unit.Tests` is
gutted down to the parser tests listed above.

## New code with no predecessor

These are where effort actually goes; nothing in the current tree does them.

1. **Connection resolution** — parse `amqp://user:pass@host:port/vhost`, apply
   the `flags > --url > $RMQ_URL > defaults` precedence *per component*, so
   `--url amqp://prod/ --vhost /test` works. Also accept `http(s)://` implying
   `--transport http`. This is the single most-tested pure function in the rewrite.
2. **Exit codes** — nothing today produces code 3 (`--count` unmet). The whole
   table is new and needs threading through both consume paths.
3. **TTY detection + the human-readable form** — replaces 622 LOC of Spectre
   formatters with, target, under 80 LOC written directly.
4. **Push-path idle window** — 1 s with no delivery, after which the subscriber
   concludes the queue is empty. Also the sole source of exit code 3 on the push
   path, so it is correctness-bearing, not just a convenience.
5. **Static stderr `Log`** — gated on `--verbose`, replacing `ILogger` throughout.
6. **HTTP transport** — the current `RabbitManagementClient` (142) only does
   purge; publish and get over HTTP are new.
7. **`--consumer-priority`** — the current code passes no consumer arguments at
   all, so `x-priority` is new capability rather than a port.
8. **`amqps://` + `--insecure`** — TLS exists today as three config keys; it
   becomes scheme-driven, which is new parsing rather than moved code.

## NDJSON schema — a deliverable, not an afterthought

`CLAUDE.md` requires that `publish` accept exactly what `consume` emits. That is
only testable once the field list is written down, so **write the schema first**,
before either side is implemented, and derive both serializer and parser from it.

`ClusterId` is **out** (resolved below), so the schema's property list is exactly
the one in `CLAUDE.md` — nothing more.

## Phases

Each phase ends **green**: `dotnet build` succeeds, and from Phase 2 onward the
two non-negotiable checks pass. A package may only be dropped once its last
consumer is gone, which is what sets the phase boundaries.

### Phase 1 — Foundation (nothing deleted, tree still builds and runs)

Add alongside the existing code, wired to nothing yet:

- connection resolution + URL parsing
- static `Log` (stderr, `--verbose`)
- JSON source-gen context and the message DTOs, per the schema above
- NDJSON writer + TTY detection
- move the six salvaged parser files into the new namespace

**Checkpoint:** build green; new unit tests for URL-precedence pass. The old CLI
still works unchanged — this phase is purely additive.

### Phase 2 — The sweep: new `Program.cs` + `publish`, old tree deleted

The moment the old world dies; unavoidably the largest phase.

- new `Program.cs`: `System.CommandLine` root, objects constructed by hand
- `publish` against AMQP, reading `--body` / `--message` / `--message-file` / STDIN
- delete every group in the deletion table above
- drop Spectre.Console, Tomlyn, and all six `Microsoft.Extensions.*` packages
  (`Configuration`, `.Binder`, `.EnvironmentVariables`, `DependencyInjection`,
  `Logging`, `.Console`) — 10 `PackageReference` entries become 2
  (`RabbitMQ.Client`, `System.CommandLine`); the other two allowlisted
  dependencies are in-box and carry no `PackageReference`
- delete `RmqCli.Integration.Tests`, `RmqCli.Subcutaneous.Tests`, and the old
  E2E tests targeting `peek` / `config` / `--ack-mode`

**Checkpoint:** build green, **zero `IL2xxx`/`IL3xxx`**, `--help` under 20 ms,
`rmq publish` verified by hand against a local broker.

> **Coverage gap opens here.** From the moment the old E2E tests are deleted
> until the new round-trip test lands in Phase 3, there is no automated safety
> net. Keep this window to a single sitting — do not start Phase 2 without
> going straight into Phase 3.

### Phase 3 — `consume` + the first new test

- sequential receive → write → flush → ack loop
- push (default) and `--pull`
- `--count`, `--follow`, `--to-file`, `--consumer-priority` (no prefetch flag —
  internal constants)
- `--requeue` via hold-unacked-then-close, **not** per-message nack
- exit codes 0 / 1 / 2 / 3 / 130

**Write the publish → consume round-trip E2E test first**, as soon as consume
returns a single message — not at the end of the phase. Then add the
`--requeue` terminates-and-preserves-depth test, which is the regression guard
for the nack loop.

**Checkpoint:** round-trip and `--requeue` E2E tests green; AOT + startup checks.

### Phase 4 — `purge`

`IChannel.QueuePurgeAsync(queue, ct)` — confirmed present in RabbitMQ.Client
7.2.0. Small phase; one E2E test.

### Phase 5 — HTTP transport

`--transport http` for publish, get, and purge.

**The drain question splits by ackmode** — verified against the HTTP API
reference, which documents `count`, `ackmode`, `encoding`, and `truncate`:

- `ack_requeue_false` "positively acknowledges the messages and marks them for
  deletion" — so repeated `/get` calls **do** drain a queue.
- `ack_requeue_true` / `reject_requeue_true` "requeue the fetched messages" — so
  repeated calls return the same messages forever. **Cannot drain**, and must
  not loop.

So destructive consume over HTTP behaves like AMQP (loop until empty, honoring
`--count`), while `--requeue` is capped at a single `/get` and warns that the
queue was not drained. Two separate implementations; see the table in `CLAUDE.md`.

An earlier draft of this plan proposed deriving a count from `messages_ready` to
fake a requeue drain. **Do not** — it races any live producer, and the "degraded
fallback, do not engineer around it" rule applies squarely.

Two implementation details that are easy to get wrong:

- **`payload_encoding`** — `/get` returns the body as a plain string only when it
  is valid UTF-8, and base64 otherwise. Handle both or binary bodies silently
  corrupt, breaking the round-trip guarantee the E2E suite exists to protect.
- **Never send `truncate`** — it would silently shorten bodies.
- **Batch size is the loss window.** `ackmode` is applied server-side before the
  response is sent, so a crash mid-write loses the entire batch. Pick a small
  constant; this path is for troubleshooting, not throughput.

**Checkpoint:** one E2E round trip over HTTP, plus a `--requeue --transport http`
test asserting it terminates and leaves queue depth unchanged. Not a parallel suite.

### Phase 6 — Test suite completion

Fill in the remaining E2E cases from `CLAUDE.md` (property round-trip fidelity
via the `consume | publish` pipe, exit code 3) and the unit slice. Confirm the
final two-project shape.

### Phase 7 — Docs and packaging

README rewrite (it still documents TOML, `config`, `peek`, and `RMQCLI_*`),
codecov badge/gate decision, `.csproj` metadata, release notes calling out the
`RMQCLI_*` → `$RMQ_URL` breaking change.

## Open questions

All resolved as of 2026-08-14 — Phase 1 is unblocked. One informational item
remains, and it no longer affects correctness:

- **Does `basic.qos` prefetch apply to `basic.get`?** Not documented on
  RabbitMQ's consumer-prefetch page, and the AMQP 0-9-1 reference has moved to
  GitHub. **This stopped mattering once `--prefetch-count` became internal:**
  requeue mode now sets `basic.qos(0)` unconditionally on both paths, so if
  prefetch does not apply to `basic.get` the call is a harmless no-op, and if it
  does, it is exactly what is needed. Behavior is identical either way. Worth
  confirming in Phase 3 only to decide whether the pull path can skip the call.

### Resolved

- **`--consumer-priority` added** (2026-08-14) — sets `x-priority` on
  `BasicConsumeAsync`, default **0**. The current code passes no consumer
  arguments at all (`SubscriberStrategy.cs:33` uses the minimal overload), so
  this is new capability, not a port.

  Silently ignored with `--pull` and `--transport http` rather than rejected —
  chosen specifically so it does not reintroduce the conditional flag validation
  that removing `--prefetch-count` eliminated. Establishes a general principle
  worth keeping: **inapplicable flags are no-ops, not usage errors.**

  Default 0 rather than a negative "polite" value, because a low-priority
  consumer on a healthy busy queue receives nothing and would exit reporting the
  queue empty — a worse failure than competing, and `--pull` / `--requeue`
  already cover the do-no-harm case.
- **`--prefetch-count` removed from the CLI** (2026-08-14) — broker QoS and the
  internal channel bound are now compile-time constants. Rationale: a
  user-settable prefetch bought nothing but flag-validation complexity and a way
  to construct broken states. Two concrete simplifications fall out:
  - the old validator rejecting `--prefetch-count` together with
    `--ack-mode requeue` has nothing left to validate and is deleted; the only
    surviving `consume` validation is `--requeue` + `--follow`
  - the "two numbers" hazard becomes an implementation note rather than a
    user-facing trap

  Pick the constants deliberately and name them: broker prefetch **100** for
  normal consume (matching the old default), **0** under `--requeue`; internal
  `Channel<T>` bound a fixed small value in both cases. They are invisible to
  users, so an accidental change will not be caught by anything but review.
- **Message count semantics** (2026-08-14) — **no `--count` means all messages,
  independent of `--requeue`.** Neither command behaves this way today, which is
  why it was asked: `consume --count` defaults to `-1` ("continuous consumption"
  — blocks until Ctrl-C and never exits on empty), and `peek --count` defaults
  to `1`. Both defaults are replaced by drain-and-exit.

  Making `--requeue` drain requires broker prefetch 0 (so delivery doesn't stall
  on unacked) while keeping the internal channel bound fixed and small (so client
  memory stays flat). The old code set `prefetch = 0` in requeue mode *and* sized
  its buffer from the same value, which is exactly the "unbound buffer growth"
  its own `ConsumeService` warned about. Keep the two numbers separate.

  Warning rule: without `--count`, always warn about unbounded growth; with
  `--count N`, only when N ≥ 1000 (the threshold the old `peek` used).
- **Idle window** (2026-08-14) — **1 second** on the push path. Doubles as the
  exit-code-3 signal; see `CLAUDE.md`.
- **`ClusterId`** (2026-08-14) — **dropped** from the message schema, deprecated
  in AMQP 0-9-1. Note this makes `MessageProperties.cs` a *near*-verbatim
  salvage rather than an unchanged one: the property and its clause in
  `HasAnyProperty()` both go.
- **TLS** (2026-08-14) — settled as the `amqps://` URL scheme plus a single
  `--insecure` flag; see the scheme table in `CLAUDE.md`. The old
  `UseTls` / `TlsServerName` / `TlsAcceptAllCertificates` triple collapses into
  scheme-plus-one-flag, and SNI derives from the URL host rather than being
  separately configurable. This was the one open question that would have forced
  the Phase 1 URL parser to be rewritten, which is why it was settled first.

## Decisions taken without explicit sign-off

Inferred from the stated goals rather than requested, recorded so they can be
overruled deliberately. **All confirmed by the user on 2026-08-14** except where
noted.

- **Logging**: `Microsoft.Extensions.Logging` removed in favor of a static
  stderr writer gated on `--verbose`.
- **No DI container**: `Microsoft.Extensions.DependencyInjection` removed;
  objects constructed by hand in `Program.cs`.
- **Environment variables renamed** `RMQCLI_RabbitMq__Host` etc. → a single
  `$RMQ_URL`. **Breaking change** for anyone with the old variables in a shell
  profile; needs a release note.
- **File rotation dropped** (`MessagesPerFile`, `MessageDelimiter`). Splitting
  large drains is `split`'s job.
- **The entire ack-mode parameter dropped.** No `--ack-mode`, no `AckModes` enum.
  `Reject` gone — bulk discard is what `purge` is for.
- **Push is the default for `consume`**, `--pull` opts into polling. The old tool
  welded polling to `peek`; separating the axes forced a default to be chosen.
- **Exit code table invented.** Code 3 (`--count` unmet) in particular is a
  judgment call.

## Corrections made during refinement

Recorded because each was a real error caught before any code was written:

- **`purge` does not need the Management API.** `QueuePurgeAsync` exists in
  RabbitMQ.Client 7.2.0. An earlier draft made purge HTTP-only, which would have
  made the HTTP client mandatory rather than a fallback.
- **The HTTP transport is a blocked-port fallback**, not a co-equal transport —
  its purpose is networks where only 80/443 reach the broker. An earlier draft
  narrowed it to purge alone, silently dropping a stated requirement.
- **`--requeue` cannot be implemented as per-message nack.** A requeued message
  returns to the head of the queue and is immediately re-read, so the loop
  re-emits forever and never drains. The old `AckHandler` got this right by
  returning early and never acking; that mechanism is now specified in
  `CLAUDE.md`.
- **A single bounded `Channel<T>` is required** by the push path, because
  `IAsyncBasicConsumer` delivers on a callback that must not block. An earlier
  draft banned `Channel<T>` outright, which would have made the push path
  unimplementable.
