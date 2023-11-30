#!/bin/bash

# Set variables
projectId="$1"
url="https://api.cloud.umbraco.com/v1/projects/$projectId/deployments"
apiKey="$2"
commitMessage="$3"

# Define function to call API to create a new deployment
function call_api {
  echo "Posting to $url with commit message: $commitMessage"
  response=$(curl --insecure -s -X POST $url \
    -H "Umbraco-Cloud-Api-Key: $apiKey" \
    -H "Content-Type: application/json" \
    -d "{\"commitMessage\":\"$commitMessage\"}")
  echo "$response"
  # extract status and deploymentId for validation and later use
  status=$(echo "$response" | jq -r '.deploymentState')
  deployment_id=$(echo "$response" | jq -r '.deploymentId')
  if [[ $status != "Created" ]]; then
    echo "Unexpected status: $status"
    exit 1
  fi
  echo "$deployment_id"
}

call_api

echo "Deployment created successfully -> $deployment_id"

# store deploymentId for later stages
echo "DEPLOYMENT_ID=$deployment_id" >> "$GITHUB_OUTPUT"