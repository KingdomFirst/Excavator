using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Rock;
using Rock.Data;
using Rock.Model;

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
        private int LoadMetrics( CsvDataModel csvData )
        {
            // Required variables
            var lookupContext = new RockContext();
            var metricService = new MetricService( lookupContext );

            var allMetrics = metricService.Queryable().AsNoTracking().ToList();
            var metricValues = new List<MetricValue>();

            var importDate = DateTime.Now;
            Metric currentMetric = null;
            int completed = 0;

            ReportProgress( 0, string.Format( "Starting metrics import ({0:N0} already exist).", 0 ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                string campus = row[MetricCampus];
                string metricName = row[MetricName];

                if ( metricName != null )
                {
                    currentMetric = allMetrics.FirstOrDefault( m => m.Title == metricName );
                    if ( currentMetric == null )
                    {
                        currentMetric = new Metric();
                        currentMetric.Title = metricName;
                        currentMetric.IsSystem = false;
                        currentMetric.Subtitle = string.Format( "{0} imported on {1}", metricName, importDate );
                        currentMetric.IsCumulative = false;
                        currentMetric.CreatedByPersonAliasId = ImportPersonAliasId;
                        currentMetric.CreatedDateTime = importDate;

                        metricService.Add( currentMetric );
                        lookupContext.SaveChangesAsync();

                        allMetrics.Add( currentMetric );
                    }

                    string value = row[MetricValue];
                    DateTime? valueDate = row[MetricService].AsDateTime();
                    string eventLabel = row[MetricLabel];

                    // save event label as the metric category?

                    var metricValue = new MetricValue();
                    metricValue.MetricValueType = MetricValueType.Measure;
                    metricValue.CreatedByPersonAliasId = ImportPersonAliasId;
                    metricValue.MetricId = currentMetric.Id;
                    metricValue.CreatedDateTime = importDate;
                    metricValue.MetricValueDateTime = valueDate;
                    metricValue.XValue = value;

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

            ReportProgress( 0, string.Format( "Finished family import: {0:N0} families added or updated.", completed ) );
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