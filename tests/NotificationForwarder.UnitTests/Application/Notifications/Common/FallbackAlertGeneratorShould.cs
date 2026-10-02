namespace NotificationForwarder.UnitTests.Application.Notifications.Common;

public sealed class FallbackAlertGeneratorShould
{
    private readonly IAlertGenerator _primary = Substitute.For<IAlertGenerator>();
    private readonly FallbackAlertGenerator _generator;

    public FallbackAlertGeneratorShould() =>
        _generator = new FallbackAlertGenerator(_primary, new TemplateAlertGenerator(), NullLogger<FallbackAlertGenerator>.Instance);

    [Fact]
    public async Task UseTheTemplateWhenThePrimaryFails()
    {
        var notification = new NotificationBuilder().WithMessage("SqlException: deadlock victim").Build();
        _primary.GenerateAsync(notification, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GeneratedAlert>(new AlertGenerationException("The AI provider answered HTTP 503.")));

        var result = await _generator.GenerateAsync(notification, TestContext.Current.CancellationToken);

        result.Kind.ShouldBe("Database problem");
        result.Message.ShouldStartWith("payments-api reported: SqlException");
    }

    [Fact]
    public async Task NotSwallowCancellation()
    {
        var notification = new NotificationBuilder().Build();
        _primary.GenerateAsync(notification, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GeneratedAlert>(new OperationCanceledException()));

        await Should.ThrowAsync<OperationCanceledException>(() => _generator.GenerateAsync(notification, TestContext.Current.CancellationToken));
    }
}
