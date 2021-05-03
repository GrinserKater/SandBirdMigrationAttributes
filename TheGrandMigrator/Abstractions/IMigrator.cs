using System;
using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<MigrationResult<User>> MigrateUsersAttributesAsync(DateTime? laterThan, int limit, int pageSize);
		Task<MigrationResult<Channel>> MigrateChannelsAttributesAsync(DateTime? laterThan, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? laterThan, int accountUserId, int limit, int pageSize);
	}
}
