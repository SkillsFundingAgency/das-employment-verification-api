namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public interface IMessageProcessor
{
    Task ProcessAsync(BinaryData body, CancellationToken cancellationToken);
}
