using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Muffin.Tenancy.Abstraction;
using Muffin.Tenancy.Services.Abstraction;
using System;
using System.Linq;

namespace Minio.FileSystem.Services
{
    public class Tenant : ITenant
    {
        public long Id { get; set; }
    }

    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationOptions _options;
        private readonly ApiKeyProvider _apiKeyProvider;

        public TenantProvider(IServiceProvider serviceProvider)
        {
            _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            _options = serviceProvider.GetRequiredService<ApplicationOptions>();
            _apiKeyProvider = serviceProvider.GetService<ApiKeyProvider>();
        }

        private ITenant _tenant = null;
        public ITenant ActiveTenant
        {
            get
            {
                if (_tenant != null)
                {
                    return _tenant;
                }

                if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated ?? false)
                {
                    var claimName = _options.TenancyClaimName;
                    var sTenantId = _httpContextAccessor?.HttpContext?.User?.Claims?.FirstOrDefault(x => x.Type == claimName)?.Value;
                    if (long.TryParse(sTenantId, out var tenantId))
                    {
                        _tenant = new Tenant() { Id = tenantId };
                    }
                }

                if (_tenant == null && _apiKeyProvider != null)
                {
                    var apiKey = _apiKeyProvider.GetApiKey();
                    if (apiKey != null)
                    {
#warning TODO hier muss noch was generisches her
                        var sTenantId = apiKey.Split(".").FirstOrDefault();
                        if (long.TryParse(sTenantId, out var tenantId))
                        {
                            _tenant = new Tenant() { Id = tenantId };
                        }
                    }
                }

                return _tenant;
            }
            set { _tenant = value; }
        }

        public ITenant GetTenant(long id)
        {
            throw new NotImplementedException();
        }

        public void RestoreTenancy()
        {
            ActiveTenant = null;
        }
    }

    public static class TenantProviderExtensions
    {
        public static void SetTenant(this ITenantProvider tenantProvider, long? id)
        {
            if (!id.HasValue)
            {
                tenantProvider.ActiveTenant = null;
            }
            else
            {
                tenantProvider.ActiveTenant = new Tenant() { Id = id.Value };
            }
        }

        public static void AddTenantProvider(this IServiceCollection services)
        {
            services.AddScoped<ITenantProvider, TenantProvider>();
        }
    }
}
