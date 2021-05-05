using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Results;
using Microsoft.Extensions.Options;
using SendbirdHttpClient.Abstractions;
using SendbirdHttpClient.Constants;
using SendbirdHttpClient.Models.Channel;
using SendbirdHttpClient.Models.Common;
using SendbirdHttpClient.Models.User;
using SendbirdHttpClient.Options;

namespace SendbirdHttpClient
{
	public partial class SendbirdHttpClient : ISendbirdHttpClient
	{ 
		private readonly HttpClient _httpClient;
		private readonly SendbirdOptions _options;

		private Dictionary<string, Uri> _restEndpoints;
		public SendbirdHttpClient(HttpClient httpClient, IOptions<SendbirdOptions> options)
		{
			if (options == null) throw new ArgumentNullException(nameof(options));

			_options = options.Value;

			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

			ConfigureHttpClient();

			BuildRestEndpointsDictionary();
		}

		public async Task<HttpClientResult<UserResource>> CreateUserAsync(int userId, string profileImageUrl, string friendlyName, bool issueAccessToken = false, bool issueSessionToken = false, long? sessionTokenExpiresAt = null)
		{
			if(userId <= 0)
				return new HttpClientResult<UserResource>(HttpStatusCode.BadRequest, $"SendbirdClientService.CreateUserAsync: invalid {nameof(userId)} [{userId}].");
			
			var requestBody = new UserUpsertRequest
			{
				UserId                        = userId.ToString(),
				ProfileImageUrl               = profileImageUrl ?? String.Empty, // requirement by Sendbird.
				Nickname                      = friendlyName,
				IssueAccessToken              = issueAccessToken,
				IssueSessionToken             = issueSessionToken,
				IsActive                      = true,
				Metadata                      = new UserMetadata(),
				QuitAllChannelsOnDeactivation = true
			};

			if(issueSessionToken && sessionTokenExpiresAt > 0)
				requestBody.SessionTokenExpiresAt = sessionTokenExpiresAt.Value;

			return await CreateUserAsync(requestBody);
		}

		public async Task<HttpClientResult<UserResource>> CreateUserAsync(UserUpsertRequest requestBody)
		{
			return await UpsertUserAsync(requestBody, HttpMethod.Post);
		}

		public async Task<HttpClientResult<UserResource>> UpdateUserAsync(UserUpsertRequest requestBody)
		{
			return await UpsertUserAsync(requestBody, HttpMethod.Put);
		}

		public async Task<HttpClientResult<List<UserResource>>> BlockUsersBulkAsync(int originatorUserId, int[] idsToBlock)
		{
			if(originatorUserId <= 0 || idsToBlock == null || idsToBlock.Length == 0)
				return new HttpClientResult<List<UserResource>>(HttpStatusCode.BadRequest,
					"SendbirdClientService.BlockUsersBulkAsync: invalid input parameters.");

			var requestBody = new UserBlockRequest
            {
                OriginatorUserId = originatorUserId,
				UserIdsToBlock = new int[idsToBlock.Length]
            };

			Array.Copy(idsToBlock, requestBody.UserIdsToBlock, idsToBlock.Length);

			return await BlockUsersBulkAsync(requestBody);
		}

		public async Task<HttpClientResult<List<UserResource>>> BlockUsersBulkAsync(UserBlockRequest requestBody)
		{
			if (requestBody == null || requestBody.OriginatorUserId <= 0 || requestBody.UserIdsToBlock?.Length == 0)
				return new HttpClientResult<List<UserResource>>(HttpStatusCode.BadRequest,
					"SendbirdClientService.BlockUsersBulkAsync: invalid input parameters.");

			string requestUrl =
				$"{_restEndpoints[Api.Endpoints.Users]}/{requestBody.OriginatorUserId}/{_restEndpoints[Api.Endpoints.Block]}";

			HttpClientResult<UsersSet> result = await SendAsync<UsersSet, UserBlockRequest>(requestUrl, HttpMethod.Post, requestBody);
			return !result.IsSuccess ? result.ShallowCopy<List<UserResource>>() : result.Convert(us => us.Users.ToList());
        }
		
		public async Task<HttpClientResult<UserMetadata>> CreateUserMetadataAsync(int userId, MetadataUpsertRequest<UserMetadata> metadata)
		{
			return await UpsertUserMetadataAsync(userId, metadata, HttpMethod.Post);
		}

		public async Task<HttpClientResult<UserMetadata>> UpdateUserMetadataAsync(int userId, MetadataUpsertRequest<UserMetadata> metadata)
		{
			return await UpsertUserMetadataAsync(userId, metadata, HttpMethod.Put);
		}

		public async Task<HttpClientResult<ChannelMetadata>> CreateChannelMetadataAsync(string url, MetadataUpsertRequest<ChannelMetadata> metadata)
		{
			return await UpsertChannelMetadataAsync(url, metadata, HttpMethod.Post);
		}

		public async Task<HttpClientResult<ChannelMetadata>> UpdateChannelMetadataAsync(string url, MetadataUpsertRequest<ChannelMetadata> metadata)
		{
			return await UpsertChannelMetadataAsync(url, metadata, HttpMethod.Put);
		}

		public async Task<HttpClientResult<ChannelResource>> CreateChannelAsync(ChannelUpsertRequest requestBody)
		{
			return await UpsertChannelAsync(requestBody, HttpMethod.Post);
		}

		public async Task<HttpClientResult<ChannelResource>> UpdateChannelAsync(ChannelUpsertRequest requestBody)
		{
			return await UpsertChannelAsync(requestBody, HttpMethod.Put);
		}

		public async Task<HttpClientResult<ChannelResource>> FreezeChannelAsync(string url)
		{
			return await UpdateChannelFreezeStateAsync(url, true);
		}

		public async Task<HttpClientResult<ChannelResource>> UnfreezeChannelAsync(string url)
		{
			return await UpdateChannelFreezeStateAsync(url, false);
		}

		public async Task<HttpClientResult<ChannelResource>> AlterChannelFreezeAsync(string url, bool isFrozen)
		{
			return await UpdateChannelFreezeStateAsync(url, isFrozen);
		}

		public async Task<HttpClientResult<int[]>> WhoIsAbsentAsync(int[] userIds)
		{
            if (userIds == null || userIds.Length == 0)
                return new HttpClientResult<int[]>(HttpStatusCode.BadRequest, "SendbirdHttpClient.WhoDoesNotExistAsync: invalid input parameters.");

			var fetchResult = await FetchUsersByIdsAsync(userIds);
			if (!fetchResult.IsSuccess) return fetchResult.ShallowCopy<int[]>();

			UserResource[] usersFound = fetchResult.Payload;
			int[] payload = userIds.Where(id => usersFound.All(uf => uf.UserId != id.ToString())).ToArray();

			return new HttpClientResult<int[]>(HttpStatusCode.OK, payload);
        }
	}
}
