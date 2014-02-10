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
using Rock.Data;
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
            int tableCount = 0;

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

                tableCount++;
            }

            return tableCount > 0 ? true : false;
        }

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool SaveData()
        {
            // not implemented yet
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
            var attributeService = new AttributeService();
            var dvService = new DefinedValueService();
            var personService = new PersonService();
            var noteService = new NoteService();

            // DefinedValues section
            // Marital statuses: Married, Single, Separated, etc
            List<DefinedValue> maritalStatusTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS ) ).ToList();

            // Connection statuses: Member, Visitor, Attendee, etc
            List<DefinedValue> connectionStatusTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ) ).ToList();

            // Record status reasons: No Activity, Moved, Deceased, etc
            List<DefinedValue> recordStatusReasons = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS_REASON ) ).ToList();

            // Record statuses: Active, Inactive, Pending
            int? statusActiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int? statusInactiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int? statusPendingId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

            // Record type: Person
            int? personRecordTypeId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON ) ).Id;

            // Suffix type: Dr., Jr., II, etc
            List<DefinedValue> suffixTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ) ).ToList();

            // Note type: Comment
            int noteCommentTypeId = new NoteTypeService().Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

            // if any are null then should create new?

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            var CurrentPersonAlias = aliasService.Get( 1 );

            foreach ( var row in nodeData )
            {
                // only import where selectedColumns.Contains( row.Column )

                var person = new Person();
                person.FirstName = row["First_Name"] as string;
                person.MiddleName = row["Middle_Name"] as string;
                person.NickName = row["Goes_By"] as string ?? person.FirstName;
                person.LastName = row["Last_Name"] as string;
                person.BirthDate = row["Date_Of_Birth"] as DateTime?;
                person.RecordTypeValueId = personRecordTypeId;

                var gender = row["Gender"] as string;
                if ( gender != null )
                {
                    person.Gender = (Gender)Enum.Parse( typeof( Gender ), gender );
                }

                string suffix = row["Suffix"] as string;
                if ( suffix != null )
                {
                    person.SuffixValueId = suffixTypes.Where( s => s.Name == suffix )
                        .Select( s => (int?)s.Id ).FirstOrDefault();
                }

                string member_status = row["Status_Name"] as string;
                if ( member_status == "Member" )
                {
                    person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
                    person.RecordStatusValueId = statusActiveId;
                }
                else if ( member_status == "Visitor" )
                {
                    person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
                    person.RecordStatusValueId = statusActiveId;
                }
                else if ( member_status == "Deceased" )
                {
                    person.IsDeceased = true;
                    person.RecordStatusValueId = statusInactiveId;
                    person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Name == "Deceased" )
                        .Select( dv => dv.Id ).FirstOrDefault();
                }
                else
                {
                    // F1 defaults are Member & Visitor; all others are user-defined
                    person.ConnectionStatusValueId = connectionStatusTypes.Where( dv => dv.Name == member_status )
                        .Select( dv => dv.Id ).FirstOrDefault();
                    person.RecordStatusValueId = statusActiveId;
                }

                string join_date = row["Status_Date"] as string;
                if ( join_date != null )
                {
                    DateTime firstCreated;
                    if ( DateTime.TryParse( join_date, out firstCreated ) )
                    {
                        person.CreatedDateTime = firstCreated;
                    }
                }

                string marital_status = row["Marital_Status"] as string;
                if ( marital_status != null )
                {
                    person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Name == marital_status )
                        .Select( dv => (int?)dv.Id ).FirstOrDefault();
                }
                else
                {
                    person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Name == "Unknown" )
                        .Select( dv => (int?)dv.Id ).FirstOrDefault();
                }

                string status_comment = row["Status_Comment"] as string;
                if ( status_comment != null )
                {
                    Note comment = new Note();
                    comment.Text = status_comment;
                    comment.NoteTypeId = noteCommentTypeId;
                    RockTransactionScope.WrapTransaction( () =>
                    {
                        noteService.Save( comment );
                    } );
                }

                // Other Properties (Attributes to create):
                // prefix
                // former name
                // first_record date
                // occupation_name
                // occupation_description
                // employer
                // school_name
                // former_church
                // bar_code
                // member_env_code
                // denomination_name
                // substatus_name (campus)

                RockTransactionScope.WrapTransaction( () =>
                {
                    personService.Add( person, CurrentPersonAlias );
                    personService.Save( person, CurrentPersonAlias );
                } );
            }
        }

        #endregion
    }
}
