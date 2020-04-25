using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using static NetExtensions.Constants;

namespace NetExtensions
{
    public class BusinessIdHandler
    {
        private readonly string _connectionSetting;
        private readonly ILogger<BusinessIdHandler> _logger;
        private readonly SqLiteDbRestore _sqLiteDbRestore;

        public BusinessIdHandler(ILogger<BusinessIdHandler> logger, SqLiteDbRestore sqLiteDbRestore, string connectionSetting = null)
        {
            _logger = logger;
            _sqLiteDbRestore = sqLiteDbRestore;
            _connectionSetting = connectionSetting;
        }

        public async Task<long> GetAsync(CancellationToken token)
        {
            var connectionStringBuilder = BusinessIdHandlerExtension.ConnectionStringBuilder(DatabaseFile, _connectionSetting);
            await RestoreDbAsync(connectionStringBuilder);

            await using var connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
            connection.Open();
            await using var transaction = connection.BeginTransaction();
            var getCommand = connection.CreateCommand();
            long id = 0;
            while (id == 0)
            {
                getCommand.CommandText = @"select BusinessId 
                                    from BusinessIds 
                                    where abs(CAST(random() AS REAL))/9223372036854775808 < 0.5
                                    and Used = 0 
                                    LIMIT 1;";
                id = (long) await getCommand.ExecuteScalarAsync(token);
            }

            getCommand.CommandText = $"update BusinessIds SET Used =1, Activated = datetime('now') where Used =0 AND BusinessId = {id}";
            await getCommand.ExecuteNonQueryAsync(token);
            transaction.Commit();
            _logger.LogInformation($"new business id is fetched: {id}");
            return id;
        }

        private async Task RestoreDbAsync((string ConnectionString, string DbFilePath) connectionStringBuilder)
        {
            if (!File.Exists(connectionStringBuilder.DbFilePath))
            {
                _logger.LogInformation($"GetAsync - Db does not exists, creating ...  file path: {connectionStringBuilder.DbFilePath}");
                await _sqLiteDbRestore.RestoreAsync(connectionStringBuilder.ConnectionString, connectionStringBuilder.DbFilePath);
            }
        }
    }
}