# rmq — RabbitMQ CLI

Operating instructions for this repository. These are durable constraints, not
suggestions. When a change would violate one, stop and raise it rather than
working around it.

> A rewrite is in progress on branch `rebuild`. Sequencing, deletion inventory,
> measured baselines, and open questions live in
> [`docs/rewrite-plan.md`](docs/rewrite-plan.md). This file describes the tool as
> it should end up, and applies to every change regardless of the rewrite.

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
   This is a **regression guard, not a goal to reach** — startup is already far
   under it. Do not optimize for speed; just don't regress. If a change pushes
   past 20 ms, the change is wrong, not the budget.
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
rule in this repository. If the push and pull paths need similar retrieval code,
write it twice. Two readable 40-line methods beat one strategy interface with two
implementations and a base class.

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

### Layout

One namespace, `Rmq`, and flat files directly under `src/RmqCli/`. At the target
size — roughly fifteen files — a folder hierarchy is filing for its own sake, and
the old tree's `Commands/…/Strategies/` depth is a symptom of the abstraction
this rewrite exists to remove. Do not add subfolders.

## Command surface

```
rmq publish -q <queue> [--body <s> | --message <json> | --message-file <p> | STDIN]
rmq publish -e <exchange> --routing-key <k> ...
rmq consume -q <queue> [--count N] [--requeue] [--follow] [--to-file <p>] [--pull]
                       [--consumer-priority N]
rmq purge <queue>

Global: --url <u> | --host/--port/--vhost/--user/--password
        --transport amqp|http (default: amqp)  --insecure
        --json  --raw  --verbose
```

Three commands. Notably:

- **`peek` does not exist as a command.** It is `consume --requeue`.
- **`config` does not exist as a command subtree.** No `show`/`init`/`path`/
  `edit`/`reset`. There is no config file to manage.

### Consume: push vs pull

`consume` uses the **push API by default** (`BasicConsumeAsync`); `--pull`
selects the polling `BasicGetAsync` API. This axis is independent of `--requeue`
and `--follow` — all combinations are valid except `--requeue --follow`.

**The important difference is consumer registration, not speed.** `BasicConsumeAsync`
registers rmq as a consumer on the queue: it appears in the broker's consumer
list, joins round-robin distribution alongside any existing consumers, and the
broker then pushes messages at it continuously — immediately pulling up to
prefetch-many into the unacked set. `BasicGetAsync` registers nothing and takes
exactly what is asked for, once.

Both remove messages. The difference is that push *keeps* taking them, which is
the footgun when attaching to a live queue. **`--pull` is the documented answer
for inspecting a queue that has real consumers on it**; `--requeue` is the answer
for giving everything back. Say so in `--help`.

The remaining differences are latency only:

1. Empty-queue exit is immediate (first null get) rather than after the idle window.
2. `--follow` becomes a poll loop with a 1 s interval between empty gets.
3. Exit code 3 comes free from the null get rather than from a timeout.

### Consumer priority

`--consumer-priority <int>` sets the `x-priority` consumer argument on the push
path. Lower-priority consumers receive messages only once every higher-priority
consumer is **blocked** (at its prefetch limit or flow-controlled), so a negative
value is the polite way to attach to a queue that production depends on.

**Default is 0** — RabbitMQ's own default, meaning rmq competes on equal footing
like any other consumer. A negative default was considered and rejected: on a
healthy busy queue every higher-priority consumer keeps up, so a low-priority rmq
would receive nothing and exit reporting the queue empty. Silently reporting
"empty" for a queue with thousands of messages flowing through it is a worse
failure than competing for messages, especially since `--pull` and `--requeue`
already cover the do-no-harm case.

**Ignored, not rejected, where it does not apply** — with `--pull` and with
`--transport http` there is no consumer to prioritize, so the flag is silently
a no-op. Do not add a validation rule for it. This is deliberate: an error here
would reintroduce exactly the conditional flag validation that dropping
`--prefetch-count` removed.

### Consume exit conditions

There is no prefetch flag and no mode parameter; these four cases are the whole
behavior, and `--requeue` does not alter any of them:

| Invocation | Exits when |
|---|---|
| `consume` | queue is empty |
| `consume --count N` | N messages consumed, **or** queue empties first (exit code 3) |
| `consume --follow` | Ctrl-C only |
| `consume --requeue` | queue is empty — same as bare `consume` |

Omitting `--count` means "all messages." On the push path, "queue is empty" means
an **idle window of 1 second** with no deliveries; that window is also the only
thing that can signal exit code 3, since with `--count 10` against a 3-message
queue nothing else distinguishes "drained early" from "still waiting." Accept the
consequence: every empty-queue `consume` on the push path pays ~1 s before exiting.

Ctrl-C always exits cleanly, acking everything already written.

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
- **`--requeue` cannot drain, and must not try.** `ackmode=ack_requeue_true`
  puts messages straight back, so a loop re-reads the same ones forever — the
  same trap as per-message nack over AMQP. See the behavior table below.
- **Never pass `truncate`**, and handle `payload_encoding: base64` on the way
  in. `/get` returns the body as a plain string only when it is valid UTF-8 and
  base64-encodes it otherwise; ignoring that silently corrupts binary bodies and
  breaks the round-trip guarantee.

### HTTP consume behavior

`/get` takes `count` and `ackmode` and returns up to `count` messages in one
response. What that permits differs sharply by ackmode, so the two cases are
written as two separate implementations — do not unify them:

| Invocation | Mechanism | Drains? |
|---|---|---|
| `consume` | loop `/get` with `ackmode=ack_requeue_false` until a short/empty response | **yes** — messages are deleted, so the queue empties |
| `consume --count N` | same loop, stopping at N | yes, or exit 3 if it empties early |
| `consume --follow` | same loop, 1 s poll interval, never exits | n/a |
| `consume --requeue` | **one** `/get` with `ackmode=ack_requeue_true` | **no** — bounded to a single batch |

`--requeue` over HTTP reads `--count` messages, or a default batch if `--count`
is omitted, and then stops. It must print a stderr warning saying the queue was
not drained and how many were read. Do not attempt to derive a count from
`messages_ready` to fake a drain — that races any live producer and is exactly
the "engineering around a limitation" this section forbids.

**Batch size is the data-loss window.** `ackmode` is applied server-side before
the response is sent, so every message in a batch is already gone from the broker
by the time rmq starts writing it out. A crash mid-write loses the whole batch.
Keep the batch small — this path is for troubleshooting, not throughput, and
RabbitMQ's own docs say `/get` is "intended for development and troubleshooting
only, not for production."
- RabbitMQ's own docs mark `/get` as unsuitable for production or high-volume use.

## Configuration

One connection knob, resolved in this precedence order (highest wins):

1. Individual flags — `--host`, `--port`, `--vhost`, `--user`, `--password`
2. `--url amqp://user:pass@host:port/vhost`
3. `$RMQ_URL` environment variable
4. Defaults — `amqp://guest:guest@localhost:5672/`

Individual flags override the corresponding component of a URL, so
`--url amqp://prod/ --vhost /test` is meaningful and well-defined.

### URL schemes

The scheme is what selects transport, TLS, and the default port — there is no
separate `--tls` flag:

| Scheme | Transport | TLS | Default port |
|---|---|---|---|
| `amqp://` | AMQP | no | 5672 |
| `amqps://` | AMQP | yes | 5671 |
| `http://` | Management HTTP | no | 15672 |
| `https://` | Management HTTP | yes | 443 |

`http(s)://` implies `--transport http` and points at the Management API
directly — the case where only 80/443 are open, which is why `https://` defaults
to 443 rather than a broker port.

With an `amqp(s)://` URL, the HTTP transport instead derives its base URL from
the same host using `--management-port`, whose default follows the scheme:
**15672 for `amqp://`, 15671 for `amqps://`** — RabbitMQ's own plain and TLS
management listener ports. The scheme already said whether TLS is in play, so the
port should not have to be repeated.

`--insecure` disables certificate validation (accepts self-signed certificates
and hostname mismatches) for dev brokers. It is the only TLS knob; SNI is
derived from the URL host. Negotiate TLS 1.2/1.3 only.

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

**`--raw` writes no separator at all** — not even a newline between messages.
Adding one would corrupt the single case `--raw` exists to serve, extracting a
body byte-for-byte, and a delimiter that is safe for binary does not exist.
Draining many messages is what NDJSON is for; `--raw` is for one payload, or for
a stream whose framing the receiving tool already knows.

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
None of it buys anything the loop above does not already provide.

Both APIs feed the same loop:

- **Pull** (`BasicGetAsync`) — the `Receive` call is literal.
- **Push** (`BasicConsumeAsync` / `IAsyncBasicConsumer`) — delivery arrives on a
  callback that must not block. A **single bounded `Channel<T>` is the sanctioned
  adapter** between that callback and the loop. This one channel is explicitly
  allowed and is not what the paragraph above bans — what is banned is the
  multi-task pipeline built on top of it.

**There is no `--prefetch-count` flag.** Broker QoS and the internal channel
bound are both internal constants — exposing them bought nothing but flag
validation. Do not reintroduce the flag; if a value needs tuning, change the
constant. See `--requeue` below for the one case where the two diverge.

### `--requeue` (the former `peek`)

`--requeue` **does not nack per message.** It skips the ack call entirely: read,
write, never acknowledge, then let the channel close — RabbitMQ requeues every
unacked delivery automatically. The sequential loop above is unchanged; one line
is simply not executed.

Do not implement this as `BasicNackAsync(requeue: true)` inside the loop. A
requeued message returns to the head of the queue and is immediately re-read, so
the loop re-emits the same message forever and never drains.

**`--requeue` drains by default, exactly like a normal consume.** Omitting
`--count` reads the queue to empty and exits. The message-count semantics do not
change based on `--requeue`.

Making that work requires two internal numbers that must not be conflated:

| Number | Normal consume | Under `--requeue` |
|---|---|---|
| Broker prefetch (`basic.qos`) | fixed constant (~100) | **0 / unlimited** |
| Internal `Channel<T>` bound | fixed constant | **unchanged — same constant** |

Broker prefetch must be 0 under `--requeue`, or the broker sends prefetch-many
deliveries, receives no acks, and delivery stalls mid-queue. The internal buffer
must *not* follow it to 0, or it becomes unbounded and client memory grows with
queue depth. Both are compile-time constants with no flag behind them.

Consequences to honor:

- The broker holds the whole queue as unacked for the duration of the run. This
  is unavoidable — AMQP 0-9-1 has no cursor or offset, so any non-destructive
  full read must hold everything unacked until the channel closes.
- **Warn on stderr about unbounded growth.** Without `--count`, always — it is
  unbounded by definition. With `--count N`, only when N is large (**≥ 1000**,
  matching the threshold the old `peek` used).
- **`--requeue` and `--follow` are mutually exclusive** — holding an entire queue
  unacked indefinitely is worse than holding prefetch-many. Reject at parse time.
  This remains the *only* flag-combination validation `consume` needs; flags that
  do not apply to a given path are ignored, not rejected.
- Requeued messages come back flagged `redelivered`. This is unavoidable over
  AMQP; state it in `--help`.

## Message model

Messages support the full set of RabbitMQ properties — content type, content
encoding, delivery mode, priority, correlation ID, reply-to, expiration, message
ID, timestamp, type, user ID, app ID — plus arbitrary headers.

**The wire schema is [`docs/message-schema.md`](docs/message-schema.md)** — one
definition, from which both the serializer and the parser are derived. Change it
there first, never in one direction only.

**`publish` reads on STDIN exactly the NDJSON that `consume` writes on STDOUT**,
so this is a supported, tested composition:

```bash
rmq consume -q source --url amqp://a/ | rmq publish -q dest --url amqp://b/
```

One JSON object per line, each with `body`, `properties`, and `routingKey`;
headers nest inside `properties`. `--message` takes the same single-object shape.
Keep the two schemas identical — if a property can be emitted, it must be
accepted.

The round-trip guarantee, stated precisely, because one half of it is a trade
rather than an absolute:

- **Properties round-trip byte-identically.** No exceptions.
- **Text and binary bodies round-trip byte-identically.** Binary rides as base64
  under an explicit `bodyEncoding` marker; a body that merely *looks* like base64
  is not decoded without it.
- **JSON bodies round-trip semantically, not byte-for-byte.** They are emitted
  inline so `jq '.body.orderId'` works, which means re-serialization drops
  insignificant whitespace. `--raw` is the escape hatch when the original bytes
  matter.

This is the property worth testing hardest, and the current tool fails it
outright: `consume` emits `"body":{…}` while `publish` parses `body` as a string,
so the pipe above throws on every JSON-bodied message. One converter serves both
directions now, and it must read back exactly what it writes.

## Testing

Two projects, and no more:

- **E2E** against real RabbitMQ via Testcontainers. This is where confidence
  actually comes from. Must cover:
  - publish → consume round trip, once each for push and `--pull`
  - full property round-trip fidelity (the `consume | publish` pipe above)
  - `--requeue` leaves the queue depth unchanged **and terminates** — the
    regression test for the nack-loop trap described in Delivery semantics
  - exit code 3 when `--count` exceeds queue depth
  - `purge`
  - a single HTTP-transport round trip — not a parallel suite
- **Unit**, thin, for the pure functions only — message JSON parsing, header
  parsing (`k:v`), property merging, AMQP URL parsing.

Do not write tests for: DTO constructors, property getters, enum round-tripping,
factory methods, or anything whose failure would be caught immediately by the
E2E round trip.

**Coverage percentage is not a goal.** Do not add tests to satisfy a threshold.

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
