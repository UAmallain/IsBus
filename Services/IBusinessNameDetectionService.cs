using IsBus.Models;

namespace IsBus.Services;

public interface IBusinessNameDetectionService
{
    Task<BusinessNameCheckResponse> CheckBusinessNameAsync(string input);
}