#!/usr/bin/env bash
# Clean up test queue by purging it via RabbitMQ Management API

set -e

QUEUE="${1:-test-queue}"
RABBITMQ_HOST="${RABBITMQ_HOST:-127.0.0.1}"
RABBITMQ_PORT="${RABBITMQ_PORT:-8080}"
RABBITMQ_USER="${RABBITMQ_USER:-guest}"
RABBITMQ_PASS="${RABBITMQ_PASS:-guest}"
VHOST="${VHOST:-%2F}"  # URL-encoded / (default '/' vhost)

echo "Purging queue: $QUEUE"

# Purge the queue using RabbitMQ Management API
curl -s -X DELETE \
  -u "$RABBITMQ_USER:$RABBITMQ_PASS" \
  "http://$RABBITMQ_HOST:$RABBITMQ_PORT/api/queues/$VHOST/$QUEUE/contents" \
  > /dev/null

echo "Queue purged: $QUEUE"
