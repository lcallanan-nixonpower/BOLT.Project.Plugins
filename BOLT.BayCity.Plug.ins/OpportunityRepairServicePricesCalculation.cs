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
    public class OpportunityRepairServicePricesCalculation : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;
        Guid opportunity_guid;
       
        public void Execute(IServiceProvider serviceProvider)
        { //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                if (entity.LogicalName == "bolt_opportunityservices")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageMisc = (Entity)context.PreEntityImages["Image"];
                            if (!preImageMisc.Attributes.Contains("bolt_opportunity"))
                                return;

                            int serviceType = (preImageMisc.GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value; //Labor:454890001,Mileage:454890002, Travel:454890003, Misc:454890004

                            Calculate_Price(opportunity_guid, serviceType, context.MessageName,context.PrimaryEntityId);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageMisc = (Entity)context.PostEntityImages["Image"];
                            if (!postImageMisc.Attributes.Contains("bolt_opportunity"))
                                return;
                            opportunity_guid = (postImageMisc.GetAttributeValue<EntityReference>("bolt_opportunity")).Id;
                            int serviceType = (postImageMisc.GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value; //Labor:454890001,Mileage:454890002, Travel:454890003, Misc:454890004

                            Calculate_Price(opportunity_guid, serviceType, context.MessageName,context.PrimaryEntityId);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Opp Line Plug-in", ex.ToString());
                        throw;
                    }
                }

            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageMisc = (Entity)context.PreEntityImages["Image"];
                    if (!preImageMisc.Attributes.Contains("bolt_opportunity"))
                        return;
                    opportunity_guid = (preImageMisc.GetAttributeValue<EntityReference>("bolt_opportunity")).Id;
                    int serviceType = (preImageMisc.GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value; //Labor:454890001,Mileage:454890002, Travel:454890003, Misc:454890004, sublet:454980005

                    Calculate_Price(opportunity_guid, serviceType, context.MessageName, context.PrimaryEntityId);
                }
            }
        }

        public void Calculate_Price(Guid oppId, int servType, string messageName, Guid servieID)
        {
            // Define Condition Values
            var query_statuscode = 1;
            //  var query_bolt_servicetype = servType;
            var query_bolt_opportunity = oppId;
            var query_bolt_opportunityservicesid = servieID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_opportunityservices");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("tb_totlaborhours", "bolt_cost", "bolt_price", "bolt_servicetype", "bolt_totallineamount", "bolt_linecost", "bolt_fuelsurcharge", "bolt_mileageamount", "bolt_travelcharge", "bolt_travelchargecost", "bolt_shopsupplyenvfee", "bolt_traveltime","bolt_miles");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
            //query.Criteria.AddCondition("bolt_servicetype", ConditionOperator.Equal, query_bolt_servicetype);
            query.Criteria.AddCondition("bolt_opportunity", ConditionOperator.Equal, query_bolt_opportunity);
            
            if(messageName == "Delete")
            query.Criteria.AddCondition("bolt_opportunityservicesid", ConditionOperator.NotEqual, query_bolt_opportunityservicesid);
         

            EntityCollection e = service.RetrieveMultiple(query);

            decimal laborpriceTotal = 0.0m;
            decimal laborcostTotal = 0.00m;

            decimal subletPriceTotal = 0.00m;
            decimal subletCosttotal = 0.00m;

            decimal milegaeTotal = 0.00m;
            decimal travelTotal = 0.00m;
            decimal travelcostTotal = 0.00m;
            decimal miscTotal = 0.00m;

            decimal fuelsurcharge = 0.00m;

            decimal shopSupplyfee = 0.00m;

            decimal totLaborhours = 0.00m;
            decimal totalTravelTime = 0.0m;
            decimal avgTravelTime =0.00m;
            int numberofServiceswithTraveltime = 0;
            decimal avgLaborhours = 0.00m;

            if (e.Entities.Count != 0)
            {
                for (int i = 0; i < e.Entities.Count; i++)  //price
                {
                    if (e.Entities[i].Attributes.Contains("bolt_mileageamount"))//Mileage
                    {

                        milegaeTotal += ((Money)e.Entities[i]["bolt_mileageamount"]).Value;
                        if (e.Entities[i].Attributes.Contains("bolt_fuelsurcharge"))
                            fuelsurcharge += ((Money)e.Entities[i]["bolt_fuelsurcharge"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_totallineamount") && e.Entities[i].Attributes.Contains("bolt_servicetype") && ((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890000)
                    {
                        laborpriceTotal += ((Money)e.Entities[i]["bolt_totallineamount"]).Value;
                        totLaborhours += Convert.ToDecimal(e.Entities[i]["tb_totlaborhours"]);
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_travelcharge") )  //Travel total
                    {
                        travelTotal += ((Money)e.Entities[i]["bolt_travelcharge"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_totallineamount") && e.Entities[i].Attributes.Contains("bolt_servicetype") && ((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890004)
                    {
                        miscTotal += ((Money)e.Entities[i]["bolt_totallineamount"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_totallineamount") && e.Entities[i].Attributes.Contains("bolt_servicetype") && ((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890005)
                    {
                        subletPriceTotal += ((Money)e.Entities[i]["bolt_totallineamount"]).Value;
                    }
                    if(e.Entities[i].Attributes.Contains("bolt_shopsupplyenvfee"))
                    {
                        shopSupplyfee += ((Money)e.Entities[i]["bolt_shopsupplyenvfee"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_traveltime"))
                    {
                        totalTravelTime += Convert.ToDecimal(e.Entities[i]["bolt_traveltime"]);
                        numberofServiceswithTraveltime += 1;
                    }

                }
                for (int i = 0; i < e.Entities.Count; i++) //cost
                {
                    if (e.Entities[i].Attributes.Contains("bolt_linecost") && e.Entities[i].Attributes.Contains("bolt_servicetype") && (((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890000))
                    {
                        laborcostTotal += ((Money)e.Entities[i]["bolt_linecost"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_travelchargecost") && e.Entities[i].Attributes.Contains("bolt_servicetype") && (((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890000))
                    {
                        travelcostTotal += ((Money)e.Entities[i]["bolt_travelchargecost"]).Value;
                    }
                    if (e.Entities[i].Attributes.Contains("bolt_linecost") && e.Entities[i].Attributes.Contains("bolt_servicetype") && (((e.Entities[i].GetAttributeValue<OptionSetValue>("bolt_servicetype")).Value) == 454890005))
                    {
                        subletCosttotal += ((Money)e.Entities[i]["bolt_linecost"]).Value;
                    }                   
                }
                avgTravelTime = (totalTravelTime)/Convert.ToDecimal(e.Entities.Count);
                avgLaborhours = (totLaborhours) / Convert.ToDecimal(e.Entities.Count);
            }
           

            Entity opportunity = new Entity("opportunity");
            opportunity.Id = opportunity_guid;

            opportunity["bolt_laborprice"] = laborpriceTotal;
            opportunity["bolt_mileage"] = milegaeTotal;
            opportunity["bolt_travel"] = travelTotal;
            opportunity["bolt_travelcost"] = travelcostTotal;
            opportunity["bolt_laborcost"] = laborcostTotal;
            opportunity["bolt_repairservicemiscprice"] = miscTotal;
            opportunity["bolt_subletprice"] = subletPriceTotal;
            opportunity["bolt_subletcost"] = subletCosttotal;
            opportunity["bolt_fuelsurcharge"] = fuelsurcharge;
            opportunity["bolt_shopsupenvfee"] = shopSupplyfee;
            opportunity["bolt_avgtraveltime"] = totalTravelTime;
            opportunity["bolt_avglaborhours"] = totLaborhours;

            service.Update(opportunity);


        }


    }
}
