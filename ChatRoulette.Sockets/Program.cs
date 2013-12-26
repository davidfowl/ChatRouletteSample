using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatRoulette.Sockets
{
    class Program
    {
        private static ConcurrentQueue<ChatClient> _queue = new ConcurrentQueue<ChatClient>();

        static void Main(string[] args)
        {
            var server = new Socket(SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Any, 4000));
            server.Listen(1000);

            HandleAccept(server).Wait();
        }

        private static async Task HandleAccept(Socket server)
        {
            while (true)
            {
                var client = await AcceptAsync(server);

                Match(new ChatClient(client));
            }
        }

        // Fire and forget
        private static async void Match(ChatClient connection)
        {
            try
            {
                await connection.WriteLineAsync("Waiting for a partner...");

                ChatClient partner;
                if (_queue.TryDequeue(out partner) && partner.Socket.Connected)
                {
                    await ChatAsync(partner, connection);
                }
                else
                {
                    _queue.Enqueue(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static async Task ChatAsync(ChatClient from, ChatClient to)
        {
            await from.WriteLineAsync("Found one say hi");
            await to.WriteLineAsync("Found one say hi");

            var fromTask = from.Stream.CopyToAsync(to.Stream);
            var toTask = to.Stream.CopyToAsync(from.Stream);

            var result = await Task.WhenAny(fromTask, toTask);

            if (fromTask.IsCompleted && toTask.IsCompleted)
            {
                // Noop
                from.Dispose();
                to.Dispose();
            }
            else if (result == fromTask)
            {
                from.Dispose();
                await to.WriteLineAsync("Your partner left!");
                Match(to);
            }
            else if (result == toTask)
            {
                to.Dispose();
                await from.WriteLineAsync("Your partner left!");
                Match(from);
            }
        }

        private static Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginAccept(cb, state), ar => socket.EndAccept(ar), null);
        }

        private class ChatClient : IDisposable
        {
            public NetworkStream Stream { get; private set; }
            public Socket Socket { get; private set; }

            public ChatClient(Socket socket)
            {
                Socket = socket;
                Stream = new NetworkStream(socket);
            }

            public async Task WriteLineAsync(string text)
            {
                var data = Encoding.UTF8.GetBytes(text + Environment.NewLine);

                await Stream.WriteAsync(data, 0, data.Length);
            }

            public void Dispose()
            {
                Stream.Dispose();
            }

            internal void Reset()
            {
                Stream = new NetworkStream(Socket);
            }
        }
    }
}
