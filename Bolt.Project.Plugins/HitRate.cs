    using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace Nixon.Bolt.Crm.Plugins.Account
{
    public class HitRate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = null;
            IOrganizationServiceFactory factory = null;
            IOrganizationService service = null;
            try
            {
                context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                service = factory.CreateOrganizationService(context.UserId);

                if (service == null) return;
                if (context.InputParameters.Contains("Target"))
                {
                    if ((context.InputParameters["Target"] is Entity && Equals(((Entity)context.InputParameters["Target"]).LogicalName, "opportunity")) ||
                    (context.InputParameters["Target"] is EntityReference && Equals(((EntityReference)context.InputParameters["Target"]).LogicalName, "opportunity")))
                    {
                        Entity entOpty = null;
                        string deleteFilter = null;
                        Int32 WonOptyCount = 0;
                        Int32 OptyCount = 0;
                        decimal WonProfit = 0;
                        decimal WonRevenue = 0;

                        if (context.MessageName.ToLower().Equals("create"))
                        {
                            entOpty = (Entity)context.InputParameters["Target"];
                        }
                        else if (context.MessageName.ToLower().Equals("update"))
                        {
                            entOpty = (Entity)context.PostEntityImages["Image"];
                        }
                        else
                        {
                            entOpty = (Entity)context.PreEntityImages["Image"];
                            deleteFilter = String.Format("<condition attribute = 'opportunityid' operator= 'ne' uitype = 'opportunity' value = '{0}' />", entOpty.Id);
                        }

                        EntityReference erfCustomer = entOpty.Contains("ownerid") ? entOpty.GetAttributeValue<EntityReference>("ownerid") : null;

                        string groupbyWon = String.Format(@"
                                            <fetch distinct='false' mapping='logical' aggregate='true'> 
                                               <entity name='opportunity'>  
                                               <attribute name='opportunityid' alias='WonOptyCount' aggregate='count' />
                                               <attribute name='new_proft1' alias='WonProfit' aggregate='sum' />
                                               <attribute name='actualvalue' alias='WonRevenue' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='customerid' operator='eq' value='{0}' />
                                                  <condition attribute='statecode' operator='eq' value='1' />
                                                   {1}
                                                </filter>
                                               </entity> 
                                            </fetch>", erfCustomer.Id, deleteFilter);

                        EntityCollection groupbyWon_result = service.RetrieveMultiple(new FetchExpression(groupbyWon));

                        foreach (var c in groupbyWon_result.Entities)
                        {
                            WonOptyCount = (Int32)((AliasedValue)c["WonOptyCount"]).Value; 
                            WonProfit = (Money)((AliasedValue)c["WonProfit"]).Value != null ? ((Money)((AliasedValue)c["WonProfit"]).Value).Value : 0;
                            WonRevenue = (Money)((AliasedValue)c["WonRevenue"]).Value != null ? ((Money)((AliasedValue)c["WonRevenue"]).Value).Value : 0;
                        }


                        string groupbyTotal = String.Format(@"
                                            <fetch distinct='false' mapping='logical' aggregate='true'> 
                                               <entity name='opportunity'>  
                                               <attribute name='opportunityid' alias='OptyCount' aggregate='count' />
                                                <filter>
                                                  <condition attribute='customerid' operator='eq' value='{0}' />
                                                   {1}
                                                </filter>
                                               </entity> 
                                            </fetch>", erfCustomer.Id, deleteFilter);

                        EntityCollection groupbyTotal_result = service.RetrieveMultiple(new FetchExpression(groupbyTotal));

                        foreach (var c in groupbyTotal_result.Entities)
                        {
                            OptyCount = (Int32)((AliasedValue)c["OptyCount"]).Value;
                        }

                        Entity objOpty = new Entity(erfCustomer.LogicalName);
                        objOpty.Id = erfCustomer.Id;
                        objOpty["bolt_hitrate"] = (Decimal) (OptyCount != 0 ? (Decimal)WonOptyCount / OptyCount:0);
                        objOpty["bolt_avgdealsize"] = WonOptyCount != 0 ? WonRevenue / WonOptyCount: 0;
                        objOpty["bolt_avgprofitperdeal"] = WonOptyCount != 0 ? WonProfit / WonOptyCount : 0;
                        objOpty["bolt_avgprofit"] = (Decimal)(WonRevenue != 0 ? (Decimal)WonProfit / WonRevenue:0);
                        service.Update(objOpty);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
