using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
	[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    public class Migrator : IMigrator
	{
		private readonly string _successLogFileName = $"successfull_entities_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
		private readonly string _failedLogFileName = $"failed_entities_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
		private readonly ITwilioHttpClient _twilioClient;
		private readonly ISendbirdHttpClient _sendbirdClient;

		public Migrator(ITwilioHttpClient twilioClient, ISendbirdHttpClient sendbirdClient)
		{
			_twilioClient   = twilioClient ?? throw new ArgumentNullException(nameof(twilioClient));
			_sendbirdClient = sendbirdClient ?? throw new ArgumentNullException(nameof(sendbirdClient));
		}

		public async Task<MigrationResult<User>> MigrateUsersAttributesAsync(DateTime? laterThan, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;

			int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			HttpClientResult<IEnumerable<User>> twilioUsersResult = _twilioClient.UserBulkRetrieve(entitiesPerPage, migrateNoMoreThan);

			var result = new MigrationResult<User>();

			if (!twilioUsersResult.IsSuccess)
			{
				result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{twilioUsersResult.FormattedMessage}]; HTTP status code: [{twilioUsersResult.HttpStatusCode}]");
				return result;
			}

			foreach (User user in twilioUsersResult.Payload)
			{
				result.EntitiesFetched.Add(user);

				await MigrateFetchedUserAsync(user, false, laterThan, result);
			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all users' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} users' attributes.";
			return result;
		}

		public async Task<MigrationResult<Channel>> MigrateChannelsAttributesAsync(DateTime? laterThan, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;

			int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			HttpClientResult<List<Channel>> twilioChannelResult = await _twilioClient.ChannelBulkRetrieveAsync(entitiesPerPage, migrateNoMoreThan);

			var result = new MigrationResult<Channel>();

			if (!twilioChannelResult.IsSuccess)
			{
				result.Message = "Migration of channels' attributes failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{twilioChannelResult.FormattedMessage}]; HTTP status code: [{twilioChannelResult.HttpStatusCode}]");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			foreach (Channel channel in twilioChannelResult.Payload)
			{
				result.EntitiesFetched.Add(channel);

				if (channel.DateCreated < laterThan)
				{
					Trace.WriteLine($"\tChannel {channel.UniqueName} skipped. Created on {channel.DateCreated}. Requested time period from {laterThan}.");
					result.EntitiesSucceeded.Add(channel);
					continue;
				}

				if (channel.MembersCount == 0)
				{
					Trace.WriteLine($"Channel {channel.UniqueName} contained no members. Skipped.");
					result.EntitiesSucceeded.Add(channel);
					continue;
				}

				int[] channelMembersIds = null;
                if (channel.MembersCount == 1)
                {
					// We could create a Fetch method to fetch a single member, but bulk retrieve is no difference.
                    HttpClientResult<Member[]> channelMemberResult = await _twilioClient.ChannelMembersBulkRetrieveAsync(channel.UniqueName);
					// If request to Twilio is successful, we will know the only member; if not, we'll proceed with empty array:
					// better not to add members to channel with single member at all rather then to re-add the removed one.
                    channelMembersIds = channelMemberResult.IsSuccess ?
	                    channelMemberResult.Payload.Select(m => Int32.TryParse(m.Id, out int id) ? id : 0).ToArray() :
	                    Array.Empty<int>();
                }

                if (channel.MembersCount == 2) channelMembersIds = channel.UniqueName.Split('-').Skip(1).Select(Int32.Parse).ToArray();

                // Checking if channel members exist as SB users. If not, we'll try to migrate them. If migration fails we'll still proceed.
				// In this case channel will simply be created with the members that already exist.
                HttpClientResult<int[]> absentMembersResult = await _sendbirdClient.WhoIsAbsentAsync(channelMembersIds);
                if (absentMembersResult.IsSuccess && absentMembersResult.Payload.Length > 0)
                {
	                Trace.WriteLine($"Migrating the nonexistent members of the channel {channel.UniqueName}...");
					foreach (int memberId in absentMembersResult.Payload)
	                {
		                Trace.WriteLine($"\tMigrating the member with ID {memberId} of the channel {channel.UniqueName}...");
						// We won't pay attention to the date of creation when migrating channel's members.
		                var memberMigrationResult = await MigrateSingleUserAttributesAsync(memberId.ToString(), true, null);
		                if (memberMigrationResult.FailedCount > 0)
		                {
			                Trace.WriteLine($"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
			                result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
		                }
		                else
			                Trace.WriteLine($"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} succeeded.");
	                }
                }

                await MigrateChannelWithMetadataAsync(channel, channelMembersIds, result);
			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.";
			return result;
		}

		public async Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? laterThan, int accountUserId, int limit, int pageSize)
        {
			var result = new MigrationResult<IResource>();

			if (accountUserId <= 0)
            {
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"{accountUserId} is invalid.");
				return result;
			}

			// When migrating a single account we will always migrate a user, because they may have recent channels.
			MigrationResult<User> userMigrationResult = await MigrateSingleUserAttributesAsync(accountUserId.ToString(), false, null);

			if (userMigrationResult.FetchedCount == 0 || userMigrationResult.FailedCount > 0)
			{
				result.EntitiesFailed.AddRange(userMigrationResult.EntitiesFailed);
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add(
					$"Failed to migrate the user with ID {accountUserId}; reason: {userMigrationResult.Message}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			MigrationResult<Channel> channelsMigrationResult = await MigrateSingleUserChannelsAttributesAsync(accountUserId.ToString(), limit, pageSize, laterThan);

			result.EntitiesFailed.AddRange(channelsMigrationResult.EntitiesFailed);
			result.EntitiesSucceeded.AddRange(channelsMigrationResult.EntitiesSucceeded);
			result.ErrorMessages.AddRange(channelsMigrationResult.ErrorMessages);
			result.Message = channelsMigrationResult.Message;

			return result;
        }

		private void WriteMigrationResultLogFiles<T>(MigrationResult<T> result)
        {
			if(result.SuccessCount > 0)
            {
				File.WriteAllLines(_successLogFileName, result.EntitiesSucceeded.Select(e => e.ToString()).ToArray());
			}

			if(result.FailedCount > 0)
            {
				File.WriteAllLines(_failedLogFileName, result.EntitiesFailed.Select(e => e.ToString()).ToArray());
            }
		}

		private async Task<MigrationResult<User>> MigrateSingleUserAttributesAsync(string userId, bool blockExistentUsersOnly, DateTime? laterThan)
        {
			HttpClientResult<User> userFetchResult = await _twilioClient.UserFetchAsync(userId).ConfigureAwait(false);

			MigrationResult<User> result = new MigrationResult<User>();

            if (!userFetchResult.IsSuccess)
            {
                result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{userFetchResult.FormattedMessage}]; HTTP status code: [{userFetchResult.HttpStatusCode}]");
                return result;
            }

			User user = userFetchResult.Payload;
            result.EntitiesFetched.Add(user);
			
            await MigrateFetchedUserAsync(user, blockExistentUsersOnly, laterThan, result);
			
			return result;
        }

		private async Task<bool> MigrateFetchedUserAsync(User user, bool blockExistentUsersOnly, DateTime? laterThan, /* mutable */MigrationResult<User> result)
		{
			if(result == null) throw new ArgumentNullException(nameof(result));

			Trace.WriteLine($"Migrating user {user.FriendlyName} with ID {user.Id}...");
			if(user.DateCreated < laterThan)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} skipped. Created on {user.DateCreated}. Requested time period from {laterThan}.");
				result.EntitiesSucceeded.Add(user);
				return false;
			}

			var userUpsertRequestBody = new UserUpsertRequest
			{
				UserId = user.Id,
                Nickname = user.FriendlyName,
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
				return false;
			}

			if (user.Attributes?.BlockedUsers == null || user.Attributes.BlockedUsers.Length == 0)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} has no blocked users.");
				return false;
			}

			Trace.WriteLine($"\tMigrating user blockages for the user {user.FriendlyName} with ID {user.Id}.");
			// We will now check who of the blockees does not exist on SB side.
			var nonExistentUsersResult = await _sendbirdClient.WhoIsAbsentAsync(user.Attributes.BlockedUsers);

			if (!nonExistentUsersResult.IsSuccess)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"Failed to query SB for blockeed of the user {user.FriendlyName} with ID {user.Id}. Reason: {nonExistentUsersResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return false;
			}

			int[] usersToBlock = blockExistentUsersOnly ? user.Attributes.BlockedUsers.Except(nonExistentUsersResult.Payload).ToArray() : user.Attributes.BlockedUsers;
			if (usersToBlock.Length == 0)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully, having no blockees. Be kind to each other :)");
				result.EntitiesSucceeded.Add(user);
				return false;
			}

			HttpClientResult<List<UserResource>> blockageResult = await _sendbirdClient.BlockUsersBulkAsync(Int32.Parse(user.Id), usersToBlock);
			if (!blockageResult.IsSuccess && blockageResult.HttpStatusCode != HttpStatusCode.NotFound)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"\tFailed to migrate blockages for the user {user.FriendlyName} with ID {user.Id}. Reason: {updateResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return false;
			}

			if (blockExistentUsersOnly && blockageResult.IsSuccess)
			{
				Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully with the existent blockees only.");
				result.EntitiesSucceeded.Add(user);
				return false;
			}

			// Worst case. The blocking endpoint on SB side returned 404. That means that at least one of the users to block does not exist on SB's side.
			Trace.WriteLine($"\tOne or more of the blockees of the user {user.FriendlyName} do not exist on SB side. Creating...");
			bool atLeastOneFailed = false;
			foreach (int id in nonExistentUsersResult.Payload)
			{
				Trace.WriteLine($"\tMigrating blockee with the id {id}...");
				// Yes, this is recursion. And we will migrate the blockees even if they are old enough.
				MigrationResult<User> blockeeMigrationResult = await MigrateSingleUserAttributesAsync(id.ToString(), true, null);
				if (blockeeMigrationResult.FailedCount == 0) continue;

				atLeastOneFailed = true;
				result.ErrorMessages.AddRange(blockeeMigrationResult.ErrorMessages);
				Debug.WriteLine(blockeeMigrationResult.ErrorMessages.Last());
			}

			// Even if one or several of the blockees failed to migrate, we consider the "main" user success.
			string finalMessage = atLeastOneFailed ?
				$"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully with the blockees." :
				$"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully, but some or all of the blockees failed.";
			Trace.WriteLine(finalMessage);
			result.EntitiesSucceeded.Add(user);
			return true;
		}

		private async Task<MigrationResult<Channel>> MigrateSingleUserChannelsAttributesAsync(string userId, int limit, int pageSize, DateTime? laterThan)
		{
			int? migrateNoMoreThan = null;
			if (limit > 0) migrateNoMoreThan = limit;
			int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			var result = new MigrationResult<Channel>();

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

				if (channel.DateCreated < laterThan)
				{
					Trace.WriteLine($"\tChannel {channel.UniqueName} skipped. Created on {channel.DateCreated}. Requested time period from {laterThan}.");
					result.EntitiesSucceeded.Add(channel);
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
						var memberMigrationResult = await MigrateSingleUserAttributesAsync(secondChannelMember.ToString(), true, null);
						if (memberMigrationResult.FailedCount > 0)
						{
							Trace.WriteLine(
								$"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
							result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
						}
						else Trace.WriteLine($"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} succeeded.");

						channelMembersIds.Add(secondChannelMember);
					}
				}

				await MigrateChannelWithMetadataAsync(channel, channelMembersIds.ToArray(), result);

			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.";
			return result;
		}

		private async Task<bool> MigrateChannelWithMetadataAsync(Channel channel, int[] channelMembersIds, /* mutable */ MigrationResult<Channel> result)
		{
			Trace.WriteLine($"Migrating channel {channel.UniqueName}...");
			OperationResult operationResult =
				await MigrationUtilities.TryCreateOrUpdateChannelWithMetadataAsync(_sendbirdClient, channel, channelMembersIds, result);

			switch (operationResult)
			{
				case OperationResult.Failure:
					result.EntitiesFailed.Add(channel);
					Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate.");
					return false;
				case OperationResult.Success:
					result.EntitiesSucceeded.Add(channel);
					Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
					return true;
				case OperationResult.Continuation:
					break;
				default:
					return false;
			}

			operationResult = await MigrationUtilities.TryUpdateOrCreateChannelMetadataAsync(_sendbirdClient, channel, result);

			if (operationResult != OperationResult.Success)
			{
				result.EntitiesFailed.Add(channel);
				Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate. Failed to migrate metadata.");
				return false;
			}

			result.EntitiesSucceeded.Add(channel);
			Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
			return true;
		}
	}
}