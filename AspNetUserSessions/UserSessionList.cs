using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace eDM.Utils
{
    public class UserSessionList
    {
        private readonly object _lockList = new object();
        private readonly ReaderWriterLockSlim _lockProps = new ReaderWriterLockSlim();
        private readonly Dictionary<string, UserSession> _list = new Dictionary<string, UserSession>();

        private TimeSpan _expireAfterMinutes = TimeSpan.FromMinutes(30);  //30 minustes is the default timeout for forms auth

        public TimeSpan ExpireAfterMinutes
        {
            //We use a simple lock here considering that this property will be seldom written (normally only at startup)
            //and concurrent reading is not a typical scenario either.
            get
            {
                _lockProps.EnterReadLock();
                try
                {
                    return _expireAfterMinutes;
                }
                finally
                {
                    _lockProps.ExitReadLock();
                }
            }
            set
            {
                _lockProps.ExitWriteLock();
                try
                {
                    _expireAfterMinutes = value;
                }
                finally
                {
                    _lockProps.EnterWriteLock();
                }
            }
        }

        /// <summary>
        /// Adds a new entry into the list, it expects that the list is already locked
        /// </summary>
        private void Add(string sessionID, string userID, string userName, DateTime lastActive, DateTime? loginDate, string clientHost, string url)
        {
            _list.Add(sessionID, new UserSession()
            {
                SessionID = sessionID,
                LastActive = lastActive,
                LoginDate = loginDate,
                UserID = userID,
                UserName = userName,
                ClientHost = clientHost,
                Url = url
            });
        }

        /// <summary>
        /// Adds or updates an existing entry. This is supposed to be called when user legs into the application..
        /// </summary>
        /// <param name="sessionID">Session ID</param>
        /// <param name="userID">Unique user ID (login name or an ID from database)</param>
        /// <param name="userName">User's name</param>
        /// <param name="loginDate">Login date, can be null</param>
        /// <param name="clientHost">Optional param, the cleint's url or hostname. If null (default), the appropriate field in the session object is not updated</param>
        /// <param name="url">Optional param, if null (default), the appropriate field in the session object is not updated</param>
        public void AddOrUpdate(string sessionID, string userID, string userName, DateTime? loginDate, string clientHost = null, string url = null)
        {
            DateTime now = DateTime.Now;

            lock (_lockList)
            {
                if (_list.TryGetValue(sessionID, out UserSession session))
                {
                    session.UserID = userID;
                    session.UserName = userName;
                    session.LastActive = now;
                    session.LoginDate = loginDate;
                    if (clientHost != null)
                        session.ClientHost = clientHost;
                    if (url != null)
                        session.Url = url;
                }
                else
                {
                    Add(sessionID, userID, userName, now, loginDate, clientHost, url);
                }
            }
        }

        /// <summary>
        /// Adds or updates an existing entry. This is supposed to be called regularly when This is the regularly called function
        /// </summary>
        /// <param name="sessionID">Session ID</param>
        /// <param name="clientHost">Optional param, if null (default), the appropriate field in the session object is not updated</param>
        /// <param name="url">Optional param, if null (default), the appropriate field in the session object is not updated</param>
        public void UpdateLastActive(string sessionID, string clientHost = null, string url = null)
        {
            DateTime now = DateTime.Now;

            lock (_lockList)
            {
                if (_list.TryGetValue(sessionID, out UserSession session))
                {
                    session.LastActive = now;
                    if (clientHost != null)
                        session.ClientHost = clientHost;
                    if (url != null)
                        session.Url = url;
                }
                else
                {
                    Add(sessionID, "", "", now, null, clientHost, url);
                }
            }
        }

        /// <summary>
        /// Removes the session from the list. Supposed to be called when the session is destroyed.
        /// </summary>
        /// <param name="sessionID">Session ID</param>
        public void Remove(string sessionID)
        {
            lock (_lockList)
            {
                _list.Remove(sessionID);
            }
        }

        /// <summary>
        /// Cleans session entries that has not been active for ExpireAfterMinutes
        /// </summary>
        private void Clean()
        {
            var cutoffTime = DateTime.Now - ExpireAfterMinutes;
            lock (_lockList)
            {
                var expiredSessions = _list.Where(entry => entry.Value.LastActive < cutoffTime).ToList();  //.Select(au => au.Key).ToList();
                foreach (var session in expiredSessions)
                {
                    _list.Remove(session.Key);
                }
            }
        }

        /// <summary>
        /// Returns the list of sessions
        /// </summary>
        /// <param name="skipCleanExpired">If true (default: false), expires sessions are not cleaned from the list before returning it</param>
        /// <returns></returns>
        public List<UserSession> GetSessions(bool skipCleanExpired = false)
        {
            if (!skipCleanExpired)
                Clean();

            var result = new List<UserSession>();
            lock (_lockList)
            {
                result.AddRange(_list.Select(entry => entry.Value.Duplicate()));
            }
            return result;
        }

        /// <summary>
        /// Loads session into the internal list. Useful when the application starts up and previous sessions should be loaded (e.g.: from a database)
        /// </summary>
        /// <param name="sessionsToLoad"></param>
        public void LoadSessions(List<UserSession> sessionsToLoad)
        {
            lock (_lockList)
            {
                foreach (var sess in sessionsToLoad)
                {
                    if (_list.TryGetValue(sess.SessionID, out UserSession listSession))
                    {
                        if (String.IsNullOrEmpty(listSession.UserID))
                            listSession.UserID = sess.UserID;
                        if (String.IsNullOrEmpty(listSession.UserName))
                            listSession.UserID = sess.UserName;
                        if (listSession.LoginDate == null)
                            listSession.LoginDate = sess.LoginDate;
                        if (String.IsNullOrEmpty(listSession.ClientHost))
                            listSession.UserID = sess.ClientHost;

                        if (listSession.LastActive < sess.LastActive)
                        {
                            listSession.LastActive = sess.LastActive;
                            listSession.Url = sess.Url;
                        }
                    }
                    else
                    {
                        _list.Add(sess.SessionID, sess.Duplicate());
                    }
                }
            }
        }
    }

    public class UserSession
    {
        /// <summary>
        /// Login name or any other unique identifier
        /// </summary>
        public string SessionID { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public DateTime LastActive { get; set; }
        public DateTime? LoginDate { get; set; }
        public string ClientHost { get; set; }
        public string Url { get; set; }

        public UserSession Duplicate()
        {
            return new UserSession()
            {
                SessionID = this.SessionID,
                LastActive = this.LastActive,
                LoginDate = this.LoginDate,
                UserID = this.UserID,
                UserName = this.UserName,
                ClientHost = this.ClientHost,
                Url = this.Url
            };
        }
    }
}