using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    /// <summary>
    /// Partial of CSVComponent that holds the family import methods
    /// </summary>
    partial class CSVComponent
    {
        /// <summary>
        /// Loads the family data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private int LoadFamily( CsvDataModel csvData )
        {
            // Required variables
            var lookupContext = new RockContext();
            var locationService = new LocationService( lookupContext );
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            int numImportedFamilies = ImportedPeople.Select( p => p.ForeignId ).Distinct().Count();

            int homeLocationTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id;
            int workLocationTypeId = DefinedValueCache.Read( new Guid( "E071472A-F805-4FC4-917A-D5E3C095C35C" ) ).Id;

            var newGroupLocations = new Dictionary<GroupLocation, string>();
            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var updatedFamilyList = new List<Group>();

            var dateFormats = new[] { "YYYY-MM-DD", "MM/dd/yyyy", "MM/dd/yy" };

            string currentFamilyId = string.Empty;
            var importDate = DateTime.Now;
            int completed = 0;
            int rows_checked = 0;
            int block_counter = 0;

            ReportProgress( 0, string.Format( "Starting family import ({0:N0} already exist).", numImportedFamilies ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                rows_checked++;
                
                string rowFamilyId = row[FamilyId];
                string rowFamilyName = row[FamilyName];

                ReportProgress( 0, "Importing family row " + (string.Format(" {0:N0} - ", rows_checked)) + rowFamilyName + " - FamilyId: " + rowFamilyId );

                if ( !string.IsNullOrWhiteSpace( rowFamilyId ) && rowFamilyId != currentFamilyGroup.ForeignId )
                {
                    currentFamilyGroup = ImportedPeople.FirstOrDefault( p => p.ForeignId == rowFamilyId );
                    if ( currentFamilyGroup == null )
                    {
                        currentFamilyGroup = new Group();
                        currentFamilyGroup.ForeignId = rowFamilyId;
                        currentFamilyGroup.Name = row[FamilyName];
                        currentFamilyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        currentFamilyGroup.GroupTypeId = familyGroupTypeId;
                        newFamilyList.Add( currentFamilyGroup );
                    }
                    else
                    {
                        lookupContext.Groups.Attach( currentFamilyGroup );
                    }

                    // Set the family campus
                    string campusName = row[Campus];
                    if ( !string.IsNullOrWhiteSpace( campusName ) )
                    {
                        var familyCampus = CampusList.Where( c => c.Name.Equals( campusName, StringComparison.InvariantCultureIgnoreCase )
                            || c.ShortCode.Equals( campusName, StringComparison.InvariantCultureIgnoreCase ) ).FirstOrDefault();
                        if ( familyCampus == null )
                        {
                            familyCampus = new Campus();
                            familyCampus.IsSystem = false;
                            familyCampus.Name = campusName;
                            lookupContext.Campuses.Add( familyCampus );
                            lookupContext.SaveChanges( true );
                        }

                        currentFamilyGroup.CampusId = familyCampus.Id;
                    }

                    // Add the family addresses since they exist in this file
                    string famAddress = row[Address];
                    string famAddress2 = row[Address2];
                    string famCity = row[City];
                    string famState = row[State];
                    string famZip = row[Zip];
                    string famCountry = row[Country];

                    // Use the core Rock location service to add or lookup an address
                    Location primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry );
                    if ( primaryAddress != null )
                    {
                        primaryAddress.Name = currentFamilyGroup.Name + " Home";

                        var primaryLocation = new GroupLocation();
                        primaryLocation.LocationId = primaryAddress.Id;
                        primaryLocation.IsMailingLocation = true;
                        primaryLocation.IsMappedLocation = true;
                        primaryLocation.GroupLocationTypeValueId = homeLocationTypeId;
                        newGroupLocations.Add( primaryLocation, rowFamilyId );
                    }

                    string famSecondAddress = row[SecondaryAddress];
                    string famSecondAddress2 = row[SecondaryAddress2];
                    string famSecondCity = row[SecondaryCity];
                    string famSecondState = row[SecondaryState];
                    string famSecondZip = row[SecondaryZip];
                    string famSecondCountry = row[SecondaryCountry];

                    Location secondaryAddress = locationService.Get( famSecondAddress, famSecondAddress2, famSecondCity, famSecondState, famSecondZip, famSecondCountry );
                    if ( secondaryAddress != null )
                    {
                        secondaryAddress.Name = currentFamilyGroup.Name + " Work";

                        var secondaryLocation = new GroupLocation();
                        secondaryLocation.LocationId = secondaryAddress.Id;
                        secondaryLocation.IsMailingLocation = true;
                        secondaryLocation.IsMappedLocation = true;
                        secondaryLocation.GroupLocationTypeValueId = workLocationTypeId;
                        newGroupLocations.Add( secondaryLocation, rowFamilyId );
                    }

                    DateTime createdDateValue;
                    if ( DateTime.TryParseExact( row[CreatedDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out createdDateValue ) )
                    {
                        currentFamilyGroup.CreatedDateTime = createdDateValue;
                        currentFamilyGroup.ModifiedDateTime = importDate;
                    }
                    else
                    {
                        currentFamilyGroup.CreatedDateTime = importDate;
                        currentFamilyGroup.ModifiedDateTime = importDate;
                    }

                    completed++;

                    block_counter++;    
                    if ( block_counter < ReportingNumber )
                    {
                        // ReportProgress( 0, string.Format( "{0:N0} families imported.", completed ) );
                    }
                    else 
                    {

                        ReportProgress(0, "...Saving...");
                        SaveFamilies( newFamilyList, newGroupLocations );

                        //ReportPartialProgress();
                        ReportProgress(0, string.Format("...Saved or updated {0:N0} families.", completed));
                        ReportProgress(0, " ");
                        
                        // Reset lookup context
                        lookupContext.SaveChanges();
                        lookupContext = new RockContext();
                        locationService = new LocationService( lookupContext );
                        newFamilyList.Clear();
                        newGroupLocations.Clear();

                        block_counter = 0;

                    }
                }
            }

            // Check to see if any rows didn't get saved to the database
            if ( newGroupLocations.Any() )
            {
                ReportProgress( 0, "...Saving..." );
                SaveFamilies(newFamilyList, newGroupLocations);

                lookupContext.SaveChanges();

                ReportProgress(0, string.Format("...Saved or updated {0:N0} families.", completed));
                ReportProgress(0, " ");

            }

            ReportProgress( 0, string.Format( "Finished family import: {0:N0} families added or updated.", completed ) );
            ReportProgress( 0, string.Format( "Finished family import: {0:N0} rows processed. ", rows_checked));
            ReportProgress( 0, " " );

            return completed;
        }

        /// <summary>
        /// Saves all family changes.
        /// </summary>
        private void SaveFamilies( List<Group> newFamilyList, Dictionary<GroupLocation, string> newGroupLocations )
        {
            var rockContext = new RockContext();

            // First save any unsaved families
            if ( newFamilyList.Any() )
            {
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges( true );
                } );

                // Add these new families to the global list
                ImportedPeople.AddRange( newFamilyList );
            }

            // Now save locations
            if ( newGroupLocations.Any() )
            {
                // Add updated family id to locations
                foreach ( var locationPair in newGroupLocations )
                {
                    int? familyGroupId = ImportedPeople.Where( g => g.ForeignId == locationPair.Value ).Select( g => (int?)g.Id ).FirstOrDefault();
                    if ( familyGroupId != null )
                    {
                        locationPair.Key.GroupId = (int)familyGroupId;
                    }
                }

                // Save locations
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Configuration.AutoDetectChangesEnabled = false;
                    rockContext.GroupLocations.AddRange( newGroupLocations.Keys );
                    rockContext.ChangeTracker.DetectChanges();
                    rockContext.SaveChanges( true );
                } );
            }
        }
    }
}
