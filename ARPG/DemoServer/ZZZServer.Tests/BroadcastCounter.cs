using System.Threading;
using Kirara.Network;

namespace ZZZServer.Tests
{
    /// <summary>Counts the server's per-tick authority broadcasts arriving at the bots, to confirm the round trip is live.</summary>
    public class BroadcastCounter : MsgHandler<NotifyUpdateFromAuthority>
    {
        private static long _count;
        public static long Count => Interlocked.Read(ref _count);

        protected override void Run(Session session, NotifyUpdateFromAuthority msg)
        {
            Interlocked.Increment(ref _count);
        }
    }
}
