using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;

using NAudio.Wave;

using Discord;
using Discord.WebSocket;
using Discord.Audio;

namespace MusicBot.Services
{
	public class AudioService
	{
		public AudioService(DiscordSocketClient client)
		{
			_client = client;
			_tcs = new TaskCompletionSource<bool>();
			_disposeToken = new CancellationTokenSource();
			WebClient = new WebClient();
			OutFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 48000, 2, 192000, 4, 16);
			Failed = Skip = Exit = false;
		}

		~AudioService()
		{
			_tcs.SetCanceled();
			_disposeToken.Cancel();

			WebClient.Dispose();
			WebClient = null;
		}

		public async Task SendAudio(Song song, IAudioClient client)
		{
			Failed = false;
			Skip = false;
			Exit = false;

			try
			{
				if (Program.PreferFFMpeg)
					await SendAudioOverFFMpeg(song, client).ConfigureAwait(false);
				else
					await SendAudioOverNAudio(song, client).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Critical, "Audio", ex.Message)); // Prints any errors to console
			}
		}

		public bool IsPlaying(Song song) => (DateTime.Now - StartTime).CompareTo(song.Duration) <= 0;

		public bool Pause
		{
			get => _internalPause;
			set
			{
				new Thread(() => _tcs.TrySetResult(value)).Start();
				_internalPause = value;
			}
		}
		internal bool _internalPause;

		public bool Skip
		{
			get
			{
				bool ret = _internalSkip;
				_internalSkip = false;
				return ret;
			}
			set => _internalSkip = value;
		}
		internal bool _internalSkip;

		public bool Exit { get; set; }

		public bool Stopped => Exit || Failed;

		public WebClient WebClient { get; private set; }

		public WaveFormat OutFormat { get; }

		private bool Failed { get; set; }

		private DateTime StartTime { get; set; }

		async Task SendAudioOverNAudio(Song song, IAudioClient client)
		{
			try
			{
				using (var reader = new MediaFoundationReader(song.FilePath))
				using (var mediaStream = new WaveChannel32(reader, .6f, 0f))
				using (var resampler = new MediaFoundationResampler(mediaStream, OutFormat))
				using (var outStream = client.CreatePCMStream(AudioApplication.Music))
				{
					resampler.ResamplerQuality = 60; // Set the quality of the resampler to 60, the highest quality
					mediaStream.PadWithZeroes = false; // Stop when we hit the end of the file
					int blockSize = OutFormat.AverageBytesPerSecond / 60; // Establish the size of our audio buffer
					byte[] buffer = new byte[blockSize];

					StartTime = DateTime.Now;

					await _client.SetGameAsync(song.Name, "http://twitch.tv/0", ActivityType.Streaming);

					while (!Skip && !Exit && !Failed && !_disposeToken.IsCancellationRequested) // Read audio into our buffer, and keep a loop open while data is present
					{
						try
						{
							if (resampler.Read(buffer, 0, blockSize) == 0)
							{
								Exit = true;
								continue;
							}

							await outStream.WriteAsync(buffer, 0, blockSize, _disposeToken.Token);

							if (Pause)
							{
								bool pauseAgain;

								do
								{
									pauseAgain = await _tcs.Task;
									_tcs = new TaskCompletionSource<bool>();
								} while (pauseAgain);
							}
						}
						catch (TaskCanceledException)
						{
							Exit = true;
						}
						catch
						{
							Failed = true;
							throw;
						}
					}

					await outStream.FlushAsync();

					await _client.SetGameAsync("?play <url>", "http://twitch.tv/0", ActivityType.Streaming);
				}
			}
			catch (Exception ex)
			{
				await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Critical, "Audio", ex.Message)); // Prints any errors to console
			}
		}

		async Task SendAudioOverFFMpeg(Song song, IAudioClient client)
		{
			try
			{
				using (var mediaStream = CreateStream(song.FilePath).StandardOutput.BaseStream)
				using (var outStream = client.CreatePCMStream(AudioApplication.Music))
				{
					int blockSize = OutFormat.AverageBytesPerSecond / 60; // Establish the size of our audio buffer
					byte[] buffer = new byte[blockSize];

					StartTime = DateTime.Now;

					await _client.SetGameAsync(song.Name, "http://twitch.tv/0", ActivityType.Streaming);

					while (!Skip && !Exit && !Failed && !_disposeToken.IsCancellationRequested) // Read audio into our buffer, and keep a loop open while data is present
					{
						try
						{
							if(await mediaStream.ReadAsync(buffer, 0, blockSize, _disposeToken.Token) == 0)
							{
								Exit = true;
								continue;
							}

							await outStream.WriteAsync(buffer, 0, blockSize, _disposeToken.Token);

							if (Pause)
							{
								bool pauseAgain;

								do
								{
									pauseAgain = await _tcs.Task;
									_tcs = new TaskCompletionSource<bool>();
								} while (pauseAgain);
							}
						}
						catch (TaskCanceledException)
						{
							Exit = true;
						}
						catch
						{
							Failed = true;
							throw;
						}
					}

					await outStream.FlushAsync();

					await _client.SetGameAsync("?play <url>", "http://twitch.tv/0", ActivityType.Streaming);
				}
			}
			catch (Exception ex)
			{
				await Program.Instance.Logger.LogDiscord(new LogMessage(LogSeverity.Critical, "Audio", ex.Message)); // Prints any errors to console
			}
		}

		private static Process CreateStream(string path)
		{
			return Process.Start(new ProcessStartInfo
			{
				FileName = ".\\bin\\ffmpeg",
				Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 -vol 154 pipe:1",
				UseShellExecute = false,
				RedirectStandardOutput = true
			});
		}

		private TaskCompletionSource<bool> _tcs;
		private CancellationTokenSource _disposeToken;
		private readonly DiscordSocketClient _client;
	}
}
