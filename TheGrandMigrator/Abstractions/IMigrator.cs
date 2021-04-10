using System.Threading.Tasks;
using TheGrandMigrator.Models;
using TwilioHttpClient.Models;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrator
	{
		Task<MigrationResult<User>> MigrateUsersAttributesAsync(int limit, int pageSize);
		Task<MigrationResult<Channel>> MigrateChannelsAttributesAsync(int limit, int pageSize);
	}
}
