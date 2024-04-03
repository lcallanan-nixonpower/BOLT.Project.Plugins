

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;


namespace Bolt.Project.Plugins
{
  public class InvoiceFieldsCalculation : IPlugin
  {
    public static Decimal invLastMonthAmount = 0.00M;
    public static Decimal invThisMonthAmount = 0.00M;
    public static Decimal invYTDAmount = 0.00M;
    private IOrganizationService service;
    private ITracingService tracingService;
    private Guid relatedProject_guid;

    public void Execute(IServiceProvider serviceProvider)
    {
      this.tracingService = (ITracingService) serviceProvider.GetService(typeof (ITracingService));
      IPluginExecutionContext service = (IPluginExecutionContext) serviceProvider.GetService(typeof (IPluginExecutionContext));
      this.service = ((IOrganizationServiceFactory) serviceProvider.GetService(typeof (IOrganizationServiceFactory))).CreateOrganizationService(new Guid?(((IExecutionContext) service).UserId));
      this.tracingService.Trace("Pre-image invoiceid number}", Array.Empty<object>());
      if (((DataCollection<string, object>) ((IExecutionContext) service).InputParameters).Contains("Target") && ((DataCollection<string, object>) ((IExecutionContext) service).InputParameters)["Target"] is Entity)
      {
        Entity inputParameter = (Entity) ((DataCollection<string, object>) ((IExecutionContext) service).InputParameters)["Target"];
        if (inputParameter.LogicalName == "new_job")
        {
          try
          {
            InvoiceFieldsCalculation.UpdateAmount(inputParameter, this.service);
          }
          catch (Exception ex)
          {
            this.tracingService.Trace("Invoice Amount Plugin: {0}", new object[1]
            {
              (object) ex.ToString()
            });
            throw;
          }
        }
        else
        {
          if (!(inputParameter.LogicalName == "bolt_invoicing"))
            return;
          try
          {
            if (((DataCollection<string, Entity>) ((IExecutionContext) service).PreEntityImages).Contains("Image"))
            {
              Entity preEntityImage = ((DataCollection<string, Entity>) ((IExecutionContext) service).PreEntityImages)["Image"];
              if (((DataCollection<string, object>) preEntityImage.Attributes).Contains("bolt_relatedproject"))
              {
                this.relatedProject_guid = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedproject").Id;
                this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedproject", "new_job");
              }
              else if (((DataCollection<string, object>) preEntityImage.Attributes).Contains("bolt_relatedresidentialproject"))
              {
                this.relatedProject_guid = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject").Id;
                this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
              }
            }
            else if (((DataCollection<string, Entity>) ((IExecutionContext) service).PostEntityImages).Contains("Image"))
            {
              Entity postEntityImage = ((DataCollection<string, Entity>) ((IExecutionContext) service).PostEntityImages)["Image"];
              if (((DataCollection<string, object>) postEntityImage.Attributes).Contains("bolt_relatedproject"))
              {
                this.relatedProject_guid = postEntityImage.GetAttributeValue<EntityReference>("bolt_relatedproject").Id;
                this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedproject", "new_job");
              }
              else if (((DataCollection<string, object>) postEntityImage.Attributes).Contains("bolt_relatedresidentialproject"))
              {
                this.relatedProject_guid = postEntityImage.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject").Id;
                this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
              }
            }
          }
          catch (Exception ex)
          {
            this.tracingService.Trace("Invoice Amount Plugin", new object[1]
            {
              (object) ex.ToString()
            });
            throw;
          }
        }
      }
      else
      {
        if (!(((IExecutionContext) service).MessageName == "Delete") || !((DataCollection<string, Entity>) ((IExecutionContext) service).PreEntityImages).Contains("Image"))
          return;
        Entity preEntityImage = ((DataCollection<string, Entity>) ((IExecutionContext) service).PreEntityImages)["Image"];
        if (((DataCollection<string, object>) preEntityImage.Attributes).Contains("bolt_relatedproject"))
        {
          this.relatedProject_guid = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedproject").Id;
          this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedproject", "new_job");
        }
        else if (((DataCollection<string, object>) preEntityImage.Attributes).Contains("bolt_relatedresidentialproject"))
        {
          this.relatedProject_guid = preEntityImage.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject").Id;
          this.Calculate_Invoice_Amount(this.relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
        }
      }
    }

    private static void UpdateAmount(Entity entity, IOrganizationService service)
    {
      Entity entity1 = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
      DateTime attributeValue1 = entity1.GetAttributeValue<DateTime>("bolt_progressbill1date");
      DateTime attributeValue2 = entity1.GetAttributeValue<DateTime>("bolt_progressbill2date");
      DateTime attributeValue3 = entity1.GetAttributeValue<DateTime>("bolt_progressbill3date");
      DateTime attributeValue4 = entity1.GetAttributeValue<DateTime>("bolt_progressbill4date");
      Decimal amount1 = entity1.GetAttributeValue<Money>("bolt_progressbill1") == null ? 0.00M : entity1.GetAttributeValue<Money>("bolt_progressbill1").Value;
      Decimal amount2 = entity1.GetAttributeValue<Money>("bolt_progressbill2") == null ? 0.00M : entity1.GetAttributeValue<Money>("bolt_progressbill2").Value;
      Decimal amount3 = entity1.GetAttributeValue<Money>("bolt_progressbill3") == null ? 0.00M : entity1.GetAttributeValue<Money>("bolt_progressbill3").Value;
      Decimal amount4 = entity1.GetAttributeValue<Money>("bolt_progressbill4") == null ? 0.00M : entity1.GetAttributeValue<Money>("bolt_progressbill4").Value;
      int month1 = DateTime.Today.Month;
      if (amount1 != 0.00M)
      {
        int month2 = attributeValue1.Month;
        InvoiceFieldsCalculation.CalculateAmounts(month1, month2, amount1);
      }
      if (amount2 != 0.00M)
      {
        int month3 = attributeValue2.Month;
        InvoiceFieldsCalculation.CalculateAmounts(month1, month3, amount2);
      }
      if (amount3 != 0.00M)
      {
        int month4 = attributeValue3.Month;
        InvoiceFieldsCalculation.CalculateAmounts(month1, month4, amount3);
      }
      if (amount4 != 0.00M)
      {
        int month5 = attributeValue4.Month;
        InvoiceFieldsCalculation.CalculateAmounts(month1, month5, amount4);
      }
      InvoiceFieldsCalculation.invYTDAmount = InvoiceFieldsCalculation.invLastMonthAmount + InvoiceFieldsCalculation.invThisMonthAmount + InvoiceFieldsCalculation.invYTDAmount;
      service.Update(new Entity("new_job")
      {
        Id = entity.Id,
        ["bolt_invoicedlastmonth"] = (object) InvoiceFieldsCalculation.invLastMonthAmount,
        ["bolt_invoicedthismonth"] = (object) InvoiceFieldsCalculation.invThisMonthAmount,
        ["bolt_invoicedthisyear"] = (object) InvoiceFieldsCalculation.invYTDAmount
      });
      InvoiceFieldsCalculation.invLastMonthAmount = 0.00M;
      InvoiceFieldsCalculation.invThisMonthAmount = 0.00M;
      InvoiceFieldsCalculation.invYTDAmount = 0.00M;
    }

    private static void CalculateAmounts(int cmon, int mon, Decimal amount)
    {
      if (mon == cmon - 1)
        InvoiceFieldsCalculation.invLastMonthAmount += amount;
      else if (mon == cmon)
        InvoiceFieldsCalculation.invThisMonthAmount += amount;
      else
        InvoiceFieldsCalculation.invYTDAmount += amount;
    }

    private void Calculate_Invoice_Amount(Guid projectID, string fieldName, string entityName)
    {
      this.tracingService.Trace("11", Array.Empty<object>());
      int num1 = 0;
      Guid guid = projectID;
      QueryExpression queryExpression = new QueryExpression("bolt_invoicing");
      queryExpression.ColumnSet.AddColumns(new string[5]
      {
        "bolt_billingdate",
        "bolt_relatedjobnumber",
        "bolt_relatedproject",
        "bolt_billingamount",
        "bolt_contractbilledamount"
      });
      queryExpression.Criteria.AddCondition("statecode", (ConditionOperator) 0, new object[1]
      {
        (object) num1
      });
      queryExpression.Criteria.AddCondition(fieldName, (ConditionOperator) 0, new object[1]
      {
        (object) guid
      });
      EntityCollection entityCollection = this.service.RetrieveMultiple((QueryBase) queryExpression);
      Decimal num2 = 0.0M;
      if (((Collection<Entity>) entityCollection.Entities).Count != 0)
      {
        for (int index = 0; index < ((Collection<Entity>) entityCollection.Entities).Count; ++index)
        {
          if (((DataCollection<string, object>) ((Collection<Entity>) entityCollection.Entities)[index].Attributes).Contains("bolt_contractbilledamount"))
            num2 += ((Money) ((Collection<Entity>) entityCollection.Entities)[index]["bolt_contractbilledamount"]).Value;
        }
      }
      this.tracingService.Trace("Total: {0}", new object[1]
      {
        (object) num2
      });
      this.service.Update(new Entity(entityName)
      {
        Id = projectID,
        ["bolt_totalinvoicingamount"] = (object) num2
      });
    }
  }
}
