using Microsoft.Extensions.DependencyInjection;

namespace NetExtensions
{
    public static class BusinessIdHandlerExtension
    {
        public static IServiceCollection AddBusinessIdHandler(this IServiceCollection services) 
        {
            services.AddScoped<BusinessIdHandler>();
            services.AddScoped<SqLiteDbRestore>();
            return services;
        }
    }
}
