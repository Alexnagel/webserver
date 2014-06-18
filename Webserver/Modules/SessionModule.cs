using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Webserver.Enums;
using Webserver.Models;

namespace Webserver.Modules
{
    class SessionModule
    {
        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int HOURS_BLOCKED      = 2;
        private const int SESSION_HOURS      = 1;

        private MySqlModule                  _mySqlModule;
        private Dictionary<String, User>     _connectedSessions;
        private Dictionary<String, int>      _loginAttempts;
        private Dictionary<String, DateTime> _blockedIPs;
        private Timer                        _sessionCheckTimer;

        public SessionModule(MySqlModule mySqlModule)
        {
            this._mySqlModule   = mySqlModule;
            _connectedSessions  = new Dictionary<String, User>();
            _loginAttempts      = new Dictionary<String, int>();
            _blockedIPs         = new Dictionary<String, DateTime>();

            _sessionCheckTimer = new Timer(checkSessions);
            _sessionCheckTimer.Change(0, 600000);
        }

        public User LoginUser(String IPaddress, String username, String password, out Warning warning)
        {
            // Check if user has a session running
            if (_connectedSessions.ContainsKey(IPaddress))
            {
                warning = Warning.NONE;
                return _connectedSessions[IPaddress];
            }

            // Check if key is in loginattempts or blocked ip
            if (_loginAttempts.ContainsKey(IPaddress) && _loginAttempts[IPaddress] == MAX_LOGIN_ATTEMPTS)
            {
                _blockedIPs.Add(IPaddress, DateTime.Now);
                _loginAttempts.Remove(IPaddress);

                warning = Warning.IP_BLOCKED;
                return null;
            }

            // Check if IP is blocked
            if (_blockedIPs.ContainsKey(IPaddress))
            {
                TimeSpan ts = DateTime.Now.Subtract(_blockedIPs[IPaddress]);
                if (ts.TotalHours < HOURS_BLOCKED)
                {
                    warning = Warning.IP_BLOCKED;
                    return null;
                }
                else
                    _blockedIPs.Remove(IPaddress);
            }

            // Log in user
            User user = _mySqlModule.CheckUser(username, password);
            if (user == null)
            {
                if (_loginAttempts.ContainsKey(IPaddress))
                {
                    _loginAttempts[IPaddress] = _loginAttempts[IPaddress]++;
                }
                else
                    _loginAttempts.Add(IPaddress, 1);

                warning = Warning.INCORRECT_VALUES;
                return null;
            }
            else
            {
                if (_loginAttempts.ContainsKey(IPaddress))
                    _loginAttempts.Remove(IPaddress);

                _connectedSessions.Add(IPaddress, user);

                warning = Warning.NONE;
                return user;
            }
        }

        public void LogOutUser(String IPadress)
        {
            _connectedSessions.Remove(IPadress);
        }

        public User CheckIPSession(String IPaddress)
        {
            if (_connectedSessions.ContainsKey(IPaddress))
                return _connectedSessions[IPaddress];
            else
                return null;
        }

        private void checkSessions(object stateInfo)
        {
            foreach(KeyValuePair<String, User> session in _connectedSessions)
            {
                TimeSpan ts = DateTime.Now.Subtract(session.Value.TimeLoggedIn);
                if (ts.TotalHours > SESSION_HOURS)
                    _connectedSessions.Remove(session.Key);
            }
        }
    } 
}
