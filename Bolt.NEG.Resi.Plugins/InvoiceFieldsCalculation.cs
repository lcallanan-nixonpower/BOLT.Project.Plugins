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
    public class InvoiceFieldsCalculation : IPlugin
    {
       
        /// </summary>
        /// <remarks>Register this plug-in on the bolt_progressbill1, bolt_progressbill2, bolt_progressbill3, bolt_progressbill4
        /// Post Operation execution stage, and Synchronous execution mode.
        /// </remarks>

        public static Decimal invLastMonthAmount = 0.00m;
        public static Decimal invThisMonthAmount = 0.00m;
        public static Decimal invYTDAmount = 0.00m;
         IOrganizationService service;
        ITracingService tracingService;
        Guid relatedProject_guid;
        public void Execute(IServiceProvider serviceProvider)
        {

            //Extract the tracing service for use in debugging sandboxed plug-ins.
             tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService.Trace("Pre-image invoiceid number}");
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is  Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
               
                if(entity.LogicalName == "invoice")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                            if (!preImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                                return;
                            relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;
                            Calculate_Invoice_Amount(relatedProject_guid);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageInvoice = (Entity)context.PostEntityImages["Image"];
                            if (!postImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                                return;
                            relatedProject_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;
                            Calculate_Invoice_Amount(relatedProject_guid);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Invoice Amount Plugin", ex.ToString());
                        throw;
                    }
                }
            }
            else if(context.MessageName=="Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                    if (!preImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                        return;
                    relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;

                    Calculate_Invoice_Amount(relatedProject_guid);
                }
            }
        }
           
        private  void Calculate_Invoice_Amount(Guid projectID)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode_active = 0;
            var query_statecode_paid = 2;
            var query_bolt_relatedresidentialproject = projectID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("invoice");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("msdyn_invoicedate", "statecode", "bolt_relatedresidentialproject", "bolt_billingamount", "bolt_contractbilledamount");

            
            // Define filter query.Criteria
            var query_Criteria_0 = new FilterExpression();
            query.Criteria.AddFilter(query_Criteria_0);
            query_Criteria_0.AddCondition("bolt_relatedresidentialproject", ConditionOperator.Equal, query_bolt_relatedresidentialproject);

            var query_Criteria_1 = new FilterExpression();
            query.Criteria.AddFilter(query_Criteria_1);

            // Define filter query_Criteria_1
            query_Criteria_1.FilterOperator = LogicalOperator.Or;

            // Define filter query.Criteria
           query_Criteria_1.AddCondition("statecode", ConditionOperator.Equal, query_statecode_active);
            // Define filter query.Criteria
            query_Criteria_1.AddCondition("statecode", ConditionOperator.Equal, query_statecode_paid);

            EntityCollection invoices = service.RetrieveMultiple(query);

            decimal totalInvoiccingAmount = 0.0m;
            decimal scheduledAmountTodal = 0.00m;
            if (invoices.Entities.Count!=0)
            {
                for (int i = 0; i < invoices.Entities.Count; i++)
                {
                    if(invoices.Entities[i].Attributes.Contains("bolt_contractbilledamount")&&((OptionSetValue)invoices.Entities[i]["statecode"]).Value==2)
                    {
                        totalInvoiccingAmount += ((Money)invoices.Entities[i]["bolt_contractbilledamount"]).Value;
                    }
                    else if (invoices.Entities[i].Attributes.Contains("bolt_contractbilledamount") && ((OptionSetValue)invoices.Entities[i]["statecode"]).Value == 0)
                    {
                        scheduledAmountTodal += ((Money)invoices.Entities[i]["bolt_contractbilledamount"]).Value;
                    }

                }

            }
            tracingService.Trace("Total: {0}", totalInvoiccingAmount);
            Entity project = new Entity("bolt_residentialproject");
            project.Id = projectID;
            project["bolt_totalinvoicingamount"] = totalInvoiccingAmount;
            project["bolt_scheduledinvoicedamount"] = scheduledAmountTodal;
            service.Update(project);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;

        }

    }
}
