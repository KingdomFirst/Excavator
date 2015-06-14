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
using System.Linq;
using System.Text.RegularExpressions;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the Notes import
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the notes.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        public void MapNotes( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var categoryService = new CategoryService( lookupContext );
            var personService = new PersonService( lookupContext );

            var noteTypes = new NoteTypeService( lookupContext ).Queryable().ToList();
            var personalNoteType = noteTypes.FirstOrDefault( nt => nt.Guid == new Guid( Rock.SystemGuid.NoteType.PERSON_TIMELINE_NOTE ) );

            var importedUsers = new UserLoginService( lookupContext ).Queryable()
                .Where( u => u.ForeignId != null )
                .ToDictionary( t => t.ForeignId.AsType<int?>(), t => t.PersonId );

            var noteList = new List<Note>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying note import ({0:N0} found).", totalRows ) );
            foreach ( var row in tableData.Where( r => r != null ) )
            {
                string text = row["Note_Text"] as string;
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                var personKeys = GetPersonKeys( individualId, householdId );
                if ( personKeys != null && !string.IsNullOrWhiteSpace( text ) )
                {
                    DateTime? dateCreated = row["NoteCreated"] as DateTime?;
                    string noteType = row["Note_Type_Name"] as string;

                    var note = new Note();
                    note.CreatedDateTime = dateCreated;
                    note.EntityId = personKeys.PersonId;

                    // These replace methods don't like being chained together
                    text = Regex.Replace( text, @"\t|\&nbsp;", " " );
                    text = text.Replace( "&#45;", "-" );
                    text = text.Replace( "&lt;", "<" );
                    text = text.Replace( "&gt;", ">" );
                    text = text.Replace( "&amp;", "&" );
                    text = text.Replace( "&quot;", @"""" );
                    text = text.Replace( "&#x0D", string.Empty );
                    text = text.Trim();

                    note.Text = text;

                    int? userId = row["NoteCreatedByUserID"] as int?;
                    if ( userId != null && importedUsers.ContainsKey( userId ) )
                    {
                        var userKeys = ImportedPeople.FirstOrDefault( p => p.PersonId == (int)importedUsers[userId] );
                        if ( userKeys != null )
                        {
                            note.CreatedByPersonAliasId = userKeys.PersonAliasId;
                        }
                    }

                    int? matchingNoteTypeId = null;
                    if ( !noteType.StartsWith( "General", StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        matchingNoteTypeId = noteTypes.Where( nt => nt.Name == noteType ).Select( i => (int?)i.Id ).FirstOrDefault();
                    }
                    else
                    {
                        matchingNoteTypeId = personalNoteType.Id;
                    }

                    if ( matchingNoteTypeId != null )
                    {
                        note.NoteTypeId = (int)matchingNoteTypeId;
                    }
                    else
                    {
                        // create the note type
                        var newNoteType = new NoteType();
                        newNoteType.EntityTypeId = personalNoteType.EntityTypeId;
                        newNoteType.EntityTypeQualifierColumn = string.Empty;
                        newNoteType.EntityTypeQualifierValue = string.Empty;
                        //newNoteType.UserSelectable = true;
                        newNoteType.IsSystem = false;
                        newNoteType.Name = noteType;

                        lookupContext.NoteTypes.Add( newNoteType );
                        lookupContext.SaveChanges( DisableAudit );

                        noteTypes.Add( newNoteType );
                        note.NoteTypeId = newNoteType.Id;
                    }

                    noteList.Add( note );
                    completed++;

                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} notes imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveNotes( noteList );
                        ReportPartialProgress();
                        noteList.Clear();
                    }
                }
            }

            if ( noteList.Any() )
            {
                SaveNotes( noteList );
            }

            ReportProgress( 100, string.Format( "Finished note import: {0:N0} notes imported.", completed ) );
        }

        /// <summary>
        /// Saves the notes.
        /// </summary>
        /// <param name="noteList">The note list.</param>
        private void SaveNotes( List<Note> noteList )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.Notes.AddRange( noteList );
                rockContext.SaveChanges( DisableAudit );
            } );
        }
    }
}