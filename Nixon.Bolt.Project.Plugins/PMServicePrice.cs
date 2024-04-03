// Decompiled with JetBrains decompiler
// Type: Bolt.Project.Plugins.PMServicePrice
// Assembly: Bolt.Project.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c5dd37b51df8cd13
// MVID: 48324824-43E0-49A4-B57F-A15C46C732D4
// Assembly location: C:\Users\abbur\OneDrive\Documents\Plugins\Bolt.Project.Plugins_1.0.0.0.dll

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;


namespace Bolt.Project.Plugins
{
  public class PMServicePrice : IPlugin
  {
    private IOrganizationService service;
    private ITracingService tracingService;
    private Decimal servicePrice = 0.00M;
    private Decimal permajorPrice = 0.00M;
    private Decimal perminorPrice = 0.00M;
    private Decimal loadbanktestPrice = 0.00M;
    private Decimal pergreenprice = 0.00M;
    private int genSize;
    private string ServiceName;

    public void Execute(IServiceProvider serviceProvider)
    {
      tracingService = (ITracingService) serviceProvider.GetService(typeof (ITracingService));
      IPluginExecutionContext context = (IPluginExecutionContext) serviceProvider.GetService(typeof (IPluginExecutionContext));
      if (!(context.InputParameters).Contains("Target") || !((context.InputParameters)["Target"] is Entity))
        return;
      tracingService.Trace("A1");
      Entity ent = (Entity) (context.InputParameters)["Target"];
      try
      {
        tracingService.Trace("A2");
                // Obtain the IOrganizationService instance which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                 service = serviceFactory.CreateOrganizationService(context.UserId);
                Entity serviceEntity = service.Retrieve(ent.LogicalName, ent.Id, new ColumnSet(true));
        if (serviceEntity.LogicalName == "bolt_plannedmaintenanceservice" && serviceEntity.Attributes.Contains("bolt_generatormake") && serviceEntity.Attributes.Contains("bolt_generatorkw") && serviceEntity.Attributes.Contains("bolt_pmservicedescription"))
        {
          string prefix = serviceEntity.FormattedValues["bolt_generatormake"].ToUpper();
            int fuelType = serviceEntity.GetAttributeValue<OptionSetValue>("bolt_fueltype").Value;
            int travelDuration = serviceEntity.GetAttributeValue<OptionSetValue>("bolt_travelduration").Value;
                    if (prefix != "KOHLER" && prefix != "CAT" && prefix != "CUMMINS")
            prefix = "KOHLER";
          Get_ServicePrice(serviceEntity, prefix,fuelType,travelDuration);
        }
        else if (serviceEntity.LogicalName == "bolt_kdservicemaintenance" && serviceEntity.Attributes.Contains("bolt_kdkwsize") && serviceEntity.Attributes.Contains("bolt_servicedescription"))
        {
            int fuelType = serviceEntity.GetAttributeValue<OptionSetValue>("bolt_fueltype").Value;
            int travelDuration = serviceEntity.GetAttributeValue<OptionSetValue>("bolt_travelduration").Value;
            string prefix = "KD";
          Get_ServicePrice(serviceEntity, prefix, fuelType, travelDuration);
        }
      }
      catch (Exception ex)
      {
        tracingService.Trace("PMServicePrice: {0}", new object[1]
        {
           ex.ToString()
        });
        throw;
      }
    }

    public void Get_ServicePrice(Entity serviceEntity, string prefix,int fuelType, int travDuration)
    {
      if (serviceEntity.LogicalName == "bolt_plannedmaintenanceservice")
      {
        genSize = serviceEntity.GetAttributeValue<int>("bolt_generatorkw");
        ServiceName = serviceEntity.GetAttributeValue<EntityReference>("bolt_pmservicedescription").Name;
      }
      else
      {
        genSize = serviceEntity.GetAttributeValue<int>("bolt_kdkwsize");
        ServiceName = serviceEntity.GetAttributeValue<EntityReference>("bolt_servicedescription").Name;
        if (!ServiceName.ToUpper().StartsWith("2 YEAR"))
          prefix = "KOHLER";
      }
      string fieldName = ConstructKWSizeFieldName(genSize, serviceEntity);
    
            var query_bolt_name = prefix.ToUpper() + " " + ServiceName;
            var query_bolt_fueltype = fuelType;
            var query_bolt_travelduration = travDuration;
            QueryExpression queryExpression = new QueryExpression("bolt_pmservicepricing");
                queryExpression.ColumnSet.AllColumns = true;
            // Add conditions to query.Criteria
            queryExpression.Criteria.AddCondition("bolt_name", ConditionOperator.Equal, query_bolt_name);
            queryExpression.Criteria.AddCondition("bolt_fueltype", ConditionOperator.Equal, query_bolt_fueltype);
            queryExpression.Criteria.AddCondition("bolt_travelduration", ConditionOperator.Equal, query_bolt_travelduration);
   
      EntityCollection entityCollection = service.RetrieveMultiple(queryExpression);

      if (entityCollection.Entities.Count <= 0 || !entityCollection.Entities[0].Attributes.Contains(fieldName))
        return;
      if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890000)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        permajorPrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
      }
      else if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890003)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        pergreenprice = ((Money)entityCollection.Entities[0][fieldName]).Value;
      }
      else if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890001)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        perminorPrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
      }
      else if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890002)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        if (entityCollection.Entities[0].Attributes.Contains("bolt_majorpricingreference"))
          permajorPrice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_majorpricingreference").Id, fieldName);
        if (entityCollection.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
          perminorPrice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference").Id, fieldName);
      }
      else if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890004)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        if (entityCollection.Entities[0].Attributes.Contains("bolt_greenpricingreference"))
          pergreenprice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_greenpricingreference").Id, fieldName);
        if (entityCollection.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
          perminorPrice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference").Id, fieldName);
      }
      else if (entityCollection.Entities[0].Attributes.Contains("bolt_pricetype") && entityCollection.Entities[0].GetAttributeValue<OptionSetValue>("bolt_pricetype").Value == 454890005)
      {
        servicePrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
        if (entityCollection.Entities[0].Attributes.Contains("bolt_majorpricingreference"))
          permajorPrice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_majorpricingreference").Id, fieldName);
        if (entityCollection.Entities[0].Attributes.Contains("bolt_minorpricingreference"))
          perminorPrice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_minorpricingreference").Id, fieldName);
        if (entityCollection.Entities[0].Attributes.Contains("bolt_greenpricingreference"))
          pergreenprice = GetMajor_Minor_Price(entityCollection.Entities[0].GetAttributeValue<EntityReference>("bolt_greenpricingreference").Id, fieldName);
      }
      SetPrices(serviceEntity, fieldName);
    }

    public Decimal GetMajor_Minor_Price(Guid id, string fieldName)
    {
      Decimal majorMinorPrice = 0.00M;
      Guid guid = id;
      QueryExpression queryExpression = new QueryExpression("bolt_pmservicepricing");
      queryExpression.ColumnSet.AllColumns = true;
      queryExpression.Criteria.AddCondition("bolt_pmservicepricingid", 0, new object[1]
      {
         guid
      });
      EntityCollection entityCollection = service.RetrieveMultiple(queryExpression);
      if (entityCollection.Entities.Count > 0 && entityCollection.Entities[0].Attributes.Contains(fieldName))
        majorMinorPrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
      return majorMinorPrice;
    }

    public Decimal GetLoadbankTestPrice(int lbttype, string fieldName)
    {
      Decimal loadbankTestPrice = 0.00M;
      int num = lbttype;
      QueryExpression queryExpression = new QueryExpression("bolt_pmservicepricing");
      queryExpression.ColumnSet.AllColumns = true;
      queryExpression.Criteria.AddCondition("bolt_service", 0, new object[1]
      {
         num
      });
      EntityCollection entityCollection = service.RetrieveMultiple(queryExpression);
      if (entityCollection.Entities.Count > 0 && entityCollection.Entities[0].Attributes.Contains(fieldName))
        loadbankTestPrice = ((Money)entityCollection.Entities[0][fieldName]).Value;
      return loadbankTestPrice;
    }

    public void SetPrices(Entity ent, string fieldName)
    {
      Entity entity = new Entity(ent.LogicalName);
      entity.Id = ent.Id;
      entity["bolt_servicepricenew"] = servicePrice;
      entity["bolt_permajornew"] = permajorPrice;
      entity["bolt_perminornew"] = perminorPrice;
      entity["bolt_pergreenmajor"] = pergreenprice;
      if (ent.Attributes.Contains("bolt_loadbanktest"))
        loadbanktestPrice = ent.GetAttributeValue<OptionSetValue>("bolt_loadbanktest").Value != 454890000 ? GetLoadbankTestPrice(454890003, fieldName) : GetLoadbankTestPrice(454890002, fieldName);
      entity["bolt_loadbanktestpricenew"] = loadbanktestPrice;
      service.Update(entity);
    }

    public string ConstructKWSizeFieldName(int size, Entity e)
    {
      string str = null;
      if (e.LogicalName == "bolt_plannedmaintenanceservice")
      {
        if (size <= 15)
          str = "bolt_1_15kw";
        else if (size >= 16 && size <= 29)
          str = "bolt_16_29kw";
        else if (size >= 30 && size <= 49)
          str = "bolt_30_49kw";
        else if (size >= 50 && size <= 75)
          str = "bolt_50_75kw";
        else if (size >= 76 && size <= 125)
          str = "bolt_76_125kw";
        else if (size >= 126 && size <= 150)
          str = "bolt_126_150kw";
        else if (size >= 151 && size <= 200)
          str = "bolt_151_200kw";
        else if (size >= 201 && size <= 250)
          str = "bolt_201_250kw";
        else if (size >= 251 && size <= 300)
          str = "bolt_251_300kw";
        else if (size >= 301 && size <= 350)
          str = "bolt_301_350kw";
        else if (size >= 351 && size <= 400)
          str = "bolt_351_400kw";
        else if (size >= 401 && size <= 450)
          str = "bolt_401_450kw";
        else if (size >= 451 && size <= 500)
          str = "bolt_451_500kw";
        else if (size >= 501 && size <= 750)
          str = "bolt_600_750kw";
        else if (size >= 800 && size <= 1000)
          str = "bolt_800_1000kw";
        else if (size >= 1100 && size <= 1500)
          str = "bolt_1100_1500kw";
        else if (size >= 1600 && size <= 2000)
          str = "bolt_1600_2000kw";
        else if (size >= 2001 && size <= 2250)
          str = "bolt_2001_2250kw";
        else if (size >= 2251 && size <= 2500)
          str = "bolt_2251_2500kw";
        else if (size >= 2501 && size <= 2800)
          str = "bolt_2501_2800kw";
        else if (size >= 2801 && size <= 3000)
          str = "bolt_2801_3000kw";
        else if (size >= 3001 && size <= 3250)
          str = "bolt_3001_3250kw";
      }
      else if (size >= 800 && size <= 1000)
        str = "bolt_kd800_1000";
      else if (size >= 1250 && size <= 1750)
        str = "bolt_kd1250_1750";
      else if (size >= 2000 && size <= 2500)
        str = "bolt_kd2000_2500";
      else if (size >= 3000 && size <= 3200)
        str = "bolt_kd2000_3200";
      return str;
    }
  }
}
