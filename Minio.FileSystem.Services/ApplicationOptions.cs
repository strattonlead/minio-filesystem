using System;

namespace Minio.FileSystem.Services
{
    public class ApplicationOptions
    {
        public bool TenancyEnabled
        {
            get
            {
                bool.TryParse(Environment.GetEnvironmentVariable("TENANCY_ENABLED"), out var tenancyEnabled);
                return tenancyEnabled;
            }
        }

        public bool FileCacheEnabled
        {
            get
            {
                bool.TryParse(Environment.GetEnvironmentVariable("FILE_CACHE_ENABLED"), out var fileCacheEnabled);
                return fileCacheEnabled;
            }
        }

        public string TenancyClaimName => Environment.GetEnvironmentVariable("TENANCY_CLAIM_NAME") ?? "tenant_id";
    }
}
