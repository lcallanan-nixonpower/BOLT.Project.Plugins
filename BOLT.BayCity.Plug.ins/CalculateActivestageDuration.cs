using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace BOLT.BayCity.Plug.ins
{
    //Registered on pre-operation stage,  to capture the inactive stage plug-in is registered on pre-operationstage.
    public class CalculateActivestageDuration : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;
        Guid opportunity_guid;
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
                if (entity.LogicalName == "bolt_opportunityservicerepairprocess"|| entity.LogicalName == "opportunitysalesprocess")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImageMisc = (Entity)context.PreEntityImages["Image"];
                            if (preImageMisc.Attributes.Contains("bpf_opportunityid")|| preImageMisc.Attributes.Contains("opportunityid"))
                            {

                            Guid opportunityId = preImageMisc.Attributes.Contains("bpf_opportunityid")?(preImageMisc.GetAttributeValue<EntityReference>("bpf_opportunityid")).Id: (preImageMisc.GetAttributeValue<EntityReference>("opportunityid")).Id;

                            Guid lastActivestageID = (preImageMisc.GetAttributeValue<EntityReference>("activestageid")).Id;

                            //Calculate_Price(opportunity_guid, serviceType, context.MessageName, context.PrimaryEntityId);
                            RetrieveandUpdateActivestageEndDate(opportunityId, lastActivestageID,entity.Id);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("Opp Line Plug-in", ex.ToString());
                        throw;
                    }
                }

            }
        }
        public void RetrieveandUpdateActivestageEndDate(Guid oppId, Guid lastActivestageId, Guid bpfId)
        {
            // Define Condition Values
            var query_bolt_opprtunity = oppId;
            var query_bolt_businessprocess =  bpfId;
            var query_bolt_processstage = lastActivestageId;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_opportunitystageduration");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_businessprocess", "bolt_enddate", "bolt_name", "bolt_opportunitystagedurationid", "bolt_opprtunity", "bolt_processname", "bolt_processstage", "bolt_startdate", "createdby", "createdon", "createdonbehalfby", "modifiedby", "modifiedon", "modifiedonbehalfby", "overriddencreatedon", "ownerid", "owningbusinessunit", "statecode", "statuscode");

            // Define filter query.Criteria
            query.Criteria.AddCondition("bolt_opprtunity", ConditionOperator.Equal, query_bolt_opprtunity);
           // query.Criteria.AddCondition("bolt_businessprocess", ConditionOperator.Equal, query_bolt_businessprocess);
            query.Criteria.AddCondition("bolt_processstage", ConditionOperator.Equal, query_bolt_processstage);

            EntityCollection ec = service.RetrieveMultiple(query);

            if(ec.Entities.Count>0)
            {
                Entity osd = new Entity("bolt_opportunitystageduration");

                osd.Id = ec.Entities[0].Id;
                //var datetime = new DateTime();
                osd["bolt_enddate"] = DateTime.Now;
                osd["bolt_stagestatus"] = true; //0=active, 1 = completed 
                service.Update(osd);
            }

        }
    }
}


