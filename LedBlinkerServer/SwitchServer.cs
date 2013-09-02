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
        static int timeout = 10000;
        static readonly ConcurrentDictionary<int, TcpClient> Connections =
            new ConcurrentDictionary<int, TcpClient>();
        static AutoResetEvent ackAutoReset = new AutoResetEvent(false);
        static readonly TaskCompletionSource<bool> ClosingEvent = new TaskCompletionSource<bool>();
        static readonly byte[] PingFrame = { 0x00 };
        static readonly byte[] OnFrame = { 0x01 };
        static readonly byte[] OffFrame = { 0x02 };

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
                            connection.NoDelay = true; // flush writes immediately to the wire
                            connection.ReceiveTimeout = timeout;
                            NetworkStream stream = connection.GetStream();
                            var readBuffer = new byte[64];
                            if (await stream.ReadAsync(readBuffer, 0, 4) == 4)
                            {
                                int deviceId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readBuffer, 0));
                                Connections[deviceId] = connection;
                                try
                                {
                                    do
                                    {
                                        if (await stream.ReadAsync(readBuffer, 0, 1) == 1)
                                        {
                                            if (readBuffer[0] == 0x00) // ACK
                                            {
                                                ackAutoReset.Set();
                                            }
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
            Task.WaitAll(Connections.Values.Where(c => c != null).Select(c => c.GetStream().WriteAsync(PingFrame, 0, PingFrame.Length)).ToArray());
        }


        public static async Task<bool> Switch(int id, bool state)
        {
            TcpClient client;

            if (Connections.TryGetValue(id, out client) && client != null)
            {
                try
                {
                    var networkStream = client.GetStream();
                    if (state)
                    {
                        await networkStream.WriteAsync(OnFrame, 0, OnFrame.Length);
                    }
                    else
                    {
                        await networkStream.WriteAsync(OffFrame, 0, OffFrame.Length);
                    }

                    if (ackAutoReset.WaitOne(timeout))
                    {
                        return true;
                    }
                    else
                    {
                        client.Close();
                        Connections[id] = null;
                        return false;
                    }
                }
                catch (SocketException)
                {
                    client.Close();
                    Connections[id] = null;
                }
                catch (IOException)
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
            config.Routes.MapHttpRoute("API", "{controller}/{id}", new { id = RouteParameter.Optional });
            var controlServer = new HttpSelfHostServer(config);
            try
            {
                await controlServer.OpenAsync();
                Trace.TraceInformation("Control endpoint listening at {0}", config.BaseAddress);
            }
            catch (Exception exception)
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