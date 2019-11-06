using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;

namespace LyraLexWeb.Pages
{
    [BindProperties]
    public class SendmeLeXModel : PageModel
    {
        public string userName { get; set; }
        public string email { get; set; }
        public string accountid { get; set; }
        
        public void OnGet()
        {

        }

        public void OnPost()
        {
            // write data to txt log
            if(!string.IsNullOrWhiteSpace(Request.Form["accountid"]))
            {
                var log = $"{DateTime.Now}|{Request.Form["userName"]}|{Request.Form["email"]}|{Request.Form["accountid"]}";
                
            }
            // send LeX
        }
    }
}