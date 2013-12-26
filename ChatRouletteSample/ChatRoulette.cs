using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace ChatRoulette.SignalR
{
    [HubName("chat")]
    public class ChatRoulette : Hub
    {
        private static ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private static ConcurrentDictionary<string, string> _pairs = new ConcurrentDictionary<string, string>();

        public void Send(string message)
        {
            string partnerConnectionId;
            if (_pairs.TryGetValue(Context.ConnectionId, out partnerConnectionId))
            {
                Clients.Client(partnerConnectionId).send(message);
                Clients.Caller.send(message);
            }
        }

        public override Task OnConnected()
        {
            Match(Context.ConnectionId);

            return base.OnConnected();
        }

        public override Task OnDisconnected()
        {
            string partnerConnectionId;
            if (_pairs.TryRemove(Context.ConnectionId, out partnerConnectionId))
            {
                Clients.Client(partnerConnectionId).send("Your partner left!");

                string otherConnectionId;
                // Remove current user from the mapping
                _pairs.TryRemove(partnerConnectionId, out otherConnectionId);

                Match(partnerConnectionId);
            }

            return base.OnDisconnected();
        }

        private void Match(string connectionId)
        {
            // REVIEW: This needs to be more threadsafe as connections can disconnect while being matched.

            string partnerConnectionId;
            if (_queue.TryDequeue(out partnerConnectionId))
            {
                Clients.Clients(new[] { connectionId, partnerConnectionId }).send("Found one say hi");

                // Pair up
                _pairs.TryAdd(connectionId, partnerConnectionId);
                _pairs.TryAdd(partnerConnectionId, Context.ConnectionId);
            }
            else
            {
                _queue.Enqueue(connectionId);

                Clients.Client(connectionId).send("Waiting for a partner...");
            }
        }
    }
}