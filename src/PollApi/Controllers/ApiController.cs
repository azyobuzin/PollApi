using System;
using System.Diagnostics;
using System.Globalization;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace PollApi.Controllers
{
    [Route("v1")]
    public class ApiController : Controller
    {
        private static readonly MemoryCacheEntryOptions s_defaultOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = new TimeSpan(TimeSpan.TicksPerDay)
        };

        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public ApiController(IMemoryCache cache, ILogger<ApiController> logger)
        {
            this._cache = cache;
            this._logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> Get(ulong id, [FromQuery] bool detailed = false)
        {
            PollInfo pollInfo;
            if (this._cache.TryGetValue(id, out pollInfo))
            {
                if (pollInfo == null)
                    goto NotFound;

                if (!detailed)
                    return this.Json(pollInfo);
            }

            var detailedPollInfo = await this.GetPollInfo(id).ConfigureAwait(false);

            if (detailedPollInfo == null)
            {
                this._cache.Set(id, null, s_defaultOptions);
                goto NotFound;
            }

            pollInfo = detailedPollInfo.ToPollInfo();

            this._cache.Set(id, pollInfo, s_defaultOptions);

            return this.Json(detailed ? detailedPollInfo : pollInfo);

            NotFound:
            return this.HttpNotFound("Not a poll tweet.");
        }

        private static int ReadInt(string s)
        {
            var m = Regex.Match(s, "[0-9]+");
            if (!m.Success) throw new FormatException();
            return int.Parse(m.Value, CultureInfo.InvariantCulture);
        }

        private async Task<DetailedPollInfo> GetPollInfo(ulong id)
        {
            string html;
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                this._logger.LogInformation("Getting the card: " + id);
                var req = new HttpRequestMessage(HttpMethod.Get, "https://twitter.com/i/cards/tfw/v1/" + id.ToString("D"));
                var headers = req.Headers;
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
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

            var result = JsonMapper.ToObject<DetailedPollInfo>(cardInfo.ToJson());

            result.choices = dom.GetElementsByClassName("PollXChoiceTextOnly-choice--text")
                .Select(x => x.TextContent).ToArray();

            result.total = ReadInt(dom.GetElementsByClassName("PollXChoiceTextOnly-footer--total")[0].TextContent);

            result.percentages = dom.GetElementsByClassName("PollXChoiceTextOnly-progress")
                .Select(x => ReadInt(x.TextContent)).ToArray();

            Debug.Assert(ulong.Parse(result.tweet_id) == id);
            Debug.Assert(result.choices.Length == result.choice_count);

            return result;
        }
    }
}
