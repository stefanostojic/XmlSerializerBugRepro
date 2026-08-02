using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using XmlSerializerBugRepro.Models;
using XmlSerializerBugRepro.Models.Collections;

namespace XmlSerializerBugRepro
{
    public class SerializationReproService(IHostApplicationLifetime lifetime, ILogger<SerializationReproService> logger) : BackgroundService
    {
        private const int LoadCalls = 50;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Load: {Count} calls", LoadCalls);
            if (!TryRun(LoadCalls, out var loadFailedAt))
            {
                logger.LogError("REPRODUCED — failed on overall call #{Call}", loadFailedAt);
                Environment.ExitCode = 1;
            }
            else
            {
                logger.LogInformation("Completed {Total} calls with no failure", LoadCalls);
                Environment.ExitCode = 0;
            }

            lifetime.StopApplication();
        }

        private bool TryRun(int count, out int failedAtCall)
        {
            for (var i = 0; i < count; i++)
            {
                try
                {
                    Serialization();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Serialization threw on call {Call} of this phase", i + 1);
                    failedAtCall = i + 1;
                    return false;
                }

                Thread.Sleep(100);
            }

            failedAtCall = -1;
            return true;
        }

        private static void Serialization()
        {
            var model = new Foo()
            {
                Foos = new CustomList<Foo>()
                {
                    new Foo
                    {
                        Bars = new CustomList<Bar>()
                        {
                            new Bar()
                        },
                    }
                }
            };

            var _serializer = new XmlSerializer(typeof(Foo));
            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter);
            _serializer.Serialize(xmlWriter, model);
        }
    }
}