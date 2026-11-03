#!/bin/sh
# Creates the bucket of the server in Floci. A normal run keeps the data volume, so the bucket can exist already.
set -eu

if aws s3api head-bucket --bucket "$STORAGE_BUCKET" > /dev/null 2>&1; then
  exit 0
fi

aws s3api create-bucket --bucket "$STORAGE_BUCKET" > /dev/null
