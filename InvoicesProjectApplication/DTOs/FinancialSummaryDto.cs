namespace InvoicesProjectApplication.DTOs;

/// <summary>
/// Resumo financeiro mensal do usuário
/// </summary>
public record FinancialSummaryDto(
    int Year,
    int Month,
    decimal TotalDebts,          // Total de débitos do mês
    decimal TotalReceivables,    // Total de recebíveis do mês
    decimal TotalCardPurchases,  // Total de compras no cartão
    decimal TotalToPay,          // Débitos + Cartões
    decimal Balance              // Recebíveis - Total a Pagar
);
