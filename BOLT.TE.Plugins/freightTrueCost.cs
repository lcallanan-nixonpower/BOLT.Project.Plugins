using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.TE.Plugins
{
    public class freightTrueCost:IPlugin
    {
        /// <summary>
        /// A plugin that calculates Freight True cost = ((Freight to First Destination) + (Freight to Jobsite)) x Number of Buyout POs
        ///
        /// </summary>
        /// <remarks>Register this plug-in on the bolt_progressbill1, bolt_progressbill2, bolt_progressbill3, bolt_progressbill4
        /// 
        /// </remarks>      
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
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
               
               if (entity.LogicalName == "new_buyoutpos")
                {
                    try
                    {
                        if (context.PreEntityImages.Contains("Image"))
                        {
                            Entity preImagePO = (Entity)context.PreEntityImages["Image"];
                            if (preImagePO.Attributes.Contains("bolt_relatedproject"))
                            {
                                relatedProject_guid = (preImagePO.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;
                                Calculate_FreightTrue_Cost(relatedProject_guid, "bolt_relatedproject", "new_job");
                            }
                        }
                        else if (context.PostEntityImages.Contains("Image"))
                        {

                            Entity postImagePO = (Entity)context.PostEntityImages["Image"];
                            if (postImagePO.Attributes.Contains("bolt_relatedproject"))
                            {
                                relatedProject_guid = (postImagePO.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;
                                Calculate_FreightTrue_Cost(relatedProject_guid, "bolt_relatedproject", "new_job");
                            }                           
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("freightTrueCost", ex.ToString());
                        throw;
                    }
                }
            }
            else if (context.MessageName == "Delete")
            {
                if (context.PreEntityImages.Contains("Image"))
                {
                    Entity preImagePO = (Entity)context.PreEntityImages["Image"];
                    if (preImagePO.Attributes.Contains("bolt_relatedproject"))
                    {
                        relatedProject_guid = (preImagePO.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;

                        Calculate_FreightTrue_Cost(relatedProject_guid, "bolt_relatedproject", "new_job");
                    }
               
                }
            }
        }



        private void Calculate_FreightTrue_Cost(Guid projectID, string fieldName, string entityName)
        {
            tracingService.Trace("11");
            // Define Condition Values
            var query_statecode = 0;
            var query_bolt_relatedproject = projectID;

            // Instantiate QueryExpression query
            var query = new QueryExpression("new_buyoutpos");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns(
                "bolt_freighttofirstdestination",
                "bolt_freighttojobsite",
                "bolt_qty",
                "bolt_relatedproject",
                "statecode");

            // Add conditions to query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition("bolt_relatedproject", ConditionOperator.Equal, query_bolt_relatedproject);
//query.Criteria.AddCondition("bolt_relatedproject", ConditionOperator.Equal, query_bolt_relatedproject);


            EntityCollection buyoutPOs = service.RetrieveMultiple(query);

            decimal ffd_Total = 0.0m;   //Freight to firts destination
            decimal fj_Total = 0.0m;
            int numberof_pos = 0;
            decimal freightTrueCost = 0.00m;
            if (buyoutPOs.Entities.Count != 0)
            {
               // numberof_pos = buyoutPOs.Entities.Count;

                for (int i = 0; i < buyoutPOs.Entities.Count; i++)
                {
                    if (buyoutPOs.Entities[i].Attributes.Contains("bolt_freighttofirstdestination"))
                    {
                        ffd_Total += ((Money)buyoutPOs.Entities[i]["bolt_freighttofirstdestination"]).Value;
                    }
                    if (buyoutPOs.Entities[i].Attributes.Contains("bolt_freighttojobsite"))
                    {
                        fj_Total += ((Money)buyoutPOs.Entities[i]["bolt_freighttojobsite"]).Value;
                    }
                    //if(((Money)buyoutPOs.Entities[i]["bolt_freighttofirstdestination"]).Value>0 || ((Money)buyoutPOs.Entities[i]["bolt_freighttojobsite"]).Value>0)
                    //{
                    //    numberof_pos += 1;
                    //}
                }
                freightTrueCost =  (ffd_Total + fj_Total);
            }

             

            tracingService.Trace("Total: {0}", freightTrueCost);
            Entity project = new Entity("new_job");
            project.Id = projectID;
            project["bolt_freightestimatedcost"] = freightTrueCost;           
            service.Update(project);
            //pro["bolt_invoicedthismonth"] = invThisMonthAmount;
            //pro["bolt_invoicedthisyear"] = invYTDAmount;

        }
    }
}
