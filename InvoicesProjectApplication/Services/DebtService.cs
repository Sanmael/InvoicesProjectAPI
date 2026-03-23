using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class DebtService : IDebtService
{
    private readonly IDebtRepository _debtRepository;

    public DebtService(IDebtRepository debtRepository)
    {
        _debtRepository = debtRepository;
    }

    public async Task<DebtDto?> GetByIdAsync(Guid id)
    {
        var debt = await _debtRepository.GetByIdAsync(id);
        return debt is null ? null : MapToDto(debt);
    }

    public async Task<IEnumerable<DebtDto>> GetByUserIdAsync(Guid userId)
    {
        var debts = await _debtRepository.GetByUserIdAsync(userId);
        return debts.Select(MapToDto);
    }

    public async Task<IEnumerable<DebtDto>> GetPendingByUserIdAsync(Guid userId)
    {
        var debts = await _debtRepository.GetPendingByUserIdAsync(userId);
        return debts.Select(MapToDto);
    }

    public async Task<DebtDto> CreateAsync(Guid userId, CreateDebtDto dto)
    {
        var debt = new Debt
        {
            UserId = userId,
            Description = dto.Description,
            Amount = dto.Amount,
            DueDate = dto.DueDate,
            Notes = dto.Notes
        };

        await _debtRepository.AddAsync(debt);
        return MapToDto(debt);
    }

    public async Task<IEnumerable<DebtDto>> CreateInstallmentAsync(Guid userId, CreateInstallmentDebtDto dto)
    {
        if (dto.Installments < 2 || dto.Installments > 60)
            throw new ArgumentException("O número de parcelas deve ser entre 2 e 60.");

        var groupId = Guid.NewGuid();
        var installmentAmount = Math.Round(dto.TotalAmount / dto.Installments, 2);
        var results = new List<DebtDto>();

        for (int i = 0; i < dto.Installments; i++)
        {
            var dueDate = dto.FirstDueDate.AddMonths(i);

            var debt = new Debt
            {
                UserId = userId,
                Description = $"{dto.Description} ({i + 1}/{dto.Installments})",
                Amount = installmentAmount,
                DueDate = dueDate,
                Notes = dto.Notes,
                IsInstallment = true,
                TotalInstallments = dto.Installments,
                InstallmentNumber = i + 1,
                InstallmentGroupId = groupId
            };

            await _debtRepository.AddAsync(debt);
            results.Add(MapToDto(debt));
        }

        return results;
    }

    public async Task<IEnumerable<DebtDto>> CreateRecurringAsync(Guid userId, CreateRecurringDebtDto dto)
    {
        if (dto.RecurringDay < 1 || dto.RecurringDay > 28)
            throw new ArgumentException("O dia da recorrência deve ser entre 1 e 28.");

        if (dto.Months < 1 || dto.Months > 60)
            throw new ArgumentException("O número de meses deve ser entre 1 e 60.");

        var groupId = Guid.NewGuid();
        var results = new List<DebtDto>();
        var startDate = (dto.StartDate ?? DateTime.UtcNow).Date;
        var firstDueDate = new DateTime(startDate.Year, startDate.Month, dto.RecurringDay, 0, 0, 0, DateTimeKind.Utc);

        if (firstDueDate < startDate)
            firstDueDate = firstDueDate.AddMonths(1);

        for (int i = 0; i < dto.Months; i++)
        {
            var dueDate = firstDueDate.AddMonths(i);

            var debt = new Debt
            {
                UserId = userId,
                Description = dto.Description,
                Amount = dto.Amount,
                DueDate = dueDate,
                Notes = dto.Notes,
                IsInstallment = false,
                TotalInstallments = dto.Months,
                InstallmentNumber = i + 1,
                InstallmentGroupId = groupId
            };

            await _debtRepository.AddAsync(debt);
            results.Add(MapToDto(debt));
        }

        return results;
    }

    public async Task<DebtDto> UpdateAsync(Guid id, UpdateDebtDto dto)
    {
        var debt = await _debtRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Débito não encontrado.");

        if (dto.Description is not null)
            debt.Description = dto.Description;

        if (dto.Amount.HasValue)
            debt.Amount = dto.Amount.Value;

        if (dto.DueDate.HasValue)
            debt.DueDate = dto.DueDate.Value;

        if (dto.IsPaid.HasValue)
        {
            debt.IsPaid = dto.IsPaid.Value;
            debt.PaidAt = dto.IsPaid.Value ? DateTime.UtcNow : null;
        }

        if (dto.Notes is not null)
            debt.Notes = dto.Notes;

        debt.UpdatedAt = DateTime.UtcNow;
        await _debtRepository.UpdateAsync(debt);
        return MapToDto(debt);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _debtRepository.DeleteAsync(id);
    }

    public async Task DeleteGroupAsync(Guid groupId)
    {
        var debts = await _debtRepository.GetByInstallmentGroupAsync(groupId);
        foreach (var d in debts.Where(d => !d.IsPaid))
            await _debtRepository.DeleteAsync(d.Id);
    }

    public async Task<DebtDto> MarkAsPaidAsync(Guid id)
    {
        var debt = await _debtRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Débito não encontrado.");

        debt.IsPaid = true;
        debt.PaidAt = DateTime.UtcNow;
        debt.UpdatedAt = DateTime.UtcNow;

        await _debtRepository.UpdateAsync(debt);
        return MapToDto(debt);
    }

    private static DebtDto MapToDto(Debt debt) =>
        new(debt.Id, debt.Description, debt.Amount, debt.DueDate,
            debt.IsPaid, debt.PaidAt, debt.Notes,
            debt.IsInstallment, debt.TotalInstallments, debt.InstallmentNumber, debt.InstallmentGroupId,
            debt.CreatedAt);
}
