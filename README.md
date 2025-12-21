# rmq - RabbitMQ CLI Tool

[![.NET](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml/badge.svg)](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/rleer/rmq-cli/branch/main/graph/badge.svg)](https://codecov.io/gh/rleer/rmq-cli)
[![License](https://img.shields.io/github/license/rleer/rmq-cli)](LICENSE)

`rmq` is a command line tool for RabbitMQ focused on developers working with RabbitMQ. 

## Features

- **Publish Messages**: Send messages to RabbitMQ queues with options for message content, headers, and properties. Supports publishing from files or standard input.
- **Consume Messages**: Retrieve messages from RabbitMQ queues and display them in the console or save to a file. Supports different acknowledgment modes and consumption via AMQP push or pull API.
- **Message Format**: Supports plain text and JSON message formats.
- **Purge Queues**: Clear all messages from a specified queue.
- **Configuration Management**: Easily manage RabbitMQ connection settings via configuration files, environment variables, or command-line flags.
- **Cross-Platform**: Works on Windows, macOS, and Linux.
- **Lightweight**: Minimal dependencies and easy to install as a .NET global tool or native binary.

For detailed usage instructions, see the [Usage](#usage) section below.

## Installation

### Install as .NET Global Tool

Build and install `rmq` as a .NET global tool:

```bash
dotnet pack
dotnet tool install -g --source . rmq
```

This requires the .NET 8 SDK to be installed on your system.

### Uninstall

To remove the tool:

```bash
dotnet tool uninstall -g rmq
```

### Build Native Binary

To create a self-contained, AOT-compiled native binary (no .NET runtime required when running), use the following command:

```bash
dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o release
```

Replace `osx-arm64` with your target runtime identifier: `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`.

The compiled binary will be in the `release/` directory.

## Configuration

`rmq` uses TOML configuration files following standard CLI tool conventions. Configuration is loaded in the following priority order (highest priority wins):

1. CLI flags
2. Environment variables (prefixed with `RMQCLI_`)
3. Custom config file (via `--config` flag)
4. User config file: `~/.config/rmq/config.toml`
5. System-wide config file: `/etc/rmq/config.toml`

### Default Configuration

On first run, `rmq` automatically creates a default configuration file at `~/.config/rmq/config.toml`.

### Configuration File Locations

- **Linux/macOS**: `~/.config/rmq/config.toml`
- **Windows**: `%APPDATA%/rmq/config.toml`
- **System-wide (Linux/macOS)**: `/etc/rmq/config.toml`
- **System-wide (Windows)**: `%PROGRAMDATA%/rmq/config.toml`


### Environment Variables

Override any configuration setting using environment variables with the `RMQCLI_` prefix:

```bash
# Override RabbitMQ host
export RMQCLI_RabbitMq__Host=production-rabbit

# Override port
export RMQCLI_RabbitMq__Port=5673

# Override user credentials
export RMQCLI_RabbitMq__User=myuser
export RMQCLI_RabbitMq__Password=mypassword
```

Note: Use double underscores (`__`) to represent nested configuration sections.

## Usage

```bash
rmq [command] [options]
```

### Global Options

- `--host`, `--port`, `--vhost`, `--user`, `--password` - Override connection settings
- `--management-port` - Override Management API port
- `--config <path>` - Use custom configuration file
- `--verbose` / `--quiet` - Control output verbosity
- `--no-color` - Disable colored output

Run `rmq --help` to see all global options.

### Commands

#### Publish Messages

Publish messages to queues or exchanges with support for message properties and custom headers.

```bash
# Simple message
rmq publish -q my-queue --body "Hello, World!"

# With properties and headers
rmq publish -q orders --body "order" --priority 5 -H "x-tenant:acme"

# From file (auto-detects JSON/NDJSON or plain text)
rmq publish -q orders --message-file batch.ndjson

# From STDIN
cat messages.txt | rmq publish -q orders

# Via exchange
rmq publish -e my-exchange --routing-key my.key --message '{"body":"order","properties":{"priority":5}}'
```

- **Input modes:** `--body`, `--message` (JSON), `--message-file`, or STDIN
- **Properties:** `--priority`, `--content-type`, `--correlation-id`, `--delivery-mode`, etc.
- **Headers:** `-H "key:value"` (repeatable)

See `rmq publish --help` for all options.

---

#### Consume Messages

Consume messages from a queue with configurable acknowledgment and output formats.

```bash
# Basic consumption
rmq consume -q my-queue

# Limit message count
rmq consume -q my-queue --count 10

# Requeue messages (non-destructive)
rmq consume -q my-queue --ack-mode requeue

# JSON output to file
rmq consume -q my-queue --output json --to-file output.json
```

- **Ack modes:** `Ack` (default), `Reject`, `Requeue`
- **Output formats:** `table` (default), `json`, `plain`
- **Options:** `--count`, `--prefetch-count`, `--compact`, `--to-file`

See `rmq consume --help` for all options.

---

#### Inspect Messages

Non-destructive message inspection using polling. Messages are automatically requeued.

```bash
rmq peek -q my-queue --count 10 --output json
```

Uses polling (inefficient for high-volume scenarios). Peeked messages marked as redelivered.

See `rmq peek --help` for all options.

---

#### Purge Queue

Delete all ready messages from a queue via RabbitMQ's Management API.

```bash
# With confirmation
rmq purge orders

# Skip confirmation
rmq purge orders --force
```

See `rmq purge --help` for all options.

---

#### Manage Configuration

Manage TOML configuration files for connection settings.

```bash
rmq config show    # View current config
rmq config init    # Create default config
rmq config path    # Show config file location
rmq config edit    # Edit in default editor
rmq config reset   # Reset to defaults
```

See `rmq config --help` for all options.

## Development

To start a RabbitMQ server with the management plugin, run the following command:

```bash
docker run -d --hostname rmq --name rabbit-server -p 8080:15672 -p 5672:5672 rabbitmq:4-management
```

You can open the RabbitMQ management interface at [http://localhost:8080](http://localhost:8080) with the default username and password both set to `guest`.

Check the scripts in the `dev/` directory for scripts to populate queues with test messages.

### Building and Running Locally

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Run the tool locally
dotnet run -- <command>
```

Check the `justfile` for more shortcuts to common tasks by running `just --list`.
