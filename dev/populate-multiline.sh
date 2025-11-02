#!/usr/bin/env bash
# Populate RabbitMQ with multi-line text messages

set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

QUEUE="test-queue"

echo "Populating '$QUEUE' with multi-line messages..."

# Create temporary file with multi-line messages
cat > /tmp/rmq-multiline-messages.txt << 'EOF'
This is a multi-line message.
It spans multiple lines.
Great for testing formatters!
---
Subject: Important Update
From: System Admin
To: All Users

Hello team,

This is a multi-line email-style message.
It contains headers and body content.

Best regards,
The System
---
ERROR: Connection timeout
Stack trace:
  at Connection.connect() line 42
  at Database.query() line 15
  at Application.start() line 8

Timestamp: 2025-01-15 12:34:56
Severity: CRITICAL
---
{
  "message": "This is JSON",
  "but": "formatted",
  "across": "multiple lines",
  "for": "readability"
}
---
Log Entry #1: Application started
Log Entry #2: Database connected
Log Entry #3: Cache initialized
Log Entry #4: API server listening on :8080
Log Entry #5: Ready to accept requests
EOF

dotnet run --project "$PROJECT_ROOT/src/RmqCli/RmqCli.csproj" --no-build --no-launch-profile -- \
  publish --queue "$QUEUE" --from-file /tmp/rmq-multiline-messages.txt

rm /tmp/rmq-multiline-messages.txt
