using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;
using System.Diagnostics;

using Discord;
using Discord.Commands;

namespace MusicBot.Services
{
	// Maintains the fetched audio for an arbitrary length of time for caching
	// for better performance on multiple requests
	public class PersistanceService
	{
		public PersistanceService()
		{
			WorkerThread = new Thread(async () =>
				{
					while (true)
					{
						// In case the bot crashed and we have files that are not accounted for
						foreach (string filePath in Directory.EnumerateFiles(mp3Directory))
						{
							if (!Files.Values.Contains(filePath))
							{
								var createdAt = File.GetCreationTime(filePath);
								if ((DateTime.Now - createdAt).Hours >= 4)
								{
									await Task.Run(() => File.Delete(filePath));
									continue;
								}

								Files.TryAdd(createdAt, filePath);
							}
						}

						if (!Files.IsEmpty)
						{
							foreach (var time in Files.Keys)
							{
								if ((DateTime.Now - time).Hours >= 4)
								{
									if (Files.TryRemove(time, out string path))
									{
										await Task.Run(() => { if (File.Exists(path)) File.Delete(path); });
									}
								}
							}
						}

						await Task.Delay(TimeSpan.FromMinutes(30.0));
					}
				})
				{
					IsBackground = true,
					Name = "MusicBot Persistancy"
				};

			if (!Directory.Exists(mp3Directory))
				Directory.CreateDirectory(mp3Directory);

			WorkerThread.Start();
		}

		~PersistanceService() => WorkerThread.Abort();

		public async Task<string> Download(string url, bool bEnque, SocketCommandContext context)
		{
			string temp = Regex.Replace(url.Split(':')[1], "[^\\w]", "");
			temp = String.Format("DEAD{0:X}", temp.QuickHash());
			string fileName = Path.Combine(mp3Directory, temp + ".ogg");
			
			if (!Files.Values.Contains(fileName))
				Files.TryAdd(DateTime.Now, fileName);

			var message = !File.Exists(fileName) ? await context.Channel.SendMessageAsync("Downloading...\n") : null;
			Song song = await Utils.Download(url, fileName, message);
			if (song != null && bEnque)
					Program.Queues[context.Guild.Id].Enqueue(song);

			// In case we get a ton of requests, arbitrarily limit how many we store
			if (Files.Count > Program.Instance.MaxFiles)
				PopOldest();

			return fileName;
		}

		private void PopOldest()
		{
			DateTime useTime = DateTime.UtcNow;
			foreach (DateTime time in Files.Keys)
			{
				// If it's less than 'Now', it's older
				if(time < useTime)
				{
					useTime = time;
				}
			}

			if(Files.TryRemove(useTime, out string usePath))
			{
				if (File.Exists(usePath))
					File.Delete(usePath);
			}
		}

		// The thread that we will dedicate to periodically checking
		// a files life time for deletion
		private readonly Thread WorkerThread;

		private ConcurrentDictionary<DateTime, string> Files = new ConcurrentDictionary<DateTime, string>();

		private readonly string mp3Directory = Path.Combine(Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName, "songs");
	}
}
