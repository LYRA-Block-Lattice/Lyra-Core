using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using LyraLexWeb.Models;
using LyraLexWeb.Common;
using LiteDB;
using MongoDB.Driver;

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
            if(req.Email?.Length >= 6 && req.UserName?.Length >= 6 && req.AccountID?.Length == 95)
            {
                // write data to log
                var lexReqs = _ctx.Context.GetCollection<FreeLeXRequest>("lexreq");
                //var filter1 = Builders<FreeLeXRequest>.Filter.Eq("Email", req.Email);
                var filter2 = Builders<FreeLeXRequest>.Filter.Eq("AccountID", req.AccountID);
                var filter = filter2;
                var findResult = await lexReqs.FindAsync(filter);
                var resultList = findResult.ToList();
                if (resultList.Any())
                {
                    var result = resultList.First();
                    if (result.State == 1)
                    {
                        msg = "already sent";
                    }
                    else
                    {
                        msg = "in queue, please be patient.";
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
                    msg = "ok, will send";
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