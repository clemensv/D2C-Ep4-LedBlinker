namespace LedBlinkService
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.ServiceBus.Messaging;

    public class SwitchController : ApiController
    {
        readonly MessagingFactory factory;

        public SwitchController(MessagingFactory factory)
        {
            this.factory = factory;
        }

        [HttpPut]
        public async Task<HttpResponseMessage> PutSwitch(int id, bool state)
        {
            var operationTimeout = TimeSpan.FromSeconds(10);
            var stopwatch = new Stopwatch();
            var sender = this.factory.CreateMessageSender(string.Format("dev-{0:X8}", id));
            var receiver = this.factory.CreateQueueClient(string.Format("ingress"));
            var sessionId = Guid.NewGuid().ToString();

            try
            {
                await sender.SendAsync(new BrokeredMessage()
                    {
                        ReplyToSessionId = sessionId,
                        TimeToLive = TimeSpan.FromSeconds(5),
                        Properties = {{"Cmd", state ? "On" : "Off"}}
                    });
                await sender.CloseAsync();

                var session = await receiver.AcceptMessageSessionAsync(sessionId, operationTimeout - stopwatch.Elapsed);
                if (session != null)
                {
                    var response = await session.ReceiveAsync(operationTimeout - stopwatch.Elapsed);
                    if (response != null)
                    {
                        object prop;
                        if (response.Properties.TryGetValue("Ack", out prop) &&
                            prop is int && (int) prop == 1)
                        {
                            return new HttpResponseMessage(HttpStatusCode.OK);
                        }
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    }
                    await session.CloseAsync();
                }
                return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
            }
            catch (MessagingEntityNotFoundException)
            {
                sender.Abort();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            catch (Exception e)
            {
                // bad style
                Trace.TraceError("Error trying to switch {0} into {1}: {2}", id, state, e.Message);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            finally
            {
                receiver.Close();
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetSwitch(int id)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}