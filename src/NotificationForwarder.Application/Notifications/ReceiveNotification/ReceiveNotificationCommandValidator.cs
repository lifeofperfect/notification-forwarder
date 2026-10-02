using FluentValidation;

namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

public sealed class ReceiveNotificationCommandValidator : AbstractValidator<ReceiveNotificationCommand>
{
    public ReceiveNotificationCommandValidator()
    {
        RuleFor(command => command.Level)
            .Must(level => NotificationLevels.Parse(level) is not null)
            .WithMessage("Use trace, debug, info, warning, error, or critical.");

        RuleFor(command => command.Message)
            .Custom((message, context) => AddFailure(context, NotificationMessage.Validate(message)));

        RuleFor(command => command.Source)
            .Custom((source, context) => AddFailure(context, NotificationEntity.ValidateSource(source)));
    }

    private static void AddFailure<T>(ValidationContext<T> context, string? error)
    {
        if (error is not null)
        {
            context.AddFailure(error);
        }
    }
}
