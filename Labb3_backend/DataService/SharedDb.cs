using System.Collections.Concurrent;
using Labb3_backend.Models;

namespace Labb3_backend.DataService
{
    public class SharedDb
    {
        private readonly ConcurrentDictionary<string, UserConnection> _connection = new();

        public ConcurrentDictionary<string, UserConnection> Connection => _connection;
    }
}
