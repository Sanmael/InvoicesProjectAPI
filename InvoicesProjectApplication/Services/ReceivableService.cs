using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class ReceivableService : IReceivableService
{
    private readonly IReceivableRepository _receivableRepository;

    public ReceivableService(IReceivableRepository receivableRepository)
    {
        _receivableRepository = receivableRepository;
    }

    public async Task<ReceivableDto?> GetByIdAsync(Guid id)
    {
        var receivable = await _receivableRepository.GetByIdAsync(id);
        return receivable is null ? null : MapToDto(receivable);
    }

    public async Task<IEnumerable<ReceivableDto>> GetByUserIdAsync(Guid userId)
    {
        var receivables = await _receivableRepository.GetByUserIdAsync(userId);
        return receivables.Select(MapToDto);
    }

    public async Task<IEnumerable<ReceivableDto>> GetPendingByUserIdAsync(Guid userId)
    {
        var receivables = await _receivableRepository.GetPendingByUserIdAsync(userId);
        return receivables.Select(MapToDto);
    }

    public async Task<ReceivableDto> CreateAsync(Guid userId, CreateReceivableDto dto)
    {
        var receivable = new Receivable
        {
            UserId = userId,
            Description = dto.Description,
            Amount = dto.Amount,
            ExpectedDate = dto.ExpectedDate,
            Notes = dto.Notes
        };

        await _receivableRepository.AddAsync(receivable);
        return MapToDto(receivable);
    }

    public async Task<IEnumerable<ReceivableDto>> CreateRecurringAsync(Guid userId, CreateRecurringReceivableDto dto)
    {
        if (dto.RecurringDay < 1 || dto.RecurringDay > 28)
            throw new ArgumentException("O dia da recorrência deve ser entre 1 e 28.");

        if (dto.Months < 1 || dto.Months > 60)
            throw new ArgumentException("O número de meses deve ser entre 1 e 60.");

        var groupId = Guid.NewGuid();
        var results = new List<ReceivableDto>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstExpectedDate = new DateOnly(today.Year, today.Month, dto.RecurringDay);

        if (firstExpectedDate < today)
            firstExpectedDate = firstExpectedDate.AddMonths(1);

        for (int i = 0; i < dto.Months; i++)
        {
            var targetDate = firstExpectedDate.AddMonths(i);

            var receivable = new Receivable
            {
                UserId = userId,
                Description = dto.Description,
                Amount = dto.Amount,
                ExpectedDate = targetDate,
                Notes = dto.Notes,
                IsRecurring = true,
                RecurringDay = dto.RecurringDay,
                RecurrenceGroupId = groupId
            };

            await _receivableRepository.AddAsync(receivable);
            results.Add(MapToDto(receivable));
        }

        return results;
    }

    public async Task<ReceivableDto> UpdateAsync(Guid id, UpdateReceivableDto dto)
    {
        var receivable = await _receivableRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Recebível não encontrado.");

        if (dto.Description is not null)
            receivable.Description = dto.Description;

        if (dto.Amount.HasValue)
            receivable.Amount = dto.Amount.Value;

        if (dto.ExpectedDate.HasValue)
            receivable.ExpectedDate = dto.ExpectedDate.Value;

        if (dto.IsReceived.HasValue)
        {
            receivable.IsReceived = dto.IsReceived.Value;
            receivable.ReceivedAt = dto.IsReceived.Value ? DateTime.UtcNow : null;
        }

        if (dto.Notes is not null)
            receivable.Notes = dto.Notes;

        receivable.UpdatedAt = DateTime.UtcNow;
        await _receivableRepository.UpdateAsync(receivable);
        return MapToDto(receivable);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _receivableRepository.DeleteAsync(id);
    }

    public async Task DeleteGroupAsync(Guid groupId)
    {
        var receivables = await _receivableRepository.GetByRecurrenceGroupAsync(groupId);
        foreach (var r in receivables.Where(r => !r.IsReceived))
            await _receivableRepository.DeleteAsync(r.Id);
    }

    public async Task<ReceivableDto> MarkAsReceivedAsync(Guid id)
    {
        var receivable = await _receivableRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Recebível não encontrado.");

        receivable.IsReceived = true;
        receivable.ReceivedAt = DateTime.UtcNow;
        receivable.UpdatedAt = DateTime.UtcNow;

        await _receivableRepository.UpdateAsync(receivable);
        return MapToDto(receivable);
    }

    private static ReceivableDto MapToDto(Receivable receivable) =>
        new(receivable.Id, receivable.Description, receivable.Amount, receivable.ExpectedDate,
            receivable.IsReceived, receivable.ReceivedAt, receivable.Notes,
            receivable.IsRecurring, receivable.RecurringDay, receivable.RecurrenceGroupId,
            receivable.CreatedAt);
}
