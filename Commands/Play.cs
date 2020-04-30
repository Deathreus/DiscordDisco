using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;

using MusicBot.Services;

namespace MusicBot.Commands
{
	public class Play : SocketModuleBase
	{
		[Command("play")]
		public async Task PlayLink([Remainder]string url)
		{
			if(String.IsNullOrEmpty(url) || !Utils.IsURL(url))
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Please provide a valid url.");
				return;
			}

			foreach(string id in File.ReadAllLines("banned-users.txt"))
			{
				if (ulong.Parse(id) == Context.Message.Author.Id)
				{
					await ReplyAsync("You aren't allowed to use me.");
					return;
				}
			}

			if (!Program.Queues.TryGetValue(Context.Guild.Id, out var queue) || queue == null)
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Something went horribly wrong, contact my agent.");
				return;
			}

			if(queue.Count >= Program.MaxRequests)
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Queue is full, try again later.");
				return;
			}

			Song song = await Program.Instance.Downloader.Download(url, true, Context);
			if (String.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Something went wrong, try again later.");
				return;
			}

			await ReplyAsync($"{Context.Message.Author.Mention}: Added to queue, wait patiently or vote to skip current song.");
			
			await Context.Message.AddReactionAsync(new Emoji("🎶"));
			await Context.Message.AddReactionAsync(Emote.Parse("<:check:463136032297189407>"));
		}
	}
}
