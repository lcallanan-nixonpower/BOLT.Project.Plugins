// Decompiled with JetBrains decompiler
// Type: BOLT.BayCity.Plug.ins.OpportunityRepairServicePricesCalculation
// Assembly: BOLT.BayCity.Plug.ins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=209aa499da59708e
// MVID: 43AADC86-CE2A-44B6-8C51-82E317CADDA7
// Assembly location: C:\Users\abbur\Downloads\BOLT.BayCity.Plug.ins_1.0.0.0 1.dll

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;


namespace BOLT.BayCity.Plug.ins
{
    public class OpportunityRepairServicePricesCalculation : IPlugin
    {
        private IOrganizationService service;
        private ITracingService tracingService;
        private Guid opportunity_guid;

        public void Execute(IServiceProvider serviceProvider)
        {
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            service = ((IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(new Guid?(context.UserId));
            if ((context.InputParameters).Contains("Target") && (context.InputParameters)["Target"] is Entity)
            {
                if (!(((Entity)(context.InputParameters)["Target"]).LogicalName == "bolt_opportunityservices"))
                    return;
                try
                {
                    if (context.PreEntityImages.Contains("Image"))
                    {
                        Entity preEntityImage = context.PreEntityImages["Image"];
                        if (!(preEntityImage.Attributes).Contains("bolt_opportunity"))
                            return;
                        Calculate_Price(opportunity_guid, preEntityImage.GetAttributeValue<OptionSetValue>("bolt_servicetype").Value, context.MessageName, context.PrimaryEntityId);
                    }
                    else if (context.PostEntityImages.Contains("Image"))
                    {
                        Entity postEntityImage = context.PostEntityImages["Image"];
                        if (!(postEntityImage.Attributes).Contains("bolt_opportunity"))
                            return;
                        opportunity_guid = postEntityImage.GetAttributeValue<EntityReference>("bolt_opportunity").Id;
                        Calculate_Price(opportunity_guid, postEntityImage.GetAttributeValue<OptionSetValue>("bolt_servicetype").Value, context.MessageName, context.PrimaryEntityId);
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Opp Line Plug-in", new object[1]
                    {
                             ex.ToString()
                    });
                    throw;
                }
            }
            else
            {
                if (!(context.MessageName == "Delete") || !context.PreEntityImages.Contains("Image"))
                    return;
                Entity preEntityImage = context.PreEntityImages["Image"];
                if (!(preEntityImage.Attributes).Contains("bolt_opportunity"))
                    return;
                opportunity_guid = preEntityImage.GetAttributeValue<EntityReference>("bolt_opportunity").Id;
                Calculate_Price(opportunity_guid, preEntityImage.GetAttributeValue<OptionSetValue>("bolt_servicetype").Value, context.MessageName, context.PrimaryEntityId);
            }
        }

        public void Calculate_Price(Guid oppId, int servType, string messageName, Guid servieID)
        {
            int num1 = 1;
            Guid guid1 = oppId;
            Guid guid2 = servieID;
            QueryExpression queryExpression = new QueryExpression("bolt_opportunityservices");
            queryExpression.ColumnSet.AddColumns(new string[13]
            {
                "tb_totlaborhours",
                "bolt_cost",
                "bolt_price",
                "bolt_servicetype",
                "bolt_totallineamount",
                "bolt_linecost",
                "bolt_fuelsurcharge",
                "bolt_mileageamount",
                "bolt_travelcharge",
                "bolt_travelchargecost",
                "bolt_shopsupplyenvfee",
                "bolt_traveltime",
                "bolt_miles"
            });
            queryExpression.Criteria.AddCondition("statuscode", 0, new object[1]{ num1});
            queryExpression.Criteria.AddCondition("bolt_opportunity", 0, new object[1]{ guid1});
            if (messageName == "Delete")
                queryExpression.Criteria.AddCondition("bolt_opportunityservicesid", (ConditionOperator)1, new object[1] {guid2});
            EntityCollection entityCollection = service.RetrieveMultiple(queryExpression);
            Decimal num2 = 0.0M;
            Decimal num3 = 0.00M;
            Decimal num4 = 0.00M;
            Decimal num5 = 0.00M;
            Decimal num6 = 0.00M;
            Decimal num7 = 0.00M;
            Decimal num8 = 0.00M;
            Decimal num9 = 0.00M;
            Decimal num10 = 0.00M;
            Decimal num11 = 0.00M;
            Decimal num12 = 0.00M;
            Decimal num13 = 0.0M;
            Decimal num14 = 0.00M;
            int num15 = 0;
            Decimal num16 = 0.00M;
            Decimal num17 = 0.00M;
            if (entityCollection.Entities.Count != 0)
            {
                for (int index = 0; index < entityCollection.Entities.Count; ++index) //Price
                {
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_mileageamount"))
                    {
                        num6 += ((Money)entityCollection.Entities[index]["bolt_mileageamount"]).Value;
                        // if ((entityCollection.Entities[index].Attributes).Contains("bolt_fuelsurcharge"))
                       // num10 += ((Money)entityCollection.Entities[index]["bolt_fuelsurcharge"]).Value;
                    }
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_fuelsurcharge"))
                    {
                        num10 += ((Money)entityCollection.Entities[index]["bolt_fuelsurcharge"]).Value;
                    }
                        if ((entityCollection.Entities[index].Attributes).Contains("bolt_totallineamount") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890000)
                    {
                        num2 += ((Money)entityCollection.Entities[index]["bolt_totallineamount"]).Value;
                        num12 += Convert.ToDecimal(entityCollection.Entities[index]["tb_totlaborhours"]);
                    }
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_travelcharge"))
                        num7 += ((Money)entityCollection.Entities[index]["bolt_travelcharge"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_totallineamount") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890004)//misc
                        num9 += ((Money)entityCollection.Entities[index]["bolt_totallineamount"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_totallineamount") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890005)
                        num4 += ((Money)entityCollection.Entities[index]["bolt_totallineamount"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_shopsupplyenvfee"))
                        num11 += ((Money)entityCollection.Entities[index]["bolt_shopsupplyenvfee"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_traveltime"))
                    {
                        num13 += Convert.ToDecimal(entityCollection.Entities[index]["bolt_traveltime"]);
                        ++num15;
                    }
                }
                for (int index = 0; index < entityCollection.Entities.Count; ++index) //costs
                {
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_linecost") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890000)//regular 
                        num3 += ((Money)entityCollection.Entities[index]["bolt_linecost"]).Value;                    
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_travelchargecost"))//Travel Cost
                        num8 += ((Money)entityCollection.Entities[index]["bolt_travelchargecost"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_linecost") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890005)//sublet
                        num5 += ((Money)entityCollection.Entities[index]["bolt_linecost"]).Value;
                    if ((entityCollection.Entities[index].Attributes).Contains("bolt_linecost") && (entityCollection.Entities[index].Attributes).Contains("bolt_servicetype") && entityCollection.Entities[index].GetAttributeValue<OptionSetValue>("bolt_servicetype").Value == 454890004)//misc service type
                        num17 += ((Money)entityCollection.Entities[index]["bolt_linecost"]).Value;
                }
                //num14 = num13 / Convert.ToDecimal(entityCollection.Entities.Count);
                //  num16 = num12 / Convert.ToDecimal(entityCollection.Entities.Count);
            }
            service.Update(new Entity("opportunity")
            {
                Id = opportunity_guid,
                ["bolt_laborprice"] = num2,
                ["bolt_mileage"] = num6,
                ["bolt_travel"] = num7,
                ["bolt_travelcost"] = num8,
                ["bolt_laborcost"] = num3,
                ["bolt_repairservicemiscprice"] = num9,
                ["bolt_misccost"]=num17,
                ["bolt_subletprice"] = num4,
                ["bolt_subletcost"] = num5,
                ["bolt_fuelsurcharge"] = num10,
                ["bolt_shopsupenvfee"] = num11,
                ["bolt_avgtraveltime"] = num13,
                ["bolt_avglaborhours"] = num12
            });
        }
    }
}
