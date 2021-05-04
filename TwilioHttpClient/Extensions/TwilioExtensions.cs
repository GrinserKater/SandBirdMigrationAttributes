using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Configuration.Http;
using TwilioHttpClient.Options;

namespace TwilioHttpClient.Extensions
{
	public static class TwilioExtensions
	{
		public static IServiceCollection AddTwilioClient(this IServiceCollection services)
		{
			var configuration = new ConfigurationBuilder()
				.SetBasePath(Constants.Configuration.BasePath)
				.AddJsonFile(Constants.Configuration.FileName)
				.Build();

			services
				.Configure<TwilioOptions>(c =>
				{
					configuration.Bind(c);
				})
				.AddHttpClient<ITwilioHttpClient, TwilioHttpClient>()
				.AddPolicyHandler(Resilience.BuildRetryPolicy());

            return services;
		}
	}
}
