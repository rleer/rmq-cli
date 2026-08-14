# rmq — RabbitMQ CLI

Operating instructions for this repository. These are durable constraints, not
suggestions. When a change would violate one, stop and raise it rather than
working around it.

## What this tool is

A developer-facing CLI for publishing and consuming RabbitMQ messages. It should
feel like a normal Unix terminal tool: reads STDIN, writes STDOUT, composes with
pipes, exits with meaningful codes, and stays out of the way.

It is not a monitoring tool, not an admin console, and not a library.

## Non-negotiables

1. **AOT-clean.** `dotnet publish -c Release -r <rid>` must produce zero
   `IL2xxx`/`IL3xxx` trim or AOT warnings. No reflection-based binding, no
   runtime code generation, no `MakeGenericType`. JSON goes through
   `System.Text.Json` source generation only.
2. **Startup budget: < 20 ms** for `--help` and `--version` on a warm cache.
   This is a **regression guard, not a goal to reach** — the pre-rewrite code
   was measured at **6.7 ms** (osx-arm64, 20-run average, AOT) with zero trim
   warnings, so startup performance was already satisfied. The rewrite is
   motivated by maintainability, not speed. Keep it that way: if a change
   pushes past 20 ms, the change is wrong, not the budget.
3. **Dependency allowlist.** Exactly these, and adding to the list is a
   deliberate decision requiring justification:
   - `RabbitMQ.Client` — AMQP protocol
   - `System.CommandLine` — argument parsing
   - `System.Text.Json` (in-box) — all JSON, source-generated
   - `System.Net.Http` (in-box) — Management HTTP API

   Explicitly removed and not to be reintroduced: `Spectre.Console`, `Tomlyn`,
   `Microsoft.Extensions.Configuration*`, `Microsoft.Extensions.DependencyInjection`,
   `Microsoft.Extensions.Logging*`.
4. **No DI container.** Construct objects manually in `Program.cs`. A container
   costs startup time and hides the object graph — both things this tool is
   specifically trying to avoid.

## Code style

**Duplication is preferred over abstraction.** This is the single most important
rule and the main thing the previous version got wrong. If the push and pull
paths need similar retrieval code, write it twice. Two readable 40-line methods
beat one strategy interface with two implementations and a base class — that is
precisely what `IMessageRetrievalStrategy` + `BaseMessageRetrievalService` were,
and they are being deleted.

Refuse to introduce, and delete on sight:

- Strategy/factory/pipeline patterns used to deduplicate two call sites
- `IFoo` interfaces with exactly one production implementation
- Base classes whose only purpose is sharing a helper method
- Formatter or output "frameworks" — write the output directly
- Wrapper types that only forward to the thing they wrap

An abstraction earns its place when there are three or more real
implementations, or when it isolates a genuine external boundary. Not before.

Prefer static methods over injected services. Prefer passing values as
parameters over holding them as fields.

## Command surface

```
rmq publish -q <queue> [--body <s> | --message <json> | --message-file <p> | STDIN]
rmq publish -e <exchange> --routing-key <k> ...
rmq consume -q <queue> [--count N] [--requeue] [--follow] [--to-file <p>]
                       [--prefetch-count N] [--pull]
rmq purge <queue>

Global: --transport amqp|http (default: amqp)
```

Three commands. Notably:

- **`peek` does not exist as a command.** It is `consume --requeue`.
- **`config` does not exist as a command subtree.** No `show`/`init`/`path`/
  `edit`/`reset`. There is no config file to manage.

### Consume: push vs pull

`consume` uses the **push API by default** (`BasicConsumeAsync`); `--pull`
selects the polling `BasicGetAsync` API. This axis is independent of
`--requeue` — all four combinations are valid, where previously "polling" was
welded to `peek`.

### Consume exit conditions

Default is **drain and exit**: stop when `--count` is reached, or when the queue
is empty. The pull path exits on the first empty get; the push path exits after
a short idle window with no deliveries.

`--follow` keeps the subscriber open indefinitely for tailing. Ctrl-C always
exits cleanly, acking everything already written.

This matters because `rmq consume -q orders | jq` must terminate on its own.

### Exit codes

Scripts branch on these, so they are part of the contract:

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Connection / authentication failure |
| 2 | Usage error (bad flags, mutually exclusive options) |
| 3 | Completed, but `--count` was not satisfied (queue drained early) |
| 130 | Interrupted (Ctrl-C) — messages written so far are acked |

Code 3 is the one that matters for pipelines: it distinguishes "got fewer
messages than asked" from a real failure.

## Transports

`--transport amqp` (default) uses `RabbitMQ.Client` over the AMQP port. This is
the real implementation and where effort belongs.

`--transport http` uses the Management HTTP API. **Its sole purpose is to work
in networks where the AMQP port is blocked and only 80/443 reach the broker.**
It is a degraded fallback, not a co-equal path — do not invest in bringing it to
parity, and do not let its limitations leak into the AMQP design.

Every operation has an AMQP implementation — including `purge`, via
`IChannel.QueuePurgeAsync(queue, ct)`. **No operation requires the HTTP API**, so
`--transport http` is uniformly the blocked-port escape hatch and nothing else.

| Operation | AMQP (default) | HTTP fallback |
|---|---|---|
| publish | `BasicPublishAsync` | `POST /api/exchanges/{vhost}/{name}/publish` |
| consume | `BasicConsumeAsync` / `BasicGetAsync` | `POST /api/queues/{vhost}/{name}/get` |
| purge   | `QueuePurgeAsync` | `DELETE /api/queues/{vhost}/{name}/contents` |

Known and accepted limitations of the HTTP path — document these in `--help`,
do not try to engineer around them:

- **No push/subscriber support.** Polling only; `--follow` degrades to a poll loop.
- **No delivery tags.** Acknowledgment is an `ackmode` request parameter decided
  *before* the message is written, so the ack-after-write guarantee below does
  **not** hold. A crash mid-write can lose messages on this path. Say so in help text.
- RabbitMQ's own docs mark `/get` as unsuitable for production or high-volume use.

## Configuration

One connection knob, resolved in this precedence order (highest wins):

1. Individual flags — `--host`, `--port`, `--vhost`, `--user`, `--password`
2. `--url amqp://user:pass@host:port/vhost`
3. `$RMQ_URL` environment variable
4. Defaults — `amqp://guest:guest@localhost:5672/`

Individual flags override the corresponding component of a URL, so
`--url amqp://prod/ --vhost /test` is meaningful and well-defined.

`--url` also accepts an `http://` or `https://` URL, which implies
`--transport http` and points at the Management API directly — the case where
only 80/443 are open. With an `amqp://` URL, the HTTP transport derives its base
URL from the same host using `--management-port` (default 15672).

No config file. No TOML. No `Microsoft.Extensions.Configuration`. Users who need
a persistent broker set `$RMQ_URL` in their shell profile.

## Output contract

Output shape depends on whether STDOUT is a terminal, and this must be honored:

- **Piped or redirected** → NDJSON, one complete JSON object per line. Machine
  parseable, `jq`-friendly, no ANSI escapes, no decoration, no progress output.
- **TTY** → a hand-rolled human-readable form with ANSI color.

`--json` forces NDJSON even on a TTY. `--raw` writes **only the message body
bytes**, no envelope and no properties, for piping payloads into other tools.
Both override TTY detection.

`--to-file <path>` writes **one file**, NDJSON, newline-delimited. No rotation,
no configurable delimiter, no `MessagesPerFile`. Splitting large drains is
`split`'s job, and the pipe already composes: `rmq consume -q q | split -l 10000 -`.

**Diagnostics go to STDERR, always.** Log lines, progress, warnings, and errors
must never contaminate STDOUT — piping is a first-class use case. Logging is a
small static stderr writer gated on `--verbose`; there is no `ILogger`
abstraction and no logging package.

## Delivery semantics

Consume is a **single sequential loop** — receive, write, flush, ack:

```csharp
while (count < limit) {
    var msg = await Receive(ct);
    await writer.WriteAsync(msg);
    await writer.FlushAsync();
    await channel.BasicAckAsync(msg.DeliveryTag, multiple: false);
}
```

A message is acked only after it is durably written, so a crash cannot lose
data. This is the whole delivery guarantee, and it needs no further machinery.
It applies to the AMQP transport; the HTTP fallback cannot provide it (see
Transports above).

**There is no ack-mode parameter.** Acking is what consume does; `--requeue` is
the single opt-out. Do not reintroduce an `AckModes` enum or an `--ack-mode`
flag — two behaviors do not need a mode parameter.

Do not reintroduce: background writer tasks, a separate ack-dispatcher task,
batched `multiple: true` acknowledgment, or a dedicated message-counter type.
The previous version spent roughly 400 lines on that machinery and bought
nothing the loop above does not already provide.

Both APIs feed the same loop:

- **Pull** (`BasicGetAsync`) — the `Receive` call is literal.
- **Push** (`BasicConsumeAsync` / `IAsyncBasicConsumer`) — delivery arrives on a
  callback that must not block. A **single bounded `Channel<T>`, sized to
  `--prefetch-count`, is the sanctioned adapter** between that callback and the
  loop. This one channel is explicitly allowed and is not what the paragraph
  above bans — what is banned is the multi-task pipeline built on top of it.

`--prefetch-count` survives as the bound on that buffer for the push path.

### `--requeue` (the former `peek`)

`--requeue` **does not nack per message.** It skips the ack call entirely: read,
write, never acknowledge, then let the channel close — RabbitMQ requeues every
unacked delivery automatically. The sequential loop above is unchanged; one line
is simply not executed.

Do not implement this as `BasicNackAsync(requeue: true)` inside the loop. A
requeued message returns to the head of the queue and is immediately re-read, so
the loop re-emits the same message forever and never drains. The previous
version got this right (`AckHandler` returned early in requeue mode rather than
nacking); preserve that.

Consequences to honor:

- Messages are held unacked, so the run is bounded by `--prefetch-count`.
  `--requeue` therefore requires `--count` (≤ prefetch), defaulting to prefetch
  if omitted. It cannot drain a queue.
- **`--requeue` and `--follow` are mutually exclusive** — holding deliveries
  unacked indefinitely is not a thing. Reject the combination at parse time.
- Requeued messages come back flagged `redelivered`. This is unavoidable over
  AMQP; state it in `--help`.

## Message model

Messages support the full set of RabbitMQ properties — content type, content
encoding, delivery mode, priority, correlation ID, reply-to, expiration, message
ID, timestamp, type, user ID, app ID — plus arbitrary headers.

Both directions round-trip losslessly: a message consumed as JSON and republished
must arrive with identical properties and body bytes. This is the property worth
testing hardest.

Concretely, **`publish` reads on STDIN exactly the NDJSON that `consume` writes
on STDOUT**, so this is a supported, tested composition:

```bash
rmq consume -q source --url amqp://a/ | rmq publish -q dest --url amqp://b/
```

One JSON object per line, each with `body`, `properties`, and `routingKey`.
`--message` takes the same single-object shape. Keep the two schemas identical —
if a property can be emitted, it must be accepted.

## Testing

The previous version had 18.4k lines of test against 6.3k lines of source,
across four projects, largely chasing coverage. Do not rebuild that.

Target shape — two projects:

- **E2E** against real RabbitMQ via Testcontainers. This is where confidence
  actually comes from. Must cover:
  - publish → consume round trip, once each for push and `--pull`
  - full property round-trip fidelity (the `consume | publish` pipe above)
  - `--requeue` leaves the queue depth unchanged **and terminates** — the
    regression test for the nack-loop trap described in Delivery semantics
  - exit code 3 when `--count` exceeds queue depth
  - `purge`
- **Unit**, thin, for the pure functions only — message JSON parsing, header
  parsing (`k:v`), property merging, AMQP URL parsing.

Delete the `Subcutaneous` and `Integration` projects; their value is covered by
the two above. Fold the useful parts of `RmqCli.Tests.Shared` (the
`RabbitMqFixture` / `RabbitMqOperations` container plumbing) directly into the
E2E project and delete it as a separate project — with only one consumer left,
a shared project is exactly the abstraction this repo rejects.

Cover the HTTP transport with a single E2E round trip, not a parallel suite.

Do not write tests for: DTO constructors, property getters, enum round-tripping,
factory methods, or anything whose failure would be caught immediately by the
E2E round trip. **Coverage percentage is not a goal** — the README carries a
codecov badge that will drop when the test suite shrinks, and that is expected.
Decide whether to remove the badge/gate rather than writing tests to satisfy it.

## Decisions taken without explicit sign-off

These follow from the stated goals but were inferred, not requested. They are
recorded here so they can be overruled deliberately rather than discovered later:

- **Logging**: `Microsoft.Extensions.Logging` removed in favor of a static stderr
  writer gated on `--verbose`. Follows from "minimal dependencies," but it is an
  architectural call.
- **No DI container**: `Microsoft.Extensions.DependencyInjection` removed; objects
  are constructed by hand in `Program.cs`.
- **Environment variable renamed** `RMQCLI_RabbitMq__Host` etc. → a single
  `$RMQ_URL`. **This is a breaking change** for anyone with the old variables in
  a shell profile. Worth a note in the README and release notes.
- **File rotation dropped** (`MessagesPerFile`, `MessageDelimiter`) — see Output.
- **The entire ack-mode parameter is dropped.** No `--ack-mode`, no `AckModes`
  enum. Acking is the default behavior; `--requeue` is the only opt-out.
  `Reject` is gone — discarding messages in bulk is what `purge` is for.
- **Push is the default for `consume`**, with `--pull` opting into polling. The
  old tool welded polling to `peek`; separating the axes means a default had to
  be chosen, and push is the better one for draining.
- **Exit code table** — invented, not specified. Code 3 (`--count` unmet) in
  particular is a judgment call.

## Known follow-ups

- The README carries a **codecov badge**; deliberately shrinking the test suite
  will drop coverage and fail any configured threshold. Remove the badge/gate or
  lower it consciously — do not write tests to satisfy it.
- The README documents the TOML config, `config` subcommands, `peek`, and
  `RMQCLI_*` variables. All become wrong on rewrite and need updating together.

## Build and verify

```bash
dotnet build
dotnet test
dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o release
```

Before considering a change done, verify the two non-negotiables that are easy
to break silently:

```bash
# 1. No AOT/trim warnings
dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 2>&1 | grep -E "IL[0-9]{4}"

# 2. Startup still under budget
time ./release/rmq --help
```

Baseline binary size for reference: 16.4 MB (osx-arm64, AOT, trimmed).
