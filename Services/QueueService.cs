using System.Threading;
using System.Linq;

namespace MusicBot.Services
{
	// Processes a queue of urls that are managed per guild
	public class QueueService
	{
		public QueueService()
		{
			WorkerThread = new Thread(async () =>
				{
					while (true)
					{
						if (Program.Queues.IsEmpty)
						{
							Thread.Sleep(250);
							continue;
						}

						foreach (ulong guildID in Program.Queues.Keys)
						{
							
							if (!Program.Queues.TryGetValue(guildID, out var queue) || queue.IsEmpty)
								continue;

							if (!Program.Connections.TryGetValue(guildID, out var client) || client == null)
								continue;

							if (queue.TryDequeue(out Song song))
							{
								MusicBot.Commands.Skip.Reset(guildID);
								await Program.Instance.Audio.SendAudio(song, client);
							}
						}

						Thread.Sleep(333);
					}
				})
				{
					IsBackground = true,
					Name = "MusicBot Queue"
				};
			WorkerThread.Start();
		}

		~QueueService() => WorkerThread.Abort();

		private readonly Thread WorkerThread;
	}
}
