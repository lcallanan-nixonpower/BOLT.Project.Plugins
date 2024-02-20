using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.Nixon.DataCenter.Plugins
{
    public class CreateUnits : IPlugin
    {
        IOrganizationService service;
        ITracingService tracingService;
        Guid packageId;
        Guid relatedProject_guid;
        EntityCollection buyoutpos;
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
                packageId = entity.Id;
                if (entity.LogicalName == "bolt_datacenterunitpackage")
                {
                    try
                    {
                        if (context.PostEntityImages.Contains("Image")) //update
                        {

                            Entity postImage = (Entity)context.PostEntityImages["Image"];

                            if (!postImage.Attributes.Contains("bolt_numberofclones")&&(postImage.GetAttributeValue<bool>("bolt_clone") is true))
                                return;

                            //var numberOfUnits = postImage.GetAttributeValue<int>("bolt_unitpackageqty");  //number of units                         
                           relatedProject_guid = (postImage.Attributes.Contains("bolt_relatedproject") ? (postImage.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id:new Guid("00000000-0000-0000-0000-000000000000"));
                            var modelNumber = (postImage.Attributes.Contains("bolt_unitmodel") ? postImage.GetAttributeValue<string>("bolt_unitmodel") : "N/A");
                           
                            var numberofClones = (postImage.Attributes.Contains("bolt_numberofclones") ? postImage.GetAttributeValue<int>("bolt_numberofclones") : 0);
                            if (numberofClones > 0) //
                            {
                                GetIndividualUnitDetails();
                                var totalexistingPackagesCount = GetnumberofExistingPackages(); // use count to generate package number
                                CreatePackages(numberofClones,totalexistingPackagesCount,postImage);                               

                            }

                        }
                        //if (context.PreEntityImages.Contains("Image")) //update
                        //{
                        //    Entity preImage = (Entity)context.PreEntityImages["Image"];

                        //    if (!preImage.Attributes.Contains("bolt_unitpackageqty"))
                        //        return;

                        //    var numberOfUnits = preImage.GetAttributeValue<int>("bolt_unitpackageqty");  //number of units                         
                        //    var modelNumber = (preImage.Attributes.Contains("bolt_unitmodel") ? preImage.GetAttributeValue<string>("bolt_unitmodel") : "N/A");  
                        //    // relatedProject_guid = (postImage.GetAttributeValue<EntityReference>("bolt_relatedproject")).Id;

                        //    if (numberOfUnits > 0) //used and true
                        //    {
                        //        CreatePackages(numberOfUnits, modelNumber);

                        //    }


                        //}
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace("DataCrnte-->CreateUnitPlug-in", ex.ToString());
                        throw;
                    }
                }
            }
        }

        public int GetnumberofExistingPackages()
        {
            // Define Condition Values
            var query_bolt_relatedproject = relatedProject_guid;

            // Instantiate QueryExpression query
            var query = new QueryExpression("bolt_datacenterunitpackage");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("bolt_name");

            // Define filter query.Criteria
            query.Criteria.AddCondition("bolt_relatedproject", ConditionOperator.Equal, query_bolt_relatedproject);
            EntityCollection existingPackages = service.RetrieveMultiple(query);
            return  existingPackages.Entities.Count;
        }
        public void GetIndividualUnitDetails()
        {
            //pull individual units(Data Center Buyout Pos)

            // Define Condition Values
            var query1_bolt_relatedpackage = packageId;
            var query1_statuscode = 1;

            // Instantiate QueryExpression query
            var query1 = new QueryExpression("bolt_datacenterpm");

            // Add columns to query.ColumnSet
            query1.ColumnSet.AddColumns("bolt_currentesd", "bolt_datacenterpmid", "bolt_expectedinvoicedate", "bolt_item", "bolt_ofunitcost", "bolt_ofunitsellinvoice", "bolt_ordernumber", "bolt_originalesd", "bolt_po", "bolt_poreleaseddate", "bolt_posentdate", "bolt_relatedpackage", "bolt_relatedproject", "bolt_status", "bolt_vendor", "createdby", "createdon", "importsequencenumber", "modifiedby", "modifiedon", "overriddencreatedon", "ownerid", "owningbusinessunit", "statecode", "statuscode");

            // Define filter query.Criteria
            query1.Criteria.AddCondition("bolt_relatedpackage", ConditionOperator.Equal, query1_bolt_relatedpackage);
            query1.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query1_statuscode);

             buyoutpos = service.RetrieveMultiple(query1);

        }
        public void CreatePackages(int cloneNumber, int existingpackagescount, Entity pImage)
        {
            for(int i =1; i<=cloneNumber;i++)
            {
                Entity package = new Entity("bolt_datacenterunitpackage");
                var packageNumber = (existingpackagescount + i);
                package["bolt_name"] = "Package "+packageNumber.ToString()+"";
                package["bolt_packagenumber"] = packageNumber;
                if (pImage.Attributes.Contains("bolt_unitmodel"))
                {
                    package["bolt_unitmodel"] = pImage.GetAttributeValue<string>("bolt_unitmodel");
                }
                if (pImage.Attributes.Contains("bolt_unitprice"))
                {
                    package["bolt_unitprice"] = pImage.GetAttributeValue<Money>("bolt_unitprice");
                }
                if (pImage.Attributes.Contains("bolt_unitcost"))
                {
                    package["bolt_unitcost"] = pImage.GetAttributeValue<Money>("bolt_unitcost");
                }
                if (pImage.Attributes.Contains("bolt_relatedproject"))
                {
                    package["bolt_relatedproject"] = new EntityReference("new_job", relatedProject_guid);
                }
                Guid newPackageID = service.Create(package);
                if(buyoutpos.Entities.Count>0)
                CreateIndividualUnits(newPackageID);
            }

        }

        public void CreateIndividualUnits(Guid Id) //create units under package.
        {
            
            for (int i=0; i<buyoutpos.Entities.Count;i++)
            {
                Entity unit = new Entity("bolt_datacenterpm");

                if (buyoutpos.Entities[i].Attributes.Contains("bolt_po"))
                {
                    unit["bolt_po"] = buyoutpos.Entities[i].GetAttributeValue<string>("bolt_po"); //details
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_item"))
                {
                    unit["bolt_item"] = buyoutpos.Entities[i].GetAttributeValue<string>("bolt_item");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_status"))
                {
                    unit["bolt_status"] = buyoutpos.Entities[i].GetAttributeValue<OptionSetValue>("bolt_status");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_vendor"))
                {
                    unit["bolt_vendor"] = new EntityReference("account",(buyoutpos.Entities[i].GetAttributeValue<EntityReference>("bolt_vendor")).Id);
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_posentdate"))
                {
                    unit["bolt_posentdate"] = buyoutpos.Entities[i].GetAttributeValue<DateTime>("bolt_posentdate");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_releasedsentdate"))
                {
                    unit["bolt_releasedsentdate"] = buyoutpos.Entities[i].GetAttributeValue<DateTime>("bolt_releasedsentdate");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_ordernumber"))
                {
                    unit["bolt_ordernumber"] = buyoutpos.Entities[i].GetAttributeValue<string>("bolt_ordernumber");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_originalesd"))
                {
                    unit["bolt_originalesd"] = buyoutpos.Entities[i].GetAttributeValue<DateTime>("bolt_originalesd");
                }

                if (buyoutpos.Entities[i].Attributes.Contains("bolt_currentesd"))
                {
                    unit["bolt_currentesd"] = buyoutpos.Entities[i].GetAttributeValue<DateTime>("bolt_currentesd");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_ofunitcost"))
                {
                    unit["bolt_ofunitcost"] = buyoutpos.Entities[i].GetAttributeValue<Decimal>("bolt_ofunitcost");
                }
                    
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_ofunitsellinvoice"))
                {
                    unit["bolt_ofunitsellinvoice"] = buyoutpos.Entities[i].GetAttributeValue<Decimal>("bolt_ofunitsellinvoice");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_expectedinvoicedate"))
                {
                    unit["bolt_expectedinvoicedate"] = buyoutpos.Entities[i].GetAttributeValue<DateTime>("bolt_expectedinvoicedate");
                }
                if (buyoutpos.Entities[i].Attributes.Contains("bolt_relatedpackage"))
                {
                    unit["bolt_relatedpackage"] = new EntityReference("bolt_datacenterunitpackage", Id);
                }
                service.Create(unit);

            }
        }
    }
}
