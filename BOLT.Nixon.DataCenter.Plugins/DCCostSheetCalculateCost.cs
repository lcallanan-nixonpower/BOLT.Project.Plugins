using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace BOLT.Nixon.DataCenter.Plugins
{
    public class DCCostSheetCalculateCostPrice : IPlugin
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
                    // Obtain the post image entity from the input parameters.  
                    Entity entity = (Entity)context.PostEntityImages["post_image"];

                    // Get all cost values
                    Money generatorCost = entity.GetAttributeValue<Money>("bolt_generatorcost") ?? new Money(0);
                    Money miscCost1 = entity.GetAttributeValue<Money>("bolt_1misccost") ?? new Money(0);
                    Money miscCost2 = entity.GetAttributeValue<Money>("bolt_2misccost") ?? new Money(0);
                    Money miscCost3 = entity.GetAttributeValue<Money>("bolt_3misccost") ?? new Money(0);
                    Money miscCost4 = entity.GetAttributeValue<Money>("bolt_4misccost") ?? new Money(0);
                    Money miscCost5 = entity.GetAttributeValue<Money>("bolt_5misccost") ?? new Money(0);
                    Money miscCost6 = entity.GetAttributeValue<Money>("bolt_6misccost") ?? new Money(0);
                    Money tankCost = entity.GetAttributeValue<Money>("bolt_tankenclosurecost") ?? new Money(0);
                    Money breakerCost = entity.GetAttributeValue<Money>("bolt_breakercost") ?? new Money(0);
                    Money connectionCost = entity.GetAttributeValue<Money>("bolt_camlockconnectionboxcost") ?? new Money(0);
                    Money dpfCost = entity.GetAttributeValue<Money>("bolt_dpfcost") ?? new Money(0);
                    Money startupCost = entity.GetAttributeValue<Money>("bolt_totalstartupcost") ?? new Money(0);
                    Money freightCost = entity.GetAttributeValue<Money>("bolt_freightcost") ?? new Money(0);

                    // Set Total Cost as Money
                    Money totalCost = new Money(generatorCost.Value + miscCost1.Value + miscCost2.Value + miscCost3.Value + miscCost4.Value + miscCost5.Value + miscCost6.Value 
                                                +tankCost.Value + breakerCost.Value + connectionCost.Value + dpfCost.Value + startupCost.Value + freightCost.Value);

                    // Add Total Cost to Target entity
                    Entity update_entity = new Entity(entity.LogicalName, entity.Id);

                    if (update_entity.Contains("bolt_totalcost"))
                    {
                        update_entity["bolt_totalcost"] = totalCost;
                    }
                    else
                    {
                        update_entity.Attributes.Add("bolt_totalcost", totalCost);
                    }

                    service.Update(update_entity);
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
