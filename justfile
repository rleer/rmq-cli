# justfile for rmq-cli

# Default recipe - list available commands
default:
    @just --list

# Build the project
build:
    dotnet build

# Run all tests
test:
    dotnet test

# Build and run tests
check: build test

# Publish native binary (replaces previous build)
publish:
    rm -rf release
    dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o release

# Install rmq as a global dotnet tool
install:
    dotnet pack
    dotnet tool install -g --source . rmq

# Uninstall rmq global tool
uninstall:
    dotnet tool uninstall -g rmq

# Reinstall rmq global tool (uninstall + install)
reinstall: uninstall install

# Clean build artifacts
clean:
    dotnet clean
    rm -rf release
    rm -rf nupkg
