using System;
using Newtonsoft.Json;

namespace PollApi
{
    public class PollInfo
    {
        [JsonIgnore]
        public string card_name;
        public int choice_count;
        public string is_open;
        public string end_time;
        [JsonIgnore]
        public DateTimeOffset EndTimeDateTime;
        public string tweet_id;
        public string[] choices;
        public int? total;
        public int[] percentages;
        public int? count1;
        public int? count2;
        public int? count3;
        public int? count4;

        public PollInfo GetInvariantData()
        {
            return new PollInfo
            {
                card_name = this.card_name,
                choice_count = this.choice_count,
                is_open = this.is_open,
                end_time = this.end_time,
                tweet_id = this.tweet_id,
                choices = this.choices
            };
        }
    }
}
