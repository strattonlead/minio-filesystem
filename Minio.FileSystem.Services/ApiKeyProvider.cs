using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Minio.FileSystem.Services
{
    public class ApiKeyProvider
    {
        private string ApiKeyHeaderName => Environment.GetEnvironmentVariable("API_KEY_HEADER_NAME");

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IDataProtector _dataProtector;

        public ApiKeyProvider(IServiceProvider serviceProvider)
        {
            _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            _dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            _dataProtector = _dataProtectionProvider.CreateProtector(Environment.GetEnvironmentVariable("DATA_PROTECTION_PURPOSE"));
        }

        public string GetApiKey()
        {
            var encryptedData = _httpContextAccessor.HttpContext?.Request?.Headers[ApiKeyHeaderName];
            if (string.IsNullOrWhiteSpace(encryptedData))
            {
                return null;
            }
            return _dataProtector.Unprotect(encryptedData);
        }
    }
}
