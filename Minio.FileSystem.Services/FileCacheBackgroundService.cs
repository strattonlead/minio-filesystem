using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Services
{
    internal class FileCacheBackgroundService : BackgroundService
    {
        private readonly ApplicationOptions _options;
        public FileCacheBackgroundService(ApplicationOptions options) => _options = options;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.FileCacheEnabled)
            {
                return;
            }

            await Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var files = Directory.EnumerateFiles("cache");
                        foreach (var file in files)
                        {
                            if (File.GetLastAccessTimeUtc(file) > DateTime.UtcNow.AddMinutes(5))
                            {
                                File.Delete(file);
                            }
                        }
                    }
                    catch { }
                }
            });
        }
    }

    public static class FileCacheBackgroundServiceExtensions
    {
        public static void AddFileCacheBackgroundService(this IServiceCollection services)
        {
            services.AddHostedService<FileCacheBackgroundService>();
        }
    }
}
