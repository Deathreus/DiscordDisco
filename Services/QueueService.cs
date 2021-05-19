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
						if (Program.Queue.IsEmpty)
						{
							Thread.Sleep(500);
							continue;
						}

						if (Program.Queue.TryDequeue(out Song song))
						{
							MusicBot.Commands.Skip.Reset();
							Task.Run(async () => {
								await Program.Instance.Audio.SendAudio(song, Program.Connection);
							});
							Thread.Sleep(5);
						}

						while (!Program.Instance.Audio.Stopped)
							Thread.Sleep(100);

						Thread.Sleep(1000);
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
