# task-06_refresh_token_with_rotation

This exercise adds a refresh token table supporting rotation and reuse detection.

The refresh token model includes:
- `TokenHash` (stored hashed, never the raw token)
- `UserId`
- `ExpiresAt`
- `RevokedAt`
- `ReplacedByToken`

The project currently contains the domain model and database schema for the refresh token table.
