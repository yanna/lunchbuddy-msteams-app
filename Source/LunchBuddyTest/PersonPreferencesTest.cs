using Icebreaker.Match;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace LunchBuddyTest
{
    [TestClass]
    public class PersonPreferencesTest
    {
        [TestMethod]
        public void TestNoPeopleData()
        {
            string userId = "Batman";
            var superman = new Person<ChannelAccount>(new TeamsChannelAccount() { Name = "Superman", ObjectId = "0" });
            var catwoman = new Person<ChannelAccount>(new TeamsChannelAccount() { Name = "Catwoman", ObjectId = "1" });
            var spiderman = new Person<ChannelAccount>(new TeamsChannelAccount() { Name = "Spiderman", ObjectId = "2" });
            var group = new List<Person<ChannelAccount>>() { superman, catwoman, spiderman };
            var userIdToPerson = group.ToDictionary(p => p.GetUserId(), p => p);
            var peopleData = new Dictionary<string, PersonData>();

            var prefs = new PersonPreferences(userId, group, userIdToPerson, peopleData).Get();
            var actualNames = prefs.Select(person => person.Data.Name).ToList();
            var expectedNames = new List<string> { "Superman", "Catwoman", "Spiderman" };

            CollectionAssert.AreEqual(expectedNames, actualNames);
        }
    }
}
