using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio.FileSystem.Backend;
using Minio.FileSystem.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMinioClient();
builder.Services.AddFileSystemService();
builder.Services.AddTenantProvider();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING") ?? "Server=localhost\\sqlexpress;Database=Minio.FileSystem.Local;Trusted_Connection=True;MultipleActiveResultSets=true");
});
builder.Services.AddSingleton<ApplicationOptions>();
builder.Services.AddFileCacheService();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddDbMigratorBackgroundService();
builder.Services.AddFileCacheBackgroundService();

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
}

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
