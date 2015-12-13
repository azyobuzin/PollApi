using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using LitJson;
using Microsoft.AspNet.Mvc;

namespace PollApi.Controllers
{
    [Route("v1")]
    public class ApiController : Controller
    {
        private readonly MyCacheService _cache;

        public ApiController(MyCacheService cache)
        {
            this._cache = cache;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> Get(ulong id)
        {
            var result = await this._cache.GetOrSet(id, GetPollInfo).ConfigureAwait(false);
            return result != null
                ? (ActionResult)this.Json(result)
                : this.HttpNotFound("Not a poll tweet.");
        }

        private static async Task<PollInfo> GetPollInfo(ulong id)
        {
            string html;
            using (var client = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://twitter.com/i/cards/tfw/v1/" + id.ToString("D"));
                var headers = req.Headers;
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko");

                var res = await client.SendAsync(req).ConfigureAwait(false);

                if (res.StatusCode == HttpStatusCode.NotFound)
                    return null;

                res.EnsureSuccessStatusCode();

                html = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            var dom = new HtmlParser().Parse(html);

            var cardInfoJson = ((IHtmlScriptElement)dom.QuerySelector("script[type=\"text/twitter-cards-serialization\"]")).Text;
            var cardInfo = JsonMapper.ToObject(cardInfoJson)["card"];

            if (!Regex.IsMatch((string)cardInfo["card_name"], "poll[234]choice_text_only"))
                return null;

            var result = JsonMapper.ToObject<PollInfo>(cardInfo.ToJson());

            result.choices = dom.GetElementsByClassName("PollXChoiceTextOnly-choice--text")
                .Select(x => x.TextContent).ToArray();

            Debug.Assert(ulong.Parse(result.tweet_id) == id);
            Debug.Assert(result.choices.Length == result.choice_count);

            return result;
        }
    }
}
