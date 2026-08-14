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

### Phase 2 — Clear the ground (delete only, no new features) ✅ done

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

**Renamed to lowercase `rmq`** (user's call, 2026-08-14): `src/rmq/rmq.csproj`,
`rmq.sln`, `test/rmq.Unit.Tests`, `test/rmq.E2E.Tests`. `RmqCli` no longer appears
anywhere. Note `dotnet new sln` now emits `.slnx` by default under the .NET 10
SDK; the classic format was kept via `--format sln`.

**E2E harness rebuilt rather than ported.** The CliWrap runner and the
Testcontainers fixture were worth keeping; the rest was not. `RabbitMqTestHelpers`
was a pure forwarding wrapper over `RabbitMqOperations` — the exact pattern
`CLAUDE.md` says to delete on sight — and `RabbitMqOperations` returned the
product's own `QueueInfo`, which no longer exists. Three files now: `Cli.cs`
(runs the *published binary*, with real Ctrl-C for the exit-130 path),
`Broker.cs` (arrange/assert over AMQP directly, so tests never verify the CLI
with the CLI), `RabbitMqFixture.cs`. Also dropped `coverlet.collector` —
coverage percentage is explicitly not a goal — and `NSubstitute`, since the unit
slice is pure functions with nothing to mock.

**Checkpoint met**, all measured 2026-08-14:

| | Before | After |
|---|---|---|
| Source | 6,249 LOC | **842 LOC** |
| Tests | 18,216 LOC | **601 LOC** |
| `PackageReference` | 10 | **2** |
| Test projects | 5 | **2** |
| Binary | 16.4 MB | **4.06 MB** |
| Startup, `--help` | 6.7 ms | **3.8 ms** |
| `IL2xxx`/`IL3xxx` | 2 | **0** |
| Build warnings | 1 | **0** |

43 unit tests and 3 E2E smoke tests green. The remaining 842 LOC is Phase 1's
seven files plus a 25-line `Program.cs` stub.

### Phase 3 — `publish` + `consume`, and the round trip that proves them ✅

Done 2026-08-14, with Phase 4 folded in. Five new files — `Amqp.cs`,
`GlobalOptions.cs`, `PublishCommand.cs`, `ConsumeCommand.cs`, `PurgeCommand.cs` —
plus a real `Program.cs`. Twelve files under `src/rmq/`, still flat.

Everything the phase listed shipped: the `System.CommandLine` root with recursive
global options, all five exit codes, the salvaged broker-error mapping (530
vhost-not-found vs access-denied, with the exception-chain walk that finds an auth
failure wrapped inside `BrokerUnreachableException`), both consume paths feeding
one sequential receive → write → flush → ack loop, and `--requeue` as
hold-unacked-then-close.

Four things the plan did not anticipate:

- **Publisher confirmations had to be abandoned.** Confirmation *tracking* is the
  only way to await a confirm per publish, and RabbitMQ.Client implements it by
  stamping `x-dotnet-pub-seq-no` onto every published message. That silently
  breaks the properties round-trip, which `CLAUDE.md` names as the property worth
  testing hardest. The replacement is one `QueueDeclarePassiveAsync` before the
  loop when `-q` is given — it catches the failure that actually happens, a
  mistyped queue name, for one round trip instead of one per message — plus
  `mandatory: true` for `-q` only, with a `BasicReturnAsync` handler that fails
  the run if anything bounced. `--exchange` stays non-mandatory: unroutable is
  ordinary there, and a fanout with no bindings is not a mistake.
- **Header normalization is a tree, not a lookup.** `byte[]` → string was the
  known trap; `x-death` on any dead-lettered message is a *list of nested field
  tables*, which would have thrown `NotSupportedException` at serialize time.
  `Amqp.ToHeaderValue` now normalizes recursively into the closed set the schema
  names, `List<object>` is registered in `MessageJsonContext`, and the read side
  rebuilds nested tables so a consumed `x-death` republishes as a table rather
  than a JSON string. Verified end to end against a real dead-letter exchange.
- **RabbitMQ 4 rejects transient non-exclusive queues.** `Broker.DeclareQueue`
  declared `durable: false`, which the `rabbitmq:4-management` fixture image
  refuses outright (`transient_nonexcl_queues` deprecated). Every broker-backed
  test would have failed at arrange.
- **Ctrl-C acks on `CancellationToken.None`.** The write, flush, and ack of the
  delivery already in hand must not take the cancelled token, or a message
  already on stdout goes unacked and reappears as a silent duplicate.

**Checkpoint met**, all measured 2026-08-14:

| | Phase 2 | Phase 3 |
|---|---|---|
| Source | 842 LOC | **1,795 LOC** |
| Tests | 601 LOC | **971 LOC** |
| `PackageReference` | 2 | **2** |
| Binary | 4.06 MB | **9.13 MB** |
| Startup, `--help` | 3.8 ms | **4.3 ms** (50-run avg) |
| `IL2xxx`/`IL3xxx` | 0 | **0** |
| Build warnings | 0 | **0** |

52 unit and 15 E2E tests green. The binary more than doubled because this is the
first build where `RabbitMQ.Client` is reachable from `Main` — before Phase 3 the
trimmer dropped it entirely, which is also why the Phase 1 AOT check needed a
probe branch to prove anything. Startup cost 0.5 ms for three commands and about
thirty options, against a 20 ms budget.

E2E coverage landed ahead of plan, so most of Phase 6 is already done: round trip
(push and `--pull`), the full-property `consume | publish` pipe with source ≠
destination, binary body fidelity, `--requeue` depth-preserving and terminating,
exit code 3 and its satisfied counterpart, `--to-file`, publish to a nonexistent
queue, `--follow` interrupted by a real Ctrl-C exiting 130 with everything acked,
and `purge`.

### Phase 4 — `purge` ✅

Folded into Phase 3. `IChannel.QueuePurgeAsync(queue, ct)`, one E2E test.

### Phase 5 — HTTP transport ✅ done

`--transport http` for publish, get, and purge. One new file, `Http.cs` (~330
LOC): client construction, error explanation, the three operations, and the
snake_case DTOs with their own source-generation context. The commands each
branch to it at the top of `Run` and are otherwise untouched.

Everything below was planned correctly. What the plan did **not** anticipate:

- **An empty property set arrives as `[]`, not `{}`.** An empty Erlang proplist
  encodes as a JSON array, so `"properties":[]` comes back on every message
  published without properties — which is most of them. Deserializing straight
  into a record throws there, with no compile-time signal. The `/get` response
  DTO therefore holds `properties` as a `JsonElement` and converts only when
  `ValueKind == Object`. Found by probing a real broker before writing the DTO,
  not by reading the API reference, which does not mention it.
- **`Uri` preserves `%2F`.** The default vhost is a path segment, and a
  canonicalization that turned `%2F` back into `/` would have addressed a vhost
  named `""` on every call. .NET 8 keeps it; pinned in a unit test so a future
  runtime change fails loudly rather than silently retargeting.
- **The management API's message counts are sampled and lag badly.** A queue
  holding three requeued messages reported `messages: 0` and stayed there. This
  independently confirms the "do not derive a count from `messages_ready`"
  prohibition below — that number is not merely racy, it is often simply wrong.
  The HTTP E2E tests assert depth over AMQP for the same reason.
- **The AMQP unbounded-growth warning had to be suppressed here.** `--requeue`
  over HTTP holds nothing unacked — the broker requeues each batch before it
  answers — so the warning was both false and directly contradicted by the
  could-not-drain warning printed immediately after it.
- **`routed` maps exactly onto `mandatory`.** The publish response reports
  routability synchronously, which is the same information `basic.return` gives
  asynchronously over AMQP, so it is counted the same way: an error for
  `--queue`, ordinary for `--exchange`. Publish behaviour is identical across
  transports, exit code included.

Two deliberate degradations, both documented in `--help` rather than engineered
around:

- **`purge` reports no count.** `DELETE /contents` answers 204 with an empty
  body; AMQP's `QueuePurgeAsync` returns the number purged and HTTP has nothing
  to return.
- **Bodies are always published as base64.** The API would take a plain string
  for a UTF-8 body, but then publish would need its own copy of the
  is-this-valid-UTF-8 rule. Base64 is correct for every body and costs a third
  more bytes on a path that exists for troubleshooting.

Measured after this phase:

| | Phase 3 | Phase 5 |
|---|---|---|
| Source | 1,795 LOC | **2,181 LOC** |
| Tests | 990 LOC | **1,082 LOC** |
| `PackageReference` | 2 | **2** |
| Binary | 9,128,016 B | **11,534,512 B** |
| Startup `--help` | 4.3 ms | **4.5 ms** (50-run avg, warm) |
| `IL2xxx`/`IL3xxx` | 0 | **0** |
| Build warnings | 0 | **0** |
| Tests | 52 unit / 15 E2E | **53 unit / 17 E2E** |

The binary grew 2.4 MB because this is the first build where `System.Net.Http`
and the TLS stack are reachable from `Main` — the same effect Phase 3 had on
`RabbitMQ.Client`. Startup is unchanged; the 13 ms an early measurement reported
was a cold page cache immediately after `dotnet publish`, not a regression.

The original plan for this phase follows.

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

### Phase 6 — Test suite completion ✅ done

Absorbed entirely into Phases 3 and 5. Property round-trip fidelity via the
`consume | publish` pipe, `--pull`, and `--follow` under Ctrl-C landed in Phase
3; the HTTP round trip and the `--requeue --transport http` bound landed in
Phase 5. The two-project shape is confirmed: `rmq.Unit.Tests` and
`rmq.E2E.Tests`, nothing else.

`--consumer-priority` is covered, but deliberately not on the axis its name
suggests: asserting that a low-priority consumer *yields* would need a second
competing consumer and a race to observe. The E2E case pins the thing that can
actually break instead — `x-priority` is a consumer argument the broker validates
at `basic.consume` time, so a wrong key or value type fails at runtime rather than
at compile time. If that trade is wrong, the fix is a two-consumer test, not a
different assertion on this one.

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
