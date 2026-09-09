using System;
using Microsoft.Extensions.Logging;

namespace Worker.Infrastructure.Logging;

public sealed class FileLogger : ILogger
{
	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new NullScope();

		public void Dispose()
		{
		}
	}

	private readonly string _categoryName;

	private readonly FileLogWriter _writer;

	public FileLogger(string categoryName, FileLogWriter writer)
	{
		_categoryName = categoryName;
		_writer = writer;
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return logLevel != LogLevel.None;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (IsEnabled(logLevel))
		{
			string text = formatter(state, exception);
			if (!string.IsNullOrWhiteSpace(text) || exception != null)
			{
				_writer.Write(logLevel, _categoryName, eventId, text, exception);
			}
		}
	}
}
