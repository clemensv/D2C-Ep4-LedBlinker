namespace LedBlinkerTestHost
{
    using System.Diagnostics;
    using System.Net;
    using LedBlinkService;

    class Program
    {
        static void Main(string[] args)
        {
            Trace.WriteLine("Starting", "Information");

            SwitchServer.Run(new IPEndPoint(IPAddress.Any, 10100), new IPEndPoint(IPAddress.Any, 10101));
        }
    }
}