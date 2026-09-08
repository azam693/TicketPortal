using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Contracts.Middlewares;

public static class ProblemDetailsCustomizer
{
    public static void AddExceptionForDevMode(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            if (context.Exception is not null
                && context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                context.ProblemDetails.Extensions["exception"] =  context.Exception.ToString();
            }
        };
    }
}