namespace PollApi
{
    public class PollInfo
    {
        public int choice_count { get; set; }
        public string end_time { get; set; }
        public string tweet_id { get; set; }
        public string[] choices { get; set; }
    }

    public class DetailedPollInfo : PollInfo
    {
        public string is_open { get; set; }
        public int total { get; set; }
        public int[] percentages { get; set; }

        public PollInfo ToPollInfo()
        {
            return new PollInfo
            {
                choice_count = this.choice_count,
                end_time = this.end_time,
                tweet_id = this.tweet_id,
                choices = this.choices
            };
        }
    }
}
