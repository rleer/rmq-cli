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
| Trim/AOT warnings | **2** | `IL2104` + `IL3053`, both from Tomlyn |
| Source | **6,249 LOC** | `src/RmqCli`, 79 files (excl. `obj/`) |
| Tests | **18,216 LOC** | 4 test projects + 1 shared library, ~2.9× source |
| `PackageReference` | **10** | drops to **2** — the other two allowlisted packages are in-box |

Two findings that correct assumptions behind the rewrite:

- **Startup was never the problem.** 6.7 ms already sits far under any
  reasonable budget. The 20 ms figure in `CLAUDE.md` is a *regression guard*, not
  a target to work toward. Nobody should optimize for speed during this rewrite.
- **Tomlyn works at runtime but does violate the AOT non-negotiable.** A TOML
  value did bind correctly through the AOT binary (`--host` resolved to
  `toml-host-marker:5673` from a config file), so the tool is not broken today.
  But a clean `dotnet publish` emits `IL2104` and `IL3053` against `Tomlyn.dll`,
  and `CLAUDE.md` non-negotiable 1 is "zero `IL2xxx`/`IL3xxx`" — so Tomlyn *is*
  disqualified, on exactly the grounds originally stated. **This corrects an
  earlier entry here** claiming 0 warnings and "not an AOT blocker"; that came
  from a grep that missed the assembly-level warnings. Every remaining warning
  disappears with Tomlyn in Phase 2, and none originate in first-party code.

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

Labels below are as revised in Phase 1 — three of them turned out to be more than
a namespace move once headers nested inside `properties` and the body became bytes.

| File | LOC | What actually happened |
|---|---|---|
| `Commands/Publish/JsonMessageParser.cs` | 107 | folded into `MessageJson`; `ParseNdjson` replaced by line-at-a-time `ReadLinesAsync`, since `consume \| publish` must stream rather than slurp |
| `Commands/Publish/PropertyMerger.cs` | 74 | rewritten against nested headers; takes `MessageProperties`, not `PublishOptions` |
| `Commands/Publish/HeaderParser.cs` | 62 | near-verbatim |
| `Shared/Json/JsonSerializationContext.cs` | 55 | became `MessageJsonContext`, retargeted at the new DTOs |
| `Shared/Json/BodyJsonConverter.cs` | 45 | became `BodyConverter`; `Read` rewritten — the old one could not read what it wrote |
| `Core/Models/MessageProperties.cs` | 34 | dropped `ClusterId`, gained `Headers` |

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

Written first, before either side was implemented: **[`docs/message-schema.md`](message-schema.md)**.
Unlike this file it is durable — it outlives the rewrite and stays the one
definition both directions derive from.

Three findings from writing it:

- **The current tool fails the round trip it was built for.** Verified: `consume`
  emits `"body":{"orderId":42}` (`BodyJsonConverter.Write` emits raw JSON) while
  `publish` parses `body` with `reader.GetString()`, which throws on an object
  token — and `Message.Body`, the publish input, has no converter attached at all.
  `rmq consume | rmq publish` therefore throws on every JSON-bodied message,
  i.e. the common case. One converter now serves both directions.
- **A `string` body cannot hold arbitrary bytes.** The schema needed a
  `bodyEncoding` discriminator before Phase 5 could handle `/get`'s
  `payload_encoding: base64` at all. It is a sibling field, so the byte↔wire
  conversion lives in `Message` rather than in a property converter, which only
  ever sees its own value.
- **JSON bodies are emitted inline, at the cost of byte-exact whitespace**
  (user's call, 2026-08-14). `jq '.body.orderId'` with no `fromjson` step was
  judged worth more than preserving insignificant whitespace. Text and binary
  bodies remain byte-exact, and `--raw` is the escape hatch.

`ClusterId` is **out** (resolved below), so the schema's property list is exactly
the one in `CLAUDE.md` — nothing more.

## Phases

**Revised 2026-08-14, after the user confirmed the old implementation is not in
use and breaking changes are free.** That removes the constraint the original
sequencing was built around — "the old CLI must keep working" — and with it most
of the reason the phases were ordered the way they were.

Three things change:

- **Delete before building, not while building.** The old tree comes out in one
  commit, before a single new command is written. Everything that made the sweep
  awkward — Tomlyn's IL warnings, the `RmqCli` name shadowing, the duplicated
  salvage copies — is a consequence of the old code being present, and all of it
  disappears the moment it is gone.
- **The coverage gap stops being a hazard.** It was a hazard because a working
  tool was briefly unguarded. Nothing ships from `rebuild` until it is finished,
  so the window is a scheduling preference, not a risk. Phases can be as long or
  short as is convenient.
- **`publish` and `consume` land together.** They were split so that the sweep
  could end with something demonstrable. The real checkpoint is the round-trip
  E2E test, which needs both, so splitting them only postpones the first
  meaningful signal.

Each phase still ends with `dotnet build` green and — from Phase 2 onward, where
the last warning-producing package is gone — **zero `IL2xxx`/`IL3xxx`** and
`--help` under 20 ms.

### Phase 1 — Foundation (nothing deleted, tree still builds and runs) ✅ done

Added alongside the existing code, wired to nothing yet. Seven files, ~640 LOC,
flat under `src/RmqCli/` in namespace **`Rmq`**:

| File | Contents |
|---|---|
| `Message.cs` | `Message`, `MessageProperties`, body byte↔wire encoding |
| `MessageJson.cs` | `MessageJsonContext`, `BodyConverter`, parse / serialize / `ReadLinesAsync` |
| `Connection.cs` | `ConnectionSettings`, per-component precedence, URL + scheme parsing |
| `Log.cs` | static stderr writer gated on `--verbose` |
| `MessageWriter.cs` | NDJSON / human / raw, TTY detection, `--to-file` |
| `HeaderParser.cs` | `k:v` with type detection |
| `PropertyMerger.cs` | CLI-over-JSON property merge |

Two corrections to what this phase was planned to be:

- **The six salvaged files were copied, not moved**, because moving them broke
  the old build. Phase 2 deletes the originals, resolving the duplication.
  `JsonMessageParser` was folded into `MessageJson`; `PropertyMerger` now takes
  `MessageProperties` instead of `PublishOptions`, which is what makes it
  testable without a command in scope.
- **The namespace is `Rmq`, not `RmqCli`.** New types under `namespace RmqCli`
  shadow the old ones throughout the old tree: with file-scoped namespaces the
  `using` directives sit in compilation-unit scope, which is searched *after*
  enclosing namespace members, so `RmqCli.Message` beat the imported
  `RmqCli.Core.Models.Message` in 35 places. `Rmq` is confirmed as the permanent
  namespace and is not renamed back.

**Checkpoint met:** `dotnet build` green, old CLI untouched; 42 new unit tests
pass; AOT publish adds no warnings beyond the two pre-existing Tomlyn ones;
`--help` measured at 5.4 ms (20-run average). The AOT check was re-run with a
temporary probe making the `Rmq` namespace reachable — the first run had proved
nothing, since `TrimMode full` had dropped the whole namespace as dead code and
produced a binary byte-identical to the baseline.

### Phase 2 — Clear the ground (delete only, no new features)

One commit, ~5,900 LOC out, nothing added. Deliberately contains no new
behaviour, so that anything broken afterwards is unambiguously new code.

- delete every group in the deletion table above, plus the six salvage
  originals now superseded by their `Rmq` copies
- `Program.cs` becomes a stub: `System.CommandLine` root with `--version` and
  `--help` and no subcommands
- drop Spectre.Console, Tomlyn, and all six `Microsoft.Extensions.*` packages
  (`Configuration`, `.Binder`, `.EnvironmentVariables`, `DependencyInjection`,
  `Logging`, `.Console`) — 10 `PackageReference` entries become 2
  (`RabbitMQ.Client`, `System.CommandLine`); the other two allowlisted
  dependencies are in-box and carry no `PackageReference`
- delete `RmqCli.Integration.Tests` and `RmqCli.Subcutaneous.Tests` whole; fold
  `RmqCli.Tests.Shared`'s `RabbitMqFixture` / `RabbitMqOperations` container
  plumbing into E2E and delete the project
- delete the old E2E tests (`peek`, `config`, `publish`, `consume`, `purge`,
  cancellation, help) — every one targets a command surface that no longer
  exists; keep `CliTestHelpers` / `RabbitMqCollection` as the harness
- gut `RmqCli.Unit.Tests` down to `ConnectionTests` and `MessageJsonTests`; the
  other ~20 files test deleted types
- drop `EnableConfigurationBindingGenerator` from the `.csproj`

**Rename, while everything is being touched anyway** — the assembly is already
called `rmq` and the namespace is now `Rmq`, so `RmqCli` survives only in paths:
`src/RmqCli` → `src/Rmq`, `RmqCli.sln` → `Rmq.sln`, `RmqCli.Unit.Tests` →
`Rmq.Unit.Tests`, `RmqCli.E2E.Tests` → `Rmq.E2E.Tests`. Free now, churn later.

**Checkpoint:** build green, **zero `IL2xxx`/`IL3xxx`** (Tomlyn was the only
source), `--help` under 20 ms, `ConnectionTests` + `MessageJsonTests` pass.

### Phase 3 — `publish` + `consume`, and the round trip that proves them

The bulk of the new code. These ship together because the first real signal is
the round-trip test, which needs both ends.

- `Program.cs`: `System.CommandLine` root, global options, objects constructed
  by hand, exit codes 0 / 1 / 2 / 3 / 130
- connection + channel setup, with the broker-error mapping salvaged from
  `RabbitChannelFactory.HandleConnectionException` as the basis for exit code 1
- `publish` against AMQP: `--body` / `--message` / `--message-file` / STDIN,
  `--header`, the property flags, `-q` vs `-e --routing-key`
- `consume`: sequential receive → write → flush → ack loop; push (default) and
  `--pull`; `--count`, `--follow`, `--to-file`, `--consumer-priority`
- `--requeue` via hold-unacked-then-close, **not** per-message nack
- AMQP header `byte[]` → string decoding at the boundary (see the schema doc)

**Write the publish → consume round-trip E2E test as soon as consume returns a
single message**, not at the end. Then the `--requeue`
terminates-and-preserves-depth test, which is the regression guard for the nack
loop, and the exit-code-3 test.

**Checkpoint:** round-trip, `--requeue`, and exit-code-3 E2E tests green; AOT and
startup checks.

### Phase 4 — `purge`

`IChannel.QueuePurgeAsync(queue, ct)` — confirmed present in RabbitMQ.Client
7.2.0. Small; one E2E test. Can be folded into Phase 3 if it is in the way.

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

Fill in the remaining E2E cases from `CLAUDE.md` — property round-trip fidelity
via the `consume | publish` pipe, `--pull`, `--follow` under Ctrl-C — and the
unit slice. Confirm the final two-project shape.

### Phase 7 — Docs and packaging

README rewrite (it still documents TOML, `config`, `peek`, and `RMQCLI_*`),
codecov badge/gate decision, `.csproj` metadata, release notes calling out the
`RMQCLI_*` → `$RMQ_URL` breaking change.

## Open questions

All resolved as of 2026-08-14. Two informational items remain; neither affects
correctness or blocks any phase:

- **Target framework stays `net8.0`.** The .NET 8 runtime was installed on this
  machine on 2026-08-14 specifically so the suite runs without
  `DOTNET_ROLL_FORWARD`, which settles the retarget question by implication. A
  move to `net10.0` remains available and is not needed by anything here.

- **Does `basic.qos` prefetch apply to `basic.get`?** Not documented on
  RabbitMQ's consumer-prefetch page, and the AMQP 0-9-1 reference has moved to
  GitHub. **This stopped mattering once `--prefetch-count` became internal:**
  requeue mode now sets `basic.qos(0)` unconditionally on both paths, so if
  prefetch does not apply to `basic.get` the call is a harmless no-op, and if it
  does, it is exactly what is needed. Behavior is identical either way. Worth
  confirming in Phase 3 only to decide whether the pull path can skip the call.

### Resolved

- **The old implementation is not in use; breaking changes are free**
  (2026-08-14). This is what allows Phase 2 to delete before building rather
  than alongside it, and removes the coverage gap as a hazard. It also settles
  the `RMQCLI_*` → `$RMQ_URL` environment rename and the loss of the TOML config
  file: both are breaking, and neither now needs a migration path — only a
  release note.
- **`--raw` writes no separator between messages** (2026-08-14) — not even a
  newline. A delimiter safe for arbitrary binary does not exist, and adding one
  defeats the single case the flag serves. Recorded in `CLAUDE.md`.
- **Management port default follows the scheme** (2026-08-14) — 15672 for
  `amqp://`, **15671** for `amqps://`, matching RabbitMQ's own plain and TLS
  management listeners. `CLAUDE.md` originally said a flat 15672, written before
  the TLS case was in view.
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
