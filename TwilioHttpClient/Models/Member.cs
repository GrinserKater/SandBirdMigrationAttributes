using TwilioHttpClient.Abstractions;

namespace TwilioHttpClient.Models
{
    public class Member : IResource
    {
        public string Id { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
