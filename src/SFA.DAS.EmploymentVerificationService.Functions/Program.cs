using Microsoft.Extensions.Hosting;
using SFA.DAS.EmploymentVerificationService.Functions.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) => services.AddEmploymentVerification(context.Configuration))
    .ConfigureServices((context, services) => services.AddEmploymentVerificationTelemetry(context.Configuration))
    .Build();

host.Run();
