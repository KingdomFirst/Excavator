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
            int workLocationTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK ) ).Id;

            var newGroupLocations = new Dictionary<GroupLocation, string>();
            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var updatedFamilyList = new List<Group>();

            var dateFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "MM/dd/yy" };

            string currentFamilyId = string.Empty;
            var importDate = DateTime.Now;
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

                    Location primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry );
                    if ( primaryAddress != null )
                    {
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
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
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

        /// <summary>
        /// Copy of Rock.Model.LocationService.Partial.cs\Get( address ) without the call to Verify()
        /// </summary>
        /// <param name="street1">A <see cref="System.String" /> representing the Address Line 1 to search by.</param>
        /// <param name="street2">A <see cref="System.String" /> representing the Address Line 2 to search by.</param>
        /// <param name="city">A <see cref="System.String" /> representing the City to search by.</param>
        /// <param name="state">A <see cref="System.String" /> representing the State to search by.</param>
        /// <param name="postalCode">A <see cref="System.String" /> representing the Zip/Postal code to search by</param>
        /// <param name="country">The country.</param>
        /// <returns>
        /// The first <see cref="Rock.Model.Location" /> where an address match is found, if no match is found a new <see cref="Rock.Model.Location" /> is created and returned.
        /// </returns>
        //public Location GetWithoutVerify( string street1, string street2, string city, string state, string postalCode, string country )
        //{
        //    var rockContext = new RockContext();
        //    var locationService = new LocationService( rockContext );

        //    // Make sure it's not an empty address
        //    if ( string.IsNullOrWhiteSpace( street1 ) &&
        //        string.IsNullOrWhiteSpace( street2 ) &&
        //        string.IsNullOrWhiteSpace( city ) &&
        //        string.IsNullOrWhiteSpace( state ) &&
        //        string.IsNullOrWhiteSpace( postalCode ) &&
        //        string.IsNullOrWhiteSpace( country ) )
        //    {
        //        return null;
        //    }

        //    // First check if a location exists with the entered values
        //    Location existingLocation = locationService.Queryable().FirstOrDefault( t =>
        //        ( t.Street1 == street1 || ( street1 == null && t.Street1 == null ) ) &&
        //        ( t.Street2 == street2 || ( street2 == null && t.Street2 == null ) ) &&
        //        ( t.City == city || ( city == null && t.City == null ) ) &&
        //        ( t.State == state || ( state == null && t.State == null ) ) &&
        //        ( t.PostalCode == postalCode || ( postalCode == null && t.PostalCode == null ) ) &&
        //        ( t.Country == country || ( country == null && t.Country == null ) ) );
        //    if ( existingLocation != null )
        //    {
        //        return existingLocation;
        //    }

        //    // If existing location wasn't found with entered values, try standardizing the values, and
        //    // search for an existing value again
        //    var newLocation = new Location
        //    {
        //        Street1 = street1,
        //        Street2 = street2,
        //        City = city,
        //        State = state,
        //        PostalCode = postalCode,
        //        Country = country
        //    };

        //    // Don't verify the location, this causes MEF to blow up
        //    // Verify( newLocation, false );

        //    existingLocation = locationService.Queryable().FirstOrDefault( t =>
        //        ( t.Street1 == newLocation.Street1 || ( newLocation.Street1 == null && t.Street1 == null ) ) &&
        //        ( t.Street2 == newLocation.Street2 || ( newLocation.Street2 == null && t.Street2 == null ) ) &&
        //        ( t.City == newLocation.City || ( newLocation.City == null && t.City == null ) ) &&
        //        ( t.State == newLocation.State || ( newLocation.State == null && t.State == null ) ) &&
        //        ( t.PostalCode == newLocation.PostalCode || ( newLocation.PostalCode == null && t.PostalCode == null ) ) &&
        //        ( t.Country == newLocation.Country || ( newLocation.Country == null && t.Country == null ) ) );

        //    if ( existingLocation != null )
        //    {
        //        return existingLocation;
        //    }

        //    // Create a new context/service so that save does not affect calling method's context
        //    locationService.Add( newLocation );
        //    rockContext.SaveChanges();

        //    // refetch it from the database to make sure we get a valid .Id
        //    return locationService.Get( newLocation.Guid );
        //}
    }
}