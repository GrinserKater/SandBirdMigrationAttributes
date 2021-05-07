using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Results;
using SendbirdHttpClient.Abstractions;
using SendbirdHttpClient.Models.Channel;
using SendbirdHttpClient.Models.Common;
using TheGrandMigrator.Enums;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;

namespace TheGrandMigrator.Utilities
{
	public static class MigrationUtilities
	{
		public static ChannelData BuildChannelData(Channel channel)
		{
			if(channel == null) return null;

			var channelData = new ChannelData
			{
				IsListingBlocked = channel.Attributes.IsListingBlocked
			};

			if (channel.Attributes.ChannelDeletedBy?.Length > 0)
			{
				channelData.ChannelDeletedBy = channel.Attributes.ChannelDeletedBy.Select(db => new DeletedByData
				{
					Date = db.Date,
					UserId = db.UserId
				}).ToArray();
			}

			if (channel.Attributes.Listing == null) return channelData;

			channelData.Listing = new ListingData
			{
				Id    = channel.Attributes.Listing.Id,
				Title = channel.Attributes.Listing.Title,
				State = channel.Attributes.Listing.State
			};

			if (channel.Attributes.Listing.FormattedPrice != null && channel.Attributes.Listing.FormattedPrice.Count > 0)
			{
				var formattedPriceData = new FormattedInfoData
				{
					Fr = channel.Attributes.Listing.FormattedPrice.TryGetValue("fr", out string frenchPrice) ? frenchPrice : null,
					De = channel.Attributes.Listing.FormattedPrice.TryGetValue("de", out string germanPrice) ? germanPrice : null,
					It = channel.Attributes.Listing.FormattedPrice.TryGetValue("it", out string italianPrice) ? italianPrice : null
				};
				channelData.Listing.FormattedPrice = formattedPriceData;
			}

			if (channel.Attributes.Listing.Location == null || channel.Attributes.Listing.Location.Count == 0) return channelData;

			var formattedLocationData = new FormattedInfoData
			{
				Fr = channel.Attributes.Listing.Location.TryGetValue("fr", out string frenchLocation) ? frenchLocation : null,
				De = channel.Attributes.Listing.Location.TryGetValue("de", out string germanLocation) ? germanLocation : null,
				It = channel.Attributes.Listing.Location.TryGetValue("it", out string italianLocation) ? italianLocation : null
			};

			channelData.Listing.Location = formattedLocationData;

			return channelData;
		}

		public static string ConvertToChannelUrl(string channelUniqueName)
		{
			return String.IsNullOrWhiteSpace(channelUniqueName) ? null : channelUniqueName.Replace('-', '_');
		}

		public static void ProcessAndLogFailure(string message, Channel channel, /* Mutable */MigrationResult<IResource> result)
		{
			if(String.IsNullOrWhiteSpace(message) || channel == null || result == null) return;

			result.EntitiesFailed.Add(channel);
			result.ErrorMessages.Add(message);
			Debug.WriteLine(message);
		}

		public static ChannelUpsertRequest BuildChannelUpsertRequestBody(Channel channel, int[] channelMembersIds)
		{
			if(channel?.Attributes == null) return null;

			var channelUpsertRequest = new ChannelUpsertRequest
			{
				ChannelUrl = ConvertToChannelUrl(channel.UniqueName),
                CreatedBy  = channel.Attributes.BuyerId,
				Name       = channel.FriendlyName,
				CoverUrl   = channel.Attributes.Listing?.MainPicture ?? String.Empty
			};

			channelUpsertRequest.UserIds = channelMembersIds ?? Array.Empty<int>();
			channelUpsertRequest.Data = BuildChannelData(channel);

			return channelUpsertRequest;
		}

		public static async Task<OperationResult> TryCreateOrUpdateChannelWithMetadataAsync(ISendbirdHttpClient sendbirdClient, Channel channel, int[] channelMembersIds,
			/* Mutable */ MigrationResult<IResource> result)
		{
			if(sendbirdClient == null) throw new ArgumentNullException(nameof(sendbirdClient));
            if(channel == null || result == null) return OperationResult.Failure;

            ChannelUpsertRequest channelUpsertRequest = BuildChannelUpsertRequestBody(channel, channelMembersIds);

			HttpClientResult<ChannelResource> channelManipulationResult = await sendbirdClient.CreateChannelAsync(channelUpsertRequest);

			if (!channelManipulationResult.IsSuccess && channelManipulationResult.HttpStatusCode != HttpStatusCode.Conflict)
			{
				ProcessAndLogFailure(
					$"\tFailed to create channel {channelUpsertRequest.ChannelUrl} on SB side. Reason: {channelManipulationResult.FormattedMessage}.",
					channel, result);
				return OperationResult.Failure;
			}

			var metadataRequestBody = new MetadataUpsertRequest<ChannelMetadata>
			{
				Metadata = new ChannelMetadata { ListingId = channel.Attributes.ListingId.ToString() }
			};

			if (channelManipulationResult.IsSuccess)
			{
				if (channel.Attributes.IsBlocked || channel.Attributes.IsListingBlocked)
				{
					HttpClientResult<ChannelResource> freezeResult = await sendbirdClient.FreezeChannelAsync(channelUpsertRequest.ChannelUrl);

					if (!freezeResult.IsSuccess)
						ProcessAndLogFailure(
							$"\tFailed to freeze the channel {channelUpsertRequest.ChannelUrl} on SB side. Reason: {freezeResult.FormattedMessage}. Proceeding...",
							channel, result);
					// We deliberately proceed with migration here, even if we failed to freeze the channel.
				}

				HttpClientResult<ChannelMetadata> createMetadataResult = await sendbirdClient.CreateChannelMetadataAsync(channelUpsertRequest.ChannelUrl, metadataRequestBody);

				if (createMetadataResult.IsSuccess) return OperationResult.Success;

				ProcessAndLogFailure(
					$"\tFailed to create metadata for channel {channel.UniqueName} on SB side. Reason: {createMetadataResult.FormattedMessage}.",
					channel, result);
				return OperationResult.Failure;
			}

			Trace.WriteLine($"\tChannel {channelUpsertRequest.ChannelUrl} already exists on SB side. Updating...");

			channelManipulationResult = await sendbirdClient.UpdateChannelAsync(channelUpsertRequest);

			if (!channelManipulationResult.IsSuccess)
			{
				ProcessAndLogFailure(
					$"\tFailed to update channel {channelUpsertRequest.ChannelUrl} on SB side. Reason: {channelManipulationResult.FormattedMessage}.",
					channel, result);

				return OperationResult.Failure;
			}

			if (channelManipulationResult.Payload.Freeze == channelUpsertRequest.Freeze) return OperationResult.Continuation; /* Now we need to try to update or create metadata. */
			
			HttpClientResult<ChannelResource> alterFreezeResult =
				await sendbirdClient.AlterChannelFreezeAsync(channelUpsertRequest.ChannelUrl, channelUpsertRequest.Freeze);

			if (!alterFreezeResult.IsSuccess)
				ProcessAndLogFailure(
					$"\tFailed to {(channelUpsertRequest.Freeze ? "freeze" : "unfreeze")} the channel {channelUpsertRequest.ChannelUrl} on SB side. Reason: {alterFreezeResult.FormattedMessage}. Proceeding...",
					channel, result);
			// We deliberately proceed with migration here, even if we failed to freeze the channel.

			return OperationResult.Continuation; /* Now we need to try to update or create metadata. */
			}

		public static async Task<OperationResult> TryUpdateOrCreateChannelMetadataAsync(ISendbirdHttpClient sendbirdClient, Channel channel,
			/* Mutable */ MigrationResult<IResource> result)
		{
			if (sendbirdClient == null) throw new ArgumentNullException(nameof(sendbirdClient));

			if (channel == null || result == null) return OperationResult.Failure;

			string channelUrl = ConvertToChannelUrl(channel.UniqueName);

			var metadataRequestBody = new MetadataUpsertRequest<ChannelMetadata>
			{
				Metadata = new ChannelMetadata { ListingId = channel.Attributes.ListingId.ToString() }
			};

			HttpClientResult<ChannelMetadata> updateMetadataResult = await sendbirdClient.UpdateChannelMetadataAsync(channelUrl, metadataRequestBody);

			if (!updateMetadataResult.IsSuccess && updateMetadataResult.HttpStatusCode != HttpStatusCode.NotFound)
			{
				MigrationUtilities.ProcessAndLogFailure(
					$"\tFailed to update metadata for channel {channelUrl} on SB side. Reason: {updateMetadataResult.FormattedMessage} .",
					channel, result);
				return OperationResult.Failure;
			}

			if (updateMetadataResult.HttpStatusCode != HttpStatusCode.NotFound) return OperationResult.Success;

			Trace.WriteLine($"\tMetadata for the channel {channel.FriendlyName} does not exist on SB side. Creating...");

			HttpClientResult<ChannelMetadata> createMetadataResult = await sendbirdClient.CreateChannelMetadataAsync(channelUrl, metadataRequestBody);

			if (createMetadataResult.IsSuccess) return OperationResult.Success;

			ProcessAndLogFailure(
				$"\tFailed to create metadata for the channel {channel.FriendlyName} on SB side. Reason: {createMetadataResult.FormattedMessage}.",
				channel, result);

			return OperationResult.Failure;
		}
	}
}
