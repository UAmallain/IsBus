using IsBus.Models;

namespace IsBus.Services;

public interface IBusinessIndicatorService
{
    BusinessIndicatorConfig GetIndicators();
    bool IsPrimarySuffix(string word);
    bool IsSecondaryIndicator(string word);
    bool IsStopWord(string word);
}