using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NetExtensions
{
    public static class BusinessIdHandlerExtension
    {
        public static IServiceCollection AddBusinessIdHandler(this IServiceCollection services, string connectionString = null) 
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddScoped<BusinessIdHandler>();
                services.AddScoped<SqLiteDbRestore>();
            }
            else
            {
                services.AddScoped(p=> new BusinessIdHandler(p.GetRequiredService<ILogger<BusinessIdHandler>>(),
                    p.GetRequiredService<SqLiteDbRestore>(), connectionString));
                services.AddScoped(p=> new SqLiteDbRestore(p.GetRequiredService<ILogger<SqLiteDbRestore>>(),
                    connectionString));
            }
            return services;
        }
    }
}
