using System.Collections.Generic;
using TheGrandMigrator.Abstractions;
using TwilioHttpClient.Abstractions;

namespace TheGrandMigrator.Models
{
    public class MigrationResult<T> : IMigrationResult<T>
    {

        public int UsersFetchedCount { get; private set; }
        public int ChannelsFetchedCount { get; private set; }
        public int UsersSuccessCount { get; private set; }
        public int ChannelsSuccessCount { get; private set; }
        public int UsersSkippedCount { get; private set; }
        public int ChannelsSkippedCount { get; private set; }
        public int UsersFailedCount { get; private set; }
        public int ChannelsFailedCount { get; private set; }
        public int TotalFetchedCount => UsersFetchedCount + ChannelsFetchedCount;
        public int TotalSuccessCount => UsersSuccessCount + ChannelsSuccessCount;
        public int TotalSkippedCount => UsersSkippedCount + ChannelsSkippedCount;
        public int TotalFailedCount => UsersFailedCount + ChannelsFailedCount;
        public List<string> ErrorMessages { get; }
        public string Message { get; set; }
        
        public MigrationResult()
        {
            ErrorMessages = new List<string>();
        }

        public void IncreaseUsersFetched()
        {
            UsersFetchedCount++;
        }
        
        public void IncreaseChannelsFetched()
        {
            ChannelsFetchedCount++;
        }
        
        public void IncreaseUsersSuccess()
        {
            UsersSuccessCount++;
        }
        
        public void IncreaseChannelsSuccess()
        {
            ChannelsSuccessCount++;
        }
        
        public void IncreaseUsersSkipped()
        {
            UsersSkippedCount++;
        }
        
        public void IncreaseChannelsSkipped()
        {
            ChannelsSkippedCount++;
        }
        
        public void IncreaseUsersFailed()
        {
            UsersFailedCount++;
        }
        
        public void IncreaseChannelsFailed()
        {
            ChannelsFailedCount++;
        }

        public void Consume(IMigrationResult<IResource> consumee, string substituteMessage = null)
        {
            if(consumee == null) return;

            UsersFetchedCount += consumee.UsersFetchedCount;
            ChannelsFetchedCount += consumee.ChannelsFetchedCount;
            UsersSuccessCount += consumee.UsersSuccessCount;
            ChannelsSuccessCount += consumee.ChannelsSuccessCount;
            UsersSkippedCount += consumee.UsersSkippedCount;
            ChannelsSkippedCount += consumee.ChannelsSkippedCount;
            UsersFailedCount += consumee.UsersFailedCount;
            ChannelsFailedCount += consumee.ChannelsFailedCount;
            
            Message = substituteMessage ?? consumee.Message;
            ErrorMessages.AddRange(consumee.ErrorMessages);
        }
    }
}