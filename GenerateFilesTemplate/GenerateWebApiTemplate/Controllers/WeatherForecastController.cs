using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GenerateWebApiTemplate.Controllers
{

    public class HealtController : Controller
    {
        [HttpGet]
        public IActionResult Ok()
        {

        }
    }
}
