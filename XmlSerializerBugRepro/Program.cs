using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebApi
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder();

            builder.Services.AddHostedService<SerializationReproService>();

            var app = builder.Build();

            app.Run();
        }
    }
}
