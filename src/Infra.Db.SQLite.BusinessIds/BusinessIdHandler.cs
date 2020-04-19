using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace NetExtensions
{
    public class BusinessIdHandler
    {
        private readonly ILogger<BusinessIdHandler> _logger;
        private readonly SqLiteDbRestore _sqLiteDbRestore;

        public BusinessIdHandler(ILogger<BusinessIdHandler> logger, SqLiteDbRestore sqLiteDbRestore)
        {
            _logger = logger;
            _sqLiteDbRestore = sqLiteDbRestore;
        }
        public async Task<long> GetAsync(CancellationToken token)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var databaseFile = $"{path}\\{Constants.DatabaseFile}";
            if (!File.Exists(databaseFile))
            {
                await _sqLiteDbRestore.RestoreAsync();
            }

            await using var connection = new SqliteConnection((new SqliteConnectionStringBuilder { DataSource = databaseFile }).ConnectionString);
            connection.Open();
            await using var transaction = connection.BeginTransaction();
            var getCommand = connection.CreateCommand();
            long id = 0;
            while (id ==0)
            {
                getCommand.CommandText = @"select BusinessId 
                                    from BusinessIds 
                                    where abs(CAST(random() AS REAL))/9223372036854775808 < 0.5
                                    and Used = 0 
                                    LIMIT 1;";
                id = (long)(await getCommand.ExecuteScalarAsync(token));
            }
            getCommand.CommandText = $"update BusinessIds SET Used =1, Activated = datetime('now') where Used =0 AND BusinessId = {id}";
            await getCommand.ExecuteNonQueryAsync(token);
            transaction.Commit();
            _logger.LogInformation($"new business id is fetched: {id}");
            return id;
        }
    }
}