namespace LedBlinkService
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.SelfHost;
   
    public static class SwitchServer
    {
        static readonly ConcurrentDictionary<int, TcpClient> Connections =
            new ConcurrentDictionary<int, TcpClient>();
        static readonly TaskCompletionSource<bool> ClosingEvent = new TaskCompletionSource<bool>();
        static readonly byte[] PingFrame = {0x00};
        static readonly byte[] OnFrame = {0x01};
        static readonly byte[] OffFrame = {0x02};

        public static void Run(IPEndPoint deviceEndPoint, IPEndPoint controlEndPoint)
        {
            Task.WaitAll(new[]
            {
                RunControlEndpoint(controlEndPoint), 
                RunDeviceEndpoint(deviceEndPoint)
            });
        }

        public static void SignalStop()
        {
            ClosingEvent.SetResult(true);
        }

        static async Task RunDeviceEndpoint(IPEndPoint deviceEP)
        {
            var deviceServer = new TcpListener(deviceEP);
            deviceServer.Start(10);
            var pingTimer = new Timer(s => PingConnections(), Connections, TimeSpan.FromSeconds(55),
                TimeSpan.FromSeconds(55));

            try
            {
                do
                {
                    TcpClient connection = await deviceServer.AcceptTcpClientAsync();
                    if (connection != null)
                    {
                        try
                        {
                            NetworkStream stream = connection.GetStream();
                            var buffer = new byte[64];
                            if (await stream.ReadAsync(buffer, 0, 4) == 4)
                            {
                                int deviceId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));
                                Connections[deviceId] = connection;
                                try
                                {
                                    do
                                    {
                                        if (await stream.ReadAsync(buffer, 0, 4) == 4)
                                        {
                                            // discard telemetry records for the time being
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    } while (true);
                                }
                                finally
                                {
                                    connection.Close();
                                    Connections[deviceId] = null;
                                }
                            }
                            else
                            {
                                connection.Close();
                            }
                        }
                        catch (SocketException se)
                        {
                            // log
                            Trace.TraceError(se.Message);
                            connection.Close();
                        }
                        catch (IOException e)
                        {
                            Trace.TraceError(e.Message);
                            connection.Close();
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError(e.Message);
                        }
                    }
                } while (true);
            }
            finally
            {
                pingTimer.Dispose();
                deviceServer.Stop();
            }
        }

        static void PingConnections()
        {
            Task.WaitAll(
                Connections.Values.Select(
                    connection => connection.GetStream().WriteAsync(PingFrame, 0, PingFrame.Length)).ToArray());
        }


        public static async Task<bool> Switch(int id, bool state)
        {
            TcpClient client;

            if (Connections.TryGetValue(id, out client))
            {
                try
                {
                    if (state)
                    {
                        await client.GetStream().WriteAsync(OnFrame, 0, OnFrame.Length);
                    }
                    else
                    {
                        await client.GetStream().WriteAsync(OffFrame, 0, OffFrame.Length);
                    }
                    return true;
                }
                catch (SocketException)
                {
                    client.Close();
                    Connections[id] = null;
                }
            }
            return false;
        }

        static async Task RunControlEndpoint(IPEndPoint controlEP)
        {
            var config = new HttpSelfHostConfiguration(
                new UriBuilder(Uri.UriSchemeHttp, "localhost", controlEP.Port).Uri);
            config.Routes.MapHttpRoute("API", "{controller}/{id}", new {id = RouteParameter.Optional});
            var controlServer = new HttpSelfHostServer(config);
            try
            {
                await controlServer.OpenAsync();
                Trace.TraceInformation("Control endpoint listening at {0}", config.BaseAddress);
            }
            catch( Exception exception)
            {
                controlServer.Dispose();
                Trace.TraceError("Control endpoint cannot open, {0}", exception.Message);
                throw;
            }
            await ClosingEvent.Task;
            try
            {
                await controlServer.CloseAsync();
            }
            finally
            {
                controlServer.Dispose();
            }
        }
    }
}