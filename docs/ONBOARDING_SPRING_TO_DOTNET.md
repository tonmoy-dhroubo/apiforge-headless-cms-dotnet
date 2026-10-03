# Developer Onboarding: From Spring Boot to .NET 10
## Guide to the ApiForge Headless CMS Codebase

Welcome to the **ApiForge Headless CMS** codebase! If your background is in Java, Spring Boot, Spring Security, and Spring Cloud, you are in the right place. 

This repository is a high-performance **.NET 10** implementation of a headless CMS that was ported directly from a Spring Boot microservice architecture (`apiforge-headless-cms-spring`). It maintains **100% contract parity** with the Spring version—preserving the exact same REST routes, JSON payloads, error envelopes, JWT claims, and database schema—while leveraging the modern speed, memory efficiency, and type safety of .NET 10 and C# 13.

This guide provides a comprehensive map from Spring concepts to their .NET counterparts and walks you through every layer of the application.

---

## Table of Contents
1. [The Big Picture: Spring Architecture vs. .NET Architecture](#1-the-big-picture-spring-architecture-vs-net-architecture)
2. [Rosetta Stone: Spring vs. .NET Concepts](#2-rosetta-stone-spring-vs-net-concepts)
3. [Language Modernization: Java 17/21 vs. C# 13](#3-language-modernization-java-1721-vs-c-13)
4. [Solution & Project Layout](#4-solution--project-layout)
5. [Dependency Injection & App Startup (Program.cs)](#5-dependency-injection--app-startup-programcs)
6. [API Layer & Controllers](#6-api-layer--controllers)
7. [Security & Authentication (Spring Security vs. ASP.NET Core)](#7-security--authentication-spring-security-vs-aspnet-core)
8. [Data Persistence: Dynamic CMS Architecture](#8-data-persistence-dynamic-cms-architecture)
9. [Error Handling & The ApiResponse Envelope](#9-error-handling--the-apiresponse-envelope)
10. [Testing: JUnit/MockMvc vs. xUnit/WebApplicationFactory](#10-testing-junitmockmvc-vs-xunitwebapplicationfactory)
11. [Configuration & Environment Profiles](#11-configuration--environment-profiles)
12. [CLI & Development Workflow](#12-cli--development-workflow)
13. [Step-by-Step: Adding a New Feature](#13-step-by-step-adding-a-new-feature)
14. [Top 10 Gotchas for Spring Developers](#14-top-10-gotchas-for-spring-developers)

---

## 1. The Big Picture: Spring Architecture vs. .NET Architecture

### The Spring Predecessor:
In the Java/Spring implementation, the system was distributed across 6 separate microservices fronted by a Spring Cloud Gateway:
* `Spring Cloud Gateway` on port **7080** (routing and perimeter auth)
* `Auth & User Service` on port **7081**
* `Content Type Service` on port **7082**
* `Content Service` on port **7083**
* `Media Service` on port **7084**
* `Permission Service` on port **7085**

Each microservice ran in its own JVM process, incurring multiple JVM memory overheads and network latency between the gateway and internal services.

### The .NET 10 Implementation:
In this .NET port, the architecture has been consolidated into a **Modular Monolith** hosted in a single ASP.NET Core process running on port **7080**:
* **Zero UI Changes**: The frontend/UI only ever spoke to the Gateway on port 7080. Because this single .NET application handles all `/api/**` routes identically, clients see zero difference.
* **Radical Resource Efficiency**: Instead of 6 JVMs eating gigabytes of RAM, a single lightweight .NET process handles the entire workload with sub-millisecond route dispatching in-process.
* **Clean Architecture**: Domain contracts (`ApiForge.Core`), infrastructure adapters (`ApiForge.Infrastructure`), and HTTP composition (`ApiForge.Api`) remain strictly separated.

---

## 2. Rosetta Stone: Spring vs. .NET Concepts

| Spring Boot / Java Ecosystem | .NET 10 / C# Ecosystem | Notes in ApiForge |
|---|---|---|
| `pom.xml` / `build.gradle` | `.csproj` (C# Project File) | SDK-style, XML-based dependency & compilation configuration. |
| Maven Parent POM / Multi-module root | `.sln` (Solution) + `Directory.Build.props` | Groups projects; `Directory.Build.props` sets global settings (e.g. `<TargetFramework>net10.0</TargetFramework>`). |
| Maven Central / JCenter | NuGet (`nuget.org`) | Standard package manager. |
| `java -jar app.jar` | `dotnet run` or `dotnet <Assembly>.dll` | CLI execution toolchain. |
| `@SpringBootApplication` & `main()` | `Program.cs` | Minimal hosting model using top-level statements. |
| Spring IoC Container (`ApplicationContext`) | `IServiceCollection` / `IServiceProvider` | Built-in native dependency injection container. |
| `@Component`, `@Service`, `@Repository` | `builder.Services.Add{Lifetime}<T>()` | Explicit registration in `Program.cs`. |
| `@Scope("singleton")` (Spring default) | `AddSingleton<TInterface, TImpl>()` | One instance shared across entire app lifetime. |
| `@Scope("request")` | `AddScoped<TInterface, TImpl>()` | One instance per HTTP request (the .NET default for services). |
| `@Scope("prototype")` | `AddTransient<TInterface, TImpl>()` | Fresh instance every time it is injected. |
| `@RestController` + `@RequestMapping` | `[ApiController]` + `[Route]` + `ControllerBase` | MVC API controller base class. |
| `@GetMapping`, `@PostMapping`, etc. | `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]` | Action HTTP verb attributes. |
| `@PathVariable("id") Long id` | `[HttpGet("{id:long}")]` + method param `long id` | Route parameter with built-in type constraint. |
| `@RequestBody MyDto dto` | `[FromBody] MyDto dto` | Deserializes JSON payload into a DTO or record. |
| `@RequestParam("files") MultipartFile` | `[FromForm(Name = "files")] IFormFile` | Multipart form data file upload. |
| `ResponseEntity<T>` | `ActionResult<T>` / `IActionResult` | Controller return type with HTTP status code and body. |
| Jackson (`ObjectMapper`) | `System.Text.Json` (`JsonSerializer`) | High-performance, zero-allocation native JSON serializer. |
| Jackson `JsonNode` | `JsonElement` / `JsonDocument` | Structural, DOM-style read-only JSON representation. |
| `@ControllerAdvice` + `@ExceptionHandler` | ASP.NET Core Middleware (`ExceptionHandlingMiddleware`) | Global pipeline interception for catching exceptions and returning uniform envelopes. |
| Spring Security `SecurityFilterChain` | `app.UseAuthentication()` & `app.UseAuthorization()` | Built-in ASP.NET Core auth middleware pipeline. |
| `@PreAuthorize("isAuthenticated()")` | `[Authorize]` attribute | Protects controller actions. |
| `permitAll()` | `[AllowAnonymous]` attribute | Allows unauthenticated access. |
| `UserDetails` / `Principal` | `ClaimsPrincipal` + `Claim` | Identity claims extracted from JWT. |
| Spring Data JPA / `JdbcTemplate` | ADO.NET (`NpgsqlConnection`, `NpgsqlCommand`) | High-performance direct SQL driver for PostgreSQL. |
| `application.properties` / `application.yml` | `appsettings.json` + `appsettings.Development.json` | JSON-based configuration hierarchy. |
| JUnit 5 + MockMvc / TestRestTemplate | xUnit + `WebApplicationFactory<Program>` | In-memory integration testing server. |

---

## 3. Language Modernization: Java 17/21 vs. C# 13

Modern C# looks familiar to any Java developer, but has several syntactical superpowers used extensively in this codebase:

### Primary Constructors (C# 12+)
In Spring, you often use Lombok's `@RequiredArgsConstructor` or explicit constructor boilerplate to inject dependencies. C# supports primary constructors directly on the class declaration:

```csharp
// C# in ApiForge.Api:
public class AuthService(IUserStore users, JwtTokenService jwt) : IAuthService
{
    // users and jwt are in scope throughout the entire class body!
}
```

Equivalent Java/Lombok:
```java
@Service
@RequiredArgsConstructor
public class AuthService implements IAuthService {
    private final UserStore users;
    private final JwtTokenService jwt;
}
```

### Records with Positional Syntax
Both Java and C# have records. In C#, records support non-destructive mutation via `with` expressions:

```csharp
// Defined in ApiForge.Core/Contracts.cs:
public sealed record ContentTypeDto(
    long? Id, 
    string Name, 
    string? PluralName, 
    string ApiId, 
    string? Description, 
    IReadOnlyList<FieldDto>? Fields, 
    DateTime? CreatedAt, 
    DateTime? UpdatedAt
);

// Non-destructive copy with modified property:
var updated = old with { Name = dto.Name, UpdatedAt = DateTime.UtcNow };
```

### Async / Await and Tasks
Java traditionally relies on multi-threading per request or reactive programming (Project Reactor `Mono`/`Flux`). C# has built-in `async` and `await`:
* `Task` is equivalent to Java's `CompletableFuture<Void>`.
* `Task<T>` is equivalent to Java's `CompletableFuture<T>` or `Mono<T>`.
* Methods returning tasks end with `Async` by convention (e.g. `CreateAsync()`).
* `CancellationToken ct = default` is passed through async methods to support cooperative request cancellation if the client disconnects.

```csharp
public async Task<UserRecord?> Find(string identifier, CancellationToken ct = default)
{
    await using var conn = Open();
    await using var cmd = new NpgsqlCommand("...", conn);
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    return await reader.ReadAsync(ct) ? await Read(reader) : null;
}
```

### Nullable Reference Types (`string?` vs `string`)
In this repository, `<Nullable>enable</Nullable>` is enforced. 
* `string` cannot be null (the compiler emits a warning/error if you assign null).
* `string?` explicitly marks that the value may be null.
* This eliminates the need for Java's `Optional<T>` in most domain transfer objects.

### Collection Expressions & Pattern Matching
* Collection literals: `[]` instead of `Collections.emptyList()`, `List.of()`, or `new ArrayList<>()`.
* Switch expressions:
```csharp
var type = f.Type switch {
    FieldType.SHORT_TEXT => "VARCHAR(255)",
    FieldType.LONG_TEXT or FieldType.RICH_TEXT => "TEXT",
    FieldType.NUMBER => "NUMERIC",
    FieldType.BOOLEAN => "BOOLEAN",
    FieldType.DATETIME => "TIMESTAMP",
    FieldType.MEDIA or FieldType.RELATION => "BIGINT",
    _ => "TEXT"
};
```

---

## 4. Solution & Project Layout

The solution is divided into three clean layers plus two test suites:

```text
apiforge-headless-cms-dotnet/
├── ApiForge.HeadlessCms.sln        # Solution file (like a Maven Aggregator / root POM)
├── Directory.Build.props           # Common compilation settings (Net 10, Nullable, etc.)
├── Dockerfile                      # Multi-stage container build
├── docker-compose.yml              # Local PostgreSQL 16 + App orchestration
├── db/
│   ├── 00_ddl.sql                  # PostgreSQL table definitions
│   └── 01_seed_auth.sql            # Initial roles, permissions, and admin user
├── src/
│   ├── ApiForge.Core/              # Domain & Contracts layer (Zero external dependencies)
│   ├── ApiForge.Infrastructure/    # Data stores, JWT, and cryptography
│   └── ApiForge.Api/               # ASP.NET Core Web Host, Controllers & Middleware
└── tests/
    ├── ApiForge.UnitTests/         # Fast isolated unit tests (xUnit)
    └── ApiForge.ApiTests/          # Full in-memory integration tests (WebApplicationFactory)
```

### Layer Dependency Rules:
1. **`ApiForge.Core`**: Has **NO** dependencies on external database libraries or web frameworks. It contains records, DTOs, enums, and pure interfaces (`IUserStore`, `IContentStore`, `IAuthService`).
2. **`ApiForge.Infrastructure`**: Depends on `ApiForge.Core`. Implements storage engines using Npgsql (PostgreSQL) and ConcurrentDictionary (In-Memory). Implements JWT and BCrypt token/security services.
3. **`ApiForge.Api`**: Depends on `ApiForge.Core` and `ApiForge.Infrastructure`. Hosts the HTTP API, defines controllers, sets up DI, and configures the middleware pipeline.

---

## 5. Dependency Injection & App Startup (Program.cs)

In Spring Boot, startup involves `@SpringBootApplication`, component scanning, and `@Configuration` classes. In modern .NET, everything is configured explicitly in `src/ApiForge.Api/Program.cs`:

### Step 1: Create the Web Builder
```csharp
var builder = WebApplication.CreateBuilder(args);
```
This loads configuration (`appsettings.json`, environment variables, user secrets) and prepares the DI container (`builder.Services`).

### Step 2: Register Storage Providers (Pluggable Architecture)
The app inspects `Storage:Provider` in the configuration. If `"Postgres"`, it registers the PostgreSQL stores; otherwise, it registers thread-safe in-memory stores:

```csharp
var isPostgres = builder.Configuration["Storage:Provider"]?.Equals("Postgres", StringComparison.OrdinalIgnoreCase) == true;
if (isPostgres)
{
    builder.Services.AddSingleton<IContentTypeStore, PostgresContentTypeStore>();
    builder.Services.AddSingleton<IContentStore, PostgresContentStore>();
    builder.Services.AddSingleton<IUserStore, PostgresUserStore>();
    builder.Services.AddSingleton<IPermissionStore, PostgresPermissionStore>();
    builder.Services.AddSingleton<IMediaStore, PostgresMediaStore>();
}
else
{
    builder.Services.AddSingleton<InMemoryContentTypeStore>();
    builder.Services.AddSingleton<IContentTypeStore>(sp => sp.GetRequiredService<InMemoryContentTypeStore>());
    builder.Services.AddSingleton<IContentStore, InMemoryContentStore>();
    builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();
    builder.Services.AddSingleton<IPermissionStore, InMemoryPermissionStore>();
    builder.Services.AddSingleton<IMediaStore, MediaStore>();
}
```
> **Spring Comparison**: In Spring, you might use `@ConditionalOnProperty(name = "storage.provider", havingValue = "postgres")` or `@Profile("postgres")`. In .NET, doing this in `Program.cs` is clean, transparent, and avoids reflection overhead.

### Step 3: Register Domain Services
```csharp
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IContentTypeService, ContentTypeService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
```

### Step 4: Configure the Middleware Pipeline
In ASP.NET Core, the order of middleware in `app.Use...` defines the HTTP execution pipeline:

```csharp
var app = builder.Build();

// 1. Catches all downstream exceptions and serializes them into ApiResponse.Fail
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Cross-Origin Resource Sharing
app.UseCors("AllowAll");

// 3. OpenAPI Documentation
app.UseSwagger();
app.UseSwaggerUI();

// 4. Decodes and verifies the Authorization Bearer JWT
app.UseAuthentication();

// 5. Enforces [Authorize] attributes on controllers
app.UseAuthorization();

// 6. Routes requests to Controller endpoints
app.MapControllers();

app.Run();
```

---

## 6. API Layer & Controllers

Controllers inherit from `ControllerBase` and use attributes that directly correspond to Spring MVC:

### Example: ContentController
```csharp
[ApiController]
[Route("api/content")]
[Authorize]
public class ContentController(IContentService service) : ControllerBase
{
    // POST /api/content/{apiId}
    [HttpPost("{apiId}")]
    public async Task<ActionResult<ApiResponse<IDictionary<string, object?>>>> Create(
        string apiId, 
        [FromBody] JsonElement body, 
        CancellationToken ct)
    {
        var data = JsonHelper.ToDictionary(body);
        var result = await service.CreateAsync(apiId, data, ct);
        return Ok(ApiResponse<IDictionary<string, object?>>.Ok(result, "Content created successfully"));
    }

    // POST /api/content/{apiId}/search
    [HttpPost("{apiId}/search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IDictionary<string, object?>>>>> Search(
        string apiId, 
        [FromBody] JsonElement body, 
        CancellationToken ct)
    {
        var filters = JsonHelper.ToDictionary(body);
        var result = await service.SearchAsync(apiId, filters, ct);
        return Ok(ApiResponse<IReadOnlyList<IDictionary<string, object?>>>.Ok(result));
    }
}
```

### Key Differences from Spring MVC:
1. **`ActionResult<T>`**: You can return `Ok(data)` (which sets HTTP 200 and serializes `data`) or `NotFound(...)`, `BadRequest(...)`, etc. It provides type-safe OpenAPI/Swagger documentation while retaining HTTP response flexibility.
2. **`JsonElement` and `JsonHelper`**: In dynamic content endpoints (where users can define arbitrary fields like `title`, `author`, `views`), the CMS does not use static DTO classes. `JsonHelper.ToDictionary(body)` parses dynamic JSON into a casing-insensitive dictionary (`Dictionary<string, object?>`), perfectly matching Jackson's `Map<String, Object>`.
3. **CancellationToken**: The ASP.NET Core host supplies a `CancellationToken` linked to the client's HTTP connection. If the client cancels or closes their browser tab, the cancellation token signals downstream database queries to abort immediately!

---

## 7. Security & Authentication (Spring Security vs. ASP.NET Core)

### JWT Authentication Configuration:
In Spring Security, you would configure a `SecurityFilterChain` bean with a custom `JwtAuthenticationFilter` and `AuthenticationEntryPoint`. In ASP.NET Core, it is configured in `Program.cs`:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        // Custom 401 Challenge to maintain the ApiResponse envelope
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Unauthorized"));
        }
    };
});
```

### Password Hashing:
Like the Spring application, passwords are encrypted using **BCrypt** (`BCrypt.Net-Next`). Passwords hashed in Spring can be validated seamlessly in .NET and vice-versa.

### Role Intersection Permission Engine:
Spring applications often use SpEL expressions like `@PreAuthorize("hasRole('ADMIN')")`. In this CMS, permissions are dynamic and stored in the database:
* **API Permissions**: Controls access to specific HTTP methods (`GET`, `POST`, `PUT`, `DELETE`) on a content type.
* **Content Permissions**: Controls high-level actions (`CREATE`, `READ`, `UPDATE`, `DELETE`, `PUBLISH`).
* When checking permissions, `PermissionService` checks for **role intersection**:
```csharp
public async Task<bool> CheckContentPermissionAsync(PermissionCheck check, CancellationToken ct = default)
{
    var all = await store.ContentAll(ct);
    return all.Any(p =>
        p.ContentTypeApiId == check.ContentTypeApiId &&
        p.Action == check.Action &&
        (p.AllowedRoles ?? []).Intersect(check.UserRoles ?? []).Any());
}
```

---

## 8. Data Persistence: Dynamic CMS Architecture

### Why ADO.NET (`Npgsql`) Instead of Entity Framework Core (or JPA)?
As a Spring developer, you might ask: *Where is Hibernate / Spring Data JPA / Entity Framework Core?*

Notice how `PostgresStores.cs` uses `NpgsqlConnection` and `NpgsqlCommand` directly (similar to Spring's `JdbcTemplate`). 
**This is a deliberate architectural decision**:
1. **Dynamic Content Tables**: In a headless CMS, users define new content types on the fly (e.g. creating an `Article` content type with custom fields).
2. When a content type is created, the CMS dynamically executes:
   ```sql
   CREATE TABLE IF NOT EXISTS ct_article (
       id BIGSERIAL PRIMARY KEY,
       created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
       updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
       title VARCHAR(255) NOT NULL,
       views NUMERIC
   );
   ```
3. Object-Relational Mappers (ORMs) like EF Core or JPA assume entity types are fixed at compile time (`@Entity public class Article`). Trying to force runtime dynamic tables and arbitrary fields into an ORM results in fragile metadata hacks.
4. Using raw parameterized ADO.NET provides maximum performance, exact control over SQL generation, zero ORM overhead, and secure parameterization preventing SQL injection.

### Identifier Sanitization:
To prevent SQL injection when creating tables with dynamic content type names:
```csharp
private static string Safe(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        throw new ApiForgeException("Invalid identifier", 400);
    return value;
}
```

---

## 9. Error Handling & The ApiResponse Envelope

The Spring API returned every single response wrapped in an envelope:
```json
{
  "success": true,
  "message": "User registered successfully",
  "data": { ... },
  "error": null
}
```
Or on error:
```json
{
  "success": false,
  "message": null,
  "data": null,
  "error": "Content type not found"
}
```

### In C#:
In `ApiForge.Core/Contracts.cs`:
```csharp
public sealed record ApiResponse<T>(bool Success, string? Message, T? Data, string? Error)
{
    public static ApiResponse<T> Ok(T? data, string? message = null) => new(true, message, data, null);
    public static ApiResponse<T> Fail(string error) => new(false, null, default, error);
}
```

### Global Middleware (`ExceptionHandlingMiddleware.cs`):
Instead of Spring's `@RestControllerAdvice`, .NET uses a middleware component at the top of the HTTP pipeline:
```csharp
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiForgeException ex)
        {
            context.Response.StatusCode = ex.Status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            // Check for PostgreSQL unique constraint violation (SqlState 23505)
            var state = ex.GetType().GetProperty("SqlState")?.GetValue(ex)?.ToString();
            context.Response.StatusCode = state == "23505" ? 409 : 500;
            context.Response.ContentType = "application/json";
            var message = state == "23505" ? "Resource already exists" : $"Internal server error: {ex.Message}";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message));
        }
    }
}
```

---

## 10. Testing: JUnit/MockMvc vs. xUnit/WebApplicationFactory

### Unit Tests (`tests/ApiForge.UnitTests/StoreTests.cs`):
Uses **xUnit**, the standard testing framework in .NET:
* `[Fact]` replaces `@Test`.
* `Assert.Equal(expected, actual)` replaces `assertEquals(expected, actual)`.
* `Assert.True(condition)` replaces `assertTrue(condition)`.

```csharp
public sealed class StoreTests
{
    [Fact]
    public async Task Content_search_is_exact_and_ands_filters()
    {
        var t = new InMemoryContentTypeStore();
        var c = new InMemoryContentStore(t);
        await t.Create(new(null, "Tag", null, "tag", null, [], null, null), default);
        await c.Create("tag", new Dictionary<string, object?> { ["label"] = "one", ["active"] = true }, default);
        await c.Create("tag", new Dictionary<string, object?> { ["label"] = "one", ["active"] = false }, default);

        var rows = await c.Search("tag", new Dictionary<string, object?> { ["label"] = "one", ["active"] = true }, default);
        Assert.Single(rows);
    }
}
```

### Integration Tests (`tests/ApiForge.ApiTests/ApiTests.cs`):
In Spring Boot, you might use `@SpringBootTest(webEnvironment = RANDOM_PORT)` with `TestRestTemplate` or `MockMvc`.
In ASP.NET Core, the gold standard is **`WebApplicationFactory<Program>`**:
* It hosts the entire web application **in-memory** (using a lightweight test server).
* No real TCP port is occupied.
* It provisions an `HttpClient` that dispatches directly into the ASP.NET Core pipeline.
* Testing runs in milliseconds.

```csharp
public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Protected_route_without_token_returns_envelope_401()
    {
        var response = await _client.GetAsync("/api/content-types");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(body!.Success);
        Assert.Equal("Unauthorized", body.Error);
    }
}
```

---

## 11. Configuration & Environment Profiles

### File Hierarchy:
1. `appsettings.json` (Default/Production base config)
2. `appsettings.Development.json` (Overrides for local development)
3. Environment variables (Highest priority)

### Spring Profiles vs. ASP.NET Core Environments:
In Spring, you use `SPRING_PROFILES_ACTIVE=dev` to activate `application-dev.yml`.
In .NET, you use `ASPNETCORE_ENVIRONMENT=Development` (or `Production`). When set to `Development`, ASP.NET Core automatically loads `appsettings.json` first, then merges `appsettings.Development.json` over it.

### Environment Variable Naming for Nested JSON:
In `appsettings.json`:
```json
{
  "Storage": {
    "Provider": "Postgres",
    "ConnectionString": "Host=localhost;..."
  }
}
```
To override in Docker or Linux environment variables, use **double underscores (`__`)**:
```bash
Storage__Provider=Postgres
Storage__ConnectionString="Host=db;Port=5432;Database=devdb;Username=dev;Password=devpass"
Jwt__Secret="your-production-secret-key-32-chars-minimum"
```

---

## 12. CLI & Development Workflow

Here is your daily command translation cheat sheet:

| Task | Maven / Gradle Command | .NET CLI Command |
|---|---|---|
| Restore dependencies | `mvn dependency:resolve` | `dotnet restore` |
| Compile code | `mvn compile` | `dotnet build` |
| Run all tests | `mvn test` | `dotnet test` |
| Run specific test | `mvn test -Dtest=StoreTests` | `dotnet test --filter FullyQualifiedName~StoreTests` |
| Run API locally | `mvn spring-boot:run` | `dotnet run --project src/ApiForge.Api` |
| Run with custom URL | `-Dserver.port=7080` | `dotnet run --project src/ApiForge.Api --urls http://localhost:7080` |
| Publish release binary | `mvn package` | `dotnet publish -c Release -o ./publish` |
| Add external package | Add `<dependency>` to `pom.xml` | `dotnet add package <PackageName>` |
| Clean build artifacts | `mvn clean` | `dotnet clean` |

### Running with Docker Compose:
The repository includes a ready-to-run Docker Compose stack:
```bash
# Spins up PostgreSQL 16, mounts schema/seed DDL, builds .NET app, and runs on port 7080
docker compose up --build
```

---

## 13. Step-by-Step: Adding a New Feature

Let's walk through how to add a hypothetical new feature: **"Webhooks"** (triggering HTTP calls when content changes).

### Step 1: Define the Domain Models & Store Interface
In `src/ApiForge.Core/Contracts.cs`:
```csharp
public sealed record WebhookDto(long? Id, string Url, string EventType, bool Active);

public interface IWebhookStore
{
    Task<WebhookDto> Create(WebhookDto dto, CancellationToken ct);
    Task<IReadOnlyList<WebhookDto>> All(CancellationToken ct);
    Task<bool> Delete(long id, CancellationToken ct);
}
```

### Step 2: Implement the Store (In-Memory & Postgres)
* In `src/ApiForge.Infrastructure/InMemoryStores.cs`: implement `IWebhookStore` with a `ConcurrentDictionary<long, WebhookDto>`.
* In `src/ApiForge.Infrastructure/PostgresStores.cs`: implement `IWebhookStore` with SQL queries using `NpgsqlCommand`.

### Step 3: Define & Implement the Service
* In `src/ApiForge.Core/Services/IWebhookService.cs`: define service methods.
* In `src/ApiForge.Api/Services/WebhookService.cs`: implement business logic and validation.

### Step 4: Register in Dependency Injection (`Program.cs`)
```csharp
if (isPostgres) {
    builder.Services.AddSingleton<IWebhookStore, PostgresWebhookStore>();
} else {
    builder.Services.AddSingleton<IWebhookStore, InMemoryWebhookStore>();
}
builder.Services.AddScoped<IWebhookService, WebhookService>();
```

### Step 5: Add Controller
In `src/ApiForge.Api/Controllers/WebhookController.cs`:
```csharp
[ApiController]
[Route("api/webhooks")]
[Authorize]
public class WebhookController(IWebhookService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebhookDto>>> Create([FromBody] WebhookDto dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(dto, ct);
        return Ok(ApiResponse<WebhookDto>.Ok(result, "Webhook created"));
    }
}
```

---

## 14. Top 10 Gotchas for Spring Developers

1. **Casing Conventions**:
   * C# uses **PascalCase** for public methods (`CreateAsync`), properties (`Username`), and classes (`AuthService`).
   * JSON serialization automatically converts PascalCase properties to **camelCase** for HTTP consumers (`JsonNamingPolicy.CamelCase`).
2. **Namespace != Folder Path**:
   * In Java, `package com.apiforge.core` MUST strictly match the folder structure `com/apiforge/core`.
   * In C#, namespaces are purely logical. While usually aligned with folders, you won't get compilation errors if they differ.
3. **Implicit Usings & Global Usings**:
   * You may notice fewer `using` statements at the top of files than `import` statements in Java. In .NET 10 with `<ImplicitUsings>enable</ImplicitUsings>`, standard namespaces like `System`, `System.Collections.Generic`, `System.Threading.Tasks`, and `System.Linq` are imported automatically.
4. **Properties vs. Fields**:
   * C# properties (`public string Name { get; set; }`) look like fields but are actually compiler-generated getter and setter methods. Always use PascalCase for properties.
5. **No Checked Exceptions**:
   * C# does not have checked exceptions (no `throws IOException`). All exceptions are unchecked. Use domain exceptions like `ApiForgeException` and handle them centrally in middleware.
6. **Async All the Way Down**:
   * Never call `.Result` or `.Wait()` on a C# `Task` in a web request! Doing so will block the thread pool and can cause deadlocks. Always use `await`.
7. **Scoped vs. Singleton**:
   * Spring beans are `@Scope("singleton")` by default.
   * In ASP.NET Core, services handling business logic should typically be registered as `AddScoped` so they are bound to the HTTP request lifetime.
8. **Double Underscore in Environment Variables**:
   * In Spring, `storage.connection-string` maps to `STORAGE_CONNECTIONSTRING` or `STORAGE_CONNECTION_STRING`.
   * In .NET, nested hierarchy uses `:`, which in environment variables is represented as `__` (e.g. `Storage__ConnectionString`).
9. **No `new` Keyword Required for Targets (Target-Typed New)**:
   * C# allows `List<string> list = new();` or `return new(id, name);` when the type is known from context.
10. **`var` is Strongly Typed**:
    * Just like Java 10's `var`, C#'s `var` is not dynamic typing; it is strictly inferred at compile-time.

---

## Conclusion

You now have a complete conceptual and practical roadmap of the `apiforge-headless-cms-dotnet` codebase. By combining clean architecture, high-performance in-process routing, pluggable storage adapters, and modern C# ergonomics, this application delivers the full capabilities of the original Spring Cloud microservices in an elegant, unified, and blazing-fast .NET 10 host.

If you have any questions or want to explore any specific module in greater detail, dive into the code starting from [Program.cs](file:///c:/Users/dhrubo/projects/apiforge-headless-cms-dotnet/src/ApiForge.Api/Program.cs)!
