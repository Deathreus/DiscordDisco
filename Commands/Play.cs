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

			Tuple<string, string> info = await Utils.GetInfo(url);
			Song song = new Song
			{
				Name = info.Item1,
				Duration = Utils.GetDuration(info.Item2),
				URL = url
			};
			Program.Queues[Context.Guild.Id].Enqueue(song);

			await ReplyAsync($"{Context.Message.Author.Mention}: Added to queue, wait patiently or vote to skip current song.");
			
			await Context.Message.AddReactionAsync(Emote.Parse("<:check:463136032297189407>"));
		}
	}
}
