# Spring-to-.NET parity evidence

| Source contract | .NET route(s) | Status |
|---|---|---|
| `ApiResponse` envelope | all JSON endpoints | implemented; camelCase names and null fields retained |
| Auth JWT + refresh | `/api/auth/register`, `login`, `validate`, `refresh` | implemented with HS256, 1h access and 7d refresh defaults |
| User administration | `/api/auth/users*` | implemented |
| Content type metadata | `/api/content-types*` | implemented, including all eight field enum values, plural default, and PostgreSQL adapter |
| Dynamic content | `/api/content/{apiId}` and `/{id}` | implemented; exact `AND` search, timestamps, and PostgreSQL dynamic tables |
| Media | `/api/upload*` | implemented; `files` multipart field, KB size, hash filename, public download |
| Permissions | `/api/permissions/{api,content}*` | implemented; role intersection checks |
| Gateway auth behavior | middleware | implemented as one-host equivalent |

## Verification

`ApiForge.UnitTests` covers plural defaults, content search semantics, and BCrypt. `ApiForge.ApiTests` uses `WebApplicationFactory` to verify public registration, JWT-bearing response shape, and 401 envelope behavior on a protected route. The Spring Postman collection can be replayed against port 7080 after registering/logging in and setting its bearer token.

## Deliberate host architecture difference

The Spring source runs separate service processes on ports 7080-7085. The .NET port uses one ASP.NET Core process at 7080 with equivalent paths because the UI only consumes the gateway contract. This is a deployment topology change, not an API contract change. PostgreSQL content metadata and dynamic rows are supported when `Storage:Provider=Postgres`; auth, media metadata, and permissions still use the process-local adapters and are a remaining production-hardening item.
