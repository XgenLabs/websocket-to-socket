using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServer
{
    class Program
    {
        static readonly ConcurrentDictionary<string, SocketConnection> _clients = new ConcurrentDictionary<string, SocketConnection>(Environment.ProcessorCount * 2, 101);

        static void Main(string[] args)
        {
            var cancellation = new CancellationTokenSource();

            ThreadPool.QueueUserWorkItem(state =>
            {
                StartListening(cancellation.Token).Wait();
            });

            Run(cancellation).Wait();
        }

        static async Task Run(CancellationTokenSource cancellation)
        {
            while (true)
            {
                Console.WriteLine("*** CHOOSE AN OPTION ***");
                Console.WriteLine("- Send message pong to specific client (1)");
                Console.WriteLine("- List connected clients (2)");
                Console.WriteLine("- Exit (0)");

                var line = Console.ReadLine();

                if (!int.TryParse(line.Trim(), out int option))
                {
                    cancellation.Cancel();
                }

                if (option == 1)
                {
                    Console.Write("Would you like send a message for what client? ");

                    var connectionId = Console.ReadLine();
                    if (_clients.TryGetValue(connectionId, out var connection))
                    {
                        await connection.Send($"pong - client: {connection.ConnectionId}");
                        Console.WriteLine("Message sent.");
                    }
                }

                if (option == 2)
                {
                    Console.WriteLine("Clients connecteds");
                    foreach (var item in _clients.Keys)
                    {
                        Console.WriteLine($"Client: {item}");
                    }
                }

                if (option == 0)
                {
                    cancellation.Cancel();
                    break;
                }
            }
        }

        static async Task StartListening(CancellationToken token)
        {
            Console.WriteLine("Waiting for incoming connection...");

            while (!token.IsCancellationRequested)
            {
                using var connection = await Listener.Start();
                if (!_clients.TryAdd(connection.ConnectionId, connection))
                {
                    Console.WriteLine($"Connection {connection.ConnectionId} refused.");
                }

                ThreadPool.QueueUserWorkItem(state =>
                {
                    Console.WriteLine($"Listening message from: {connection.ConnectionId}");
                    ReceiveLoopAsync(connection, token).Wait();
                });
            }

            Console.WriteLine("Shutdown");
        }

        private static async Task ReceiveLoopAsync(SocketConnection connection, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var message = await connection.Receive();
                Console.WriteLine($"Received from {connection.ConnectionId}: {message}");
            }
        }
    }

    public class Listener
    {
        public static Task<SocketConnection> Start()
        {
            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, 11000);

            // Create a TCP/IP socket.  
            Socket listener = new Socket(localEndPoint.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            var taskSource = new TaskCompletionSource<SocketConnection>();

            // Bind the socket to the local endpoint and listen for incoming connections.  
            listener.Bind(localEndPoint);
            listener.Listen(10);

            void callback(IAsyncResult result)
            {
                try
                {
                    Console.WriteLine($"New client connecting");
                    var resultSocket = listener.EndAccept(result);
                    var socketConnection = new SocketConnection(listener, resultSocket);
                    Console.WriteLine($"New client connected:{socketConnection.ConnectionId}");
                    taskSource.SetResult(socketConnection);
                }
                catch (Exception e)
                {
                    taskSource.SetException(e);
                }

            }

            listener.BeginAccept(new AsyncCallback(callback), null);

            return taskSource.Task;
        }
    }

    public class SocketConnection : IDisposable
    {
        public Socket _server;
        public Socket _client;
        public string ConnectionId { get; }

        public SocketConnection(Socket server, Socket client)
        {
            _server = server;
            _client = client;
            ConnectionId = Guid.NewGuid().ToString();
        }

        public Task<string> Receive()
        {
            var taskSource = new TaskCompletionSource<string>();
            var buffer = new byte[512];

            void callback(IAsyncResult ar)
            {
                var read = _client.EndReceive(ar);
                if (read == 0)
                {
                    taskSource.SetCanceled();
                    return;
                }

                var message = Encoding.GetEncoding("iso-8859-1").GetString(buffer, 0, read);
                taskSource.SetResult(message);
            }

            _client.BeginReceive(buffer, 0, 512, SocketFlags.None, new AsyncCallback(callback), null);

            return taskSource.Task;
        }

        public Task Send(string message)
        {
            var taskSource = new TaskCompletionSource<bool>();
            var buffer = Encoding.GetEncoding("iso-8859-1").GetBytes(message);

            void callback(IAsyncResult result)
            {
                try
                {
                    _client.EndSend(result);
                    taskSource.SetResult(false);
                }
                catch (Exception e)
                {
                    taskSource.SetException(e);
                }
            }

            _client.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(callback), null);

            return taskSource.Task;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }
            if (disposing)
            {
                try
                {
                    _server.Shutdown(SocketShutdown.Both);
                }
                catch { }
                _server.Dispose();
            }
            disposedValue = true;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
