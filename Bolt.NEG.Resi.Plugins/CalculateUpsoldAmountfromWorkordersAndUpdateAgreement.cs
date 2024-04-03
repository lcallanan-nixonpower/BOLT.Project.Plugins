using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class CalculateUpsoldAmountfromWorkordersAndUpdateAgreement : IPlugin
{
    decimal majorUpsoldAmount = 0;
    decimal minorUpsoldAmount = 0;
    public void Execute(IServiceProvider serviceProvider)
    {
        IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

        if (context.MessageName == "Create" || context.MessageName == "Update" || context.MessageName == "Delete")
        {
            Entity workOrder = null;
            

            if (context.MessageName != "Delete")
            {
                workOrder = (Entity)context.InputParameters["Target"];
            }
            else
            {
                if (context.PreEntityImages.Contains("Target"))
                {
                    workOrder = context.PreEntityImages["Target"];
                }
            }

            if (workOrder != null && workOrder.LogicalName == "msdyn_workorder" && workOrder.Contains("bolt_additionalproducts") && workOrder.Contains("msdyn_agreement") && workOrder.Contains("msdyn_workordertype"))
            {
                Guid agreementId = workOrder.Contains("msdyn_agreement") ? ((EntityReference)workOrder["msdyn_agreement"]).Id : Guid.Empty;
                EntityReference lookupValue = (EntityReference)workOrder["msdyn_workordertype"];
               
                // Check the value of the lookup field
                if (lookupValue.Name == "a" || lookupValue.Name == "b")
                {
                    CalculateUpsoldAmountOfWorkorderRecords("msdyn_workorder", "bolt_additionalproducts", agreementId, context.MessageName, workOrder.Id, service);
                    UpdateAgreementFields("msdyn_agreement", agreementId, service);
                }
            
            }
        }
    }

    private void CalculateUpsoldAmountOfWorkorderRecords(string entityName, string fieldName, Guid agreementId, string context,Guid workorderid, IOrganizationService service)
    {
        var query_0_msdyn_workordertype = "8f6277b4-59ff-ed11-8f6e-00224824e812";
        var query_0_msdyn_workordertype1 = "62fc60eb-4901-ee11-8f6e-00224824eeac";
        var query_1_msdyn_agreement = agreementId;
        var query_1_statuscode = 1;
        var query_1_workorderid = workorderid;
        // Instantiate QueryExpression query
        var query = new QueryExpression(entityName);

        // Add columns to query.ColumnSet
        query.ColumnSet.AddColumns("bolt_additionalproducts");

        // Define filter query.Criteria
        var query_Criteria_0 = new FilterExpression();
        query.Criteria.AddFilter(query_Criteria_0);

        // Define filter query_Criteria_0
        query_Criteria_0.FilterOperator = LogicalOperator.Or;
        query_Criteria_0.AddCondition("msdyn_workordertype", ConditionOperator.Equal, query_0_msdyn_workordertype);
        query_Criteria_0.AddCondition("msdyn_workordertype", ConditionOperator.Equal, query_0_msdyn_workordertype1);
       
        var query_Criteria_1 = new FilterExpression();
        query.Criteria.AddFilter(query_Criteria_1);

        // Define filter query_Criteria_1
        query_Criteria_1.AddCondition("msdyn_agreement", ConditionOperator.Equal, query_1_msdyn_agreement);
        query_Criteria_1.AddCondition("statuscode", ConditionOperator.Equal, query_1_statuscode);
        if (context == "Delete")
            query_Criteria_1.AddCondition("msdyn_workorderid", ConditionOperator.NotEqual, query_1_workorderid);


        EntityCollection childRecords = service.RetrieveMultiple(query);
       


        foreach (Entity childRecord in childRecords.Entities)
        {
            if (childRecord.Contains(fieldName) && childRecord[fieldName] is Money moneyValue && ((EntityReference)childRecord["msdyn_workordertype"]).Name == "Major PM")
            {
                majorUpsoldAmount += moneyValue.Value;
            }
        }
        foreach (Entity childRecord in childRecords.Entities)
        {
            if (childRecord.Contains(fieldName) && childRecord[fieldName] is Money moneyValue && ((EntityReference)childRecord["msdyn_workordertype"]).Name == "Minor PM")
            {
                minorUpsoldAmount += moneyValue.Value;
            }
        }

    }

    private void UpdateAgreementFields(string entityName, Guid agreementId, IOrganizationService service)
    {
        Entity parentEntity = new Entity(entityName);
        parentEntity.Id = agreementId;
        parentEntity["bolt_majorupsold"] = majorUpsoldAmount;
        parentEntity["bolt_minorupsold"] = minorUpsoldAmount;
        service.Update(parentEntity);
    }
}
//






