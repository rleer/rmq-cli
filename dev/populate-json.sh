#!/usr/bin/env bash
# Populate RabbitMQ with JSON messages

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"

echo "Populating '$QUEUE' with JSON messages..."

# Create temporary JSON messages file
cat > /tmp/rmq-json-messages.txt << 'EOF'
{"id": 1, "type": "order", "status": "pending", "amount": 99.99, "timestamp": "2025-01-15T10:30:00Z"}
{"id": 2, "type": "order", "status": "completed", "amount": 150.00, "timestamp": "2025-01-15T11:15:00Z"}
{"id": 3, "type": "user", "action": "login", "userId": "user123", "ip": "192.168.1.1"}
{"id": 4, "type": "user", "action": "logout", "userId": "user456", "ip": "192.168.1.2"}
{"id": 5, "type": "payment", "status": "success", "transactionId": "txn_abc123", "amount": 250.50}
{"id": 6, "type": "payment", "status": "failed", "transactionId": "txn_def456", "amount": 75.00, "error": "insufficient_funds"}
{"id": 7, "type": "notification", "channel": "email", "recipient": "user@example.com", "subject": "Order Confirmation"}
{"id": 8, "type": "notification", "channel": "sms", "recipient": "+1234567890", "message": "Your order is ready"}
{"id": 9, "type": "inventory", "productId": "prod_001", "action": "restock", "quantity": 100}
{"id": 10, "type": "inventory", "productId": "prod_002", "action": "sale", "quantity": -5}
EOF

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --from-file /tmp/rmq-json-messages.txt

rm /tmp/rmq-json-messages.txt
