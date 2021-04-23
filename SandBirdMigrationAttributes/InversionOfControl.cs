using Microsoft.Extensions.DependencyInjection;
using SendbirdHttpClient.Extensions;
using TheGrandMigrator;
using TheGrandMigrator.Abstractions;
using TwilioHttpClient.Extensions;

namespace SandBirdMigrationAttributes
{
    public static class InversionOfControl
    {
        public static ServiceProvider Setup()
        {
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSendbirdHttpClient()
                .AddTwilioClient()
                .AddSingleton<IMigrator, Migrator>()
                .BuildServiceProvider();

            return serviceProvider;
        }
    }
}
