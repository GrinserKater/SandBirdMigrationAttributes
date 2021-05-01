using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<MigrationResult<User>> MigrateUsersAttributesAsync(int limit, int pageSize);
		Task<MigrationResult<Channel>> MigrateChannelsAttributesAsync(int limit, int pageSize);
		Task<MigrationResult<IResource>> MigrateSingleAccountAttributesAsync(int accountUserId, int limit, int pageSize);
	}
}
