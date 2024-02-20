using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class UpdateAgreementwithInvoiceamountdue : IPlugin
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
            Entity invoice = null;


            if (context.MessageName != "Delete")
            {
                invoice = (Entity)context.InputParameters["Target"];
            }
            else
            {
                if (context.PreEntityImages.Contains("Target"))
                {
                    invoice = context.PreEntityImages["Target"];
                }
            }

            if (invoice != null && invoice.LogicalName == "invoice" && invoice.Contains("msdyn_amountdue") && invoice.Contains("bolt_relatedagreement") && invoice.Contains("bolt_agreementinvoicetype"))
            {
                Guid agreementId = invoice.Contains("bolt_relatedagreement") ? ((EntityReference)invoice["bolt_relatedagreement"]).Id : Guid.Empty;
                int invoiceType =(invoice.GetAttributeValue<OptionSetValue>("bolt_agreementinvoicetype")).Value;

                // Check the value of the lookup field
                if (invoiceType == 123 )
                {
                    CalculateUpsoldAmountOfinvoiceRecords("invoice", "msdyn_amountdue", invoiceType,agreementId, context.MessageName, invoice.Id, service);
                    UpdateAgreementFields("msdyn_agreement", agreementId, service);
                }
               
            }
        }
    }

    private void CalculateUpsoldAmountOfinvoiceRecords(string entityName, string fieldName, int invoicetype,Guid agreementId, string context, Guid invoiceid, IOrganizationService service)
    {
        // Define Condition Values
        var query_statecode = 3;
        var query_bolt_relatedagreement = agreementId;
        var query_bolt_agreementinvoicetype = invoicetype;
        var query_invoiceid = invoiceid;

        // Instantiate QueryExpression query
        var query = new QueryExpression(entityName);

        // Add columns to query.ColumnSet
        query.ColumnSet.AddColumns(fieldName);

        // Define filter query.Criteria
        query.Criteria.AddCondition("statecode", ConditionOperator.NotEqual, query_statecode);
        query.Criteria.AddCondition("bolt_relatedagreement", ConditionOperator.Equal, query_bolt_relatedagreement);
        query.Criteria.AddCondition("bolt_agreementinvoicetype", ConditionOperator.Equal, query_bolt_agreementinvoicetype);
        if (context == "Delete")
            query.Criteria.AddCondition("invoiceid", ConditionOperator.NotEqual, query_invoiceid);


        EntityCollection childRecords = service.RetrieveMultiple(query);



        foreach (Entity childRecord in childRecords.Entities)
        {
            if (childRecord.Contains(fieldName) && childRecord[fieldName] is Money moneyValue && ((EntityReference)childRecord["msdyn_invoicetype"]).Name == "Major PM")
            {
                majorUpsoldAmount += moneyValue.Value;
            }
        }
        foreach (Entity childRecord in childRecords.Entities)
        {
            if (childRecord.Contains(fieldName) && childRecord[fieldName] is Money moneyValue && ((EntityReference)childRecord["msdyn_invoicetype"]).Name == "Minor PM")
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







