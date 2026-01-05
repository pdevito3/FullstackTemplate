# Keycloak Configuration Management

This directory contains templatized Keycloak configuration for managing realm settings across multiple environments.

## Overview

The configuration uses [keycloak-config-cli](https://github.com/adorsys/keycloak-config-cli) to apply realm configuration idempotently. This allows:

- **Version-controlled configuration**: All Keycloak settings are stored in Git
- **Environment-specific values**: URLs and secrets are injected via environment variables
- **Idempotent application**: Safe to run on every deployment
- **Incremental updates**: Changes are applied without full realm reimport

## Files

| File | Purpose |
|------|---------|
| `realm.json` | Main realm configuration (clients, roles, scopes) |
| `users-nonprod.json` | Test users - **only import in non-production environments** |

## Environment Variables

The configuration templates use the following environment variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `KC_CLIENT_SECRET` | OAuth2 client secret | `super-secret-change-me` |
| `KC_BFF_URL` | BFF application URL (for redirect URIs) | `https://bff--myproject.us-central.northflank.app` |

## Usage

### Local Development (Aspire)

Local development continues to use `FullstackTemplate.AppHost/keycloak/realm-export.json` with Keycloak's built-in `--import-realm` flag. This is simpler for local dev as URLs are static.

### Deployed Environments (Northflank)

The `northflank.json` includes a Job that runs keycloak-config-cli after Keycloak starts:

1. **Keycloak starts** with admin credentials but no realm configured
2. **keycloak-config job runs** and applies `realm.json`
3. **For non-prod**: Also applies `users-nonprod.json` to seed test users

### Manual Application

To apply configuration manually (useful for testing):

```bash
# Pull the keycloak-config-cli image
docker pull adorsys/keycloak-config-cli:latest-26.0.0

# Apply configuration
docker run --rm \
  -e KEYCLOAK_URL=http://localhost:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=admin \
  -e KC_CLIENT_SECRET=super-secret-client-secret-change-in-production \
  -e KC_BFF_URL=http://localhost:7130 \
  -v $(pwd)/infra/keycloak:/config:ro \
  adorsys/keycloak-config-cli:latest-26.0.0 \
  --import-files-locations=/config/realm.json
```

For non-prod with test users:
```bash
docker run --rm \
  -e KEYCLOAK_URL=http://localhost:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=admin \
  -e KC_CLIENT_SECRET=super-secret-client-secret-change-in-production \
  -e KC_BFF_URL=http://localhost:7130 \
  -v $(pwd)/infra/keycloak:/config:ro \
  adorsys/keycloak-config-cli:latest-26.0.0 \
  --import-files-locations=/config/realm.json,/config/users-nonprod.json
```

## Environment Setup

### UAT Environment

```bash
KC_CLIENT_SECRET=<generate-unique-secret>
KC_BFF_URL=https://bff--myproject-uat.us-central.northflank.app
# Include users-nonprod.json
```

### Production Environment

```bash
KC_CLIENT_SECRET=<generate-unique-secret>
KC_BFF_URL=https://bff--myproject.us-central.northflank.app
# Do NOT include users-nonprod.json
```

## Making Configuration Changes

1. **Edit the template files** in this directory
2. **Test locally** using the manual application commands above
3. **Commit and push** - Northflank will re-run the config job on next deployment

For client-scoped changes (redirect URIs, etc.), only `realm.json` needs updating.

## Troubleshooting

### Configuration not applying

1. Check the keycloak-config job logs in Northflank
2. Verify Keycloak is healthy before the job runs
3. Ensure environment variables are set correctly

### Users not created

- Verify `users-nonprod.json` is included in `--import-files-locations`
- Check that the realm exists first (realm.json must be applied first)

### Secrets/URLs wrong

- Environment variable substitution uses `$(env:VAR_NAME)` syntax
- Verify the variable is set in Northflank's job configuration
