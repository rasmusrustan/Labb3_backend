using Labb3_backend.DataService;
using Labb3_backend.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Labb3_backend.Hubs
{
    public class ChatHub : Hub
    {
        private readonly SharedDb _sharedDb;

        public ChatHub(SharedDb sharedDb)
        {
            _sharedDb = sharedDb;
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
    }
}
