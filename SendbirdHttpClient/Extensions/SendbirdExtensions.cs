using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SendbirdHttpClient.Abstractions;
using SendbirdHttpClient.Configuration.Http;
using SendbirdHttpClient.Options;

namespace SendbirdHttpClient.Extensions
{
	public static class SendbirdExtensions
	{
		public static IServiceCollection AddSendbirdHttpClient(this IServiceCollection services)
		{
			IConfigurationRoot configuration = new ConfigurationBuilder()
				.SetBasePath(Constants.Configuration.BasePath)
				.AddJsonFile(Constants.Configuration.FileName)
				.Build();

			services
				.Configure<SendbirdOptions>(c =>
				{
					configuration.Bind(c);
				})
				.AddHttpClient<ISendbirdHttpClient, SendbirdHttpClient>()
				.AddPolicyHandler(Resilience.BuildRetryPolicy());

			return services;
		}
	}
}
