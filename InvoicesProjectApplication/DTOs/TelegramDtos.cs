namespace InvoicesProjectApplication.DTOs;

public record TelegramLinkDto(
    string BotUsername,
    string LinkToken
);

public record TelegramStatusDto(
    bool IsLinked,
    string? TelegramUsername,
    bool NotificationsEnabled
);

public record TelegramToggleDto(
    bool Enabled
);

public record TelegramWebhookUpdateDto(
    long UpdateId,
    TelegramWebhookMessageDto? Message
);

public record TelegramWebhookMessageDto(
    long MessageId,
    TelegramWebhookChatDto? Chat,
    TelegramWebhookUserDto? From,
    string? Text
);

public record TelegramWebhookChatDto(
    long Id,
    string? Type
);

public record TelegramWebhookUserDto(
    long Id,
    string? FirstName,
    string? Username
);
