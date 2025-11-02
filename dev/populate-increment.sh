#!/usr/bin/env bash
# Populate RabbitMQ with simple text incremental messages
# Usage: ./populate-incremental.sh [count]
# Default count: 10

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"
COUNT="${1:-10}"

echo "Populating '$QUEUE' with incremental messages..."

TEMP_FILE=$(mktemp)
trap "rm -f $TEMP_FILE" EXIT

for i in $(seq 1 $COUNT); do
  echo "Incremental message #$i" >> "$TEMP_FILE"
done

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --message-file "$TEMP_FILE"
  --content-type "text/plain" \
  --app-id "counter-service" \
  -H "x-counter:$COUNT"
