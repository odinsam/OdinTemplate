using Microsoft.AspNetCore.Mvc;

namespace GenerateWebApiTemplate.Controllers
{

    public class HealtController : Controller
    {
        [HttpGet]
        public IActionResult OK()
        {
            return Ok();
        }
    }
}
