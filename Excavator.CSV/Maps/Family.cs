using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using static Excavator.Utility.CachedTypes;
using static Excavator.Utility.Extensions;

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

            var newGroupLocations = new Dictionary<GroupLocation, string>();
            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var updatedFamilyList = new List<Group>();

            var dateFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "MM/dd/yy" };

            var currentFamilyKey = string.Empty;
            var completed = 0;

            ReportProgress( 0, $"Starting family import ({ImportedFamilies.Count:N0} already exist)." );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                var rowFamilyKey = row[FamilyId];
                var rowFamilyId = rowFamilyKey.AsType<int?>();
                var rowFamilyName = row[FamilyName];

                if ( rowFamilyKey != null && rowFamilyKey != currentFamilyGroup.ForeignKey )
                {
                    currentFamilyGroup = ImportedFamilies.FirstOrDefault( g => g.ForeignKey == rowFamilyKey );
                    if ( currentFamilyGroup == null )
                    {
                        currentFamilyGroup = new Group
                        {
                            ForeignKey = rowFamilyKey,
                            ForeignId = rowFamilyId,
                            Name = row[FamilyName],
                            CreatedByPersonAliasId = ImportPersonAliasId,
                            GroupTypeId = FamilyGroupTypeId
                        };
                        newFamilyList.Add( currentFamilyGroup );
                    }
                    else
                    {
                        currentFamilyGroup.Name = row[FamilyName];
                        lookupContext.Groups.Attach( currentFamilyGroup );
                    }

                    // Set the family campus
                    var campusName = row[Campus];
                    if ( !string.IsNullOrWhiteSpace( campusName ) )
                    {
                        var familyCampus = CampusList.FirstOrDefault( c => c.Name.Equals( campusName, StringComparison.InvariantCultureIgnoreCase )
                            || c.ShortCode.Equals( campusName, StringComparison.InvariantCultureIgnoreCase ) );
                        if ( familyCampus == null )
                        {
                            familyCampus = new Campus
                            {
                                IsSystem = false,
                                Name = campusName,
                                ShortCode = campusName.RemoveWhitespace(),
                                IsActive = true
                            };
                            lookupContext.Campuses.Add( familyCampus );
                            lookupContext.SaveChanges( DisableAuditing );
                            CampusList.Add( familyCampus );
                        }

                        currentFamilyGroup.CampusId = familyCampus.Id;
                    }

                    // Add the family addresses since they exist in this file
                    var famAddress = row[Address];
                    var famAddress2 = row[Address2];
                    var famCity = row[City];
                    var famState = row[State];
                    var famZip = row[Zip];
                    var famCountry = row[Country];

                    var primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry, verifyLocation: false );

                    if ( primaryAddress != null && currentFamilyGroup.GroupLocations.Count == 0 )
                    {
                        var primaryLocation = new GroupLocation
                        {
                            LocationId = primaryAddress.Id,
                            IsMailingLocation = true,
                            IsMappedLocation = true,
                            GroupLocationTypeValueId = HomeLocationTypeId
                        };
                        newGroupLocations.Add( primaryLocation, rowFamilyKey );
                    }

                    var famSecondAddress = row[SecondaryAddress];
                    var famSecondAddress2 = row[SecondaryAddress2];
                    var famSecondCity = row[SecondaryCity];
                    var famSecondState = row[SecondaryState];
                    var famSecondZip = row[SecondaryZip];
                    var famSecondCountry = row[SecondaryCountry];

                    var secondaryAddress = locationService.Get( famSecondAddress, famSecondAddress2, famSecondCity, famSecondState, famSecondZip, famSecondCountry, verifyLocation: false );

                    if ( secondaryAddress != null && currentFamilyGroup.GroupLocations.Count < 2 )
                    {
                        var secondaryLocation = new GroupLocation
                        {
                            LocationId = secondaryAddress.Id,
                            IsMailingLocation = true,
                            IsMappedLocation = false,
                            GroupLocationTypeValueId = PreviousLocationTypeId
                        };
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
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, $"{completed:N0} families imported." );
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

            ReportProgress( 0, $"Finished family import: {completed:N0} families added or updated." );
            return completed;
        }

        /// <summary>
        /// Saves all family changes.
        /// </summary>
        /// <param name="newFamilyList">The new family list.</param>
        /// <param name="newGroupLocations">The new group locations.</param>
        private void SaveFamilies( List<Group> newFamilyList, Dictionary<GroupLocation, string> newGroupLocations )
        {
            var rockContext = new RockContext();

            // First save any unsaved families
            if ( newFamilyList.Any() )
            {
                rockContext.WrapTransaction( () =>
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
                    var familyGroupId = ImportedFamilies.Where( g => g.ForeignKey == locationPair.Value ).Select( g => (int?)g.Id ).FirstOrDefault();
                    if ( familyGroupId != null )
                    {
                        locationPair.Key.GroupId = (int)familyGroupId;
                    }
                }

                // Save locations
                rockContext.WrapTransaction( () =>
                {
                    rockContext.GroupLocations.AddRange( newGroupLocations.Keys );
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }
    }
}
