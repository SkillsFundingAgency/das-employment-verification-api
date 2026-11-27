using SFA.DAS.EmploymentVerificationService.Functions.Extensions;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SFA.DAS.EmploymentVerificationService.Application.Extensions;
using SFA.DAS.EmploymentVerificationService.Data.Extensions;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddScoped<VerifyNotEmployedProcessor>();
builder.Services.AddScoped<EmploymentCheckCompletedEventProcessor>();
builder.Services.AddScoped<IMessageProcessorFactory, MessageProcessorFactory>();
builder.Services.AddData(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();
builder.Logging.RemoveApplicationInsightsDefaultFilter();

builder.Build().Run();
