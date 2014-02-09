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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Linq;
using OrcaMDF.Core.Engine;
using OrcaMDF.Core.MetaData;
using Rock.Model;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
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
            var scanner = new DataScanner( database );
            var personService = new PersonService();
            var admin = personService.Get( 1 );

            foreach ( var node in selectedNodes.Where( n => n.Checked != false ) )
            {
                IQueryable<Row> rowData = scanner.ScanTable( node.Name ).AsQueryable();
                List<string> selectedColumns = node.Columns.Where( c => c.Checked == true )
                    .Select( c => c.Name ).ToList();

                switch ( node.Name )
                {
                    case "Individual_Household":
                        MapPerson( rowData, selectedColumns );
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
        private void MapPerson( IQueryable<Row> nodeData, List<string> selectedColumns )
        {
            var aliasService = new PersonAliasService();
            var definedValueService = new DefinedValueService();
            var CurrentPersonAlias = aliasService.Get( 1 );
            var personService = new PersonService();

            foreach ( var row in nodeData )
            {
                // only import where selectedColumns.Contains( row.Column )

                var person = new Person();
                person.FirstName = row["First_Name"] as string;
                person.MiddleName = row["Middle_Name"] as string;
                person.NickName = row["Goes_By"] as string ?? person.FirstName;
                person.LastName = row["Last_Name"] as string;
                person.BirthDate = row["Date_Of_Birth"] as DateTime?;

                var gender = row["Gender"] as string;
                if ( gender != null )
                {
                    person.Gender = (Gender)Enum.Parse( typeof( Gender ), gender );
                }

                string marital_status = row["Marital_Status"] as string;
                if ( marital_status == "Married" )
                {
                    person.MaritalStatusValue = definedValueService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_MARITAL_STATUS_MARRIED ) );
                }

                //Single
                //Separated
                //Divorced
                //Widow/ed/er

                // former name?
                // prefix
                // suffix
                // marital_status
                // first_record
                // occupation_name
                // occupation_description
                // employer
                // school_name
                // former_church
                // status_name (member, etc)
                // status_date (joined)
                // substatus (campus)
                // bar_code
                // member_env_code
                // status_comment (notes)
                // denomination_name

                personService.Add( person, CurrentPersonAlias );
                personService.Save( person, CurrentPersonAlias );

                //var groupMember = new GroupMember();
                //groupMember.Person = new Person();
                //groupMember.Person.Guid = row.PersonGuid.Value;
                //groupMember.Person.RecordStatusValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE.AsGuid() ).Id;

                //if ( row.RoleId.HasValue )
                //{
                //    groupMember.GroupRoleId = row.RoleId.Value;
                //}
            }
        }

        #endregion
    }
}
