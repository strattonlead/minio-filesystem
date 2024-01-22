using Microsoft.Extensions.DependencyInjection;
using Minio.Exceptions;
using Minio.FileSystem.Backend;
using Muffin.Tenancy.Services.Abstraction;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Services
{
    public class MinioClientWithTenancy : MinioClient
    {
        private readonly MinioClientOptions _options;
        private readonly ITenantProvider _tenantProvider;
        public bool Ready => _tenantProvider?.ActiveTenant != null;
        public string BucketName => Ready ? string.Format(_options.BucketName, _tenantProvider.ActiveTenant.Id) : null;

        public MinioClientWithTenancy(IServiceProvider serviceProvider)
        {
            _options = serviceProvider.GetRequiredService<MinioClientOptions>();
            _tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
        }

        public async Task<MinioClient> BuildAndCreateBucket()
        {
            var client = Build();
            await EnsureReady();
            return client;
        }

        private bool Initialized = false;
        public async Task<bool> EnsureReady()
        {
            if (Ready && !Initialized)
            {
                var beArgs = new BucketExistsArgs()
                        .WithBucket(BucketName);
                bool found = await BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(BucketName);
                    await MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                Initialized = true;
                return true;
            }
            return Ready && Initialized;
        }
    }

    public static class TeanantMinioClientExtensions
    {
        public static void AddMinioClient(this IServiceCollection services, Action<MinioClientOptionsBuilder> builder = null)
        {
            var optionsBuilder = new MinioClientOptionsBuilder();
            builder?.Invoke(optionsBuilder);

            services.AddSingleton(optionsBuilder.Options);
            services.AddScoped<IMinioClient>(serviceProvider =>
                 ((MinioClientWithTenancy)new MinioClientWithTenancy(serviceProvider)
                    .WithEndpoint(optionsBuilder.Options.Endpoint)
                    .WithCredentials(optionsBuilder.Options.AccessKey, optionsBuilder.Options.SecretKey)
                    .WithSSL(true))
                    .BuildAndCreateBucket()
                    .Result
            );
        }

        public static string BucketName(this IMinioClient minio) => ((MinioClientWithTenancy)minio).BucketName;

        public static async Task PutObjectAsync(this IMinioClient minio, FileSystemItemEntity fileSystemItem, Stream stream, bool @override, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            if (@override || !await minio.ObjectExistsAsync(fileSystemItem))
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(minio.BucketName())
                    .WithObject(fileSystemItem.StoragePath)
                    .WithObjectSize(stream.Length)
                    .WithStreamData(stream)
                    .WithContentType(fileSystemItem.ContentType);
                await minio.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task PutObjectAsync(this IMinioClient minio, ThumbnailEntity thumbnail, Stream stream, bool @override, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            if (@override || !await minio.ObjectExistsAsync(thumbnail))
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(minio.BucketName())
                    .WithObject(thumbnail.StoragePath)
                    .WithObjectSize(stream.Length)
                    .WithStreamData(stream)
                    .WithContentType(thumbnail.ContentType);
                await minio.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task PutObjectAsync(this IMinioClient minio, FileSystemItemEntity fileSystemItem, string filePath, bool @override, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            if (@override || !await minio.ObjectExistsAsync(fileSystemItem))
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(minio.BucketName())
                    .WithObject(fileSystemItem.StoragePath)
                    .WithFileName($"{filePath}")
                    .WithContentType(fileSystemItem.ContentType);
                await minio.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task GetObjectAsync(this IMinioClient minio, FileSystemItemEntity FileSystemItem, Stream outputStream, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(minio.BucketName())
                .WithObject(FileSystemItem.StoragePath)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(outputStream, ct);
                });

            await minio.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
        }

        public static async Task GetObjectAsync(this IMinioClient minio, FileSystemItemEntity FileSystemItem, Func<Stream, CancellationToken, Task> streamAction, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            var getObjectArgs = new GetObjectArgs()
               .WithBucket(minio.BucketName())
               .WithObject(FileSystemItem.StoragePath)
               .WithCallbackStream(streamAction);

            await minio.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
        }

        public static async Task RemoveObjectAsync(this IMinioClient minio, FileSystemItemEntity FileSystemItem, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            var removeObjectArgs = new RemoveObjectArgs()
              .WithBucket(minio.BucketName())
              .WithObject(FileSystemItem.StoragePath);

            await minio.RemoveObjectAsync(removeObjectArgs, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> ObjectExistsAsync(this IMinioClient minio, FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(minio.BucketName())
                .WithObject(fileSystemItem.StoragePath)
                .WithCallbackStream(x => { });

            try
            {
                await minio.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectNotFoundException) { return false; }
            catch (Exception e)
            {
                throw new Exception("Error in ObjectExistsAsync", e);
            }

            return true;
        }

        public static async Task<bool> ObjectExistsAsync(this IMinioClient minio, ThumbnailEntity thumbnail, CancellationToken cancellationToken = default)
        {
            var ready = await ((MinioClientWithTenancy)minio).EnsureReady();
            if (!ready)
            {
                throw new InvalidOperationException("IMinioClient is not ready. probably tenant missing");
            }

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(minio.BucketName())
                .WithObject(thumbnail.StoragePath)
                .WithCallbackStream(x => { });

            try
            {
                await minio.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectNotFoundException) { return false; }
            catch (Exception e)
            {
                throw new Exception("Error in ObjectExistsAsync", e);
            }

            return true;
        }
    }
}
