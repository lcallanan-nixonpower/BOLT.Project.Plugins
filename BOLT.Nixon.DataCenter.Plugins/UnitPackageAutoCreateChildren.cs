using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace BOLT.Nixon.DataCenter.Plugins
{
    public class UnitPackageAutoCreateChildren : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                //Entity entity = (Entity)context.InputParameters["Target"];

                // Obtain the IOrganizationService instance which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    // Create pointer to context target
                    Entity target = (Entity)context.InputParameters["Target"];

                    // Retrieve optionset values for Category field
                    var attributeRequest = new RetrieveAttributeRequest
                    {
                        EntityLogicalName = "bolt_datacenterpm",
                        LogicalName = "bolt_category",
                        RetrieveAsIfPublished = true
                    };
                    var attributeResponse = (RetrieveAttributeResponse)service.Execute(attributeRequest);
                    var attributeMetadata = (EnumAttributeMetadata)attributeResponse.AttributeMetadata;

                    // Initiate Entity Collection for related Data Center PMs
                    EntityCollection child_records = new EntityCollection();

                    // Loop through the options creating new child records and add to target Related Entities
                    foreach (OptionMetadata option in attributeMetadata.OptionSet.Options)
                    {
                        Entity DataCenterPM = new Entity("bolt_datacenterpm");
                        DataCenterPM["bolt_category"] = new OptionSetValue((int)option.Value);
                        DataCenterPM["bolt_po"] = option.Label.UserLocalizedLabel.Label;
                        DataCenterPM["ownerid"] = new Entity("systemuser", context.InitiatingUserId).ToEntityReference();

                        Guid recordID = service.Create(DataCenterPM);

                        child_records.Entities.Add(new Entity("bolt_datacenterpm", recordID));
                    }

                    // Add child records to target
                    target.RelatedEntities.Add(new Relationship("bolt_bolt_datacenterunitpackage_bolt_datacenterpm_RelatedPackage"), child_records);

                    service.Update(target);
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in BOLT.Nixon.DataCenter.Plugins.DCCostSheetCalculateCost plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("BOLT.Nixon.DataCenter.Plugins.DCCostSheetCalculateCost plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
