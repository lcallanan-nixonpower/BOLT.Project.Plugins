using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.BayCity.Plug.ins
{
    public class OpportunityLineCost : IPlugin
    {
        public static Decimal invYTDAmount = 0.00m;
        IOrganizationService service;
        ITracingService tracingService;
        Guid opportunity_guid;
        public void Execute(IServiceProvider serviceProvider)
        {
            //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                if (entity.LogicalName == "opportunityproduct")
                {
                    try
                    {
                        if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageMisc = (Entity)context.PostEntityImages["Image"];
                            if (!postImageMisc.Attributes.Contains("opportunityid"))
                                return;
                            opportunity_guid = (postImageMisc.GetAttributeValue<EntityReference>("opportunityid")).Id;
                            Calculate_Cost(opportunity_guid, postImageMisc, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Opp Line Plug-in", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete"&& context.PreEntityImages.Contains("Image"))
            {
                try
                {
                    Entity preImageMisc = (Entity)context.PreEntityImages["Image"];
                    if (!preImageMisc.Attributes.Contains("opportunityid"))
                        return;
                    opportunity_guid = (preImageMisc.GetAttributeValue<EntityReference>("opportunityid")).Id;
                    Calculate_Cost(opportunity_guid, preImageMisc, context);
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Opp Line Plug-in Delete Action", ex.ToString());
                    throw;
                }
            }
           
        }


        public void Calculate_Cost(Guid id, Entity Oppline, IPluginExecutionContext c)
        {
            // Define Condition Values
            var query_opportunityid = id;

            // Instantiate QueryExpression query
            var query = new QueryExpression("opportunityproduct");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("extendedamount", "new_extendedcost", "opportunityid", "priceperunit", "productname");

            // Define filter query.Criteria
            query.Criteria.AddCondition("opportunityid", ConditionOperator.Equal, query_opportunityid);
            //if(c.MessageName == "Delete")
            //{

            //}

            EntityCollection e = service.RetrieveMultiple(query);

            decimal cost = 0.0m;
          

            if (e.Entities.Count != 0)
            {
               for(int i = 0; i < e.Entities.Count; i++)
                {
                    if(e.Entities[i].Attributes.Contains("new_extendedcost"))
                    cost += ((Money)e.Entities[i]["new_extendedcost"]).Value;
                }
                             
             }

            Entity opp = new Entity("opportunity");
            opp.Id = opportunity_guid;

            opp["bolt_totalpartscost"] = cost;
            service.Update(opp);

        }

    }
}
