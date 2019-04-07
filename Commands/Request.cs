using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;

using MusicBot.Services;

namespace MusicBot.Commands
{
	public class Request : SocketModuleBase
	{
		[Command("request"), STAThread]
		public async Task DMRequest([Remainder]string url)
		{
			if(String.IsNullOrEmpty(url) || !Utils.IsURL(url))
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Please provide a valid url.");
				return;
			}

			string file = await Program.Instance.Downloader.Download(url, false, Context);
			if(String.IsNullOrEmpty(file) || !File.Exists(file))
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Something went wrong, try again later.");
				return;
			}

			var channel = await Context.Message.Author.GetOrCreateDMChannelAsync();
			if(channel != null)
			{
				await channel.SendFileAsync(file, "Here's those fresh beats you requested");
				await channel.CloseAsync(new RequestOptions
				{
					RetryMode = RetryMode.AlwaysRetry
				});
				
				await Context.Message.AddReactionAsync(new Emoji("🎶"));
				await Context.Message.AddReactionAsync(Emote.Parse("<:check:463136032297189407>"));
				
				return;
			}
			
			var response = await Context.Channel.SendFileAsync(file, $"Sorry {Context.Message.Author.Mention}, I couldn't send a DM, here's a temporary file...");
			await response.AddReactionAsync(new Emoji("🎶"));

			new Thread(async () =>
			{
				Thread.Sleep(TimeSpan.FromMinutes(5.0).Milliseconds);
				await response.DeleteAsync(new RequestOptions
				{
					AuditLogReason = "Temporary message deleted",
					RetryMode = RetryMode.AlwaysRetry
				});
			}).Start();
		}
	}
}
