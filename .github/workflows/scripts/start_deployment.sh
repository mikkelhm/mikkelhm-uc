#!/bin/bash

# Set variables
projectId="$1"
deploymentId="$2"
url="https://api.cloud.umbraco.com/v1/projects/$projectId/deployments/$deploymentId"
apiKey="$3"

# Define function to call API to start thedeployment
function call_api {
  echo "$url"
  response=$(curl --insecure -s -X PATCH $url \
    -H "Umbraco-Cloud-Api-Key: $apiKey" \
    -H "Content-Type: application/json" \
    -d "{\"deploymentState\": \"Queued\"}")
  echo "$response"
  # http status 202 expected here
  # extract status for validation
  status=$(echo "$response" | jq -r '.deploymentState')
  if [[ $status != "Queued" ]]; then
    echo "Unexpected status: $status"
    exit 1
  fi
}

call_api

echo "Deployment started successfully -> $deployment_id"