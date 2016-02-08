using System;
using Newtonsoft.Json;

namespace PollApi
{
    // 闇深くなってきたので、シリアライズとデシリアライズの型分けよう！いつか
    public class PollInfo
    {
        [JsonIgnore]
        public string card_name;
        public int choice_count;
        [JsonIgnore]
        public string is_open;
        [JsonProperty("is_open")]
        public bool? BoolIsOpen
        {
            get
            {
                bool x;
                return bool.TryParse(this.is_open, out x) ? (bool?)x : null;
            }
        }
        public string end_time;
        [JsonIgnore]
        public DateTimeOffset EndTimeDateTime;
        public string tweet_id;
        public string[] choices;
        public int? total;
        public int[] percentages;
        [JsonIgnore]
        public int count1;
        [JsonIgnore]
        public int count2;
        [JsonIgnore]
        public int? count3;
        [JsonIgnore]
        public int? count4;
        public int[] counts;

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
