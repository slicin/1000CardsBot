using System;
using System.Threading.Tasks;
using CardBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;

namespace CardBot
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = "",
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages
            });

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "." }
                
            });

            //commands.RegisterCommands<MyFirstModule>();
            commands.RegisterCommands<Game>();

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}