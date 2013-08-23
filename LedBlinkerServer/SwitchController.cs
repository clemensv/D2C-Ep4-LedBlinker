namespace LedBlinkService
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class SwitchController : ApiController
    {
        [HttpPut]
        public async Task<HttpResponseMessage> PutSwitch(int id, bool state)
        {
            if (await SwitchServer.Switch(id, state))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetSwitch(int id)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}