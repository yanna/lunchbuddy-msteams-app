//----------------------------------------------------------------------------------------------
// <copyright file="PersonData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;
    using System.Linq;

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
        /// <param name="userId">User AAD id of the user</param>
        /// <param name="name">name of user to be compared with lowPreferenceNames</param>
        public PersonData(string userId, string name)
        {
            this.UserId = userId;
            this.Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonData"/> class.
        /// </summary>
        /// <param name="userId">User AAD id of the user</param>
        /// <param name="name">name of user to be compared with lowPreferenceNames</param>
        /// <param name="pastMatches">past matches</param>
        /// <param name="discipline">data/engineering/design/pm</param>
        /// <param name="gender">gender</param>
        /// <param name="seniority">e.g intern</param>
        /// <param name="teams">sub team names</param>
        /// <param name="lowPreferenceNames">Full names of people the user has low preference for</param>
        public PersonData(string userId, string name, List<PastMatch> pastMatches, string discipline, string gender, string seniority, List<string> teams, List<string> lowPreferenceNames)
        {
            this.UserId = userId;
            this.Name = name;
            this.PastMatches = pastMatches;
            this.Discipline = discipline;
            this.Gender = gender;
            this.Seniority = seniority;
            this.Teams = teams;
            this.LowPreferenceNames = lowPreferenceNames;
        }

        /// <summary>
        /// Gets or sets the user AAD id. Should not be empty.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name to be used to compare for LowPreferenceNames. Should not be empty.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of past matches
        /// </summary>
        public List<PastMatch> PastMatches { get; set; } = new List<PastMatch>();

        /// <summary>
        /// Gets or sets the Area of discipline
        /// </summary>
        public string Discipline { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Gender
        /// </summary>
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the seniority of the person eg intern
        /// </summary>
        public string Seniority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the team names the person belongs to.
        /// </summary>
        public List<string> Teams { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the full name of people the person doesn't want to match with
        /// </summary>
        public List<string> LowPreferenceNames { get; set; } = new List<string>();

        /// <summary>
        /// Gets the team names the person belongs to (in lower case)
        /// </summary>
        /// <returns>lower case team names</returns>
        public List<string> GetTeamsInLowerCase() => this.Teams.Select(t => t.ToLowerInvariant()).ToList();

        /// <summary>
        /// Gets or sets the full name of people the person doesn't want to match with (in lower case)
        /// </summary>
        /// <returns>lower case names</returns>
        public List<string> GetLowPreferenceNamesInLowerCase() => this.LowPreferenceNames.Select(n => n.ToLowerInvariant()).ToList();
    }
}