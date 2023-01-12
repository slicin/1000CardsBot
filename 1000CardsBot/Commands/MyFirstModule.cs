using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardBot.Commands
{
    public class MyFirstModule : BaseCommandModule
    {
        [Command("random")]
        public async Task RandomCommand(CommandContext ctx, int min, int max)
        {
            var random = new Random();
            await ctx.RespondAsync($"Your number is: {random.Next(min, max)}");
        }

        [Command("greet")]
        public async Task GreetCommand(CommandContext ctx, DiscordMember member)
        {
            await ctx.RespondAsync($"Greetings, {member.Mention}! Thank you for executing me!");
        }
    }
}
