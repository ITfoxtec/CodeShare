using System.Collections.Concurrent;

namespace WebApp.Identity
{
    public class LogoutMemoryCache
    {
        public ConcurrentBag<string> List { get; private set; } = new ConcurrentBag<string>();

        public void Remove(string item)
        {
            List = new ConcurrentBag<string>(List.Except(new[] { item }));
        }
    }
}
