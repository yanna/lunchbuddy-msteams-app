//----------------------------------------------------------------------------------------------
// <copyright file="StableMarriageAlgorithm.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The Gale-Shapley Stable Marriage Algorithm states that given N men and N women,
    /// where each person has ranked all members of the opposite sex in order of preference,
    /// marry the men and women together such that there are no two people of opposite sex who
    /// would both rather have each other than their current partners.
    /// If there are no such people, all the marriages are “stable”.
    ///
    ///  Note that the two groups are called male and female for the purposes of the algorithm only.
    /// It is not necessarily the case that everyone in female group is a female etc.
    /// </summary>
    public class StableMarriageAlgorithm
    {
        /// <summary>
        /// Perform the marriage match
        /// </summary>
        /// <param name="guys">Set of guys with preferences for each girl</param>
        /// <typeparam name="T">type of the data contained in person</typeparam>
        public static void DoMarriage<T>(IList<Person<T>> guys)
        {
            /*Gale-Shapley Stable Marriage algorithm
                https://en.wikipedia.org/wiki/Stable_marriage_problem
                function stableMatching {
                    Initialize all m ∈ M and w ∈ W to free
                    while ∃ free man m who still has a woman w to propose to {
                    w = first woman on m’s list to whom m has not yet proposed
                    if w is free
                        (m, w) become engaged
                    else some pair (m', w) already exists
                        if w prefers m to m'
                            m' becomes free
                            (m, w) become engaged
                        else
                            (m', w) remain engaged
                }
            */

            int freeGuysCount = guys.Count;
            while (freeGuysCount > 0)
            {
                var freeGuy = guys.FirstOrDefault(guy => guy.Fiance == null);
                if (freeGuy == null)
                {
                    break;
                }

                Person<T> gal = freeGuy.NextCandidateNotYetProposedTo();
                if (gal == null)
                {
                    break;
                }

                if (gal.Fiance == null)
                {
                    freeGuy.EngageTo(gal);
                    freeGuysCount--;
                }
                else if (gal.Prefers(freeGuy))
                {
                    freeGuy.EngageTo(gal);
                }
            }
        }
    }
}