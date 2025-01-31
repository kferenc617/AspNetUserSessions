using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using eDM.Utils;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;

namespace Tests
{
    [TestClass]
    public class TestUserSessionList
    {
        private static readonly int C_TIMEOUT_MINUTES = 10;

        private static UserSessionList _userSessionList;

        [ClassInitialize()]
        public static void ClassInit(TestContext _)
        {
            _userSessionList = new UserSessionList
            {
                ExpireAfterMinutes = TimeSpan.FromMinutes(C_TIMEOUT_MINUTES)
            };
        }


        [TestInitialize]
        public void TestInit()
        {
            _userSessionList.Clear();
        }


        [TestMethod]
        public void TestUpdateLastActive()
        {
            _userSessionList.UpdateLastActive("10001", "192.168.0.1");
            _userSessionList.UpdateLastActive("12345", "192.168.0.123");
            Thread.Sleep(1000);
            _userSessionList.UpdateLastActive("12345", "192.168.0.123");

            var list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);

            var noItem = list.Find((item) => item.ClientHost == "192.168.0.0");
            Assert.IsNull(noItem);

            var oldItem = list.Find((item) => item.ClientHost == "192.168.0.1");
            var newerItem = list.Find((item) => item.ClientHost == "192.168.0.123");
            Assert.IsNotNull(oldItem);
            Assert.IsNotNull(newerItem);

            Assert.IsTrue((newerItem.LastActive - oldItem.LastActive).TotalSeconds > 1.0);

            _userSessionList.UpdateLastActive("12345", "home-nb.vpn.acme.org", "Login.aspx");
            list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);
            newerItem = list.Find((item) => item.SessionID == "12345");
            Assert.IsNotNull(newerItem);
            Assert.AreEqual("home-nb.vpn.acme.org", newerItem.ClientHost);
            Assert.AreEqual("Login.aspx", newerItem.Url);
        }

        [TestMethod]
        public void TestRemove()
        {
            _userSessionList.UpdateLastActive("1", "host01.acme.org");
            _userSessionList.UpdateLastActive("2", "host02.acme.org");

            var list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);

            _userSessionList.Remove("2");
            list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(1, list.Count);

            _userSessionList.Clear();
            list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void TestClean()
        {
            var toLoad = new List<UserSession>
            {
                new UserSession() { SessionID = "aaa", LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES + 1) },
                new UserSession() { SessionID = "bbb", LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES - 5) }
            };
            _userSessionList.LoadSessions(toLoad);

            var list = _userSessionList.GetSessions(true);  //skip clean, must return all loaded
            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);

            list = _userSessionList.GetSessions();  //get again with default param, "aaa" must be cleaned
            Assert.IsNotNull(list);
            Assert.AreEqual(1, list.Count);

            var remainingItem = list.Find((item) => item.SessionID == "bbb");
            Assert.IsNotNull(remainingItem);
        }

        [TestMethod]
        public void TestLoad()
        {
            _userSessionList.UpdateLastActive("veryold", "host01.acme.org", "Login");

            var toLoad = new List<UserSession>
            {
                new UserSession() { SessionID = "veryold", LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES + 1), UserID = "user1", UserName = "Jane Doe" },
                new UserSession() { SessionID = "new", LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES - 5), UserID = "user2", UserName = "John Doe" }
            };
            _userSessionList.LoadSessions(toLoad);

            var list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);  //Clean should not remove "veryold"

            var veryOldItem = list.Find((item) => item.SessionID == "veryold");
            Assert.IsNotNull(veryOldItem);
            //fields must have been merged
            Assert.AreEqual("host01.acme.org", veryOldItem.ClientHost);
            Assert.AreEqual("Login", veryOldItem.Url);
            Assert.AreEqual("user1", veryOldItem.UserID);
            Assert.AreEqual("Jane Doe", veryOldItem.UserName);

            //Test merging ClientHost
            _userSessionList.Clear();
            toLoad = new List<UserSession>
            {
                new UserSession()
                {
                    SessionID = "veryold",
                    LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES + 10),
                    Url = "old.aspx",
                    UserID = "user1",
                    UserName = "Jane Doe"
                }
            };
            _userSessionList.LoadSessions(toLoad);

            toLoad = new List<UserSession>
            {
                new UserSession()
                {
                    SessionID = "veryold",
                    LastActive = DateTime.Now - TimeSpan.FromMinutes(5),
                    Url = "new.aspx" ,
                    ClientHost = "newhost"
                }
            };
            _userSessionList.LoadSessions(toLoad);

            list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(1, list.Count);

            veryOldItem = list.Find((item) => item.SessionID == "veryold");
            Assert.IsNotNull(veryOldItem);
            //fields must have been merged
            Assert.AreEqual("newhost", veryOldItem.ClientHost);
            Assert.AreEqual("new.aspx", veryOldItem.Url);
            Assert.AreEqual("user1", veryOldItem.UserID);
            Assert.AreEqual("Jane Doe", veryOldItem.UserName);
        }

        [TestMethod]
        public void TestAddOrUpdate()
        {
            var toLoad = new List<UserSession>
            {
                new UserSession()
                {
                    SessionID = "notsoold",
                    LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES - 8),
                    UserID = "user1",
                    UserName = "Jane Doe"
                },
                new UserSession()
                {
                    SessionID = "old",
                    LastActive = DateTime.Now - TimeSpan.FromMinutes(C_TIMEOUT_MINUTES - 15)
                }
            };
            _userSessionList.LoadSessions(toLoad);

            _userSessionList.AddOrUpdate("new", "", "", null, "notebook1.lan", "default.aspx");
            _userSessionList.AddOrUpdate("old", "john.doe", "John Doe", DateTime.Now, "destop1.lan", "Login.aspx");

            var list = _userSessionList.GetSessions();
            Assert.IsNotNull(list);
            Assert.AreEqual(3, list.Count);

            var newItem = list.Find((item) => item.SessionID == "new");
            Assert.IsNotNull(newItem);
            //new item, most of the fields are emtpy
            Assert.AreEqual("", newItem.UserID);
            Assert.AreEqual("", newItem.UserName);
            Assert.AreEqual("notebook1.lan", newItem.ClientHost);
            Assert.AreEqual("default.aspx", newItem.Url);

            //fields must have been merged
            var oldItem = list.Find((item) => item.SessionID == "old");
            Assert.IsNotNull(oldItem);
            //new item, most of the fields are emtpy
            Assert.AreEqual("john.doe", oldItem.UserID);
            Assert.AreEqual("John Doe", oldItem.UserName);
            Assert.IsNotNull(oldItem.LoginDate);
            Assert.AreEqual("destop1.lan", oldItem.ClientHost);
            Assert.AreEqual("Login.aspx", oldItem.Url);
        }
    }
}
