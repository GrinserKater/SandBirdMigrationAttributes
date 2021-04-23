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
			if(limit > 0)
				migrateNoMoreThan = limit;

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

				if (channel.Attributes == null)
				{
					Trace.WriteLine($"Channel {channel.UniqueName} contained to attributes to migrate.");
					result.EntitiesSucceeded.Add(channel);
					continue;
				}

				Trace.WriteLine($"Migrating channel {channel.UniqueName}...");

				OperationResult operationResult = await MigrationUtilities.TryCreateOrUpdateChannelWithMetadata(_sendbirdClient, channel, result);

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
	}
}
