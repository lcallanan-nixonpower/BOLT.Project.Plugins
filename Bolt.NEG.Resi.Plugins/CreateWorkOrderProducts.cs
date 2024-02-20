using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;


namespace Bolt.NEG.Resi.Plugins
{
    
    public class CreateWorkOrderProducts : IPlugin
    {
        IOrganizationService service;
        Guid workOrderID;
        Guid primaryQuoteID;
       
        List<Guid> productIds = new List<Guid>();
        List<double> quantity = new List<double>();
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                Entity woEntity = service.Retrieve("msdyn_workorder", entity.Id, new ColumnSet(true));
                if (entity.LogicalName == "msdyn_workorder" && woEntity.Attributes.Contains("bolt_residentialproject")&&woEntity.Attributes.Contains("msdyn_workordertype"))
                {   //Get Project from the workorder, equioment is associated with the quote.
                    //So, to obatain equipment query the primary quote of the project.
                    string wotype = woEntity.GetAttributeValue<EntityReference>("msdyn_workordertype").Name;
                    
                    if (wotype != "Delivery") //if workorder type is not 'Delivery' stop the process.
                        return;
                   
                    workOrderID = entity.Id;
                    Guid ProjectId = woEntity.GetAttributeValue<EntityReference>("bolt_residentialproject").Id;
                    Get_GEN_ATS_ProductID(ProjectId);
                    GET_AccessoriesProductIDs();
                    Creat_WOProduct();
                }
            }

        }
        public void Get_GEN_ATS_ProductID(Guid projectId) //Get Primary Quote associated with the  project
        {            
            // Define Condition Values
            var query_bolt_primary = 454890000;
            var query_bolt_relatedproject = projectId;

            // Instantiate QueryExpression query
            var query = new QueryExpression("quote");

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("name", "bolt_atsproduct1", "bolt_atsproduct2", "bolt_equipmentkitproduct", "bolt_generatorproduct1", "quoteid");
            query.AddOrder("name", OrderType.Ascending);

            // Define filter query.Criteria
            query.Criteria.AddCondition("bolt_primary", ConditionOperator.Equal, query_bolt_primary);
            query.Criteria.AddCondition("bolt_relatedproject", ConditionOperator.Equal, query_bolt_relatedproject);

            EntityCollection quotes = service.RetrieveMultiple(query);
            

            if (quotes.Entities.Count > 0) //if there is a Primary Quote exists
            {//get the producdId's to create a workorder products
             //GEN,ATS and Kits products are lookups, but the accessories are 1 to many relation.

                primaryQuoteID = quotes.Entities[0].Id; // quote GUID to retrieve residential adders(accessories) products.
                if (quotes.Entities[0].Attributes.Contains("bolt_generatorproduct1"))
                {
                    productIds.Add(quotes.Entities[0].GetAttributeValue<EntityReference>("bolt_generatorproduct1").Id);
                    quantity.Add(1.0);
                }
                if (quotes.Entities[0].Attributes.Contains("bolt_atsproduct1"))
                {
                    productIds.Add(quotes.Entities[0].GetAttributeValue<EntityReference>("bolt_atsproduct1").Id);
                    quantity.Add(1);
                }
                if (quotes.Entities[0].Attributes.Contains("bolt_atsproduct2"))
                {
                    productIds.Add(quotes.Entities[0].GetAttributeValue<EntityReference>("bolt_atsproduct2").Id);
                    quantity.Add(1);
                }

                if (quotes.Entities[0].Attributes.Contains("bolt_equipmentkitproduct"))
                {
                    GET_KitsproductIds(quotes.Entities[0]);
                }
            }            

        }
        public void GET_KitsproductIds(Entity bundle)
        {
            if (bundle.Attributes.Contains("bolt_equipmentkitproduct"))
            {
                //get productids from product bundle
                // Define Condition Values
                var query2_productid = bundle.GetAttributeValue<EntityReference>("bolt_equipmentkitproduct").Id;

                // Instantiate QueryExpression query
                var query2 = new QueryExpression("productassociation");

                // Add columns to query.ColumnSet
                query2.ColumnSet.AddColumns("associatedproduct");

                // Define filter query.Criteria
                query2.Criteria.AddCondition("productid", ConditionOperator.Equal, query2_productid);

                EntityCollection bundleProducts = service.RetrieveMultiple(query2);

                if (bundleProducts.Entities.Count > 0)
                {
                    foreach (var b in bundleProducts.Entities)
                    {
                        productIds.Add(b.GetAttributeValue<EntityReference>("associatedproduct").Id);
                        quantity.Add(b.Attributes.Contains("quantity") ? b.GetAttributeValue<int>("quantity") : 1);
                    }
                }
            }
        }
         public void  GET_AccessoriesProductIDs()
        {
            //get all the accessories connected to the primary quote

            // Define Condition Values
            var query1_statuscode = 1;
            var query1_bolt_quote = primaryQuoteID;

            // Instantiate QueryExpression query
            var query1 = new QueryExpression("bolt_residentialadders");

            // Add columns to query.ColumnSet
            query1.ColumnSet.AddColumns("bolt_product", "bolt_qty");

            // Define filter query.Criteria
            query1.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query1_statuscode);
            query1.Criteria.AddCondition("bolt_quote", ConditionOperator.Equal, query1_bolt_quote);

            EntityCollection Adders = service.RetrieveMultiple(query1);

            if(Adders.Entities.Count>0)
            {
                foreach(var a in Adders.Entities)
                {
                    if(a.Attributes.Contains("bolt_product"))
                    {
                        productIds.Add(a.GetAttributeValue<EntityReference>("bolt_product").Id);
                        quantity.Add(a.Attributes.Contains("bolt_qty") ? a.GetAttributeValue<int>("bolt_qty") : 1);
                    }
                }
            }

        }
        public void Creat_WOProduct()
        {
            
            for(int i=0;i<productIds.Count;i++)
            {
                Guid productId = productIds[i];
                // Add your custom logic or validations here

                // Create a new Work Order Product record
                Entity newWorkOrderProduct = new Entity("msdyn_workorderproduct");
                newWorkOrderProduct["msdyn_workorder"] = new EntityReference("msdyn_workorder", workOrderID);
                newWorkOrderProduct["msdyn_product"] = new EntityReference("product", productId);
                newWorkOrderProduct["msdyn_estimatequantity"] = quantity[i];

                // Add additional attributes or set default values as needed

                // Create the Work Order Product record
                service.Create(newWorkOrderProduct);

            }
            //foreach(var item in productIds)
            //{
            //    Guid productId = item;
            //    // Add your custom logic or validations here

            //    // Create a new Work Order Product record
            //    Entity newWorkOrderProduct = new Entity("msdyn_workorderproduct");
            //    newWorkOrderProduct["msdyn_workorder"] = new EntityReference("msdyn_workorder", workOrderID);
            //    newWorkOrderProduct["msdyn_product"] = new EntityReference("product", productId);
            //    newWorkOrderProduct["msdyn_estimatequantity"] = 1;

            //    // Add additional attributes or set default values as needed

            //    // Create the Work Order Product record
            //    service.Create(newWorkOrderProduct);
            //}      
          
        }
    }
}
