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
    public class Agreement_PriceCalculation : IPlugin
    {
        public static Decimal invYTDAmount = 0.00m;
        IOrganizationService service;
        ITracingService tracingService;
        Guid relatedAgreement_guid;
        public void Execute(IServiceProvider serviceProvider)
        {
            //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                if (entity.LogicalName == "msdyn_customerasset")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                            if (!preImageInvoice.Attributes.Contains("bolt_agreement"))
                                return;
                            relatedAgreement_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_agreement")).Id;
                            Calculate_TotalPrice(relatedAgreement_guid);
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImageInvoice = (Entity)context.PostEntityImages["Image"];
                            if (!postImageInvoice.Attributes.Contains("bolt_agreement"))
                                return;
                            relatedAgreement_guid = (postImageInvoice.GetAttributeValue<EntityReference>("bolt_agreement")).Id;
                            Calculate_TotalPrice(relatedAgreement_guid);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Agreement_PriceCal Plugin", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImageInvoice = (Entity)context.PreEntityImages["Image"];
                    if (!preImageInvoice.Attributes.Contains("bolt_agreement"))
                        return;
                    relatedAgreement_guid = (preImageInvoice.GetAttributeValue<EntityReference>("bolt_agreement")).Id;

                    Calculate_TotalPrice(relatedAgreement_guid);
                }
            }
        }


        private void Calculate_TotalPrice(Guid agreementID)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode = 0;
            var query_bolt_agreement = agreementID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("msdyn_customerasset");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_agreement", "bolt_productstotal", "bolt_servicestotal", "bolt_totalfromincidenttype", "statecode");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition("bolt_agreement", ConditionOperator.Equal, query_bolt_agreement);


            EntityCollection assets = service.RetrieveMultiple(query);

            decimal productstotal = 0.0m;
            decimal servicestotal = 0.0m;

            if (assets.Entities.Count != 0)
            {
                for (int i = 0; i < assets.Entities.Count; i++)
                {
                    if (assets.Entities[i].Attributes.Contains("bolt_productstotal") )
                    {
                        productstotal += (((Money)assets.Entities[i]["bolt_productstotal"]).Value );
                    }
                    if (assets.Entities[i].Attributes.Contains("bolt_servicestotal"))
                    {
                        servicestotal += ((Money)assets.Entities[i]["bolt_servicestotal"]).Value;
                    }

                }
         
            }
       
            Entity agreement = new Entity("msdyn_agreement");
            agreement.Id = agreementID;
            agreement["bolt_totalagreementproducts"] = productstotal;
            agreement["bolt_totalagreementservices"] = servicestotal;
            service.Update(agreement);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;
        }
    }
}
