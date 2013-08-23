namespace LedBlinkService
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.ServiceRuntime;
    
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            Trace.WriteLine("Starting", "Information");

            var controlEp = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["controlEP"];
            var deviceEp = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["deviceEP"];
            SwitchServer.Run(deviceEp.IPEndpoint, controlEp.IPEndpoint);
        }
    }
}