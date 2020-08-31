using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord.Commands;

namespace MusicBot.Commands
{
	public class Skip : SocketModuleBase
	{
		[Command("skip"), Alias("next")]
		public async Task SkipSong()
		{
			if(Program.Instance.Audio.Stopped)
			{
				await ReplyAsync($"Yo {Context.Message.Author.Mention}, you can't vote if nothing is playing!");
				return;
			}

			ulong ID = Context.Message.Author.Id;
			if(!GuildVotes.Contains(ID))
			{
				SkipVotes += 1;
				GuildVotes.Add(ID);
			}
			else
			{
				await ReplyAsync($"Sorry {Context.Message.Author.Mention}, but you've already voted to skip.");
			}

			await Context.Message.AddReactionAsync(new Discord.Emoji("👌"));

			int ratio = (Context.Guild.VoiceChannels.First(c => c.Name.Equals(Program.Channel)).Users.Count / 4) + 1;

			if (SkipVotes >= ratio)
			{
				SkipVotes = 0;
				GuildVotes.Clear();
				Program.Instance.Audio.Skip = true;
				await ReplyAsync("People agreed the current song is trash, skipping...");
			}
		}

		public static void Reset()
		{
			SkipVotes = 0;
			GuildVotes.Clear();
		}

		private static int SkipVotes = 0;

		private static List<ulong> GuildVotes = new List<ulong>();
	}
}
