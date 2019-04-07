using System;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MusicBot.Services
{
	public class LogService
	{
		private readonly DiscordSocketClient _client;
		private readonly CommandService _commands;

		public LogService(DiscordSocketClient discord, CommandService commands)
		{
			_client = discord;
			_commands = commands;

			_client.Log += LogDiscord;
			_commands.Log += LogCommand;
		}

		public Task LogDiscord(LogMessage message)
		{
			switch (message.Severity)
			{
				case LogSeverity.Critical:
				case LogSeverity.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Debug:
				case LogSeverity.Verbose:
					Console.ForegroundColor = ConsoleColor.Gray;
					break;
				case LogSeverity.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogSeverity.Info:
					Console.ForegroundColor = ConsoleColor.Green;
					break;
				default:
					break;
			}

			Console.WriteLine(message.ToString());
			Console.ResetColor();

			return Task.CompletedTask;
		}

		public Task LogCommand(LogMessage message)
		{
			// Return an error message for async commands
			if (message.Exception is CommandException command)
			{
				var _ = command.Context.Channel.SendMessageAsync($"Error: {command.Message}");
			}

			LogDiscord(message);
			return Task.CompletedTask;
		}
	}
}
