using System.Diagnostics;

namespace AssetManagerAgent.Services.Platform;

public sealed class CommandResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    public bool Success => ExitCode == 0;
}

public sealed class CommandRunner
{
    public async Task<CommandResult> RunAsync(
        string command,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await outputTask,
            StandardError = await errorTask
        };
    }
}
