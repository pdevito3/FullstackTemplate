# Dockerfile for Keycloak configuration using keycloak-config-cli
# This packages the realm configuration files with the keycloak-config-cli tool

FROM adorsys/keycloak-config-cli:latest-26.0.0

# Copy realm configuration files
COPY realm.json /config/realm.json
COPY users-nonprod.json /config/users-nonprod.json
COPY entrypoint.sh /entrypoint.sh

# Default environment variables (override at runtime)
ENV KEYCLOAK_URL=http://keycloak:8080
ENV IMPORT_MANAGED=full
ENV IMPORT_VAR_SUBSTITUTION_ENABLED=true

# For production: only import realm.json
# For non-prod: import both realm.json and users-nonprod.json
# Set IMPORT_FILES_LOCATIONS at runtime to control this
ENV IMPORT_FILES_LOCATIONS=/config/realm.json

# Set to "true" to include test users (non-prod environments)
ENV KC_INCLUDE_TEST_USERS=false

USER root
RUN chmod +x /entrypoint.sh
USER 1000

ENTRYPOINT ["/entrypoint.sh"]
