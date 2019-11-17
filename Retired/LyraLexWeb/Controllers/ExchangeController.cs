using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Exchange;
using LyraLexWeb.Common;
using LyraLexWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LyraLexWeb
{
    [Route("api/[controller]")]
    public class ExchangeController : Controller
    {
        private readonly IHubContext<ExchangeHub> _hubContext;
        MongodbContext dbCtx;

        public ExchangeController(IHubContext<ExchangeHub> hubContext, MongodbContext ctx)
        {
            _hubContext = hubContext;
            dbCtx = ctx;
        }

        // GET: api/<controller>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Json("Lyra DEX Online");
        }

        [HttpGet("tokens/{key}")]
        [HttpGet("tokens")]
        public IEnumerable<string> SearchToken(string key)
        {
            return new string[] { "Debug.Test", "Wizard.Coin", $"keyis {key}" };
        }

        // GET api/<controller>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitOrder([FromBody]TokenTradeOrder value)
        {
            CancelKey key;
            if (!value.VerifySignature(value.AccountID))
            {
                key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
            }
            else
            {
                key = await dbCtx.AddOrder(Request, value);
                // signal the dealing engine

            }
            return Json(key);
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
