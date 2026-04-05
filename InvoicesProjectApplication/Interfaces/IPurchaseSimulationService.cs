using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IPurchaseSimulationService
{
    Task<PurchaseSimulationResultDto> SimulateAsync(Guid userId, PurchaseSimulationRequestDto request);
}
