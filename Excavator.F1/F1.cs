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
using System.Data;
using System.Web;

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
            var personService = new PersonService();
            var admin = personService.Get( 1 );

            foreach( var node in selectedNodes.Where( n => n.Checked != false ) )
            {
                DataTable nodeData = GetData( node.Id );

                switch( node.Name )
                {
                    case "Individual_Household":
                        MapPerson( nodeData );
                        break;
                    default:
                        break;
                }
            }
            
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

        #region Mapped Data

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="nodeData">The node data.</param>
        private void MapPerson( DataTable nodeData )
        {
            var aliasService = new PersonAliasService();
            var CurrentPersonAlias = aliasService.Get( 1 );
            var personService = new PersonService();

            foreach( DataRow row in nodeData.Rows )
            {
                // only import where node.Checked

                var person = new Person();
                //person.FirstName = row["First_Name"] as string;
                //person.LastName = row["Last_Name"] as string;
                //person.BirthDate = row["Date_Of_Birth"] as DateTime?;                
                //person.Gender = (Gender) (Enum.Parse( typeof(Gender), row["Gender"] as string) ?? Gender.Unknown );

                //personService.Add( person, CurrentPersonAlias );
                //personService.Save( person, CurrentPersonAlias );
            }            
        }

        #endregion
    }
}
