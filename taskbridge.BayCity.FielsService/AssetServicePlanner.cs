using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace taskbridge.BayCity.FielsService
{
    public class ServicePlannerPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity target = (Entity)context.InputParameters["Target"];

                if (target.LogicalName == "msdyn_customerasset")
                {
                    Entity asset = service.Retrieve("msdyn_customerasset", target.Id, new ColumnSet(true));

                    bool createplanneritems = asset.GetAttributeValue<bool>("tb_createserviceplanner");
                    DateTime initialServiceDate = asset.GetAttributeValue<DateTime>("tb_initialservicedate");
                    OptionSetValue serviceFrequencyOption = asset.GetAttributeValue<OptionSetValue>("tb_servicefrequency");
                    OptionSetValue serviceDurationOption = asset.GetAttributeValue<OptionSetValue>("tb_serviceduration");

                    if (serviceFrequencyOption != null && serviceDurationOption != null&&createplanneritems is true)
                    {
                        string serviceFrequency = GetOptionSetText(service, "msdyn_customerasset", "tb_servicefrequency", serviceFrequencyOption.Value);
                        int serviceDuration = MapDurationToYears(serviceDurationOption.Value);


                        GenerateServicePlannerRecords(service, target.Id, initialServiceDate, serviceFrequency, serviceDuration, tracingService);
                    }
                }
            }
        }
        private int MapDurationToYears(int durationOptionSetValue)
        {
            // Map the OptionSetValue to the corresponding number of years
            switch (durationOptionSetValue)
            {
                case 126700000: // Example value for 1 year
                    return 1;
                case 126700001: // Example value for 2 years
                    return 2;
                case 126700002: // Example value for 3 years
                    return 3;
                case 126700003: // Example value for 4 years
                    return 4;
                case 126700004: // Example value for 5 years
                    return 5;
                default:
                    return 0;
            }
        }

        private void GenerateServicePlannerRecords(IOrganizationService service, Guid assetId, DateTime initialServiceDate, string serviceFrequency, int serviceDuration, ITracingService tracingService)
        {
            int majorServiceInterval = 12; // Default to annual
            int minorServiceInterval = 0;

            switch (serviceFrequency.ToLower())
            {
                case "annual":
                    majorServiceInterval = 12;
                    break;
                case "semi annual":
                    majorServiceInterval = 12;
                    minorServiceInterval = 6;
                    break;
                case "quarterly":
                    majorServiceInterval = 12;
                    minorServiceInterval = 3;
                    break;
                case "monthly":
                    majorServiceInterval = 1; // Monthly
                    break;
                default:
                    tracingService.Trace($"Unknown service frequency: {serviceFrequency}");
                    return;
            }

            int totalMajorServices = serviceDuration * (12 / majorServiceInterval); // Calculate the total number of major services
            int totalMinorServices = (minorServiceInterval > 0) ? serviceDuration * (12 / minorServiceInterval) : 0; // Calculate the total number of minor services if applicable

            int serviceNumber = 1;
            for (int year = 0; year < serviceDuration; year++)
            {
                for (int month = 0; month < 12; month += minorServiceInterval > 0 ? minorServiceInterval : majorServiceInterval)
                {
                    Entity serviceRecord = new Entity("tb_assetserviceplanner");
                    serviceRecord["tb_relatedasset"] = new EntityReference("msdyn_customerasset", assetId);
                    serviceRecord["tb_servicenumber"] = serviceNumber;
                    serviceRecord["statuscode"] = new OptionSetValue(126700001); // Assuming 0 represents "Unscheduled"
                    serviceRecord["tb_servicedate"] = initialServiceDate.AddMonths(year * 12 + month);

                    if (serviceFrequency.ToLower() == "semi annual")
                    {
                        serviceRecord["tb_type"] = new OptionSetValue(serviceNumber % 2 == 1 ? 126700001 : 126700000); //  126700001 for Minor and 126700000 for Major
                    }
                    else if (serviceFrequency.ToLower() == "quarterly")
                    {
                        serviceRecord["tb_type"] = new OptionSetValue(serviceNumber % 4 == 0 ? 126700000 : 126700001); // 126700001 for Minor and 126700000 for Major
                    }
                    else if (serviceFrequency.ToLower() == "monthly")
                    {
                        serviceRecord["tb_type"] = new OptionSetValue(126700000); 
                    }
                    else
                    {
                        serviceRecord["tb_type"] = new OptionSetValue(126700000);
                    }

                    service.Create(serviceRecord);
                    serviceNumber++;
                }
            }
        }

        private string GetOptionSetText(IOrganizationService service, string entityLogicalName, string attributeLogicalName, int optionSetValue)
        {
            var attributeRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityLogicalName,
                LogicalName = attributeLogicalName,
                RetrieveAsIfPublished = true
            };

            var attributeResponse = (RetrieveAttributeResponse)service.Execute(attributeRequest);
            var attributeMetadata = (PicklistAttributeMetadata)attributeResponse.AttributeMetadata;

            foreach (var option in attributeMetadata.OptionSet.Options)
            {
                if (option.Value == optionSetValue)
                {
                    return option.Label.UserLocalizedLabel.Label;
                }
            }

            return null;
        }
    }
}
