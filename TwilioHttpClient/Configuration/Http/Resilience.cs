using System;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;

namespace TwilioHttpClient.Configuration.Http
{
	internal class Resilience
	{
		private const byte MaxRetryAttempts = 3;

		public static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
		{
			return HttpPolicyExtensions
					.HandleTransientHttpError()
					.OrResult(m => (int)m.StatusCode == 429 /*Too many requests*/)
					.WaitAndRetryAsync(MaxRetryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
						retryAttempt)));
		}
	}
}
