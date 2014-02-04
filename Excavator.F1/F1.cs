// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System;
using System.ComponentModel.Composition;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>    
    [Export( typeof( ExcavatorComponent) )]
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
            get { return "FellowshipOne"; }
        }

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData()
        {
            var pServer = new PersonService();

            // for each selected node
            // fire a method to import and map a Rock.[Model] object

            // test creating a person
            var person = new Person();
            person.BirthDate = new DateTime( 1980, 1, 1 );
            person.FirstName = "David";
            person.LastName = "Stevens";
            person.Gender = Gender.Male;

            pServer.Save( person );
            
            return false;
        }

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool SaveData()
        {
            // not implemented
            return false;
        }


        #endregion
    }
}
