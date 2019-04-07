using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Discord;
using Discord.Commands;

namespace MusicBot.Commands
{
	public class Search : SocketModuleBase
	{
		[Command("search")]
		public async Task AllSearch([Remainder]string query) // Can't just alias both commands to one word, have to make a command that performs both
		{
			await YTSearch(query);
			await SCSearch(query);
		}

		[Command("ytsearch")]
		public async Task YTSearch([Remainder]string query)
		{
			query.FormatForURL("+");
			if (Uri.TryCreate($"https://www.youtube.com/results?search_query={query}&page=1", UriKind.Absolute, out Uri result))
			{
				var list = new List<TrackInfo>();
				try
				{
					string html = await Program.Instance.Audio.WebClient.DownloadStringTaskAsync(result);

					// Search string
					string pattern = "<div class=\"yt-lockup-content\">.*?title=\"(?<NAME>.*?)\".*?</div></div></div></li>";
					MatchCollection matches = Regex.Matches(html, pattern, RegexOptions.Singleline);

					for (int ctr = 0; ctr <= matches.Count - 1; ctr++)
					{
						// Title
						string title = matches[ctr].Groups[1].Value;

						// Author
						string author = matches[ctr].Value.Substring("/user/", "class").Replace('"', ' ').Trim();

						// Duration
						string duration = matches[ctr].Value.Substring("id=\"description-id-", "span").Substring(": ", "<").Replace(".", "");

						// Url
						string url = String.Concat("http://www.youtube.com/watch?v=", matches[ctr].Value.Substring("watch?v=", "\""));

						// Thumbnail
						string thumbnail = "https://i.ytimg.com/vi/" + matches[ctr].Value.Substring("watch?v=", "\"") + "/mqdefault.jpg";

						// Remove playlists
						if (title != "__title__")
						{
							if (duration != "")
							{
								// Add item to list
								var track = new TrackInfo
								{
									Name = title,
									Author = author,
									Duration = duration,
									URL = url,
									Thumb = thumbnail
								};
								list.Add(track);
							}
						}
					}
				}
				catch (Exception ex)
				{
					await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Error, "Search", ex.Message));
				}

				if (list.Count < 1)
				{
					await ReplyAsync("<:youtube:463135962936115214> No results, try different search terms");
					return;
				}

				var emb = new EmbedBuilder
				{
					Description = "YouTube results",
					Color = Color.Red,
					Timestamp = DateTime.UtcNow
				}.WithFooter(new EmbedFooterBuilder()
					.WithIconUrl(CDN.GetEmojiUrl(463135962936115214, false)));

				int i = 1;
				foreach (var track in list)
				{
					string title = track.Name;
					string author = track.Author;
					string url = track.URL;

					emb.AddField($"{i}", $"{author} - {title}\n\r{url}");

					i++;
				}

				await Context.Channel.SendMessageAsync("", embed: emb.Build());
			}
		}

		[Command("scsearch")]
		public async Task SCSearch([Remainder]string query)
		{
			query.FormatForURL("%20");
			//609550b81957a871adb254ababcc435c
			if (Uri.TryCreate($"https://api.soundcloud.com/tracks?q={query}&client_id=qeKwELFPARbJJEy0QYSOzftXk8acBMsw&limit=15", UriKind.Absolute, out Uri result))
			{
				var list = new List<TrackInfo>();
				try
				{
					string json = await Program.Instance.Audio.WebClient.DownloadStringTaskAsync(result);
					json = json.Replace("[{", "").Replace("}]", "");
					foreach (var o in json.Split("},{", StringSplitOptions.RemoveEmptyEntries))
					{
						JObject obj = JObject.Parse("{" + o + "}");

						string kind = (string)obj["kind"];
						if (!kind.Equals("track"))
							continue;

						string title = (string)obj["title"];

						string author = (string)obj["user"]["username"];

						string duration = TimeSpan.FromMilliseconds(double.Parse((string)obj["duration"])).ToString();

						string url = (string)obj["permalink_url"];

						string thumbnail = (string)obj["artwork_url"];

						if (duration != "")
						{
							// Add item to list
							var track = new TrackInfo
							{
								Name = title,
								Author = author,
								Duration = duration,
								URL = url,
								Thumb = thumbnail,
							};
							list.Add(track);
						}
					}
				}
				catch(JsonReaderException ex)
				{
					await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Error, "Search", "", ex));

					File.WriteAllText("badjson.json", await Program.Instance.Audio.WebClient.DownloadStringTaskAsync(result));
				}
				catch(Exception ex)
				{
					await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Error, "Search", "", ex));
				}

				if(list.Count < 1)
				{
					await ReplyAsync("<:soundcloud:463135942027509771> No results, try different search terms");
					return;
				}

				var emb = new EmbedBuilder
				{
					Description = "SoundCloud results",
					Color = Color.Orange,
					Timestamp = DateTime.UtcNow
				}.WithFooter(new EmbedFooterBuilder()
					.WithIconUrl(CDN.GetEmojiUrl(463135942027509771, false)));

				int i = 1;
				foreach (var track in list)
				{
					string title = track.Name;
					string author = track.Author;
					string url = track.URL;

					emb.AddField($"{i}", $"{author} - {title}\n\r{url}");

					i++;
				}

				await Context.Channel.SendMessageAsync("", embed: emb.Build());
			}
		}

		internal struct TrackInfo
		{
			public string Name;
			public string Duration;
			public string Author;
			public string URL;
			public string Thumb;

			public override string ToString()
			{
				return String.Join("\n\r", new string[] { Name, Author, Duration, URL, Thumb });
			}
		}
	}
}
