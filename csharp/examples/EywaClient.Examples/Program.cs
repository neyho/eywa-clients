// Entry point - delegates to ManualIntegrationTest
namespace EywaClient.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        await ManualIntegrationTest.Run(args);
    }
}