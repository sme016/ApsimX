﻿using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.CLEM.Activities
{
    /// <summary>
    /// Activity to buy resources
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("This activity manages the purchase of a specified resource.")]
    [HelpUri(@"Content/Features/Activities/All resources/BuyResource.htm")]
    [Version(1, 0, 2, "Automatically handles transactions with Marketplace if present")]
    [Version(1, 0, 1, "")]
    public class ResourceActivityBuy : CLEMActivityBase
    {
        private double units;
        private ResourcePricing price;
        private FinanceType bankAccount;
        private IResourceType resourceToBuy;
        private double unitsCanAfford;

        /// <summary>
        /// Bank account to use
        /// </summary>
        [Description("Bank account to use")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { typeof(Finance) } })]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of account to use required")]
        public string AccountName { get; set; }

        /// <summary>
        /// Resource type to buy
        /// </summary>
        [Description("Resource to buy")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { typeof(AnimalFoodStore), typeof(HumanFoodStore), typeof(Equipment), typeof(GreenhouseGases), typeof(HumanFoodStore), typeof(OtherAnimals), typeof(ProductStore), typeof(WaterStore) } })]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of resource type required")]
        public string ResourceTypeName { get; set; }

        /// <summary>
        /// Units to purchase
        /// </summary>
        [Description("Number of packets")]
        [Required, GreaterThanEqualValue(0)]
        public double Units { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ResourceActivityBuy()
        {
            TransactionCategory = "Expense";
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            // get bank account object to use
            bankAccount = Resources.FindResourceType<Finance, FinanceType>(this, AccountName, OnMissingResourceActionTypes.ReportWarning, OnMissingResourceActionTypes.ReportErrorAndStop);
            // get resource type to buy
            resourceToBuy = Resources.FindResourceType<ResourceBaseWithTransactions, IResourceType>(this, ResourceTypeName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);

            // get pricing
            if((resourceToBuy as CLEMResourceTypeBase).MarketStoreExists)
                if ((resourceToBuy as CLEMResourceTypeBase).EquivalentMarketStore.PricingExists(PurchaseOrSalePricingStyleType.Sale))
                    price = (resourceToBuy as CLEMResourceTypeBase).EquivalentMarketStore.Price(PurchaseOrSalePricingStyleType.Sale);

            // no market price found... look in local resources and allow 0 price if not found
            if(price is null)
                price = resourceToBuy.Price(PurchaseOrSalePricingStyleType.Purchase);
        }

        /// <inheritdoc/>
        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
            List<ResourceRequest> requests = new List<ResourceRequest>();

            // calculate units
            units = Units;
            if (price!=null && price.UseWholePackets)
                units = Math.Truncate(Units);

            unitsCanAfford = units;
            if (units > 0 & (resourceToBuy as CLEMResourceTypeBase).MarketStoreExists)
            {
                // determine how many units we can afford
                double cost = units * price.PricePerPacket;
                if (cost > bankAccount.FundsAvailable)
                {
                    unitsCanAfford = bankAccount.FundsAvailable / price.PricePerPacket;
                    if(price.UseWholePackets)
                        unitsCanAfford = Math.Truncate(unitsCanAfford);
                }

                CLEMResourceTypeBase mkt = (resourceToBuy as CLEMResourceTypeBase).EquivalentMarketStore;

                requests.Add(new ResourceRequest()
                {
                    AllowTransmutation = true,
                    Required = unitsCanAfford * price.PacketSize * this.FarmMultiplier,
                    Resource = mkt as IResourceType,
                    ResourceType = mkt.Parent.GetType(),
                    ResourceTypeName = (mkt as IModel).Name,
                    Category = "Purchase " + (resourceToBuy as Model).Name,
                    ActivityModel = this
                });
            }
            return requests;
        }

        /// <inheritdoc/>
        public override GetDaysLabourRequiredReturnArgs GetDaysLabourRequired(LabourRequirement requirement)
        {
            double daysNeeded;
            switch (requirement.UnitType)
            {
                case LabourUnitType.Fixed:
                    daysNeeded = requirement.LabourPerUnit;
                    break;
                case LabourUnitType.perUnit:
                    daysNeeded = units * requirement.LabourPerUnit;
                    break;
                default:
                    throw new Exception(String.Format("LabourUnitType {0} is not supported for {1} in {2}", requirement.UnitType, requirement.Name, this.Name));
            }
            return new GetDaysLabourRequiredReturnArgs(daysNeeded, TransactionCategory, (resourceToBuy as CLEMModel).NameWithParent);
        }

        /// <inheritdoc/>
        public override void AdjustResourcesNeededForActivity()
        {
            // adjust amount needed by labour shortfall.
            double labprop = this.LabourLimitProportion;

            // get additional reduction based on labour cost shortfall as cost has already been accounted for
            double priceprop = 1;
            if (labprop < 1)
            {
                if(unitsCanAfford < units)
                    priceprop = unitsCanAfford / units;

                if(labprop < priceprop)
                {
                    unitsCanAfford = units * labprop;
                    if(price.UseWholePackets)
                        unitsCanAfford = Math.Truncate(unitsCanAfford);
                }
            }

            if (unitsCanAfford > 0 & (resourceToBuy as CLEMResourceTypeBase).MarketStoreExists)
            {
                // find resource entry in market if present and reduce
                ResourceRequest rr = ResourceRequestList.Where(a => a.Resource == (resourceToBuy as CLEMResourceTypeBase).EquivalentMarketStore).FirstOrDefault();
                if(rr.Required != unitsCanAfford * price.PacketSize * this.FarmMultiplier)
                    rr.Required = unitsCanAfford * price.PacketSize * this.FarmMultiplier;
            }
            return;
        }

        /// <inheritdoc/>
        public override void DoActivity()
        {
            Status = ActivityStatus.NotNeeded;
            // take local equivalent of market from resource

            double provided = 0;
            if ((resourceToBuy as CLEMResourceTypeBase).MarketStoreExists)
            {
                // find resource entry in market if present and reduce
                ResourceRequest rr = ResourceRequestList.Where(a => a.Resource == (resourceToBuy as CLEMResourceTypeBase).EquivalentMarketStore).FirstOrDefault();
                provided = rr.Provided / this.FarmMultiplier;
            }
            else
                provided = unitsCanAfford * price.PacketSize;

            if (provided > 0)
            {
                resourceToBuy.Add(provided, this, "", TransactionCategory);
                Status = ActivityStatus.Success;
            }

            // make financial transactions
            if (bankAccount != null)
            {
                ResourceRequest payment = new ResourceRequest()
                {
                    AllowTransmutation = false,
                    MarketTransactionMultiplier = this.FarmMultiplier,
                    Required = provided / price.PacketSize * price.PricePerPacket,
                    Resource = bankAccount,
                    ResourceType = typeof(Finance),
                    ResourceTypeName = bankAccount.Name,
                    Category = TransactionCategory,
                    RelatesToResource = (resourceToBuy as CLEMModel).NameWithParent,
                    ActivityModel = this
                };
                bankAccount.Remove(payment);
            }

        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">Buy ");
                if (Units <= 0)
                    htmlWriter.Write("<span class=\"errorlink\">[VALUE NOT SET]</span>");
                else
                    htmlWriter.Write("<span class=\"setvalue\">" + Units.ToString("0.###") + "</span>");
                htmlWriter.Write(" packages of ");
                htmlWriter.Write(CLEMModel.DisplaySummaryValueSnippet(ResourceTypeName, "Resource not set", HTMLSummaryStyle.Resource));
                htmlWriter.Write(" using ");
                htmlWriter.Write(CLEMModel.DisplaySummaryValueSnippet(AccountName, "Account not set", HTMLSummaryStyle.Resource));
                htmlWriter.Write("</div>");
                return htmlWriter.ToString(); 
            }
        } 
        #endregion

    }
}
