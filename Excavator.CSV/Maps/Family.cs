using System;
using System.Collections.Generic;
using System.Data.Entity;
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

            int numImportedFamilies = ImportedPeople.Select( p => p.FamilyId ).Distinct().Count();

            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var newGroupLocations = new Dictionary<GroupLocation, string>();

            string currentFamilyId = string.Empty;
            int completed = 0;

            ReportProgress( 0, string.Format( "Starting family import ({0:N0} already exist).", numImportedFamilies ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                string rowFamilyId = row[FamilyId];
                string rowFamilyName = row[FamilyName];

                if ( !string.IsNullOrWhiteSpace( rowFamilyId ) && rowFamilyId != currentFamilyGroup.ForeignId )
                {
                    if ( !ImportedPeople.Any( p => p.FamilyId == rowFamilyId ) )
                    {
                        currentFamilyGroup = new Group();
                        currentFamilyGroup.ForeignId = rowFamilyId;
                        currentFamilyGroup.Name = row[FamilyName];
                        currentFamilyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        currentFamilyGroup.GroupTypeId = familyGroupTypeId;
                        newFamilyList.Add( currentFamilyGroup );
                    }

                    // Set the family campus
                    var campusName = row[Campus] as string;
                    if ( !string.IsNullOrWhiteSpace( campusName ) )
                    {
                        var familyCampus = CampusList.Where( c => c.Name.StartsWith( campusName ) ).FirstOrDefault();
                        if ( familyCampus == null )
                        {
                            familyCampus = new Campus();
                            familyCampus.IsSystem = false;
                            familyCampus.Name = campusName;
                            lookupContext.Campuses.Add( familyCampus );
                        }

                        currentFamilyGroup.CampusId = familyCampus.Id;
                    }

                    // Add the family addresses since they exist in this file
                    var famAddress = row[Address] as string;
                    var famAddress2 = row[Address2] as string;
                    var famCity = row[City] as string;
                    var famState = row[State] as string;
                    var famZip = row[Zip] as string;
                    var famCountry = row[Country] as string;

                    // Use the core Rock location service to add or lookup an address
                    Location primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry );
                    if ( primaryAddress != null )
                    {
                        primaryAddress.Name = currentFamilyGroup.Name + " Home";

                        var primaryLocation = new GroupLocation();
                        //primaryLocation.GroupId = currentFamilyGroup.Id;
                        primaryLocation.LocationId = primaryAddress.Id;
                        primaryLocation.IsMailingLocation = true;
                        primaryLocation.IsMappedLocation = true;

                        newGroupLocations.Add( primaryLocation, rowFamilyId );
                    }

                    var famSecondAddress = row[SecondaryAddress] as string;
                    var famSecondAddress2 = row[SecondaryAddress2] as string;
                    var famSecondCity = row[SecondaryCity] as string;
                    var famSecondState = row[SecondaryState] as string;
                    var famSecondZip = row[SecondaryZip] as string;
                    var famSecondCountry = row[SecondaryCountry] as string;

                    Location secondaryAddress = locationService.Get( famSecondAddress, famSecondAddress2, famSecondCity, famSecondState, famSecondZip, famSecondCountry );
                    if ( secondaryAddress != null )
                    {
                        secondaryAddress.Name = currentFamilyGroup.Name + " Work";

                        var secondaryLocation = new GroupLocation();
                        //secondaryLocation.GroupId = currentFamilyGroup.Id;
                        secondaryLocation.LocationId = primaryAddress.Id;
                        secondaryLocation.IsMailingLocation = true;
                        secondaryLocation.IsMappedLocation = true;

                        newGroupLocations.Add( secondaryLocation, rowFamilyId );
                    }

                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} families imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFamilyChanges( newFamilyList, newGroupLocations );
                        ReportPartialProgress();
                        newGroupLocations.Clear();

                        // Reset lookup context
                        lookupContext = new RockContext();
                        locationService = new LocationService( lookupContext );
                    }
                }
            }

            // Check to see if any rows didn't get saved to the database
            if ( newGroupLocations.Any() )
            {
                SaveFamilyChanges( newFamilyList, newGroupLocations );
            }

            ReportProgress( 0, string.Format( "Finished family import: {0:N0} families imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves all family changes.
        /// </summary>
        private void SaveFamilyChanges( List<Group> newFamilyList, Dictionary<GroupLocation, string> newGroupLocations )
        {
            var rockContext = new RockContext();

            // First save any unsaved families
            if ( newFamilyList.Any() )
            {
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges();
                } );
            }

            if ( newGroupLocations.Any() )
            {
                // Add group id to locations
                foreach ( var locationPair in newGroupLocations )
                {
                    // lookup group id by family foreign id
                    // locationPair.Key.GroupId =
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
