using System.Threading;
using System.Linq;
using System.Threading.Tasks;

namespace MusicBot.Services
{
	// Processes a queue of urls that are managed per guild
	public class QueueService
	{
		public QueueService()
		{
			WorkerThread = new Thread(() =>
				{
					while (true)
					{
						if (Program.Queues.IsEmpty)
						{
							Thread.Sleep(500);
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
								Task.Run(async () => {
									await Program.Instance.Audio.SendAudio(song, client);
								});
								Thread.Sleep(5);
							}

							while (!Program.Instance.Audio.Stopped)
								Thread.Sleep(100);
						}

						Thread.Sleep(500);
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
