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
    public class Resi_CostSheet_TotalMiscCalculation : IPlugin
    {
        public static Decimal invYTDAmount = 0.00m;
        IOrganizationService service;
        ITracingService tracingService;
        Guid relatedCostsheet_guid;
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
                if (entity.LogicalName == "bolt_residentialmiscequipment")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageMisc = (Entity)context.PreEntityImages["Image"];
                            if (!preImageMisc.Attributes.Contains("bolt_relatedresidentialquote"))
                                return;
                            relatedCostsheet_guid = (preImageMisc.GetAttributeValue<EntityReference>("bolt_relatedresidentialquote")).Id;
                            Calculate_TotalAdder_Cost(relatedCostsheet_guid);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageMisc = (Entity)context.PostEntityImages["Image"];
                            if (!postImageMisc.Attributes.Contains("bolt_relatedresidentialquote"))
                                return;
                            relatedCostsheet_guid = (postImageMisc.GetAttributeValue<EntityReference>("bolt_relatedresidentialquote")).Id;
                            Calculate_TotalAdder_Cost(relatedCostsheet_guid);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("TotalAdderCost Plugin", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                    if (!preImageInvoice.Attributes.Contains("bolt_relatedresidentialquote"))
                        return;
                    relatedCostsheet_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialquote")).Id;

                    Calculate_TotalAdder_Cost(relatedCostsheet_guid);
                }
            }
        }


        private void Calculate_TotalAdder_Cost(Guid QuoteID)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode = 0;
            var query_quote = QuoteID;
       
            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_residentialmiscequipment");

            // Add all columns to query.ColumnSet
            query.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition("bolt_relatedresidentialquote", ConditionOperator.Equal, query_quote);            
            //query.Criteria.AddCondition("bolt_cost", ConditionOperator.NotNull);

            EntityCollection miscItems = service.RetrieveMultiple(query);

            decimal costtotal = 0.0m;
            decimal pricetotal = 0.0m;

            if (miscItems.Entities.Count != 0)
            {
                for (int i = 0; i < miscItems.Entities.Count; i++)
                {
                    if (miscItems.Entities[i].Attributes.Contains("bolt_misctotalcost"))
                    {
                        costtotal += ((Money)miscItems.Entities[i]["bolt_misctotalcost"]).Value;
                    }

                }
                for (int i = 0; i < miscItems.Entities.Count; i++)
                {
                    if (miscItems.Entities[i].Attributes.Contains("bolt_misctotalprice"))
                    {
                        pricetotal += ((Money)miscItems.Entities[i]["bolt_misctotalprice"]).Value;
                    }

                }

            }
            tracingService.Trace("Total: {0}", costtotal);
            Entity resiquote = new Entity("bolt_quote");
            resiquote.Id = QuoteID;
            resiquote["bolt_totalmiscequipmentcost"] = costtotal;
            resiquote["bolt_totalmiscequipmentprice"] = pricetotal;
            service.Update(resiquote);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;
        }

    }
}

