# rmq - RabbitMQ CLI Tool

[![.NET](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml/badge.svg)](https://github.com/rleer/rmq-cli/actions/workflows/dotnet.yml)
[![PR Check](https://github.com/rleer/rmq-cli/actions/workflows/pr.yml/badge.svg)](https://github.com/rleer/rmq-cli/actions/workflows/pr.yml)
[![codecov](https://codecov.io/gh/rleer/rmq-cli/branch/main/graph/badge.svg)](https://codecov.io/gh/rleer/rmq-cli)
[![License](https://img.shields.io/github/license/rleer/rmq-cli)](LICENSE)

`rmq` is a command line tool for RabbitMQ focused on developers working with RabbitMQ. 


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

To create a self-contained, AOT-compiled native binary (no .NET runtime required):

```bash
dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o release
```

Replace `osx-arm64` with your target runtime identifier:
- **macOS ARM64**: `osx-arm64`
- **macOS x64**: `osx-x64`
- **Linux x64**: `linux-x64`
- **Linux ARM64**: `linux-arm64`
- **Windows x64**: `win-x64`

The compiled binary will be in the `release/` directory.

## Configuration

`rmq` uses TOML configuration files following standard CLI tool conventions. Configuration is loaded in the following priority order (highest priority wins):

1. Environment variables (prefixed with `RMQCLI_`)
2. Custom config file (via `--config` flag)
3. User config file: `~/.config/rmq/config.toml`
4. System-wide config file: `/etc/rmq/config.toml`

### Default Configuration

On first run, `rmq` automatically creates a default configuration file at `~/.config/rmq/config.toml`.

### Configuration File Locations

- **Linux/macOS**: `~/.config/rmq/config.toml`
- **Windows**: `%APPDATA%/rmq/config.toml`
- **System-wide (Linux/macOS)**: `/etc/rmq/config.toml`
- **System-wide (Windows)**: `%PROGRAMDATA%/rmq/config.toml`

### Configuration Management

Use the built-in configuration commands to manage your settings:

```bash
# Show configuration file location
rmq config path

# Display current configuration
rmq config show

# Open configuration in default editor
rmq config edit

# Reset configuration to defaults
rmq config reset

# Use a custom configuration file
rmq --config /path/to/custom-config.toml <command>
```

### Environment Variables

Override any configuration setting using environment variables with the `RMQCLI_` prefix:

```bash
# Override RabbitMQ host
export RMQCLI_RabbitMqConfig__Host=production-rabbit

# Override port
export RMQCLI_RabbitMqConfig__Port=5673

# Override user credentials
export RMQCLI_RabbitMqConfig__User=myuser
export RMQCLI_RabbitMqConfig__Password=mypassword
```

Note: Use double underscores (`__`) to represent nested configuration sections.

## Usage

### Publishing Messages

```bash
# Publish a simple text message
rmq publish --queue myqueue --message "Hello, World!"

# Publish from a file
rmq publish --queue myqueue --file message.txt

# Use custom RabbitMQ settings
rmq --config production-config.toml publish --queue myqueue --message "Hello"
```

### Consuming Messages

```bash
# Consume messages and display to console
rmq consume --queue myqueue

# Consume messages and save to file
rmq consume --queue myqueue --output file --file messages.txt
```

## Development

To start a RabbitMQ server with the management plugin, run the following command:

```bash
docker run -d --hostname rmq --name rabbit-server -p 8080:15672 -p 5672:5672 rabbitmq:4-management
```

You can open the RabbitMQ management interface at [http://localhost:8080](http://localhost:8080) with the default username and password both set to `guest`.

### Building

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

