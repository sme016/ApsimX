﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Models.CLEM.Groupings;
using Models.Core.Attributes;
using System.IO;
using Newtonsoft.Json;

namespace Models.CLEM.Activities
{
    /// <summary>Ruminant predictive stocking activity</summary>
    /// <summary>This activity ensures the total herd size is acceptible to graze the dry season pasture</summary>
    /// <summary>It is designed to consider individuals already marked for sale and add additional individuals before transport and sale.</summary>
    /// <summary>It will check all paddocks that the specified herd are grazing</summary>
    /// <version>1.0</version>
    /// <updates>1.0 First implementation of this activity using IAT/NABSA processes</updates>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("Manage ruminant stocking during the dry season using predicted future pasture biomass")]
    [Version(1, 0, 3, "Avoids double accounting while removing individuals")]
    [Version(1, 0, 1, "")]
    [Version(1, 0, 2, "Updated assessment calculations and ability to report results")]
    [HelpUri(@"Content/Features/Activities/Ruminant/RuminantPredictiveStocking.htm")]
    public class RuminantActivityPredictiveStocking: CLEMRuminantActivityBase, IValidatableObject
    {
        [Link]
        private Clock clock = null;

        /// <summary>
        /// Month for assessing dry season feed requirements
        /// </summary>
        [Description("Month for assessing dry season feed requirements")]
        [Required, Month]
        public MonthsOfYear AssessmentMonth { get; set; }

        /// <summary>
        /// Number of months to assess
        /// </summary>
        [Description("Number of months to assess")]
        [Required, GreaterThanEqualValue(0)]
        public int DrySeasonLength { get; set; }

        /// <summary>
        /// Minimum estimated feed (kg/ha) allowed at end of period
        /// </summary>
        [Description("Minimum estimated feed (kg/ha) allowed at end of period")]
        [Required, GreaterThanEqualValue(0)]
        public double FeedLowLimit { get; set; }

        /// <summary>
        /// Predicted pasture at end of assessment period
        /// </summary>
        [JsonIgnore]
        public double PasturePredicted { get; private set; }

        /// <summary>
        /// Predicted pasture shortfall at end of assessment period
        /// </summary>
        public double PastureShortfall { get {return Math.Max(0, FeedLowLimit - PasturePredicted); } }

        /// <summary>
        /// AE to destock
        /// </summary>
        [JsonIgnore]
        public double AeToDestock { get; private set; }

        /// <summary>
        /// AE destocked
        /// </summary>
        [JsonIgnore]
        public double AeDestocked { get; private set; }

        /// <summary>
        /// AE destock shortfall
        /// </summary>
        public double AeShortfall { get {return AeToDestock - AeDestocked; } }

        /// <summary>
        /// Constructor
        /// </summary>
        public RuminantActivityPredictiveStocking()
        {
            TransactionCategory = "Livestock.Destock";
        }

        #region validation

        /// <summary>
        /// Validate this model
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // check that this model contains children RuminantDestockGroups with filters
            var results = new List<ValidationResult>();
            // check that this activity contains at least one RuminantGroup with Destock reason (filters optional as someone might want to include entire herd)
            if (this.FindAllChildren<RuminantGroup>().Count() == 0)
            {
                string[] memberNames = new string[] { "Ruminant group" };
                results.Add(new ValidationResult("At least one [f=RuminantGroup] with a [Destock] reason and a [f=RuminantFilter] must be present under this [a=RuminantActivityPredictiveStocking] activity", memberNames));
            }
            else if (this.FindAllChildren<RuminantGroup>().Where(a => a.Reason != RuminantStockGroupStyle.Destock).Count() > 0)
            {
                string[] memberNames = new string[] { "Ruminant group" };
                results.Add(new ValidationResult("Only [f=RuminantGroup] with a [Destock] reason are permitted under this [a=RuminantActivityPredictiveStocking] activity", memberNames));
            }

            return results;
        } 
        #endregion

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            AeToDestock = 0;
            AeDestocked = 0;
            this.InitialiseHerd(false, true);
        }

        /// <summary>An event handler to call for changing stocking based on prediced pasture biomass</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMAnimalStock")]
        private void OnCLEMAnimalStock(object sender, EventArgs e)
        {
            AeToDestock = 0;
            AeDestocked = 0;
            // this event happens after management has marked individuals for purchase or sale.
            if (clock.Today.Month == (int)AssessmentMonth)
            {
                this.Status = ActivityStatus.NotNeeded;
                // calculate dry season pasture available for each managed paddock holding stock not flagged for sale

                foreach (var paddockGroup in HerdResource.Herd.Where(a => (a.Location??"") != "").GroupBy(a => a.Location))
                {
                    // multiple breeds are currently not supported as we need to work out what to do with diferent AEs
                    if(paddockGroup.GroupBy(a => a.Breed).Count() > 1)
                    {
                        throw new ApsimXException(this, "Seasonal destocking paddocks containing multiple breeds is currently not supported\r\nActivity:"+this.Name+", Paddock: "+paddockGroup.Key);
                    }

                    // total adult equivalents not marked for sale of all breeds on pasture for utilisation
                    double totalAE = paddockGroup.Where(a => a.SaleFlag == HerdChangeReason.None).Sum(a => a.AdultEquivalent);

                    double shortfallAE = 0;
                    // Determine total feed requirements for dry season for all ruminants on the pasture
                    // We assume that all ruminant have the BaseAnimalEquivalent to the specified herd

                    GrazeFoodStoreType pasture = Resources.FindResourceType<GrazeFoodStore, GrazeFoodStoreType>(this, paddockGroup.Key, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);
                    double pastureBiomass = pasture.Amount;

                    // Adjust fodder balance for detachment rate (6%/month in NABSA, user defined in CLEM, 3%)
                    // AL found the best estimate for AAsh Barkly example was 2/3 difference between detachment and carryover detachment rate with average 12month pool ranging from 10 to 96% and average 46% of total pasture.
                    double detachrate = pasture.DetachRate + ((pasture.CarryoverDetachRate - pasture.DetachRate) * 0.66);
                    // Assume a consumption rate of 2% of body weight.
                    double feedRequiredAE = paddockGroup.FirstOrDefault().BreedParams.BaseAnimalEquivalent * 0.02 * 30.4; //  2% of AE animal per day
                    for (int i = 0; i <= this.DrySeasonLength; i++)
                    {
                        // only include detachemnt if current biomass is positive, not already overeaten
                        if (pastureBiomass > 0)
                            pastureBiomass *= (1.0 - detachrate);

                        if (i > 0) // not in current month as already consumed by this time.
                            pastureBiomass -= (feedRequiredAE * totalAE);
                    }

                    // Shortfall in Fodder in kg per hectare
                    // pasture at end of period in kg/ha
                    double pastureShortFallKgHa = pastureBiomass / pasture.Manager.Area;
                    PasturePredicted = pastureShortFallKgHa;
                    // shortfall from low limit
                    pastureShortFallKgHa = Math.Max(0, FeedLowLimit - pastureShortFallKgHa);
                    // Shortfall in Fodder in kg for paddock
                    double pastureShortFallKg = pastureShortFallKgHa * pasture.Manager.Area;

                    if (pastureShortFallKg == 0)
                        return;

                    // number of AE to sell to balance shortfall_kg over entire season
                    shortfallAE = pastureShortFallKg / (feedRequiredAE* this.DrySeasonLength);
                    AeToDestock = shortfallAE;

                    // get prediction
                    HandleDestocking(shortfallAE, paddockGroup.Key);

                    // fire event to allow reporting of findings
                    OnReportStatus(new EventArgs());
                }
            }
            else
                this.Status = ActivityStatus.Ignored;
        }

        private void HandleDestocking(double animalEquivalentsforSale, string paddockName)
        {
            if (animalEquivalentsforSale <= 0)
            {
                AeDestocked = 0;
                this.Status = ActivityStatus.Ignored;
                return;
            }

            // move to underutilised paddocks
            // TODO: This can be added later as an activity including spelling

            // remove all potential purchases from list as they can't be supported.
            // This does not change the shortfall AE as they were not counted in TotalAE pressure.
            HerdResource.PurchaseIndividuals.RemoveAll(a => a.Location == paddockName);

            // remove individuals to sale as specified by destock groups
            foreach (var item in FindAllChildren<RuminantGroup>().Where(a => a.Reason == RuminantStockGroupStyle.Destock))
            {
                // works with current filtered herd to obey filtering.
                var herd = item.Filter(CurrentHerd(false))
                    .Where(a => a.Location == paddockName && !a.ReadyForSale);

                foreach (Ruminant ruminant in herd)
                {
                    if (ruminant.SaleFlag != HerdChangeReason.DestockSale)
                    {
                        animalEquivalentsforSale -= ruminant.AdultEquivalent;
                        ruminant.SaleFlag = HerdChangeReason.DestockSale;
                    }

                    if (animalEquivalentsforSale <= 0)
                    {
                        AeDestocked = 0;
                        this.Status = ActivityStatus.Success;
                        return;
                    }
                }
            }

            AeDestocked = AeToDestock - animalEquivalentsforSale;
            this.Status = ActivityStatus.Partial;
            
            // handling of sucklings with sold female is in RuminantActivityBuySell
            // buy or sell is handled by the buy sell activity
        }

        /// <inheritdoc/>
        public event EventHandler ReportStatus;

        /// <inheritdoc/>
        protected void OnReportStatus(EventArgs e)
        {
            ReportStatus?.Invoke(this, e);
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">Pasture will be assessed in ");
                if ((int)AssessmentMonth > 0 & (int)AssessmentMonth <= 12)
                {
                    htmlWriter.Write("<span class=\"setvalue\">");
                    htmlWriter.Write(AssessmentMonth.ToString());
                }
                else
                    htmlWriter.Write("<span class=\"errorlink\">No month set");

                htmlWriter.Write("</span> for a dry season of ");
                if (DrySeasonLength > 0)
                {
                    htmlWriter.Write("<span class=\"setvalue\">");
                    htmlWriter.Write(DrySeasonLength.ToString("#0"));
                }
                else
                    htmlWriter.Write("<span class=\"errorlink\">No length");

                htmlWriter.Write("</span> months ");
                htmlWriter.Write("</div>");
                htmlWriter.Write("\r\n<div class=\"activityentry\">The herd will be sold to maintain ");
                htmlWriter.Write("<span class=\"setvalue\">");
                htmlWriter.Write(FeedLowLimit.ToString("#,##0"));
                htmlWriter.Write("</span> kg/ha at the end of this period");
                htmlWriter.Write("</div>");
                return htmlWriter.ToString(); 
            }
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerClosingTags()
        {
            return "\r\n</div>";
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerOpeningTags()
        {
            string html = "";
            html += "\r\n<div class=\"activitygroupsborder\">";
            html += "<div class=\"labournote\">Individuals will be sold in the following order</div>";

            if (FindAllChildren<RuminantGroup>().Count() == 0)
                html += "\r\n<div class=\"errorlink\">No ruminant filter groups provided</div>";
            return html;
        } 
        #endregion

    }
}
