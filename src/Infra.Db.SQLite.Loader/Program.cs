using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetExtensions;

namespace Infra.Db.SQLite.Loader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddBusinessIdHandler();
            services.AddScoped(typeof(ILogger<>), typeof(CustomLogger<>));
            var buildServiceProvider = services.BuildServiceProvider();
            var businessIdHandler = buildServiceProvider.GetService<BusinessIdHandler>();
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

    public class CustomLogger<T> : ILogger<T>
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            return;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
