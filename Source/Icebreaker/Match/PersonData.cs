//----------------------------------------------------------------------------------------------
// <copyright file="PersonData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;

    /// <summary>
    /// Data about a person including past matches, discipline etc.
    /// </summary>
    public class PersonData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PersonData"/> class.
        /// </summary>
        public PersonData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonData"/> class.
        /// </summary>
        /// <param name="pastMatches">past matches</param>
        /// <param name="discipline">data/engineering/design/pm</param>
        /// <param name="gender">gender</param>
        /// <param name="seniority">intern/senior/principal/leadership/other</param>
        /// <param name="teams">sub team names</param>
        public PersonData(List<PastMatch> pastMatches, string discipline, string gender, string seniority, List<string> teams)
        {
            this.PastMatches = pastMatches;
            this.Discipline = discipline;
            this.Gender = gender;
            this.Seniority = seniority;
            this.Teams = teams;
        }

        /// <summary>
        /// Gets the list of past matches
        /// </summary>
        public List<PastMatch> PastMatches { get; private set; } = new List<PastMatch>();

        /// <summary>
        /// Gets the Area of discipline
        /// </summary>
        public string Discipline { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Gender
        /// </summary>
        public string Gender { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the seniority of the person [intern, senior, principal, leadership, other]
        /// </summary>
        public string Seniority { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the team names the person belongs to.
        /// </summary>
        public List<string> Teams { get; private set; } = new List<string>();
    }
}