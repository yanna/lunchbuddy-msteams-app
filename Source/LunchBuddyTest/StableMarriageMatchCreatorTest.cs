using System.Collections.Generic;
using Icebreaker.Helpers;
using Icebreaker.Match;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LunchBuddyTest
{
    [TestClass]
    public class StableMarriageMatchCreatorTest
    {
        [TestMethod]
        public void TestCreateMatchesNoPeople()
        {
            var algo = new StableMarriageMatchCreator(new System.Random(), new Dictionary<string, PersonData>());
            var match = algo.CreateMatches(new List<ChannelAccount>());
            Assert.AreEqual(0, match.Pairs.Count);
        }

        [TestMethod]
        public void TestCreateMatchesOnePerson()
        {
            var algo = new StableMarriageMatchCreator(new System.Random(), new Dictionary<string, PersonData> { { "abc", new PersonData() { Discipline = "design" } } });
            var match = algo.CreateMatches(new List<ChannelAccount> { new TeamsChannelAccount { ObjectId = "abc" } });
            Assert.AreEqual(0, match.Pairs.Count);
            Assert.AreEqual("abc", match.OddPerson.GetUserId());
        }

        [TestMethod]
        public void TestCreateMatchesFivePeople()
        {
            var algo = new StableMarriageMatchCreator(new System.Random(12345), new Dictionary<string, PersonData> {
                { "aaa", new PersonData() { Discipline = "design" } },
                { "bbb", new PersonData() { Discipline = "engineering" } },
                { "eee", new PersonData() { Discipline = "engineering" } },
            });
            var match = algo.CreateMatches(new List<ChannelAccount> {
                new TeamsChannelAccount { ObjectId = "aaa" },
                new TeamsChannelAccount { ObjectId = "bbb" },
                new TeamsChannelAccount { ObjectId = "ccc" },
                new TeamsChannelAccount { ObjectId = "ddd" },
                new TeamsChannelAccount { ObjectId = "eee" },
            });

            // System.Random(12345) shuffles to: a, b, e, c, d
            Assert.AreEqual(2, match.Pairs.Count);
            Assert.AreEqual("ddd", match.OddPerson.GetUserId());

            Assert.AreEqual("ccc", match.Pairs[0].Item2.GetUserId());
            Assert.AreEqual("eee", match.Pairs[1].Item2.GetUserId());
        }
    }
}
