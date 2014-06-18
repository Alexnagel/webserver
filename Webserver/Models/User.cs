using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webserver.Enums;

namespace Webserver.Models
{
    public class User
    {
        private readonly int _id;
        private readonly String _username;
        private readonly UserRights _userRights;
        private readonly DateTime _timeLoggedIn;

        public User(int id, String username, UserRights userRights, DateTime timeLoggedIn)
        {
            _id = id;
            _username = username;
            _userRights = userRights;
            _timeLoggedIn = timeLoggedIn;
        }

        public int ID { get { return _id; } }
        public String Username { get { return _username; } }
        public UserRights UserRight { get { return _userRights; } }
        public DateTime TimeLoggedIn { get { return _timeLoggedIn; } }
    }
}
