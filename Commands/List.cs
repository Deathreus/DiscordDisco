using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace MusicBot.Commands
{
	public class List : SocketModuleBase
	{
		[Command("list")]
		public async Task ListSongs()
		{
			var embed = new EmbedBuilder
			{
				Title = "Up Next",
				Color = Color.Gold,
				Timestamp = DateTime.UtcNow
			};

			if (!Program.Queue.IsEmpty)
			{
				int i = 1;
				using (var _enum = Program.Queue.GetEnumerator())
				{
					while (_enum.MoveNext())
					{
						Song song = _enum.Current;
						embed.AddField($"{i}", $"{song.Name}\n{song.Duration}", true);
						i++;
					}
				}
			}

			await ReplyAsync("", false, embed.Build());
		}
	}
}
