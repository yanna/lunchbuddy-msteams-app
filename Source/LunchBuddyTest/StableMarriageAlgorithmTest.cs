using System;
using System.Collections.Generic;
using System.Text;
using Icebreaker.Match;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LunchBuddyTest
{
    [TestClass]
    public class StableMarriageAlgorithmTest
    {
        [TestMethod]
        public void TestMatchingAlgorithm()
        {
            Person a = new Person("a");
            Person b = new Person("b");
            Person c = new Person("c");
            Person d = new Person("d");
            Person e = new Person("e");
            Person f = new Person("f");
            Person g = new Person("g");
            Person h = new Person("h");
            Person i = new Person("i");
            Person j = new Person("j");

            Person f1 = new Person("f1");
            Person f2 = new Person("f2");
            Person f3 = new Person("f3");
            Person f4 = new Person("f4");
            Person f5 = new Person("f5");
            Person f6 = new Person("f6");
            Person f7 = new Person("f7");
            Person f8 = new Person("f8");
            Person f9 = new Person("f9");
            Person f10 = new Person("f10");

            a.Preferences = new List<Person>() { f1, f5, f3, f9, f10, f4, f6, f2, f8, f7 };
            b.Preferences = new List<Person>() { f3, f8, f1, f4, f5, f6, f2, f10, f9, f7 };
            c.Preferences = new List<Person>() { f8, f5, f1, f4, f2, f6, f9, f7, f3, f10 };
            d.Preferences = new List<Person>() { f9, f6, f4, f7, f8, f5, f10, f2, f3, f1 };
            e.Preferences = new List<Person>() { f10, f4, f2, f3, f6, f5, f1, f9, f8, f7 };
            f.Preferences = new List<Person>() { f2, f1, f4, f7, f5, f9, f3, f10, f8, f6 };
            g.Preferences = new List<Person>() { f7, f5, f9, f2, f3, f1, f4, f8, f10, f6 };
            h.Preferences = new List<Person>() { f1, f5, f8, f6, f9, f3, f10, f2, f7, f4 };
            i.Preferences = new List<Person>() { f8, f3, f4, f7, f2, f1, f6, f9, f10, f5 };
            j.Preferences = new List<Person>() { f1, f6, f10, f7, f5, f2, f4, f3, f9, f8 };

            f1.Preferences = new List<Person>() { b, f, j, g, i, a, d, e, c, h };
            f2.Preferences = new List<Person>() { b, a, c, f, g, d, i, e, j, h };
            f3.Preferences = new List<Person>() { f, b, e, g, h, c, i, a, d, j };
            f4.Preferences = new List<Person>() { f, j, c, a, i, h, g, d, b, e };
            f5.Preferences = new List<Person>() { j, h, f, d, a, g, c, e, i, b };
            f6.Preferences = new List<Person>() { b, a, e, i, j, d, f, g, c, h };
            f7.Preferences = new List<Person>() { j, g, h, f, b, a, c, e, d, i };
            f8.Preferences = new List<Person>() { g, j, b, a, i, d, h, e, c, f };
            f9.Preferences = new List<Person>() { i, c, h, g, f, b, a, e, j, d };
            f10.Preferences = new List<Person>() { e, h, g, a, b, j, c, i, f, d };

            List<Person> guys = new List<Person>(f1.Preferences);
            StableMarriageAlgorithm.DoMarriage(guys);

            StringBuilder actualMatches = new StringBuilder();
            foreach (Person guy in guys)
            {
                actualMatches.AppendLine(string.Format("{0} is engaged to {1}", guy.Name, guy.Fiance.Name));
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
    }
}
