using System;
using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<MigrationResult<IResource>> MigrateUsersAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int accountUserId, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateSingleChannelAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, string channelUniqueIdentifier);
	}
}
