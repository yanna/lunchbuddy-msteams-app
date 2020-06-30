using Icebreaker.Helpers;
using Icebreaker.Match;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LunchBuddyTest
{
    [TestClass]
    public class PersonPreferencesTest
    {
        private List<Person<ChannelAccount>> group;
        private Dictionary<string, Person<ChannelAccount>> userIdToPerson;

        [TestInitialize]
        public void TestInitialize()
        {
            var superman = new Person<ChannelAccount>(new TeamsChannelAccount { Name = "Superman", ObjectId = "0" });
            var catwoman = new Person<ChannelAccount>(new TeamsChannelAccount { Name = "1Catwoman", ObjectId = "1" });
            var spiderman = new Person<ChannelAccount>(new TeamsChannelAccount { Name = "2Spiderman", ObjectId = "2" });
            var batman = new Person<ChannelAccount>(new TeamsChannelAccount { Name = "3Batman", ObjectId = "3" });

            this.group = new List<Person<ChannelAccount>>() { catwoman, spiderman, batman };
            this.userIdToPerson = group.ToDictionary(p => p.Data.GetUserId(), p => p);
        }

        [TestMethod]
        public void TestNoPeopleData()
        {
            var emptyPeopleData = new Dictionary<string, PersonData>();
            var prefs = new PersonPreferences("0", this.group, this.userIdToPerson, emptyPeopleData).Get();

            var actualNames = prefs.Select(person => person.Data.Name).ToList();
            var expectedNames = new List<string> { "1Catwoman", "2Spiderman", "3Batman" };

            CollectionAssert.AreEqual(expectedNames, actualNames);
        }

        [TestMethod]
        public void TestSameNoTeam()
        {
            // If both the source and the candidates have no team, they are not considered "on the same team"
            var sameTeamPeopleData = new Dictionary<string, PersonData>
            {
                { "1", new PersonData{ Teams = new List<string>{ "dc" } } }
            };

            var prefs = new PersonPreferences("0", this.group, this.userIdToPerson, sameTeamPeopleData).Get();

            var actualNames = prefs.Select(person => person.Data.Name).ToList();
            var expectedNames = new List<string> { "1Catwoman", "2Spiderman", "3Batman" };

            CollectionAssert.AreEqual(expectedNames, actualNames);
        }

        [TestMethod]
        public void TestSameTeam()
        {
            var sameTeamPeopleData = new Dictionary<string, PersonData>
            {
                { "0", new PersonData{ Teams = new List<string>{ "dc" } } },
                { "3", new PersonData{ Teams = new List<string>{ "dc", "gotham" } } }
            };

            var prefs = new PersonPreferences("0", this.group, this.userIdToPerson, sameTeamPeopleData).Get();

            var actualNames = prefs.Select(person => person.Data.Name).ToList();
            var expectedNames = new List<string> { "3Batman", "1Catwoman", "2Spiderman" };

            CollectionAssert.AreEqual(expectedNames, actualNames);
        }


        [TestMethod]
        public void TestPastMatches()
        {
            var sameTeamPeopleData = new Dictionary<string, PersonData>
            {
                { "0", new PersonData
                    {
                        Teams = new List<string>{ "dc" },
                        PastMatches = new List<PastMatch>{
                            new PastMatch("1", DateTime.Today.AddDays(-2)), 
                            new PastMatch("3", DateTime.Today.AddDays(-1)), 
                            new PastMatch("1", DateTime.Today) }
                    }
                },
                { "3", new PersonData
                    {
                        Teams = new List<string>{ "dc", "gotham" },
                    }
                },
                { "1", new PersonData
                    {
                        Teams = new List<string>{ "dc", "gotham" },
                        Seniority = "principal"
                    }
                }
            };

            var prefs = new PersonPreferences("0", this.group, this.userIdToPerson, sameTeamPeopleData).Get();

            var actualNames = prefs.Select(person => person.Data.Name).ToList();

            // No match first, then oldest match
            var expectedNames = new List<string> { "2Spiderman", "3Batman", "1Catwoman" };

            CollectionAssert.AreEqual(expectedNames, actualNames);
        }
    }
}
