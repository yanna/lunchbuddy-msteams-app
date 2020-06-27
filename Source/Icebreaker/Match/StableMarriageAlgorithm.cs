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
    /// The Gale-Shapley Stable Marriage Algorithm matches a set of men to
    /// a set of girls given each person's preference for each other person.
    /// </summary>
    public class StableMarriageAlgorithm
    {
        /// <summary>
        /// Perform the marraige match
        /// </summary>
        /// <param name="guys">Set of guys with preferences for each girl</param>
        public static void DoMarriage(IList<Person> guys)
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

                Person gal = freeGuy.NextCandidateNotYetProposedTo();
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