using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		public async Task<MigrationResult<User>> MigrateUsersAttributesAsync(int limit, int pageSize)
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

				if (user.Attributes == null ||
					user.Attributes.BlockedByAdminAt == null &&
					(user.Attributes.BlockedUsers == null || user.Attributes.BlockedUsers.Length == 0))
				{
					Trace.WriteLine($"User {user.FriendlyName} with ID {user.Id} contained no attributes to migrate.");
					result.EntitiesSucceeded.Add(user);
					continue;
				}

				Trace.WriteLine($"Migrating user {user.FriendlyName} with ID {user.Id}...");

				var userUpsertRequestBody = new UserUpsertRequest
				{
					Nickname = user.FriendlyName,
					Metadata = new UserMetadata(),
					IssueSessionToken = true
				};

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
					result.ErrorMessages.Add(
						$"Failed to create a user {user.FriendlyName} with ID {user.Id} on SB side. Reason: {updateResult.FormattedMessage}.");
					Debug.WriteLine(result.ErrorMessages.Last());
					continue;
				}

				if (user.Attributes.BlockedUsers.Length <= 0)
				{
					Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} has no blocked users.");
					continue;
				}

				Trace.WriteLine($"\tMigrating user blockages for the user {user.FriendlyName} with ID {user.Id}.");
				HttpClientResult<List<UserResource>> blockageResult = await _sendbirdClient.BlockUsersBulkAsync(Int32.Parse(user.Id), user.Attributes.BlockedUsers);

				if (!blockageResult.IsSuccess)
				{
					result.EntitiesFailed.Add(user);
					result.ErrorMessages.Add(
						$"\tFailed to migrate Migrating user blockages for the user {user.FriendlyName} with ID {user.Id}. Reason: {updateResult.FormattedMessage}.");
					Debug.WriteLine(result.ErrorMessages.Last());
					continue;
				}

				result.EntitiesSucceeded.Add(user);
			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all users' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} users' attributes.";
			return result;
		}

		public async Task<MigrationResult<Channel>> MigrateChannelsAttributesAsync(int limit, int pageSize)
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
                if (channel.Attributes == null || channel.MembersCount == 0)
				{
					Trace.WriteLine($"Channel {channel.UniqueName} contained no attributes to migrate or had no members.");
					result.EntitiesSucceeded.Add(channel);
					continue;
				}

				int[] channelMembersIds = null;
                if (channel.MembersCount == 1)
                {
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
		                var memberMigrationResult = await MigrateSingleUserAttributesAsync(memberId.ToString(), true);
		                if (memberMigrationResult.FailedCount > 0)
		                {
			                Trace.WriteLine($"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
			                result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
		                }
		                else
			                Trace.WriteLine($"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} succeeded.");
	                }
                }

                Trace.WriteLine($"Migrating channel {channel.UniqueName}...");
                OperationResult operationResult = await MigrationUtilities.TryCreateOrUpdateChannelWithMetadata(_sendbirdClient, channel, channelMembersIds, result);

				switch (operationResult)
				{
					case OperationResult.Failure:
						continue;
					case OperationResult.Success:
						result.EntitiesSucceeded.Add(channel);
						Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
						continue;
					case OperationResult.Continuation:
						break;
					default:
						continue;
				}

				operationResult = await MigrationUtilities.TryUpdateOrCreateChannelMetadata(_sendbirdClient, channel, result);

				if (operationResult != OperationResult.Success) continue;

				result.EntitiesSucceeded.Add(channel);
				Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.";
			return result;
		}

		public async Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(int accountUserId, int limit, int pageSize)
        {
			var result = new MigrationResult<IResource>();

			if (accountUserId <= 0)
            {
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"{accountUserId} is invalid.");
				return result;
			}

			MigrationResult<User> userMigrationResult = await MigrateSingleUserAttributesAsync(accountUserId.ToString(), false);

			if (userMigrationResult.FailedCount > 0)
			{
				result.EntitiesFailed.AddRange(userMigrationResult.EntitiesFailed);
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add(
					$"Failed to migrate the user with ID {accountUserId}; reason: {userMigrationResult.Message}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			MigrationResult<Channel> channelsMigrationResult = await MigrateSingleUserChannelsAttributesAsync(accountUserId.ToString(), limit, pageSize);

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

		private async Task<MigrationResult<User>> MigrateSingleUserAttributesAsync(string userId, bool blockExistentUsersOnly)
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

			Trace.WriteLine($"Migrating user {user.FriendlyName} with ID {user.Id}...");
			var userUpsertRequestBody = new UserUpsertRequest
			{
				Nickname = user.FriendlyName,
				Metadata = new UserMetadata(),
				IssueSessionToken = true
			};

			if(user.Attributes?.BlockedByAdminAt != null)
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
                Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully, having no blockees. Be kind to each other :)");
				result.EntitiesSucceeded.Add(user);
				return result;
            }

            HttpClientResult<List<UserResource>> blockageResult = await _sendbirdClient.BlockUsersBulkAsync(Int32.Parse(user.Id), usersToBlock);
            if (!blockageResult.IsSuccess && blockageResult.HttpStatusCode != HttpStatusCode.NotFound)
			{
				result.EntitiesFailed.Add(user);
				result.ErrorMessages.Add($"\tFailed to migrate blockages for the user {user.FriendlyName} with ID {user.Id}. Reason: {updateResult.FormattedMessage}.");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result ;
			}

            if (blockExistentUsersOnly && blockageResult.IsSuccess)
            {
                Trace.WriteLine($"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully with the existent blockees only.");
                result.EntitiesSucceeded.Add(user);
                return result;
			}

			// Worst case. The blocking endpoint on SB side returned 404. That means that at least one of the users to block does not exist on SB's side.
			Trace.WriteLine($"\tOne or more of the blockees of the user {user.FriendlyName} do not exist on SB side. Creating...");
            bool atLeastOneFailed = false;
            foreach (int id in nonExistentUsersResult.Payload)
            {
                Trace.WriteLine($"\tMigrating blockee with the id {id}...");
				MigrationResult<User> blockeeMigrationResult = await MigrateSingleUserAttributesAsync(id.ToString(), true);
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
			return result;
        }

		private async Task<MigrationResult<Channel>> MigrateSingleUserChannelsAttributesAsync(string userId, int limit, int pageSize)
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
						var memberMigrationResult = await MigrateSingleUserAttributesAsync(secondChannelMember.ToString(), true);
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

				Trace.WriteLine($"Migrating channel {channel.UniqueName}...");
				OperationResult operationResult =
					await MigrationUtilities.TryCreateOrUpdateChannelWithMetadata(_sendbirdClient, channel, channelMembersIds.ToArray(), result);

				switch (operationResult)
				{
					case OperationResult.Failure:
						result.EntitiesFailed.Add(channel);
						Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate.");
						continue;
					case OperationResult.Success:
						result.EntitiesSucceeded.Add(channel);
						Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
						continue;
					case OperationResult.Continuation:
						break;
					default:
						continue;
				}

				operationResult = await MigrationUtilities.TryUpdateOrCreateChannelMetadata(_sendbirdClient, channel, result);

				if (operationResult != OperationResult.Success)
				{
					result.EntitiesFailed.Add(channel);
					Trace.WriteLine($"\tChannel {channel.UniqueName} failed to migrate. Failed to migrate metadata.");
					continue;
				}

				result.EntitiesSucceeded.Add(channel);
				Trace.WriteLine($"\tChannel {channel.UniqueName} migrated successfully.");
			}

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes migrated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			WriteMigrationResultLogFiles(result);

			result.Message = $"Migration finished. Totally migrated {result.SuccessCount} channels' attributes.";
			return result;
		}
	}
}