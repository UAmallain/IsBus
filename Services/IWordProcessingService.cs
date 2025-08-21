namespace IsBus.Services;

public interface IWordProcessingService
{
    Task<int> ProcessBusinessNameWordsAsync(string businessName);
}