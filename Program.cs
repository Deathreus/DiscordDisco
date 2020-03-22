using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;

using MusicBot.Services;

namespace MusicBot
{
	public sealed class Program : IDisposable
	{
		public LogService Logger { get; private set; }
		public AudioService Audio { get; private set; }
		public PersistanceService Downloader { get; private set; }
		public IServiceProvider Services { get; private set; }
		public static int MaxFiles { get { return Config.MaxFiles; } }
		public static int MaxRequests { get { return Config.MaxRequests; } }
		public static string Channel { get { return Config.ChannelName; } }
		public static bool PreferFFMpeg { get { return Config.PreferFFMpeg; } }

		public static ConcurrentDictionary<ulong, IAudioClient> Connections { get; } = new ConcurrentDictionary<ulong, IAudioClient>();
		public static ConcurrentDictionary<ulong, ConcurrentQueue<Song>> Queues { get; } = new ConcurrentDictionary<ulong, ConcurrentQueue<Song>>();

		public static Program Instance { get; private set; }

		[STAThread]
		public static void Main(params string[] args)
		{
			Config = new Configuration(args);
		#if DEBUG
			Console.Write(Config);
		#endif

			SetConsoleCtrlHandler(new EventHandler((e) =>
			{
				Instance.Dispose();

				Thread.SpinWait(3);

				Environment.Exit(-1);

				return true;
			}), true);

			Instance = new Program();
			Instance.MainAsync().GetAwaiter().GetResult();
		}

		private async Task MainAsync()
		{
			_client = new DiscordSocketClient(new DiscordSocketConfig
			{
				ConnectionTimeout = 4000,
				HandlerTimeout = 15000,
		#if DEBUG
				LogLevel = LogSeverity.Verbose
		#endif
			});
			
			_commands = new CommandService(new CommandServiceConfig
			{
				CaseSensitiveCommands = true,
				DefaultRunMode = RunMode.Async,
		#if DEBUG
				LogLevel = LogSeverity.Verbose
		#endif
			});

			Services = BuildServices();
			await FetchServices(Services);

			_client.Ready += OnReady;
			_client.GuildUnavailable += OnLoseGuild;
			_client.GuildAvailable += OnGuildAvailable;

			await _client.LoginAsync(TokenType.Bot, Config.Token);
			await _client.StartAsync();

			await Task.Delay(-1);
		}

		private static Task OnLoseGuild(SocketGuild guild)
		{
			if (Connections.TryRemove(guild.Id, out var client))
			{
				client.StopAsync();
				client.Dispose();
			}

			if (Queues.TryRemove(guild.Id, out var queue))
			{
				do {
					queue.TryDequeue(out var _);
				} while (!queue.IsEmpty);
			}

			return Task.CompletedTask;
		}

		private static Task OnGuildAvailable(SocketGuild guild)
		{
			string name = Config.ChannelName;
			IVoiceChannel channel = guild.VoiceChannels.Where(c => c.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault();
			if (channel != null)
			{
				Task.Run(async () =>
				{
					IAudioClient client = await channel.ConnectAsync();
					Connections.TryAdd(guild.Id, client);
					Queues.TryAdd(guild.Id, new ConcurrentQueue<Song>());
				});
			}

			return Task.CompletedTask;
		}

		private async Task OnReady()
		{
			await _client.SetGameAsync("?play <url>", "http://twitch.tv/0", ActivityType.Streaming);

			new Thread(async () =>
			{
				while (true)
				{
					foreach (var guild in _client.Guilds)
					{
						if (Instance.Audio.Stopped)
						{
							await OnLoseGuild(guild);
							await OnGuildAvailable(guild);
						}
					}

					Thread.Sleep(TimeSpan.FromMinutes(15));
				}
			})
			{
				IsBackground = true,
				Name = "KeepAlive Task"
			}.Start();
		}

		private async Task FetchServices(IServiceProvider provider)
		{
			await Task.Run(() =>
			{
				provider.GetRequiredService<QueueService>();
				Downloader = provider.GetRequiredService<PersistanceService>();
				Audio = provider.GetRequiredService<AudioService>();
				Logger = provider.GetRequiredService<LogService>();
			});
			await provider.GetRequiredService<CommandHandlingService>().InitializeAsync(provider);
		}

		public void Dispose()
		{
			var guilds = _client.Guilds.ToList();
			foreach (var guild in guilds)
				OnLoseGuild(guild);
			
			_client.Dispose();
		}

		private IServiceProvider BuildServices()
			=> new ServiceCollection()
				.AddSingleton(_client)
				.AddSingleton<QueueService>()
				.AddSingleton<PersistanceService>()
				.AddSingleton(_commands)
				.AddSingleton<AudioService>()
				.AddSingleton<CommandHandlingService>()
				.AddSingleton<LogService>()
				.BuildServiceProvider();

		internal class Configuration
		{
			// Voice channel to send to
			public string ChannelName { get; set; }
			// Max number of requests in the queue
			public int MaxRequests { get; set; } = 5;
			// Max files cached
			public int MaxFiles { get; set; } = 15;

			public string Token { get; set; }

			public bool PreferFFMpeg { get; set; } = false;

			public Configuration(string[] args)
			{
				ChannelName = args.ElementAtOrDefault(Array.FindIndex(args, s => s.Equals("--channel")) + 1);
				MaxRequests = int.Parse(args.ElementAtOrDefault(Array.FindIndex(args, s => s.Equals("--max-requests")) + 1));
				MaxFiles = int.Parse(args.ElementAtOrDefault(Array.FindIndex(args, s => s.Equals("--max-files")) + 1));
				Token = args.ElementAtOrDefault(Array.FindIndex(args, s => s.Equals("--token")) + 1);
				PreferFFMpeg = Array.FindIndex(args, s => s.Equals("--prefer-ffmpeg")) != -1;
			}

			public override string ToString()
			{
				string result = String.Empty;
				result += $"Channel: \"{ChannelName}\"" + "\n\r";
				result += $"Maximum requests: {MaxRequests.ToString()}" + "\n\r";
				result += $"Maximum stored files: {MaxFiles.ToString()}" + "\n\r";
				result += $"Bot token: {Token}" + "\n\r";
				result += $"Prefer FFMPeg: {PreferFFMpeg}" + "\n\r";
				return result;
			}
		};
		private static Configuration Config { get; set; }

		private DiscordSocketClient _client;
		private CommandService _commands;


		delegate bool EventHandler(int exitType);
		[DllImport("kernel32.dll")]
		static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
	}
}
