#!/usr/bin/env bash
# Populate RabbitMQ with large messages (for performance testing)

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"
COUNT=5

echo "Populating '$QUEUE' with $COUNT large messages (1KB-10KB each)..."

for i in $(seq 1 $COUNT); do
  # Generate a message with repeated content (approx 5KB)
  MESSAGE=$(printf "Large message #$i - " && head -c 5000 /dev/urandom | base64 | tr -d '\n')

  dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
    publish --queue "$QUEUE" --body "$MESSAGE"

done
