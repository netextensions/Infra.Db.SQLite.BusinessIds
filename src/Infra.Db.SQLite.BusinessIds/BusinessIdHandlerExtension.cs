using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace NetExtensions
{
    public static class BusinessIdHandlerExtension
    {
        public static IServiceCollection AddBusinessIdHandler(this IServiceCollection services, string connectionSetting = null)
        {

            services.AddScoped(p => new BusinessIdHandler(p.GetRequiredService<ILogger<BusinessIdHandler>>(),
                p.GetRequiredService<SqLiteDbRestore>(), connectionSetting));
            return services.AddScoped<SqLiteDbRestore>();

        }

        public static (string ConnectionString, string DbFilePath) ConnectionStringBuilder(string defaultDatabaseFileName, string connectionSetting)
        {
            var connectionString = PrepareConnectionString(defaultDatabaseFileName, connectionSetting);
            var builder = new SqliteConnectionStringBuilder { ConnectionString = connectionString };
            var dbPath = builder.Values.OfType<string>().FirstOrDefault();
            return (connectionString, dbPath);
        }

        private static string PrepareConnectionString(string defaultDatabaseFileName, string connectionSetting)
        {
            var connectionString = ChooseDataSource(defaultDatabaseFileName, connectionSetting);
            return (string.Concat(connectionString.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant()).StartsWith("datasource=")
                ? connectionString : $"Data Source={connectionString}";
        }

        private static  string ChooseDataSource(string defaultDatabaseFileName, string  connectionSetting)
        {
            return !string.IsNullOrWhiteSpace(connectionSetting) ? connectionSetting
                : $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\{defaultDatabaseFileName}";
        } 
    }
}
