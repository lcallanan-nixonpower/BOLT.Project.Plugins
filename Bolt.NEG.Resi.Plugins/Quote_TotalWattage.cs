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
   public class Quote_TotalWattage : IPlugin
    {
       
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
                if (entity.LogicalName == "bolt_loads")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                            if (!preImageInvoice.Attributes.Contains("quote"))
                                return;
                            relatedQuote_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;
                            Calculate_LoadWattage(relatedQuote_guid);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageInvoice = (Entity)context.PostEntityImages["Image"];
                            if (!postImageInvoice.Attributes.Contains("bolt_quote"))
                                return;
                            relatedQuote_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;
                            Calculate_LoadWattage(relatedQuote_guid);
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
                    if (!preImageInvoice.Attributes.Contains("bolt_quote"))
                        return;
                    relatedQuote_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;

                    Calculate_LoadWattage(relatedQuote_guid);
                }
            }
        }

        private void Calculate_LoadWattage(Guid QuoteID)
        {
            tracingService.Trace("11");
            // Define Condition Values
            // Define Condition Values
            var query_statuscode = 1;
            var query_bolt_lc = false;
            var query_quote = QuoteID;
           // var query_bolt_loadname = 454890021;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_loads");

            // Add all columns to query.ColumnSet
            query.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
            query.Criteria.AddCondition("bolt_lc", ConditionOperator.Equal, query_bolt_lc);
            query.Criteria.AddCondition("bolt_loadtype", ConditionOperator.NotNull);
            query.Criteria.AddCondition("bolt_quote", ConditionOperator.Equal, query_quote);


            EntityCollection Load_records = service.RetrieveMultiple(query);

            double wattage=0;
            double wattage_100 = 0;
            double wattage_40 = 0;

            double wattage_AcLoad = 0;

            if (Load_records.Entities.Count != 0)
            {
                for (int i = 0; i < Load_records.Entities.Count; i++) //AC Load
                {
                    if (Load_records.Entities[i].Attributes.Contains("bolt_100wattage") && ((Load_records.Entities[i].GetAttributeValue<bool>("bolt_acload")) == false))
                    {
                        wattage += (int)Load_records.Entities[i]["bolt_100wattage"];
                    }
                    if (Load_records.Entities[i].Attributes.Contains("bolt_100wattage") && ((Load_records.Entities[i].GetAttributeValue<bool>("bolt_acload")) == true))
                    {
                        wattage_AcLoad += (int)Load_records.Entities[i]["bolt_100wattage"];
                    }
                }            

            }
            if(wattage>10000) 
            {
                wattage_100 = 10000;
                wattage_40 = (wattage - 10000) * (0.4);
            }
            else
            {
                wattage_100 = wattage;
            }
           // tracingService.Trace("Total: {0}", costtotal);
            Entity resiquote = new Entity("quote");
            resiquote.Id = QuoteID;
            resiquote["bolt_100totalwattage"] = wattage_100;
            resiquote["bolt_40totalwattage"] = wattage_40;
            resiquote["bolt_acwattage"] = wattage_AcLoad;
            service.Update(resiquote);
            
        }
    }
}
