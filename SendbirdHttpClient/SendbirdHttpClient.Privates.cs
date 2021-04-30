using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Common.Json;
using Common.Results;
using SendbirdHttpClient.Constants;
using SendbirdHttpClient.Http;
using SendbirdHttpClient.Models;
using SendbirdHttpClient.Models.Channel;
using SendbirdHttpClient.Models.Common;
using SendbirdHttpClient.Models.User;

namespace SendbirdHttpClient
{
	public partial class SendbirdHttpClient
	{ 
		
		private void BuildRestEndpointsDictionary()
		{
			_restEndpoints = new Dictionary<string, Uri>
			{
				{ Api.Endpoints.Users, new Uri(Api.Endpoints.Users, UriKind.Relative) },
				{ Api.Endpoints.Block, new Uri(Api.Endpoints.Block, UriKind.Relative) },
				{ Api.Endpoints.Channels, new Uri(Api.Endpoints.Channels, UriKind.Relative) },
				{ Api.GroupChannelType, new Uri(Api.GroupChannelType, UriKind.Relative) },
				{ Api.Endpoints.Metadata, new Uri(Api.Endpoints.Metadata, UriKind.Relative) },
				{ Api.Endpoints.Freeze, new Uri(Api.Endpoints.Freeze, UriKind.Relative) }
			};
		}

		private async Task<HttpClientResult<UserMetadata>> UpsertUserMetadataAsync(int userId, MetadataUpsertRequest<UserMetadata> metadata, HttpMethod method)
		{
			if (userId <= 0 || metadata?.Metadata == null || String.IsNullOrWhiteSpace(metadata.Metadata.BlockedByAdminAt))
				return new HttpClientResult<UserMetadata>(HttpStatusCode.BadRequest,
					"SendbirdHttpClient.UpsertUserMetadataAsync: invalid input parameters.");

			string requestUrl =
				$"{_restEndpoints[Api.Endpoints.Users]}/{userId}/{_restEndpoints[Api.Endpoints.Metadata]}";

			HttpClientResult<UserMetadata> result = await SendAsync<UserMetadata, MetadataUpsertRequest<UserMetadata>>(requestUrl, method, metadata);

			return result;
		}

		private async Task<HttpClientResult<ChannelMetadata>> UpsertChannelMetadataAsync(string channelUrl, MetadataUpsertRequest<ChannelMetadata> metadata, HttpMethod method)
		{
			if (String.IsNullOrWhiteSpace(channelUrl) || metadata?.Metadata == null ||
				!Int32.TryParse(metadata.Metadata.ListingId, out int listingId) || listingId <= 0)
				return new HttpClientResult<ChannelMetadata>(HttpStatusCode.BadRequest,
					$"SendbirdHttpClient.UpsertChannelMetadataAsync: invalid input parameters. channelUrl: [{channelUrl}], listingId [{metadata?.Metadata?.ListingId}].");

			string requestUrl =
				$"{_restEndpoints[Api.GroupChannelType]}/{channelUrl}/{_restEndpoints[Api.Endpoints.Metadata]}";

			HttpClientResult<ChannelMetadata> result =
				await SendAsync<ChannelMetadata, MetadataUpsertRequest<ChannelMetadata>>(requestUrl, method, metadata);

			return result;
		}

		private async Task<HttpClientResult<UserResource>> UpsertUserAsync(UserUpsertRequest requestBody, HttpMethod method)
		{
			if (!IsRequestBodyValid(requestBody, out string validationMessage))
				return new HttpClientResult<UserResource>(HttpStatusCode.BadRequest, $"SendbirdHttpClient.UpsertUserAsync: {validationMessage}.");

			string requestUrl = method == HttpMethod.Put ? $"{_restEndpoints[Api.Endpoints.Users]}/{requestBody.UserId}" :
				$"{_restEndpoints[Api.Endpoints.Users]}";

			HttpClientResult<UserResource> result = await SendAsync<UserResource, UserUpsertRequest>(requestUrl, method, requestBody);

			return result;
		}

		private async Task<HttpClientResult<ChannelResource>> UpsertChannelAsync(ChannelUpsertRequest requestBody, HttpMethod method)
		{
			if (String.IsNullOrWhiteSpace(requestBody?.ChannelUrl))
				return new HttpClientResult<ChannelResource>(HttpStatusCode.BadRequest, "SendbirdHttpClient.UpsertChannelAsync: invalid input parameters.");

			string requestUrl = method == HttpMethod.Put ? $"{_restEndpoints[Api.GroupChannelType]}/{requestBody.ChannelUrl}" :
				$"{_restEndpoints[Api.GroupChannelType]}";

			HttpClientResult<ChannelResource> result = await SendAsync<ChannelResource, ChannelUpsertRequest>(requestUrl, method, requestBody);

			return result;
		}

		private bool IsRequestBodyValid(UserUpsertRequest requestBody, out string message)
		{
			if (requestBody == null)
			{
				message = $"{nameof(requestBody)} cannot be null.";
				return false;
			}

			message = null;
			var validationMessage = new StringBuilder();

			if (String.IsNullOrWhiteSpace(requestBody.UserId))
				validationMessage.AppendLine($"{nameof(requestBody.UserId)} cannot be empty.");

			if (!Int32.TryParse(requestBody.UserId, out int userId) || userId <= 0)
				validationMessage.AppendLine($"{nameof(requestBody.UserId)} is invalid.");

			if (requestBody.ProfileImageUrl == null)
				validationMessage.AppendLine($"{nameof(requestBody.ProfileImageUrl)} cannot be null.");

			if (requestBody.PreferredLanguages != null && requestBody.PreferredLanguages.Length > 4)
				validationMessage.AppendLine($"{nameof(requestBody.PreferredLanguages)} supports up to 4 entries.");

			if (validationMessage.Length == 0) return true;

			message = validationMessage.ToString();

			return false;
		}

		private HttpClientResult<Failure> BuildFailureResult(string responseContent)
		{
			Failure failureResponse = CustomJsonSerializer.DeserializeFromString<Failure>(responseContent);

			return failureResponse == null ?
				new HttpClientResult<Failure>(HttpStatusCode.InternalServerError, "Failed to deserilase error response.") :
				new HttpClientResult<Failure>(ConvertErrorStatusCode((ErrorCodes)failureResponse.Code), failureResponse.Message);
		}

		private static HttpStatusCode ConvertErrorStatusCode(ErrorCodes errorCode)
		{
			switch (errorCode)
			{
				case ErrorCodes.ResourceNotFound:
					return HttpStatusCode.NotFound;
				case ErrorCodes.ResouceAlreadyExists:
					return HttpStatusCode.Conflict;
				case ErrorCodes.InvalidValue:
					return HttpStatusCode.BadRequest;
				default:
					return HttpStatusCode.InternalServerError;
			}
		}

		private void ConfigureHttpClient()
		{
			if (String.IsNullOrEmpty(_options.ApplicationId) || String.IsNullOrEmpty(_options.SecondaryToken))
				throw new ArgumentNullException(
					$"{nameof(_options.ApplicationId)} or {(_options.SecondaryToken)} is missing in the configuration.");

			_httpClient.BaseAddress = new Uri(String.Format(Api.BaseUrl, _options.ApplicationId));
			_httpClient.DefaultRequestHeaders.Add("Api-Token", _options.SecondaryToken);
		}

		private async Task<HttpClientResult<T>> SendAsync<T, T1>(Uri requestUrl, HttpMethod method, T1 requestBody) where T : class
		{
			using (var request = new HttpRequestMessage(method, requestUrl) {Content = new JsonContent(CustomJsonSerializer.Serialize(requestBody))})
			using (HttpResponseMessage response = await _httpClient.SendAsync(request))
			{
				string responseContent = await response.Content.ReadAsStringAsync();

				if (response.IsSuccessStatusCode)
					return new HttpClientResult<T>(response.StatusCode, CustomJsonSerializer.DeserializeFromString<T>(responseContent));

				HttpClientResult<Failure> errorResult = BuildFailureResult(responseContent);

				return errorResult.ShallowCopy<T>();
			}
		}

		private async Task<HttpClientResult<T>> SendAsync<T, T1>(string requestUrl, HttpMethod method, T1 requestBody) where T : class
		{
			return await SendAsync<T, T1>(new Uri(requestUrl, UriKind.Relative), method, requestBody);
		}

        private async Task<HttpClientResult<T>> SendAsync<T>(string requestUrl, HttpMethod method) where T : class
        {
            return await SendAsync<T, object>(new Uri(requestUrl, UriKind.Relative), method, null);
        }

		private async Task<HttpClientResult<ChannelResource>> UpdateChannelFreezeStateAsync(string url, bool isFrozen)
		{
			if (String.IsNullOrWhiteSpace(url))
				return new HttpClientResult<ChannelResource>(HttpStatusCode.BadRequest, "SendbirdHttpClient.UpdateChannelFreezeStateAsync: invalid input parameters.");

			string requestUrl = $"{_restEndpoints[Api.GroupChannelType]}/{url}/{_restEndpoints[Api.Endpoints.Freeze]}";

			var requestBody = new ChannelFreezeUpdateRequest { Freeze = isFrozen };

			HttpClientResult<ChannelResource> result = await SendAsync<ChannelResource, ChannelFreezeUpdateRequest>(requestUrl, HttpMethod.Put, requestBody);

			return result;
		}

        private async Task<HttpClientResult<UserResource[]>> FetchUsersByIdsAsync(int[] ids)
        {
            var userIdsAsString = String.Join(",", ids);

			string requestUrl = $"{_restEndpoints[Api.Endpoints.Users]}?{Api.Parameters.UserIds}={userIdsAsString}";

            HttpClientResult<UserResource[]> result = await SendAsync<UserResource[]>(requestUrl, HttpMethod.Get);

            return result;
        }
    }
}
