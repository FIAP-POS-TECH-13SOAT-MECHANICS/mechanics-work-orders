#!/bin/bash

QUEUES=(
  "fiap-mechanics-dev-customer-created"
  "fiap-mechanics-dev-work-order-created"
  "fiap-mechanics-dev-status-changed"
  "fiap-mechanics-dev-payment-approved"
)

for QUEUE in "${QUEUES[@]}"; do
  awslocal sqs create-queue --queue-name "$QUEUE"
done
