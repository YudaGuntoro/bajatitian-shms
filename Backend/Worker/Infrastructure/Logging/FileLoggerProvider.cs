using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Worker.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider, IDisposable
{
	private readonly ConcurrentDictionary<string, FileLogger> _loggers = new ConcurrentDictionary<string, FileLogger>(StringComparer.OrdinalIgnoreCase);

	private readonly FileLogWriter _writer;

	public FileLoggerProvider(string logDirectory)
	{
		_writer = new FileLogWriter(logDirectory);
	}

	public ILogger CreateLogger(string categoryName)
	{
		return _loggers.GetOrAdd(categoryName, (string name) => new FileLogger(name, _writer));
	}

	public void Dispose()
	{
		_loggers.Clear();
		_writer.Dispose();
	}
}
