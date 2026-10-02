namespace NotificationForwarder.UnitTests.Domain.Notifications;

public sealed class GeneratedAlertShould
{
    [Fact]
    public void AcceptWellFormedOutputAndTrimIt()
    {
        var alert = GeneratedAlert.TryCreate("  Missing worker heartbeat ", " worker-7 silent for 15 minutes ", " No heartbeat for 15 minutes.\nThis likely means the worker is down.\n");

        alert.ShouldNotBeNull();
        alert.Kind.ShouldBe("Missing worker heartbeat");
        alert.Title.ShouldBe("worker-7 silent for 15 minutes");
        alert.Message.ShouldBe("No heartbeat for 15 minutes.\nThis likely means the worker is down.");
    }

    [Theory]
    [InlineData("ERROR: worker-7 silent")]
    [InlineData("warning - worker-7 silent")]
    [InlineData("Critical:worker-7 silent")]
    public void StripASeverityPrefixTheGeneratorAddedAnyway(string title) =>
        GeneratedAlert.TryCreate("K", title, "M").ShouldNotBeNull().Title.ShouldBe("worker-7 silent");

    [Theory]
    [InlineData(null, "T", "M")]
    [InlineData("K", "T", "")]
    [InlineData("K", "Two\nlines", "M")]
    [InlineData("K", "T", "Bad\u0001control")]
    public void RejectOutputThatBreaksTheContract(string? kind, string? title, string? message) =>
        GeneratedAlert.TryCreate(kind, title, message).ShouldBeNull();

    [Fact]
    public void RejectOverlongFields()
    {
        GeneratedAlert.TryCreate(new string('k', GeneratedAlert.MaximumKindLength + 1), "T", "M").ShouldBeNull();
        GeneratedAlert.TryCreate("K", new string('t', GeneratedAlert.MaximumTitleLength + 1), "M").ShouldBeNull();
        GeneratedAlert.TryCreate("K", "T", new string('m', GeneratedAlert.MaximumMessageLength + 1)).ShouldBeNull();
    }
}
