using System;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MusicBot.Services
{
	public class CommandHandlingService
	{
		private readonly CommandService _commands;
		private readonly DiscordSocketClient _client;
		private IServiceProvider _provider;

		public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands)
		{
			_commands = commands;
			_client = discord;
			_provider = provider;

			_client.MessageReceived += HandleCommand;
		}

		public async Task InitializeAsync(IServiceProvider services)
			=> await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

		private async Task HandleCommand(SocketMessage rawMessage)
		{
			// Ignore system messages and messages from bots
			if (!(rawMessage is SocketUserMessage message)) return;
			if (message.Source != MessageSource.User) return;

			int argPos = 0;
			if (!(message.HasCharPrefix('?', ref argPos) 
					|| message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;

			var context = new SocketCommandContext(_client, message);

			var result = await _commands.ExecuteAsync(context, argPos, _provider);
			if (!result.IsSuccess)
				await context.Channel.SendMessageAsync(string.Format("{0}: {1}", message.Author.Mention, result.ErrorReason));
		}
	}
}