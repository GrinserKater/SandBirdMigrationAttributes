using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Results;
using SendbirdHttpClient.Models.Channel;
using SendbirdHttpClient.Models.Common;
using SendbirdHttpClient.Models.User;

namespace SendbirdHttpClient.Abstractions
{
	public interface ISendbirdHttpClient
	{
		Task<HttpClientResult<UserResource>> CreateUserAsync(int userId, string profileImageUrl, string friendlyName, bool issueAccessToken = false, bool issueSessionToken = false, long? sessionTokenExpiresAt = null);
		Task<HttpClientResult<UserResource>> CreateUserAsync(UserUpsertRequest requestBody);
		Task<HttpClientResult<UserResource>> UpdateUserAsync(UserUpsertRequest requestBody);
		Task<HttpClientResult<List<UserResource>>> BlockUsersBulkAsync(int originatorUserId, int[] idsToBlock);
		Task<HttpClientResult<UserMetadata>> CreateUserMetadataAsync(int userId, MetadataUpsertRequest<UserMetadata> metadata);
		Task<HttpClientResult<UserMetadata>> UpdateUserMetadataAsync(int userId, MetadataUpsertRequest<UserMetadata> metadata);
		Task<HttpClientResult<ChannelMetadata>> CreateChannelMetadataAsync(string url, MetadataUpsertRequest<ChannelMetadata> metadata);
		Task<HttpClientResult<ChannelMetadata>> UpdateChannelMetadataAsync(string url, MetadataUpsertRequest<ChannelMetadata> metadata);
		Task<HttpClientResult<ChannelResource>> CreateChannelAsync(ChannelUpsertRequest requestBody);
		Task<HttpClientResult<ChannelResource>> UpdateChannelAsync(ChannelUpsertRequest requestBody);
		Task<HttpClientResult<ChannelResource>> FreezeChannelAsync(string url);
		Task<HttpClientResult<ChannelResource>> UnfreezeChannelAsync(string url);
		Task<HttpClientResult<ChannelResource>> AlterChannelFreezeAsync(string url, bool isFrozen);
		Task<HttpClientResult<int[]>> WhoIsAbsentAsync(int[] userIds);
	}
}
