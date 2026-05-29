using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Tests.RealTimeInventorySystem.Tests;

internal class TestRateLimitStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(
        Action<IApplicationBuilder> next)
    {
        return app =>
        {
            int counter = 0;

            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Method == "PATCH")
                {
                    counter++;

                    if (counter > 2)
                    {
                        context.Response.StatusCode = 429;
                        return;
                    }
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}