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
using OrcaMDF.Core.MetaData;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the Email/Phone # import methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the communication data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapCommunication( IQueryable<Row> tableData )
        {
            // Individual_ID
            // Household_ID
            // Communication_Type
            // Communication_Value
            // Listed
            // Communication_Comment
            // LastUpdatedDate

            var attributeService = new AttributeService();

            //Communication type
            //Email	108888
            //Home Phone	90971
            //Mobile	73878
            //Infellowship Login	14407
            //Alternate Phone	8255
            //Work Phone	7819
            //Previous Phone	3280
            //Emergency Phone	1833

            // Look up additional Person attributes (existing)
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Add an Attribute for the secondary F1 Contribution Id
            var secondaryEmailAttribute = personAttributes.FirstOrDefault( a => a.Key == "F1SecondaryEmail" );
            if ( secondaryEmailAttribute == null )
            {
                secondaryEmailAttribute = new Rock.Model.Attribute();
                secondaryEmailAttribute.Key = "F1SecondaryEmail";
                secondaryEmailAttribute.Name = "F1 Secondary Email";
                secondaryEmailAttribute.FieldTypeId = TextFieldTypeId;
                secondaryEmailAttribute.EntityTypeId = PersonEntityTypeId;
                secondaryEmailAttribute.EntityTypeQualifierValue = string.Empty;
                secondaryEmailAttribute.EntityTypeQualifierColumn = string.Empty;
                secondaryEmailAttribute.Description = "The secondary email for FellowshipOne person that was imported";
                secondaryEmailAttribute.DefaultValue = string.Empty;
                secondaryEmailAttribute.IsMultiValue = false;
                secondaryEmailAttribute.IsRequired = false;
                secondaryEmailAttribute.Order = 0;

                attributeService.Add( secondaryEmailAttribute, ImportPersonAlias );
                attributeService.Save( secondaryEmailAttribute, ImportPersonAlias );
            }

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = totalRows / 100;
            ReportProgress( 0, Environment.NewLine + string.Format( "Starting batch import ({0} to import)...", totalRows ) );
            foreach ( var row in tableData )
            {
                int? batchId = row["BatchID"] as int?;
                if ( batchId != null )
                {
                    var batch = new FinancialBatch();
                    batch.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    batch.Status = batchStatusClosed;

                    string name = row["BatchName"] as string;
                    if ( name != null )
                    {
                        name = name.Trim();
                        batch.Name = name.Left( 50 );
                        batch.CampusId = CampusList.Where( c => name.StartsWith( c.Name ) || name.StartsWith( c.ShortCode ) )
                            .Select( c => (int?)c.Id ).FirstOrDefault();
                    }

                    DateTime? batchDate = row["BatchDate"] as DateTime?;
                    if ( batchDate != null )
                    {
                        batch.BatchStartDateTime = batchDate;
                        batch.BatchEndDateTime = batchDate;
                    }

                    decimal? amount = row["BatchAmount"] as decimal?;
                    if ( amount != null )
                    {
                        batch.ControlAmount = amount.HasValue ? amount.Value : new decimal();
                    }

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var batchService = new FinancialBatchService();
                        batchService.Add( batch, ImportPersonAlias );
                        batchService.Save( batch, ImportPersonAlias );

                        batch.Attributes = new Dictionary<string, AttributeCache>();
                        batch.Attributes.Add( "F1BatchId", secondaryEmailAttribute );
                        batch.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                        Rock.Attribute.Helper.SaveAttributeValue( batch, batchAttribute, batchId.ToString(), ImportPersonAlias );
                    } );

                    completed++;
                    if ( completed % 30 == 0 )
                    {
                        if ( completed % percentage != 0 )
                        {
                            ReportProgress( 0, "." );
                        }
                        else
                        {
                            int percentComplete = completed / percentage;
                            ReportProgress( percentComplete, Environment.NewLine + string.Format( "{0} records imported ({1}% complete)...", completed, percentComplete ) );
                        }
                    }
                }
            }

            ReportProgress( 100, Environment.NewLine + string.Format( "Finished communications import ({0} batches imported).", completed ) );
        }
    }
}
