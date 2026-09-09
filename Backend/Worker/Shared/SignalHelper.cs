namespace Worker.Shared;

public static class SignalHelper
{
	public static string TopicToMachineName(string topic)
	{
		string text = (string.IsNullOrWhiteSpace(topic) ? "unknown" : topic.Trim().Trim('/').Replace('/', '-'));
		return string.IsNullOrWhiteSpace(text) ? "unknown" : text;
	}
}
