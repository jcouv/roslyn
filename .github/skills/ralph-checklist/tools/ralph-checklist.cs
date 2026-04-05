#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImportDirectoryBuildProps=false
#:property ImportDirectoryBuildTargets=false

using System.Diagnostics;

const int SuccessExitCode = 0;
const int BlockedExitCode = 2;
const int FailedExitCode = 3;

const string LoopStatusKey = "loop_status";
const string ActiveStatus = "active";
const string CompletedStatus = "completed";
const string BlockedStatus = "blocked";
const string FailedStatus = "failed";

if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

try
{
    if (args[0] is "help" or "--help" or "-h")
    {
        return PrintHelp();
    }

    return RunCopilot(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

int RunCopilot(string[] commandArgs)
{
    RequireArgs(commandArgs, 1, "<tracker.md> [copilot args...]");

    var documentPath = Path.GetFullPath(commandArgs[0]);
    if (!File.Exists(documentPath))
    {
        throw new InvalidOperationException($"Loop document not found: {documentPath}");
    }

    var initialStatus = GetLoopStatus(documentPath);
    if (!IsActiveStatus(initialStatus))
    {
        return ExitForLoopStatus(initialStatus);
    }

    var lastWriteTimeUtc = GetLastWriteTimeUtc(documentPath);

    while (true)
    {
        var exitCode = RunCopilotOnce(documentPath, commandArgs.Skip(1));
        if (exitCode != 0)
        {
            return exitCode;
        }

        var loopStatus = GetLoopStatus(documentPath);
        if (!IsActiveStatus(loopStatus))
        {
            return ExitForLoopStatus(loopStatus);
        }

        var updatedLastWriteTimeUtc = GetLastWriteTimeUtc(documentPath);
        if (updatedLastWriteTimeUtc == lastWriteTimeUtc)
        {
            Console.Error.WriteLine($"Tracker file did not change and '{LoopStatusKey}' is still '{ActiveStatus}'.");
            Console.Error.WriteLine("Set 'loop_status' to 'completed', 'blocked', or 'failed' in the tracker frontmatter to stop the loop explicitly.");
            return 0;
        }

        lastWriteTimeUtc = updatedLastWriteTimeUtc;
    }
}

int RunCopilotOnce(string documentPath, IEnumerable<string> extraArguments)
{
    var prompt = $"Follow instructions in {documentPath}";
    var startInfo = new ProcessStartInfo
    {
        FileName = "copilot",
        UseShellExecute = false,
        RedirectStandardInput = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        WorkingDirectory = Environment.CurrentDirectory,
    };

    startInfo.ArgumentList.Add("-p");
    startInfo.ArgumentList.Add(prompt);
    startInfo.ArgumentList.Add("--yolo");

    foreach (var argument in extraArguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        throw new InvalidOperationException("Failed to start copilot.");
    }

    process.WaitForExit();
    return process.ExitCode;
}

DateTime GetLastWriteTimeUtc(string documentPath)
{
    if (!File.Exists(documentPath))
    {
        throw new InvalidOperationException($"Loop document not found: {documentPath}");
    }

    return File.GetLastWriteTimeUtc(documentPath);
}

string GetLoopStatus(string documentPath)
{
    var frontmatter = ReadFrontmatter(documentPath);
    if (!frontmatter.TryGetValue(LoopStatusKey, out var status) || string.IsNullOrWhiteSpace(status))
    {
        return ActiveStatus;
    }

    return status.Trim().ToLowerInvariant();
}

bool IsActiveStatus(string status)
    => GetLoopStatusKind(status) == ActiveStatus;

string GetLoopStatusKind(string status)
{
    var separatorIndex = status.IndexOf(':');
    var value = separatorIndex >= 0 ? status[..separatorIndex] : status;
    return value.Trim().ToLowerInvariant();
}

Dictionary<string, string> ReadFrontmatter(string documentPath)
{
    using var reader = new StreamReader(documentPath);

    var firstLine = reader.ReadLine();
    if (firstLine is not "---")
    {
        return [];
    }

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        if (line == "---")
        {
            break;
        }

        var separatorIndex = line.IndexOf(':');
        if (separatorIndex < 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        if (key.Length == 0)
        {
            continue;
        }

        result[key] = value;
    }

    return result;
}

int ExitForLoopStatus(string status)
{
    Console.WriteLine($"Loop status is '{status}'.");

    return GetLoopStatusKind(status) switch
    {
        CompletedStatus => SuccessExitCode,
        BlockedStatus => BlockedExitCode,
        FailedStatus => FailedExitCode,
        _ => SuccessExitCode,
    };
}

int PrintHelp()
{
    Console.WriteLine("ralph-checklist usage:");
    Console.WriteLine("  dotnet run --file .github/skills/ralph-checklist/tools/ralph-checklist.cs -- <tracker.md> [copilot args...]");
    Console.WriteLine();
    Console.WriteLine("This repeatedly launches copilot in non-interactive mode with the prompt:");
    Console.WriteLine("  Follow instructions in <absolute-path-to-tracker.md>");
    Console.WriteLine();
    Console.WriteLine("The loop keeps running while the tracker frontmatter has 'loop_status: active'.");
    Console.WriteLine("Set 'loop_status' to 'completed', 'blocked', or 'failed' to stop explicitly.");
    Console.WriteLine("If the tracker file timestamp does not change and loop_status is still active, the runner exits as a safety check.");
    Console.WriteLine();
    Console.WriteLine("Yolo mode is enabled automatically.");
    Console.WriteLine("Any extra arguments are forwarded to copilot.");
    return 0;
}

void RequireArgs(string[] commandArgs, int requiredLength, string usage)
{
    if (commandArgs.Length < requiredLength)
    {
        throw new InvalidOperationException($"Usage: {usage}");
    }
}
