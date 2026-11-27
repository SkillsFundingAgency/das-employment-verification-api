using Azure.Messaging.ServiceBus;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Processors;

[TestFixture]
public class MessageProcessorFactoryTests
{
    private Mock<IServiceProvider> provider = null!;
    private VerifyNotEmployedProcessor command = null!;
    private EmploymentCheckCompletedEventProcessor completion = null!;
    private MessageProcessorFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        command = new VerifyNotEmployedProcessor(Mock.Of<IVerifyNotEmployedService>(MockBehavior.Strict));
        completion = new EmploymentCheckCompletedEventProcessor(Mock.Of<IEmploymentCheckCompletionService>(MockBehavior.Strict));
        provider = new Mock<IServiceProvider>(MockBehavior.Strict);
        provider.Setup(x => x.GetService(typeof(VerifyNotEmployedProcessor))).Returns(command);
        provider.Setup(x => x.GetService(typeof(EmploymentCheckCompletedEventProcessor))).Returns(completion);
        factory = new MessageProcessorFactory(provider.Object);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GetProcessor_SelectsCommand(bool assemblyQualified)
    {
        // Arrange
        var message = Message(assemblyQualified ? typeof(VerifyNotEmployed).AssemblyQualifiedName :
            typeof(VerifyNotEmployed).FullName);

        // Act
        var processor = factory.GetProcessor(message);

        // Assert
        Assert.That(processor, Is.SameAs(command));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GetProcessor_SelectsCompletionWithSemicolonMetadata(bool assemblyQualified)
    {
        // Arrange
        var name = assemblyQualified ? typeof(EmploymentCheckCompletedEvent).AssemblyQualifiedName :
            typeof(EmploymentCheckCompletedEvent).FullName;
        var message = Message($" ; Other.Base, Other;  {name} ; NServiceBus.IEvent, NServiceBus ; ");

        // Act
        var processor = factory.GetProcessor(message);

        // Assert
        Assert.That(processor, Is.SameAs(completion));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Unknown.Message, Unknown")]
    [TestCase("VerifyNotEmployed")]
    [TestCase("sfa.das.employmentverificationservice.messages.learner.commands.verifynotemployed")]
    public void GetProcessor_RejectsMissingUnsupportedOrInexactMetadata(string? metadata)
    {
        // Arrange
        var message = Message(metadata);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetProcessor(message));
        provider.VerifyNoOtherCalls();
    }

    [Test]
    public void GetProcessor_RejectsConflictingSupportedTypes()
    {
        // Arrange
        var message = Message(
            $"{typeof(VerifyNotEmployed).AssemblyQualifiedName};{typeof(EmploymentCheckCompletedEvent).FullName}");

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetProcessor(message));
        provider.VerifyNoOtherCalls();
    }

    [Test]
    public void GetProcessor_AcceptsRepeatedSameConcreteType()
    {
        // Arrange
        var message = Message(
            $"{typeof(VerifyNotEmployed).FullName};{typeof(VerifyNotEmployed).AssemblyQualifiedName}");

        // Act
        var processor = factory.GetProcessor(message);

        // Assert
        Assert.That(processor, Is.SameAs(command));
    }

    [Test]
    public void GetProcessor_RejectsNonStringHeader()
    {
        // Arrange
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            properties: new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = 42 });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetProcessor(message));
        provider.VerifyNoOtherCalls();
    }

    private static ServiceBusReceivedMessage Message(string? header) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("invalid JSON"),
            properties: header is null ? new Dictionary<string, object>() :
                new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = header });
}
