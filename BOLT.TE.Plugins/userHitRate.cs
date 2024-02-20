using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.TE.Plugins
{
    public class userHitRate : IPlugin
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
                        Int32 TotOptyCount = 0;
                        Int32 OpenOptyCount = 0;
                        Int32 LostOptyCount = 0;
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

                        EntityReference erfUser = entOpty.Contains("ownerid") ? entOpty.GetAttributeValue<EntityReference>("ownerid") : null;

                        string groupbyWon = String.Format(@"
                                            <fetch distinct='false' mapping='logical' aggregate='true'> 
                                               <entity name='opportunity'>  
                                               <attribute name='opportunityid' alias='WonOptyCount' aggregate='count' />
                                               <attribute name='bolt_profit' alias='WonProfit' aggregate='sum' />
                                               <attribute name='actualvalue' alias='WonRevenue' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='ownerid' operator='eq' value='{0}' />
                                                  <condition attribute='statecode' operator='eq' value='1' />
                                                   {1}
                                                </filter>
                                               </entity> 
                                            </fetch>", erfUser.Id, deleteFilter);

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
                                                  <condition attribute='ownerid' operator='eq' value='{0}' />
                                                   {1}
                                                </filter>
                                               </entity> 
                                            </fetch>", erfUser.Id, deleteFilter);

                        EntityCollection groupbyTotal_result = service.RetrieveMultiple(new FetchExpression(groupbyTotal));

                        foreach (var c in groupbyTotal_result.Entities)
                        {
                            TotOptyCount = (Int32)((AliasedValue)c["OptyCount"]).Value;
                        }


                        string groupbyOpen = String.Format(@"
                                            <fetch distinct='false' mapping='logical' aggregate='true'> 
                                               <entity name='opportunity'>  
                                               <attribute name='opportunityid' alias='OpenOptyCount' aggregate='count' />
                                                <filter>
                                                  <condition attribute='ownerid' operator='eq' value='{0}' />
                                                   <condition attribute='statecode' operator='eq' value='0' />
                                                   {1}
                                                </filter>
                                               </entity> 
                                            </fetch>", erfUser.Id, deleteFilter);

                        EntityCollection groupbyOpen_result = service.RetrieveMultiple(new FetchExpression(groupbyOpen));
                        foreach (var c in groupbyOpen_result.Entities)
                        {
                            OpenOptyCount = (Int32)((AliasedValue)c["OpenOptyCount"]).Value;
                        }

                        LostOptyCount = TotOptyCount - (OpenOptyCount + WonOptyCount);

                        Entity objUser = new Entity(erfUser.LogicalName);
                        objUser.Id = erfUser.Id;
                        objUser["bolt_hitrate"] = (Decimal)(TotOptyCount != 0 ? (Decimal)WonOptyCount / TotOptyCount : 0);
                        objUser["bolt_openopportunities"] = OpenOptyCount != 0 ? OpenOptyCount : 0;
                        objUser["bolt_wonopportunities"] = WonOptyCount != 0 ? WonOptyCount : 0;
                        objUser["bolt_lostopportunities"] = LostOptyCount != 0 ? LostOptyCount : 0;
                        service.Update(objUser);
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
