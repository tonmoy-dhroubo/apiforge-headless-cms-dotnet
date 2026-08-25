# ApiForge Headless CMS (.NET 10)

This is the .NET port of `apiforge-headless-cms-spring`. It keeps the Spring UI-facing HTTP contract: the same paths, methods, JSON property names, `{success,message,data,error}` envelope, JWT access/refresh semantics, BCrypt passwords, exact-match `AND` content search, multipart `files` uploads, and public media-file download route.

## Architecture

The port is a single ASP.NET Core host that replaces the Spring Cloud Gateway plus six co-located services. `ApiForge.Core` contains contracts and store abstractions, `ApiForge.Infrastructure` contains security and storage adapters, and `ApiForge.Api` contains HTTP composition. This keeps deployment simple while retaining clear domain/infrastructure boundaries; the route surface is intentionally unchanged for the existing UI.

The default development provider is deterministic in-memory storage. `db/00_ddl.sql` is the PostgreSQL-compatible schema matching the source project and is mounted by Compose; `Storage:Provider=Postgres` enables the PostgreSQL content-type and dynamic-content adapters without changing the API contract. User, media, and permission metadata remain process-local in this initial port and must be moved to their corresponding PostgreSQL repositories before multi-instance production deployment.

## Run and verify

Requires the .NET 10 SDK (the source host may only have runtimes). From this directory:

```powershell
dotnet restore ApiForge.HeadlessCms.sln
dotnet test ApiForge.HeadlessCms.sln --configuration Release
dotnet run --project src/ApiForge.Api --urls http://localhost:7080
```

`docker compose up --build` runs the app on port 7080 with PostgreSQL. Configure `Jwt:Secret`, `Jwt:RefreshSecret`, and a production storage adapter through environment variables or user secrets; never commit secrets.

## Contract inventory

Auth: register, login, validate, refresh, Google redirect, user list/get/roles/delete.

Content types: create/list/get by numeric ID/get by `apiId`/update/delete. Field types remain `SHORT_TEXT`, `LONG_TEXT`, `RICH_TEXT`, `NUMBER`, `BOOLEAN`, `DATETIME`, `MEDIA`, and `RELATION`.

Dynamic content: create/list/exact-match `POST /search`/get/update/delete under `/api/content/{apiId}`.

Media: multipart `files` upload, list, metadata get/delete, and public download.

Permissions: API/content permission CRUD, filtering by content type, and role-intersection checks.

## Security and compatibility notes

As in the source deployment, auth register/login/validate and media file downloads are public; all other `/api/**` endpoints require `Authorization: Bearer <access-token>`. Every error is normalized to the source envelope. OAuth Google is kept as the same redirect contract; provider credentials and callback hosting are deployment concerns.

See `docs/PARITY.md` for the source-to-port evidence matrix and known operational differences.
