namespace Labb3_backend.Models
{
    public class UserConnection
    {
        public string UserName { get; set; } = string.Empty;
        public string ChatRoom { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsDrawer => Role == "student";
        public int Points { get; set; } = 0;

        public int RoundsPlayed { get; set; } = 0;
        public bool IsReady { get; set; } = false;
    }

}
