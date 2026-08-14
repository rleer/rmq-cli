# rmq — a RabbitMQ CLI

[![.NET](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml/badge.svg)](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml)
[![License](https://img.shields.io/github/license/rleer/rmq-cli)](LICENSE)

A developer-facing command line tool for publishing and consuming RabbitMQ
messages. It behaves like a normal Unix tool: it reads STDIN, writes STDOUT,
composes with pipes, exits with meaningful codes, and stays out of the way.

```bash
rmq consume -q orders | jq '.body.orderId'
rmq consume -q source --url amqp://a/ | rmq publish -q dest --url amqp://b/
```

It is not a monitoring tool, not an admin console, and not a library. There are
three commands.

## Install

### Native binary

Self-contained and AOT-compiled — no .NET runtime needed to run it, and it starts
in a few milliseconds.

```bash
dotnet publish src/rmq/rmq.csproj -c Release -r osx-arm64 -o release
```

Replace `osx-arm64` with your target: `osx-x64`, `linux-x64`, `linux-arm64`,
`win-x64`. The binary lands in `release/`.

### .NET global tool

```bash
dotnet pack
dotnet tool install -g --source ./nupkg rmq   # dotnet tool uninstall -g rmq
```

Requires the .NET 8 SDK.

## Connecting

There is no config file. One connection knob, resolved in this order — highest
wins:

1. Individual flags: `--host`, `--port`, `--vhost`, `--user`, `--password`
2. `--url amqp://user:pass@host:port/vhost`
3. `$RMQ_URL`
4. Defaults: `amqp://guest:guest@localhost:5672/`

Precedence is applied per component, so `--url amqp://prod/ --vhost /test` is
meaningful. If you have a broker you always talk to, put it in your shell profile:

```bash
export RMQ_URL=amqp://guest:guest@localhost:5672/
```

The scheme selects transport, TLS, and the default port — there is no `--tls` flag:

| Scheme | Transport | TLS | Default port |
|---|---|---|---|
| `amqp://` | AMQP | no | 5672 |
| `amqps://` | AMQP | yes | 5671 |
| `http://` | Management API | no | 15672 |
| `https://` | Management API | yes | 443 |

`--insecure` accepts self-signed certificates and hostname mismatches, for dev
brokers. It is the only TLS knob.

## Commands

### publish

```bash
# A literal body
rmq publish -q orders --body "hello"

# Properties and headers
rmq publish -q orders --body "order" --priority 5 --persistent -H "x-tenant:acme"

# One message as JSON, in the shape consume emits
rmq publish -q orders --message '{"body":{"id":1},"properties":{"priority":5}}'

# NDJSON from a file or a pipe
rmq publish -q orders --message-file batch.ndjson
cat batch.ndjson | rmq publish -q orders

# Via an exchange
rmq publish -e events --routing-key order.created --body "..."
```

The body comes from `--body`, `--message`, `--message-file`, or STDIN. Header
values are typed by inspection: `-H "x-attempt:3"` is a number, `true` is a
boolean, everything else is a string. Property flags override the same field in
the JSON, per field, and the destination always comes from `--queue` or
`--exchange` — never from the message.

Publishing to a `--queue` that does not exist is an error. Publishing to an
`--exchange` that routes nowhere is not.

### consume

```bash
rmq consume -q orders                  # drain the queue, then exit
rmq consume -q orders --count 10       # stop after 10
rmq consume -q orders --follow         # keep waiting; Ctrl-C to stop
rmq consume -q orders --requeue        # read everything, put it all back
rmq consume -q orders --pull           # take only what is asked for
rmq consume -q orders --to-file out.ndjson
```

**`consume` terminates on its own**, which is what makes `rmq consume -q orders | jq`
work. It exits when the queue is empty:

| Invocation | Exits when |
|---|---|
| `consume` | queue is empty |
| `consume --count N` | N consumed, **or** the queue empties first (exit code 3) |
| `consume --follow` | Ctrl-C only |
| `consume --requeue` | queue is empty — same as bare `consume` |

Ctrl-C always exits cleanly, having acknowledged everything already written.

#### Push, pull, and requeue

By default `consume` **registers as a consumer** (`basic.consume`): it appears in
the broker's consumer list, joins round-robin distribution alongside any existing
consumers, and the broker pushes messages at it continuously. That is the footgun
when attaching to a live queue.

- **`--pull`** registers nothing and takes exactly what is asked for, once. This
  is the answer for inspecting a queue that production depends on.
- **`--requeue`** is the answer for giving everything back: nothing is
  acknowledged, and the broker returns every message when the connection closes.
  Requeued messages come back flagged `redelivered`, which AMQP offers no way to
  avoid. The broker holds the whole queue unacknowledged for the duration, so rmq
  warns about memory growth.

Both push and pull *remove* messages. The difference is that push keeps taking
them.

**`--consumer-priority N`** sets `x-priority` on the push path. Lower-priority
consumers receive messages only once every higher-priority consumer is blocked,
so a negative value is the polite way to attach to a queue production depends on.

### purge

```bash
rmq purge orders
```

Discards every message in a queue, over AMQP. There is no undo and no
confirmation prompt.

## Output

Output shape follows the destination:

- **Piped or redirected** → NDJSON, one complete JSON object per line. No ANSI
  escapes, no decoration, no progress output.
- **A terminal** → a human-readable form with colour.

| Flag | Effect |
|---|---|
| `--json` | Force NDJSON even on a terminal |
| `--raw` | Write **only** the message body bytes — no envelope, and no separator between messages |
| `--to-file <path>` | Write NDJSON to a file instead of stdout |
| `--verbose` | Diagnostics on stderr |

`--raw` writes no separator at all, not even a newline, because a delimiter that
is safe for arbitrary binary does not exist. Use it for one payload; use NDJSON
for many. Splitting a large drain is `split`'s job:

```bash
rmq consume -q orders | split -l 10000 -
```

Diagnostics, warnings, and errors always go to stderr, so piping is never
contaminated.

### Message format

`publish` reads on STDIN exactly the NDJSON that `consume` writes on STDOUT, so
round-tripping a queue between brokers is a pipe. One JSON object per line with
`body`, `properties`, and `routingKey`; headers nest inside `properties`. JSON
bodies are emitted inline so `jq '.body.orderId'` works with no `fromjson` step,
and binary bodies ride as base64 under an explicit `bodyEncoding` marker.

The full definition is [`docs/message-schema.md`](docs/message-schema.md).

## Exit codes

Scripts branch on these, so they are part of the contract:

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Connection or authentication failure |
| 2 | Usage error (bad flags, mutually exclusive options) |
| 3 | Completed, but `--count` was not satisfied (the queue drained early) |
| 130 | Interrupted (Ctrl-C) — messages written so far are acknowledged |

Code 3 is the one that matters for pipelines: it distinguishes "got fewer
messages than asked for" from a real failure.

## Transports

`--transport amqp` (the default) is the real implementation. Every operation has
an AMQP implementation, `purge` included.

`--transport http` uses the Management HTTP API, and exists for exactly one
reason: **networks where the AMQP port is blocked and only 80/443 reach the
broker.** It is a degraded fallback, not a co-equal path.

| Operation | AMQP (default) | HTTP fallback |
|---|---|---|
| publish | `basic.publish` | `POST /api/exchanges/…/publish` |
| consume | `basic.consume` / `basic.get` | `POST /api/queues/…/get` |
| purge | `queue.purge` | `DELETE /api/queues/…/contents` |

Known and accepted limitations of the HTTP path:

- **No push support.** `--pull` and `--consumer-priority` are ignored, and
  `--follow` becomes a poll loop.
- **No delivery tags**, so the ack-after-write guarantee below does not hold. A
  crash mid-write can lose the batch in hand.
- **`--requeue` cannot drain.** Requeued messages are handed straight back, so it
  reads one batch, warns on stderr, and stops.
- **`purge` reports no count** — the API answers with an empty body.

An `http(s)://` URL implies `--transport http`. With an `amqp(s)://` URL, the
HTTP transport derives its base URL from the same host using `--management-port`,
which defaults to 15672 (or 15671 under TLS).

## Delivery guarantee

Consume is a single sequential loop: receive, write, flush, acknowledge. A
message is acknowledged only after it has been durably written, so a crash cannot
lose data. That is the whole guarantee, and it applies to the AMQP transport.

## Development

Start a broker with the management plugin:

```bash
docker run -d --hostname rmq --name rabbit-server -p 8080:15672 -p 5672:5672 rabbitmq:4-management
```

The management UI is at [http://localhost:8080](http://localhost:8080), user and
password both `guest`. The `dev/` directory has scripts that populate queues with
test messages.

```bash
dotnet build
dotnet test test/rmq.Unit.Tests   # pure functions, fast, no broker

just prepare-e2e-test             # publish the native binary the E2E tests drive
dotnet test test/rmq.E2E.Tests    # needs Docker

dotnet test                       # both, via the solution
```

There are two test projects and no more. The unit tests cover the pure functions
— message JSON, header parsing, property merging, URL resolution. The E2E tests
drive the *published native binary* against a real RabbitMQ in Testcontainers,
which is where confidence actually comes from, and they need Docker. Run
`just --list` for the other shortcuts.

Two things are easy to break silently and are worth checking before calling a
change done:

```bash
dotnet publish src/rmq/rmq.csproj -c Release -r osx-arm64 2>&1 | grep -E "IL[0-9]{4}"
time ./release/rmq --help
```

The first must print nothing — zero trim or AOT warnings is a hard requirement.
The second must stay under 20 ms. [`CLAUDE.md`](CLAUDE.md) has the full set of
constraints this repository is built around.
