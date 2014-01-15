//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using OrcaMDF.Core.Engine;

namespace Excavator
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>    
    class F1 : ExcavatorComponent
    {
        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public override string FullName
        {
            get { return "FellowshipOne";  }
        }

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool Load( object database )
        {
            return false;
        }

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool Save()
        {
            return false;
        }
            

        #endregion
    }
}
