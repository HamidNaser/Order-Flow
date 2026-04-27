#!/bin/bash

echo "runner_ip=$(curl -s https://api.ipify.org)" >> "$GITHUB_OUTPUT"

# Retrieve and mask AWS Secrets
export MONGODB_ATLAS_PUBLIC_API_KEY=$(aws secretsmanager get-secret-value --secret-id "common/mongodb-atlas-public-api-key" | jq -r .SecretString)
export MONGODB_ATLAS_PRIVATE_API_KEY=$(aws secretsmanager get-secret-value --secret-id "common/mongodb-atlas-private-api-key" | jq -r .SecretString)
export MONGO_ADMIN_PASSWORD=$(aws secretsmanager get-secret-value --secret-id "order-api/mongodb-orders-admin-password" | jq -r .SecretString)

echo "::add-mask::$MONGODB_ATLAS_PUBLIC_API_KEY"
echo "::add-mask::$MONGODB_ATLAS_PRIVATE_API_KEY"
echo "::add-mask::$MONGO_ADMIN_PASSWORD"


# Retrieve MongoDB Atlas Project ID and SRV URL based on environment
export MONGODB_ATLAS_PROJECT_ID=$(atlas projects list -o json | jq -r ".results | map(select(.name == \"$MONGO_DB_PROJECT_NAME\")) | .[0].id")
export MONGODB_ATLAS_SRV_URL=$(atlas clusters describe orders-cluster -o json | jq -r '.connectionStrings.standardSrv')


# Set atlas cli environment variables
echo "MONGODB_ATLAS_PUBLIC_API_KEY=$MONGODB_ATLAS_PUBLIC_API_KEY" >> $GITHUB_ENV
echo "MONGODB_ATLAS_PRIVATE_API_KEY=$MONGODB_ATLAS_PRIVATE_API_KEY" >> $GITHUB_ENV
echo "MONGODB_ATLAS_PROJECT_ID=$MONGODB_ATLAS_PROJECT_ID" >> $GITHUB_ENV


# Set outputs for mongosh connection
echo "mongodb_password=$MONGO_ADMIN_PASSWORD" >> $GITHUB_OUTPUT
echo "mongodb_atlas_srv_url=$MONGODB_ATLAS_SRV_URL" >> $GITHUB_OUTPUT
