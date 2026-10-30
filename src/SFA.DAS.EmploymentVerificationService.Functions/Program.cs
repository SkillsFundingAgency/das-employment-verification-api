using Microsoft.Extensions.Hosting;
using SFA.DAS.EmploymentVerificationService.Functions.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) => services.AddEmploymentVerification(context.Configuration))
    .Build();

host.Run();
