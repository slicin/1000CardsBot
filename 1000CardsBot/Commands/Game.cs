using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System.Text;
using System.Text.RegularExpressions;

namespace CardBot.Commands
{
    public class Game : BaseCommandModule
    {
        HttpClient httpClient = new HttpClient();

        Dictionary<ulong, Player> playerList = new Dictionary<ulong, Player>();

        bool gameStarted = false;
        //int deckSize = 50;
        string cardDir = "C:\\Users\\student\\Desktop\\Cards\\";

        // Card Lists
        List<string> cardList = new List<string>();
        List<string> deck = new List<string>();
        List<string> discardPile = new List<string>();
        List<string> fieldCenter = new List<string>();

        public Game()
        {
            // Initialize card list
            ReloadCardList();
        }

        // Commands
        [Command("add")]
        public async Task AddCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("To add points to a player, specify the points to add followed by mentioning them.\n" +
                "Example:\n.add 200 @Coolguy123");
            return;
        }

        [Command("add")]
        public async Task AddCommand(CommandContext ctx, int points, DiscordMember player)
        {
            if (!playerList.ContainsKey(player.Id))
            {
                await ctx.RespondAsync("That player isn't currently playing.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            if (points <= 0)
            {
                await ctx.RespondAsync("Please add a positive number of points.");
                return;
            }

            playerList[player.Id].points += points;
            await ctx.RespondAsync($"{player.Mention} has gained {points} points!\nThey now have {playerList[player.Id].points}.");
        }

        [Command("begin")]
        public async Task BeginCommand(CommandContext ctx)
        {
            if (playerList.Count < 1)
            {
                await ctx.RespondAsync("You need at least 2 players to begin the game.");
                return;
            }

            if (gameStarted)
            {
                await ctx.RespondAsync("A game is already in progress.");
                return;
            }

            gameStarted = true;
            await ctx.RespondAsync("One moment while I generate a new deck and deal the starting hands.");

            GenerateNewDeck(50);
            AssignDiscordMembersForDM(ctx);
            await DealStartingHand(ctx);
        }

        [Command("begin")]
        public async Task BeginCommand(CommandContext ctx, int deckSize)
        {
            if (playerList.Count < 1)
            {
                await ctx.RespondAsync("You need at least 2 players to begin the game.");
                return;
            }

            if (gameStarted)
            {
                await ctx.RespondAsync("A game is already in progress.");
                return;
            }

            if (deckSize < 30 || deckSize > cardList.Count)
            {
                await ctx.RespondAsync($"Please specify a deck size between 30 and {cardList.Count}");
                return;
            }

            gameStarted = true;
            await ctx.RespondAsync("One moment while I generate a new deck and deal the starting hands.");

            GenerateNewDeck(deckSize);
            AssignDiscordMembersForDM(ctx);
            await DealStartingHand(ctx);
        }

        [Command("cards")]
        public async Task CardsCommand(CommandContext ctx)
        {
            String cardListString = "";
            foreach (var card in cardList)
            {
                string moddedCardName = card.Replace(".jpg", "");
                moddedCardName = moddedCardName.Replace(".png", "");
                cardListString += moddedCardName + "\n";
            }

            string fileName = $"{cardDir}\\Misc\\cardlist.txt";

            try
            {
                // Check if file already exists. If yes, delete it
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                // Create a new file     
                using (FileStream fs = File.Create(fileName))
                {
                    // Add cardlist to file    
                    Byte[] cardListData = new UTF8Encoding(true).GetBytes(cardListString);
                    fs.Write(cardListData, 0, cardListData.Length);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());
            }

            using (var fs = new FileStream($"{fileName}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"The Card List contains {cardList.Count} cards.")
                    .WithFiles(new Dictionary<string, Stream>() { { fileName, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("cards")]
        public async Task CardsCommand(CommandContext ctx, string option)
        {
            if (option.ToLower() == "reload")
            {
                // Reload card list from card folder
                int oldCardCount = cardList.Count;
                ReloadCardList();

                await ctx.RespondAsync($"Card List was reloaded. {cardList.Count - oldCardCount} new cards were added.\n" +
                    $"Previous: {oldCardCount}\n" +
                    $"Current: {cardList.Count}");
            }
        }

        [Command("deck")]
        public async Task DeckCommand(CommandContext ctx)
        {
            ulong playerID = ctx.Message.Author.Id;
            if (!playerList.ContainsKey(playerID))
            {
                await ctx.RespondAsync("You haven't joined the game yet. Use .join to play.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            string deckString = "";
            foreach (var card in deck)
            {
                deckString += $"{TrimFileExtension(card)}\n";
            }

            await ctx.RespondAsync($"There are {deck.Count} cards in the deck:\n" +
                $"{deckString}");
        }

        [Command("discard")]
        public async Task DiscardCommand(CommandContext ctx)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            String discardString = "";
            foreach (var card in discardPile)
            {
                string moddedCardName = card.Replace(".jpg", "");
                moddedCardName = moddedCardName.Replace(".png", "");
                discardString += moddedCardName + "\n";
            }

            await ctx.RespondAsync($"The discard pile contains {discardPile.Count} cards.\n {discardString}\n" +
                $"To discard a card, specify the card name or pattern. Example:\n" +
                $".discard karate_ant\n" +
                $".discard karate");
        }

        [Command("discard")]
        public async Task DiscardCommand(CommandContext ctx, string cardSearch)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            ulong playerID = ctx.Message.Author.Id;

            //if (!playerList.ContainsKey(playerID))
            //{
            //    await ctx.RespondAsync("You haven't joined the game yet. Use .join to play.");
            //    return;
            //}

            List<string> sourceList = playerList[playerID].hand;

            // Search hand for card
            bool doneSearching = false;
            List<string> resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].hand, true);
            if (resultList.Count == 1)
            {
                doneSearching = true;
                sourceList = playerList[playerID].hand;
            }

            if (!doneSearching)
            {
                // Search field monsters for card
                resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].fieldMonsters, true);
                if (resultList.Count == 1)
                {
                    doneSearching = true;
                    sourceList = playerList[playerID].fieldMonsters;
                }
            }

            if (!doneSearching)
            {
                // Search field spells for card
                resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].fieldSpells, true);
                if (resultList.Count == 1)
                {
                    doneSearching = true;
                    sourceList = playerList[playerID].fieldSpells;
                }
            }

            if (!doneSearching)
            {
                // Search field center for card
                resultList = await SearchListForCard(ctx, cardSearch, fieldCenter, true);
                if (resultList.Count == 1)
                {
                    doneSearching = true;
                    sourceList = fieldCenter;
                }
            }

            if (!doneSearching)
            {
                await ctx.RespondAsync("Ooch");
                return;
            }

            string foundCard = resultList[0];
            //Console.WriteLine($"Found a monster in the battle zone called {foundCard}.");

            // Remove battle position from card name if it was found in the monster zone
            if (sourceList == playerList[playerID].fieldMonsters)
            {
                //Console.WriteLine($"About to discard a card from the monster zone called {foundCard}");
                foundCard = foundCard.Substring(0, foundCard.Length - 6);
                foundCard += ".jpg";
            }

            //Console.WriteLine($"About to discard a card from the monster zone called {foundCard}");

            MoveCardToList(foundCard, sourceList, discardPile, true);

            // Remove card from monster zone if it was found there
            if (sourceList == playerList[playerID].fieldMonsters)
            {
                playerList[playerID].fieldMonsters.Remove(resultList[0]);
            }


            // DM the player their updated hand if the card came from the hand
            string cardName = TrimFileExtension(foundCard);
            string playerHandCount = "";
            if (sourceList == playerList[playerID].hand)
            {
                await DMPlayerHand(playerID, $"You have discarded {cardName}.\n");
                playerHandCount += $"{ctx.Message.Author.Username} now has {playerList[playerID].hand.Count} cards in hand.";
            }

            // Build discord message to show everyone the discarded card
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{ctx.Message.Author.Username} sends {cardName} to the discard pile. {playerHandCount}")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("draw")]
        public async Task DrawCommand(CommandContext ctx)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            ulong playerID = ctx.Message.Author.Id;

            //if (!await PlayerJoined(ctx))
            //    return;

            if (deck.Count <= 0)
            {
                await ctx.RespondAsync("The deck is empty.");
                return;
            }

            var random = new Random();
            int index = random.Next(deck.Count);
            string cardName = deck[index];

            // Remove drawn card from deck and add it to player's hand
            deck.Remove(cardName);
            playerList[playerID].hand.Add(cardName);

            // Create a DM with the player
            string stripCard = TrimFileExtension(cardName);
            await DMPlayerHand(playerID, $"You have drawn a {stripCard}.\n");

            await ctx.RespondAsync($"{ctx.Message.Author.Username} drew a card and now has {playerList[playerID].hand.Count} cards in hand.\nCards remaining in deck: {deck.Count}");
        }

        [Command("end")]
        public async Task EndCommand(CommandContext ctx)
        {
            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            gameStarted = false;
            ResetGame();

            await ctx.RespondAsync("The game has ended and the play area has been reset.");
        }

        [Command("field")]
        public async Task FieldCommand(CommandContext ctx)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            ulong playerID = ctx.Message.Author.Id;
            string fieldString = "";

            // Enumerate center cards
            fieldString += $"**__--- Center ({fieldCenter.Count}/5) ---__**\n";
            foreach (var centerCard in fieldCenter)
            {
                fieldString += $"{TrimFileExtension(centerCard)}\n";
            }

            // Enumerate player fields
            foreach (var player in playerList)
            {
                fieldString += $"\n**__--- {player.Value.discordMember.Username} Field ---__**\n";

                // Get monsters
                fieldString += $"__- Monsters ({playerList[player.Key].fieldMonsters.Count}/5) -__\n";
                foreach (var monster in playerList[player.Key].fieldMonsters)
                {
                    //fieldString += $"{TrimCardName(monster)}\n";
                    fieldString += $"{monster}\n";
                }

                // Get spells
                fieldString += $"__- Spells ({playerList[player.Key].fieldSpells.Count}/5) -__\n";
                foreach (var spell in playerList[player.Key].fieldSpells)
                {
                    fieldString += $"{TrimFileExtension(spell)}\n";
                }
            }

            await ctx.RespondAsync($"{ctx.Message.Author.Username} examines the field.\n\n" +
                $"{fieldString}");
        }

        [Command("flip")]
        public async Task FlipCommand(CommandContext ctx)
        {
            // Flip coin
            var random = new Random();
            int lowerBound = 0;
            int upperBound = 2;
            int flip = random.Next(lowerBound, upperBound);

            string result = "";
            switch (flip)
            {
                case 0:
                    result = "Heads";
                    break;
                case 1:
                    result = "Tails";
                    break;
            }

            await ctx.RespondAsync($"{ctx.Message.Author.Username} flips a coin, and the result is... {result}!");
        }

        [Command("give")]
        public async Task GiveCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Give a card to another player by specifying the card number in your hand (for concealment) and then mentioning the player. \n" +
                "Example:\n" +
                ".give 2 @Coolguy123");
        }

        [Command("give")]
        public async Task GiveCommand(CommandContext ctx, int handNumber, DiscordMember targetPlayer)
        {
            ulong playerID = ctx.Message.Author.Id;

            if (!playerList.ContainsKey(playerID))
            {
                await ctx.RespondAsync("You haven't joined the game. Use .join.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            if (!playerList.ContainsKey(targetPlayer.Id))
            {
                await ctx.RespondAsync("That player isn't currently playing.");
                return;
            }

            if (handNumber <= 0 || handNumber > 20)
            {
                await ctx.RespondAsync("Please specify a valid card number in your hand.");
                return;
            }

            // Prevent players from giving themselves cards (Comment out for debugging)
            //if (playerID == targetPlayer.Id)
            //{
            //    await ctx.RespondAsync("You can't give yourself a card. What are you, some kind of lunatic?");
            //    return;
            //}

            // Check if the number is a valid card in hand
            string foundCard = playerList[playerID].hand[handNumber-1];
            string stripCard = foundCard.Substring(0, foundCard.Length - 4);
            if (foundCard == "")
            {
                await ctx.RespondAsync($"You don't have {handNumber} cards in your hand.");
                return;
            }

            // Remove the card from player hand and add it to target player hand
            playerList[playerID].hand.Remove(foundCard);
            playerList[targetPlayer.Id].hand.Add(foundCard);

            // DM the giving player the given card and updated hand
            await DMPlayerHand(playerID, $"You have given {stripCard} to {targetPlayer.Username}.\n");

            // DM the receiving player the given card and updated hand
            await DMPlayerHand(targetPlayer.Id, $"You have received {stripCard} from {playerList[playerID].discordMember.Username}.\n");

            // Announce the card transfer
            await ctx.RespondAsync($"{ctx.Message.Author.Username} passes a card to {targetPlayer.Username}.");
        }

        [Command("hand")]
        public async Task HandCommand(CommandContext ctx)
        {
            ulong playerID = ctx.Message.Author.Id;

            if (!playerList.ContainsKey(playerID))
            {
                await ctx.RespondAsync("You haven't joined the game yet. Use .join to play.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            if (playerList[playerID].hand.Count <= 0)
            {
                await ctx.RespondAsync("Your hand is empty.");
                return;
            }

            // Create string of hand cards
            Dictionary<string, Stream> handCards = new Dictionary<string, Stream>();
            string handString = "";
            int handSlot = 1;

            foreach (var card in playerList[playerID].hand)
            {
                var fs = new FileStream($"{cardDir}{card}", FileMode.Open, FileAccess.Read);
                handCards.Add(card, fs);
                //string stripCard = card.Substring(0, card.Length - 4);
                string stripCard = TrimFileExtension(card);
                handString += handSlot + ". " + stripCard + "\n";
                handSlot++;
            }

            // DM the player their hand and react to their command
            await DMPlayerHand(playerID, $"");
            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":hand_splayed:"));
        }

        [Command("insert")]
        public async Task InsertCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Insert a card from the card list into the deck by specifying its name. Example:\n" +
                ".insert karate_ant");
        }

        [Command("insert")]
        public async Task InsertCommand(CommandContext ctx, string cardSearch)
        {
            List<string> resultList = await SearchListForCard(ctx, cardSearch, cardList, false);

            if (resultList.Count != 1)
                return;

            var foundCard = resultList[0];
            var cardName = TrimFileExtension(foundCard);

            MoveCardToList(foundCard, cardList, deck, false);

            // Build a message and attach the card to show everyone
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"A {cardName} is inserted into the deck.\n" +
                    $"The deck now contains {deck.Count} cards.")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("join")]
        public async Task JoinCommand(CommandContext ctx)
        {
            if (gameStarted)
            {
                await ctx.RespondAsync("A game is already in progress.");
                return;
            }

            if (!playerList.ContainsKey(ctx.Message.Author.Id))
            {
                Player newPlayer = new Player();
                newPlayer.discordUser = ctx.Message.Author;
                newPlayer.id = ctx.Message.Author.Id;

                playerList.Add(ctx.Message.Author.Id, newPlayer);

                await ctx.RespondAsync($"{ctx.Message.Author.Username} has joined the game");

            }
            else
            {
                await ctx.RespondAsync("You've already joined the game.");
            }

            //if (!playerList.Contains(ctx.Message.Author))
            //{
            //    playerList.Add(ctx.Message.Author);
            //    String playerListString = "";
            //    foreach (var player in playerList)
            //    {
            //        playerListString += player.Mention + " ";
            //    }
            //    await ctx.RespondAsync($"{ctx.Message.Author.Mention} has joined the game. Current players are: {playerListString}");
            //}
            //else
            //{
            //    await ctx.RespondAsync($"{ctx.Message.Author.Mention}, you've already joined the game.");
            //}

        }

        [Command("look")]
        public async Task LookCommand(CommandContext ctx)
        {
            await ctx.RespondAsync($"Use this command to inspect any card in the card list. You can search via card name or pattern.\n" +
                $"Example:\n" +
                $".look karate_ant\n" +
                $".look kara");
        }

        [Command("look")]
        public async Task LookCommand(CommandContext ctx, string cardSearch)
        {
            // Search for card in card list
            List<string> resultList = await SearchListForCard(ctx, cardSearch, cardList, false);

            if (resultList.Count != 1)
                return;

            // Build a message and attach the card to show everyone
            string foundCard = resultList[0];
            string cardName = TrimFileExtension(foundCard);
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{ctx.Message.Author.Username} looks at {cardName} in the card list.")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("new")]
        public async Task NewCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("To add a new card, attach a single .jpg or .png image and provide a card name with underscore spacing.\nExample:\n.new some_new_card\n:black_joker:");
        }

        [Command("new")]
        public async Task NewCommand(CommandContext ctx, string cardName)
        {
            var regexItem = new Regex("^[a-zA-Z0-9_]*$");
            if (!regexItem.IsMatch(cardName))
            {
                await ctx.RespondAsync("Please choose a card name with underscore spacing. (Example: a_really_cool_card)");
                return;
            }

            if (deck.Contains(cardName + ".jpg") || deck.Contains(cardName + ".png"))
            {
                await ctx.RespondAsync("A card with that name already exists in the deck.");
                return;
            }

            // Ensure there is one attachment
            int attachments = ctx.Message.Attachments.Count;
            if (attachments != 1)
            {
                await ctx.RespondAsync($"Please attach the card you want to add.");
                return;
            }

            // Ensure attachment is either a .jpg or .png
            if (!ctx.Message.Attachments.First().FileName.EndsWith(".png") & !ctx.Message.Attachments.First().FileName.EndsWith(".jpg"))
            {
                await ctx.RespondAsync($"Please attach a card in .jpg or .png format.");
                return;
            }

            // Download card to deck folder and add its filename to the deck list and card library
            using (var stream = await httpClient.GetStreamAsync(ctx.Message.Attachments.First().Url))
            {
                using (var fileStream = new FileStream($"{cardDir}{cardName}.jpg", FileMode.CreateNew))
                {
                    await stream.CopyToAsync(fileStream);
                    deck.Add($"{cardName}.jpg");
                    cardList.Add($"{cardName}.jpg");
                }
            }

            // Update web card list
            // Here is the raw post request content from my browser's network monitor:
            //content=%23Card+List%0D%0A%0D%0Acard++%0D%0Acard++%0D%0Acard++%0D%0Acard++%0D%0Acard++%0D%0Acard++&custom_url=cardlist&page_id=1443204&custom_edit_code=&title=&author=&description=&form_level=3&edit_code=A3G9H4A0GK450H45H&username=&update=Save+and+done&original_title=&original_url=cardlist
            //edit code A3G9H4A0GK450H45H
            //var json = JsonConvert.SerializeObject(person);
            //var data = new StringContent(json, Encoding.UTF8, "application/json");

            //var url = "https://httpbin.org/post";
            //using var client = new HttpClient();

            //var response = await client.PostAsync(url, data);
            //var content = new FormUrlEncodedContent(new[]
            //{
            //    new KeyValuePair<string, string>("content", "%23Card+List%0D%0A%0D%0Apoop++%0D%0Apoop++%0D%0Apoop++%0D%0Apoop++%0D%0Apoop++%0D%0Apoop++"),
            //    new KeyValuePair<string, string>("custom_url", "cardlist"),
            //    new KeyValuePair<string, string>("page_id", "1443204"),
            //    new KeyValuePair<string, string>("form_level", "3"),
            //    new KeyValuePair<string, string>("edit_code", "A3G9H4A0GK450H45H"),
            //    new KeyValuePair<string, string>("update", "Save+and+done"),
            //    new KeyValuePair<string, string>("original_url", "cardlist")
            //});

            //var json = JsonConvert.SerializeObject(content);
            //var data = new StringContent(json, Encoding.UTF8, "application/json");

            //var result = await httpClient.PostAsync("http://txti.es/", data);
            //Console.WriteLine($"Result: {result}\nStatus: {result.StatusCode}");

            await ctx.RespondAsync($"Card added. Card List now contains {cardList.Count} cards.");
        }

        [Command("pos")]
        public async Task PosCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Change the position of one of your monsters by specifying its name and the position. Examples:\n" +
                ".pos karate_ant atk\n" +
                ".pos karate_ant def");
        }

        [Command("pos")]
        public async Task PosCommand(CommandContext ctx, string cardSearch, string position)
        {
            ulong playerID = ctx.Message.Author.Id;
            List<string> resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].fieldMonsters, false);

            if (resultList.Count != 1)
                return;

            string foundCard = resultList[0];

            // Determine new battle position string
            string newPosition = "";
            if (position.ToLower() == "atk")
            {
                newPosition = " (ATK)";
            }
            else if (position.ToLower() == "def")
            {
                newPosition = " (DEF)";
            }
            else
            {
                await ctx.RespondAsync("Please specify a valid battle position (atk or def).");
                return;
            }

            // Change selected monster's name to reflect new battle position
            string newMonsterName = ChangeBattlePosition(foundCard, newPosition);
            playerList[playerID].fieldMonsters[playerList[playerID].fieldMonsters.IndexOf(foundCard)] = newMonsterName;

            // Announce the position change
            await ctx.RespondAsync($"{ctx.Message.Author.Username} changes {TrimBattlePosition(foundCard)} to{newPosition} position.");
        }

        [Command("restart")]
        public async Task RestartCommand(CommandContext ctx)
        {
            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            ResetGame();
            //// Empty and then re-initialize deck with files in deck folder
            //deck.Clear();
            //string[] cardFiles = Directory.GetFiles(deckPath);
            //foreach (var card in cardFiles)
            //{
            //    deck.Add(card.Replace(deckPath, ""));
            //}
            //deckSize = deck.Count();

            //// Empty discard pile and player hands and clear player scores
            //discardPile.Clear();
            //foreach (var player in playerList)
            //{
            //    player.Value.hand.Clear();
            //    player.Value.points = 0;
            //}

            await ctx.RespondAsync("One moment - restarting the game...");

            await DealStartingHand(ctx);
        }

        [Command("revive")]
        public async Task ReviveCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Revive a card to your hand from the discard pile by specifying the card name.");
        }

        [Command("revive")]
        public async Task ReviveCommand(CommandContext ctx, string cardSearch)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            ulong playerID = ctx.Message.Author.Id;

            List<string> resultList = await SearchListForCard(ctx, cardSearch, discardPile, false);

            if (resultList.Count != 1)
                return;

            string foundCard = resultList[0];
            string cardName = TrimFileExtension(foundCard);
            MoveCardToList(foundCard, discardPile, playerList[playerID].hand, true);

            await DMPlayerHand(playerID, $"You revived {cardName} from the discard pile.\n");

            // Build a message and attach the card to show everyone
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{ctx.Message.Author.Username} moves {cardName} from the discard pile to their hand, and now has {playerList[playerID].hand.Count} cards in hand.")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("roll")]
        public async Task RollCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Roll dice and specify the amount and sides.\n" +
                "Examples:\n" +
                ".roll 1d6\n" +
                ".roll 1d20");
        }

        [Command("roll")]
        public async Task RollCommand(CommandContext ctx, string die)
        {
            if (!die.Contains("d"))
            {
                await ctx.RespondAsync("Roll dice and specify the amount and sides.\n" +
                    "Examples:\n" +
                    ".roll 1d6\n" +
                    ".roll 1d20");
                return;
            }

            string[] dieParams = die.Split("d");
            string dieCount = dieParams[0];
            string dieSides = dieParams[1];

            if (!int.TryParse(dieCount, out _) || !int.TryParse(dieSides, out _))
            {
                await ctx.RespondAsync("Provide valid dice numbers.\n Example: .roll 1d6");
                return;
            }

            int iDieCount = int.Parse(dieCount);
            int iDieSides = int.Parse(dieSides);

            if (iDieCount > 20 || iDieSides > 20)
            {
                await ctx.RespondAsync("Dice numbers cannot exceed 20.");
                return;
            }

            // Roll dice and collect results
            var random = new Random();
            int lowerBound = 1;
            int upperBound = iDieSides + 1;
            int total = 0;
            string resultString = "";
            for (int i = 0; i < iDieCount; i++)
            {
                int roll = random.Next(lowerBound, upperBound);
                total += roll;
                resultString += roll.ToString() + "\n";
            }
            resultString += "Total: " + total.ToString();

            await ctx.RespondAsync($"{ctx.Message.Author.Username} rolls {die}!\n{resultString}");
        }

        [Command("score")]
        public async Task ScoreCommand(CommandContext ctx)
        {
            //if (!gameStarted)
            //{
            //    await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
            //    return;
            //}

            if (playerList.Count == 0)
            {
                await ctx.RespondAsync("There are currently no players playing.");
                return;
            }

            string scoreBoard = "";
            foreach (var player in playerList)
            {
                scoreBoard += player.Value.discordUser.Username + ": " + player.Value.points.ToString() + "\n";
            }

            await ctx.RespondAsync("Player Scores:\n" + $"{scoreBoard}");
        }

        [Command("search")]
        public async Task SearchCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Search a card from the deck and add it to your hand by specifying the card name.");
        }

        [Command("search")]
        public async Task SearchCommand(CommandContext ctx, string cardSearch)
        {
            if (!await CheckPlayerJoined(ctx))
                return;

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            ulong playerID = ctx.Message.Author.Id;
            List<string> resultList = await SearchListForCard(ctx, cardSearch, deck, false);

            if (resultList.Count != 1)
                return;

            MoveCardToList(resultList[0], deck, playerList[playerID].hand, true);

            // Build a message and attach the card to show everyone
            string foundCard = resultList[0];
            string cardName = TrimFileExtension(foundCard);
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{ctx.Message.Author.Username} pulls {cardName} from the deck and adds it to their hand.\n" +
                    $"They now have {playerList[playerID].hand.Count} cards in hand.\nThe deck now has {deck.Count} cards.\n")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }

            await DMPlayerHand(playerID, $"You took a {cardName} from the deck.\n");
        }

        [Command("show")]
        public async Task ShowCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("To show a card, type in it's name or a pattern.\n" +
                "For example, you can show karate_ant with any of the following commands:\n" +
                ".show karate_ant\n" +
                ".show karate\n" +
                ".show ant");
        }

        [Command("show")]
        public async Task ShowCommand(CommandContext ctx, string cardSearch)
        {
            ulong playerID = ctx.Message.Author.Id;

            if (!playerList.ContainsKey(playerID))
            {
                await ctx.RespondAsync("You haven't joined the game yet. Use .join to play.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            // Search for card in hand
            List<string> resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].hand, false);

            if (resultList.Count != 1)
                return;

            // Build a message and attach the card to show everyone
            string foundCard = resultList[0];
            string cardName = foundCard.Substring(0, foundCard.Length - 4);
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{ctx.Message.Author.Username} shows {cardName} to everyone.")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        [Command("sub")]
        public async Task SubCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("To subtract points from a player, specify the points to subtract followed by mentioning them.\n" +
                "Example:\n.sub 200 @Coolguy123");
            return;
        }

        [Command("sub")]
        public async Task SubCommand(CommandContext ctx, int points, DiscordMember player)
        {
            if (!playerList.ContainsKey(player.Id))
            {
                await ctx.RespondAsync("That player isn't currently playing.");
                return;
            }

            if (!gameStarted)
            {
                await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
                return;
            }

            if (points <= 0)
            {
                await ctx.RespondAsync("Please subtract a positive number of points.");
                return;
            }

            playerList[player.Id].points -= points;
            await ctx.RespondAsync($"{player.Mention} has lost {points} points!\nThey now have {playerList[player.Id].points}.");
        }

        [Command("use")]
        public async Task UseCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Use any card by placing it on the field. Specify the card name and field zone. Examples:\n" +
                ".use karate_ant monster\n" +
                ".use fire magic\n" +
                ".use stalemate center");
        }

        [Command("use")]
        public async Task UseCommand(CommandContext ctx, string cardSearch, string destination)
        {
            ulong playerID = ctx.Message.Author.Id;
            List<string> resultList = await SearchListForCard(ctx, cardSearch, playerList[playerID].hand, false);

            if (resultList.Count != 1)
                return;

            string foundCard = resultList[0];
            string cardName = TrimFileExtension(foundCard);

            string actionString = "";

            // Destination list as determined by destination argument
            List<string> destList = new List<string>();

            if (destination.ToLower() == "mon" || destination.ToLower() == "monster")
            {
                destList = playerList[playerID].fieldMonsters;
                actionString = $"{ctx.Message.Author.Username} summons {cardName} to the field.";
            }
            else if (destination.ToLower() == "mag" || destination.ToLower() == "magic")
            {
                destList = playerList[playerID].fieldSpells;
                actionString = $"{ctx.Message.Author.Username} activates {cardName}.";
            }
            else if (destination.ToLower() == "cen" || destination.ToLower() == "center")
            {
                destList = fieldCenter;
                actionString = $"{ctx.Message.Author.Username} activates {cardName} in the center.";
            }
            else
            {
                // Didn't enter a valid destination
                return;
            }

            // If destList already has 5 cards, cancel the move
            if (destList.Count >= 5)
            {
                await ctx.RespondAsync($"That field zone '{destination}' is already full.");
                return;
            }

            MoveCardToList(foundCard, playerList[playerID].hand, destList, true);

            // Append (ATK) to the card if it was sent to a monster zone
            if (destList == playerList[playerID].fieldMonsters)
            {
                playerList[playerID].fieldMonsters[playerList[playerID].fieldMonsters.Count - 1] = TrimFileExtension(playerList[playerID].fieldMonsters[playerList[playerID].fieldMonsters.Count - 1]);
                playerList[playerID].fieldMonsters[playerList[playerID].fieldMonsters.Count - 1] += " (ATK)";
            }

            actionString += $"\n{ctx.Message.Author.Username} now has {playerList[playerID].hand.Count} cards in hand.";

            await DMPlayerHand(playerID, $"You moved {cardName} to the field.\n");

            // Build a message and attach the card to show everyone
            using (var fs = new FileStream($"{cardDir}{foundCard}", FileMode.Open, FileAccess.Read))
            {
                var msg = await new DiscordMessageBuilder()
                    .WithContent($"{actionString}")
                    .WithFiles(new Dictionary<string, Stream>() { { foundCard, fs } })
                    .SendAsync(ctx.Channel);
            }
        }

        // Helpers
        void AssignDiscordMembersForDM(CommandContext ctx)
        {
            var memberList = ctx.Message.Channel.Guild.Members;
            foreach (var player in playerList)
            {
                foreach (var member in memberList)
                {
                    if (member.Value.Id == player.Value.id)
                    {
                        player.Value.discordMember = member.Value;
                        break;
                    }
                }
            }
        }

        void ResetGame()
        {
            // Clear discard pile
            discardPile.Clear();

            // Clear player hands, fields, and scores
            foreach (var player in playerList)
            {
                player.Value.hand.Clear();
                player.Value.fieldMonsters.Clear();
                player.Value.fieldSpells.Clear();
                player.Value.points = 0;
            }

            // Clear field center
            fieldCenter.Clear();
        }

        void GenerateNewDeck(int cardAmount)
        {
            // Clear the current deck
            deck.Clear();

            // Create a copy of the card list to pull cards from
            List<string> tempCardList = new List<string>(cardList);

            // Move cardAmount random cards from tempCardList into deck
            var random = new Random();
            for (int i = 0; i < cardAmount; i++)
            {
                int index = random.Next(tempCardList.Count);
                string cardName = tempCardList[index];

                // Move selected card from tempCardList to deck
                tempCardList.Remove(cardName);
                deck.Add(cardName);
            }
        }

        void ReloadCardList()
        {
            cardList.Clear();
            string[] cardFiles = Directory.GetFiles(cardDir);
            foreach (var card in cardFiles)
            {
                cardList.Add(card.Replace(cardDir, ""));
            }
        }

        void MoveCardToList(string cardName, List<string> sourceList, List<string> destList, bool removeFromSource)
        {
            destList.Add(cardName);
            if (removeFromSource)
            {
                sourceList.Remove(cardName);
            }
        }

        bool RequiredJoinedAndStarted(CommandContext ctx)
        {
            return true;
            //if (!await CheckPlayerJoined(ctx))
            //    return;

            //if (!gameStarted)
            //{
            //    await ctx.RespondAsync("The game hasn't started yet. Use .begin to start the game.");
            //    return;
            //}
        }

        string TrimFileExtension(string cardName)
        {
            string trimmedCardName = cardName.Substring(0, cardName.Length - 4);
            return trimmedCardName;
        }

        string TrimBattlePosition(string cardName)
        {
            string trimmedCardName = cardName.Substring(0, cardName.Length - 6);
            return trimmedCardName;
        }

        string ChangeBattlePosition(string oldMonsterName, string newPosition)
        {
            string newMonsterName = $"{oldMonsterName.Substring(0, oldMonsterName.Length - 6)}{newPosition}";
            return newMonsterName;
        }

        async Task<bool> CheckPlayerJoined(CommandContext ctx)
        {
            if (!playerList.ContainsKey(ctx.Message.Author.Id))
            {
                await ctx.RespondAsync("You haven't joined the game yet. Use .join to play.");
                return false;
            }

            return true;
        }

        async Task<List<string>> SearchListForCard(CommandContext ctx, string cardSearch, List<string> sourceList, bool suppressError)
        {
            // Search sourceList for cardName
            cardSearch = cardSearch.ToLower();
            string foundCard = "";
            var resultList = sourceList.FindAll(delegate (string s) { return s.Contains(cardSearch); });

            // If there is an exact match, pick that card, otherwise you literally can't view cards
            // like "karate" due to cards like "karate_ant" being returned in the results
            foreach (var result in resultList)
            {
                if (TrimFileExtension(result) == cardSearch)
                {
                    resultList.Clear();
                    resultList.Add(result);
                    break;
                }
            }

            if (resultList.Count > 1)
            {
                if (!suppressError)
                    await ctx.RespondAsync($"Your search of {cardSearch} returned more than 1 result. Try again.");
                return resultList;
            }

            foundCard += sourceList.FirstOrDefault(s => s.Contains(cardSearch));

            if (resultList.Count == 0)
            {
                if (!suppressError)
                    await ctx.RespondAsync($"Search for {cardSearch} returned 0 results.");
                return resultList;
            }

            return resultList;
        }

        async Task DMPlayerHand(ulong playerID, string prefix)
        {
            // Create string of hand cards
            Dictionary<string, Stream> handCards = new Dictionary<string, Stream>();
            string handString = "";
            int handSlot = 1;

            foreach (var card in playerList[playerID].hand)
            {
                var fs = new FileStream($"{cardDir}{card}", FileMode.Open, FileAccess.Read);
                handCards.Add(card, fs);
                string stripCard = card.Substring(0, card.Length - 4);
                handString += handSlot + ". " + stripCard + "\n";
                handSlot++;
            }
            //Console.WriteLine("Finished walking the player's hand.");

            // Grab a reference to the message auther with type DiscordMember and create a DM
            var dm = new DiscordDmChannel();
            dm = await playerList[playerID].discordMember.CreateDmChannelAsync();
            //Console.WriteLine("Finished creating a discord DM channel with the player.");

            var msg = await new DiscordMessageBuilder()
                .WithContent($"{prefix}Here is your current hand:\n{handString}")
                .WithFiles(handCards)
                .SendAsync(dm);
            //Console.WriteLine("Finished building and sending the DM with player hand.");
        }

        async Task DealStartingHand(CommandContext ctx)
        {
            // Walk through the player list, dealing them each a starting hand
            string dealString = "";
            int deckStartingSize = deck.Count;
            foreach (var player in playerList)
            {
                // Deal a random card 5 times
                for (int i = 0; i < 5; i++)
                {
                    var random = new Random();
                    int index = random.Next(deck.Count);
                    string cardName = deck[index];

                    // Remove drawn card from deck and add it to player's hand
                    deck.Remove(cardName);
                    player.Value.hand.Add(cardName);
                }

                dealString += $"Dealt 5 cards to {player.Value.discordUser.Username}.\n";

                // Build the hand string
                Dictionary<string, Stream> handCards = new Dictionary<string, Stream>();
                string handString = "";

                int handSlot = 1;
                foreach (var card in player.Value.hand)
                {
                    var fs = new FileStream($"{cardDir}{card}", FileMode.Open, FileAccess.Read);
                    handCards.Add(card, fs);
                    string stripCard = card.Substring(0, card.Length - 4);
                    handString += handSlot + ". " + stripCard + "\n";
                    handSlot++;
                }

                // DM
                await DMPlayerHand(player.Value.discordMember.Id, "The game has begun, and you have been dealt your starting hand.\n");
            }

            dealString += $"Deck now has {deck.Count} cards.";

            await ctx.RespondAsync($"You begin the game with {deckStartingSize} cards in the deck.\n" +
                $"{dealString}");
        }
    }
}