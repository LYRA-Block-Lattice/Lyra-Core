using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Exchange;
using LyraLexWeb.Common;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LyraLexWeb
{
    [Route("api/[controller]")]
    public class ExchangeController : Controller
    {
        private readonly MongodbContext _ctx;

        public ExchangeController(MongodbContext ctx)
        {
            _ctx = ctx;
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

        // POST api/<controller>
        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody]TokenTradeOrder value)
        {
            CancelKey key;
            if (!value.VerifySignature(value.AccountID))
            {
                key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
            }
            else
            {
                key = await _ctx.AddOrder(Request, value);
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
