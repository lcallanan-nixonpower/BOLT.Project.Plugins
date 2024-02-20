using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.BayCity.Plug.ins
{
    public class CostSheetTotalPMCost : IPlugin
    {
        /// <summary>
        /// A plugin that add all the related Planeed maintenance and KD records 'Total contract Price' and updtaes cost sheet Total PM Cost field.
        /// new_job(Project)
        /// </summary>
        /// <remarks>
        /// Post Operation execution stage, and Synchronous execution mode.
        /// </remarks>

        public static decimal totalPMamount = 0.00m;
        public static decimal totalKDamount = 0.00m;
        public static decimal totalamount = 0.00m;

        IOrganizationService service;
        ITracingService tracingService;
        public void Execute(IServiceProvider serviceProvider)
        {

            //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                tracingService.Trace("A1");
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];



                try
                {

                    tracingService.Trace("A2");
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    service = serviceFactory.CreateOrganizationService(context.UserId);
                    if ((context.MessageName == "Create" || context.MessageName == "Update"))
                    {
                        Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        if (ent.Attributes.Contains("bolt_costsheet"))
                        {
                            Guid costsheetid = (ent.GetAttributeValue<EntityReference>("bolt_costsheet")).Id;

                            if ((entity.LogicalName != "bolt_plannedmaintenanceservice" || entity.LogicalName != "bolt_kdservicemaintenance") && costsheetid != null)
                            {
                                PMAmountcalculation(costsheetid);
                                KDAmountcalculation(costsheetid);
                                totalamount = totalKDamount + totalPMamount;

                                updateCostsheet(costsheetid);


                            }
                        }
                    }


                }
                catch (Exception ex)
                {
                    tracingService.Trace("TotalStartupCostSheet: {0}", ex.ToString());
                    throw;
                }
            }
            else if (context.MessageName.ToUpper() == "DELETE")
            {
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                service = serviceFactory.CreateOrganizationService(context.UserId);
                Entity preImage = (Entity)context.PreEntityImages["preImage"];
                if (preImage.Contains("bolt_costsheet"))
                {
                    if ((preImage.GetAttributeValue<EntityReference>("bolt_costsheet")).Id != null)
                    {
                        Guid id = (preImage.GetAttributeValue<EntityReference>("bolt_costsheet")).Id;
                        PMAmountcalculation(id);
                        KDAmountcalculation(id);
                        totalamount = totalKDamount + totalPMamount;
                        updateCostsheet(id);


                    }
                }
            }
        }

        public void PMAmountcalculation(Guid id)
        {
            tracingService.Trace("1");
            // Define Condition Values
            var query_statecode = 0;
            var query_bolt_costsheet = id;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_plannedmaintenanceservice");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_totalcontractamount");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
            query.Criteria.AddCondition("bolt_costsheet", ConditionOperator.Equal, query_bolt_costsheet);

            EntityCollection PMcollection = service.RetrieveMultiple(query);

            if (PMcollection.Entities.Count != 0)
            {
                totalPMamount = 0.00m;
                for (int i = 0; i < PMcollection.Entities.Count; i++)
                {
                    if (PMcollection.Entities[i].Attributes.Contains("bolt_totalcontractamount"))
                        totalPMamount += ((Money)PMcollection.Entities[i]["bolt_totalcontractamount"]).Value;

                }
            }
            tracingService.Trace("2");


        }
        public void KDAmountcalculation(Guid id)
        {
            tracingService.Trace("3");
            // Define Condition Values
            var query_statuscode = 1;
            var query_bolt_costsheet = id;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_kdservicemaintenance");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_totalkdcontractprice");

            // Define filter query.Criteria
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
            query.Criteria.AddCondition("bolt_costsheet", ConditionOperator.Equal, query_bolt_costsheet);

            EntityCollection kdcollection = service.RetrieveMultiple(query);


            if (kdcollection.Entities.Count != 0)
            {
                totalKDamount = 0.00m;
                for (int i = 0; i < kdcollection.Entities.Count; i++)
                {
                    if (kdcollection.Entities[i].Attributes.Contains("bolt_totalkdcontractprice"))
                        totalKDamount += ((Money)kdcollection.Entities[i]["bolt_totalkdcontractprice"]).Value;
                }
            }
            tracingService.Trace("4");

        }

        public void updateCostsheet(Guid csid)
        {
            tracingService.Trace("5");
            Entity e = new Entity();
            e.LogicalName = "bolt_costsheet";
            e.Id = csid;
            e["bolt_totalpmcost"] = new Money(totalamount);
            service.Update(e);
            totalamount = 0.00m;

            tracingService.Trace("fina");
        }
    }
}
