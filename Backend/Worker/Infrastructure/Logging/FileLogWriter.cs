using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Worker.Infrastructure.Logging;

public sealed class FileLogWriter : IDisposable
{
	private sealed record LogEntry(DateTimeOffset Timestamp, string Line);

	private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();

	private readonly string _logDirectory;

	private readonly Timer _timer;

	private int _isDraining;

	private bool _disposed;

	public FileLogWriter(string logDirectory)
	{
		_logDirectory = logDirectory;
		Directory.CreateDirectory(_logDirectory);
		_timer = new Timer(delegate
		{
			DrainQueue();
		}, null, TimeSpan.FromMilliseconds(250.0), TimeSpan.FromMilliseconds(250.0));
	}

	public void Write(LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		string line = BuildLine(now, logLevel, categoryName, eventId, message, exception);
		_queue.Enqueue(new LogEntry(now, line));
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_timer.Dispose();
			DrainQueue();
		}
	}

	private void DrainQueue()
	{
		if (Interlocked.Exchange(ref _isDraining, 1) == 1)
		{
			return;
		}
		try
		{
			Directory.CreateDirectory(_logDirectory);
			while (_queue.TryDequeue(out var result))
			{
				string path = Path.Combine(_logDirectory, $"worker-{result.Timestamp:yyyyMMdd}.log");
				File.AppendAllText(path, result.Line, Encoding.UTF8);
			}
		}
		finally
		{
			Volatile.Write(ref _isDraining, 0);
		}
	}

	private static string BuildLine(DateTimeOffset timestamp, LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
		stringBuilder.Append(" [");
		stringBuilder.Append(logLevel);
		stringBuilder.Append("] ");
		stringBuilder.Append(categoryName);
		if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
		{
			stringBuilder.Append(" EventId=");
			stringBuilder.Append(eventId.Id);
			if (!string.IsNullOrWhiteSpace(eventId.Name))
			{
				stringBuilder.Append('/');
				stringBuilder.Append(eventId.Name);
			}
		}
		stringBuilder.Append(" - ");
		stringBuilder.AppendLine(message);
		if (exception != null)
		{
			stringBuilder.AppendLine(exception.ToString());
		}
		return stringBuilder.ToString();
	}
}
