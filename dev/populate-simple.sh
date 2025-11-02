#!/usr/bin/env bash
# Populate RabbitMQ with simple text messages
# Usage: ./populate-simple.sh [count]
# Default count: 10

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"
COUNT="${1:-10}"

echo "Populating '$QUEUE' with $COUNT simple messages..."

# Create temporary file with messages
TEMP_FILE=$(mktemp)
trap "rm -f $TEMP_FILE" EXIT

for i in $(seq 1 $COUNT); do
  echo "Simple message #$i" >> "$TEMP_FILE"
done

# Publish all messages in one command
dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --from-file "$TEMP_FILE"

