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

# Publish native binary for E2E tests and place in designated directory
prepare-e2e-test:
    rm -rf test/RmqCli.E2E.Tests/bin/rmq-published
    dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o test/RmqCli.E2E.Tests/bin/rmq-published

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

# Run rmq via dotnet run (usage: just run <args>)
# Note that 'just' will flatten the arguments into a single string. This might cause unexpected escaping behaviour. Use the rmq-dev alias to avoid that.
run *args:
    @dotnet run --project src/RmqCli/RmqCli.csproj --no-build --no-launch-profile -- {{args}}

# Create alias for running rmq via dotnet run (use: eval $(just create-alias))
create-alias:
    @echo "alias rmq-dev='dotnet run --project {{justfile_directory()}}/src/RmqCli/RmqCli.csproj --no-build --no-launch-profile --'"

# Clean build artifacts
clean:
    dotnet clean
    rm -rf release
    rm -rf nupkg
