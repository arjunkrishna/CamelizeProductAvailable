using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CamelizeProductAvailable
{
    class Program
    {
        public static Microsoft.Extensions.Configuration.IConfiguration configuration;

        private static async Task Main(string[] args)
        {
            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                var serviceProvider = services.BuildServiceProvider();
                await serviceProvider.GetService<ProductSearch>().Run();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.StackTrace +" " + exp.InnerException?.Message);
            }
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(AppContext.BaseDirectory)?.FullName)
            .AddJsonFile("appsettings.json", false)
            .Build();

            services.AddSingleton(configuration);

            services.AddLogging(configure => configure.AddConsole())
            .AddTransient<ProductSearch>();
        }
    }

    class MyJsonType
    {
        public List<string> OutOfStockProductUrl { get; set; }
    }
}
