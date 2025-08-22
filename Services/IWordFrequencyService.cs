using IsBus.Models;

namespace IsBus.Services;

public interface IWordFrequencyService
{
    Task<Dictionary<string, int>> GetWordFrequenciesAsync(List<string> words);
    Task<bool> IsHighFrequencyBusinessWordAsync(string word, int threshold = 100);
}