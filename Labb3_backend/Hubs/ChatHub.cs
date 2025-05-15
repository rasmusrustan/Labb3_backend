using Labb3_backend.DataService;
using Labb3_backend.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Tasks;

namespace Labb3_backend.Hubs
{
    public class ChatHub : Hub
    {
        private readonly SharedDb _sharedDb;

        private static readonly ConcurrentDictionary<string, string> _gameWords = new();
        
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly ConcurrentDictionary<string, DateTime> _roundStartTimes = new();

        private static readonly TimeSpan RoundDuration = TimeSpan.FromSeconds(30);

        public ChatHub(SharedDb sharedDb, IHttpClientFactory httpClientFactory)
        {
            _sharedDb = sharedDb;
            _httpClientFactory = httpClientFactory;
        }

        public async Task JoinChatRoom(string userName, string chatRoom, string role)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoom);
            _sharedDb.Connection[Context.ConnectionId] = new UserConnection { UserName = userName, ChatRoom = chatRoom ,Role = role};

            await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "admin", $"{userName} has joined the chat room {chatRoom} ({role})");
        }

        public async Task SendMessage(string chatRoom, string userName, string message)
        {
            if(_sharedDb.Connection.TryGetValue(Context.ConnectionId, out var connection))
            {
                if(chatRoom == "announcement" && connection.Role != "teacher")
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "Sorry only teachers can write in this forum");
                    return;
                }
            }
            await Clients.Group(chatRoom).SendAsync("ReceiveMessage", userName, message);
        }

        public async Task GuessWord(string chatRoom, string userName, string guess)
        {

            if (_gameWords.TryGetValue(chatRoom, out var word))
            {
                if (guess.Equals(word, StringComparison.OrdinalIgnoreCase))
                {
                    // Stoppa spelet direkt
                    _gameWords.TryRemove(chatRoom, out _);

                    if (_roundStartTimes.TryRemove(chatRoom, out var startTime))
                    {
                        var timeTaken = DateTime.UtcNow - startTime;
                        var timeLeft = RoundDuration - timeTaken;
                        int score = Math.Max(10 + (int)timeLeft.TotalSeconds, 10);

                        if (_sharedDb.Connection.TryGetValue(Context.ConnectionId, out var user))
                        {
                            user.Points += score;
                        }

                        await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", $"{userName} guessed the word correctly! The word was '{word}'. Points: {score}");
                    }
                }
            }
        }
        public async Task UserReady(string chatRoom, string userName)
        {
            await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", $"{userName} är redo!");
        }

        public async Task StartGame(string chatRoom)
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://localhost:7255/words/random");

            if (response.IsSuccessStatusCode)
            {
                var word = await response.Content.ReadAsStringAsync();

                _gameWords[chatRoom] = word.Trim('"');
                _roundStartTimes[chatRoom] = DateTime.UtcNow;

                foreach (var conn in _sharedDb.Connection)
                {
                    if (conn.Value.ChatRoom == chatRoom && conn.Value.Role == "student")
                    {
                        await Clients.Client(conn.Key).SendAsync("ShowWord", word);
                    }
                }

                await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", "Game started! A new word has been chosen.");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(RoundDuration);


                    if (_gameWords.ContainsKey(chatRoom))
                    {
                        string answer = _gameWords[chatRoom];
                        _gameWords.TryRemove(chatRoom, out _);
                        _roundStartTimes.TryRemove(chatRoom, out _);

                        await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", $"Time's up! The correct word was '{answer}'");
                        _gameWords.TryRemove(chatRoom, out _);

                    }

                });

            }
            else
            {
                await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", "Failed to start game, could not fetch word.");
            }
        }

        public async Task SwitchRoles(string chatRoom)
        {
            var players = _sharedDb.Connection
                .Where(kvp => kvp.Value.ChatRoom == chatRoom)
                .Select(kvp => new { ConnId = kvp.Key, Info = kvp.Value })
                .ToList();

            if (players.Count == 2)
            {
                // Byt roller
                foreach (var player in players)
                {
                    player.Info.Role = player.Info.Role == "student" ? "teacher" : "student";
                    player.Info.RoundsPlayed += 1; // om du vill hålla koll på rundor
                }

                // Meddela roller
                foreach (var player in players)
                {
                    await Clients.Client(player.ConnId).SendAsync("ReceiveMessage", "System", $"Din roll är nu: {player.Info.Role}");
                }

                // Hämta nytt ord
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync("https://localhost:7255/words/random");

                if (response.IsSuccessStatusCode)
                {
                    var word = await response.Content.ReadAsStringAsync();
                    _gameWords[chatRoom] = word.Trim('"');
                    _roundStartTimes[chatRoom] = DateTime.UtcNow;

                    // Skicka ord till den nya "student"
                    foreach (var player in players)
                    {
                        if (player.Info.Role == "student")
                        {
                            await Clients.Client(player.ConnId).SendAsync("ShowWord", word);
                        }
                        await Clients.Client(player.ConnId).SendAsync("SetRole", player.Info.Role);

                    }

                    await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", "Game started! A new word has been chosen.");

                    // Starta timer
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(RoundDuration);

                        if (_gameWords.ContainsKey(chatRoom))
                        {
                            string answer = _gameWords[chatRoom];
                            _gameWords.TryRemove(chatRoom, out _);
                            _roundStartTimes.TryRemove(chatRoom, out _);

                            await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", $"Tiden är slut! Rätt ord var '{answer}'");
                        }
                    });
                }
                else
                {
                    await Clients.Group(chatRoom).SendAsync("ReceiveMessage", "System", "Kunde inte hämta nytt ord för ny runda.");
                }
            }
        }



    }
}
