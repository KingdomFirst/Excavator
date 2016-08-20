using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Excavator.Utility;
using Rock;
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
        private int LoadFamily( CSVInstance csvData )
        {
            // Required variables
            var lookupContext = new RockContext();
            var locationService = new LocationService( lookupContext );
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            int numImportedFamilies = ImportedFamilies.Count();

            int homeLocationTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id;
            int workLocationTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK ) ).Id;

            var newGroupLocations = new Dictionary<GroupLocation, string>();
            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var updatedFamilyList = new List<Group>();

            var dateFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "MM/dd/yy" };

            string currentFamilyKey = string.Empty;
            int completed = 0;

            ReportProgress( 0, string.Format( "Starting family import ({0:N0} already exist).", numImportedFamilies ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( (row = csvData.Database.FirstOrDefault()) != null )
            {
                string rowFamilyKey = row[FamilyId];
                int? rowFamilyId = rowFamilyKey.AsType<int?>();
                string rowFamilyName = row[FamilyName];

                if ( rowFamilyKey != null && rowFamilyKey != currentFamilyGroup.ForeignKey )
                {
                    currentFamilyGroup = ImportedFamilies.FirstOrDefault( g => g.ForeignKey == rowFamilyKey );
                    if ( currentFamilyGroup == null )
                    {
                        currentFamilyGroup = new Group();
                        currentFamilyGroup.ForeignKey = rowFamilyKey;
                        currentFamilyGroup.ForeignId = rowFamilyId;
                        currentFamilyGroup.Name = row[FamilyName];
                        currentFamilyGroup.CreatedByPersonAliasId = ImportPersonAliasId;
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
                            familyCampus.ShortCode = campusName.RemoveWhitespace();
                            lookupContext.Campuses.Add( familyCampus );
                            lookupContext.SaveChanges( DisableAuditing );
                            CampusList.Add( familyCampus );
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

                    Location primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry, verifyLocation: false );

                    if ( primaryAddress != null )
                    {
                        var primaryLocation = new GroupLocation();
                        primaryLocation.LocationId = primaryAddress.Id;
                        primaryLocation.IsMailingLocation = true;
                        primaryLocation.IsMappedLocation = true;
                        primaryLocation.GroupLocationTypeValueId = homeLocationTypeId;
                        newGroupLocations.Add( primaryLocation, rowFamilyKey );
                    }

                    string famSecondAddress = row[SecondaryAddress];
                    string famSecondAddress2 = row[SecondaryAddress2];
                    string famSecondCity = row[SecondaryCity];
                    string famSecondState = row[SecondaryState];
                    string famSecondZip = row[SecondaryZip];
                    string famSecondCountry = row[SecondaryCountry];

                    Location secondaryAddress = locationService.Get( famSecondAddress, famSecondAddress2, famSecondCity, famSecondState, famSecondZip, famSecondCountry, verifyLocation: false );

                    if ( secondaryAddress != null )
                    {
                        var secondaryLocation = new GroupLocation();
                        secondaryLocation.LocationId = secondaryAddress.Id;
                        secondaryLocation.IsMailingLocation = true;
                        secondaryLocation.IsMappedLocation = true;
                        secondaryLocation.GroupLocationTypeValueId = workLocationTypeId;
                        newGroupLocations.Add( secondaryLocation, rowFamilyKey );
                    }

                    DateTime createdDateValue;
                    if ( DateTime.TryParseExact( row[CreatedDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out createdDateValue ) )
                    {
                        currentFamilyGroup.CreatedDateTime = createdDateValue;
                        currentFamilyGroup.ModifiedDateTime = ImportDateTime;
                    }
                    else
                    {
                        currentFamilyGroup.CreatedDateTime = ImportDateTime;
                        currentFamilyGroup.ModifiedDateTime = ImportDateTime;
                    }

                    completed++;
                    if ( completed % (ReportingNumber * 10) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} families imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFamilies( newFamilyList, newGroupLocations );
                        ReportPartialProgress();

                        // Reset lookup context
                        lookupContext.SaveChanges();
                        lookupContext = new RockContext();
                        locationService = new LocationService( lookupContext );
                        newFamilyList.Clear();
                        newGroupLocations.Clear();
                    }
                }
            }

            // Check to see if any rows didn't get saved to the database
            if ( newGroupLocations.Any() )
            {
                SaveFamilies( newFamilyList, newGroupLocations );
            }

            lookupContext.SaveChanges();
            DetachAllInContext( lookupContext );
            lookupContext.Dispose();

            ReportProgress( 0, string.Format( "Finished family import: {0:N0} families added or updated.", completed ) );
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
                rockContext.WrapTransaction( ( ) =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges( DisableAuditing );
                } );

                // Add these new families to the global list
                ImportedFamilies.AddRange( newFamilyList );
            }

            // Now save locations
            if ( newGroupLocations.Any() )
            {
                // Set updated family id on locations
                foreach ( var locationPair in newGroupLocations )
                {
                    int? familyGroupId = ImportedFamilies.Where( g => g.ForeignKey == locationPair.Value ).Select( g => ( int? )g.Id ).FirstOrDefault();
                    if ( familyGroupId != null )
                    {
                        locationPair.Key.GroupId = ( int )familyGroupId;
                    }
                }

                // Save locations
                rockContext.WrapTransaction( ( ) =>
                {
                    rockContext.Configuration.AutoDetectChangesEnabled = false;
                    rockContext.GroupLocations.AddRange( newGroupLocations.Keys );
                    rockContext.ChangeTracker.DetectChanges();
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }
    }
}