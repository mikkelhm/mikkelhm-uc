#!/bin/bash

# Set variables
projectId="$1"
deploymentId="$2"
url="https://api.cloud.umbraco.com/v1/projects/$projectId/deployments/$deploymentId/package"
apiKey="$3"
file="$4"

function call_api {
  response=$(curl --insecure -s -X POST $url \
    -H "Umbraco-Cloud-Api-Key: $apiKey" \
    -H "Content-Type: multipart/form-data" \
    --form "file=@$file")

  echo "$response"
}

call_api