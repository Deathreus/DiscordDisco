using System;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace MusicBot.Commands
{
	public class Help : SocketModuleBase
	{
		[Command("help"), Alias("info")]
		public async Task SendHelp(string command = null)
		{
			if(!String.IsNullOrEmpty(command))
			{
				string description;
				switch (command)
				{
					case "request":
						description = "Given a valid URL, will download an audio file and send it over private message. Downloads are cached, so subsequent commands to the" +
							" same URL will happen alot faster than the first one. If it fails to DM you, it will just post it as a temporary message for 5 minutes." +
							" Will error out if provided an invalid URL or the website is not youtube.com our soundcloud.com.";
						break;
					case "play":
						description = "Given a valid URL, will download an audio file and play it over voice. Downloads are cached, so subsequent commands to the" +
							" same URL will happen alot faster than the first one. Will error out if given an invalid URL or just for unknown reasons ¯\\_(ツ)_/¯";
						break;
					case "search":
						description = "Same funcionality as the specific search functions, but looks for the phrase anywhere and returns lists of URLs.";
						break;
					case "ytsearch":
						description = "Given a search phrase, returns a small list of videos with their URLs from YouTube so you can copy and paste for the other commands." +
							" Will error out if it doesn't find anything or you put invalid characters in your search.";
						break;
					case "scsearch":
						description = "Given a search phrase, returns a small list of videos with their URLs from SoundCloud so you can copy and paste for the other commands." +
							" Will error out if it doesn't find anything or you put invalid characters in your search.";
						break;
					default:
						await ReplyAsync($"{Context.Message.Author.Mention}: Please provide a known command or a command with a help string...");
						return;
				}

				var embed = new EmbedBuilder
				{
					Title = command,
					Color = Color.DarkBlue,
					Timestamp = DateTime.UtcNow,
					Description = description
				}.Build();
				await Context.Channel.SendMessageAsync("", false, embed);
				return;
			}

			var emb = new EmbedBuilder
			{
				Title = "Available Commands",
				Description = "Type ?help <command> for expanded info on each command",
				Color = Color.DarkBlue,
				Timestamp = DateTime.UtcNow,

				Footer = new EmbedFooterBuilder
				{
					Text = "?help",
					IconUrl = Context.Client.CurrentUser.GetAvatarUrl()
				},

				Fields = {
					new EmbedFieldBuilder
					{
						Name = "vvv",
						IsInline = false,
						Value = "?help [command] - Display command info\n" +
								"?info [command] - Display command info\n" +
								"?request <url> - Have me DM you a file with the sound from the URL\n" +
								"?play <url> - Have me play play the song directly through voice\n" +
								"?skip - Vote to skip the currently playing song\n" +
								"?search <query> - Search both soundcloud and youtube for the phrase\n" +
								"?ytsearch <query> - Search youtube only\n" +
								"?scsearch <query> - Search soundcloud only\n" +
								"?list - List the queue of songs I have lined up\n"
					}
				}
			};

			await Context.Channel.SendMessageAsync("", false, emb.Build());
		}
	}
}
