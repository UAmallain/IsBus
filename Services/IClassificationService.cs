namespace IsBus.Services;

public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(string input);
}