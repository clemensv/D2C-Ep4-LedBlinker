namespace LedBlinkService
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.SelfHost;
    using Microsoft.ServiceBus.Messaging;
    using Ninject;

    public static class SwitchServer
    {
        static int timeout = 10000;
        static readonly TaskCompletionSource<bool> ClosingEvent = new TaskCompletionSource<bool>();
        static readonly byte[] PingFrame = { 0x00 };
        static readonly byte[] OnFrame = { 0x01 };
        static readonly byte[] OffFrame = { 0x02 };

        public static void Run(IPEndPoint deviceEndPoint, IPEndPoint controlEndPoint, MessagingFactory messagingFactory)
        {
            Task.WaitAll(new[]
            {
                RunControlEndpoint(controlEndPoint, messagingFactory), 
                RunDeviceEndpoint(deviceEndPoint, messagingFactory)
            });
        }

        public static void SignalStop()
        {
            ClosingEvent.SetResult(true);
        }

        static async Task RunDeviceEndpoint(IPEndPoint deviceEP, MessagingFactory messagingFactory)
        {
            var deviceServer = new TcpListener(deviceEP);
            deviceServer.Start(10);

            try
            {
                do
                {
                    TcpClient connection = await deviceServer.AcceptTcpClientAsync();
                    if (connection != null)
                    {
                        try
                        {
                            var pendingSessions = new Queue<string>();
                            connection.NoDelay = true; // flush writes immediately to the wire
                            connection.ReceiveTimeout = timeout;
                            NetworkStream deviceConnectionStream = connection.GetStream();
                            var readBuffer = new byte[64];
                            if (await deviceConnectionStream.ReadAsync(readBuffer, 0, 4) == 4)
                            {
                                int deviceId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readBuffer, 0));

                                try
                                {
                                    Task<BrokeredMessage> queueReceive = null;
                                    Task<int> socketRead = null;
                                    Task pingDelay = null;
                                    var cancelPing = new CancellationTokenSource();

                                    // set up the receiver for the per-device queue and senbder for the response queue
                                    var deviceQueueReceiver = messagingFactory.CreateMessageReceiver(string.Format("dev-{0:X8}", deviceId), ReceiveMode.PeekLock);
                                    var responseQueueSender = messagingFactory.CreateMessageSender(string.Format("ingress"));

                                    do
                                    {
                                        // receive from the queue
                                        queueReceive = queueReceive ?? deviceQueueReceiver.ReceiveAsync();
                                        // read from the socket
                                        socketRead = socketRead ?? deviceConnectionStream.ReadAsync(readBuffer, 0, 1);
                                        // ping delay
                                        pingDelay = pingDelay ?? Task.Delay(TimeSpan.FromSeconds(235), cancelPing.Token);

                                        // wait for any of the four operations (including completion) to be done
                                        var completedTask = await Task.WhenAny(queueReceive, socketRead, pingDelay, ClosingEvent.Task);

                                        if (completedTask == socketRead)
                                        {
                                            try
                                            {
                                                // read from the socket completed and not a ping
                                                if (socketRead.Result == 1)
                                                {
                                                    if (readBuffer[0] != PingFrame[0])
                                                    {
                                                        await responseQueueSender.SendAsync(new BrokeredMessage()
                                                            {
                                                                SessionId = pendingSessions.Dequeue(),
                                                                Properties = {{"Ack", (int) readBuffer[0]}}
                                                            });
                                                    }
                                                }
                                                else
                                                {
                                                    // no more data from the socket. Break out of the loop.
                                                    break;
                                                }
                                            }
                                            finally
                                            {
                                                socketRead = null;
                                            }
                                        }
                                        else if (completedTask == queueReceive)
                                        {
                                            try
                                            {
                                                // read from the queue completed
                                                var message = queueReceive.Result;
                                                if (message != null)
                                                {
                                                    var command = message.Properties["Cmd"] as string;
                                                    if (command != null)
                                                    {
                                                        switch (command.ToUpperInvariant())
                                                        {
                                                            case "ON":
                                                                pendingSessions.Enqueue(message.ReplyToSessionId);
                                                                await deviceConnectionStream.WriteAsync(OnFrame, 0, OnFrame.Length);
                                                                await message.CompleteAsync();
                                                                cancelPing.Cancel();
                                                                break;
                                                            case "OFF":
                                                                pendingSessions.Enqueue(message.ReplyToSessionId);
                                                                await deviceConnectionStream.WriteAsync(OffFrame, 0, OffFrame.Length);
                                                                await message.CompleteAsync();
                                                                cancelPing.Cancel();
                                                                break;
                                                        }
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                queueReceive = null;
                                            }
                                        }
                                        else if (completedTask == pingDelay)
                                        {
                                            try
                                            {
                                                if (pingDelay.IsCanceled)
                                                {
                                                    cancelPing = new CancellationTokenSource();
                                                }
                                                else
                                                {
                                                    await deviceConnectionStream.WriteAsync(PingFrame, 0, PingFrame.Length);
                                                }
                                            }
                                            finally
                                            {
                                                pingDelay = null;
                                            }
                                        }
                                        else
                                        {
                                            // closing event was fired
                                            break;
                                        }
                                    }
                                    while (true);
                                }
                                finally
                                {
                                    connection.Close();
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
                deviceServer.Stop();
            }
        }


        static async Task RunControlEndpoint(IPEndPoint controlEP, MessagingFactory messagingFactory)
        {
            var config = new HttpSelfHostConfiguration(new UriBuilder(Uri.UriSchemeHttp, "localhost", controlEP.Port).Uri);

            var injector = new StandardKernel();
            injector.Bind<MessagingFactory>().ToConstant(messagingFactory);
            config.DependencyResolver = new Ninject.WebApi.DependencyResolver.NinjectDependencyResolver(injector);

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