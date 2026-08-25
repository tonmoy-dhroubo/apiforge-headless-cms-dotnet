using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiForge.Api.Middleware;
using ApiForge.Api.Services;
using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Storage Provider Stores
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

// Security & Token Service
builder.Services.AddSingleton<JwtTokenService>();

// Domain Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IContentTypeService, ContentTypeService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Authentication & JWT Bearer Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "apiforge-headless-cms-secret-key-minimum-256-bits-required-for-hs256";
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
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Unauthorized"));
        }
    };
});

builder.Services.AddAuthorization();

// Controllers & JSON Serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Global Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS & Swagger
app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();

// Auth Middleware Pipeline
app.UseAuthentication();
app.UseAuthorization();

// Map MVC Controllers
app.MapControllers();

app.Run();

public partial class Program { }
