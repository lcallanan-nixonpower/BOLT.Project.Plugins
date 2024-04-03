using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Bolt.Project.Plugins
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
        decimal permajorPrice=0.00m;
        decimal perminorPrice = 0.00m;
        decimal loadbanktestPrice = 0.00m;
        decimal pergreenprice = 0.00m;
        int genSize;
        string ServiceName;
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
                    if (ent.LogicalName== "bolt_plannedmaintenanceservice" && ent.Attributes.Contains("bolt_generatormake") && ent.Attributes.Contains("bolt_generatorkw") && ent.Attributes.Contains("bolt_pmservicedescription"))
                    {
                        string genMake = (ent.FormattedValues["bolt_generatormake"]).ToUpper();
                        int fuelType = ent.GetAttributeValue<OptionSetValue>("bolt_fueltype").Value;
                        int travelDuration = ent.GetAttributeValue<OptionSetValue>("bolt_travelduration").Value;
                        if (genMake!="KOHLER"&&genMake!="CAT"&&genMake!="CUMMINS") //if make is otherthan kohler, CUMINS and CAT, then default genMake  to "KOHLER"
                        {
                            genMake = "KOHLER"; //all pm service descriptions starts with the Genmake(kohle,cat,cummins,) prefix.
                        }
                       
                        Get_ServicePrice(ent,genMake, fuelType, travelDuration);
                    }
                    // KD Maintenanc sERVICE
                   else if (ent.LogicalName == "bolt_kdservicemaintenance"  && ent.Attributes.Contains("bolt_kdkwsize") && ent.Attributes.Contains("bolt_servicedescription"))
                    {
                        int fuelType = ent.GetAttributeValue<OptionSetValue>("bolt_fueltype").Value;
                        int travelDuration = ent.GetAttributeValue<OptionSetValue>("bolt_travelduration").Value;

                        string prefix = "KD"; //since KD service has no generator make field, so defaulting it to KD. All KD service descriptions starts with the 'KD' prefix 

                        Get_ServicePrice(ent, prefix, fuelType, travelDuration);
                    }
                    else if (ent.LogicalName == "bolt_plannedmaintenanceservice" && ent.Attributes.Contains("bolt_loadbanktest") && ent.Attributes.Contains("bolt_generatorkw"))// this step is for to calculate loadbank price.
                    {
                        genSize = ent.GetAttributeValue<int>("bolt_generatorkw");
                        string columnName = ConstructKWSizeFieldName(genSize, ent);
                        //set the prices on Service record
                        if (columnName != null)
                            SetPrices(ent, columnName); // this method pulls loadbank price and updates PM Table.
                    }
                    else if(ent.LogicalName == "bolt_kdservicemaintenance" && ent.Attributes.Contains("bolt_loadbanktest") && ent.Attributes.Contains("bolt_kdkwsize"))// this step is for only to calculate loadbank price.
                    {
                        genSize = ent.GetAttributeValue<int>("bolt_kdkwsize");
                        string columnName = ConstructKWSizeFieldName(genSize, ent);
                        //set the prices on Service record
                        if(columnName!=null)
                        SetPrices(ent, columnName); // this method pulls loadbank price and updates KD Table.
                    }

                }
                catch(Exception ex)
                {
                    tracingService.Trace("PMServicePrice: {0}", ex.ToString());
                    throw;
                }
              }
        }

        //method to get PM service Price
        public void Get_ServicePrice(Entity serviceEntity, string prefix, int fuelType, int travDuration) //planned maintenance service entity
        {           
            if (serviceEntity.LogicalName == "bolt_plannedmaintenanceservice") //pm entity
            {
                 genSize = serviceEntity.GetAttributeValue<int>("bolt_generatorkw");
                 ServiceName = (serviceEntity.GetAttributeValue<EntityReference>("bolt_pmservicedescription")).Name;
            }
            else //kd entity
            {
                genSize = serviceEntity.GetAttributeValue<int>("bolt_kdkwsize");
                ServiceName = (serviceEntity.GetAttributeValue<EntityReference>("bolt_servicedescription")).Name;

                if(!ServiceName.ToUpper().StartsWith("2 YEAR"))
                {
                    prefix = "KOHLER"; //if service description doesnot start with KD, then this service price related KOHLER KD Price combination, refer service price docuemnt for more details
                }
            }

            string columnName = ConstructKWSizeFieldName(genSize,serviceEntity);      

            string pmservicepricingName = prefix.ToUpper() + " " + ServiceName;
            var query_bolt_fueltype = fuelType;
            var query_bolt_travelduration = travDuration;
            // Define Condition Values
            var query_bolt_name = pmservicepricingName;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_pmservicepricing");

            // Add all columns to query.ColumnSet
            query.ColumnSet.AllColumns = true;

            // Define filter query.Criteria
            query.Criteria.AddCondition("bolt_name", ConditionOperator.Equal, query_bolt_name);
            query.Criteria.AddCondition("bolt_fueltype", ConditionOperator.Equal, query_bolt_fueltype);
            query.Criteria.AddCondition("bolt_travelduration", ConditionOperator.Equal, query_bolt_travelduration);

            EntityCollection resultset = service.RetrieveMultiple(query);

            if(resultset.Entities.Count>0 && resultset.Entities[0].Attributes.Contains(columnName))
            {
                if (resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890000)//if price type is  Major
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;
                    permajorPrice = ((Money)(resultset.Entities[0][columnName])).Value;
                }
                else if (resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890003)//if price type is  Green
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;
                    pergreenprice = ((Money)(resultset.Entities[0][columnName])).Value;
                }
                else if((resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890001)) //If pricetype is Minor
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;
                    perminorPrice = ((Money)(resultset.Entities[0][columnName])).Value;
                }
                else if ((resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890002)) //If pricetype is Major + Minor
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;

                    if (resultset.Entities[0].Attributes.Contains("bolt_majorpricingreference"))
                    {
                        permajorPrice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_majorpricingreference")).Id, columnName);
                    }
                    if (resultset.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
                    {
                        perminorPrice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference")).Id,columnName);
                    }
                }
                else if ((resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890004)) //If pricetype is Green + Minor
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;

                    if (resultset.Entities[0].Attributes.Contains("bolt_greenpricingreference"))
                    {
                        pergreenprice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_greenpricingreference")).Id, columnName);
                    }
                    if (resultset.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
                    {
                        perminorPrice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference")).Id, columnName);
                    }
                }
                else if ((resultset.Entities[0].Attributes.Contains("bolt_pricetype") && (resultset.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype")).Value == 454890005)) //If pricetype is Major + Minor + Green
                {
                    servicePrice = ((Money)(resultset.Entities[0][columnName])).Value;

                    if (resultset.Entities[0].Attributes.Contains("bolt_majorpricingreference"))
                    {
                        permajorPrice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_majorpricingreference")).Id, columnName);
                    }
                    if (resultset.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
                    {
                        perminorPrice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference")).Id, columnName);
                    }
                    if (resultset.Entities[0].Attributes.Contains("bolt_greenpricingreference"))
                    {
                        pergreenprice = GetMajor_Minor_Price((resultset.Entities[0].GetAttributeValue<EntityReference>("bolt_greenpricingreference")).Id, columnName);
                    }
                }
                //set the prices on Service record
                SetPrices(serviceEntity, columnName);

            }          

        }

        //method to get major and minor price
        public decimal GetMajor_Minor_Price(Guid id,string fieldName) //get major or minor price if the service price is Major/Green + Minor
        {
            decimal price =0.00m;
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

            if (result.Entities.Count>0&&result.Entities[0].Attributes.Contains(fieldName))
            {
                price = ((Money)(result.Entities[0][fieldName])).Value;
            }

            return price;

        }

        //Method to get Load bank test price from the 'PM Service Pricing' entity
        public decimal GetLoadbankTestPrice(int lbttype, string fieldName ) //GET LOAD BANK TEST PRICE 
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

            if(resultingentities.Entities.Count>0)
            {
               if(resultingentities.Entities[0].Attributes.Contains(fieldName))
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
            Entity serviceEnt = new Entity(ent.LogicalName);

            serviceEnt.Id = ent.Id;

            serviceEnt["bolt_servicepricenew"] = servicePrice;
            serviceEnt["bolt_permajornew"] = permajorPrice;
            serviceEnt["bolt_perminornew"] = perminorPrice;
            serviceEnt["bolt_pergreenmajor"] = pergreenprice;

            //get LoadbanktestPrice from the PM service pricing table

            if (ent.Attributes.Contains("bolt_loadbanktest"))
            {
               var lbtType =  (ent.GetAttributeValue<OptionSetValue>("bolt_loadbanktest")).Value;

                if(lbtType== 454890000)//2hr (Pm/Kd table value)
                {
                    loadbanktestPrice = GetLoadbankTestPrice(454890002, fieldName); //45489002 = 2 hr
                }
                else//4hr
                {
                    loadbanktestPrice = GetLoadbankTestPrice(454890003,fieldName);// 4 hr name
                }
              
            }
            serviceEnt["bolt_loadbanktestpricenew"] = loadbanktestPrice;
            service.Update(serviceEnt);

        }
     
        //Method to generate the field name 
        public string ConstructKWSizeFieldName(int size,Entity e) //construct field name to get the pricefield from PM Service Pricing  using 'bolt_generatorkw'(PM Mainetanceservice)' field.
        {
            string fieldname = null;
            if (e.LogicalName == "bolt_plannedmaintenanceservice")
            {
                if (size <= 15)
                {
                    fieldname = "bolt_1_15kw";
                }
                else if (size >= 16 && size <= 29)
                {
                    fieldname = "bolt_16_29kw";
                }
                else if (size >= 30 && size <= 49)
                {
                    fieldname = "bolt_30_49kw";
                }
                else if (size >= 50 && size <= 75)
                {
                    fieldname = "bolt_50_75kw";
                }
                else if (size >= 76 && size <= 125)
                {
                    fieldname = "bolt_76_125kw";
                }
                else if (size >= 126 && size <= 150)
                {
                    fieldname = "bolt_126_150kw";
                }
                else if (size >= 151 && size <= 200)
                {
                    fieldname = "bolt_151_200kw";
                }
                else if (size >= 201 && size <= 250)
                {
                    fieldname = "bolt_201_250kw";
                }
                else if (size >= 251 && size <= 300)
                {
                    fieldname = "bolt_251_300kw";
                }
                else if (size >= 301 && size <= 350)
                {
                    fieldname = "bolt_301_350kw";
                }
                else if (size >= 351 && size <= 400)
                {
                    fieldname = "bolt_351_400kw";
                }
                else if (size >= 401 && size <= 450)
                {
                    fieldname = "bolt_401_450kw";
                }
                else if (size >= 451 && size <= 500)
                {
                    fieldname = "bolt_451_500kw";
                }
                else if (size >= 501 && size <= 750)
                {
                    fieldname = "bolt_600_750kw";
                }
                else if (size >= 800 && size <= 1000)
                {
                    fieldname = "bolt_800_1000kw";

                }
                else if (size >= 1100 && size <= 1500)
                {
                    fieldname = "bolt_1100_1500kw";

                }
                else if (size >= 1600 && size <= 2000)
                {
                    fieldname = "bolt_1600_2000kw";

                }
                else if (size >= 2001 && size <= 2250)
                {

                    fieldname = "bolt_2001_2250kw";
                }
                else if (size >= 2251 && size <= 2500)
                {
                    fieldname = "bolt_2251_2500kw";

                }
                else if (size >= 2501 && size <= 2800)
                {
                    fieldname = "bolt_2501_2800kw";

                }
                else if (size >= 2801 && size <= 3000)
                {

                    fieldname = "bolt_2801_3000kw";
                }
                else if (size >= 3001 && size <= 3250)
                {
                    fieldname = "bolt_3001_3250kw";

                }
            }
            else
            {
                if (size >= 800 && size <=1000)
                {
                    fieldname = "bolt_kd800_1000";
                }
                else if (size >= 1250 && size <= 1750)
                {
                    fieldname = "bolt_kd1250_1750";
                }
                else if (size >= 2000 && size <= 2500)
                {
                    fieldname = "bolt_kd2000_2500";
                }
                else if (size >= 3000 && size <= 3200)
                {
                    fieldname = "bolt_kd2000_3200";
                }
            }
            return fieldname;
        }
    }
}
