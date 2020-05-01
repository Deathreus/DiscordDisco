using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;

using MusicBot.Services;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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

			Song song = await Program.Instance.Downloader.Download(url, false, Context);
			if (String.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
			{
				await ReplyAsync($"{Context.Message.Author.Mention}: Something went wrong, try again later.");
				return;
			}

			var message = await Context.Channel.SendMessageAsync("Processing...");
			string file = await PostProcess(song);
			await message.ModifyAsync((mp) => mp.Content = "Processing...\nDone!");

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

		private Task<string> PostProcess(Song song)
		{
			string directory = Path.GetDirectoryName(song.FilePath);
			string fileName = Path.GetFileName(song.FilePath);

			string newName = string.Format("{0}.mp3", song.Name).Trim();

			Encoding encoder = Encoding.GetEncoding(Encoding.ASCII.EncodingName,
													new EncoderReplacementFallback(string.Empty),
													new DecoderExceptionFallback());
			byte[] bytes = Encoding.Convert(Encoding.UTF8, encoder, Encoding.UTF8.GetBytes(newName));
			newName = Encoding.ASCII.GetString(bytes);
			
			foreach (char invalidChar in Path.GetInvalidPathChars())
			{
				newName = newName.Replace(invalidChar, '_');
			}

			string metaArguments = $"-metadata title=\"{song.Name.Escape()}\" -metadata album=downloaded";
			var ffmpeg = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = $".\\bin\\ffmpeg",
					Arguments = $"-hide_banner -y -i \"{directory}\\{fileName}\" {metaArguments} -b:a 196k \"{directory}\\{newName}\"",
					CreateNoWindow = true,
					UseShellExecute = false
				}
			};

			try
			{
				ffmpeg.Start();
				ffmpeg.WaitForExit();
			}
			catch (Exception ex)
			{
				Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Warning, "ffmpeg", $"Could not reprocess downloaded song:\n\r{ex}"));
				return Task.FromException<string>(ex);
			}

			ffmpeg.Dispose();

			return Task.FromResult($"{directory}\\{newName}");
		}
	}
}
