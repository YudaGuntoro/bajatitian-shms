namespace Worker.Configuration;

public sealed record WorkerRuntimeOptions(bool LocalOnly = true)
{
    public static WorkerRuntimeOptions Load()
    {
        var value = Environment.GetEnvironmentVariable("WORKER_LOCAL_ONLY")
            ?? Environment.GetEnvironmentVariable("Worker__LocalOnly")
            ?? Config.Instance.Read("LocalOnly", "Worker");
        if (string.IsNullOrWhiteSpace(value)) return new WorkerRuntimeOptions();
        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" => new WorkerRuntimeOptions(true),
            "false" or "0" => new WorkerRuntimeOptions(false),
            _ => throw new FormatException("Worker LocalOnly must be true or false.")
        };
    }
}
