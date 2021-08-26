﻿using Models.CLEM.Activities;
using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using Models.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace Models.CLEM
{
    /// <summary>
    /// CLEM Zone to control simulation
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Simulation))]
    [Description("This represents a shared market place for CLEM farms")]
    [HelpUri(@"Content/Features/Market.htm")]
    [Version(1, 0, 2, "Tested and functioning for targeted feeding including transmutations but still needs movement of goods to market.")]
    [Version(1, 0, 1, "Early implementation of market place for multi-farm simulations. This is a major addition and is not checked for full functionality.")]
    [ScopedModel]
    public class Market: Zone, IValidatableObject, ICLEMUI
    {
        [Link]
        private Summary summary = null;

        private ResourcesHolder resources;

        /// <summary>Area of the zone.</summary>
        /// <value>The area.</value>
        [JsonIgnore]
        public new double Area { get; set; }

        /// <summary>Gets or sets the slope.</summary>
        /// <value>The slope.</value>
        [JsonIgnore]
        public new double Slope { get; set; }

        /// <summary>
        /// not used in CLEM
        /// </summary>
        [JsonIgnore]
        public new double AspectAngle { get; set; }

        /// <summary>Local altitude (meters above sea level).</summary>
        [JsonIgnore]
        public new double Altitude { get; set; } = 50;

        /// <summary>
        /// Identifies the last selected tab for display
        /// </summary>
        [JsonIgnore]
        public string SelectedTab { get; set; }

        /// <summary>
        /// ResourceHolder for the market
        /// </summary>
        public ResourcesHolder Resources { get
            {
                if(resources == null)
                    resources = this.FindAllChildren<ResourcesHolder>().FirstOrDefault();
                return resources; 
            }
        }

        private FinanceType bankAccount;
        /// <summary>
        /// Default (first) bank account for the market
        /// </summary>
        public FinanceType BankAccount
        {
            get
            {
                if (bankAccount == null)
                    bankAccount = Resources.FindResourceGroup<Finance>()?.FindAllChildren<FinanceType>().FirstOrDefault() as FinanceType;
                return bankAccount;
            }
        }

        /// <summary>An event handler to allow us to perform validation</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMValidate")]
        private void OnCLEMValidate(object sender, EventArgs e)
        {
            // validation is performed here
            // this event fires after Activity and Resource validation so that resources are available to check in the validation.
            // commencing is too early as Summary has not been created for reporting.
            // some values assigned in commencing will not be checked before processing, but will be caught here
            // each ZoneCLEM and Market will call this validation for all children
            // CLEM components above ZoneCLEM (e.g. RandomNumberGenerator) needs to validate itself
            if (!ZoneCLEM.Validate(this, "", this, summary))
            {
                string error = "@i:Invalid parameters in model";

                // get all validations 
                if (summary.Messages() != null)
                {
                    foreach (DataRow item in summary.Messages().Rows)
                    {
                        if (item[3].ToString().StartsWith("Invalid"))
                            error += "\r\n" + item[3].ToString();
                    }
                }
                throw new ApsimXException(this, error.Replace("&shy;", "."));
            }
        }

        #region validation
        /// <summary>
        /// Validate object
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            // check that one resources and on activities are present.
            int holderCount = this.FindAllChildren<ResourcesHolder>().Count();
            if (holderCount == 0)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("A market place must contain a Resources Holder to manage resources", memberNames));
            }
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("A market place must contain only one (1) Resources Holder to manage resources", memberNames));
            }
            holderCount = this.FindAllChildren<ActivitiesHolder>().Count();
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Activities" };
                results.Add(new ValidationResult("A market place must contain only one (1) Activities Holder to manage activities", memberNames));
            }
            // only one market
            holderCount = FindAncestor<Simulation>().FindAllChildren<Market>().Count();
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Markets" };
                results.Add(new ValidationResult("Only one [m=Market] place is allowed in a CLEM [Simulation]", memberNames));
            }

            return results;
        } 
        #endregion

        #region descriptive summary
        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="useFullDescription">Use full verbose description</param>
        /// <param name="htmlString"></param>
        /// <returns></returns>
        public string GetFullSummary(object model, bool useFullDescription, string htmlString)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write($"\r\n<div class=\"holdermain\" style=\"opacity: {((!this.Enabled) ? "0.4" : "1")}\">");
                foreach (CLEMModel cm in this.FindAllChildren<CLEMModel>())
                    htmlWriter.Write(cm.GetFullSummary(cm, true, ""));
                htmlWriter.Write("</div>");
                return htmlWriter.ToString();
            }
        } 
        #endregion


    }
}
