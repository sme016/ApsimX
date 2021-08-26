﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.Core.Attributes;
using System.IO;

namespace Models.CLEM.Activities
{
    /// <summary>Tracking settings for Ruminant purchases and sales</summary>
    /// <summary>If this model is provided within RuminantActivityBuySell, trucking costs and loading rules will occur</summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(RuminantActivityBuySell))]
    [Description("This provides trucking settings for the Ruminant Buy and Sell Activity and will determine costs and emissions if required.")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Activities/Ruminant/Trucking.htm")]
    public class TruckingSettings : CLEMModel
    {
        [Link]
        private ResourcesHolder resources = null;

        private GreenhouseGasesType co2Store;
        private GreenhouseGasesType methaneStore;
        private GreenhouseGasesType n2oStore;

        /// <summary>
        /// Label to assign each transaction created by this activity in ledgers
        /// </summary>
        [Description("Category for transactions")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Category for transactions required")]
        public string TransactionCategory { get; set; }

        /// <summary>
        /// Distance to market
        /// </summary>
        [Description("Distance to market (km)")]
        [Required, GreaterThanEqualValue(0)]
        public double DistanceToMarket { get; set; }

        /// <summary>
        /// Cost of trucking ($/km/truck)
        /// </summary>
        [Description("Cost of trucking ($/km/truck)")]
        [Required, GreaterThanEqualValue(0)]
        public double CostPerKmTrucking { get; set; }

        /// <summary>
        /// Number of 450kg animals per truck load
        /// </summary>
        [Description("Number of 450kg animals per truck load")]
        [Required, GreaterThanValue(0)]
        public double Number450kgPerTruck { get; set; }

        /// <summary>
        /// Minimum number of truck loads before selling (0 continuous sales)
        /// </summary>
        [Description("Minimum number of truck loads before selling (0 continuous sales)")]
        [Required, GreaterThanEqualValue(0)]
        public double MinimumTrucksBeforeSelling { get; set; }

        /// <summary>
        /// Minimum proportion of truck load before selling (0 continuous sales)
        /// </summary>
        [Description("Minimum proportion of truck load before selling (0 continuous sales)")]
        [Required, GreaterThanEqualValue(0)]
        public double MinimumLoadBeforeSelling { get; set; }

        /// <summary>
        /// Minimum number of truck loads before buying (0 continuous purchase)
        /// </summary>
        [Description("Minimum number of truck loads before buying (0 no limit)")]
        [Required, GreaterThanEqualValue(0)]
        public double MinimumTrucksBeforeBuying { get; set; }

        /// <summary>
        /// Minimum proportion of truck load before buying (0 continuous purchase)
        /// </summary>
        [Description("Minimum proportion of truck load before buying (0 no limit)")]
        [Required, GreaterThanEqualValue(0)]
        public double MinimumLoadBeforeBuying { get; set; }

        /// <summary>
        /// Truck CO2 emissions per km
        /// </summary>
        [Description("Truck CO2 emissions per km")]
        [Required, GreaterThanEqualValue(0)]
        public double TruckCO2Emissions { get; set; }

        /// <summary>
        /// Truck methane emissions per km
        /// </summary>
        [Description("Truck Methane emissions per km")]
        [Required, GreaterThanEqualValue(0)]
        public double TruckMethaneEmissions { get; set; }

        /// <summary>
        /// Truck N2O emissions per km
        /// </summary>
        [Description("Truck Nitrous oxide emissions per km")]
        [Required, GreaterThanEqualValue(0)]
        public double TruckN2OEmissions { get; set; }

        /// <summary>
        /// Methane store for emissions
        /// </summary>
        [Description("Greenhouse gas store for methane emissions")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { "Use store named Methane if present", typeof(GreenhouseGases) } })]
        [System.ComponentModel.DefaultValue("Use store named Methane if present")]
        public string MethaneStoreName { get; set; }

        /// <summary>
        /// Carbon dioxide store for emissions
        /// </summary>
        [Description("Greenhouse gas store for carbon dioxide emissions")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { "Use store named CO2 if present", typeof(GreenhouseGases) } })]
        [System.ComponentModel.DefaultValue("Use store named CO2 if present")]
        public string CarbonDioxideStoreName { get; set; }

        /// <summary>
        /// Nitrous oxide store for emissions
        /// </summary>
        [Description("Greenhouse gas store for nitrous oxide emissions")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { "Use store named N2O if present", typeof(GreenhouseGases) } })]
        [System.ComponentModel.DefaultValue("Use store named N2O if present")]
        public string NitrousOxideStoreName { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public TruckingSettings()
        {
            base.ModelSummaryStyle = HTMLSummaryStyle.SubActivity;
            TransactionCategory = "Livestock.Trucking";
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            if (TruckMethaneEmissions > 0)
                if (MethaneStoreName is null || MethaneStoreName == "Use store named Methane if present")
                    methaneStore = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, "Methane", OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore);
                else
                    methaneStore = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, MethaneStoreName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop) as GreenhouseGasesType;

            if (TruckCO2Emissions > 0)
                if (CarbonDioxideStoreName is null || CarbonDioxideStoreName == "Use store named CO2 if present")
                    co2Store = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, "CO2", OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore);
                else
                    co2Store = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, CarbonDioxideStoreName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);

            if (TruckN2OEmissions > 0)
                if (NitrousOxideStoreName is null || NitrousOxideStoreName == "Use store named N2O if present")
                    n2oStore = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, "N2O", OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore);
                else
                    n2oStore = resources.FindResourceType<GreenhouseGases, GreenhouseGasesType>(this, NitrousOxideStoreName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);
        }

        /// <summary>
        /// Method to report trucking emissions.
        /// </summary>
        /// <param name="numberOfTrucks">Number of trucks</param>
        /// <param name="isSales">Determines if this is a sales or purchase shipment</param>
        public void ReportEmissions(int numberOfTrucks, bool isSales)
        {
            if(numberOfTrucks > 0)
            {
                List<string> gases = new List<string>() { "Methane", "CO2", "N2O" };
                double emissions = 0;
                foreach (string gas in gases)
                {
                    GreenhouseGasesType gasstore = null;
                    switch (gas)
                    {
                        case "Methane":
                            gasstore = methaneStore;
                            emissions = TruckMethaneEmissions;
                            break;
                        case "CO2":
                            gasstore = co2Store;
                            emissions = TruckCO2Emissions;
                            break;
                        case "N2O":
                            gasstore = n2oStore;
                            emissions = TruckN2OEmissions;
                            break;
                        default:
                            gasstore = null;
                            break;
                    }

                    if (gasstore != null && emissions > 0)
                        gasstore.Add(numberOfTrucks * DistanceToMarket * emissions , this.Parent as CLEMModel, (isSales ? "sales" : "purchases"), TransactionCategory);
                }
            }
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">It is <span class=\"setvalue\">" + DistanceToMarket.ToString("0.##") + "</span> km to market and costs <span class=\"setvalue\">" + CostPerKmTrucking.ToString("0.##") + "</span> per km per truck");
                htmlWriter.Write("</div>");

                htmlWriter.Write("\r\n<div class=\"activityentry\">Each truck load can carry ");
                if (Number450kgPerTruck == 0)
                    htmlWriter.Write("<span class=\"errorlink\">[NOT SET]</span>");
                else
                    htmlWriter.Write("<span class=\"setvalue\">" + Number450kgPerTruck.ToString("0.###") + "</span>");

                htmlWriter.Write(" 450 kg individuals</div>");

                if (MinimumLoadBeforeSelling > 0 || MinimumTrucksBeforeSelling > 0)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    if (MinimumTrucksBeforeSelling > 0)
                        htmlWriter.Write("A minimum of <span class=\"setvalue\">" + MinimumTrucksBeforeSelling.ToString("###") + "</span> truck loads is required");

                    if (MinimumLoadBeforeSelling > 0)
                    {
                        if (MinimumTrucksBeforeSelling > 0)
                            htmlWriter.Write(" and each ");
                        else
                            htmlWriter.Write("Each ");

                        htmlWriter.Write("truck must be at least <span class=\"setvalue\">" + MinimumLoadBeforeSelling.ToString("0.##%") + "</span> full");
                    }
                    htmlWriter.Write(" for sales</div>");
                }

                if (MinimumLoadBeforeBuying > 0 || MinimumTrucksBeforeBuying > 0)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    if (MinimumTrucksBeforeBuying > 0)
                        htmlWriter.Write("A minimum of <span class=\"setvalue\">" + MinimumTrucksBeforeBuying.ToString("###") + "</span> truck loads is required");

                    if (MinimumLoadBeforeBuying > 0)
                    {
                        if (MinimumTrucksBeforeBuying > 0)
                            htmlWriter.Write(" and each ");
                        else
                            htmlWriter.Write("Each ");

                        htmlWriter.Write("truck must be at least <span class=\"setvalue\">" + MinimumLoadBeforeBuying.ToString("0.##%") + "</span> full");
                    }
                    htmlWriter.Write(" for purchases</div>");
                }

                if (TruckMethaneEmissions > 0 || TruckN2OEmissions > 0)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">Each truck will emmit ");
                    if (TruckMethaneEmissions > 0)
                        htmlWriter.Write("<span class=\"setvalue\">" + TruckMethaneEmissions.ToString("0.###") + "</span> kg methane");

                    if (TruckCO2Emissions > 0)
                    {
                        if (TruckMethaneEmissions > 0)
                            htmlWriter.Write(", ");

                        htmlWriter.Write("<span class=\"setvalue\">" + TruckCO2Emissions.ToString("0.###") + "</span> kg carbon dioxide");
                    }
                    if (TruckN2OEmissions > 0)
                    {
                        if (TruckMethaneEmissions + TruckCO2Emissions > 0)
                            htmlWriter.Write(" and ");

                        htmlWriter.Write("<span class=\"setvalue\">" + TruckN2OEmissions.ToString("0.###") + "</span> kg nitrous oxide");
                    }
                    htmlWriter.Write(" per km");
                    htmlWriter.Write("</div>");

                    if (TruckMethaneEmissions > 0)
                    {
                        htmlWriter.Write("\r\n<div class=\"activityentry\">Methane emissions will be placed in ");
                        if (MethaneStoreName is null || MethaneStoreName == "Use store named Methane if present")
                            htmlWriter.Write("<span class=\"resourcelink\">[GreenhouseGases].Methane</span> if present");
                        else
                            htmlWriter.Write($"<span class=\"resourcelink\">{MethaneStoreName}</span>");

                        htmlWriter.Write("</div>");
                    }
                    if (TruckCO2Emissions > 0)
                    {
                        htmlWriter.Write("\r\n<div class=\"activityentry\">Carbon dioxide emissions will be placed in ");
                        if (CarbonDioxideStoreName is null || CarbonDioxideStoreName == "Use store named CO2 if present")
                            htmlWriter.Write("<span class=\"resourcelink\">[GreenhouseGases].CO2</span> if present");
                        else
                            htmlWriter.Write($"<span class=\"resourcelink\">{CarbonDioxideStoreName}</span>");

                        htmlWriter.Write("</div>");
                    }
                    if (TruckN2OEmissions > 0)
                    {
                        htmlWriter.Write("\r\n<div class=\"activityentry\">Nitrous oxide emissions will be placed in ");
                        if (NitrousOxideStoreName is null || NitrousOxideStoreName == "Use store named N2O if present")
                            htmlWriter.Write("<span class=\"resourcelink\">[GreenhouseGases].N2O</span> if present");
                        else
                            htmlWriter.Write($"<span class=\"resourcelink\">{NitrousOxideStoreName}</span>");

                        htmlWriter.Write("</div>");
                    }
                }
                return htmlWriter.ToString(); 
            }
        } 
        #endregion

    }
}
