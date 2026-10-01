# Clerk authentication for ALLINONE

ALLINONE is a native WPF Windows app. Its Clerk integration uses a browser-based OAuth 2.0 Authorization Code flow with PKCE, then returns to the running app through a localhost callback.

## Clerk setup

1. Create a Clerk application.
2. In Clerk, enable the Native API for the application.
3. Enable the Google and Apple social connections you want available to users.
4. Create an OAuth application for ALLINONE.
5. Make the OAuth client **Public** and require PKCE.
6. Add this redirect URI to the Clerk OAuth application:

`http://127.0.0.1:54321/oauth/callback`

7. Give the OAuth application the scopes `openid`, `profile`, `email`, and `offline_access`.

## Local configuration

Set these environment variables on the Windows machine before launching ALLINONE:

`ALLINONE_CLERK_FRONTEND_API_URL` = your Clerk Frontend API URL

`ALLINONE_CLERK_CLIENT_ID` = the public Client ID of the ALLINONE OAuth application

Optional:

`ALLINONE_CLERK_AUTHORIZE_URL` = Clerk OAuth authorize endpoint

`ALLINONE_CLERK_TOKEN_URL` = Clerk OAuth token endpoint

`ALLINONE_CLERK_REDIRECT_URI` = `http://127.0.0.1:54321/oauth/callback`

The app does not require or embed a Clerk client secret. The Windows app is a public OAuth client.

## Security

The authorization code is protected with PKCE and a random state value. Session token data is encrypted with Windows DPAPI for the current Windows user instead of being stored as plain text in settings.json.

Never commit Clerk secrets to GitHub.

## Current flow

ALLINONE → system browser → Clerk → Google/Apple or another enabled Clerk sign-in method → localhost callback → ALLINONE → encrypted local session.