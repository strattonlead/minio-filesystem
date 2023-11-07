using Microsoft.AspNetCore.Mvc;

namespace Minio.FileSystem.Services.Authorization
{
    public class ApiKeyAttribute : ServiceFilterAttribute
    {
        public ApiKeyAttribute()
            : base(typeof(ApiKeyAuthorizationFilter))
        {
        }
    }
}
