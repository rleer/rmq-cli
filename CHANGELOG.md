# Changelog

## 0.1.0

First release. `rmq` is a developer-facing CLI for publishing and consuming
RabbitMQ messages, built to behave like a normal Unix tool: it reads STDIN,
writes NDJSON on STDOUT, composes with pipes, and exits with codes scripts can
branch on.

### Commands

- **`publish`** — to a queue or an exchange, from `--body`, `--message`,
  `--message-file`, or STDIN. Full AMQP property set plus arbitrary headers,
  typed by inspection. Publishing to a `--queue` that does not exist is an error.
- **`consume`** — drains a queue and exits, so `rmq consume -q orders | jq`
  terminates on its own. `--count N` stops early and reports exit code 3 if the
  queue empties first; `--follow` waits indefinitely; `--pull` takes only what is
  asked for without registering as a consumer; `--requeue` reads everything and
  gives it all back.
- **`purge`** — empties a queue over AMQP.

### Guarantees

- **`publish` reads exactly the NDJSON that `consume` writes**, so moving a queue
  between brokers is a pipe. Properties round-trip byte-identically; text and
  binary bodies do too, with binary carried as base64 under an explicit
  `bodyEncoding` marker. JSON bodies round-trip semantically, because they are
  emitted inline so `jq '.body.orderId'` works without a `fromjson` step. The
  schema is [`docs/message-schema.md`](docs/message-schema.md).
- **A message is acknowledged only after it has been durably written**, so a
  crash cannot lose data. Ctrl-C exits cleanly with everything already written
  acknowledged.
- **Diagnostics always go to stderr**, so piping is never contaminated.

### Connecting

`$RMQ_URL`, or `--url`, or individual `--host`/`--port`/`--vhost`/`--user`/
`--password` flags, resolved per component so `--url amqp://prod/ --vhost /test`
is meaningful. The URL scheme selects transport, TLS, and the default port.

`--transport http` talks to the Management HTTP API instead, for networks where
the AMQP port is blocked and only 80/443 reach the broker. It is a degraded
fallback: polling only, no delivery tags and therefore no ack-after-write
guarantee, `--requeue` reads one batch rather than draining, and `purge` reports
no count.

### Build

Self-contained NativeAOT with zero trim or AOT warnings, `--help` in a few
milliseconds, and exactly two package references — `RabbitMQ.Client` and
`System.CommandLine`.

---

### If you built from the pre-rewrite tree

Nothing was ever published or tagged, so this is a first release rather than a
breaking change to one. But the tool was rewritten from scratch and the command
surface is not compatible with what was on `main` before. What went away:

| Removed | Replacement |
|---|---|
| `rmq peek` | `rmq consume --requeue` |
| `rmq config show` / `init` / `path` / `edit` / `reset` | none — there is no config file |
| TOML config files (`~/.config/rmq/config.toml`) | `$RMQ_URL`, or `--url`, or individual flags |
| `RMQCLI_*` environment variables | `$RMQ_URL` |
| `--config`, `--user-config-path` | none |
| `--ack-mode Ack\|Reject\|Requeue` | acking is what `consume` does; `--requeue` is the one opt-out |
| `--output table\|json\|plain` | shape follows the destination; `--json` and `--raw` override |
| `--prefetch-count` | an internal constant — it bought nothing but flag validation |
| `--compact` | none |
| `--quiet`, `--no-color` | `--verbose` gates diagnostics; `$NO_COLOR` is honoured |
| `purge --force` | `purge` no longer prompts, so there is nothing to force |

`purge` also moved from the Management HTTP API to AMQP, so it no longer needs
the management port to be reachable.
