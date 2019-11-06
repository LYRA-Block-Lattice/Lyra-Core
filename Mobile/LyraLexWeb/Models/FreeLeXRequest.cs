using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Models
{
    public class FreeLeXRequest
    {
        public DateTime TimeRequested => DateTime.Now;
        [Required]
        [MinLength(6)]
        public string UserName { get; set; }
        [Required]
        [MinLength(6)]
        public string Email { get; set; }
        [Required]
        [StringLength(95)]
        public string AccountID { get; set; }
    }
}
