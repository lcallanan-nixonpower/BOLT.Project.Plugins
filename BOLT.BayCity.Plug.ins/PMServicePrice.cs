﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
namespace BOLT.BayCity.Plug.ins
{
    public class PMServicePrice : IPlugin
    {
        /// <summary>
        /// A plugin that setsup service price on the Planned mainetanace, KD maintenance, Special pricing entities.
        /// PM service pricing table has the prices 
        /// 
        /// </summary>
        /// <remarks>
        /// Post Operation execution stage, and ASynchronous execution mode.
        /// </remar
        /// ks>
        IOrganizationService service;
        ITracingService tracingService;
        decimal servicePrice = 0.00m;
        decimal permajorPrice = 0.00m;
        decimal perminorPrice = 0.00m;
        decimal loadbanktestPrice = 0.00m;       
        int genSize;
        string ServiceName;
        string breakdownpricekw;
        Entity pmentity;
        public void Execute(IServiceProvider serviceProvider)
        {

            //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                tracingService.Trace("A1");
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {

                    tracingService.Trace("A2");
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    service = serviceFactory.CreateOrganizationService(context.UserId);

                    Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));

                    // PLANEED Maintenanc sERVICE
                    if (ent.LogicalName == "bolt_plannedmaintenanceservice" && ent.Attributes.Contains("bolt_generatormake") && ent.Attributes.Contains("bolt_generatorkw") && ent.Attributes.Contains("bolt_pmservicedescription"))
                    {
                        string genMake = (ent.FormattedValues["bolt_generatormake"]).ToUpper(); 
                        bool breakdownrequired = ent.GetAttributeValue<bool>("bolt_breakdownpricerequired");
                        if (breakdownrequired is false)
                            return;

                        if (genMake != "KOHLER" && genMake != "CAT" && genMake != "ONAN" && genMake !="SPECTRUM" && genMake !="GENERAC") //if make is otherthan kohler, CUMINS and CAT, then default genMake  to "KOHLER"
                        {
                            genMake = "GENERAC"; //all pm service descriptions starts with the Genmake(kohle,cat,ONAN,) prefix.
                        }
                        else if(genMake == "SPECTRUM")
                        {
                            genMake = "KOHLER";
                        }
                        else if (genMake == "OTHER")
                        {
                            genMake = "GENERAC";
                        }

                        Get_ServicePrice(ent, genMake);
                    }
                    // KD Maintenanc sERVICE
                    else if (ent.LogicalName == "bolt_kdservicemaintenance" && ent.Attributes.Contains("bolt_kdkwsize") && ent.Attributes.Contains("bolt_servicedescription"))
                    {
                        string prefix = "KD"; //since KD service has no generator make field, so defaulting it to KD. All KD service descriptions starts with the 'KD' prefix 

                        Get_ServicePrice(ent, prefix);
                    }

                }
                catch (Exception ex)
                {
                    tracingService.Trace("PMServicePrice: {0}", ex.ToString());
                    throw;
                }
            }
        }

        //method to get PM service Price
        public void Get_ServicePrice(Entity serviceEntity, string prefix) //planned maintenance service entity
        {
            int ft=000000;//fuel type
            int milesrange=0000000;//miles range
            if (serviceEntity.LogicalName == "bolt_plannedmaintenanceservice") //pm entity
            {
                genSize = serviceEntity.GetAttributeValue<int>("bolt_generatorkw");
                ServiceName = (serviceEntity.GetAttributeValue<EntityReference>("bolt_pmservicedescription")).Name;
                 ft =  (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_fueltype")).Value; //fuel type : Gas or Diesel
               milesrange = (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_milesrange")).Value; //miles range : 0-50,51-100

            }
            else //kd entity
            {
                genSize = serviceEntity.GetAttributeValue<int>("bolt_kdkwsize");
                ServiceName = (serviceEntity.GetAttributeValue<EntityReference>("bolt_servicedescription")).Name;
                milesrange = (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_milesrange")).Value;
                ft = 454890001;//defaulting to Diesel
            }

            string columnName = ConstructKWSizeFieldName(genSize, serviceEntity);
            if (columnName == "incorrect kwsize")//stop the execution if the wrong kw is entered
                return;
                  
                if (serviceEntity.Attributes.Contains("bolt_pricetype") && (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890000)//if price type is  Major
                {                   
                    GetMajorServicePriceBreakdown(prefix, ft, milesrange, serviceEntity);
                }
                 else if ((serviceEntity.Attributes.Contains("bolt_pricetype") && (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890001)) //If pricetype is Minor
                {                   
                    GetMinorServicePriceBreakdown(prefix, ft, milesrange, serviceEntity);
                }
                else if ((serviceEntity.Attributes.Contains("bolt_pricetype") && (serviceEntity.GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890002)) //If pricetype is Major + Minor
                {
                  
                        GetMajorServicePriceBreakdown(prefix, ft, milesrange, serviceEntity);
                        GetMinorServicePriceBreakdown(prefix, ft, milesrange, serviceEntity);
                    
                }

                SetPrices(serviceEntity, columnName); 


        }

        //method to get major and minor price
        public decimal GetMajor_Minor_Price(Guid id, string fieldName) //get major or minor price if the service price is Major/Green + Minor
        {
            decimal price = 0.00m;
            // Define Condition Values
            var query2_bolt_pmservicepricingid = id;

            // Instantiate QueryExpression query
            var query2 = new QueryExpression("bolt_pmservicepricing");

            // Add columns to query.ColumnSet
            // Add all columns to query.ColumnSet
            query2.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query2.Criteria.AddCondition("bolt_pmservicepricingid", ConditionOperator.Equal, query2_bolt_pmservicepricingid);

            EntityCollection result = service.RetrieveMultiple(query2);

            if (result.Entities.Count > 0 && result.Entities[0].Attributes.Contains(fieldName))
            {
                price = ((Money)(result.Entities[0][fieldName])).Value;
            }

            return price;

        }

        //Method to get Load bank test price from the 'PM Service Pricing' entity
        public decimal GetLoadbankTestPrice(int lbttype, string fieldName) //GET LOAD BANK TEST PRICE 
        {
            decimal lbtPrice = 0.00m;

            // Define Condition Values
            var query3_bolt_service = lbttype;

            // Instantiate QueryExpression query
            var query3 = new QueryExpression("bolt_pmservicepricing");

            // Add all columns to query.ColumnSet
            query3.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query3.Criteria.AddCondition("bolt_service", ConditionOperator.Equal, query3_bolt_service);

            EntityCollection resultingentities = service.RetrieveMultiple(query3);

            if (resultingentities.Entities.Count > 0)
            {
                if (resultingentities.Entities[0].Attributes.Contains(fieldName))
                {
                    lbtPrice = ((Money)(resultingentities.Entities[0][fieldName])).Value;
                }
            }
            return lbtPrice;

        }

        //Method to update Planned mainetanace entity
        //Planned Maintenance Entity'
        public void SetPrices(Entity ent, string fieldName)
        {
            // pmentity = new Entity(ent.LogicalName);

            //pmentity.Id = ent.Id;

           // pmentity["bolt_servicepricenew"] = servicePrice;
            pmentity["bolt_permajornew"] = permajorPrice;
            pmentity["bolt_perminornew"] = perminorPrice;
                      

            //get LoadbanktestPrice from the PM service pricing table

            if (ent.Attributes.Contains("bolt_loadbanktest"))
            {
                var lbtType = (ent.GetAttributeValue<OptionSetValue>("bolt_loadbanktest")).Value;

                if (lbtType == 454890000)//2hr
                {
                    loadbanktestPrice = GetLoadbankTestPrice(454890002, fieldName); //45489002 = 2 hr (pm service pricing optionset value)
                    GetLoadbankPriceBreakdown("2 hour LBT");
                }               
                
                else if(lbtType == 454890001)//4hr
                {
                    loadbanktestPrice = GetLoadbankTestPrice(454890003, fieldName);// 4 hr name(pm service pricing optionset value)
                    GetLoadbankPriceBreakdown("4 hour LBT");
                }
                else
                {
                    loadbanktestPrice = GetLoadbankTestPrice(454890004, fieldName);// 1 hr name(pm service pricing optionset value)
                    GetLoadbankPriceBreakdown("1 hour LBT");
                }

            }
            pmentity["bolt_loadbanktestpricenew"] = loadbanktestPrice;
            service.Update(pmentity);

        }

        //method to get price break down

        //Method to get price break down
        public void GetMinorServicePriceBreakdown(string make , int fueltype,int milesrange, Entity ent) //bolt_pmservicepricingbreakdown
        {
            if (pmentity == null)
            {//if only minor service exists 
                pmentity = new Entity(ent.LogicalName);
                pmentity.Id = ent.Id;
            }
            

            // Define Condition Values
            var query4_bolt_name = "Minor";
            var query4_bolt_kw = breakdownpricekw;
            var query4_bolt_fueltype = fueltype;
            var query4_bolt_milesrange = milesrange;
            var query4_bolt_status = 1;

            // Instantiate QueryExpression query
            var query4 = new QueryExpression("bolt_pmservicepricingbreakdown");

            // Add columns to query.ColumnSet
            // Add all columns to query.ColumnSet
            query4.ColumnSet.AllColumns = true;
            //query4.ColumnSet.AddColumns("bolt_ba", "bolt_freight", "bolt_kw", "bolt_labor", "bolt_mileage", "bolt_name", "bolt_partsmultiplier", "bolt_partssell", "bolt_pmservicepricingbreakdownid", "bolt_shopsupply", "bolt_total", "statecode");

            // Define filter query.Criteria
            query4.Criteria.AddCondition("bolt_name", ConditionOperator.Equal, query4_bolt_name);
            query4.Criteria.AddCondition("bolt_kw", ConditionOperator.Equal, query4_bolt_kw);
            query4.Criteria.AddCondition("bolt_fueltype", ConditionOperator.Equal, query4_bolt_fueltype);
            query4.Criteria.AddCondition("bolt_milesrange", ConditionOperator.Equal, query4_bolt_milesrange);
            query4.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query4_bolt_status);

            EntityCollection breakdownpriceresults = service.RetrieveMultiple(query4);

            if(breakdownpriceresults.Entities.Count>0)
            {
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_partssell"))
                {
                    pmentity["bolt_minorpartssell"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_partssell"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_partssellcost"))
                {
                    pmentity["bolt_minorpartssellcost"]= ((Money)(breakdownpriceresults.Entities[0]["bolt_partssellcost"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_labor"))
                {
                    pmentity["bolt_minorlabor"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_labor"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_laborcost"))
                {
                    pmentity["bolt_minorlaborcost"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_laborcost"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_shopsupply"))
                {
                    pmentity["bolt_minorshopsupply"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_shopsupply"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_shopsupplycost"))
                {
                    pmentity["bolt_minorshopsupplycost"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_shopsupplycost"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_mileage"))
                {
                    pmentity["bolt_minormileage"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_mileage"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_mileagecost"))
                {
                    pmentity["bolt_minormileagecost"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_mileagecost"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_freight"))
                {
                    pmentity["bolt_minorfreight"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_freight"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_freightcost"))
                {
                    pmentity["bolt_minorfreightcost"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_freightcost"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_ba"))
                {
                    pmentity["bolt_minorba"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_ba"])).Value;
                }               
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_total"))
                {
                    pmentity["bolt_minortotal"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_total"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_totalcost"))
                {
                    pmentity["bolt_minortotalcost"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_totalcost"])).Value;
                }
                //04/08/2024
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_oftechs"))
                {
                    pmentity["bolt_minoroftechs"] = breakdownpriceresults.Entities[0]["bolt_oftechs"];
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_travellaborhours"))
                {
                    pmentity["bolt_minortravellaborhours"] = breakdownpriceresults.Entities[0]["bolt_travellaborhours"];
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_travelcostperhour"))
                {
                    pmentity["bolt_minortravelcostperhour"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_travelcostperhour"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_travelsellperhour"))
                {
                    pmentity["bolt_minortravelsellperhour"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_travelsellperhour"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_onsitelaborhourspertech"))
                {
                    pmentity["bolt_minoronsitelaborhourspertech"] = ((breakdownpriceresults.Entities[0]["bolt_onsitelaborhourspertech"]));
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_onsitesellperhour"))
                {
                    pmentity["bolt_minoronsitesellperhour"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_onsitesellperhour"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_onsitecostperhour"))
                {
                    pmentity["bolt_minoronsitecostperhour"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_onsitecostperhour"])).Value;
                }
                if (breakdownpriceresults.Entities[0].Attributes.Contains("bolt_misc"))
                {
                    pmentity["tb_minormisc"] = ((Money)(breakdownpriceresults.Entities[0]["bolt_misc"])).Value;
                }

            }

        }
       
        //Method to get minor service price breakdown
        public void GetMajorServicePriceBreakdown(string make, int fueltype, int milesrange, Entity ent) //bolt_pmservicepricingbreakdown
        {
            pmentity = new Entity(ent.LogicalName);

            pmentity.Id = ent.Id;

            // Define Condition Values
            var query5_bolt_name = make;
            var query5_bolt_kw = breakdownpricekw;
            var query5_bolt_fueltype = fueltype;
            var query5_bolt_milesrange = milesrange;
            var query5_bolt_status = 1;

            // Instantiate QueryExpression query
            var query5 = new QueryExpression("bolt_pmservicepricingbreakdown");

            // Add columns to query.ColumnSet
            // Add all columns to query.ColumnSet
            query5.ColumnSet.AllColumns = true;
            //query4.ColumnSet.AddColumns("bolt_ba", "bolt_freight", "bolt_kw", "bolt_labor", "bolt_mileage", "bolt_name", "bolt_partsmultiplier", "bolt_partssell", "bolt_pmservicepricingbreakdownid", "bolt_shopsupply", "bolt_total", "statecode");

            // Define filter query.Criteria
            query5.Criteria.AddCondition("bolt_name", ConditionOperator.Equal, query5_bolt_name);
            query5.Criteria.AddCondition("bolt_kw", ConditionOperator.Equal, query5_bolt_kw);
            query5.Criteria.AddCondition("bolt_fueltype", ConditionOperator.Equal, query5_bolt_fueltype);
            query5.Criteria.AddCondition("bolt_milesrange", ConditionOperator.Equal, query5_bolt_milesrange);
            query5.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query5_bolt_status);

            EntityCollection breakdownpriceresults_major = service.RetrieveMultiple(query5);

            if (breakdownpriceresults_major.Entities.Count > 0)
            {
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_partssell"))
                {
                    pmentity["bolt_partssell"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_partssell"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_partssellcost"))
                {
                    pmentity["bolt_partssellcost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_partssellcost"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_labor"))
                {
                    pmentity["bolt_labor"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_labor"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_laborcost"))
                {
                    pmentity["bolt_laborcost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_laborcost"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_shopsupply"))
                {
                    pmentity["bolt_shopsupply"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_shopsupply"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_shopsupplycost"))
                {
                    pmentity["bolt_shopsupplycost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_shopsupplycost"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_mileage"))
                {
                    pmentity["bolt_mileage"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_mileage"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_mileagecost"))
                {
                    pmentity["bolt_mileagecost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_mileagecost"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_freight"))
                {
                    pmentity["bolt_freight"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_freight"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_freightcost"))
                {
                    pmentity["bolt_freightcost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_freightcost"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_ba"))
                {
                    pmentity["bolt_ba"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_ba"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_total"))
                {
                    pmentity["bolt_total"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_total"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_totalcost"))
                {
                    pmentity["bolt_totalcost"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_totalcost"])).Value;
                }

                //-- 04/08/2024

                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_oftechs"))
                {
                    pmentity["bolt_oftechs"] = breakdownpriceresults_major.Entities[0]["bolt_oftechs"];
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_travellaborhours"))
                {
                    pmentity["bolt_travellaborhours"] = breakdownpriceresults_major.Entities[0]["bolt_travellaborhours"];
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_onsitelaborhourspertech"))
                {
                    pmentity["bolt_onsitelaborhourspertech"] = ((breakdownpriceresults_major.Entities[0]["bolt_onsitelaborhourspertech"]));
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_travelcostperhour"))
                {
                    pmentity["bolt_travelcostperhour"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_travelcostperhour"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_travelsellperhour"))
                {
                    pmentity["bolt_travelsellperhour"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_travelsellperhour"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_onsitesellperhour"))
                {
                    pmentity["bolt_onsitesellperhour"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_onsitesellperhour"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_onsitecostperhour"))
                {
                    pmentity["bolt_onsitecostperhour"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_onsitecostperhour"])).Value;
                }
                if (breakdownpriceresults_major.Entities[0].Attributes.Contains("bolt_misc"))
                {
                    pmentity["bolt_misc"] = ((Money)(breakdownpriceresults_major.Entities[0]["bolt_misc"])).Value;
                }
            }

        }

        //Method to get Loadbank service price breakdown
        public void GetLoadbankPriceBreakdown(string name) //bolt_pmservicepricingbreakdown
        {           

            // Define Condition Values
            var query6_bolt_name = name;
            var query6_bolt_kw = breakdownpricekw;
            var query6_bolt_status = 1;


            // Instantiate QueryExpression query
            var query6 = new QueryExpression("bolt_pmservicepricingbreakdown");

            // Add columns to query.ColumnSet
            // Add all columns to query.ColumnSet
            query6.ColumnSet.AllColumns = true;
            //query4.ColumnSet.AddColumns("bolt_ba", "bolt_freight", "bolt_kw", "bolt_labor", "bolt_mileage", "bolt_name", "bolt_partsmultiplier", "bolt_partssell", "bolt_pmservicepricingbreakdownid", "bolt_shopsupply", "bolt_total", "statecode");

            // Define filter query.Criteria
            query6.Criteria.AddCondition("bolt_name", ConditionOperator.Equal, query6_bolt_name);
            query6.Criteria.AddCondition("bolt_kw", ConditionOperator.Equal, query6_bolt_kw);
            query6.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query6_bolt_status);


            EntityCollection breakdownpriceresults_loadbank = service.RetrieveMultiple(query6);

            if (breakdownpriceresults_loadbank.Entities.Count > 0)
            {                
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_labor"))
                {
                    pmentity["bolt_loadbanklabor"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_labor"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_laborcost"))
                {
                    pmentity["bolt_loadbanklaborcost"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_laborcost"])).Value;
                }
                //--04/10/2024
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_kwrating"))
                {
                    pmentity["bolt_kwrating"] = breakdownpriceresults_loadbank.Entities[0]["bolt_kwrating"];
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_priceperkw"))
                {
                    pmentity["bolt_priceperkw"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_priceperkw"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_loadbankfee"))
                {
                    pmentity["bolt_loadbankfee"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_loadbankfee"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_setupteardownhourspertech"))
                {
                    pmentity["bolt_setupteardownhourspertech"] = ((breakdownpriceresults_loadbank.Entities[0]["bolt_setupteardownhourspertech"]));
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_setupteardowncosthour"))
                {
                    pmentity["bolt_setupteardowncosthour"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_setupteardowncosthour"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_setupteardownsellhour"))
                {
                    pmentity["bolt_setupteardownsellhour"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_setupteardownsellhour"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_lengthoftest"))
                {
                    pmentity["bolt_lengthoftest"] = ((breakdownpriceresults_loadbank.Entities[0]["bolt_lengthoftest"]));
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_testingcosthour"))
                {
                    pmentity["bolt_testingcosthour"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_testingcosthour"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_testingsellhour"))
                {
                    pmentity["bolt_testingsellhour"] = ((Money)(breakdownpriceresults_loadbank.Entities[0]["bolt_testingsellhour"])).Value;
                }
                if (breakdownpriceresults_loadbank.Entities[0].Attributes.Contains("bolt_oftechs"))
                {
                    pmentity["bolt_laboroftechs"] = breakdownpriceresults_loadbank.Entities[0]["bolt_oftechs"];
                }

            }

        }

        //Method to generate the field name 
        public string ConstructKWSizeFieldName(int size, Entity e) //construct field name to get the pricefield from PM Service Pricing  using 'bolt_generatorkw'(PM Mainetanceservice)' field.
        {
            string fieldname = "incorrect kwsize";
            if (e.LogicalName == "bolt_plannedmaintenanceservice")
            {
                if (size <= 15)
                {
                    fieldname = "bolt_1_15kw";
                    breakdownpricekw = "1-15";
                }
                else if (size >= 16 && size <= 29)
                {
                    fieldname = "bolt_16_29kw";
                    breakdownpricekw = "16-29";
                }
                else if (size >= 30 && size <= 49)
                {
                    fieldname = "bolt_30_49kw";
                    breakdownpricekw = "30-49";
                }
                else if (size >= 50 && size <= 75)
                {
                    fieldname = "bolt_50_75kw";
                    breakdownpricekw = "50-75";
                }
                else if (size >= 76 && size <= 125)
                {
                    fieldname = "bolt_76_125kw";
                    breakdownpricekw = "76-125";
                }
                else if (size >= 126 && size <= 150)
                {
                    fieldname = "bolt_126_150kw";
                    breakdownpricekw = "126-150";
                }
                else if (size >= 151 && size <= 200)
                {
                    fieldname = "bolt_151_200kw";
                    breakdownpricekw = "151-200";
                }
                else if (size >= 201 && size <= 250)
                {
                    fieldname = "bolt_201_250kw";
                    breakdownpricekw = "201-250";
                }
                else if (size >= 251 && size <= 300)
                {
                    fieldname = "bolt_251_300kw";
                    breakdownpricekw = "251-300";
                }
                else if (size >= 301 && size <= 350)
                {
                    breakdownpricekw = "301-350";
                    fieldname = "bolt_301_350kw";
                }
                else if (size >= 351 && size <= 400)
                {
                    fieldname = "bolt_351_400kw";
                    breakdownpricekw = "351-400";
                }             
                else if (size >= 401 && size <= 500)
                {
                    fieldname = "bolt_451_500kw";
                    breakdownpricekw = "401-500";
                }
                else if (size >= 501 && size <= 750)
                {
                    fieldname = "bolt_501_750kw";
                    breakdownpricekw = "501-750";
                }
                else if (size >= 751 && size <= 1000)
                {
                    fieldname = "bolt_800_1000kw";
                    breakdownpricekw = "751-1000";

                }
                else if (size >= 1001 && size <= 1500)
                {
                    fieldname = "bolt_1100_1500kw";
                    breakdownpricekw = "1001-1500";

                }
                else if (size >= 1501 && size <= 2000)
                {
                    fieldname = "bolt_1600_2000kw";
                    breakdownpricekw = "1501-2000";
                }
                else if (size >= 20001 && size <= 2250)
                {

                    fieldname = "bolt_2001_2250kw";
                    breakdownpricekw = "2001-2250";
                }
                else if (size >= 2251 && size <= 2500)
                {
                    fieldname = "bolt_2251_2500kw";
                    breakdownpricekw = "2251-2500";

                }
                else if (size >= 2501 && size <= 3000)
                {
                    fieldname = "bolt_2501_2800kw";
                    breakdownpricekw = "2501-3000";
                }
                //else if (size >= 3000 && size <= 3250)
                //{

                //    fieldname = "bolt_2801_3000kw";
                //    breakdownpricekw = "2801-3000";
                //}
                else if (size >= 3001 && size <= 3250)
                {
                    fieldname = "bolt_3001_3250kw";
                    breakdownpricekw = "3001-3250";

                }
            }
            else
            {
                if (size >= 700 && size <= 1000)
                {
                    fieldname = "bolt_kd800_1000";
                    breakdownpricekw = "700-1000";
                }
                else if (size >= 1250 && size <= 1750)
                {
                    fieldname = "bolt_kd1250_1750";
                    breakdownpricekw = "1250-1750";
                }
                else if (size >= 2000 && size <= 2500)
                {
                    fieldname = "bolt_kd2000_2500";
                    breakdownpricekw = "2000-2500";
                }
                else if (size >= 2800 && size <= 3250)
                {
                    fieldname = "bolt_kd2000_3200";
                    breakdownpricekw = "2800-3250";
                }
            }
           
            return fieldname;
        }
    }
}
