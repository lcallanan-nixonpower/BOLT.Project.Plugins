using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace BOLT.BayCity.Plug.ins
{
    //Registered on pre-operation stage to update end date,  
    public class CalculateActivestageDuration : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;
        Guid opportunityId;
        Guid lastActivestageID;
        Guid activeStageID;
        string stageName;
        int stageStatus;
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
                            if (preImageMisc.Attributes.Contains("bpf_opportunityid") || preImageMisc.Attributes.Contains("opportunityid"))
                            {

                                opportunityId = preImageMisc.Attributes.Contains("bpf_opportunityid") ? (preImageMisc.GetAttributeValue<EntityReference>("bpf_opportunityid")).Id : (preImageMisc.GetAttributeValue<EntityReference>("opportunityid")).Id;

                                lastActivestageID = (preImageMisc.GetAttributeValue<EntityReference>("activestageid")).Id;

                                stageName = (preImageMisc.GetAttributeValue<EntityReference>("activestageid")).Name;
                            }
                        }
                         
                            if (context.PostEntityImages.Contains("Image"))
                        {
                                Entity postImageMisc = (Entity)context.PostEntityImages["Image"];
                                if (postImageMisc.Attributes.Contains("bpf_opportunityid") || postImageMisc.Attributes.Contains("opportunityid"))
                                {

                                    opportunityId = postImageMisc.Attributes.Contains("bpf_opportunityid") ? (postImageMisc.GetAttributeValue<EntityReference>("bpf_opportunityid")).Id : (postImageMisc.GetAttributeValue<EntityReference>("opportunityid")).Id;

                                    activeStageID = (postImageMisc.GetAttributeValue<EntityReference>("activestageid")).Id;

                                    stageName = (postImageMisc.GetAttributeValue<EntityReference>("activestageid")).Name;

                                     stageStatus = postImageMisc.GetAttributeValue<OptionSetValue>("statecode").Value;
                                }
                            }
                        if ( context.MessageName.ToUpper() == "UPDATE")
                        {                           

                                RetrieveandUpdateActivestageEndDate(opportunityId, lastActivestageID, entity.Id, stageName);

                            if (stageStatus == 0)
                            {
                                CreateOpportunityStageDuration(opportunityId, activeStageID, stageName);
                            }
                          
                        }
                        else if (context.MessageName.ToUpper() == "CREATE")
                        {
                           
                                CreateOpportunityStageDuration(opportunityId, activeStageID, stageName);                      

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
        public void RetrieveandUpdateActivestageEndDate(Guid oppId, Guid stageId, Guid bpfId, string StageName)
        {
            // Define Condition Values
            //Get The Last Active Stage
            var query_bolt_opprtunity = oppId;
            var query_bolt_businessprocess =  bpfId;
            var query_bolt_processstage = stageId.ToString();
            var query_bolt_processstatus = "Active";

            // Instantiate QueryExpression query
            var query = new QueryExpression("tb_opportunitystageduration");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("tb_completedon", "tb_name", "tb_opportunitystagedurationid", "tb_opportunity", "tb_stagestatus", "tb_activestage", "tb_startedon", "createdby", "createdon", "createdonbehalfby", "modifiedby", "modifiedon", "modifiedonbehalfby", "overriddencreatedon", "ownerid", "owningbusinessunit", "statecode", "statuscode");

            // Define filter query.Criteria
            query.Criteria.AddCondition("tb_opportunity", ConditionOperator.Equal, query_bolt_opprtunity);
           // query.Criteria.AddCondition("bolt_businessprocess", ConditionOperator.Equal, query_bolt_businessprocess);
            query.Criteria.AddCondition("tb_activestage", ConditionOperator.Equal, query_bolt_processstage);
            query.Criteria.AddCondition("tb_stagestatus", ConditionOperator.Equal, query_bolt_processstatus);
            EntityCollection ec = service.RetrieveMultiple(query);

            if(ec.Entities.Count>0)
            {
                Entity osd = new Entity("tb_opportunitystageduration");

                osd.Id = ec.Entities[0].Id;
                //var datetime = new DateTime();
                osd["tb_completedon"] = DateTime.Now;
                osd["tb_stagestatus"] = "Closed";
                service.Update(osd);
            }         
        
        }

        public void CreateOpportunityStageDuration(Guid oppId, Guid ActivestageId, string StageName)
        {
          
            Entity osd = new Entity("tb_opportunitystageduration");


            osd["tb_name"] = StageName;
            osd["tb_startedon"] = DateTime.Now;
            osd["tb_stagestatus"] = "Active";
            osd["tb_opportunity"] = new EntityReference("opportunity", oppId);
            osd["tb_activestage"] = ActivestageId.ToString();
            service.Create(osd);
        }
    }
}


