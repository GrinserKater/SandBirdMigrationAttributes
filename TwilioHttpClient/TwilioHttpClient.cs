using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Json;
using Common.Results;
using Twilio.Base;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Http;
using Twilio.Rest.Chat.V2.Service;
using Twilio.Rest.Chat.V2.Service.Channel;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;
using TwilioHttpClient.Models.Attributes.Channel;
using TwilioHttpClient.Models.Attributes.User;
using TwilioHttpClient.Options;
using HttpClient = System.Net.Http.HttpClient;

namespace TwilioHttpClient
{
	public class TwilioHttpClient : ITwilioHttpClient
	{
		private const string ApiBaseUrl = "https://chat.twilio.com/v2";
		private const int DefaultPageSize = 50;

		private readonly string _chatServiceId;
		private readonly TwilioOptions _options;
		private readonly TwilioRestClient _twilioRestClient;

		public TwilioHttpClient(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<TwilioOptions> options)
		{
			if (options == null) throw new ArgumentNullException(nameof(options));
			if (httpClient == null) throw new ArgumentNullException(nameof(options));

			_options = options.Value;

			if (String.IsNullOrWhiteSpace(_options.AccountSid) || String.IsNullOrWhiteSpace(_options.AuthToken))
				throw new ArgumentNullException(
					$"{nameof(_options.AccountSid)} and/or {nameof(_options.AuthToken)} is missing.");

			if (String.IsNullOrWhiteSpace(_options.ChatServiceId))
				throw new ArgumentNullException($"{nameof(_options.ChatServiceId)} is missing.");

			_chatServiceId = _options.ChatServiceId;

			_twilioRestClient = new TwilioRestClient(_options.AccountSid, _options.AuthToken, httpClient: new SystemNetHttpClient(httpClient));
		}

		public async Task<HttpClientResult<List<User>>> UserBulkRetrieveAsync(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			try
			{
				ResourceSet<UserResource> userResources = await UserResource.ReadAsync(_chatServiceId, currentPageSize, limit, _twilioRestClient).ConfigureAwait(false);

				var result = userResources.Select(ur => new User
				{
					Id = ur.Identity,
					FriendlyName = ur.FriendlyName,
					Attributes = JsonSerializer.Deserialize<UserAttributes>(ur.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				}).ToList();

				return new HttpClientResult<List<User>>(HttpStatusCode.OK, result);
			}
			catch (Exception ex)
			{
				return ProcessException<List<User>>(ex, nameof(UserBulkRetrieveAsync));
			}
		}

		public HttpClientResult<IEnumerable<User>> UserBulkRetrieve(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			try
			{
				ResourceSet<UserResource> userResources = UserResource.Read(_chatServiceId, currentPageSize, limit, _twilioRestClient);

				var result = userResources.Select(ur => new User
				{
					Id = ur.Identity,
					FriendlyName = ur.FriendlyName,
					Attributes = JsonSerializer.Deserialize<UserAttributes>(ur.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				});

				return new HttpClientResult<IEnumerable<User>>(HttpStatusCode.OK, result);
			}
			catch (Exception ex)
			{
				return ProcessException<IEnumerable<User>>(ex, nameof(UserBulkRetrieve));
			}
		}

		public async Task<HttpClientResult<List<Channel>>> ChannelBulkRetrieveAsync(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			try
			{
				var options = new ReadChannelOptions(_chatServiceId)
				{
					Limit = limit,
					PageSize = currentPageSize,
					Type = new List<ChannelResource.ChannelTypeEnum> { ChannelResource.ChannelTypeEnum.Private }
				};

				ResourceSet<ChannelResource> channelResources = await ChannelResource.ReadAsync(options, _twilioRestClient).ConfigureAwait(false);

				var result = channelResources.Select(cr => new Channel
				{
					UniqueName = cr.UniqueName,
					FriendlyName = cr.FriendlyName,
					MembersCount = cr.MembersCount ?? 0,
					Attributes = JsonSerializer.Deserialize<ChannelAttributes>(cr.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				}).ToList();

				return new HttpClientResult<List<Channel>>(HttpStatusCode.OK, result);
			}
			catch (Exception ex)
			{
				return ProcessException<List<Channel>>(ex, nameof(ChannelBulkRetrieveAsync));
			}
		}

		public async Task<HttpClientResult<User>> UserFetchAsync(string userId)
		{
			if (!Int32.TryParse(userId, out int userIdAsInt) || userIdAsInt <= 0)
                return new HttpClientResult<User>(HttpStatusCode.BadRequest, $"Invalid {nameof(userId)} value: [{userId}]");

            try
            {
                UserResource fetchResult = await UserResource.FetchAsync(_chatServiceId, userId).ConfigureAwait(false);

				var payload = new User
				{
					Id = fetchResult.Identity,
					FriendlyName = fetchResult.FriendlyName,
					Attributes = CustomJsonSerializer.DeserializeFromString<UserAttributes>(fetchResult.Attributes)
				};

				return new HttpClientResult<User>(HttpStatusCode.OK, payload);
            }
			catch(Exception ex)
            {
				return ProcessException<User>(ex, nameof(UserFetchAsync));
            }
        }

		public async Task<HttpClientResult<Member[]>> ChannelMembersBulkRetrieveAsync(string channelUniqueIdentifier)
        {
            if (String.IsNullOrWhiteSpace(channelUniqueIdentifier))
                return new HttpClientResult<Member[]>(HttpStatusCode.BadRequest, $"Invalid {nameof(channelUniqueIdentifier)} value: [{channelUniqueIdentifier}]");

            try
            {
                ResourceSet<MemberResource> readResult = await MemberResource.ReadAsync(_chatServiceId, channelUniqueIdentifier, client: _twilioRestClient);
                var payload = readResult.Select(mr => new Member { Id = mr.Identity }).ToArray();

                return new HttpClientResult<Member[]>(HttpStatusCode.OK, payload);
            }
			catch (Exception ex)
            {
                return ProcessException<Member[]>(ex, nameof(ChannelMembersBulkRetrieveAsync));
            }
		}

        private HttpClientResult<T> ProcessException<T>(Exception ex, string methodName, params string[] extraInfo) where T : class
		{
			string loggedMessage = $"[{nameof(TwilioHttpClient)}.{methodName}]: {{0}}";

			string originalMessage = String.Empty;

			HttpStatusCode httpStatusCode = 0;

			switch (ex)
			{
				// case when API returns null as a response.
				case ApiConnectionException ace when ace.Message == "Connection Error: No response received.":
					loggedMessage = String.Format(loggedMessage, "Twilio API simply returned null as a response.This means that either a requested resource is not found, or Twilio is experiencing issues.");
					originalMessage = ace.Message;
					httpStatusCode = HttpStatusCode.ServiceUnavailable;
					break;
				case ApiConnectionException ace:
					loggedMessage = String.Format(loggedMessage, $"Connection to the Twilio API could not be established. Exception: [{ace}].");
					originalMessage = ace.Message;
					httpStatusCode = HttpStatusCode.ServiceUnavailable;
					break;
				case ApiException ae:
					loggedMessage = String.Format(loggedMessage, $"Twilio helper library has thrown an API exception when executing the method. Exception: [{ae}].");
					originalMessage = ae.Message;
					httpStatusCode = (HttpStatusCode)ae.Status;
					break;
				case AuthenticationException authE:
					loggedMessage = String.Format(loggedMessage, $"Authentication failed when exectuing the method. Exception: [{authE}].");
					originalMessage = authE.Message;
					httpStatusCode = HttpStatusCode.Unauthorized;
					break;
				case AggregateException aggE:
					StringBuilder sb = new StringBuilder(String.Format(loggedMessage, "Aggregated exception occured. It contains the following exceptions:"));
					aggE.Handle(innerEx =>
					{
						sb.AppendLine($"Inner exception: [{innerEx}]");
						return true;
					});
					loggedMessage = sb.ToString();
					httpStatusCode = HttpStatusCode.InternalServerError;
					break;
				default:
					loggedMessage = String.Format(loggedMessage, $"something very bad has happend; we don't know, what that is... Yet... Exception: [{ex}].");
					httpStatusCode = HttpStatusCode.InternalServerError;
					break;
			}

			if (!extraInfo.Any())
				return new HttpClientResult<T>(httpStatusCode, loggedMessage, originalMessage);

			StringBuilder result = new StringBuilder($"{loggedMessage}. Extra information:");

			for (int i = 0; i < extraInfo.Length; i++)
				result.AppendLine(extraInfo[i]);

			return new HttpClientResult<T>(httpStatusCode, result.ToString(), originalMessage);
		}

	}
}
