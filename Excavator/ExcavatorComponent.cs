//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

namespace Excavator
{
    /// <summary>
    /// Excavator class holds the base methods and properties needed to convert data to Rock
    /// </summary>
    public abstract class ExcavatorComponent
    {
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

        /// <summary>
        /// Loads the data for this instance.
        /// </summary>
        /// <returns></returns>
        public abstract bool Load();

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public abstract bool Save();
    }
}
