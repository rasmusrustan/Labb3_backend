using Microsoft.AspNetCore.Mvc;

namespace Labb3_backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WordsController : ControllerBase
    {
        private static readonly string[] Words = { "katt", "bil", "hus", "tr�d", "dator", "boll" };
        private readonly Random _random = new();

        [HttpGet("random")]
        public IActionResult GetRandomWord()
        {
            var word = Words[_random.Next(Words.Length)];
            return Ok(word);
        }
    }
}
