// Examples demonstrating the Ouroboros.Easy API

using Ouroboros.Easy.Localization;
using EasyPipeline = Ouroboros.Easy.Pipeline;
using EasyPipelineResult = Ouroboros.Easy.PipelineResult;

namespace Ouroboros.Examples.Easy;

public static class EasyApiExamples
{
    public static async Task SimpleTextPipelineExample()
    {
        Console.WriteLine("=== Simple Text Pipeline ===\n");
        
        EasyPipelineResult result = await EasyPipeline.Create()
            .About("Explain quantum computing in simple terms")
            .Draft()
            .Critique()
            .Improve()
            .WithModel("llama3")
            .WithTemperature(0.7)
            .RunAsync();
        
        if (result.IsSuccess)
        {
            Console.WriteLine("Success!");
            Console.WriteLine(result.Output);
        }
        else
        {
            Console.WriteLine($"Error: {result.Error}");
        }
    }
    
    public static void MultiLanguageExample()
    {
        Console.WriteLine("=== Multi-Language Support ===\n");
        
        MultiLanguageSupport.CurrentLanguage = "en";
        Console.WriteLine($"English: {MultiLanguageSupport.Get("WelcomeMessage")}");
        
        MultiLanguageSupport.CurrentLanguage = "de";
        Console.WriteLine($"German: {MultiLanguageSupport.Get("WelcomeMessage")}");
        
        MultiLanguageSupport.CurrentLanguage = "fr";
        Console.WriteLine($"French: {MultiLanguageSupport.Get("WelcomeMessage")}");
    }
}
