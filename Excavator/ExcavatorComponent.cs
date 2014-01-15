//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using System;

namespace Excavator
{
    /// <summary>
    /// Excavator class holds the base methods and properties needed to convert data to Rock
    /// </summary>
    public abstract class ExcavatorComponent
    {
        #region Fields 
        
        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public abstract string FullName 
        {
            get;
        }  

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public virtual string errorMessage
        {
            get { return string.Empty; }
        }

        #endregion

        #region Methods 

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <returns></returns>
        public abstract bool Load( object database );

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public abstract bool Save();

        /// <summary>
        /// Returns the full name of this excavator type.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return FullName;
        }   
        
        #endregion        
    }    
}
