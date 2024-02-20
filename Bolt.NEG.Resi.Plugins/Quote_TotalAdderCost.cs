using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Bolt.NEG.Resi.Plugins
{
    public class Quote_TotalAdderCost : IPlugin
    {
        public static Decimal invYTDAmount = 0.00m;
        IOrganizationService service;
        ITracingService tracingService;
        Guid relatedQuote_guid;
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
                if (entity.LogicalName == "bolt_residentialadders")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImage = (Entity)context.PreEntityImages["Image"];
                            if (!preImage.Attributes.Contains("quote"))
                                return;
                            relatedQuote_guid = (preImage.GetAttributeValue<EntityReference>("quote")).Id;
                            Calculate_TotalAdder_Cost(relatedQuote_guid);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImage = (Entity)context.PostEntityImages["Image"];
                            if (!postImage.Attributes.Contains("quote"))
                                return;
                            relatedQuote_guid = (postImage.GetAttributeValue<EntityReference>("quote")).Id;
                            Calculate_TotalAdder_Cost(relatedQuote_guid);
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
                    if (!preImageInvoice.Attributes.Contains("quote"))
                        return;
                    relatedQuote_guid = (preImageInvoice.GetAttributeValue<EntityReference>("quote")).Id;

                    Calculate_TotalAdder_Cost(relatedQuote_guid);
                }
            }
        }


        private void Calculate_TotalAdder_Cost(Guid QuoteID)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode = 0;
            var query_quote = QuoteID;
            var query_bolt_addtototalcost = true;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_residentialadders");

            // Add all columns to query.ColumnSet
            query.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition("quote", ConditionOperator.Equal, query_quote);
            query.Criteria.AddCondition("bolt_addtototalcost", ConditionOperator.Equal, query_bolt_addtototalcost);
            //query.Criteria.AddCondition("bolt_cost", ConditionOperator.NotNull);

            EntityCollection adders = service.RetrieveMultiple(query);

            decimal costtotal = 0.0m;
            decimal pricetotal = 0.0m;

            if (adders.Entities.Count != 0)
            {
                for (int i = 0; i < adders.Entities.Count; i++)
                {
                    if (adders.Entities[i].Attributes.Contains("bolt_actualcost")&& adders.Entities[i].Attributes.Contains("bolt_qty"))
                    {
                        costtotal +=( ((Money)adders.Entities[i]["bolt_actualcost"]).Value * (adders.Entities[i].GetAttributeValue<int>("bolt_qty")));
                    }
                   else if (adders.Entities[i].Attributes.Contains("bolt_actualcost"))
                    {
                        costtotal += ((Money)adders.Entities[i]["bolt_actualcost"]).Value;
                    }


                }
                for (int i = 0; i < adders.Entities.Count; i++)
                {
                    if (adders.Entities[i].Attributes.Contains("bolt_price") && adders.Entities[i].Attributes.Contains("bolt_qty"))
                    {
                        pricetotal += ((Money)adders.Entities[i]["bolt_price"]).Value * (adders.Entities[i].GetAttributeValue<int>("bolt_qty"));
                    }
                    else if (adders.Entities[i].Attributes.Contains("bolt_price"))
                    {
                        pricetotal += ((Money)adders.Entities[i]["bolt_price"]).Value;
                    }

                }

            }
            tracingService.Trace("Total: {0}", costtotal);
            Entity resiquote = new Entity("quote");
            resiquote.Id = QuoteID;
            resiquote["bolt_totaladdercost"] = costtotal;
            resiquote["bolt_totaladderprice"] = pricetotal;
            service.Update(resiquote);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;
        }

    }
}
