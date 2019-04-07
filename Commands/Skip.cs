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

			ulong ID = Context.Guild.Id;

			if (!SkipVotes.Keys.Contains(ID))
				SkipVotes.Add(ID, 0);

			if (!GuildVotes.Keys.Contains(ID))
				GuildVotes.Add(ID, new List<ulong>());

			var list = GuildVotes.Where(kv => kv.Key.Equals(ID)).Single().Value;
			if(!list.Contains(Context.Message.Author.Id))
			{
				SkipVotes[ID] = SkipVotes[ID] + 1;
				list.Add(Context.Message.Author.Id);
			}
			else
			{
				await ReplyAsync($"Sorry {Context.Message.Author.Mention}, but you've already voted to skip.");
			}

			await Context.Message.AddReactionAsync(new Discord.Emoji("👌"));

			int ratio = (Context.Guild.VoiceChannels.First(c => c.Name.Equals(Program.Instance.Channel)).Users.Count / 4) + 1;

			if (SkipVotes[ID] >= ratio)
			{
				SkipVotes[ID] = 0;
				GuildVotes[ID].Clear();
				Program.Instance.Audio.Skip = true;
				await ReplyAsync("People agreed the current song is trash, skipping...");
			}
		}

		public static void Reset(ulong ID)
		{
			if (!SkipVotes.Keys.Contains(ID))
				SkipVotes.Add(ID, 0);

			if (!GuildVotes.Keys.Contains(ID))
				GuildVotes.Add(ID, new List<ulong>());

			SkipVotes[ID] = 0;
			GuildVotes[ID].Clear();
		}

		private static Dictionary<ulong, int> SkipVotes = new Dictionary<ulong, int>();

		private static Dictionary<ulong, List<ulong>> GuildVotes = new Dictionary<ulong, List<ulong>>();
	}
}
