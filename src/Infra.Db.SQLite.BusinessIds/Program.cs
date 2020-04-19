using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.Db.SQLite.BusinessIds
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var businessIdHandler = new BusinessIdHandler();
            var st = DateTime.Now;
            for (int i = 0; i < 1; i++)
            {
                var result = await businessIdHandler.GetAsync(CancellationToken.None);

            }
            var b = DateTime.Now - st;
            Console.WriteLine("Hello World!");
        }
    }
}
