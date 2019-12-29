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
    public class LyraNotifyController : ControllerBase
    {
        private INotifyAPI _notify;
        public LyraNotifyController(INotifyAPI notify)
        {
            _notify = notify;
        }
        // GET: api/LyraNode
        [HttpGet]
        public async Task<GetNotificationAPIResult> GetAsync(string AccountID, string Signature)
        {
            return await _notify.GetNotification(AccountID, Signature);
        }
    }
}
