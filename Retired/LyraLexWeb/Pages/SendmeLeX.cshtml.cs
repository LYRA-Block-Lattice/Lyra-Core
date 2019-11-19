using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using LyraLexWeb.Services;
using LiteDB;
using MongoDB.Driver;
using Lyra.Exchange;

namespace LyraLexWeb.Pages
{
    [BindProperties]
    public class SendmeLeXModel : PageModel
    {
        public FreeLeXRequest req { get; set; }

        public string msg { get; set; }

        private readonly MongodbContext _ctx;

        public SendmeLeXModel(MongodbContext db)
        {
            _ctx = db;
        }
        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            // filter user input at server side
            if(req.Email?.Length >= 6 && req.UserName?.Length >= 3 && req.AccountID?.Length == 95)
            {
                // write data to log
                var lexReqs = _ctx.Context.GetCollection<FreeLeXRequest>("lexreq");
                var filter1 = Builders<FreeLeXRequest>.Filter.Eq("Email", req.Email);
                var filter2 = Builders<FreeLeXRequest>.Filter.Eq("AccountID", req.AccountID);
                var filter = filter1 & filter2;
                var findResult = await lexReqs.FindAsync(filter);
                var resultList = findResult.ToList();
                if (resultList.Any())
                {
                    var result = resultList.First();
                    // send result: 1=success; 2=InvalidDestinationAccountId; 3=unknown error
                    if (result.State == 1)
                    {
                        msg = $"Free LeX has already sent to {req.Email} [{req.AccountID}]";
                    }
                    else if(result.State == 0)
                    {
                        msg = "Your request is now in sending queue, please be patient.";
                    }
                    else
                    {
                        msg = $"Something wrong sending LeX to {req.Email} [{req.AccountID}]";
                    }
                }
                else
                {
                    // add to database queue. send by other daemon
                    var queueReq = new FreeLeXRequest
                    {
                        AccountID = req.AccountID,
                        Email = req.Email,
                        UserName = req.UserName,
                        State = 0
                    };
                    await lexReqs.InsertOneAsync(queueReq);
                    msg = "Your request is now in sending queue, it will be processed soon.";
                }                
            }
            else
            {
                msg = "invalid user input data";
            }

            //msg = $"{DateTime.Now}|{Request.Form["userName"]}|{Request.Form["email"]}|{Request.Form["accountid"]}";
            return Page();
        }
    }
}