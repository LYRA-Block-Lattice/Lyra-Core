using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LyraLexWeb2
{
    [Route("api/[controller]")]
    [ApiController]
    public class LyraNodeController : ControllerBase
    {
        private INodeAPI _node;
        public LyraNodeController(INodeAPI node)
        {
            _node = node;
        }
        // GET: api/LyraNode
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAsync()
        {
            return await _node.GetSyncHeight();
        }

        // GET: api/LyraNode/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/LyraNode
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT: api/LyraNode/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
