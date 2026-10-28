#!/bin/sh
# Seeds the Cognito user pool, the app client, the groups, and the test users in Floci.
# The pool ID, the client ID, and the client secret are pinned, so the AppHost knows them before Floci starts.
# The callback URLs come from the APP_* variables, because an isolated run gives the apps random ports.
set -eu

POOL_ID="us-east-1_aspire"
CLIENT_NAME="aspire-app"
CLIENT_SECRET="super-secret-client-secret-change-in-production"
PASSWORD="password123!"

aws cognito-idp create-user-pool \
  --pool-name aspire \
  --username-attributes email \
  --auto-verified-attributes email \
  --user-pool-tags "floci:override-id=$POOL_ID,floci:override-cognito-client-id=use-name,floci:override-cognito-client-secret=$CLIENT_SECRET" \
  > /dev/null

aws cognito-idp create-user-pool-client \
  --user-pool-id "$POOL_ID" \
  --client-name "$CLIENT_NAME" \
  --generate-secret \
  --supported-identity-providers COGNITO \
  --allowed-o-auth-flows-user-pool-client \
  --allowed-o-auth-flows code \
  --allowed-o-auth-scopes openid profile email \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --enable-token-revocation \
  --callback-urls \
    "$APP_FRONTEND_URL/signin-oidc" \
    "$APP_BFF_URL/signin-oidc" \
    "$APP_SERVER_URL/swagger/oauth2-redirect.html" \
    "$APP_SERVER_HTTP_URL/swagger/oauth2-redirect.html" \
  --logout-urls \
    "$APP_FRONTEND_URL" \
    "$APP_FRONTEND_URL/signout-callback-oidc" \
    "$APP_BFF_URL/signout-callback-oidc" \
  > /dev/null

aws cognito-idp create-group --user-pool-id "$POOL_ID" --group-name admin > /dev/null
aws cognito-idp create-group --user-pool-id "$POOL_ID" --group-name user > /dev/null

create_user() {
  email="$1"
  shift
  aws cognito-idp admin-create-user \
    --user-pool-id "$POOL_ID" \
    --username "$email" \
    --user-attributes Name=email,Value="$email" Name=email_verified,Value=true \
    --message-action SUPPRESS \
    > /dev/null
  aws cognito-idp admin-set-user-password \
    --user-pool-id "$POOL_ID" \
    --username "$email" \
    --password "$PASSWORD" \
    --permanent
  for group in "$@"; do
    aws cognito-idp admin-add-user-to-group --user-pool-id "$POOL_ID" --username "$email" --group-name "$group"
  done
}

create_user admin@example.com admin user
create_user user@example.com user
