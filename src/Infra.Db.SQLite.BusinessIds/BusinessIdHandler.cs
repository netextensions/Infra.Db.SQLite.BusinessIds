using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace NetExtensions
{
    public class BusinessIdHandler
    {
        public async Task<long> GetAsync(CancellationToken token)
        {
            if (!File.Exists(Constants.DatabaseFile))
            {
                var sqLiteDbRestore = new SqLiteDbRestore();
                sqLiteDbRestore.Restore();
            }

            await using var connection = new SqliteConnection((new SqliteConnectionStringBuilder { DataSource = Constants.DatabaseFile }).ConnectionString);
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
            return id;
        }
    }
}