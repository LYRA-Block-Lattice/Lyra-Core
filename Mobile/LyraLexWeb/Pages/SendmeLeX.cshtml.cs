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

namespace LyraLexWeb.Pages
{
    [BindProperties]
    public class SendmeLeXModel : PageModel
    {
        public FreeLeXRequest req { get; set; }

        public string msg { get; set; }

        private readonly LiteDbContext _db;

        public SendmeLeXModel(LiteDbContext db)
        {
            _db = db;
        }
        public void OnGet()
        {

        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            // write data to log
            var lexReqs = _db.Context.GetCollection<FreeLeXRequest>("FreeLeXRequest");
            if(lexReqs.Exists(Query.EQ("Email", req.Email)))
            {
                msg = "already sent";
            }
            else
            {
                // send LeX
                msg = "ok, will send";
            }

            return Page();

            //msg = $"{DateTime.Now}|{Request.Form["userName"]}|{Request.Form["email"]}|{Request.Form["accountid"]}";
            
        }
    }
}