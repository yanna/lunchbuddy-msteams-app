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
    /// <typeparam name="T">Type of the data that is contained</typeparam>
    public class Person<T>
    {
        private int candidateIndex = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Person{T}"/> class.
        /// </summary>
        /// <param name="data">extra data to be stored</param>
        public Person(T data)
        {
            this.Data = data;
        }

        /// <summary>
        /// Gets or sets the person's preferences.
        /// The first ones in the list will be more preferred than the later ones.
        /// </summary>
        public List<Person<T>> Preferences { get; set; } = new List<Person<T>>();

        /// <summary>
        /// Gets or sets the fiance
        /// </summary>
        public Person<T> Fiance { get; set; }

        /// <summary>
        /// Gets the custom data the person stores
        /// </summary>
        public T Data { get; private set; }

        /// <summary>
        /// Whether the given person is more preferred than the fiance
        /// </summary>
        /// <param name="person">person</param>
        /// <returns>true or false</returns>
        public bool Prefers(Person<T> person)
        {
            return this.Preferences.FindIndex(o => o == person) < this.Preferences.FindIndex(o => o == this.Fiance);
        }

        /// <summary>
        /// Next candidate that's less preferred than the current one.
        /// </summary>
        /// <returns>next candidate</returns>
        public Person<T> NextCandidateNotYetProposedTo()
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
        public void EngageTo(Person<T> person)
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