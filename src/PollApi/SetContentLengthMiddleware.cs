using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;

namespace PollApi
{
    public class SetContentLengthMiddleware
    {
        private readonly RequestDelegate _next;

        public SetContentLengthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            using (var ms = new MemoryStream())
            {
                var res = httpContext.Response;
                var baseStream = res.Body;
                res.Body = ms;
                await this._next(httpContext).ConfigureAwait(false);

                if (!res.ContentLength.HasValue)
                    res.ContentLength = ms.Length;

                ms.WriteTo(baseStream);
            }
        }
    }
}
