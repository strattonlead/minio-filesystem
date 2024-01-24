using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Minio.FileSystem.Backend;
using Minio.FileSystem.Services;
using Minio.FileSystem.Services.Authorization;
using System;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMinioClient();
builder.Services.AddFileSystemService();
builder.Services.AddTenantProvider();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(Environment.GetEnvironmentVariable("NPGSQL_CONNECTION_STRING") ?? "fill me");
});

var applicationName = Environment.GetEnvironmentVariable("APPLICATION_NAME");
bool.TryParse(Environment.GetEnvironmentVariable("USE_DATA_PROTECTION"), out var useDataProtection);
if (useDataProtection)
{
    builder.Services.AddDbContext<DataProtectionDbContext>(options =>
    {
        options.UseNpgsql(Environment.GetEnvironmentVariable("DATA_PROTECTION_CONNECTION_STRING"));
    });

    builder.Services.AddDataProtection()
        .SetApplicationName(applicationName)
        .PersistKeysToDbContext<DataProtectionDbContext>();
}

builder.Services.AddSingleton<ApplicationOptions>();
builder.Services.AddFileCacheService();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddDbMigratorBackgroundService();
builder.Services.AddFileCacheBackgroundService();
builder.Services.AddScoped<ThumbnailService>();
builder.Services.AddHostedService<ThumbnailBackgroundService>();
builder.Services.AddSingleton<ApiKeyAuthorizationFilter>();
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme()
    {
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Authorization by X-API-Key inside request's header",
        Scheme = "ApiKeyScheme"
    });

    var key = new OpenApiSecurityScheme()
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "ApiKey"
        },
        In = ParameterLocation.Header
    };

    var requirement = new OpenApiSecurityRequirement { { key, new List<string>() } };
    c.AddSecurityRequirement(requirement);
});
builder.Services.AddSingleton<ApiKeyAuthorizationFilter>();
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

bool.TryParse(Environment.GetEnvironmentVariable("USE_AUTHENTICATION"), out var useAuthentication);
if (useAuthentication)
{
    var authenticationScheme = Environment.GetEnvironmentVariable("AUTHENTICATION_SCHEME");
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = authenticationScheme;
        options.DefaultChallengeScheme = authenticationScheme;
        options.DefaultScheme = authenticationScheme;
    });

    bool.TryParse(Environment.GetEnvironmentVariable("USE_COOKIE_AUTHENTICATION"), out var useCookieAuthentication);
    if (useCookieAuthentication)
    {
        authBuilder.AddCookie(authenticationScheme, options =>
        {
            bool.TryParse(Environment.GetEnvironmentVariable("SLIDING_EXPIRATION"), out var slidingExpiration);
            int.TryParse(Environment.GetEnvironmentVariable("SAME_SITE"), out var sameSite);
            var sameSiteMode = (SameSiteMode)sameSite;
            bool.TryParse(Environment.GetEnvironmentVariable("HTTP_ONLY"), out var httpOnly);
            int.TryParse(Environment.GetEnvironmentVariable("SECURE_POLICY"), out var securePolicy);
            var securePolicyMode = (CookieSecurePolicy)securePolicy;
            TimeSpan.TryParse(Environment.GetEnvironmentVariable("EXPIRE_TIME_SPAN"), out var expireTimeSpan);

            options.SlidingExpiration = slidingExpiration;
            options.Cookie.Name = Environment.GetEnvironmentVariable("COOKIE_NAME");
            options.Cookie.Domain = Environment.GetEnvironmentVariable("DOMAIN");
            options.Cookie.Path = Environment.GetEnvironmentVariable("COOKIE_PATH");
            options.Cookie.SameSite = sameSiteMode;
            options.Cookie.HttpOnly = httpOnly;
            options.Cookie.SecurePolicy = securePolicyMode;
            options.ExpireTimeSpan = expireTimeSpan;
        });
    }
    bool.TryParse(Environment.GetEnvironmentVariable("USE_API_KEY_AUTHENTICATION"), out var useApiKeyAuthentication);
    if (useApiKeyAuthentication)
    {
        if (!useDataProtection)
        {
            throw new ArgumentException("USE_DATA_PROTECTION must be true when using USE_API_KEY_AUTHENTICATION");
        }

        var dataProtectionPurpose = Environment.GetEnvironmentVariable("DATA_PROTECTION_PURPOSE");
        if (string.IsNullOrWhiteSpace(dataProtectionPurpose))
        {
            throw new Exception("DATA_PROTECTION_PURPOSE must be set!");
        }

        var apiKeyHeaderName = Environment.GetEnvironmentVariable("API_KEY_HEADER_NAME");
        if (string.IsNullOrWhiteSpace(apiKeyHeaderName))
        {
            throw new Exception("API_KEY_HEADER_NAME must be set! (e.g. X-API-Key)");
        }

        builder.Services.AddSingleton<ApiKeyProvider>();
    }


}

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
