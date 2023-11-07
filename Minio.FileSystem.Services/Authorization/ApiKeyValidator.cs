using System;

namespace Minio.FileSystem.Services.Authorization
{
    public class ApiKeyValidator : IApiKeyValidator
    {
        public bool IsValid(string apiKey)
        {
            return apiKey == Environment.GetEnvironmentVariable("API_KEY");
        }
    }

    public interface IApiKeyValidator
    {
        bool IsValid(string apiKey);
    }
}
