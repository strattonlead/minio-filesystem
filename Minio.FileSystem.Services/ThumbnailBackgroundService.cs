using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Services
{
    public class ThumbnailBackgroundService : BackgroundService
    {
        #region Properties

        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Constructor

        public ThumbnailBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<ThumbnailBackgroundService>>();
        }

        #endregion

        #region ThumbnailBackgroundService

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                        var newThumbnailsCount = await thumbnailService.CreateMissingThumbnailsAsync();
                        if (newThumbnailsCount > 0)
                        {
                            _logger.LogInformation($"{newThumbnailsCount} new thumbnails created");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error in ThumbnailBackgroundService");
                }
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        #endregion

    }
}
