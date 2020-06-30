using System.Collections.Generic;
using System.Text;
using Icebreaker.Helpers;
using Icebreaker.Match;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LunchBuddyTest
{
    [TestClass]
    public class StableMarriageAlgorithmTest
    {
        [TestMethod]
        public void TestMatchingAlgorithm()
        {
            var a = new Person<string>("a");
            var b = new Person<string>("b");
            var c = new Person<string>("c");
            var d = new Person<string>("d");
            var e = new Person<string>("e");
            var f = new Person<string>("f");
            var g = new Person<string>("g");
            var h = new Person<string>("h");
            var i = new Person<string>("i");
            var j = new Person<string>("j");

            var f1 = new Person<string>("f1");
            var f2 = new Person<string>("f2");
            var f3 = new Person<string>("f3");
            var f4 = new Person<string>("f4");
            var f5 = new Person<string>("f5");
            var f6 = new Person<string>("f6");
            var f7 = new Person<string>("f7");
            var f8 = new Person<string>("f8");
            var f9 = new Person<string>("f9");
            var f10 = new Person<string>("f10");

            a.Preferences = new List<Person<string>>() { f1, f5, f3, f9, f10, f4, f6, f2, f8, f7 };
            b.Preferences = new List<Person<string>>() { f3, f8, f1, f4, f5, f6, f2, f10, f9, f7 };
            c.Preferences = new List<Person<string>>() { f8, f5, f1, f4, f2, f6, f9, f7, f3, f10 };
            d.Preferences = new List<Person<string>>() { f9, f6, f4, f7, f8, f5, f10, f2, f3, f1 };
            e.Preferences = new List<Person<string>>() { f10, f4, f2, f3, f6, f5, f1, f9, f8, f7 };
            f.Preferences = new List<Person<string>>() { f2, f1, f4, f7, f5, f9, f3, f10, f8, f6 };
            g.Preferences = new List<Person<string>>() { f7, f5, f9, f2, f3, f1, f4, f8, f10, f6 };
            h.Preferences = new List<Person<string>>() { f1, f5, f8, f6, f9, f3, f10, f2, f7, f4 };
            i.Preferences = new List<Person<string>>() { f8, f3, f4, f7, f2, f1, f6, f9, f10, f5 };
            j.Preferences = new List<Person<string>>() { f1, f6, f10, f7, f5, f2, f4, f3, f9, f8 };

            f1.Preferences = new List<Person<string>>() { b, f, j, g, i, a, d, e, c, h };
            f2.Preferences = new List<Person<string>>() { b, a, c, f, g, d, i, e, j, h };
            f3.Preferences = new List<Person<string>>() { f, b, e, g, h, c, i, a, d, j };
            f4.Preferences = new List<Person<string>>() { f, j, c, a, i, h, g, d, b, e };
            f5.Preferences = new List<Person<string>>() { j, h, f, d, a, g, c, e, i, b };
            f6.Preferences = new List<Person<string>>() { b, a, e, i, j, d, f, g, c, h };
            f7.Preferences = new List<Person<string>>() { j, g, h, f, b, a, c, e, d, i };
            f8.Preferences = new List<Person<string>>() { g, j, b, a, i, d, h, e, c, f };
            f9.Preferences = new List<Person<string>>() { i, c, h, g, f, b, a, e, j, d };
            f10.Preferences = new List<Person<string>>() { e, h, g, a, b, j, c, i, f, d };

            var guys = new List<Person<string>>(f1.Preferences);
            StableMarriageAlgorithm.DoMarriage<string>(guys);

            StringBuilder actualMatches = new StringBuilder();
            foreach (Person<string> guy in guys)
            {
                actualMatches.AppendLine(string.Format("{0} is engaged to {1}", guy.Data, guy.Fiance.Data));
            }

            var expectedMatches =
@"b is engaged to f3
f is engaged to f2
j is engaged to f1
g is engaged to f7
i is engaged to f8
a is engaged to f9
d is engaged to f6
e is engaged to f10
c is engaged to f4
h is engaged to f5
";
            Assert.AreEqual(expectedMatches, actualMatches.ToString());
        }

        [TestMethod]
        public void TestCreateMatchesNoPeople()
        {
            var algo = new StableMarriageAlgorithm(new System.Random(), new Dictionary<string, PersonData>());
            var match = algo.CreateMatches(new List<ChannelAccount>());
            Assert.AreEqual(0, match.Pairs.Count);
        }

        [TestMethod]
        public void TestCreateMatchesOnePerson()
        {
            var algo = new StableMarriageAlgorithm(new System.Random(), new Dictionary<string, PersonData> { { "abc", new PersonData() { Discipline = "design" } } });
            var match = algo.CreateMatches(new List<ChannelAccount> { new TeamsChannelAccount { ObjectId = "abc" } });
            Assert.AreEqual(0, match.Pairs.Count);
            Assert.AreEqual("abc", match.OddPerson.GetUserId());
        }

        [TestMethod]
        public void TestCreateMatchesFivePeople()
        {
            var algo = new StableMarriageAlgorithm(new System.Random(12345), new Dictionary<string, PersonData> { 
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
