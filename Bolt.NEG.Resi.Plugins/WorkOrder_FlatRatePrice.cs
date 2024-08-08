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
    public class WorkOrder_FlatRatePrice : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;
        Guid relatedWO_guid;
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
                if (entity.LogicalName == "msdyn_workorderproduct")
                {
                    try
                    {                       
                      if (context.PostEntityImages.Contains("Image")) //update
                        {

                            Entity postImageWOP = (Entity)context.PostEntityImages["Image"];
                          
                            if (!postImageWOP.Attributes.Contains("msdyn_workorder")&&!postImageWOP.Attributes.Contains("msdyn_linestatus") && !postImageWOP.Attributes.Contains("msdyn_description") && !postImageWOP.Attributes.Contains("msdyn_estimateunitamount")&& !postImageWOP.Attributes.Contains("bolt_upsoldproduct"))
                                return;
                            
                            var productAmount = postImageWOP.GetAttributeValue<Money>("msdyn_estimateunitamount");  //estimated amount                            
                            relatedWO_guid = (postImageWOP.GetAttributeValue<EntityReference>("msdyn_workorder")).Id;                            

                            if (productAmount != null) //used and true
                            {

                                Calculate_TotalFlatLineProducts_Cost(relatedWO_guid);
                            }
                       
                         
                        }
                        if (context.PreEntityImages.Contains("Image")) //update
                        {

                            Entity preImageWOP = (Entity)context.PreEntityImages["Image"];

                            if (!preImageWOP.Attributes.Contains("msdyn_workorder") && !preImageWOP.Attributes.Contains("msdyn_linestatus") && !preImageWOP.Attributes.Contains("msdyn_description") && !preImageWOP.Attributes.Contains("msdyn_estimateunitamount") && !preImageWOP.Attributes.Contains("bolt_upsoldproduct"))
                                return;

                            var productAmount = preImageWOP.GetAttributeValue<Money>("msdyn_estimateunitamount");  //estimated amount                            
                            relatedWO_guid = (preImageWOP.GetAttributeValue<EntityReference>("msdyn_workorder")).Id;

                            if (productAmount != null) //used and true
                            {

                                Calculate_TotalFlatLineProducts_Cost(relatedWO_guid);
                            }


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
                    Entity preImageWOP = (Entity)context.PreEntityImages["Image"];
                    if (!preImageWOP.Attributes.Contains("msdyn_workorder") && !preImageWOP.Attributes.Contains("msdyn_linestatus") && !preImageWOP.Attributes.Contains("msdyn_description") && !preImageWOP.Attributes.Contains("msdyn_estimateunitamount") && !preImageWOP.Attributes.Contains("bolt_upsoldproduct"))
                        return;
                    var linestatus = preImageWOP.GetAttributeValue<OptionSetValue>("msdyn_linestatus"); //Line Status == Used                   
                    var productAmount = preImageWOP.GetAttributeValue<Money>("msdyn_estimateunitamount");  //estimated amount
                    var upsoldProduct = preImageWOP.GetAttributeValue<bool>("bolt_upsoldproduct"); //up sold product
                    relatedWO_guid = (preImageWOP.GetAttributeValue<EntityReference>("msdyn_workorder")).Id;
            
                    if (linestatus.Value == 690970001  && productAmount != null)
                    {

                        Calculate_TotalFlatLineProducts_Cost(relatedWO_guid);
                    }
                }
            }
        }

        private void Calculate_TotalFlatLineProducts_Cost(Guid WorderID) //used
        {
            // Define Condition Values
            var query_statuscode = 1; //active
           // var query_bolt_upsoldproduct = true;
           // var query_msdyn_linestatus = 690970001; //used
            var query_msdyn_workorder = WorderID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("msdyn_workorderproduct");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("msdyn_estimateunitamount", "msdyn_workorder", "bolt_upsoldproduct", "msdyn_estimatetotalamount","msdyn_linestatus", "msdyn_totalamount");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
           // query.Criteria.AddCondition("bolt_upsoldproduct", ConditionOperator.Equal, query_bolt_upsoldproduct);
           // query.Criteria.AddCondition("msdyn_linestatus", ConditionOperator.Equal, query_msdyn_linestatus);
            query.Criteria.AddCondition("msdyn_workorder", ConditionOperator.Equal, query_msdyn_workorder);

            EntityCollection wops = service.RetrieveMultiple(query);

            decimal upsoldcosttotal_used = 0.0m;
            decimal upsoldcosttotal_estimate = 0.0m;
            decimal costtotal_used = 0.0m;
            if (wops.Entities.Count != 0)
            {
                for (int i = 0; i < wops.Entities.Count; i++)
                {
                    // Calculate costs if line status is "used" and product amount is not null
                    if (wops.Entities[i].Attributes.Contains("msdyn_totalamount") && (wops.Entities[i].GetAttributeValue<OptionSetValue>("msdyn_linestatus")).Value == 690970001 && wops.Entities[i].GetAttributeValue<bool>("bolt_upsoldproduct") is true)
                    {
                        upsoldcosttotal_used += ((Money)wops.Entities[i]["msdyn_totalamount"]).Value;
                    }
                    else if  (wops.Entities[i].Attributes.Contains("msdyn_estimatetotalamount") && (wops.Entities[i].GetAttributeValue<OptionSetValue>("msdyn_linestatus")).Value == 690970000 && wops.Entities[i].GetAttributeValue<bool>("bolt_upsoldproduct") is true)
                    {
                        upsoldcosttotal_estimate += ((Money)wops.Entities[i]["msdyn_estimatetotalamount"]).Value;
                    }

                    if (wops.Entities[i].Attributes.Contains("msdyn_totalamount") && (wops.Entities[i].GetAttributeValue<OptionSetValue>("msdyn_linestatus")).Value == 690970001)
                    {
                        costtotal_used += ((Money)wops.Entities[i]["msdyn_totalamount"]).Value;
                    }
                }             

            }


            Entity wo = new Entity("msdyn_workorder");
            wo.Id = relatedWO_guid;
            wo["bolt_additionalproducts"] = upsoldcosttotal_used;
           wo["bolt_estimatedadditionalproducts"] = upsoldcosttotal_estimate;
            wo["bolt_usedproductstotal"] = costtotal_used;
            service.Update(wo);

        }

        //private void Calculate_TotalFlatLineProducts_Cost_EstimatedorDeleted(Guid WorderID, Money pAmount) //Estimate or productitem  deleted
        //{
        //    Entity worder = service.Retrieve("msdyn_workorder", relatedWO_guid, new ColumnSet(true));

        //    if (worder.Attributes.Contains("bolt_additionalproducts") && worder.Attributes.Contains("bolt_flatrate"))
        //    {
        //        var addAmount = worder.GetAttributeValue<Money>("bolt_additionalproducts");
        //       if (addAmount.Value == 0 || addAmount == null)
        //            return;
        //        var newAmount = addAmount.Value - pAmount.Value;

        //        Entity wo = new Entity("msdyn_workorder");
        //        wo.Id = relatedWO_guid;
        //        wo["bolt_additionalproducts"] = newAmount;

        //        service.Update(wo);

        //    }
        //}

    }
}
