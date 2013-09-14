namespace LedBlinkerTestHost
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using LedBlinkService;
    using Microsoft.ServiceBus.Messaging;

    class Program
    {
        static void Main(string[] args)
        {
            Trace.WriteLine("Starting", "Information");

            if (args.Length == 0)
            {
                Console.WriteLine("connection string required");
                return;
            }

            var messagingFactory = MessagingFactory.CreateFromConnectionString(args[0]);
            SwitchServer.Run(new IPEndPoint(IPAddress.Any, 10100), new IPEndPoint(IPAddress.Any, 10101), messagingFactory);
        }
    }
}