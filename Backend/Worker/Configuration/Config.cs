using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Worker.Configuration;

public sealed class Config
{
	private readonly Lazy<IReadOnlyDictionary<string, Dictionary<string, string>>> _sections = new Lazy<IReadOnlyDictionary<string, Dictionary<string, string>>>(LoadSections, LazyThreadSafetyMode.ExecutionAndPublication);

	public static Config Instance { get; } = new Config();

	private Config()
	{
	}

	public string? Read(string key, string section)
	{
		if (!_sections.Value.TryGetValue(section, out Dictionary<string, string>? value))
		{
			return null;
		}
		return value.TryGetValue(key, out var value2) && !string.IsNullOrWhiteSpace(value2) ? value2 : null;
	}

	public int ReadInt(string key, string section, int defaultValue)
	{
		var s = Read(key, section);
		return int.TryParse(s, out var result) ? result : defaultValue;
	}

	public IReadOnlyList<string> ReadList(string key, string section)
	{
		var text = Read(key, section);
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<string>();
		}
		return (from item in text.Split(new char[3] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where !string.IsNullOrWhiteSpace(item)
			select item).ToList();
	}

	private static IReadOnlyDictionary<string, Dictionary<string, string>> LoadSections()
	{
		string path = ResolveSettingsPath();
		Dictionary<string, Dictionary<string, string>> dictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
		string text = string.Empty;
		if (!File.Exists(path))
		{
			return dictionary;
		}
		string[] array = File.ReadAllLines(path);
		foreach (string text2 in array)
		{
			string text3 = text2.Trim();
			if (text3.Length == 0 || text3.StartsWith(';') || text3.StartsWith('#'))
			{
				continue;
			}
			if (text3.StartsWith('[') && text3.EndsWith(']'))
			{
				string text4 = text3;
				text = text4.Substring(1, text4.Length - 1 - 1).Trim();
				dictionary.TryAdd(text, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
				continue;
			}
			int num = text3.IndexOf('=');
			if (num > 0 && !string.IsNullOrWhiteSpace(text))
			{
				string key = text3.Substring(0, num).Trim();
				string value = text3.Substring(num + 1).Trim();
				dictionary[text][key] = value;
			}
		}
		return dictionary;
	}

	private static string ResolveSettingsPath()
	{
		string[] array = new string[2]
		{
			Path.Combine(AppContext.BaseDirectory, "Settings.ini"),
			Path.Combine(Directory.GetCurrentDirectory(), "Settings.ini")
		};
		return array.FirstOrDefault(File.Exists) ?? array[0];
	}
}
