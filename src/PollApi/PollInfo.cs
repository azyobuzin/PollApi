using Newtonsoft.Json;

namespace PollApi
{
    public class PollInfo
    {
        public int choice_count { get; set; }
        public string end_time { get; set; }
        public string tweet_id { get; set; }
        public string[] choices { get; set; }
    }
}
