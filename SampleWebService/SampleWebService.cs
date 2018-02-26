using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.Extensions.Logging;

namespace SampleWebService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class SampleWebService : StatelessService
    {
        public SampleWebService(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                             // configure logging from appsettings.json
                                    .ConfigureLogging((hostingContext, logging)  => {
                                        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                                        // add ETW logging https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?tabs=aspnetcore2x#eventsource
                                        // this generate a lot of "The parameters to the Event method do not match the parameters to the WriteEvent method. This may cause the event to be displayed incorrectly." in debug window
                                        // and no events in Diagnostic Window of Visual Studio, but events itself are recorded (tested with PSH event etl file)
                                        // see more https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/608, https://stackoverflow.com/questions/42123222/tpl-etw-events-have-extra-parameters-that-cause-excessive-debugger-output
                                        logging.AddEventSourceLogger();

                                    })
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }
    }
}
