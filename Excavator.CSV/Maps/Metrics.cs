using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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
        private int LoadMetrics( CSVInstance csvData )
        {
            // Required variables
            var lookupContext = new RockContext();
            var metricService = new MetricService( lookupContext );
            var categoryService = new CategoryService( lookupContext );
            var metricSourceTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.METRIC_SOURCE_TYPE ) ).DefinedValues;
            var metricManualSource = metricSourceTypes.FirstOrDefault( m => m.Guid == new Guid( Rock.SystemGuid.DefinedValue.METRIC_SOURCE_VALUE_TYPE_MANUAL ) );

            var metricEntityTypeId = EntityTypeCache.Read<Rock.Model.MetricCategory>( false, lookupContext ).Id;
            var campusEntityTypeId = EntityTypeCache.Read<Rock.Model.Campus>( false, lookupContext ).Id;

            var campuses = CampusCache.All( lookupContext );
            var currentMetrics = metricService.Queryable().AsNoTracking().ToList();
            var metricCategories = categoryService.Queryable().AsNoTracking()
                .Where( c => c.EntityType.Guid == new Guid( Rock.SystemGuid.EntityType.METRICCATEGORY ) ).ToList();

            var defaultMetricCategory = metricCategories.FirstOrDefault( c => c.Name == "Metrics" );

            if ( defaultMetricCategory == null )
            {
                defaultMetricCategory = new Category();
                defaultMetricCategory.Name = "Metrics";
                defaultMetricCategory.IsSystem = false;
                defaultMetricCategory.EntityTypeId = metricEntityTypeId;
                defaultMetricCategory.EntityTypeQualifierColumn = string.Empty;
                defaultMetricCategory.EntityTypeQualifierValue = string.Empty;

                categoryService.Add( defaultMetricCategory );
                lookupContext.SaveChanges();
            }

            var metricValues = new List<MetricValue>();

            var importDate = DateTime.Now;
            Metric currentMetric = null;
            int completed = 0;

            ReportProgress( 0, string.Format( "Starting metrics import ({0:N0} already exist).", 0 ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                string metricCampus = row[MetricCampus];
                string metricName = row[MetricName];
                string metricCategory = row[MetricCategory];

                if ( metricName != null )
                {
                    decimal? value = row[MetricValue].AsDecimalOrNull();
                    DateTime? valueDate = row[MetricService].AsDateTime();
                    var metricCategoryId = defaultMetricCategory.Id;

                    // create the category if it doesn't exist
                    Category newMetricCategory = null;
                    if ( !string.IsNullOrEmpty( metricCategory ) )
                    {
                        newMetricCategory = metricCategories.FirstOrDefault( c => c.Name == metricCategory );
                        if ( newMetricCategory == null )
                        {
                            newMetricCategory = new Category();
                            newMetricCategory.Name = "Metrics";
                            newMetricCategory.IsSystem = false;
                            newMetricCategory.EntityTypeId = metricEntityTypeId;
                            newMetricCategory.EntityTypeQualifierColumn = string.Empty;
                            newMetricCategory.EntityTypeQualifierValue = string.Empty;

                            categoryService.Add( newMetricCategory );
                            lookupContext.SaveChanges();
                        }

                        metricCategoryId = newMetricCategory.Id;
                    }

                    // create metric if it doesn't exist
                    currentMetric = currentMetrics.FirstOrDefault( m => m.Title == metricName );
                    if ( currentMetric == null )
                    {
                        if ( valueDate.HasValue )
                        {
                            var timeFrame = (DateTime)valueDate;
                            if ( timeFrame.TimeOfDay.TotalSeconds > 0 )
                            {
                                metricName = string.Format( "{0} {1}", timeFrame.ToString( "HH:mm" ), metricName );
                            }
                        }

                        currentMetric = new Metric();
                        currentMetric.Title = metricName;
                        currentMetric.IsSystem = false;
                        currentMetric.IsCumulative = false;
                        currentMetric.SourceSql = string.Empty;
                        currentMetric.Description = string.Empty;
                        currentMetric.IconCssClass = string.Empty;
                        currentMetric.EntityTypeId = campusEntityTypeId;
                        currentMetric.SourceValueTypeId = metricManualSource.Id;
                        currentMetric.Subtitle = string.Format( "{0} imported on {1}", metricName, importDate );
                        currentMetric.CreatedByPersonAliasId = ImportPersonAliasId;
                        currentMetric.CreatedDateTime = importDate;
                        currentMetric.MetricCategories.Add( new MetricCategory { CategoryId = metricCategoryId } );

                        metricService.Add( currentMetric );
                        lookupContext.SaveChanges();

                        currentMetrics.Add( currentMetric );
                    }

                    var campusId = campuses.Where( c => c.Name == metricCampus || c.ShortCode == metricCampus )
                        .Select( c => (int?)c.Id ).FirstOrDefault();

                    // create values for this metric
                    var metricValue = new MetricValue();
                    metricValue.MetricValueType = MetricValueType.Measure;
                    metricValue.CreatedByPersonAliasId = ImportPersonAliasId;
                    metricValue.CreatedDateTime = importDate;
                    metricValue.MetricValueDateTime = valueDate;
                    metricValue.MetricId = currentMetric.Id;
                    metricValue.EntityId = campusId;
                    metricValue.Note = string.Empty;
                    metricValue.XValue = string.Empty;
                    metricValue.YValue = value;
                    metricValues.Add( metricValue );

                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} metrics imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveMetrics( metricValues );
                        ReportPartialProgress();

                        // Reset lookup context
                        lookupContext.SaveChanges();
                        lookupContext = new RockContext();
                        metricValues.Clear();
                    }
                }
            }

            // Check to see if any rows didn't get saved to the database
            if ( metricValues.Any() )
            {
                SaveMetrics( metricValues );
            }

            lookupContext.SaveChanges();
            lookupContext.Dispose();

            ReportProgress( 0, string.Format( "Finished metrics import: {0:N0} metrics added or updated.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves all the metric values.
        /// </summary>
        private void SaveMetrics( List<MetricValue> metricValues )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.MetricValues.AddRange( metricValues );
                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}