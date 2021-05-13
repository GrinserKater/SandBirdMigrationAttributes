using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Results;
using SendbirdHttpClient.Abstractions;
using SendbirdHttpClient.Models.User;
using TheGrandMigrator.Abstractions;
using TheGrandMigrator.Constants;
using TheGrandMigrator.Enums;
using TheGrandMigrator.Models;
using TheGrandMigrator.Utilities;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;
using SendbirdUserResource = SendbirdHttpClient.Models.User.UserResource;

namespace TheGrandMigrator
{
    public class Migrator : IMigrator
	{
        private readonly ITwilioHttpClient _twilioClient;
		private readonly ISendbirdHttpClient _sendbirdClient;

		public Migrator(ITwilioHttpClient twilioClient, ISendbirdHttpClient sendbirdClient)
		{
			_twilioClient   = twilioClient ?? throw new ArgumentNullException(nameof(twilioClient));
			_sendbirdClient = sendbirdClient ?? throw new ArgumentNullException(nameof(sendbirdClient));
        }

		public async Task<MigrationResult<IResource>> MigrateUsersAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;
            int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			Trace.WriteLine("Fetching users from Twilio in a bulk mode. This might take a couple of minutes...");
			HttpClientResult<IEnumerable<User>> twilioUsersResult = _twilioClient.UserBulkRetrieve(entitiesPerPage, migrateNoMoreThan);

			var result = new MigrationResult<IResource>();

			if (!twilioUsersResult.IsSuccess)
			{
				result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{twilioUsersResult.FormattedMessage}]; HTTP status code: [{twilioUsersResult.HttpStatusCode}]");
				return result;
			}

			// TODO: filtering can be done here based on the dateBefore and dateAfter parameter.
			// This will reduce the amount of iterations, but the logging must be adjusted appropriately. 
			foreach (User user in twilioUsersResult.Payload)
			{
				result.EntitiesFetched.Add(user);
                var userMigrationResult = await MigrateFetchedUserAsync(user, false, dateBefore, dateAfter);
				CopyMigrationResult(userMigrationResult, result, null);
			}

            result.Message = result.FailedCount == 0 ?
                $"Migration finished. Totally migrated {result.SuccessCount} users' attributes.":
                $"Not all users' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}

		public async Task<MigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;
            int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

            Trace.WriteLine("Fetching channels from Twilio in a bulk mode. This might take a couple of minutes...");
			HttpClientResult<List<Channel>> twilioChannelResult = await _twilioClient.ChannelBulkRetrieveAsync(entitiesPerPage, migrateNoMoreThan);

			var result = new MigrationResult<IResource>();

			if (!twilioChannelResult.IsSuccess)
			{
				result.Message = "Migration of channels' attributes failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{twilioChannelResult.FormattedMessage}]; HTTP status code: [{twilioChannelResult.HttpStatusCode}]");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

            // TODO: filtering can be done here based on the dateBefore and dateAfter parameter, and on member count.
            // This will reduce the amount of iterations, but the logging must be adjusted appropriately. 
			foreach (Channel channel in twilioChannelResult.Payload)
            {
                result.EntitiesFetched.Add(channel);

				MigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(channel, dateBefore, dateAfter);
				CopyMigrationResult(singleChannelMigrationResult, result, null);
            }

    		result.Message = result.FailedCount == 0 ?
				$"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.":
				$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}

		public async Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int accountUserId, int limit, int pageSize)
        {
			var result = new MigrationResult<IResource>();

			if (accountUserId <= 0)
            {
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"{accountUserId} is invalid.");
				return result;
			}

			// When migrating a single account we will always migrate a user, because they may have recent channels.
			MigrationResult<IResource> userMigrationResult = await MigrateSingleUserAttributesAsync(accountUserId.ToString(), false, null, null);
            if (userMigrationResult.FetchedCount == 0)
            {
                result.Message = "Migration of the account failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to migrate the user with ID {accountUserId}; reason: {userMigrationResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
			}

			CopyMigrationResult(userMigrationResult, result, null);

            if (userMigrationResult.FailedCount > 0)
			{
                result.Message = "Migration of the account failed. See ErrorMessages for details.";
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }

			MigrationResult<IResource> channelsMigrationResult = await MigrateSingleUserChannelsAttributesAsync(accountUserId.ToString(), limit, pageSize, dateBefore, dateAfter);
			
            CopyMigrationResult(channelsMigrationResult, result, $"{userMigrationResult.Message}; {channelsMigrationResult.Message}");
            return result;
        }

        public async Task<MigrationResult<IResource>> MigrateSingleChannelAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, string channelUniqueIdentifier)
        {
            Trace.WriteLine($"Fetching channel {channelUniqueIdentifier} from Twilio...");
			HttpClientResult<Channel> twilioChannelResult = await _twilioClient.ChannelFetchAsync(channelUniqueIdentifier);

            var result = new MigrationResult<IResource>();

            if (String.IsNullOrWhiteSpace(channelUniqueIdentifier))
            {
                result.Message = "Migration of the channel failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"{channelUniqueIdentifier} is invalid.");
                return result;
            }

			if (!twilioChannelResult.IsSuccess)
            {
                result.Message = $"Migration of attributes for the channel {channelUniqueIdentifier} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{twilioChannelResult.FormattedMessage}]; HTTP status code: [{twilioChannelResult.HttpStatusCode}]");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }

			Channel channel = twilioChannelResult.Payload;
            result.EntitiesFetched.Add(channel);

            MigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(channel, dateBefore, dateAfter);
            CopyMigrationResult(singleChannelMigrationResult, result, null);

            result.Message = result.FailedCount == 0 ?
				$"Migration finished. Channel {channelUniqueIdentifier} successfully migrated with attributes." :
				$"Migration of the channel {channelUniqueIdentifier} with attributes failed. See ErrorMessages for details.";
            return result;
        }

		private async Task<MigrationResult<IResource>> MigrateSingleUserAttributesAsync(string userId, bool blockExistentUsersOnly, DateTime? dateBefore, DateTime? dateAfter)
        {
			HttpClientResult<User> userFetchResult = await _twilioClient.UserFetchAsync(userId).ConfigureAwait(false);

			MigrationResult<IResource> result = new MigrationResult<IResource>();

            if (!userFetchResult.IsSuccess)
            {
                result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{userFetchResult.FormattedMessage}]; HTTP status code: [{userFetchResult.HttpStatusCode}]");
				// We will add a dummy user with the failed ID for the correct stats.
				result.EntitiesFailed.Add(new User { Id = userId });
                return result;
            }

			User user = userFetchResult.Payload;
            result.EntitiesFetched.Add(user);
			
            var userMigrationResult = await MigrateFetchedUserAsync(user, blockExistentUsersOnly, dateBefore, dateAfter);
			CopyMigrationResult(userMigrationResult, result, null);
            return result;
        }

		private async Task<MigrationResult<IResource>> MigrateFetchedUserAsync(User user, bool blockExistentUsersOnly, DateTime? dateBefore, DateTime? dateAfter)
		{
            Trace.WriteLine($"Migrating user {user.FriendlyName} with ID {user.Id} with {(blockExistentUsersOnly ? "only existent blockees":"all the blockees")} if any...");
			MigrationResult<IResource> result = new MigrationResult<IResource>();

            if(!IsIncludedByDate(user.DateUpdated, dateBefore, dateAfter))
			{
				Trace.WriteLine(
                    $"\tUser {user.FriendlyName} with ID {user.Id} skipped. Last updated on {user.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
				result.EntitiesSkipped.Add(user);
				return result;
			}

			var userUpsertRequestBody = new UserUpsertRequest
			{
				UserId = user.Id,
                Nickname = user.FriendlyName ?? String.Empty,
				ProfileImageUrl = user.ProfileImageUrl ?? String.Empty, // required by Sendbird
				Metadata = new UserMetadata(),
				IssueSessionToken = true
			};

			if (user.Attributes?.BlockedByAdminAt != null)
				userUpsertRequestBody.Metadata.BlockedByAdminAt = user.Attributes.BlockedByAdminAt.ToString();

			HttpClientResult<SendbirdUserResource> updateResult = await _sendbirdClient.UpdateUserAsync(userUpsertRequestBody);

			if (updateResult.HttpStatusCode == HttpStatusCode.NotFound)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} does not exist on SB side. Creating...");
				updateResult = await _sendbirdClient.CreateUserAsync(userUpsertRequestBody);
			}

			if (!updateResult.IsSuccess)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"Failed to create a user {user.FriendlyName} with ID {user.Id} on SB side. Reason: {updateResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			if (user.Attributes?.BlockedUsers == null || user.Attributes.BlockedUsers.Length == 0)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} has no blocked users.");
                result.EntitiesSucceeded.Add(user);
				return result;
			}

			Trace.WriteLine($"\tMigrating user blockages for the user {user.FriendlyName} with ID {user.Id}.");
			// We will now check who of the blockees does not exist on SB side.
			var nonExistentUsersResult = await _sendbirdClient.WhoIsAbsentAsync(user.Attributes.BlockedUsers);

			if (!nonExistentUsersResult.IsSuccess)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"Failed to query SB for blockeed of the user {user.FriendlyName} with ID {user.Id}. Reason: {nonExistentUsersResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			int[] usersToBlock = blockExistentUsersOnly ? user.Attributes.BlockedUsers.Except(nonExistentUsersResult.Payload).ToArray() : user.Attributes.BlockedUsers;
			if (usersToBlock.Length == 0)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} migrate successfuly. No blocked user currently exists. Be kind to each other :)");
				result.EntitiesSucceeded.Add(user);
				return result;
			}

			// Worst case. At least one of the users to block does not exist on SB's side.
            var atLeastOneBlockeeFailed = false;
			if (!blockExistentUsersOnly && nonExistentUsersResult.Payload.Length > 0)
            {
                Trace.WriteLine($"\tOne or more of the blockees of the user {user.FriendlyName} do not exist on SB side. Creating...");
				MigrationResult<IResource> blockeeMigrationResult = null;
                foreach (int id in nonExistentUsersResult.Payload)
                {
                    Trace.WriteLine($"\tMigrating blockee with the id {id}...");
                    // Yes, this is recursion. And we will migrate the blockees even if they are old enough.
                    blockeeMigrationResult = await MigrateSingleUserAttributesAsync(id.ToString(), true, null, null);
                }
				// ReSharper disable once PossibleNullReferenceException
                if (blockeeMigrationResult.FailedCount > 0) atLeastOneBlockeeFailed = true;
				CopyMigrationResult(blockeeMigrationResult, result, null);
			}

            HttpClientResult<List<UserResource>> blockageResult = await _sendbirdClient.BlockUsersBulkAsync(Int32.Parse(user.Id), usersToBlock);
			if (!blockageResult.IsSuccess)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"\tFailed to migrate blockages for the user {user.FriendlyName} with ID {user.Id}. Reason: {blockageResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			// Even if one or several of the blockees failed to migrate, we consider the "main" user success.
			string finalMessage = atLeastOneBlockeeFailed ?
				$"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully, but some or all of the blockees failed.":
                $"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully with the blockees.";
			Trace.WriteLine(finalMessage);
			result.EntitiesSucceeded.Add(user);
			return result;
		}

		private async Task<MigrationResult<IResource>> MigrateSingleUserChannelsAttributesAsync(string userId, int limit, int pageSize, DateTime? dateBefore, DateTime? dateAfter)
		{
			int? migrateNoMoreThan = null;
			if (limit > 0) migrateNoMoreThan = limit;
			int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			var result = new MigrationResult<IResource>();

			// Unfortunately, smart Twilio engineers decided to return absolutely different object as UserChannelResource rather than ChannelResource.
			HttpClientResult<List<UserChannel>> userChannelsFetchResult = await _twilioClient.UserChannelsBulkRetrieveAsync(userId, entitiesPerPage, migrateNoMoreThan);
			if (!userChannelsFetchResult.IsSuccess)
			{
				result.Message = $"Migration of account's attributes for the ID {userId} failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{userChannelsFetchResult.FormattedMessage}]; HTTP status code: [{userChannelsFetchResult.HttpStatusCode}]");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			if (userChannelsFetchResult.Payload.Count == 0)
			{
				result.Message = $"No channels attributes for the account ID {userId} to migrate.";
				return result;
			}

			// This piece of code might seem very similar to the one in MigrateChannelsAttributesAsync,
			// but this one is slightly optimised for the single user case.
			foreach (UserChannel userChannel in userChannelsFetchResult.Payload)
			{
				HttpClientResult<Channel> channelFetchResult = await _twilioClient.ChannelFetchAsync(userChannel.ChannelSid);
				if (!channelFetchResult.IsSuccess)
				{
					result.ErrorMessages.Add(
						$"Failed to retrieve channel with SID {userChannel.ChannelSid}; reason: {channelFetchResult.FormattedMessage}; HTTP status code: {channelFetchResult.HttpStatusCode}.");
					Debug.WriteLine(result.ErrorMessages.Last());
					continue;
				}

				var channel = channelFetchResult.Payload;
                result.EntitiesFetched.Add(channel);
				if(!IsIncludedByDate(channel.DateUpdated, dateBefore, dateAfter))
                {
					Trace.WriteLine($"\tChannel {channel.UniqueName} skipped. Last updated on {channel.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
					result.EntitiesSkipped.Add(channel);
					continue;
				}

				List<int> channelMembersIds = new List<int>{ Int32.Parse(userId) };

				if (channel.MembersCount == 2)
				{
					int secondChannelMember = Int32.Parse(channel.UniqueName.Split('-').Skip(1).First(id => channelMembersIds.All(cmi => cmi != Int32.Parse(id))));

					// Checking if the channel member exists as SB users. If not, we'll try to migrate them. If migration fails we'll still proceed.
					// In this case channel will simply be created with the members that already exist.
					HttpClientResult<int[]> absentMembersResult = await _sendbirdClient.WhoIsAbsentAsync(new[] { secondChannelMember });
					if (absentMembersResult.IsSuccess && absentMembersResult.Payload.Length > 0)
					{
						Trace.WriteLine($"Migrating the nonexistent member with ID {secondChannelMember} of the channel {channel.UniqueName}...");
						// Channel's member will be migrated disregarding the age. 
						var memberMigrationResult = await MigrateSingleUserAttributesAsync(secondChannelMember.ToString(), true, null, null);
                        if (memberMigrationResult.FetchedCount == 0)
                        {
                            Trace.WriteLine(
                                $"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
                            result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
                        }

						if (memberMigrationResult.FailedCount > 0)
						{
							Trace.WriteLine(
								$"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
							result.EntitiesFailed.AddRange(memberMigrationResult.EntitiesFailed);
                            result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
						}
                        else
                        {
                            Trace.WriteLine($"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} succeeded.");
							result.EntitiesSucceeded.AddRange(memberMigrationResult.EntitiesSucceeded);
                        }

						channelMembersIds.Add(secondChannelMember);
					}
				}

				var channelMigrationResult =  await MigrateChannelWithMetadataAsync(channel, channelMembersIds.ToArray());
				CopyMigrationResult(channelMigrationResult, result, null);
            }

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.";
			return result;
		}

		private async Task<MigrationResult<IResource>> MigrateChannelWithMetadataAsync(Channel channel, int[] channelMembersIds)
		{
			Trace.WriteLine($"Migrating channel {channel.UniqueName}...");
			var result = new MigrationResult<IResource>();

			OperationResult operationResult =
				await MigrationUtilities.TryCreateOrUpdateChannelWithMetadataAsync(_sendbirdClient, channel, channelMembersIds, result);

			switch (operationResult)
			{
				case OperationResult.Failure:
					result.EntitiesFailed.Add(channel);
					Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate.");
					return result;
				case OperationResult.Success:
					result.EntitiesSucceeded.Add(channel);
					Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
					return result;
				case OperationResult.Continuation:
					break;
				default:
					return result;
			}

			operationResult = await MigrationUtilities.TryUpdateOrCreateChannelMetadataAsync(_sendbirdClient, channel, result);

			if (operationResult != OperationResult.Success)
			{
				result.EntitiesFailed.Add(channel);
				Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate. Failed to migrate metadata.");
				return result;
			}

			result.EntitiesSucceeded.Add(channel);
			Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
			return result;
		}

        private static void CopyMigrationResult(MigrationResult<IResource> from, /* mutable */ MigrationResult<IResource> to, string customMessage)
        {
			if (from == null || to == null) throw new ArgumentNullException();

			to.Message = customMessage ?? from.Message;
            to.ErrorMessages.AddRange(from.ErrorMessages);

			to.EntitiesFetched.AddRange(from.EntitiesFetched);
            to.EntitiesFailed.AddRange(from.EntitiesFailed);
            to.EntitiesSucceeded.AddRange(from.EntitiesSucceeded);
            to.EntitiesSkipped.AddRange(from.EntitiesSkipped);
        }
        private bool IsIncludedByDate(DateTime? reference, DateTime? before, DateTime? after)
        {
			if (reference == null || before == null && after == null) return true;
			// Intersection (reference is IN the date interval).
            if (after <= before) return after <= reference && reference <= before;
			// Exclusion (reference is either older than before or younger than after).
            return reference <= before || reference >= after;
        }

        private async Task<MigrationResult<IResource>> MigrateSingleChannelAttributesAsync(Channel channel, DateTime? dateBefore, DateTime? dateAfter)
        {
			var result = new MigrationResult<IResource>();

			if (!IsIncludedByDate(channel.DateUpdated, dateBefore, dateAfter))
			{
				Trace.WriteLine($"\tChannel {channel.UniqueName} skipped. Last updated on {channel.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
				result.EntitiesSkipped.Add(channel);
				return result;
			}

			if (channel.MembersCount == 0)
			{
				Trace.WriteLine($"Channel {channel.UniqueName} contained no members. Skipped.");
				result.EntitiesSkipped.Add(channel);
				return result;
			}

			if (channel.Attributes != null && (channel.Attributes.ListingId == 0 || channel.Attributes.SellerId == 0 && channel.Attributes.BuyerId == 0))
			{
				Trace.WriteLine(
					$"Channel {channel.UniqueName} contained uncertain data in the attributes. Listing ID: [{channel.Attributes.ListingId}]; buyer ID [{channel.Attributes.BuyerId}]; seller ID: [{channel.Attributes.SellerId}]. Skipped.");
				result.EntitiesSkipped.Add(channel);
				return result;
			}

			int[] channelMembersIds;
			if (channel.MembersCount == 1)
			{
				// We could create a Fetch method to fetch a single member, but bulk retrieve is no difference.
				HttpClientResult<Member[]> channelMemberResult = await _twilioClient.ChannelMembersBulkRetrieveAsync(channel.UniqueName);
				// If request to Twilio is successful, we will know the only member; if not, we'll proceed with empty array:
				// better not to add members to channel with single member at all rather then to re-add the removed one.
				if (!channelMemberResult.IsSuccess)
				{
					Trace.WriteLine($"Failed to fetch members from Twilio for the channel {channel.UniqueName}. Reason: {channelMemberResult.FormattedMessage}.");
					channelMembersIds = Array.Empty<int>();
				}
				else channelMembersIds = channelMemberResult.Payload.Select(m => Int32.TryParse(m.Id, out int id) ? id : 0).ToArray();
            }
			else channelMembersIds = channel.UniqueName.Split('-').Skip(1).Select(Int32.Parse).ToArray();

			// Checking if channel members exist as SB users. If not, we'll try to migrate them. If migration fails we'll still proceed.
			// In this case channel will simply be created with the members that already exist.
			HttpClientResult<int[]> absentMembersResult = await _sendbirdClient.WhoIsAbsentAsync(channelMembersIds);
			if (absentMembersResult.IsSuccess && absentMembersResult.Payload.Length > 0)
			{
				MigrationResult<IResource> memberMigrationResult = null;
				Trace.WriteLine($"Migrating the nonexistent members of the channel {channel.UniqueName}...");
				foreach (int memberId in absentMembersResult.Payload)
				{
					Trace.WriteLine($"\tMigrating the member with ID {memberId} of the channel {channel.UniqueName}...");
					// We won't pay attention to the date of creation when migrating channel's members.
					memberMigrationResult = await MigrateSingleUserAttributesAsync(memberId.ToString(), false, null, null);
					Trace.WriteLine(memberMigrationResult.FailedCount > 0
						? $"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}."
						: $"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} succeeded.");
				}
				CopyMigrationResult(memberMigrationResult, result, null);
			}

			var channelMigrationResult = await MigrateChannelWithMetadataAsync(channel, channelMembersIds);
			CopyMigrationResult(channelMigrationResult, result, null);
			return result;
		}
	}
}