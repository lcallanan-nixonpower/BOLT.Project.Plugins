using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Bolt.NEG.Resi.Plugins
{
    public class DeleteDeliveryWorkOrderProduct : IPlugin
    {
        IOrganizationService service;
        Guid workOrderID;
        Guid primaryQuoteID;
        Guid productID;
        public void Execute(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
