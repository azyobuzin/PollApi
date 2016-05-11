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

                // 終了済み投票
                if (pollInfo.is_open == "false")
                    return this.Json(pollInfo);

                // detailed でないなら必要なデータだけ返す
                // 投票期間が終了しているなら再取得
                if (!detailed && pollInfo.EndTimeDateTime > DateTimeOffset.UtcNow)
                    return this.Json(pollInfo.GetInvariantData());
            }

            pollInfo = await this.GetPollInfo(id).ConfigureAwait(false);

            if (pollInfo == null)
            {
                this._cache.Set(id, null, s_defaultOptions);
                goto NotFound;
            }

            this._cache.Set(id, pollInfo, s_defaultOptions);
            return this.Json(pollInfo);

        NotFound:
            return this.HttpNotFound("Not a poll tweet.");
        }

        private static int ReadInt(string s)
        {
            var m = Regex.Match(s, "[0-9,]+");
            if (!m.Success) throw new FormatException();
            return int.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture);
        }

        private class TwitterCardsSerialization
        {
#pragma warning disable 649
            public PollInfo card;
#pragma warning restore 649
        }

        private async Task<PollInfo> GetPollInfo(ulong id)
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

            // <div class="TwitterCardsGrid TwitterCard TwitterCard--animation">
            var grid = dom.Body.ChildNodes.OfType<IHtmlDivElement>().First();
            var cardInfoJson = grid.ChildNodes.OfType<IHtmlScriptElement>().First().Text;
            var result = JsonMapper.ToObject<TwitterCardsSerialization>(cardInfoJson).card;

            if (!Regex.IsMatch(result.card_name, "poll[234]choice_text_only"))
                return null;

            result.card_name = null;
            result.EndTimeDateTime = DateTimeOffset.Parse(result.end_time);
            result.counts = result.count3.HasValue
                ? (result.count4.HasValue
                    ? new[] { result.count1, result.count2, result.count3.Value, result.count4.Value }
                    : new[] { result.count1, result.count2, result.count3.Value })
                : new[] { result.count1, result.count2 };

            // <div class="TwitterCardsGrid-col--12 PollXChoice-optionsWrapper">
            var optionsWrapper = grid.ChildNodes.OfType<IHtmlDivElement>().First().ChildNodes.OfType<IHtmlDivElement>().First()
                .ChildNodes.OfType<IHtmlDivElement>().First().ChildNodes.OfType<IHtmlDivElement>().First();

            result.choices = optionsWrapper.GetElementsByClassName("PollXChoice-choice--text")
                .Select(x => x.ChildNodes.OfType<IHtmlSpanElement>().ElementAt(1).TextContent)
                .ToArray();

            result.total = ReadInt(optionsWrapper.ChildNodes.OfType<IHtmlDivElement>().Last()
                .GetElementsByClassName("PollXChoice-footer--total")[0].TextContent);

            result.percentages = optionsWrapper.GetElementsByClassName("PollXChoice-progress")
                .Select(x => ReadInt(x.TextContent)).ToArray();

            Debug.Assert(ulong.Parse(result.tweet_id) == id);
            Debug.Assert(result.choices.Length == result.choice_count);
            Debug.Assert(result.counts.Length == result.choice_count);

            return result;
        }
    }
}
