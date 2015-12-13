using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace PollApi
{
    public class MyCacheService
    {
        private static readonly MemoryCacheEntryOptions s_defaultOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = new TimeSpan(TimeSpan.TicksPerDay)
        };

        private readonly IMemoryCache _memoryCache;

        public MyCacheService(IMemoryCache memoryCache)
        {
            this._memoryCache = memoryCache;
        }

        public async Task<PollInfo> GetOrSet(ulong id, Func<ulong, Task<PollInfo>> valueFactory)
        {
            PollInfo result;
            if (!this._memoryCache.TryGetValue(id, out result))
            {
                result = await valueFactory(id).ConfigureAwait(false);
                this._memoryCache.Set(id, result, s_defaultOptions);
            }
            return result;
        }
    }
}
