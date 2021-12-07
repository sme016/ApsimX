﻿using Models.CLEM.Interfaces;
using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace Models.CLEM.Activities
{
    /// <summary>
    /// Activity to processes one resource into another resource with associated labour and costs
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("Process one resource into another resource with associated labour and costs")]
    [HelpUri(@"Content/Features/Activities/All resources/ProcessResource.htm")]
    [Version(1, 0, 1, "")]
    public class ResourceActivityProcess : CLEMActivityBase
    {
        /// <summary>
        /// Resource type to process
        /// </summary>
        [Description("Resource to process")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { typeof(AnimalFoodStore), typeof(HumanFoodStore), typeof(Equipment), typeof(GreenhouseGases), typeof(OtherAnimals), typeof(ProductStore), typeof(WaterStore) } })]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of resource type to process required")]
        public string ResourceTypeProcessedName { get; set; }

        /// <summary>
        /// Resource type created
        /// </summary>
        [Description("Resource created")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { typeof(AnimalFoodStore), typeof(HumanFoodStore), typeof(Equipment), typeof(GreenhouseGases), typeof(OtherAnimals), typeof(ProductStore), typeof(WaterStore) } })]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of resource type created required")]
        public string ResourceTypeCreatedName { get; set; }

        /// <summary>
        /// Conversion rate
        /// </summary>
        [Description("Rate to convert processed resource to created resource")]
        [Required, GreaterThanValue(0)]
        public double ConversionRate { get; set; }

        /// <summary>
        /// Reserve
        /// </summary>
        [Description("Amount to reserve")]
        [Required, GreaterThanEqualValue(0)]
        public double Reserve { get; set; }

        /// <summary>
        /// Resource to process
        /// </summary>
        [JsonIgnore]
        private IResourceType resourceTypeProcessModel { get; set; }

        /// <summary>
        /// Resource created
        /// </summary>
        [JsonIgnore]
        private IResourceType resourceTypeCreatedModel { get; set; }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            resourceTypeProcessModel = Resources.FindResourceType<ResourceBaseWithTransactions, IResourceType>(this, ResourceTypeProcessedName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);
            resourceTypeCreatedModel = Resources.FindResourceType<ResourceBaseWithTransactions, IResourceType>(this, ResourceTypeCreatedName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);
        }

        /// <summary>
        /// constructor
        /// </summary>
        public ResourceActivityProcess()
        {
            TransactionCategory = "Process";
        }

        /// <inheritdoc/>
        public override void AdjustResourcesNeededForActivity()
        {
            // get labour shortfall
            double labprop = this.LimitProportion(typeof(LabourType));
            // get finance shortfall
            double finprop = this.LimitProportion(typeof(FinanceType));

            // reduce amount used
            double limit = Math.Min(labprop, finprop);

            if(limit<1)
            {
                // find process resource entry in resource list
                ResourceRequest rr = ResourceRequestList.Where(a => a.ResourceType == resourceTypeProcessModel.GetType()).FirstOrDefault();
                if (rr != null)
                {
                    // reduce amount required
                    rr.Required *= limit;
                }
            }
        }

        /// <inheritdoc/>
        public override void DoActivity()
        {
            // processed resource should already be taken
            Status = ActivityStatus.NotNeeded;
            // add created resources
            ResourceRequest rr = ResourceRequestList.Where(a => (a.Resource != null && a.Resource.GetType() == resourceTypeProcessModel.GetType())).FirstOrDefault();
            if (rr != null)
            {
                resourceTypeCreatedModel.Add(rr.Provided * ConversionRate, this, (resourceTypeCreatedModel as CLEMModel).NameWithParent, "Created");
                if(rr.Provided > 0)
                {
                    Status = ActivityStatus.Success;
                }
            }
        }

        /// <inheritdoc/>
        public override GetDaysLabourRequiredReturnArgs GetDaysLabourRequired(LabourRequirement requirement)
        {
            double daysNeeded;

            // get amount to processed
            double amountToProcess = resourceTypeProcessModel.Amount;
            if (Reserve > 0)
            {
                amountToProcess = Math.Min(amountToProcess, Reserve);
            }

            switch (requirement.UnitType)
            {
                case LabourUnitType.Fixed:
                    daysNeeded = requirement.LabourPerUnit;
                    break;
                case LabourUnitType.perUnit:
                    daysNeeded = amountToProcess / requirement.UnitSize * requirement.LabourPerUnit;
                    break;
                default:
                    throw new Exception(String.Format("LabourUnitType {0} is not supported for {1} in {2}", requirement.UnitType, requirement.Name, this.Name));
            }
            return new GetDaysLabourRequiredReturnArgs(daysNeeded, TransactionCategory, (resourceTypeCreatedModel as CLEMModel).NameWithParent);
        }

        /// <inheritdoc/>
        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
            List<ResourceRequest> resourcesNeeded = new List<ResourceRequest>();

            double amountToProcess = resourceTypeProcessModel.Amount;
            if (Reserve > 0)
            {
                amountToProcess = Math.Min(amountToProcess, Reserve);
            }

            // get finances required.
            foreach (ResourceActivityFee item in this.FindAllChildren<ResourceActivityFee>())
            {
                if (ResourceRequestList == null)
                {
                    ResourceRequestList = new List<ResourceRequest>();
                }

                double sumneeded = 0;
                switch (item.PaymentStyle)
                {
                    case ResourcePaymentStyleType.Fixed:
                        sumneeded = item.Amount;
                        break;
                    case ResourcePaymentStyleType.perUnit:
                        sumneeded = amountToProcess*item.Amount;
                        break;
                    case ResourcePaymentStyleType.perBlock:
                        ResourcePricing price = resourceTypeProcessModel.Price(PurchaseOrSalePricingStyleType.Both);
                        double blocks = amountToProcess / price.PacketSize;
                        if(price.UseWholePackets)
                        {
                            blocks = Math.Truncate(blocks);
                        }
                        sumneeded = blocks * item.Amount;
                        break;
                    default:
                        throw new Exception(String.Format("PaymentStyle [{0}] is not supported for [{1}] in [a={2}]", item.PaymentStyle, item.Name, this.Name));
                }
                if (sumneeded > 0)
                {
                    ResourceRequestList.Add(new ResourceRequest()
                    {
                        AllowTransmutation = false,
                        Required = sumneeded,
                        Resource = item.BankAccount,
                        ResourceType = typeof(Finance),
                        ResourceTypeName = item.BankAccount.Name,
                        ActivityModel = this,
                        FilterDetails = null,
                        Category = TransactionCategory,
                        RelatesToResource = (resourceTypeCreatedModel as CLEMModel).NameWithParent
                    }
                    );
                }
            }

            // get process resource required
            if (amountToProcess > 0)
            {
                resourcesNeeded.Add(
                    new ResourceRequest()
                    {
                        AllowTransmutation = true,
                        Required = amountToProcess,
                        Resource = resourceTypeProcessModel,
                        ResourceType = (resourceTypeProcessModel as Model).Parent.GetType(),
                        ResourceTypeName = (resourceTypeProcessModel as Model).Name,
                        ActivityModel = this,
                        Category = TransactionCategory,
                        RelatesToResource = (resourceTypeCreatedModel as CLEMModel).NameWithParent
                    }
                );
            }
            return resourcesNeeded;
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">Process ");
                htmlWriter.Write(CLEMModel.DisplaySummaryValueSnippet(ResourceTypeProcessedName, "Resource not set", HTMLSummaryStyle.Resource));
                htmlWriter.Write(" into ");
                htmlWriter.Write(CLEMModel.DisplaySummaryValueSnippet(ResourceTypeCreatedName, "Resource not set", HTMLSummaryStyle.Resource));
                htmlWriter.Write(" at a rate of ");
                if (ConversionRate <= 0)
                    htmlWriter.Write("<span class=\"errorlink\">[RATE NOT SET]</span>");
                else
                    htmlWriter.Write("1:<span class=\"resourcelink\">" + ConversionRate.ToString("0.###") + "</span>");
                htmlWriter.Write("</div>");
                if (Reserve > 0)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    htmlWriter.Write("<span class=\"setvalue\">" + Reserve.ToString("0.###") + "</span> will be reserved.");
                    htmlWriter.Write("</div>");
                }
                return htmlWriter.ToString(); 
            }
        } 
        #endregion
    }
}
