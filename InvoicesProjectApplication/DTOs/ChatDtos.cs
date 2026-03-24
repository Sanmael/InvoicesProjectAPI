namespace InvoicesProjectApplication.DTOs;

public record ChatMessageDto(
    string Role,
    string Content
);

public record ChatRequestDto(
    string Message,
    List<ChatMessageDto>? History
);

public record ChatResponseDto(
    string Reply,
    List<ChatActionResult>? Actions
);

public record ChatActionResult(
    string Type,
    string Description,
    bool Success
);
