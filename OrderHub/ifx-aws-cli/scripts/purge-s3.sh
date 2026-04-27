#!/bin/bash
set -euo pipefail

if [ -z "${BUCKET_PREFIX:-}" ]; then
    echo "ERROR: BUCKET_PREFIX environment variable is not set"
    exit 1
fi

BUCKETS=(
"${BUCKET_PREFIX}-us-east-1-orders"
"${BUCKET_PREFIX}-us-west-2-orders"
)
BATCH_SIZE=800

# Function to delete objects in batches
delete_in_batches() {
    local bucket=$1
    local objects_json=$2
    local total=$(echo "$objects_json" | jq 'length')

    if [ "$total" -eq 0 ]; then
        echo "No objects to delete"
        return
    fi

    echo "Found $total objects to delete"
    local batch_count=$(( (total + BATCH_SIZE - 1) / BATCH_SIZE ))

    for i in $(seq 0 $((batch_count - 1))); do
        local offset=$((i * BATCH_SIZE))
        local batch=$(echo "$objects_json" | jq -c ".[$offset:$offset+$BATCH_SIZE]")

        if [ "$(echo "$batch" | jq 'length')" -gt 0 ]; then
            echo "Deleting batch $((i + 1))/$batch_count ($(echo "$batch" | jq 'length') objects)..."
            aws s3api delete-objects \
                --bucket "$bucket" \
                --delete "{\"Objects\":$batch,\"Quiet\":true}" \
                --output json > /dev/null
        fi
    done
}

# Process each bucket
for BUCKET in "${BUCKETS[@]}"; do
    echo "========================================"
    echo "Starting S3 bucket purge for: $BUCKET"
    echo "========================================"

    # 1. List and delete all object versions
    echo "Processing object versions..."
    VERSIONS=$(aws s3api list-object-versions \
        --bucket "$BUCKET" \
        --output json \
        --query 'Versions[*].{Key:Key,VersionId:VersionId}')

    delete_in_batches "$BUCKET" "$VERSIONS"

    # 2. List and delete all delete markers
    echo "Processing delete markers..."
    DELETE_MARKERS=$(aws s3api list-object-versions \
        --bucket "$BUCKET" \
        --output json \
        --query 'DeleteMarkers[*].{Key:Key,VersionId:VersionId}')

    delete_in_batches "$BUCKET" "$DELETE_MARKERS"

    echo "Completed purge for: $BUCKET"
    echo ""
done

echo "All S3 buckets purged successfully"
