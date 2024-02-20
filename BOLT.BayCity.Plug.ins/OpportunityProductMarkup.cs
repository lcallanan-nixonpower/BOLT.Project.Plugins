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


    public class OpportunityProductMarkup : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;       
        decimal markup;
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
                if (entity.LogicalName == "opportunity")
                {
                    try
                    {                       
                            Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));

                         markup = (ent.GetAttributeValue<decimal>("bolt_partsmarkup"));
                        if (markup != null && markup > 0)
                        {
                            // Define Condition Values
                            var query_opportunityid = ent.Id;

                            // Instantiate QueryExpression query
                            var query = new QueryExpression("opportunityproduct");

                            // Add all columns to query.ColumnSet
                            query.ColumnSet.AllColumns = true;

                            // Define filter query.Criteria
                            query.Criteria.AddCondition("opportunityid", ConditionOperator.Equal, query_opportunityid);

                            EntityCollection p = service.RetrieveMultiple(query);

                            if(p.Entities.Count>0)
                            {
                                for(int i = 0; i < p.Entities.Count; i++)
                                {
                                    if (p.Entities[i].Attributes.Contains("bolt_cost"))
                                    {
                                        decimal partcost = ((Money)p.Entities[i]["bolt_cost"]).Value;
                                        if(partcost>0)
                                        {
                                            decimal markupprice = partcost + (partcost*(markup/100));

                                            Entity Opp_Product = new Entity("opportunityproduct");
                                            Opp_Product.Id = p.Entities[i].Id;

                                            Opp_Product["ispriceoverridden"] = true;
                                            Opp_Product["priceperunit"] = markupprice;
                                            service.Update(Opp_Product);

                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Opp Line Plug-in", ex.ToString());
                        throw;
                    }
                }

            }
        }
        }
    }

