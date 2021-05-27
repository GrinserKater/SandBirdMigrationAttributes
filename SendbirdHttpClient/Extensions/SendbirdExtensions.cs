using System.IO;
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
                .SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile($"{Constants.Configuration.BasePath}\\{Constants.Configuration.FileName}")
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
