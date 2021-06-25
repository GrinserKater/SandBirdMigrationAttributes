using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Results;
using SandBirdMigrationAttributes.Logging.Enums;
using SendbirdHttpClient.Abstractions;
using SendbirdHttpClient.Models.User;
using TheGrandMigrator.Abstractions;
using TheGrandMigrator.Constants;
using TheGrandMigrator.Enums;
using TheGrandMigrator.Logging;
using TheGrandMigrator.Models;
using TheGrandMigrator.Utilities;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;
using TwilioHttpClient.Models.Attributes.User;
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

		public async Task<IMigrationResult<IResource>> MigrateUsersAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;
            int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

            int tasksQuanity = 10;

            var result = new MigrationResult<IResource>();
            LoggingUtilities.Log("Fetching users from Twilio in a bulk mode. This might take a couple of minutes...");
			try
			{
				IEnumerable<User> twilioUsersResult = _twilioClient.UserBulkRetrieve(entitiesPerPage, migrateNoMoreThan);

				List<Task<IMigrationResult<IResource>>> chunkMigrationTasks = new List<Task<IMigrationResult<IResource>>>(tasksQuanity);
				User[] usersChunk = new User[pageSize];
				int i = 0;
				using (var twilioUserResultEnumerator = twilioUsersResult.GetEnumerator())
				{
					bool hasNext = twilioUserResultEnumerator.MoveNext();
					while (hasNext)
					{
						var user = twilioUserResultEnumerator.Current;
						if(user == null) continue;

						LoggingUtilities.Log($"Fetched the user {user.Id} - {user.FriendlyName}.");
						result.IncreaseUsersFetched();
						var currentUserCopy = new User
						{
							Id = user.Id,
							DateCreated = user.DateCreated,
							DateUpdated = user.DateUpdated,
							FriendlyName = user.FriendlyName,
							ProfileImageUrl = user.ProfileImageUrl,
							Attributes = new UserAttributes
							{
								BlockedByAdminAt = user.Attributes?.BlockedByAdminAt
							}
						};
						if (user.Attributes?.BlockedUsers != null && user.Attributes.BlockedUsers.Length > 0)
						{
							currentUserCopy.Attributes.BlockedUsers = new int[user.Attributes.BlockedUsers.Length];
							Array.Copy(user.Attributes.BlockedUsers, currentUserCopy.Attributes.BlockedUsers, 0);
						}
						usersChunk[i++] = currentUserCopy;

						hasNext = twilioUserResultEnumerator.MoveNext();
						
						if (i == pageSize || !hasNext)
						{
							User[] chunkToProcess = new User[pageSize];
							Array.Copy(usersChunk, chunkToProcess, usersChunk.Length);
							chunkMigrationTasks.Add(Task.Run(async () =>
							{
								var chunkMigrationResult = await MigrateUsersChunkAsync(chunkToProcess, false, dateBefore, dateAfter);
								Array.Clear(chunkToProcess, 0, chunkToProcess.Length);
								return chunkMigrationResult;
							}));
							Array.Clear(usersChunk, 0, usersChunk.Length);
							i = 0;
						}
					
						if(chunkMigrationTasks.Count < tasksQuanity && hasNext) continue;

						IMigrationResult<IResource>[] intermediateResults = await Task.WhenAll(chunkMigrationTasks);
						foreach (var intermediateResult in intermediateResults)
						{
							result.Consume(intermediateResult);
						}
						chunkMigrationTasks.Clear();
					}
				}
			}
			catch (Exception ex)
			{
				result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{ex.Message}].");
				return result;
			}
			
            result.Message = result.UsersFailedCount == 0 ?
                $"Migration finished. Totally migrated {result.UsersSuccessCount} users' attributes.":
                $"Not all users' attributes migrated successfully. {result.UsersFailedCount} failed, {result.UsersSuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}

		public async Task<IMigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize)
		{
			int? migrateNoMoreThan = null;
			if(limit > 0) migrateNoMoreThan = limit;
            int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;
            
            var result = new MigrationResult<IResource>();
            LoggingUtilities.Log("Fetching channels from Twilio in a bulk mode. This might take a couple of minutes...");
            try
            {
	            IEnumerable<Channel> twilioChannelResult = _twilioClient.ChannelBulkRetrieve(entitiesPerPage, migrateNoMoreThan);

	            // TODO: filtering can be done here based on the dateBefore and dateAfter parameter, and on member count.
	            // This will reduce the amount of iterations, but the logging must be adjusted appropriately. 
	            foreach (Channel channel in twilioChannelResult)
	            {
		            LoggingUtilities.Log($"Fetched the channel {channel.UniqueName}.");
		            result.IncreaseChannelsFetched();

		            IMigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(channel, dateBefore, dateAfter);
		            result.Consume(singleChannelMigrationResult);
	            }
            }
            catch (Exception ex)
            {
	            result.Message = "Migration of channels' attributes failed. See ErrorMessages for details.";
	            result.ErrorMessages.Add($"Message: [{ex.Message}].");
	            Debug.WriteLine(result.ErrorMessages.Last());
	            return result;
            }
            
            result.Message = result.ChannelsFailedCount == 0 ?
				$"Migration finished. Totally migrated {result.ChannelsSuccessCount} channels' attributes.":
				$"Not all channels' attributes migrated successfully. {result.ChannelsFailedCount} failed, {result.ChannelsSuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}

		public async Task<IMigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, string fileName)
		{
			var result = new MigrationResult<IResource>();
            LoggingUtilities.Log($"Reading channel identifiers from file {fileName}...");
            try
            {
	            var channelUniqueIdentifiers = DataSourceUtilities.ReadAllFromFile(fileName, out int totalLines);
	            LoggingUtilities.Log($"Read {totalLines} channel identifiers.");
	            int migratedSoFar = 0;
	            foreach (string channelUniqueIdentifier in channelUniqueIdentifiers)
	            {
		            IMigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(dateBefore, dateAfter, channelUniqueIdentifier);
		            LoggingUtilities.WriteTraceLine($"Channels migrated so far: {++migratedSoFar}/{totalLines}");
		            result.Consume(singleChannelMigrationResult);
	            }
            }
            catch (Exception ex)
            {
	            result.Message = "Migration of channels' attributes failed. See ErrorMessages for details.";
	            result.ErrorMessages.Add($"Message: [{ex.Message}].");
	            Debug.WriteLine(result.ErrorMessages.Last());
	            return result;
            }
            
            result.Message = result.ChannelsFailedCount == 0 ?
				$"Migration finished. Totally migrated {result.ChannelsSuccessCount} channels' attributes.":
				$"Not all channels' attributes migrated successfully. {result.ChannelsFailedCount} failed, {result.ChannelsSuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}
		
		// Experimental method.
		public async Task<IMigrationResult<IResource>> MigrateChannelsAttributesParallelAsync(DateTime? dateBefore, DateTime? dateAfter, string fileName)
		{
			var result = new MigrationResult<IResource>();
            LoggingUtilities.Log("Reading unique identifiers from file...");
            try
            {
	            List<Task<MigrationResult<IResource>>> tasks = new List<Task<MigrationResult<IResource>>>();
	            foreach (IEnumerable<string> channelUniqueNames in DataSourceUtilities.ReadBatchFromFile(fileName, 10))
	            {
		            var list = channelUniqueNames.ToList();
		            Task<MigrationResult<IResource>> task = Task.Run(async () =>
		            {
			            var localResult = new MigrationResult<IResource>();
			            foreach (string channelUniqueName in list)
			            {
				            IMigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(dateBefore, dateAfter, channelUniqueName);
				            localResult.Consume(singleChannelMigrationResult);
			            }
			            return localResult;
		            });
		            tasks.Add(task);
	            }

	            var cont = Task.WhenAll(tasks);
	            try
	            {
		            cont.Wait();
	            }
	            catch (Exception e)
	            {
		            result.Message = e.Message;
	            }

	            await Task.Delay(200);
	            foreach (var migrationResult in cont.Result)
	            {
		            result.Consume(migrationResult);
	            }
            }
            catch (Exception ex)
            {
	            result.Message = "Migration of channels' attributes failed. See ErrorMessages for details.";
	            result.ErrorMessages.Add($"Message: [{ex.Message}].");
	            Debug.WriteLine(result.ErrorMessages.Last());
	            return result;
            }
            
            result.Message = result.ChannelsFailedCount == 0 ?
				$"Migration finished. Totally migrated {result.ChannelsSuccessCount} channels' attributes.":
				$"Not all channels' attributes migrated successfully. {result.ChannelsFailedCount} failed, {result.ChannelsSuccessCount} succeeded. See ErrorMessages for details.";
			return result;
		}

		public async Task<IMigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int accountUserId, int limit, int pageSize)
        {
			var result = new MigrationResult<IResource>();

			if (accountUserId <= 0)
            {
				result.Message = "Migration of the account failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"{accountUserId} is invalid.");
				return result;
			}

			// When migrating a single account we will always migrate a user, because they may have recent channels.
			IMigrationResult<IResource> userMigrationResult = await MigrateSingleUserAttributesAsync(accountUserId.ToString(), false, null, null);
            if (userMigrationResult.UsersFetchedCount == 0)
            {
                result.Message = "Migration of the account failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to migrate the user with ID {accountUserId}; reason: {userMigrationResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
			}
            
            result.Consume(userMigrationResult);

            if (userMigrationResult.UsersFailedCount > 0)
			{
                result.Message = "Migration of the account failed. See ErrorMessages for details.";
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }

			IMigrationResult<IResource> channelsMigrationResult = await MigrateSingleUserChannelsAttributesAsync(accountUserId.ToString(), limit, pageSize, dateBefore, dateAfter);
			result.Consume(channelsMigrationResult, $"{userMigrationResult.Message}; {channelsMigrationResult.Message}");
			return result;
        }

        public async Task<IMigrationResult<IResource>> MigrateSingleChannelAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, string channelUniqueIdentifier)
        {
            var result = new MigrationResult<IResource>();

            if (String.IsNullOrWhiteSpace(channelUniqueIdentifier))
            {
                result.Message = "Migration of the channel failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"{channelUniqueIdentifier} is invalid.");
                return result;
            }
            
            LoggingUtilities.Log($"Fetching channel {channelUniqueIdentifier} from Twilio...");
            HttpClientResult<Channel> twilioChannelResult = await _twilioClient.ChannelFetchAsync(channelUniqueIdentifier);

			if (!twilioChannelResult.IsSuccess)
            {
                result.Message = $"Migration of attributes for the channel {channelUniqueIdentifier} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{twilioChannelResult.FormattedMessage}]; HTTP status code: [{twilioChannelResult.HttpStatusCode}]");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }

			Channel channel = twilioChannelResult.Payload;
			result.IncreaseChannelsFetched();

			IMigrationResult<IResource> singleChannelMigrationResult = await MigrateSingleChannelAttributesAsync(channel, dateBefore, dateAfter);
			result.Consume(singleChannelMigrationResult);

			result.Message = result.ChannelsFailedCount == 0 ?
				$"Migration finished. Channel {channelUniqueIdentifier} successfully migrated with attributes." :
				$"Migration of the channel {channelUniqueIdentifier} with attributes failed. See ErrorMessages for details.";
            return result;
        }

		private async Task<IMigrationResult<IResource>> MigrateSingleUserAttributesAsync(string userId, bool blockExistentUsersOnly, DateTime? dateBefore, DateTime? dateAfter)
        {
			HttpClientResult<User> userFetchResult = await _twilioClient.UserFetchAsync(userId).ConfigureAwait(false);

			MigrationResult<IResource> result = new MigrationResult<IResource>();

            if (!userFetchResult.IsSuccess)
            {
                result.Message = "Migration of users attributes failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{userFetchResult.FormattedMessage}]; HTTP status code: [{userFetchResult.HttpStatusCode}]");
				// We will add a dummy user with the failed ID for the correct stats.
				result.IncreaseUsersFailed();
				LoggingUtilities.LogEntityProcessingResultToFile(userId, EntityProcessingResult.Failure);
                return result;
            }
            
            User user = userFetchResult.Payload;
            LoggingUtilities.Log($"Fetched the user {user.Id} - {user.FriendlyName}.");
			result.IncreaseUsersFetched();

			var userMigrationResult = await MigrateFetchedUserAsync(user, blockExistentUsersOnly, dateBefore, dateAfter);
			result.Consume(userMigrationResult);
			return result;
        }

		private async Task<IMigrationResult<IResource>> MigrateFetchedUserAsync(User user, bool blockExistentUsersOnly, DateTime? dateBefore, DateTime? dateAfter)
		{
			LoggingUtilities.Log($"Migrating user {user.FriendlyName} with ID {user.Id} with {(blockExistentUsersOnly ? "only existent blockees":"all the blockees")} if any...");
			MigrationResult<IResource> result = new MigrationResult<IResource>();

            if(!IsIncludedByDate(user.DateUpdated, dateBefore, dateAfter))
			{
				LoggingUtilities.Log(
                    $"\tUser {user.FriendlyName} with ID {user.Id} skipped. Last updated on {user.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
				result.IncreaseUsersSkipped();
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Skipped);
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
				LoggingUtilities.Log($"\tUser {user.FriendlyName} with ID {user.Id} does not exist on SB side. Creating...");
				updateResult = await _sendbirdClient.CreateUserAsync(userUpsertRequestBody);
			}

			if (!updateResult.IsSuccess)
			{
				result.IncreaseUsersFailed();
				result.ErrorMessages.Add($"Failed to create a user {user.FriendlyName} with ID {user.Id} on SB side. Reason: {updateResult.FormattedMessage}.");
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Failure);
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			if (user.Attributes?.BlockedUsers == null || user.Attributes.BlockedUsers.Length == 0)
			{
				LoggingUtilities.Log($"\tUser {user.FriendlyName} with ID {user.Id} has no blocked users.");
				result.IncreaseUsersSuccess();
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Success);
				return result;
			}

			LoggingUtilities.Log($"\tMigrating user blockages for the user {user.FriendlyName} with ID {user.Id}.");
			// We will now check who of the blockees does not exist on SB side.
			var nonExistentUsersResult = await _sendbirdClient.WhoIsAbsentAsync(user.Attributes.BlockedUsers);

			if (!nonExistentUsersResult.IsSuccess)
			{
				result.IncreaseUsersFailed();
				result.ErrorMessages.Add($"Failed to query SB for blockeed of the user {user.FriendlyName} with ID {user.Id}. Reason: {nonExistentUsersResult.FormattedMessage}.");
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Failure);
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			int[] usersToBlock = blockExistentUsersOnly ? user.Attributes.BlockedUsers.Except(nonExistentUsersResult.Payload).ToArray() : user.Attributes.BlockedUsers;
			if (usersToBlock.Length == 0)
			{
				LoggingUtilities.Log($"\tUser {user.FriendlyName} with ID {user.Id} migrate successfuly. No blocked user currently exists. Be kind to each other :)");
				result.IncreaseUsersSuccess();
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Success);
				return result;
			}

			// Worst case. At least one of the users to block does not exist on SB's side.
            var atLeastOneBlockeeFailed = false;
			if (!blockExistentUsersOnly && nonExistentUsersResult.Payload.Length > 0)
            {
	            LoggingUtilities.Log($"\tOne or more of the blockees of the user {user.FriendlyName} do not exist on SB side. Creating...");
				IMigrationResult<IResource> blockeeMigrationResult = null;
                foreach (int id in nonExistentUsersResult.Payload)
                {
	                LoggingUtilities.Log($"\tMigrating blockee with the id {id}...");
                    // Yes, this is recursion. And we will migrate the blockees even if they are old enough.
                    blockeeMigrationResult = await MigrateSingleUserAttributesAsync(id.ToString(), true, null, null);
                }
				// ReSharper disable once PossibleNullReferenceException
                if (blockeeMigrationResult.UsersFailedCount > 0) atLeastOneBlockeeFailed = true;
                result.Consume(blockeeMigrationResult);
            }

            HttpClientResult<List<UserResource>> blockageResult = await _sendbirdClient.BlockUsersBulkAsync(Int32.Parse(user.Id), usersToBlock);
			if (!blockageResult.IsSuccess)
			{
				result.IncreaseUsersFailed();
				result.ErrorMessages.Add($"\tFailed to migrate blockages for the user {user.FriendlyName} with ID {user.Id}. Reason: {blockageResult.FormattedMessage}.");
				LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Failure);
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			// Even if one or several of the blockees failed to migrate, we consider the "main" user success.
			string finalMessage = atLeastOneBlockeeFailed ?
				$"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully, but some or all of the blockees failed.":
                $"\tUser {user.FriendlyName} with ID {user.Id} migrated successfully with the blockees.";
			LoggingUtilities.Log(finalMessage);
			result.IncreaseUsersSuccess();
			LoggingUtilities.LogEntityProcessingResultToFile(user.Id, EntityProcessingResult.Success);
			return result;
		}

		private async Task<IMigrationResult<IResource>> MigrateUsersChunkAsync(User[] users, bool blockExistentUsersOnly, DateTime? dateBefore, DateTime? dateAfter)
		{
			var result = new MigrationResult<IResource>();
			foreach (User user in users)
			{
				if(user == null) continue;
				var singleResult = await MigrateFetchedUserAsync(user, blockExistentUsersOnly, dateBefore, dateAfter);
				result.Consume(singleResult);
			}

			return result;
		}

		private async Task<IMigrationResult<IResource>> MigrateSingleUserChannelsAttributesAsync(string userId, int limit, int pageSize, DateTime? dateBefore, DateTime? dateAfter)
		{
			int? migrateNoMoreThan = null;
			if (limit > 0) migrateNoMoreThan = limit;
			int entitiesPerPage = pageSize == 0 ? Migration.DefaultPageSize : pageSize;

			var result = new MigrationResult<IResource>();

			// Unfortunately, smart Twilio engineers decided to return absolutely different object as UserChannelResource rather than ChannelResource.
			IAsyncEnumerable<UserChannel> userChannelsFetchResult = _twilioClient.UserChannelsBulkRetrieveAsync(userId, entitiesPerPage, migrateNoMoreThan);
			try
			{
				// This piece of code might seem very similar to the one in MigrateChannelsAttributesAsync,
				// but this one is slightly optimised for the single user case.
				await foreach (UserChannel userChannel in userChannelsFetchResult)
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
					result.IncreaseChannelsFetched();
					if(!IsIncludedByDate(channel.DateUpdated, dateBefore, dateAfter))
					{
						LoggingUtilities.Log($"\tChannel {channel.UniqueName} skipped. Last updated on {channel.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
						result.IncreaseChannelsSkipped();
						LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Skipped);
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
							LoggingUtilities.Log($"Migrating the nonexistent member with ID {secondChannelMember} of the channel {channel.UniqueName}...");
							// Channel's member will be migrated disregarding the age. 
							var memberMigrationResult = await MigrateSingleUserAttributesAsync(secondChannelMember.ToString(), true, null, null);
							if (memberMigrationResult.UsersFetchedCount == 0)
							{
								LoggingUtilities.Log(
									$"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
								result.ErrorMessages.AddRange(memberMigrationResult.ErrorMessages);
							}

							if (memberMigrationResult.UsersFailedCount > 0)
								LoggingUtilities.Log(
									$"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}.");
							else
								LoggingUtilities.Log($"\tMigration of the member with ID {secondChannelMember} of the channel {channel.UniqueName} succeeded.");
		
							result.Consume(memberMigrationResult);
						}
						channelMembersIds.Add(secondChannelMember);
					}

					var channelMigrationResult =  await MigrateChannelWithMetadataAsync(channel, channelMembersIds.ToArray());
					result.Consume(channelMigrationResult);
				}
			}
			catch (Exception ex)
			{
				result.Message = $"Migration of account's attributes for the ID {userId} failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{ex.Message}].");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			if (result.ChannelsFetchedCount == 0)
			{
				result.Message = $"No channels attributes for the account ID {userId} to migrate.";
				return result;
			}
			
			result.Message = result.ChannelsFailedCount > 0 ?
				$"Not all channels' attributes migrated successfully. {result.ChannelsFailedCount} failed, {result.ChannelsSuccessCount} succeeded. See ErrorMessages for details." :
				$"Migration finished. Totally migrated {result.ChannelsSuccessCount} channels' attributes.";
			
			return result;
		}

		private async Task<IMigrationResult<IResource>> MigrateChannelWithMetadataAsync(Channel channel, int[] channelMembersIds)
		{
			LoggingUtilities.Log($"Migrating channel {channel.UniqueName}...");
			var result = new MigrationResult<IResource>();

			OperationResult operationResult =
				await MigrationUtilities.TryCreateOrUpdateChannelWithMetadataAsync(_sendbirdClient, channel, channelMembersIds, result);

			switch (operationResult)
			{
				case OperationResult.Failure:
					result.IncreaseChannelsFailed();
					LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Failure);
					LoggingUtilities.Log($"\tChannel {channel.UniqueName} failed to migrate.");
					return result;
				case OperationResult.Success:
					result.IncreaseChannelsSuccess();
					LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Success);
					LoggingUtilities.Log($"\tChannel {channel.UniqueName} migrated successfully.");
					return result;
				case OperationResult.Continuation:
					break;
				default:
					return result;
			}

			operationResult = await MigrationUtilities.TryUpdateOrCreateChannelMetadataAsync(_sendbirdClient, channel, result);

			if (operationResult != OperationResult.Success)
			{
				result.IncreaseChannelsFailed();
				LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Failure);
				LoggingUtilities.Log($"\tChannel {channel.UniqueName} failed to migrate. Failed to migrate metadata.");
				return result;
			}

			result.IncreaseChannelsSuccess();
			LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Success);
			LoggingUtilities.Log($"\tChannel {channel.UniqueName} migrated successfully.");
			return result;
		}
		private bool IsIncludedByDate(DateTime? reference, DateTime? before, DateTime? after)
        {
			if (reference == null || before == null && after == null) return true;
			// Intersection (reference is IN the date interval).
            if (after <= before) return after <= reference && reference <= before;
			// Exclusion (reference is either older than before or younger than after).
            return reference <= before || reference >= after;
        }

        private async Task<IMigrationResult<IResource>> MigrateSingleChannelAttributesAsync(Channel channel, DateTime? dateBefore, DateTime? dateAfter)
        {
			var result = new MigrationResult<IResource>();

			if (!IsIncludedByDate(channel.DateUpdated, dateBefore, dateAfter))
			{
				LoggingUtilities.Log($"\tChannel {channel.UniqueName} skipped. Last updated on {channel.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
				result.IncreaseChannelsSkipped();
				LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Skipped);
				return result;
			}

			if (channel.MembersCount == 0)
			{
				LoggingUtilities.Log($"Channel {channel.UniqueName} contained no members. Skipped.");
				result.IncreaseChannelsSkipped();
				LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Skipped);
				return result;
			}

			if (channel.Attributes != null && (channel.Attributes.ListingId == 0 || channel.Attributes.SellerId == 0 && channel.Attributes.BuyerId == 0))
			{
				LoggingUtilities.Log(
					$"Channel {channel.UniqueName} contained uncertain data in the attributes. Listing ID: [{channel.Attributes.ListingId}]; buyer ID [{channel.Attributes.BuyerId}]; seller ID: [{channel.Attributes.SellerId}]. Skipped.");
				result.IncreaseChannelsSkipped();
				LoggingUtilities.LogEntityProcessingResultToFile(channel.UniqueName, EntityProcessingResult.Skipped);
				return result;
			}

			int[] actualChannelMembersIds;
			int[] originalChannelMembersIds = channel.UniqueName.Split('-').Skip(1).Select(Int32.Parse).ToArray();
			if (channel.MembersCount == 1)
			{
				// We could create a Fetch method to fetch a single member, but bulk retrieve is no difference.
				HttpClientResult<Member[]> channelMemberResult = await _twilioClient.ChannelMembersBulkRetrieveAsync(channel.UniqueName);
				// If request to Twilio is successful, we will know the only member; if not, we'll proceed with empty array:
				// better not to add members to channel with single member at all rather then to re-add the removed one.
				if (!channelMemberResult.IsSuccess)
				{
					LoggingUtilities.Log($"Failed to fetch members from Twilio for the channel {channel.UniqueName}. Reason: {channelMemberResult.FormattedMessage}.");
					actualChannelMembersIds = Array.Empty<int>();
				}
				else actualChannelMembersIds = channelMemberResult.Payload.Select(m => Int32.TryParse(m.Id, out int id) ? id : 0).ToArray();
            }
			else actualChannelMembersIds = originalChannelMembersIds;

			// Checking if the original channel members exist as SB users. If not, we'll try to migrate them. If migration fails we'll still proceed.
			// In this case channel will simply be created with the members that already exist.
			// Based on the last experience, we will migrate both members' users, but still add only one (in case there is only one) as a member.
			HttpClientResult<int[]> absentMembersResult = await _sendbirdClient.WhoIsAbsentAsync(originalChannelMembersIds);
			if (absentMembersResult.IsSuccess && absentMembersResult.Payload.Length > 0)
			{
				IMigrationResult<IResource> memberMigrationResult = null;
				LoggingUtilities.Log($"Migrating the nonexistent members of the channel {channel.UniqueName}...");
				foreach (int memberId in absentMembersResult.Payload)
				{
					LoggingUtilities.Log($"\tMigrating the member with ID {memberId} of the channel {channel.UniqueName}...");
					// We won't pay attention to the date of creation when migrating channel's members.
					memberMigrationResult = await MigrateSingleUserAttributesAsync(memberId.ToString(), false, null, null);
					LoggingUtilities.Log(memberMigrationResult.UsersFailedCount > 0
						? $"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} failed. Reason: {memberMigrationResult.Message}."
						: $"\tMigration of the member with ID {memberId} of the channel {channel.UniqueName} succeeded.");
				}
				result.Consume(memberMigrationResult);
			}
			
			// As mentioned, we will still add only actual members of the channel.
			var channelMigrationResult = await MigrateChannelWithMetadataAsync(channel, actualChannelMembersIds);
			result.Consume(channelMigrationResult);
			return result;
		}
	}
}