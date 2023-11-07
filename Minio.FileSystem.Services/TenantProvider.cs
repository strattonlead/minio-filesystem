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

        public TenantProvider(IServiceProvider serviceProvider)
        {
            _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            _options = serviceProvider.GetRequiredService<ApplicationOptions>();
        }

        public ITenant ActiveTenant
        {
            get
            {
                if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated ?? false)
                {
                    var claimName = _options.TenancyClaimName;
                    var sTenantId = _httpContextAccessor?.HttpContext?.User?.Claims?.FirstOrDefault(x => x.Type == claimName)?.Value;
                    if (long.TryParse(sTenantId, out var tenantId))
                    {
                        return new Tenant() { Id = tenantId };
                    }
                }

                return null;
            }
            set { }
        }

        public ITenant GetTenant(long id)
        {
            throw new NotImplementedException();
        }

        public void RestoreTenancy()
        {
            throw new NotImplementedException();
        }
    }

    public static class TenantProviderExtensions
    {
        public static void AddTenantProvider(this IServiceCollection services)
        {
            services.AddScoped<ITenantProvider, TenantProvider>();
        }
    }
}
