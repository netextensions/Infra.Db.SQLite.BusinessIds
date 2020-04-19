using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetExtensions;

namespace Infra.Db.SQLite.Loader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var businessIdHandler = new BusinessIdHandler();
            var start = DateTime.Now;
            var items = new List<long>();
            for (var i = 0; i < 100; i++)
            {
                items.Add(await businessIdHandler.GetAsync(CancellationToken.None));
            }
            var end = DateTime.Now - start;
            var delimiter = ",";
            Console.WriteLine($"time:{end} ids: {items.Aggregate("", (i, j) => i + delimiter + j)}");
        }
    }
}
