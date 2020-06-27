//----------------------------------------------------------------------------------------------
// <copyright file="Person.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;

    /// <summary>
    /// Person for the stable marriage algorithm
    /// </summary>
    public class Person
    {
        private int candidateIndex = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Person"/> class.
        /// </summary>
        /// <param name="name">name</param>
        public Person(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets or sets the person's preferences.
        /// The first ones in the list will be more preferred than the later ones.
        /// </summary>
        public List<Person> Preferences { get; set; } = new List<Person>();

        /// <summary>
        /// Gets or sets the fiance
        /// </summary>
        public Person Fiance { get; set; }

        /// <summary>
        /// Whether the given person is more preferred than the fiance
        /// </summary>
        /// <param name="person">person</param>
        /// <returns>true or false</returns>
        public bool Prefers(Person person)
        {
            return this.Preferences.FindIndex(o => o == person) < this.Preferences.FindIndex(o => o == this.Fiance);
        }

        /// <summary>
        /// Next candidate that's less preferred than the current one.
        /// </summary>
        /// <returns>next candidate</returns>
        public Person NextCandidateNotYetProposedTo()
        {
            if (this.candidateIndex >= this.Preferences.Count)
            {
                return null;
            }

            return this.Preferences[this.candidateIndex++];
        }

        /// <summary>
        /// Make the current person engaged to given person and vice versa
        /// </summary>
        /// <param name="person">Person to be enagaged to</param>
        public void EngageTo(Person person)
        {
            if (person.Fiance != null)
            {
                person.Fiance.Fiance = null;
            }

            person.Fiance = this;

            if (this.Fiance != null)
            {
                this.Fiance.Fiance = null;
            }

            this.Fiance = person;
        }
    }
}