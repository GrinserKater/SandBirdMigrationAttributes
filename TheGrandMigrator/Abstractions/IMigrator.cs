using System;
using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<MigrationResult<User>> MigrateUsersAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateChannelsAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(DateTime? dateBefore, DateTime? dateAfter, int accountUserId, int limit, int pageSize);
	}
}
