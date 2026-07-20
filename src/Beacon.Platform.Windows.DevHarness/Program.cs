using System.Diagnostics;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;

using var orchestrator = new QueryOrchestrator(
    [new AppSearchProvider(), new FileSearchProvider(reason => Console.Error.WriteLine($"files: {reason}"))]);

Console.Write("query> ");
var query = Console.ReadLine();
if (!string.IsNullOrWhiteSpace(query))
{
    var stopwatch = Stopwatch.StartNew();
    await foreach (var result in orchestrator.SearchAsync(query, QueryScope.Files))
        Console.WriteLine($"{result.Kind,-12} {result.Title} | {result.FilePath}");
    Console.Error.WriteLine($"elapsed: {stopwatch.ElapsedMilliseconds} ms");
}
