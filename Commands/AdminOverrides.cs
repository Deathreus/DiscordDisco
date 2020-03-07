using System;
using System.Threading.Tasks;

using Discord.Commands;

namespace MusicBot.Commands
{
	[RequireUserPermission(Discord.ChannelPermission.ManageChannels)]
	public class AdminOverrides : SocketModuleBase
	{
		[Command("fskip")]
		public async Task ForceSkip()
		{
			if (Program.Instance.Audio.Stopped)
				return;

			Program.Instance.Audio.Skip = true;
			await ReplyAsync("Forcefully skipping the current song.");
		}

		[Command("fpause")]
		public async Task ForcePause()
		{
			if (Program.Instance.Audio.Stopped)
				return;

			bool pause = Program.Instance.Audio.Pause;
			Program.Instance.Audio.Pause = !pause;
			await ReplyAsync(String.Format("Forcefully {0}paused the current song", pause ? "un" : String.Empty));
		}
	}
}
