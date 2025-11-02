#!/usr/bin/env bash
# Clean up test queue by consuming and acknowledging all messages

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="${1:-test-queue}"

echo "Cleaning up queue: $QUEUE"

# Consume messages with ack (this removes them from the queue)
# Use timeout to stop after 3 seconds of waiting for messages
# The consume command will process all available messages and then timeout
timeout 4s dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  consume --queue "$QUEUE" --ack-mode ack --output plain --quiet > /dev/null 2>&1 || true

echo "Queue cleaned: $QUEUE"
