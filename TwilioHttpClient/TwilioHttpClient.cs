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
using Twilio.Rest.Chat.V2.Service.User;
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
		private const int DefaultPageSize = 50;

		private readonly string _chatServiceId;
		private readonly TwilioRestClient _twilioRestClient;

		public TwilioHttpClient(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<TwilioOptions> options)
		{
			if (options == null) throw new ArgumentNullException(nameof(options));
			if (httpClient == null) throw new ArgumentNullException(nameof(options));

			var currentOptions = options.Value;

			if (String.IsNullOrWhiteSpace(currentOptions.AccountSid) || String.IsNullOrWhiteSpace(currentOptions.AuthToken))
				throw new ArgumentNullException(
					$"{nameof(currentOptions.AccountSid)} and/or {nameof(currentOptions.AuthToken)} is missing.");

			if (String.IsNullOrWhiteSpace(currentOptions.ChatServiceId))
				throw new ArgumentNullException($"{nameof(currentOptions.ChatServiceId)} is missing.");

			_chatServiceId = currentOptions.ChatServiceId;

			_twilioRestClient = new TwilioRestClient(currentOptions.AccountSid, currentOptions.AuthToken, httpClient: new SystemNetHttpClient(httpClient));
		}

		public async IAsyncEnumerable<User> UserBulkRetrieveAsync(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;
			
			ResourceSet<UserResource> userResources = await UserResource.ReadAsync(_chatServiceId, currentPageSize, limit, _twilioRestClient).ConfigureAwait(false);
			foreach (UserResource resource in userResources)
			{
				var user = new User
				{
					Id = resource.Identity,
					FriendlyName = resource.FriendlyName,
					Attributes = JsonSerializer.Deserialize<UserAttributes>(resource.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				};

				yield return user;
			}
		}

		public IEnumerable<User> UserBulkRetrieve(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			ResourceSet<UserResource> userResources = UserResource.Read(_chatServiceId, currentPageSize, limit, _twilioRestClient);

			foreach (UserResource resource in userResources)
			{
				var user = new User
				{
					Id           = resource.Identity,
					FriendlyName = resource.FriendlyName,
					DateCreated  = resource.DateCreated,
					DateUpdated  = resource.DateUpdated,
					Attributes   = JsonSerializer.Deserialize<UserAttributes>(resource.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				};

				yield return user;
			}
		}

		public async IAsyncEnumerable<Channel> ChannelBulkRetrieveAsync(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;
			
			var options = new ReadChannelOptions(_chatServiceId)
			{
				Limit = limit,
				PageSize = currentPageSize,
				Type = new List<ChannelResource.ChannelTypeEnum> { ChannelResource.ChannelTypeEnum.Private }
			};

			ResourceSet<ChannelResource> channelResources = await ChannelResource.ReadAsync(options, _twilioRestClient).ConfigureAwait(false);
			foreach (var resource in channelResources)
			{
				var channel = new Channel
				{
					Sid = resource.Sid,
					UniqueName = resource.UniqueName,
					FriendlyName = resource.FriendlyName,
					MembersCount = resource.MembersCount ?? 0,
					DateCreated = resource.DateCreated,
					DateUpdated = resource.DateUpdated,
					Attributes = JsonSerializer.Deserialize<ChannelAttributes>(resource.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				};
				yield return channel;
			}
		}
		
		public IEnumerable<Channel> ChannelBulkRetrieve(int pageSize, int? limit = null)
		{
			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;
			
			var options = new ReadChannelOptions(_chatServiceId)
			{
				Limit = limit,
				PageSize = currentPageSize,
				Type = new List<ChannelResource.ChannelTypeEnum> { ChannelResource.ChannelTypeEnum.Private }
			};

			ResourceSet<ChannelResource> channelResources = ChannelResource.Read(options, _twilioRestClient);
			foreach (var resource in channelResources)
			{
				var channel = new Channel
				{
					Sid = resource.Sid,
					UniqueName = resource.UniqueName,
					FriendlyName = resource.FriendlyName,
					MembersCount = resource.MembersCount ?? 0,
					DateCreated = resource.DateCreated,
					DateUpdated = resource.DateUpdated,
					Attributes = JsonSerializer.Deserialize<ChannelAttributes>(resource.Attributes, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				};
				yield return channel;
			}
		}

		public async Task<HttpClientResult<Channel>> ChannelFetchAsync(string channelUniqueIdentifier)
		{
			if (String.IsNullOrWhiteSpace(channelUniqueIdentifier))
				return new HttpClientResult<Channel>(HttpStatusCode.BadRequest, $"Invalid {nameof(channelUniqueIdentifier)} value: [{channelUniqueIdentifier}]");

			try
			{
				ChannelResource fetchResult = await ChannelResource.FetchAsync(_chatServiceId, channelUniqueIdentifier, _twilioRestClient).ConfigureAwait(false);

				var payload = new Channel
				{
					Sid = fetchResult.Sid,
					UniqueName = fetchResult.UniqueName,
					FriendlyName = fetchResult.FriendlyName,
					MembersCount = fetchResult.MembersCount ?? 0,
					DateCreated = fetchResult.DateCreated,
					DateUpdated = fetchResult.DateUpdated,
					Attributes = CustomJsonSerializer.DeserializeFromString<ChannelAttributes>(fetchResult.Attributes)
				};

				return new HttpClientResult<Channel>(HttpStatusCode.OK, payload);
			}
			catch (Exception ex)
			{
				return ProcessException<Channel>(ex, nameof(ChannelFetchAsync));
			}
		}

		public async Task<HttpClientResult<User>> UserFetchAsync(string userId)
		{
			if (!Int32.TryParse(userId, out int userIdAsInt) || userIdAsInt <= 0)
                return new HttpClientResult<User>(HttpStatusCode.BadRequest, $"Invalid {nameof(userId)} value: [{userId}]");

            try
            {
                UserResource fetchResult = await UserResource.FetchAsync(_chatServiceId, userId, _twilioRestClient).ConfigureAwait(false);
                var payload = new User
				{
					Id = fetchResult.Identity,
					FriendlyName = fetchResult.FriendlyName,
					DateCreated = fetchResult.DateCreated,
					DateUpdated = fetchResult.DateUpdated,
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
                ResourceSet<MemberResource> readResult =
	                await MemberResource.ReadAsync(_chatServiceId, channelUniqueIdentifier, client: _twilioRestClient).ConfigureAwait(false);
                var payload = readResult.Select(mr => new Member { Id = mr.Identity }).ToArray();

                return new HttpClientResult<Member[]>(HttpStatusCode.OK, payload);
            }
			catch (Exception ex)
            {
                return ProcessException<Member[]>(ex, nameof(ChannelMembersBulkRetrieveAsync));
            }
		}

		public async IAsyncEnumerable<UserChannel> UserChannelsBulkRetrieveAsync(string userId, int pageSize, int? limit = null)
		{
			if (!Int32.TryParse(userId, out int userIdAsInt) || userIdAsInt <= 0)
				yield break;

			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			var options = new ReadUserChannelOptions(_chatServiceId, userId)
			{
				PageSize = currentPageSize,
				Limit = limit
			};
			ResourceSet<UserChannelResource> userChannelResources = await UserChannelResource.ReadAsync(options, _twilioRestClient);
			foreach (UserChannelResource resource in userChannelResources)
			{
				var userChannel = new UserChannel { ChannelSid = resource.ChannelSid };
				yield return userChannel;
			}
		}
		
		public IEnumerable<UserChannel> UserChannelsBulkRetrieve(string userId, int pageSize, int? limit = null)
		{
			if (!Int32.TryParse(userId, out int userIdAsInt) || userIdAsInt <= 0)
				yield break;

			int currentPageSize = pageSize > 0 ? pageSize : DefaultPageSize;

			var options = new ReadUserChannelOptions(_chatServiceId, userId)
			{
				PageSize = currentPageSize,
				Limit = limit
			};
			ResourceSet<UserChannelResource> userChannelResources = UserChannelResource.Read(options, _twilioRestClient);
			foreach (UserChannelResource resource in userChannelResources)
			{
				var userChannel = new UserChannel { ChannelSid = resource.ChannelSid };
				yield return userChannel;
			}
		}

        private HttpClientResult<T> ProcessException<T>(Exception ex, string methodName, params string[] extraInfo) where T : class
		{
			string loggedMessage = $"[{nameof(TwilioHttpClient)}.{methodName}]: {{0}}";

			string originalMessage = String.Empty;

			HttpStatusCode httpStatusCode;

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
