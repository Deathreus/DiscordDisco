using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Discord;
using Discord.Commands;

namespace MusicBot
{
	public class Utils
	{
		/// <summary>
		/// Download Song or Video
		/// </summary>
		/// 
		/// <param name="url">
		/// The URL to the Song or Video
		/// </param>
		/// 
		/// <param name="output">
		/// The file name to output to
		/// </param>
		/// 
		/// <returns>
		/// A new <see cref="Song"/> object
		/// </returns>
		/// 
		/// <exception cref="ArgumentException">
		/// Unsupported or invalid <paramref name="url"/> or invalid <paramref name="output"/> path
		/// </exception>
		public static async Task<Song> Download(string url, string output, IUserMessage userMessage)
		{
			if (output.Any(c => Path.GetInvalidPathChars().Contains(c)))
				throw new ArgumentException($"Bad output: '{output}'");

			if (url.Split("://", StringSplitOptions.None)[1].Contains("youtu.be"))
				url = "https://www.youtube.com/watch?v=" + url.Split(".be/", StringSplitOptions.None)[1];

			if (url.ToLower().Contains("youtube.com"))
			{
				return await DownloadFromYouTube(url, output, userMessage);
			}
			else if (url.ToLower().Contains("soundcloud.com"))
			{
				return await DownloadFromSoundCloud(url, output, userMessage);
			}
			else
			{
				throw new ArgumentException("Video URL not supported!");
			}
		}


		/// <summary>
		/// Basic rudimentary check that we have been given a valid URL
		/// </summary>
		/// 
		/// <param name="url">
		/// String to compare
		/// </param>
		/// 
		/// <returns>
		/// True if <paramref name="url"/> starts with http(s)
		/// </returns>
		public static bool IsURL(string url)
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
				return uri.Scheme == "http" || uri.Scheme == "https";

			return false;
		}


		/// <summary>
		/// Get video title and duration from YouTube URL
		/// </summary>
		/// <param name="url">URL to the YouTube Video</param>
		/// <returns>The YouTube Video title and duration</returns>
		private static async Task<Tuple<string, string>> GetInfo(string url)
		{
			var tcs = new TaskCompletionSource<Tuple<string, string>>();

			new Thread(() => {
				try
				{
					string title;
					string duration;

					//Get Video info
					var youtubedl = Process.Start(new ProcessStartInfo
					{
						FileName = ".\\bin\\youtube-dl",
						Arguments = $"-s --get-title --get-duration {url}",
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						UseShellExecute = false
					});

					while (!youtubedl.HasExited)
						Thread.Sleep(100);

					//Read
					string[] lines = youtubedl.StandardOutput.ReadToEnd().Split('\n');

					if (lines.Length >= 2)
					{
						title = lines[0];
						duration = lines[1];
					}
					else
					{
						title = "No Title found";
						duration = "0";
					}

					tcs.SetResult(new Tuple<string, string>(title, duration));
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
					Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Critical, "youtube-dl", ex.Message));
				}
			}).Start();

			var result = await tcs.Task;
			if (result == null)
				throw new Exception("youtube-dl.exe process failed with no exception.");

			return result;
		}

		private static async Task<Song> DownloadFromYouTube(string url, string file, IUserMessage userMessage)
		{
			var tcs = new TaskCompletionSource<Song>();

			string original = file.Remove(file.LastIndexOf('.'), 4);
			original = string.Concat(original, ".ogg");

			var ProgressBucket = new ConcurrentQueue<string>();

			new Thread(() => {
				// Get title and duration
				var info = GetInfo(url).GetAwaiter().GetResult();

				// We've already cached this
				if (File.Exists(file))
				{
					//Return MP3 Path & Video Title
					tcs.SetResult(new Song
					{
						FilePath = file,
						Name = info.Item1,
						Duration = TimeSpan.Parse(info.Item2)
					});
					return;
				}

				string[] splits = info.Item2.Split(':');
				string formattedDur = (splits.GetLength(0) > 2) ?
										$"{splits[0]}:{splits[1]}:{splits[2]}"
										:
										(splits.GetLength(0) > 1) ?
										$"00:{splits[0]}:{splits[1]}"
										:
										$"00:00:{splits[0]}";

				if (TimeSpan.Parse(formattedDur).TotalHours > 1)
				{
					userMessage?.Channel.SendMessageAsync("Woah there! That video's a little big 'innit?");
					tcs.SetResult(new Song());
					return;
				}

				//Download Video
				var youtubedl = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = ".\\bin\\youtube-dl",
						Arguments = $"{arguments} -o \"{original}\" {url}",
						CreateNoWindow = false,
						RedirectStandardOutput = true,
						UseShellExecute = false
					}
				};

				DateTimeOffset lastTick = DateTime.Now;

				youtubedl.OutputDataReceived += async (s, e) =>
				{
					if (string.IsNullOrEmpty(e.Data) || youtubedl.HasExited)
					{
						return;
					}

					if (e.Data.Contains("ERROR"))
					{
						await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Error, "youtube-dl", e.Data));
						return;
					}

					if (!e.Data.Contains("[download]"))
					{
						return;
					}

					if (!Regex.IsMatch(e.Data, @"\b\d+([\.,]\d+)?", RegexOptions.None))
					{
						return;
					}

					var perc = Convert.ToDecimal(Regex.Match(e.Data, @"\b\d+([\.,]\d+)?").Value);
					if (perc > 100 || perc < 0)
					{
						Console.WriteLine($"Odd data: {perc}");
						return;
					}

					if ((DateTime.Now - lastTick).Milliseconds >= 1000 || perc < 2 || perc > 98)
					{
						string status = "Downloading...\n";

						int percent = (int)(perc/100 * 10);

						int i;
						for (i = 0; i < percent; i++)
							status += "▬";

						status += "\uD83D\uDD18";

						for (; i < 10; i++)
							status += "▬";

						ProgressBucket.Enqueue(status);

						lastTick = DateTime.Now;
					}
				};

				youtubedl.Start();
				youtubedl.BeginOutputReadLine();

				//Wait until download is finished
				while (!youtubedl.HasExited)
					Thread.Sleep(100);

				while (ProgressBucket.Count > 0)
				{
					if (ProgressBucket.TryDequeue(out var status))
					{
						userMessage?.ModifyAsync(mp =>
						{
							mp.Content = status;
						},
						new RequestOptions
						{
							RetryMode = RetryMode.RetryRatelimit,
							Timeout = null
						});

						Thread.Sleep(1000);
					}
				}

				if (File.Exists(original))
				{
					//Return MP3 Path & Video Title
					tcs.SetResult(new Song
					{
						FilePath = file,
						Name = info.Item1,
						Duration = TimeSpan.Parse(formattedDur)
					});
				}
				else
				{
					//Error downloading
					tcs.SetResult(null);
					Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Warning, "youtube-dl", $"Could not download Song, youtube-dl responded with:\n\r"));//{youtubedl.StandardOutput.ReadToEnd()}
				}
			}).Start();

			Song result = await tcs.Task;
			if (result == null)
				throw new Exception("youtube-dl.exe failed to download!");

			return result;
		}

		private static async Task<Song> DownloadFromSoundCloud(string url, string file, IUserMessage userMessage)
		{
			var tcs = new TaskCompletionSource<Song>();

			string original = file.Remove(file.Length - 4, 4);
			original = string.Concat(original, ".ogg");

			var ProgressBucket = new ConcurrentQueue<string>();

			new Thread(() => {
				// Get title and duration
				var info = GetInfo(url).GetAwaiter().GetResult();

				// We've already cached this
				if (File.Exists(file))
				{
					//Return MP3 Path & Video Title
					tcs.SetResult(new Song
					{
						FilePath = file,
						Name = info.Item1,
						Duration = TimeSpan.Parse(info.Item2)
					});
					return;
				}

				string[] splits = info.Item2.Split(':');
				string formattedDur = (splits.GetLength(0) > 2) ?
										$"{splits[0]}:{splits[1]}:{splits[2]}"
										:
										(splits.GetLength(0) > 1) ?
										$"00:{splits[0]}:{splits[1]}"
										:
										$"00:00:{splits[0]}";

				if(TimeSpan.Parse(formattedDur).TotalHours > 0.8)
				{
					userMessage?.Channel.SendMessageAsync("Woah there! That video's a little big 'innit?");
					tcs.SetResult(new Song());
					return;
				}

				//Download track
				var youtubedl = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = ".\\bin\\youtube-dl",
						Arguments = $"{arguments} -o \"{file}\" {url}",
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						UseShellExecute = false
					}
				};

				DateTimeOffset lastTick = DateTime.Now;

				youtubedl.OutputDataReceived += async (s, e) =>
				{
					if (string.IsNullOrEmpty(e.Data) || youtubedl.HasExited)
					{
						return;
					}

					if (e.Data.Contains("ERROR"))
					{
						await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Error, "youtube-dl", e.Data));
						return;
					}

					if (!e.Data.Contains("[download]"))
					{
						return;
					}

					if (!Regex.IsMatch(e.Data, @"\b\d+([\.,]\d+)?", RegexOptions.None))
					{
						return;
					}

					var perc = Convert.ToDecimal(Regex.Match(e.Data, @"\b\d+([\.,]\d+)?").Value);
					if (perc > 100 || perc < 0)
					{
						Console.WriteLine($"Odd data: {perc}");
						return;
					}

					if ((DateTime.Now - lastTick).Milliseconds >= 1000 || perc < 2 || perc > 98)
					{
						string status = "Downloading...\n";

						int percent = (int)((perc / 100) * 10);

						int i;
						for (i = 0; i < percent; i++)
							status += "▬";

						status += "\uD83D\uDD18";

						for (; i < 10; i++)
							status += "▬";

						ProgressBucket.Enqueue(status);

						lastTick = DateTime.Now;
					}
				};

				youtubedl.Start();
				youtubedl.BeginOutputReadLine();

				//Wait until download is finished
				while (!youtubedl.HasExited)
					Thread.Sleep(100);

				while (ProgressBucket.Count > 0)
				{
					if (ProgressBucket.TryDequeue(out var status))
					{
						userMessage?.ModifyAsync(mp =>
						{
							mp.Content = status;
						},
						new RequestOptions
						{
							RetryMode = RetryMode.RetryRatelimit,
							Timeout = null
						});

						Thread.Sleep(1000);
					}
				}

				if (File.Exists(original))
				{
					//Return MP3 Path & Video Title
					tcs.SetResult(new Song
					{
						FilePath = file,
						Name = info.Item1,
						Duration = TimeSpan.Parse(formattedDur)
					});
				}
				else
				{
					//Error downloading
					tcs.SetResult(null);
					Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Warning, "youtube-dl", $"Could not download Song, youtube-dl responded with:\n\r"));//{youtubedl.StandardOutput.ReadToEnd()}
				}
			}).Start();

			Song result = await tcs.Task;
			if (result == null)
				throw new Exception("youtube-dl.exe failed to download!");

			return result;
		}

		private static readonly string arguments = "--extract-audio --audio-format vorbis --audio-quality 128 --no-cache-dir --prefer-ffmpeg --limit-rate 1.2M";
	}

	public abstract class SocketModuleBase : ModuleBase<SocketCommandContext> {}
}

namespace System
{
	public static class _String
	{
		/// <summary>
		/// Retrieves a substring from this instance. The substring starts after a given
		/// start string and ends before the specified end string (exclusively).
		/// </summary>
		/// 
		/// <returns>
		/// A new <see cref="string"/> instance.
		/// </returns>
		public static string Substring(this String src, string start, string end)
		{
			int Start, End;

			if (src.Contains(start) && src.Contains(end))
			{
				Start = src.IndexOf(start, 0) + start.Length;
				End = src.IndexOf(end, Start);

				return src.Substring(Start, End - Start);
			}
			else
			{
				return "";
			}
		}
		/// <summary>
		/// Splits a string into substrings before and after the seperator. You can specify
		/// whether the substrings include empty array elements.
		/// </summary>
		/// 
		/// <param name="seperator">
		/// A string that delimits the substrings in this string, or null.
		/// </param>
		/// 
		/// <param name="options">
		/// System.StringSplitOptions.RemoveEmptyEntries to omit empty array elements from
		/// the array returned; or System.StringSplitOptions.None to include empty array
		/// elements in the array returned.
		/// </param>
		/// 
		/// <returns>
		/// A <see cref="string"/> array whose elements contain the substrings in this string that are delimited
		/// by one or more strings in separator. For more information, see the Remarks section.
		/// </returns>
		/// 
		/// <exception cref="ArgumentException">
		/// <paramref name="options"/> is not one of the System.StringSplitOptions values.
		/// </exception>
		public static string[] Split(this string src, string seperator, StringSplitOptions options)
		{
			var separr = new string[1] { seperator };
			return src.Split(separr, options);
		}

		public static UInt32 QuickHash(this string src)
		{
			UInt32 hash = 0;
			foreach (char c in src)
			{
				hash += c;
				hash += hash << 10;
				hash ^= hash >> 6;
			}

			hash += hash << 3;
			hash ^= hash >> 11;
			hash += hash << 15;

			return hash;
		}

		public static void FormatForURL(this string src, string whitespaceReplacement/*, char[] toTrim, string[] toStrip*/)
		{
			// Just start off by trimming all whitespace from the front and tail
			src = src.Trim();

			// Snip off quotes surrounding the string, but leave a quote at the
			// beginning or end if it exists and isn't encapsulating the whole string
			if (src[0] == '"' && src[src.Length - 1] == '"')
				src = src.Trim('"');
			/*
			// Additional characters we'd like stripped
			foreach (var trim in toTrim)
				src = src.Trim(trim);

			// Strings we'd like omitted from the final result
			foreach (var strip in toStrip)
				src = src.Replace(strip, "");
			*/
			// Finally, substitute our whitespace
			src = src.Replace(" ", whitespaceReplacement);
		}
	}
}
