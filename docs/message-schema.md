# NDJSON message schema

One JSON object per line. `consume` writes it; `publish` reads it. **The two are
the same schema** — if `consume` can emit a field, `publish` must accept it. This
file is the single definition both sides are derived from.

```bash
rmq consume -q source --url amqp://a/ | rmq publish -q dest --url amqp://b/
```

## Shape

A line with everything set (`bodyEncoding` is absent here because the body is
text — see Body encoding below):

```json
{
  "body": {"orderId": 42},
  "routingKey": "orders.created",
  "exchange": "events",
  "redelivered": true,
  "properties": {
    "contentType": "application/json",
    "contentEncoding": "gzip",
    "deliveryMode": 2,
    "priority": 5,
    "correlationId": "b1f2…",
    "replyTo": "rpc.reply",
    "expiration": "60000",
    "messageId": "msg-1",
    "timestamp": 1755168000,
    "type": "OrderCreated",
    "userId": "guest",
    "appId": "checkout",
    "headers": {"x-attempt": 3, "x-source": "web"}
  }
}
```

A binary body instead looks like this, and the two forms never co-occur:

```json
{"body":"//4AAYA=","bodyEncoding":"base64","routingKey":"uploads"}
```

Every field is optional except `body`. Null and default values are **omitted on
write** and **accepted on read**, so a message with no properties is just
`{"body":"hello"}` and `redelivered` appears only when it is true.

`properties` carries the full AMQP 0-9-1 property set minus `clusterId`, which is
deprecated and deliberately dropped. Headers are nested **inside** `properties`,
not at the root — they are message properties, and nesting keeps the root
reserved for envelope and routing.

## Body encoding

`body` is a JSON *value*, not always a string. Three cases, decided by the bytes:

| Body bytes | Emitted as | `bodyEncoding` |
|---|---|---|
| valid UTF-8 that parses as a JSON object or array | that JSON, inline | omitted |
| valid UTF-8, anything else | a JSON string | omitted |
| not valid UTF-8 (binary) | a base64 JSON string | `"base64"` |

Inline JSON is the point of the whole schema: `jq '.body.orderId'` works
directly, with no `fromjson` step.

Reading is the exact inverse, and `bodyEncoding` is what disambiguates a body
that happens to *look* like base64 from one that *is*:

- `bodyEncoding: "base64"` → decode base64 to bytes
- a JSON string → UTF-8 bytes of the string
- any other JSON value → UTF-8 bytes of its compact serialization

### Fidelity guarantee

**Properties round-trip byte-identically. Bodies round-trip byte-identically
except when the body is JSON, where the guarantee is semantic.**

A JSON body is re-emitted compactly, so insignificant whitespace and key
formatting are not preserved:

```
in:  {\n  "orderId": 42\n}    (24 bytes)
out: {"orderId":42}           (15 bytes)
```

This is a deliberate trade — inline `jq`-native bodies were chosen over byte
exactness for JSON specifically. Text and binary bodies are unaffected and are
byte-exact, which is where corruption actually bites. If you need a JSON body
preserved to the byte, `--raw` writes the original bytes untouched.

## Header values

AMQP field tables are typed, so header values are typed here too: JSON strings,
numbers, and booleans map to AMQP `longstr`, `long`/`double`, and `bool`. On the
way in, `--header k:v` infers the type from the text — `3` is a number, `true` is
a boolean, everything else is a string.

**Header conversion happens at the AMQP boundary, not in the serializer.**
RabbitMQ.Client hands back `byte[]` for every `longstr` header, and `byte[]`
serialized as JSON becomes base64 — so a header reading `x-source: web` would
silently arrive as `"d2Vi"`. Consume decodes those to strings before building the
`Message`, which is also why `byte[]` is deliberately *not* registered in
`MessageJsonContext`: a `byte[]` reaching the serializer is a bug at the boundary,
and leaving it unregistered makes that fail loudly rather than quietly.

Header values are not always scalars. A dead-lettered message carries `x-death`,
which is a **list of nested field tables**, and RabbitMQ also uses
`AmqpTimestamp` and `BinaryTableValue` inside tables. The boundary therefore
normalizes the whole tree — recursively, not just the top level — into the closed
set this schema names:

| AMQP field-table type | Emitted as |
|---|---|
| `longstr` (`byte[]`) | JSON string, or base64 if the bytes are not valid UTF-8 |
| `bool` | JSON boolean |
| any integer width | JSON number (long) |
| `float` / `double` / `decimal` | JSON number (double) |
| `AmqpTimestamp` | JSON number, Unix seconds |
| `BinaryTableValue` | base64 JSON string |
| nested table | JSON object |
| array | JSON array |

Reading is the exact inverse: nested objects and arrays become field tables
again, so a consumed `x-death` republishes as the table it was rather than as a
JSON string. Note that unlike the top-level `body`, header binary has no
`bodyEncoding`-style marker — a `longstr` that is not valid UTF-8 becomes base64
and reads back as that base64 text. Headers are metadata; this has never mattered
in practice, and adding a per-value discriminator would complicate every header
for a case nobody has.

## Fields not in the schema

Deliberately absent, because `publish` would have to accept anything `consume`
emits and none of these mean anything on the way back in:

- `deliveryTag` — a per-channel counter, meaningless outside the session
- `queue` — the source queue; the destination comes from `-q` / `-e`
- `bodySize` / `bodySizeBytes` — derivable from `body`

`redelivered` *is* kept: it survives a `--requeue` run and is worth seeing.
`publish` accepts and ignores it.

## Precedence on publish

CLI options beat the JSON line, per field. `rmq publish -q dest --priority 9`
against a line carrying `"priority": 1` publishes with priority 9, and the
routing target always comes from `-q` / `-e --routing-key` rather than from
`routingKey` in the line.
