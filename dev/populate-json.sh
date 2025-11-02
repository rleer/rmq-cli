#!/usr/bin/env bash
# Populate RabbitMQ with JSON messages using NDJSON format with properties and headers

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"

echo "Populating '$QUEUE' with JSON messages (NDJSON format with properties/headers)..."

# Create temporary NDJSON file with complete message format
TEMP_FILE=$(mktemp)
trap "rm -f $TEMP_FILE" EXIT

cat > "$TEMP_FILE" << 'EOF'
{"body":"{\"id\":1,\"type\":\"order\",\"status\":\"pending\",\"amount\":99.99}","properties":{"priority":5,"contentType":"application/json","appId":"order-service"},"headers":{"x-tenant":"acme-corp","x-region":"us-east-1"}}
{"body":"{\"id\":2,\"type\":\"order\",\"status\":\"completed\",\"amount\":150.00}","properties":{"priority":3,"contentType":"application/json","appId":"order-service"},"headers":{"x-tenant":"premium","x-region":"us-west-2"}}
{"body":"{\"id\":3,\"type\":\"user\",\"action\":\"login\",\"userId\":\"user123\"}","properties":{"contentType":"application/json","appId":"auth-service"},"headers":{"x-ip":"192.168.1.1","x-session-id":"sess-abc123"}}
{"body":"{\"id\":4,\"type\":\"payment\",\"status\":\"success\",\"amount\":250.50}","properties":{"priority":9,"contentType":"application/json","appId":"payment-service"},"headers":{"x-transaction-id":"txn-xyz789","x-merchant":"stripe"}}
{"body":"{\"id\":5,\"type\":\"notification\",\"channel\":\"email\",\"recipient\":\"user@example.com\"}","properties":{"contentType":"application/json","appId":"notification-service"},"headers":{"x-priority":"high","x-template":"order-confirmation"}}
EOF

# Publish using --message-file (auto-detects NDJSON format)
dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --message-file "$TEMP_FILE"
