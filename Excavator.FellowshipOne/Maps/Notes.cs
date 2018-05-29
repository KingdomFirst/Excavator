using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using static Excavator.Utility.CachedTypes;
using static Excavator.Utility.Extensions;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the Notes import
    /// </summary>
    public partial class F1Component
    {
        /// <summary>
        /// Maps the contact form data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="totalRows">The total rows.</param>
        public void MapContactFormData( IQueryable<Row> tableData, long totalRows = 0 )
        {
            var lookupContext = new RockContext();

            var importedCommunicationCount = new CommunicationService( lookupContext ).Queryable().Count( c => c.ForeignKey != null );
            var importedNoteCount = new NoteService( lookupContext ).Queryable().Count( n => n.ForeignKey != null );

            var prayerRequestors = new Dictionary<int, Person>();

            var communicationList = new List<Communication>();
            var prayerList = new List<PrayerRequest>();
            var noteList = new List<Note>();

            if ( totalRows == 0 )
            {
                totalRows = tableData.Count();
            }

            var completedItems = 0;
            var percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, $"Verifying contact items ({totalRows:N0} found, {importedNoteCount + importedCommunicationCount:N0} already exist)." );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                // ContactFormData joins to IndividualContactNotes on ContactInstItemID
                var itemForeignKey = row["ContactInstItemID"] as int?;
                var householdId = row["HouseholdID"] as int?;
                var itemIndividualId = row["ContactItemIndividualID"] as int?;
                var individualId = row["ContactIndividualID"] as int?;
                var createdDate = row["ContactActivityDate"] as DateTime?;
                var modifiedDate = row["ContactDatetime"] as DateTime?;
                var approvalDate = row["ContactFormLastUpdatedDate"] as DateTime?;
                var itemType = row["ContactFormName"] as string;
                var itemStatus = row["ContactStatus"] as string;
                var itemCaption = row["ContactItemName"] as string;
                var noteText1 = row["ContactNote"] as string;
                var noteText2 = row["ContactItemNote"] as string;
                var itemUserId = row["ContactItemAssignedUserID"] as int?;
                var contactUserId = row["ContactAssignedUserID"] as int?;
                var initialContactUserId = row["InitialContactCreatedByUserID"] as int?;
                var isConfidential = row["IsContactItemConfidential"] as int?;
                var itemText = !string.IsNullOrWhiteSpace( noteText1 ) ? $"{noteText1}<br>{noteText2}" : noteText2 ?? string.Empty;

                // look up the person this contact form is for
                var personKeys = GetPersonKeys( itemIndividualId ?? individualId, householdId );
                if ( personKeys != null && ( !string.IsNullOrWhiteSpace( itemCaption ) || !string.IsNullOrWhiteSpace( itemText ) ) )
                {
                    var assignedUserId = itemUserId ?? contactUserId ?? initialContactUserId ?? 0;
                    var userPersonAliasId = PortalUsers.ContainsKey( assignedUserId ) ? (int?)PortalUsers[assignedUserId] : null;
                    if ( itemType.Equals( "Email", StringComparison.CurrentCultureIgnoreCase ) )
                    {
                        // create the recipient list for this contact
                        var recipients = new List<CommunicationRecipient> {
                            new CommunicationRecipient {
                                Status = CommunicationRecipientStatus.Delivered,
                                PersonAliasId = personKeys.PersonAliasId,
                                CreatedDateTime = createdDate ?? modifiedDate,
                                CreatedByPersonAliasId = userPersonAliasId,
                                ModifiedByPersonAliasId = userPersonAliasId,
                                ForeignKey = personKeys.PersonForeignId.ToString(),
                                ForeignId = personKeys.PersonForeignId
                            }
                        };

                        // create an email record for this contact form
                        var emailSubject = !string.IsNullOrWhiteSpace( itemCaption ) ? itemCaption.Left( 100 ) : itemText.Left( 100 );
                        var communication = AddCommunication( lookupContext, EmailCommunicationMediumTypeId, emailSubject, itemText, false,
                            CommunicationStatus.Approved, recipients, false, createdDate ?? modifiedDate, itemForeignKey.ToString(), userPersonAliasId );

                        communicationList.Add( communication );
                    }
                    else if ( !string.IsNullOrWhiteSpace( itemCaption ) && itemCaption.EndsWith( "Prayer Request", StringComparison.CurrentCultureIgnoreCase ) )
                    {
                        // create a prayer request
                        Person requestor = null;
                        prayerRequestors.TryGetValue( personKeys.PersonId, out requestor );
                        if ( requestor == null )
                        {
                            requestor = lookupContext.People.FirstOrDefault( p => p.Id.Equals( personKeys.PersonId ) );
                            prayerRequestors.Add( personKeys.PersonId, requestor );
                        }

                        var request = AddPrayerRequest( lookupContext, null, personKeys.PersonAliasId, requestor.FirstName, requestor.LastName, requestor.Email, itemText ?? itemCaption, string.Empty,
                            !itemStatus.Equals( "Closed", StringComparison.CurrentCultureIgnoreCase ), false, createdDate ?? modifiedDate, approvalDate, itemForeignKey.ToString(), userPersonAliasId );
                        if ( request != null )
                        {
                            prayerList.Add( request );
                        }
                    }
                    else
                    {
                        //strip campus from type
                        var campusId = GetCampusId( itemType );
                        if ( campusId.HasValue )
                        {
                            itemType = StripPrefix( itemType, campusId );
                        }

                        // create a note for this contact form
                        var note = AddEntityNote( lookupContext, PersonEntityTypeId, personKeys.PersonId, itemCaption, itemText, false, false, itemType,
                            null, false, createdDate ?? modifiedDate, itemForeignKey.ToString(), userPersonAliasId );

                        noteList.Add( note );
                    }

                    completedItems++;

                    if ( completedItems % percentage < 1 )
                    {
                        var percentComplete = completedItems / percentage;
                        ReportProgress( percentComplete, $"{completedItems:N0} contact items imported ({percentComplete}% complete)." );
                    }
                    else if ( completedItems % ReportingNumber < 1 )
                    {
                        SaveCommunications( communicationList );
                        SavePrayerRequests( prayerList );
                        SaveNotes( noteList );
                        ReportPartialProgress();

                        communicationList.Clear();
                        prayerList.Clear();
                        noteList.Clear();
                    }
                }
            }

            if ( communicationList.Any() || noteList.Any() )
            {
                SaveCommunications( communicationList );
                SavePrayerRequests( prayerList );
                SaveNotes( noteList );
            }

            ReportProgress( 100, $"Finished contact item import: {completedItems:N0} items imported." );
        }

        /// <summary>
        /// Maps the individual contact notes.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="totalRows">The total rows.</param>
        public void MapIndividualContactNotes( IQueryable<Row> tableData, long totalRows = 0 )
        {
            var lookupContext = new RockContext();

            var importedNotes = new NoteService( lookupContext ).Queryable().Where( n => n.ForeignId != null )
                .ToDictionary( n => n.ForeignId, n => n.Id );
            var importedRequests = new PrayerRequestService( lookupContext ).Queryable().Where( r => r.ForeignId != null )
                .ToDictionary( r => r.ForeignId, r => r.Id );

            var noteList = new List<Note>();
            int? confidentialNoteTypeId = null;

            if ( totalRows == 0 )
            {
                totalRows = tableData.Count();
            }

            var completedItems = 0;
            var percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, $"Verifying contact notes ({totalRows:N0} found, {importedNotes.Count:N0} already exist)." );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                var userId = row["UserID"] as int?;
                var individualId = row["IndividualID"] as int?;
                var itemForeignKey = row["ContactInstItemID"] as int?;
                var createdDate = row["IndividualContactDatetime"] as DateTime?;
                var noteText = row["IndividualContactNote"] as string;
                var confidentialText = row["ConfidentialNote"] as string;

                var personKeys = GetPersonKeys( individualId, null );
                if ( personKeys != null && ( !string.IsNullOrWhiteSpace( noteText ) || !string.IsNullOrWhiteSpace( confidentialText ) ) )
                {
                    int? creatorAliasId = null;
                    if ( userId.HasValue && PortalUsers.ContainsKey( (int)userId ) )
                    {
                        creatorAliasId = PortalUsers[(int)userId];
                    }

                    var noteId = 0;
                    var noteEntityId = personKeys.PersonId;
                    var noteEntityTypeId = PersonEntityTypeId;
                    var noteTypeId = PersonalNoteTypeId;

                    // add a confidential note
                    if ( !string.IsNullOrWhiteSpace( confidentialText ) )
                    {
                        var confidential = AddEntityNote( lookupContext, noteEntityTypeId, noteEntityId, string.Empty, confidentialText, false, false,
                            "Confidential Note", confidentialNoteTypeId, false, createdDate, itemForeignKey.ToString() );
                        confidentialNoteTypeId = confidential.NoteTypeId;

                        noteList.Add( confidential );
                    }

                    // this is new or an update to timeline note
                    if ( importedNotes.ContainsKey( itemForeignKey ) )
                    {
                        noteId = importedNotes[itemForeignKey];
                    }
                    // note this as a prayer request comment
                    else if ( importedRequests.ContainsKey( itemForeignKey ) )
                    {
                        noteEntityTypeId = PrayerRequestTypeId;
                        noteEntityId = importedRequests[itemForeignKey];
                        noteTypeId = PrayerNoteTypeId;
                    }

                    // add the note text
                    if ( !string.IsNullOrWhiteSpace( noteText ) )
                    {
                        var note = AddEntityNote( lookupContext, noteEntityTypeId, noteEntityId, string.Empty, noteText, false, false,
                            null, noteTypeId, false, createdDate, itemForeignKey.ToString() );
                        note.Id = noteId;

                        noteList.Add( note );
                    }

                    completedItems++;
                    if ( completedItems % percentage < 1 )
                    {
                        var percentComplete = completedItems / percentage;
                        ReportProgress( percentComplete, $"{completedItems:N0} notes imported ({percentComplete}% complete)." );
                    }
                    else if ( completedItems % ReportingNumber < 1 )
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

            ReportProgress( 100, $"Finished contact note import: {completedItems:N0} notes imported." );
        }

        /// <summary>
        /// Maps the notes.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="totalRows">The total rows.</param>
        public void MapNotes( IQueryable<Row> tableData, long totalRows = 0 )
        {
            var lookupContext = new RockContext();

            var importedUsers = new UserLoginService( lookupContext ).Queryable().AsNoTracking()
                .Where( u => u.ForeignId != null )
                .ToDictionary( t => t.ForeignId, t => t.PersonId );

            var noteList = new List<Note>();

            if ( totalRows == 0 )
            {
                totalRows = tableData.Count();
            }

            var completedItems = 0;
            var percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, $"Verifying note import ({totalRows:N0} found)." );
            foreach ( var row in tableData.Where( r => r != null ) )
            {
                var noteType = row["Note_Type_Name"] as string;
                var text = row["Note_Text"] as string;
                var individualId = row["Individual_ID"] as int?;
                var householdId = row["Household_ID"] as int?;
                var noteTypeActive = row["NoteTypeActive"] as bool?;
                var noteArchived = row["NoteArchived"] as bool?;
                var noteTextArchived = row["NoteTextArchived"] as bool?;
                var dateCreated = row["NoteCreated"] as DateTime?;

                // see if pre-import helper fix is present
                var noteArchivedFlag = row["NoteArchived"] as int?;
                var noteTextArchivedFlag = row["NoteTextArchived"] as int?;
                noteArchived = noteArchived.HasValue ? noteArchived : noteArchivedFlag > 0;
                noteTextArchived = noteTextArchived.HasValue ? noteTextArchived : noteTextArchivedFlag > 0;

                var noteExcluded = noteArchived == true || noteTextArchived == true;
                var personKeys = GetPersonKeys( individualId, householdId );
                if ( personKeys != null && !string.IsNullOrWhiteSpace( text ) && noteTypeActive == true && !noteExcluded )
                {
                    int? creatorAliasId = null;
                    var userId = row["NoteCreatedByUserID"] as int?;

                    if ( userId.HasValue && PortalUsers.ContainsKey( (int)userId ) )
                    {
                        creatorAliasId = PortalUsers[(int)userId];
                    }

                    var noteTypeId = noteType.StartsWith( "General", StringComparison.InvariantCultureIgnoreCase ) ? (int?)PersonalNoteTypeId : null;
                    var note = AddEntityNote( lookupContext, PersonEntityTypeId, personKeys.PersonId, string.Empty, text, false, false, noteType, noteTypeId, false, dateCreated,
                        $"Note imported {ImportDateTime}", creatorAliasId );

                    noteList.Add( note );
                    completedItems++;

                    if ( completedItems % percentage < 1 )
                    {
                        var percentComplete = completedItems / percentage;
                        ReportProgress( percentComplete, $"{completedItems:N0} notes imported ({percentComplete}% complete)." );
                    }
                    else if ( completedItems % ReportingNumber < 1 )
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

            ReportProgress( 100, $"Finished note import: {completedItems:N0} notes imported." );
        }

        /// <summary>
        /// Saves the communications.
        /// </summary>
        /// <param name="communicationList">The communication list.</param>
        private static void SaveCommunications( List<Communication> communicationList )
        {
            if ( communicationList.Count > 0 )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Communications.AddRange( communicationList );
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }

        /// <summary>
        /// Saves the prayer requests.
        /// </summary>
        /// <param name="prayerList">The prayer list.</param>
        private static void SavePrayerRequests( List<PrayerRequest> prayerList )
        {
            if ( prayerList.Count > 0 )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( () =>
                {
                    rockContext.PrayerRequests.AddRange( prayerList );
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }

        /// <summary>
        /// Saves the notes.
        /// </summary>
        /// <param name="noteList">The note list.</param>
        private static void SaveNotes( List<Note> noteList )
        {
            if ( noteList.Count > 0 )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Configuration.AutoDetectChangesEnabled = false;
                    rockContext.Notes.AddRange( noteList.Where( n => n.Id == 0 ) );

                    foreach ( var note in noteList.Where( n => n.Id > 0 ) )
                    {
                        var existingNote = rockContext.Notes.FirstOrDefault( n => n.Id == note.Id );
                        if ( existingNote != null )
                        {
                            existingNote.Text += note.Text;
                            rockContext.Entry( existingNote ).State = EntityState.Modified;
                        }
                    }

                    rockContext.ChangeTracker.DetectChanges();
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }
    }
}
