namespace NotificationForwarder.UnitTests.Infrastructure.Generation;

public sealed class TemplateAlertGeneratorShould
{
    private readonly TemplateAlertGenerator _generator = new();

    [Theory]
    [InlineData("Npgsql connection pool exhausted", "Database problem")]
    [InlineData("JWT token signature invalid for user 42", "Security problem")]
    [InlineData("Something odd happened", "Unclassified notification")]
    public async Task PickAKindByKeyword(string message, string expectedKind)
    {
        var alert = await _generator.GenerateAsync(new NotificationBuilder().WithMessage(message).Build(), TestContext.Current.CancellationToken);

        alert.Kind.ShouldBe(expectedKind);
        alert.Title.ShouldBe(expectedKind + " reported by payments-api");
    }

    [Fact]
    public async Task QuoteTheSourceAndMessageAndSayTheAssistantWasUnavailable()
    {
        var notification = new NotificationBuilder().WithLevel(NotificationLevel.Critical).WithMessage(new string('m', 400)).Build();

        var alert = await _generator.GenerateAsync(notification, TestContext.Current.CancellationToken);

        alert.Title.ShouldNotContain("CRITICAL");
        alert.Message.ShouldStartWith("payments-api reported: " + new string('m', 300) + "...");
        alert.Message.ShouldContain("AI assistant was unavailable");
    }

    [Fact]
    public async Task FitTheTitleForTheLongestAllowedSource()
    {
        var notification = new NotificationBuilder().WithSource(new string('s', NotificationEntity.MaximumSourceLength)).Build();

        var alert = await _generator.GenerateAsync(notification, TestContext.Current.CancellationToken);

        alert.Title.Length.ShouldBe(GeneratedAlert.MaximumTitleLength);
        alert.Title.ShouldEndWith("...");
    }

    [Theory]
    [InlineData("login failed, password=hunter2 for bob", "login failed, password=[redacted] for bob")]
    [InlineData("header Authorization: Bearer eyJhbGci.x.y rejected", "header Authorization: [redacted] rejected")]
    [InlineData("api_key: \"sk-live-123\" expired", "api_key: [redacted] expired")]
    public async Task RedactCredentialsInTheQuotedText(string message, string expected)
    {
        var alert = await _generator.GenerateAsync(new NotificationBuilder().WithMessage(message).Build(), TestContext.Current.CancellationToken);

        alert.Message.ShouldStartWith("payments-api reported: " + expected);
    }
}
