#!/usr/bin/env bash
# Populate RabbitMQ with simple text messages and demonstrate various features
# Usage: ./populate-simple.sh

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"

echo "Populating '$QUEUE' with messages demonstrating various features..."

# 1. Simple plain text message
echo ""
echo "1. Publishing a plain text message..."

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --body "Hello, RabbitMQ!"

# 2. Message with properties
echo ""
echo "2. Publishing message with properties..."
dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" \
  --body "High priority order" \
  --priority 9 \
  --content-type "text/plain" \
  --app-id "order-service"

# 3. Message with custom headers
echo ""
echo "3. Publishing message with custom headers..."
dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" \
  --body "Order with metadata" \
  -H "x-tenant:acme-corp" \
  -H "x-user-id:user-123" \
  -H "x-region:us-east-1"

# 4. Message with both properties and headers
echo ""
echo "4. Publishing message with properties and headers..."
dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" \
  --body "Full-featured message" \
  --priority 5 \
  --content-type "application/json" \
  --correlation-id "req-$(uuidgen)" \
  -H "x-tenant:premium" \
  -H "x-version:2.0"
