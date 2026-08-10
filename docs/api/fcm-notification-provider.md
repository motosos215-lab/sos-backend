# FCM Notification Provider

FCM Push Notification Provider enables real push delivery for prepared notification attempts when the provider is explicitly enabled. It is disabled by default and falls back to the simulated notification provider while disabled.

## Configuration

Use environment variables only. Do not store service account JSON, Base64 values, private keys, certificates, server keys, refresh tokens or real push tokens in source control, appsettings, logs, audit metadata or API responses.

Supported variables:

- `Notifications__Providers__Fcm__Enabled`
- `Notifications__Providers__Fcm__ProjectId`
- `Notifications__Providers__Fcm__ServiceAccountJson`
- `Notifications__Providers__Fcm__ServiceAccountJsonBase64`
- `Notifications__Providers__Fcm__ServiceAccountFilePath`
- `Notifications__Providers__Fcm__DefaultTitle`
- `Notifications__Providers__Fcm__DefaultTtlSeconds`

Credential priority:

1. `Notifications__Providers__Fcm__ServiceAccountJson`
2. `Notifications__Providers__Fcm__ServiceAccountJsonBase64`
3. `Notifications__Providers__Fcm__ServiceAccountFilePath`

If multiple credential sources are configured, the provider uses the priority above and reports a safe warning in provider status. The credential value is never returned.

## DigitalOcean App Platform

DigitalOcean App Platform can reject or mis-handle raw JSON service account values because of JSON characters in environment variable forms.

Recommended option for DigitalOcean:

- `Notifications__Providers__Fcm__ServiceAccountJsonBase64`

The value must be the UTF-8 Firebase service account JSON encoded as Base64. The API decodes it only in memory before initializing FirebaseAdmin. The decoded JSON and the Base64 value are never logged, audited or returned by API responses.

## Provider Status

Admins can inspect safe provider status with:

- `GET /api/v1/admin/notifications/providers/status`

The response only includes safe status fields such as `fcmCredentialSource`:

- `environment_json`
- `environment_json_base64`
- `environment_file_path`
- `none`

The response does not include credential values, decoded JSON, Base64 content, private keys or full sensitive file contents.
