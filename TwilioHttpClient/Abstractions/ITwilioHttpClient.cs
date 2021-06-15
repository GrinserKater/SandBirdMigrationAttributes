using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Results;
using TwilioHttpClient.Models;

namespace TwilioHttpClient.Abstractions
{
    public interface ITwilioHttpClient
    {
	    IAsyncEnumerable<User> UserBulkRetrieveAsync(int pageSize, int? limit = null);
	    HttpClientResult<IEnumerable<User>> UserBulkRetrieve(int pageSize, int? limit = null);
	    IAsyncEnumerable<Channel> ChannelBulkRetrieveAsync(int pageSize, int? limit = null);
        Task<HttpClientResult<User>> UserFetchAsync(string userId);
        Task<HttpClientResult<Member[]>> ChannelMembersBulkRetrieveAsync(string channelUniqueIdentifier);
        IAsyncEnumerable<UserChannel> UserChannelsBulkRetrieveAsync(string userId, int pageSize, int? limit = null);
        Task<HttpClientResult<Channel>> ChannelFetchAsync(string channelUniqueIdentifier);
    }
}
