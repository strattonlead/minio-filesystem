using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio.Filesystem.Backend;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Services
{
    public class DbMigratorBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DbMigratorBackgroundService> _logger;
        public DbMigratorBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<DbMigratorBackgroundService>>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                try
                {
                    await dbContext.Database.MigrateAsync(stoppingToken);
                    dbContext.SaveChanges();
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
            }
        }
    }

    public static class DbMigratorBackgroundServiceExtensions
    {
        public static void AddDbMigratorBackgroundService(this IServiceCollection services)
        {
            services.AddHostedService<DbMigratorBackgroundService>();
        }
    }
}
