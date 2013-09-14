namespace LedBlinkService
{
    using System.Diagnostics;
    using Microsoft.ServiceBus.Messaging;
    using Microsoft.WindowsAzure.ServiceRuntime;
    
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            Trace.WriteLine("Starting", "Information");

            var controlEp = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["controlEP"];
            var deviceEp = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["deviceEP"];
            var messagingFactory =
                MessagingFactory.CreateFromConnectionString(
                    RoleEnvironment.GetConfigurationSettingValue("serviceBusConnectionString"));
            SwitchServer.Run(deviceEp.IPEndpoint, controlEp.IPEndpoint, messagingFactory);
        }
    }
}