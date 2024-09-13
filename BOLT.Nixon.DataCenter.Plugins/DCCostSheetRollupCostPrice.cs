using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace BOLT.Nixon.DataCenter.Plugins
{
    public class DCCostSheetRollupCostPrice : IPlugin
    {
        /// <summary>
        /// A plugin that rolls up the Cost and Price fields from Cost Sheet to Project for Data Center entities.
        /// </summary>
        /// <remarks>
        /// Entity: bolt_datacentercostsheet (Data Center Cost Sheet)
        /// Message, Stage, Order, Mode: Create, PostOperation, 1, Synchronous, Filter - bolt_quotedprice, bolt_totalcost
        /// Image: Post Image - bolt_primary, bolt_project, bolt_quotedprice, bolt_totalcost
        /// Message, Stage, Order, Mode: Update, PostOperation, 1, Synchronous, Filter - bolt_primary, bolt_quotedprice, bolt_totalcost
        /// Image: Post Image - bolt_primary, bolt_project, bolt_quotedprice, bolt_totalcost
        /// </remarks>
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
                    // Obtain the post image entity from the input parameters.  
                    Entity entity = (Entity)context.PostEntityImages["post_image"];

                    // Check if Data Center CS is primary
                    if (entity.GetAttributeValue<bool>("bolt_primary"))
                    {
                        if (entity.Attributes.Contains("bolt_project"))
                        {
                            // Get Project reference from image
                            EntityReference projectRef = entity.GetAttributeValue<EntityReference>("bolt_project");

                            // Convert Project reference to entity type
                            Entity project = new Entity(projectRef.LogicalName, projectRef.Id);

                            // Add attributes to update
                            project.Attributes.Add("new_cost", entity.GetAttributeValue<Money>("bolt_totalcost"));
                            project.Attributes.Add("new_salesrev", entity.GetAttributeValue<Money>("bolt_quotedprice"));

                            // Update related project
                            service.Update(project);
                        }
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in BOLT.Nixon.DataCenter.Plugins.DCCostSheetRollupCostPrice plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("BOLT.Nixon.DataCenter.Plugins.DCCostSheetRollupCostPrice plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
