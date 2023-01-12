using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardBot
{
    public class Player
    {
        public DiscordUser? discordUser;
        public DiscordMember? discordMember;

        public int points;

        public ulong id;

        public List<string> hand;

        public List<string> fieldMonsters;
        public List<string> fieldSpells;

        public Player()
        {
            points = 0;
            hand = new List<string>();
            fieldMonsters = new List<string>();
            fieldSpells = new List<string>();
        }
    }
}
