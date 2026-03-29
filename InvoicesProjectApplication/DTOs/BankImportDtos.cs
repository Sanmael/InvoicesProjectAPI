namespace InvoicesProjectApplication.DTOs;

public record BankTransactionDto(
    string Description,
    decimal Amount,
    string Date,
    string TransactionType, // "credit" or "debit"
    string? Category,
    string? Memo
);

public record BankImportResultDto(
    string FileName,
    string? BankName,
    string? AccountId,
    int TotalTransactions,
    List<BankTransactionDto> Transactions
);

public record ConfirmBankImportDto(
    List<BankTransactionDto> Transactions,
    string? CreditCardId
);
