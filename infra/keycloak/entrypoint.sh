#!/bin/sh
set -e

echo "=== Keycloak Config CLI ==="
echo "Keycloak URL: $KEYCLOAK_URL"
echo "Include test users: $KC_INCLUDE_TEST_USERS"

# Build the import files list based on environment
if [ "$KC_INCLUDE_TEST_USERS" = "true" ]; then
    echo "Including test users (non-production mode)"
    export IMPORT_FILES_LOCATIONS="/config/realm.json,/config/users-nonprod.json"
else
    echo "Production mode - no test users"
    export IMPORT_FILES_LOCATIONS="/config/realm.json"
fi

echo "Import files: $IMPORT_FILES_LOCATIONS"

# Wait for Keycloak to be ready
echo "Waiting for Keycloak to be ready..."
MAX_RETRIES=60
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if wget -q --spider "$KEYCLOAK_URL/health/ready" 2>/dev/null; then
        echo "Keycloak is ready!"
        break
    fi

    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo "Waiting for Keycloak... attempt $RETRY_COUNT/$MAX_RETRIES"
    sleep 5
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo "ERROR: Keycloak did not become ready in time"
    exit 1
fi

# Run the keycloak-config-cli
echo "Applying Keycloak configuration..."
java $JAVA_OPTS -jar /opt/keycloak-config-cli.jar \
    --keycloak.url="$KEYCLOAK_URL" \
    --keycloak.user="$KEYCLOAK_ADMIN" \
    --keycloak.password="$KEYCLOAK_ADMIN_PASSWORD" \
    --import.managed="$IMPORT_MANAGED" \
    --import.var-substitution.enabled="$IMPORT_VAR_SUBSTITUTION_ENABLED" \
    --import.files.locations="$IMPORT_FILES_LOCATIONS"

CONFIG_EXIT_CODE=$?

if [ $CONFIG_EXIT_CODE -eq 0 ]; then
    echo "=== Configuration applied successfully ==="
else
    echo "=== Configuration failed with exit code $CONFIG_EXIT_CODE ==="
    exit $CONFIG_EXIT_CODE
fi

# Keep container running (for Northflank service model)
# Scale this service to 0 after initial configuration
echo "Configuration complete. Container will remain running."
echo "Scale this service to 0 instances to save resources."
sleep infinity
