using System;

namespace Minio.FileSystem.Services
{
    public class MinioClientOptions
    {
        public string Endpoint { get; set; } = Environment.GetEnvironmentVariable("S3_ENDPOINT");
        public string AccessKey { get; set; } = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
        public string SecretKey { get; set; } = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
        public string BucketName { get; set; } = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
    }

    public class MinioClientOptionsBuilder
    {
        internal MinioClientOptions Options = new MinioClientOptions();

        public MinioClientOptionsBuilder UseEndpoint(string endpoint)
        {
            Options.Endpoint = endpoint;
            return this;
        }

        public MinioClientOptionsBuilder UseAccessKey(string accessKey)
        {
            Options.AccessKey = accessKey;
            return this;
        }

        public MinioClientOptionsBuilder UseSecretKey(string secretKey)
        {
            Options.SecretKey = secretKey;
            return this;
        }

        public MinioClientOptionsBuilder UseBucketName(string bucketName)
        {
            Options.BucketName = bucketName;
            return this;
        }
    }
}
