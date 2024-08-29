using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.Nixon.DataCenter.Plugins
{
    // Removed public designation since plugin is no longer required and won't be included when updating plugin package via PRT.
    class CostSheetAMountsCalculation : IPlugin
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
            tracingService.Trace("Pre-image invoiceid number}");
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                   if (entity.LogicalName == "bolt_costsheetequipment")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                            if (preImageInvoice.Attributes.Contains("bolt_quote"))
                            {
                                relatedQuote_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;
                                Calculate_Amount(relatedQuote_guid, "bolt_quote", "bolt_datacentercostsheet");
                            }
                            
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageInvoice = (Entity)context.PostEntityImages["Image"];
                            if (postImageInvoice.Attributes.Contains("bolt_quote"))
                            {
                                relatedQuote_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;
                                Calculate_Amount(relatedQuote_guid, "bolt_quote", "bolt_datacentercostsheet");
                            }
                           
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Invoice Amount Plugin", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                    if (preImageInvoice.Attributes.Contains("bolt_quote"))
                    {
                        relatedQuote_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_quote")).Id;

                        Calculate_Amount(relatedQuote_guid, "bolt_quote", "bolt_datacentercostsheet");
                    }                   
                }
            }
        }
        private void Calculate_Amount(Guid quoteID, string fieldName, string entityName)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statuscode = 1;
            var query_bolt_quote = quoteID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_costsheetequipment");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_description", "bolt_item", "bolt_name", "bolt_qty", "bolt_quote", "bolt_size", "bolt_totalcost");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
            query.Criteria.AddCondition("bolt_quote", ConditionOperator.Equal, query_bolt_quote);

            EntityCollection equipmentLines = service.RetrieveMultiple(query);

            decimal total = 0.0m;
            decimal totgeneratorCost = 0.0m;
            decimal totatsCost = 0.00m;
            decimal tottankenclosureCost = 0.00m;
            decimal totloadbankCost = 0.00m;
            decimal totsparepartsCost = 0.00m;
            decimal totmiscCost = 0.00m;
            decimal totconnectionCabinetCost = 0.00m;
            decimal totstartupCost = 0.00m;
            decimal totFreightCost = 0.00m;

            

            if (equipmentLines.Entities.Count != 0)
            {
                for (int i = 0; i < equipmentLines.Entities.Count; i++)
                {
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost")&& ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890000)//GEN
                    {
                        totgeneratorCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890001)//ATS
                    {
                        totatsCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890002)//Tankenclosure
                    {
                        tottankenclosureCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890003)//loadbank
                    {
                        totloadbankCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890004)//spareparts
                    {
                        totsparepartsCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890005)//misc
                    {
                        totmiscCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890006)//connectionCabinets
                    {
                        totconnectionCabinetCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890007)//startup
                    {
                        totstartupCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }
                    if (equipmentLines.Entities[i].Attributes.Contains("bolt_totalcost") && ((equipmentLines.Entities[i].GetAttributeValue<OptionSetValue>("bolt_item")).Value) == 454890008)//freight
                    {
                        totFreightCost += ((Money)equipmentLines.Entities[i]["bolt_totalcost"]).Value;
                    }

                }

            }
            tracingService.Trace("Total: {0}", total);
            Entity quote = new Entity(entityName);
            quote.Id = quoteID;
            quote["bolt_totalgeneratorcost"] = totgeneratorCost;
            quote["bolt_totalatscost"] =totatsCost;
            quote["bolt_totalmisccost"] =totmiscCost;
            quote["bolt_totalsparepartscost"] =totsparepartsCost;
            quote["bolt_totalconnectioncabinetscost"] =totconnectionCabinetCost;
            quote["bolt_totaltankenclosurecost"] =tottankenclosureCost;
            quote["bolt_totalloadbankcost"] =totloadbankCost;
            quote["bolt_totalstartupcost"] =totstartupCost;
            quote["bolt_totalfreightcost"] =totFreightCost;
            service.Update(quote);       

        }
    }
}
