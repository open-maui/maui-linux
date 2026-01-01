using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

public class EmailService : IEmail
{
	public bool IsComposeSupported => true;

	public async Task ComposeAsync()
	{
		await ComposeAsync(new EmailMessage());
	}

	public async Task ComposeAsync(string subject, string body, params string[] to)
	{
		EmailMessage val = new EmailMessage
		{
			Subject = subject,
			Body = body
		};
		if (to != null && to.Length != 0)
		{
			val.To = new List<string>(to);
		}
		await ComposeAsync(val);
	}

	public async Task ComposeAsync(EmailMessage? message)
	{
		if (message == null)
		{
			throw new ArgumentNullException("message");
		}
		string text = BuildMailtoUri(message);
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xdg-open",
				Arguments = "\"" + text + "\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Failed to open email client", innerException);
		}
	}

	private static string BuildMailtoUri(EmailMessage? message)
	{
		StringBuilder stringBuilder = new StringBuilder("mailto:");
		List<string> to = message.To;
		if (to != null && to.Count > 0)
		{
			stringBuilder.Append(string.Join(",", message.To.Select(Uri.EscapeDataString)));
		}
		List<string> list = new List<string>();
		if (!string.IsNullOrEmpty(message.Subject))
		{
			list.Add("subject=" + Uri.EscapeDataString(message.Subject));
		}
		if (!string.IsNullOrEmpty(message.Body))
		{
			list.Add("body=" + Uri.EscapeDataString(message.Body));
		}
		List<string> cc = message.Cc;
		if (cc != null && cc.Count > 0)
		{
			list.Add("cc=" + string.Join(",", message.Cc.Select(Uri.EscapeDataString)));
		}
		List<string> bcc = message.Bcc;
		if (bcc != null && bcc.Count > 0)
		{
			list.Add("bcc=" + string.Join(",", message.Bcc.Select(Uri.EscapeDataString)));
		}
		if (list.Count > 0)
		{
			stringBuilder.Append('?');
			stringBuilder.Append(string.Join("&", list));
		}
		return stringBuilder.ToString();
	}
}
