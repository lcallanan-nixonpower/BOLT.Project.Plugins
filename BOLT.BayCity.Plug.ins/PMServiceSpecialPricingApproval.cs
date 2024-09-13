using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.BayCity.Plug.ins
{
    public class PMServiceSpecialPricingApproval : IPlugin
    {
        /// <summary>
        /// A plugin that updates related Opportunity.SpecialPricingApproval field based on values of children PM Service.SpecialPricingApproval values.
        /// </summary>
        /// <remarks>
        /// Entity: bolt_plannedmaintenanceservice (Planned Maintenance Service)
        /// Message, Stage, Order, Mode: Create, PostOperation, 1, Synchronous, Filter - bolt_specialpricingrequested, bolt_specialpricingstatus
        /// Image: Post Image - bolt_serviceid, bolt_specialpricingrequested, bolt_specialpricingstatus
        /// Message, Stage, Order, Mode: Delete, PostOperation, 1, Synchronous 
        /// Image: Pre Image - bolt_serviceid, bolt_specialpricingrequested, bolt_specialpricingstatus
        /// Message, Stage, Order, Mode: Update, PostOperation, 1, Synchronous, Filter - bolt_specialpricingrequested, bolt_specialpricingstatus
        /// Image: Post Image -bolt_serviceid, bolt_specialpricingrequested, bolt_specialpricingstatus
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target"))
            {
                // Obtain the IOrganizationService instance which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    if (context.MessageName == "Delete")
                    {
                        // Get pre-image
                        Entity pre_entity = (Entity)context.PreEntityImages["pre_image"];

                        // Get Opportunity reference
                        EntityReference opp_entref = pre_entity.GetAttributeValue<EntityReference>("bolt_serviceid");

                        if (opp_entref != null)
                        {
                            // Get the "overall" Special Pricing Status based on child records
                            OptionSetValue opp_SP_Status = Retrieve_Related_PMS_Records(opp_entref);

                            // Update Special Pricing based on child record values
                            Entity opp = new Entity(opp_entref.LogicalName, opp_entref.Id);
                            if (opp_SP_Status.Value == 0)
                            {
                                opp["bolt_specialpricingstatus"] = null;
                            }
                            else
                            {
                                opp["bolt_specialpricingstatus"] = opp_SP_Status;
                            }

                            // Update opportunity
                            service.Update(opp);
                        }
                    }
                    else if (context.MessageName == "Update")
                    {
                        // Get post-image
                        Entity post_entity = (Entity)context.PostEntityImages["post_image"];

                        // Get Opportunity reference
                        EntityReference opp_entref = post_entity.GetAttributeValue<EntityReference>("bolt_serviceid");

                        if (opp_entref != null)
                        {
                            // Get the "overall" Special Pricing Status based on child records
                            OptionSetValue opp_SP_Status = Retrieve_Related_PMS_Records(opp_entref);

                            // Update Special Pricing based on child record values
                            Entity opp = new Entity(opp_entref.LogicalName, opp_entref.Id);
                            if (opp_SP_Status.Value == 0)
                            {
                                opp["bolt_specialpricingstatus"] = null;
                            }
                            else
                            {
                                opp["bolt_specialpricingstatus"] = opp_SP_Status;
                            }

                            // Update opportunity
                            service.Update(opp);
                        }
                    }
                    else if (context.MessageName == "Create")
                    {
                        // Get post-image
                        Entity post_entity = (Entity)context.PostEntityImages["post_image"];

                        // Get Opportunity reference
                        EntityReference opp_entref = post_entity.GetAttributeValue<EntityReference>("bolt_serviceid");

                        if (opp_entref != null)
                        {
                            // Get the "overall" Special Pricing Status based on child records
                            OptionSetValue opp_SP_Status = Retrieve_Related_PMS_Records(opp_entref);

                            // Update Special Pricing based on child record values
                            Entity opp = new Entity(opp_entref.LogicalName, opp_entref.Id);
                            if (opp_SP_Status.Value == 0)
                            {
                                opp["bolt_specialpricingstatus"] = null;
                            }
                            else
                            {
                                opp["bolt_specialpricingstatus"] = opp_SP_Status;
                            }

                            // Update opportunity
                            service.Update(opp);
                        }


                    }

                    //Helper Functions
                    #region
                    // Retrieve related PMS records
                    OptionSetValue Retrieve_Related_PMS_Records(EntityReference opp)
                    {
                        // Instantiate QueryExpression query
                        var query = new QueryExpression("bolt_plannedmaintenanceservice");

                        // Add columns to query.ColumnSet
                        query.ColumnSet.AddColumns("bolt_specialpricingrequested", "bolt_specialpricingstatus");

                        // Set Condition Values
                        var query_statecode = 0;
                        var query_bolt_serviceid = opp.Id;
                        var query_bolt_specialpricingrequested = true;

                        // Add conditions to query.Criteria
                        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
                        query.Criteria.AddCondition("bolt_serviceid", ConditionOperator.Equal, query_bolt_serviceid);
                        query.Criteria.AddCondition("bolt_specialpricingrequested", ConditionOperator.Equal, query_bolt_specialpricingrequested);

                        EntityCollection records = service.RetrieveMultiple(query);

                        OptionSetValue Opp_SP_Status = new OptionSetValue();

                        // Determine what the overall Opp Special Pricing Status should be
                        foreach (Entity record in records.Entities)
                        {
                            bool SP_Req = record.GetAttributeValue<bool>("bolt_specialpricingrequested");
                            OptionSetValue SP_Status = record.GetAttributeValue<OptionSetValue>("bolt_specialpricingstatus");
                            if (SP_Req)
                            {
                                if (SP_Status.Value == 454890000) // Requested
                                {
                                    Opp_SP_Status = SP_Status;
                                }
                                else if (SP_Status.Value == 454890001) // Accepted
                                {
                                    if (Opp_SP_Status.Value == 0)
                                    {
                                        Opp_SP_Status = SP_Status;
                                    }
                                }
                                else if (SP_Status.Value == 454890002) // Rejected
                                {
                                    Opp_SP_Status = SP_Status;
                                    break;
                                }
                            }
                        }

                        return Opp_SP_Status;

                    }
                    #endregion
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in Service Sales - PM: PM Special Pricing Approval plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("Service Sales - PM: PM Special Pricing Approval plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
