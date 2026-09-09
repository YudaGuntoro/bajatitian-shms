using System;
using System.Data.Common;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace Worker.Shared;

public static class DbRetry
{
	public static async Task OpenWithRetryAsync(DbConnection connection, ILogger logger, string operationName, CancellationToken cancellationToken, int maxRetry = 10)
	{
		ArgumentNullException.ThrowIfNull(connection, "connection");
		ArgumentNullException.ThrowIfNull(logger, "logger");
		for (int attempt = 1; attempt <= maxRetry; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await connection.OpenAsync(cancellationToken);
				return;
			}
			catch (MySqlException exception) when (attempt < maxRetry)
			{
				logger.LogWarning(exception, "[DB] {Operation} retry {Attempt}/{MaxRetry}", operationName, attempt, maxRetry);
				await Task.Delay(GetRetryDelay(attempt), cancellationToken);
			}
			catch (TimeoutException exception2) when (attempt < maxRetry)
			{
				logger.LogWarning(exception2, "[DB] {Operation} timeout retry {Attempt}/{MaxRetry}", operationName, attempt, maxRetry);
				await Task.Delay(GetRetryDelay(attempt), cancellationToken);
			}
		}
		await connection.OpenAsync(cancellationToken);
	}

	private static TimeSpan GetRetryDelay(int attempt)
	{
		return TimeSpan.FromSeconds(Math.Min(attempt, 10));
	}

	public static bool IsDatabaseException(Exception exception)
	{
		bool flag = ((exception is MySqlException || exception is TimeoutException || exception is SocketException || exception is IOException) ? true : false);
		return flag || (exception.InnerException != null && IsDatabaseException(exception.InnerException));
	}
}
