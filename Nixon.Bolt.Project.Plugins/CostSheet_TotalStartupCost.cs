

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;


namespace Bolt.Project.Plugins
{
  public class CostSheet_TotalStartupCost : IPlugin
  {
    public static Decimal totalPMamount = 0.00M;
    public static Decimal totalKDamount = 0.00M;
    public static Decimal totalamount = 0.00M;
    private IOrganizationService service;
    private ITracingService tracingService;

    public void Execute(IServiceProvider serviceProvider)
    {
      this.tracingService = (ITracingService) serviceProvider.GetService(typeof (ITracingService));
      IPluginExecutionContext service = (IPluginExecutionContext) serviceProvider.GetService(typeof (IPluginExecutionContext));
      if (((DataCollection<string, object>) ((IExecutionContext) service).InputParameters).Contains("Target") && ((DataCollection<string, object>) ((IExecutionContext) service).InputParameters)["Target"] is Entity)
      {
        this.tracingService.Trace("A1", Array.Empty<object>());
        Entity inputParameter = (Entity) ((DataCollection<string, object>) ((IExecutionContext) service).InputParameters)["Target"];
        try
        {
          this.tracingService.Trace("A2", Array.Empty<object>());
          this.service = ((IOrganizationServiceFactory) serviceProvider.GetService(typeof (IOrganizationServiceFactory))).CreateOrganizationService(new Guid?(((IExecutionContext) service).UserId));
          if (!(((IExecutionContext) service).MessageName == "Create") && !(((IExecutionContext) service).MessageName == "Update"))
            return;
          Entity entity = this.service.Retrieve(inputParameter.LogicalName, inputParameter.Id, new ColumnSet(true));
          if (((DataCollection<string, object>) entity.Attributes).Contains("bolt_relatedcostsheet"))
          {
            Guid id = entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheet").Id;
            if (inputParameter.LogicalName != "bolt_plannedmaintenanceservice" || inputParameter.LogicalName != "bolt_kdservicemaintenance")
            {
              this.PMAmountcalculation(id);
              this.KDAmountcalculation(id);
              CostSheet_TotalStartupCost.totalamount = CostSheet_TotalStartupCost.totalKDamount + CostSheet_TotalStartupCost.totalPMamount;
              this.updateCostsheet(id);
            }
          }
        }
        catch (Exception ex)
        {
          this.tracingService.Trace("TotalStartupCostSheet: {0}", new object[1]
          {
            (object) ex.ToString()
          });
          throw;
        }
      }
      else
      {
        if (!(((IExecutionContext) service).MessageName.ToUpper() == "DELETE"))
          return;
        this.service = ((IOrganizationServiceFactory) serviceProvider.GetService(typeof (IOrganizationServiceFactory))).CreateOrganizationService(new Guid?(((IExecutionContext) service).UserId));
        Entity preEntityImage = ((DataCollection<string, Entity>) ((IExecutionContext) service).PreEntityImages)["preImage"];
        if (preEntityImage.Contains("bolt_relatedcostsheet"))
        {
          Guid id1 = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedcostsheet").Id;
          if (true)
          {
            Guid id2 = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedcostsheet").Id;
            this.PMAmountcalculation(id2);
            this.KDAmountcalculation(id2);
            CostSheet_TotalStartupCost.totalamount = CostSheet_TotalStartupCost.totalKDamount + CostSheet_TotalStartupCost.totalPMamount;
            this.updateCostsheet(id2);
          }
        }
      }
    }

    public void PMAmountcalculation(Guid id)
    {
      this.tracingService.Trace("1", Array.Empty<object>());
      int num = 0;
      Guid guid = id;
      QueryExpression queryExpression = new QueryExpression("bolt_plannedmaintenanceservice");
      queryExpression.ColumnSet.AddColumns(new string[1]
      {
        "bolt_totalcontractamount"
      });
      queryExpression.Criteria.AddCondition("statecode", (ConditionOperator) 0, new object[1]
      {
        (object) num
      });
      queryExpression.Criteria.AddCondition("bolt_relatedcostsheet", (ConditionOperator) 0, new object[1]
      {
        (object) guid
      });
      EntityCollection entityCollection = this.service.RetrieveMultiple((QueryBase) queryExpression);
      if (((Collection<Entity>) entityCollection.Entities).Count != 0)
      {
        CostSheet_TotalStartupCost.totalPMamount = 0.00M;
        for (int index = 0; index < ((Collection<Entity>) entityCollection.Entities).Count; ++index)
        {
          if (((DataCollection<string, object>) ((Collection<Entity>) entityCollection.Entities)[index].Attributes).Contains("bolt_totalcontractamount"))
            CostSheet_TotalStartupCost.totalPMamount += ((Money) ((Collection<Entity>) entityCollection.Entities)[index]["bolt_totalcontractamount"]).Value;
        }
      }
      else
        CostSheet_TotalStartupCost.totalPMamount = 0.00M;
      this.tracingService.Trace("2", Array.Empty<object>());
    }

    public void KDAmountcalculation(Guid id)
    {
      this.tracingService.Trace("3", Array.Empty<object>());
      int num = 1;
      Guid guid = id;
      QueryExpression queryExpression = new QueryExpression("bolt_kdservicemaintenance");
      queryExpression.ColumnSet.AddColumns(new string[1]
      {
        "bolt_totalkdcontractprice"
      });
      queryExpression.Criteria.AddCondition("statuscode", (ConditionOperator) 0, new object[1]
      {
        (object) num
      });
      queryExpression.Criteria.AddCondition("bolt_relatedcostsheet", (ConditionOperator) 0, new object[1]
      {
        (object) guid
      });
      EntityCollection entityCollection = this.service.RetrieveMultiple((QueryBase) queryExpression);
      if (((Collection<Entity>) entityCollection.Entities).Count != 0)
      {
        CostSheet_TotalStartupCost.totalKDamount = 0.00M;
        for (int index = 0; index < ((Collection<Entity>) entityCollection.Entities).Count; ++index)
        {
          if (((DataCollection<string, object>) ((Collection<Entity>) entityCollection.Entities)[index].Attributes).Contains("bolt_totalkdcontractprice"))
            CostSheet_TotalStartupCost.totalKDamount += ((Money) ((Collection<Entity>) entityCollection.Entities)[index]["bolt_totalkdcontractprice"]).Value;
        }
      }
      else
        CostSheet_TotalStartupCost.totalKDamount = 0.00M;
      this.tracingService.Trace("4", Array.Empty<object>());
    }

    public void updateCostsheet(Guid csid)
    {
      this.tracingService.Trace("5", Array.Empty<object>());
      this.service.Update(new Entity()
      {
        LogicalName = "quote",
        Id = csid,
        ["bolt_totalpmcost"] = (object) new Money(CostSheet_TotalStartupCost.totalamount)
      });
      CostSheet_TotalStartupCost.totalamount = 0.00M;
      this.tracingService.Trace("fina", Array.Empty<object>());
    }
  }
}
