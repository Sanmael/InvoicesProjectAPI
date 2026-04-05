namespace InvoicesProjectApplication.DTOs;

public record TagEventoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    DateTime? DataInicio,
    DateTime? DataFim,
    DateTime CreatedAt,
    int TotalDebts,
    int TotalCardPurchases,
    decimal TotalGastos
);

public record CreateTagEventoDto(
    string Nome,
    string? Descricao,
    DateTime? DataInicio,
    DateTime? DataFim
);

public record UpdateTagEventoDto(
    string? Nome,
    string? Descricao,
    DateTime? DataInicio,
    DateTime? DataFim
);
