#!/usr/bin/env bash
# Populate RabbitMQ with mixed message types (JSON, plain text, structured data)

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"

echo "Populating '$QUEUE' with mixed message types..."

# Create temporary file with various message formats
TEMP_FILE=$(mktemp)
trap "rm -f $TEMP_FILE" EXIT

cat > "$TEMP_FILE" << 'EOF'
Plain text message - simple and straightforward
{"type": "json", "data": {"id": 1, "value": "structured"}}
key1=value1,key2=value2,key3=value3
<xml><message>XML formatted data</message><id>42</id></xml>
WARN: Low disk space - 15% remaining
user_id:12345|action:purchase|item_id:prod_789|price:29.99
SUCCESS,2025-01-15T14:30:00Z,operation_completed,duration_ms:1250
EOF

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --message-file "$TEMP_FILE"
