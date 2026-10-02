using FluentValidation.Results;

namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

internal static class ReceiveNotificationCommandMapper
{
    public static NotificationEntity ToEntity(this ReceiveNotificationCommand command, NotificationLevel level, DateTimeOffset receivedAt) =>
        NotificationEntity.Receive(level, NotificationMessage.Create(command.Message!), command.Source, receivedAt);

    public static List<ValidationError> ToValidationErrors(this ValidationResult validation) =>
        validation.Errors
            .Select(failure => new ValidationError(ToCamelCase(failure.PropertyName), failure.ErrorMessage))
            .ToList();

    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
