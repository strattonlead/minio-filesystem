﻿namespace Minio.FileSystem.WebApi.Models
{
    public class GetModel
    {
        public string VirtualPath { get; set; }
        public long? TenantId { get; set; }
    }
}
