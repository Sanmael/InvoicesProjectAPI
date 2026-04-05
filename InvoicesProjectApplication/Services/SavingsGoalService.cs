using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Enums;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class SavingsGoalService : ISavingsGoalService
{
    private readonly ISavingsGoalRepository _repository;

    public SavingsGoalService(ISavingsGoalRepository repository)
    {
        _repository = repository;
    }

    public async Task<SavingsGoalDto?> GetByIdAsync(Guid id)
    {
        var goal = await _repository.GetByIdAsync(id);
        return goal is null ? null : MapToDto(goal);
    }

    public async Task<IEnumerable<SavingsGoalDto>> GetByUserIdAsync(Guid userId)
    {
        var goals = await _repository.GetByUserIdAsync(userId);
        return goals.Select(MapToDto);
    }

    public async Task<IEnumerable<SavingsGoalDto>> GetActiveByUserIdAsync(Guid userId)
    {
        var goals = await _repository.GetActiveByUserIdAsync(userId);
        return goals.Select(MapToDto);
    }

    public async Task<SavingsGoalDto> CreateAsync(Guid userId, CreateSavingsGoalDto dto)
    {
        var goal = new SavingsGoal
        {
            UserId = userId,
            Title = dto.Title,
            Description = dto.Description,
            ProductImageDataUrl = dto.ProductImageDataUrl,
            ProductUrl = dto.ProductUrl,
            TargetAmount = dto.TargetAmount,
            CurrentAmount = dto.CurrentAmount,
            Deadline = dto.Deadline,
            Category = ExpenseCategory.Normalize(dto.Category)
        };

        await _repository.AddAsync(goal);
        return MapToDto(goal);
    }

    public async Task<SavingsGoalDto> UpdateAsync(Guid id, UpdateSavingsGoalDto dto)
    {
        var goal = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Meta não encontrada.");

        if (dto.Title is not null)
            goal.Title = dto.Title;

        if (dto.Description is not null)
            goal.Description = dto.Description;

        if (dto.ProductImageDataUrl is not null)
            goal.ProductImageDataUrl = dto.ProductImageDataUrl;

        if (dto.ProductUrl is not null)
            goal.ProductUrl = dto.ProductUrl;

        if (dto.TargetAmount.HasValue)
            goal.TargetAmount = dto.TargetAmount.Value;

        if (dto.CurrentAmount.HasValue)
            goal.CurrentAmount = dto.CurrentAmount.Value;

        if (dto.Deadline.HasValue)
            goal.Deadline = dto.Deadline.Value;

        if (dto.Category is not null)
            goal.Category = ExpenseCategory.Normalize(dto.Category);

        CheckCompletion(goal);
        goal.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(goal);
        return MapToDto(goal);
    }

    public async Task<SavingsGoalDto> AddAmountAsync(Guid id, decimal amount)
    {
        var goal = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Meta não encontrada.");

        goal.CurrentAmount += amount;
        CheckCompletion(goal);
        goal.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(goal);
        return MapToDto(goal);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static void CheckCompletion(SavingsGoal goal)
    {
        if (!goal.IsCompleted && goal.CurrentAmount >= goal.TargetAmount)
        {
            goal.IsCompleted = true;
            goal.CompletedAt = DateTime.UtcNow;
        }
        else if (goal.IsCompleted && goal.CurrentAmount < goal.TargetAmount)
        {
            goal.IsCompleted = false;
            goal.CompletedAt = null;
        }
    }

    private static SavingsGoalDto MapToDto(SavingsGoal goal)
    {
        var progress = goal.TargetAmount > 0
            ? Math.Min(100, Math.Round(goal.CurrentAmount / goal.TargetAmount * 100, 1))
            : 0;

        return new SavingsGoalDto(
            goal.Id,
            goal.Title,
            goal.Description,
            goal.ProductImageDataUrl,
            goal.ProductUrl,
            goal.TargetAmount,
            goal.CurrentAmount,
            goal.Deadline,
            goal.Category,
            goal.IsCompleted,
            goal.CompletedAt,
            progress,
            goal.CreatedAt);
    }
}
