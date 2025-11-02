# Development Scripts

This directory contains scripts to help with local development and testing of the RabbitMQ CLI tool.

## Prerequisites

- RabbitMQ server running (see main README for Docker command)
- Project built: `dotnet build` or `just build`

## Test Data Population Scripts

### Simple Messages
```bash
./dev/populate-simple.sh [count]
```
Publishes simple text messages to `test-queue`. Default count is 10 if not specified.

Examples:
```bash
# 10 messages (default)
./dev/populate-simple.sh

# 50 messages
./dev/populate-simple.sh 50
```

**Note:** All messages are generated and sent in a single publish operation for better performance.

### JSON Messages
```bash
./dev/populate-json.sh
```
Publishes 10 structured JSON messages (orders, users, payments, notifications, inventory) to `test-queue`.

### Large Messages
```bash
./dev/populate-large.sh
```
Publishes 5 large messages (~5KB each) for performance testing.

### Burst Messages
```bash
./dev/populate-burst.sh
```
Uses the `--burst` flag to publish 100 messages quickly.

### Multi-line Messages
```bash
./dev/populate-multiline.sh
```
Publishes 5 multi-line messages (emails, logs, error traces) to test formatters.

### Mixed Format Messages
```bash
./dev/populate-mixed.sh
```
Publishes 7 messages with different formats (JSON, XML, CSV, key-value, plain text).

## Cleanup

### Clean a Queue
```bash
./dev/cleanup-queue.sh [queue-name]
```
Consumes and acknowledges all messages from a queue (defaults to `test-queue`). Uses a 4-second timeout to automatically stop when the queue is empty.

Examples:
```bash
# Clean default queue
./dev/cleanup-queue.sh

# Clean specific queue
./dev/cleanup-queue.sh my-custom-queue
```

## Typical Development Workflow

```bash
# 1. Start RabbitMQ
docker run -d --hostname rmq --name rabbit-server -p 8080:15672 -p 5672:5672 rabbitmq:4-management

# 2. Build the project
just build

# 3. Populate test data
./dev/populate-simple.sh 100    # 100 simple messages
# or
./dev/populate-json.sh          # 10 JSON messages

# 4. Test consuming
just run consume --queue test-queue --count 5

# 5. Test peeking
just run peek --queue test-queue --count 3 --output json

# 6. Clean up when done
./dev/cleanup-queue.sh
```

## Performance Testing

For performance testing, you can quickly generate many messages:

```bash
# Generate 10,000 simple messages
./dev/populate-simple.sh 10000

# Test consume performance
time just run consume --queue test-queue --count 10000 --ack-mode ack --output plain > /dev/null

# Clean up
./dev/cleanup-queue.sh
```

## Making Scripts Executable

If you get permission errors, make the scripts executable:

```bash
chmod +x dev/*.sh
```
