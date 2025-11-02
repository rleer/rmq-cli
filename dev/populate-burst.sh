#!/usr/bin/env bash
# Populate RabbitMQ with a burst of many messages (using --burst flag)
# Usage: ./populate-burst.sh [burst_size]
# Default count: 100

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"
BURST_SIZE=${1:-100}

echo "Populating '$QUEUE' with burst of $BURST_SIZE messages..."

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --body "Burst test message" --burst $BURST_SIZE
