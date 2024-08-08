using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.TE.Plugins
{
    public class InvoiceCalculation : IPlugin
    {
        /// <summary>
        /// A plugin that calculates bolt_invoicedlastmonth,bolt_invoicedthismonth,bolt_invoicedthismonth
        /// new_job(Project)
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
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];

                if (entity.LogicalName == "new_job")
                {    //return;

                    try
                    {
                        UpdateAmount(entity, service);

                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Invoice Amount Plugin: {0}", ex.ToString());
                        throw;
                    }
                }
                else if (entity.LogicalName == "bolt_invoicing")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                            if (preImageInvoice.Attributes.Contains("bolt_relatedproject"))
                            {
                                relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;
                                Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedproject", "new_job");
                            }
                            else if (preImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                            {
                                relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;
                                Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
                            }
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageInvoice = (Entity)context.PostEntityImages["Image"];
                            if (postImageInvoice.Attributes.Contains("bolt_relatedproject"))
                            {
                                relatedProject_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;
                                Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedproject", "new_job");
                            }
                            else if (postImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                            {
                                relatedProject_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;
                                Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Invoice Amount Plugin", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                    if (preImageInvoice.Attributes.Contains("bolt_relatedproject"))
                    {
                        relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;

                        Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedproject", "new_job");
                    }
                    else if (preImageInvoice.Attributes.Contains("bolt_relatedresidentialproject"))
                    {
                        relatedProject_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_relatedresidentialproject")).Id;
                        Calculate_Invoice_Amount(relatedProject_guid, "bolt_relatedresidentialproject", "bolt_residentialproject");
                    }
                }
            }
        }

        private static void UpdateAmount(Entity entity, IOrganizationService service)
        {
            Entity e = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));


            DateTime pbd1 = e.GetAttributeValue<DateTime>("bolt_progressbill1date");
            DateTime pbd2 = e.GetAttributeValue<DateTime>("bolt_progressbill2date");
            DateTime pbd3 = e.GetAttributeValue<DateTime>("bolt_progressbill3date");
            DateTime pbd4 = e.GetAttributeValue<DateTime>("bolt_progressbill4date");

            //get amounts
            Decimal pbd1amount = (e.GetAttributeValue<Money>("bolt_progressbill1")) is null ? 0.00m : e.GetAttributeValue<Money>("bolt_progressbill1").Value;

            Decimal pbd2amount = (e.GetAttributeValue<Money>("bolt_progressbill2")) is null ? 0.00m : e.GetAttributeValue<Money>("bolt_progressbill2").Value;

            Decimal pbd3amount = (e.GetAttributeValue<Money>("bolt_progressbill3")) is null ? 0.00m : e.GetAttributeValue<Money>("bolt_progressbill3").Value;

            Decimal pbd4amount = (e.GetAttributeValue<Money>("bolt_progressbill4")) is null ? 0.00m : e.GetAttributeValue<Money>("bolt_progressbill4").Value;

            //get present month
            DateTime today = DateTime.Today;
            var current_month = today.Month;

            if (pbd1 != null && pbd1amount != 0.00m)
            {

                var pbd1Month = pbd1.Month;
                CalculateAmounts(current_month, pbd1Month, pbd1amount);

            }
            if (pbd2 != null && pbd2amount != 0.00m)
            {
                var pbd2Month = pbd2.Month;
                CalculateAmounts(current_month, pbd2Month, pbd2amount);

            }
            if (pbd3 != null && pbd3amount != 0.00m)
            {
                var pbd3Month = pbd3.Month;
                CalculateAmounts(current_month, pbd3Month, pbd3amount);

            }
            if (pbd4 != null && pbd4amount != 0.00m)
            {
                var pbd4Month = pbd4.Month;
                CalculateAmounts(current_month, pbd4Month, pbd4amount);
            }

            invYTDAmount = invLastMonthAmount + invThisMonthAmount + invYTDAmount;

            Entity pro = new Entity("new_job");
            pro.Id = entity.Id;
            pro["bolt_invoicedlastmonth"] = invLastMonthAmount;
            pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            pro["bolt_invoicedthisyear"] = invYTDAmount;
            service.Update(pro);

            //reset to 0$
            invLastMonthAmount = 0.00m;
            invThisMonthAmount = 0.00m;
            invYTDAmount = 0.00m;

        }



        // Method to calculate price in an opportunity        
        private static void CalculateAmounts(int cmon, int mon, Decimal amount)
        {

            if (mon == cmon - 1)
            {
                invLastMonthAmount = invLastMonthAmount + amount;
            }
            else if (mon == cmon)
            {
                invThisMonthAmount = invThisMonthAmount + amount;
            }
            else
            {
                invYTDAmount = invYTDAmount + amount;
            }
        }


        private void Calculate_Invoice_Amount(Guid projectID, string fieldName, string entityName)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode = 0;
            var query_bolt_relatedproject = projectID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_invoicing");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_billingdate", "bolt_relatedjobnumber", "bolt_relatedproject", "bolt_billingamount","bolt_freightbilled");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition(fieldName, ConditionOperator.Equal, query_bolt_relatedproject);

            EntityCollection invoices = service.RetrieveMultiple(query);

            decimal total = 0.0m;
            decimal freightbilled_Total = 0.0m;

            if (invoices.Entities.Count != 0)
            {
                for (int i = 0; i < invoices.Entities.Count; i++)
                {
                    if (invoices.Entities[i].Attributes.Contains("bolt_billingamount"))
                    {
                        total += ((Money)invoices.Entities[i]["bolt_billingamount"]).Value;
                    }
                    if (invoices.Entities[i].Attributes.Contains("bolt_freightbilled"))
                    {
                        freightbilled_Total += ((Money)invoices.Entities[i]["bolt_freightbilled"]).Value;
                    }
                }

            }
            tracingService.Trace("Total: {0}", total);
            Entity project = new Entity(entityName);
            project.Id = projectID;
            project["bolt_totalinvoicingamount"] = total;
            if (entityName == "new_job")
            {
                project["bolt_freightpostedcost"] = freightbilled_Total;
            }
            service.Update(project);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;

        }

    }
}
