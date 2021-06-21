using System;
using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<IMigrationResult<IResource>> MigrateUsersAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<IMigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<IMigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int accountUserId, int limit, int pageSize);
		Task<IMigrationResult<IResource>> MigrateSingleChannelAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, string channelUniqueIdentifier);
	}
}