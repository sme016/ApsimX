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
    [Description("This represents all CLEM farm resources and activities")]
    [HelpUri(@"Content/Features/CLEMComponent.htm")]
    [Version(1, 0, 4, "Random numbers and iteration property moved from this component to a stand-alone component\r\nChanges will be required to your setup")]
    [Version(1, 0, 3, "Updated filtering logic to improve performance")]
    [Version(1, 0, 2, "New ResourceUnitConverter functionality added that changes some reporting.\r\nThis change will cause errors for all previous custom resource ledger reports created using the APSIM Report component.\r\nTo fix errors add \".Name\" to all LastTransaction.ResourceType and LastTransaction.Activity entries in custom ledgers (i.e. LastTransaction.ResourceType.Name as Resource). The CLEM ReportResourceLedger component has been updated to automatically handle the changes")]
    [Version(1,0,1,"")]
    [ScopedModel]
    public class ZoneCLEM: Zone, IValidatableObject, ICLEMUI, ICLEMDescriptiveSummary
    {
        [Link]
        private Summary summary = null;
        [Link]
        private Clock clock = null;

        /// <summary>
        /// Identifies the last selected tab for display
        /// </summary>
        [JsonIgnore]
        public string SelectedTab { get; set; }

        /// <summary>
        /// Multiplier from single farm to regional number of farms for market transactions
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(1)]
        [Required, GreaterThanValue(0)]
        [Description("Farm multiplier to supply and receive from market")]
        public double FarmMultiplier { get; set; }

        /// <summary>
        /// Index of the simulation Climate Region
        /// </summary>
        [Description("Region id")]
        public int ClimateRegion { get; set; }

        /// <summary>
        /// Ecological indicators calculation interval (in months, 1 monthly, 12 annual)
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(12)]
        [Description("Ecological indicators calculation interval (in months, 1 monthly, 12 annual)")]
        [JsonIgnore, GreaterThanValue(0)]
        public int EcologicalIndicatorsCalculationInterval { get; set; }

        /// <summary>
        /// End of month to calculate ecological indicators
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(7)]
        [Description("End of first month to calculate ecological indicators")]
        [Required, Month]
        public MonthsOfYear EcologicalIndicatorsCalculationMonth { get; set; }

        /// <summary>
        /// Include in overall Descriptive Summary (HTML)
        /// </summary>
        [Description("Include in simulation descriptive summary (HTML)")]
        [System.ComponentModel.DefaultValueAttribute(false)]
        public bool AutoCreateDescriptiveSummary { get; set; }

        /// <summary>
        /// Month this overhead is next due.
        /// </summary>
        [JsonIgnore]
        public DateTime EcologicalIndicatorsNextDueDate { get; set; }

        // ignore zone base class properties

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
        /// Summary style to use for this component
        /// </summary>
        public HTMLSummaryStyle ModelSummaryStyle { get; set; }

        private string wholeSimulationSummaryFile = "";

        /// <summary>
        /// Constructor
        /// </summary>
        public ZoneCLEM()
        {
            ModelSummaryStyle = HTMLSummaryStyle.Helper;
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            EcologicalIndicatorsCalculationInterval = 12;

            // remove the overall summary description file if present
            string[] filebits = (sender as Simulation).FileName.Split('.');
            wholeSimulationSummaryFile = filebits.First() + "." + "html";
            if (File.Exists(wholeSimulationSummaryFile))
                File.Delete(wholeSimulationSummaryFile);
        }

        [EventSubscribe("Completed")]
        private void OnCompleted(object sender, EventArgs e)
        {
            // if auto create summary 
            if (AutoCreateDescriptiveSummary)
            if (!File.Exists(wholeSimulationSummaryFile))
                    System.IO.File.WriteAllText(wholeSimulationSummaryFile, CLEMModel.CreateDescriptiveSummaryHTML(this, false, false, (sender as Simulation).FileName));
                else
                {
                    string html = File.ReadAllText(wholeSimulationSummaryFile);
                    using (StringWriter htmlWriter = new StringWriter())
                    {
                        int index = html.IndexOf("<!-- CLEMZoneBody -->");
                        if (index > 0)
                        {
                            htmlWriter.Write(html.Substring(0, index - 1));
                            htmlWriter.Write(CLEMModel.CreateDescriptiveSummaryHTML(this, false, true));
                            htmlWriter.Write(html.Substring(index));
                            System.IO.File.WriteAllText(wholeSimulationSummaryFile, htmlWriter.ToString());
                        }
                    }
                }
        }


        /// <summary>
        /// Method to determine if this is the month to calculate ecological indicators
        /// </summary>
        /// <returns></returns>
        public bool IsEcologicalIndicatorsCalculationMonth()
        {
            return this.EcologicalIndicatorsNextDueDate.Year == clock.Today.Year && this.EcologicalIndicatorsNextDueDate.Month == clock.Today.Month;
        }

        /// <summary>Data stores to clear at start of month</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("EndOfMonth")]
        private void OnEndOfMonth(object sender, EventArgs e)
        {
            if (IsEcologicalIndicatorsCalculationMonth())
                this.EcologicalIndicatorsNextDueDate = this.EcologicalIndicatorsNextDueDate.AddMonths(this.EcologicalIndicatorsCalculationInterval);
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
            if (clock.StartDate.ToShortDateString() == "1/01/0001")
            {
                string[] memberNames = new string[] { "Clock.StartDate" };
                results.Add(new ValidationResult(String.Format("Invalid start date {0}", clock.StartDate.ToShortDateString()), memberNames));
            }
            if (clock.EndDate.ToShortDateString() == "1/01/0001")
            {
                string[] memberNames = new string[] { "Clock.EndDate" };
                results.Add(new ValidationResult(String.Format("Invalid end date {0}", clock.EndDate.ToShortDateString()), memberNames));
            }
            if (clock.StartDate.Day != 1)
            {
                string[] memberNames = new string[] { "Clock.StartDate" };
                results.Add(new ValidationResult(String.Format("CLEM must commence on the first day of a month. Invalid start date {0}", clock.StartDate.ToShortDateString()), memberNames));
            }
            // check that one resources and on activities are present.
            int holderCount = this.FindAllChildren<ResourcesHolder>().Count();
            if (holderCount == 0)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("CLEM must contain a Resources Holder to manage resources", memberNames));
            }
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("CLEM must contain only one (1) Resources Holder to manage resources", memberNames));
            }
            holderCount = this.FindAllChildren<ActivitiesHolder>().Count();
            if (holderCount == 0)
            {
                string[] memberNames = new string[] { "CLEM.Activities" };
                results.Add(new ValidationResult("CLEM must contain an Activities Holder to manage activities", memberNames));
            }
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Activities" };
                results.Add(new ValidationResult("CLEM must contain only one (1) Activities Holder to manage activities", memberNames));
            }
            return results;
        }

        /// <summary>An event handler to allow us to validate properties and setup</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMValidate")]
        private void OnCLEMValidate(object sender, EventArgs e)
        {
            // validation is performed here
            // this event fires after Activity and Resource initialisation so that resources are available to check in the validation.
            // Commencing is too early as Summary has not been created for reporting.
            // some values assigned in commencing will not be checked before processing, but will be caught here
            // each ZoneCLEM and Market will call this validation for all children
            // CLEM components above ZoneCLEM (e.g. RandomNumberGenerator) needs to validate itself
            if (!Validate(this, "", this, summary))
            {
                string error = ""; //"@i:Invalid parameters in model";

                // get all validations 
                if(summary.Messages() != null)
                    foreach (DataRow item in summary.Messages().Rows)
                        if (item[3].ToString().StartsWith("Invalid"))
                            error += "\r\n" + item[3].ToString();
                throw new ApsimXException(this, error.Replace("&shy;","."));
            }

            if (clock.StartDate.Year > 1) // avoid checking if clock not set.
            if ((int)EcologicalIndicatorsCalculationMonth >= clock.StartDate.Month)
            {
                DateTime trackDate = new DateTime(clock.StartDate.Year, (int)EcologicalIndicatorsCalculationMonth, clock.StartDate.Day);
                while (trackDate.AddMonths(-EcologicalIndicatorsCalculationInterval) >= clock.Today)
                    trackDate = trackDate.AddMonths(-EcologicalIndicatorsCalculationInterval);
                EcologicalIndicatorsNextDueDate = trackDate;
            }
            else
            {
                EcologicalIndicatorsNextDueDate = new DateTime(clock.StartDate.Year, (int)EcologicalIndicatorsCalculationMonth, clock.StartDate.Day);
                while (clock.StartDate > EcologicalIndicatorsNextDueDate)
                    EcologicalIndicatorsNextDueDate = EcologicalIndicatorsNextDueDate.AddMonths(EcologicalIndicatorsCalculationInterval);
            }
        }

        /// <summary>
        /// Internal method to iterate through all children in CLEM and report any parameter setting errors
        /// </summary>
        /// <param name="model">The model being validated</param>
        /// <param name="modelPath">Pass blank string. Used for tracking model path</param>
        /// <param name="parentZone">The name of the containing ZoneCLEM or Market for reporting</param>
        /// <param name="summary">Link to summary for reporting</param>
        /// <returns>Boolean indicating whether validation was successful</returns>
        public static bool Validate(IModel model, string modelPath, Model parentZone, ISummary summary)
        {
            string starter = "[=";
            if(typeof(IResourceType).IsAssignableFrom(model.GetType()))
                starter = "[r=";
            if (model.GetType() == typeof(ZoneCLEM))
                starter = "[z=";
            if (model.GetType() == typeof(ResourcesHolder))
                starter = "[rs=";
            if (model.GetType() == typeof(LabourRequirement))
                starter = "[l=";
            if (model.GetType().IsSubclassOf(typeof(ResourceBaseWithTransactions)))
                starter = "[r=";
            if (model.GetType() == typeof(ActivitiesHolder))
                starter = "[as=";
            if (model.GetType().IsSubclassOf(typeof(CLEMActivityBase)))
                starter = "[a=";
            if (model.GetType().Name.Contains("Group"))
                starter = "[g=";
            if (model.GetType().Name.Contains("Timer"))
                starter = "[t=";
            if (model.GetType().Name.Contains("Filter"))
                starter = "[f=";

            if (model is CLEMModel)
                (model as CLEMModel).CLEMParentName = parentZone.Name;
            modelPath += starter+model.Name+"]";
            modelPath = modelPath.Replace("][", "]&shy;[");
            bool valid = true;
            var validationContext = new ValidationContext(model, null, null);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(model, validationContext, validationResults, true);
            if(model.Name.EndsWith(" "))
                validationResults.Add(new ValidationResult("Component name cannot end with a space character", new string[] { "Name" }));

            if (validationResults.Count > 0)
            {
                valid = false;
                // report all errors
                foreach (var validateError in validationResults)
                {
                    // get description
                    string text = "";
                    var property = model.GetType().GetProperty(validateError.MemberNames.FirstOrDefault());
                    if (property != null)
                    {
                        text = "";
                        if (property.GetCustomAttributes(typeof(DescriptionAttribute), true).Count() > 0)
                        {
                            var attribute = property.GetCustomAttributes(typeof(DescriptionAttribute), true)[0];
                            var description = (DescriptionAttribute)attribute;
                            text = description.ToString();
                        }
                    }
                    string error = String.Format("Invalid parameter value in " + modelPath + "" + Environment.NewLine + "PARAMETER: " + validateError.MemberNames.FirstOrDefault());
                    if (text != "")
                        error += String.Format(Environment.NewLine + "DESCRIPTION: " + text);
                    error += String.Format(Environment.NewLine + "PROBLEM: " + validateError.ErrorMessage + Environment.NewLine);
                    summary.WriteWarning(parentZone, error);
                }
            }
            foreach (var child in model.Children)
            {
                bool result = Validate(child, modelPath, parentZone, summary);
                if (valid && !result)
                    valid = false;
            }
            return valid;
        }
        #endregion

        #region Descriptive summary

        ///<inheritdoc/>
        public string GetFullSummary(IModel model, bool useFullDescription, string htmlString)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"holdermain\" style=\"opacity: " + ((!this.Enabled) ? "0.4" : "1") + "\">");

                // get clock
                IModel parentSim = FindAncestor<Simulation>();

                htmlWriter.Write(CLEMModel.AddMemosToSummary(parentSim));

                // create the summary box with properties of this component
                if (this is ICLEMDescriptiveSummary)
                {
                    bool formatForParentControl = true;
                    htmlWriter.Write(this.ModelSummaryOpeningTags(formatForParentControl));
                    htmlWriter.Write(this.ModelSummaryInnerOpeningTagsBeforeSummary());
                    htmlWriter.Write(this.ModelSummary(formatForParentControl));
                    htmlWriter.Write(CLEMModel.AddMemosToSummary(this));
                    htmlWriter.Write(this.ModelSummaryInnerOpeningTags(formatForParentControl));
                    htmlWriter.Write(this.ModelSummaryInnerClosingTags(formatForParentControl));
                    htmlWriter.Write(this.ModelSummaryClosingTags(formatForParentControl));
                }

                // find random number generator
                RandomNumberGenerator rnd = parentSim.FindAllChildren<RandomNumberGenerator>().FirstOrDefault();
                if (rnd != null)
                {
                    htmlWriter.Write("\r\n<div class=\"clearfix defaultbanner\">");
                    htmlWriter.Write("<div class=\"namediv\">" + rnd.Name + "</div>");
                    htmlWriter.Write("<div class=\"typediv\">RandomNumberGenerator</div>");
                    htmlWriter.Write("</div>");
                    htmlWriter.Write("\r\n<div class=\"defaultcontent\">");
                    htmlWriter.Write("\r\n<div class=\"activityentry\">Random numbers are provided for this simultion.<br />");
                    if (rnd.Seed == 0)
                        htmlWriter.Write("Every run of this simulation will be different.");
                    else
                        htmlWriter.Write("Each run of this simulation will be identical using the seed <span class=\"setvalue\">" + rnd.Seed.ToString() + "</span>");
                    htmlWriter.Write("\r\n</div>");

                    htmlWriter.Write(CLEMModel.AddMemosToSummary(rnd));

                    htmlWriter.Write("\r\n</div>");
                }

                Clock clk = parentSim.FindAllChildren<Clock>().FirstOrDefault() as Clock;
                if (clk != null)
                {
                    htmlWriter.Write("\r\n<div class=\"clearfix defaultbanner\">");
                    htmlWriter.Write("<div class=\"namediv\">" + clk.Name + "</div>");
                    htmlWriter.Write("<div class=\"typediv\">Clock</div>");
                    htmlWriter.Write("</div>");
                    htmlWriter.Write("\r\n<div class=\"defaultcontent\">");
                    htmlWriter.Write("\r\n<div class=\"activityentry\">This simulation runs from ");
                    if (clk.StartDate == null)
                        htmlWriter.Write("<span class=\"errorlink\">[START DATE NOT SET]</span>");
                    else
                        htmlWriter.Write("<span class=\"setvalue\">" + clk.StartDate.ToShortDateString() + "</span>");
                    htmlWriter.Write(" to ");
                    if (clk.EndDate == null)
                        htmlWriter.Write("<span class=\"errorlink\">[END DATE NOT SET]</span>");
                    else
                        htmlWriter.Write("<span class=\"setvalue\">" + clk.EndDate.ToShortDateString() + "</span>");
                    htmlWriter.Write("\r\n</div>");

                    htmlWriter.Write(CLEMModel.AddMemosToSummary(clk));

                    htmlWriter.Write("\r\n</div>");
                    htmlWriter.Write("\r\n</div>");
                }

                foreach (CLEMModel cm in this.FindAllChildren<CLEMModel>().Cast<CLEMModel>())
                    htmlWriter.Write(cm.GetFullSummary(cm, true, ""));
                return htmlWriter.ToString(); 
            }
        }

        ///<inheritdoc/>
        public string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">");
                htmlWriter.Write("This farm is identified as region ");
                htmlWriter.Write($"<span class=\"setvalue\">{ClimateRegion}</span></div>");

                ResourcesHolder resources = this.FindChild<ResourcesHolder>();
                if(resources != null)
                    if (resources.FoundMarket != null)
                    {
                        htmlWriter.Write("\r\n<div class=\"activityentry\">");
                        htmlWriter.Write("This farm represents ");
                        htmlWriter.Write($"<span class=\"setvalue\">{FarmMultiplier}</span></div> farm(s) when trading with the Market</div>");
                    }

                if ((this.FindDescendant<RuminantActivityGrazeAll>() != null) || (this.FindDescendant<RuminantActivityGrazePasture>() != null) || (this.FindDescendant<RuminantActivityGrazePastureHerd>() != null))
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    htmlWriter.Write("Ecological indicators will be calculated every ");
                    if (EcologicalIndicatorsCalculationInterval <= 0)
                        htmlWriter.Write("<span class=\"errorlink\">NOT SET</span> months");
                    else
                        htmlWriter.Write($"<span class=\"setvalue\">{EcologicalIndicatorsCalculationInterval}</span> month{((EcologicalIndicatorsCalculationInterval == 1) ? "" : "s")}");
                    htmlWriter.Write($" starting at the end of {EcologicalIndicatorsCalculationMonth}</div>");
                }

                if (AutoCreateDescriptiveSummary)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    htmlWriter.Write($"This component will be included in the overall simulation summary decription html file</div>");
                }
                return htmlWriter.ToString();
            }
        }

        ///<inheritdoc/>
        public string ModelSummaryClosingTags(bool formatForParentControl)
        {
            return "\r\n</div>\r\n</div>";
        }

        ///<inheritdoc/>
        public string ModelSummaryOpeningTags(bool formatForParentControl)
        {
            string overall = "default";
            string extra = "";

            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"holder" + ((extra == "") ? "main" : "sub") + " " + overall + "\" style=\"opacity: " + ((!this.Enabled) ? 0.4 : 1.0).ToString() + ";\">");
                htmlWriter.Write("\r\n<div class=\"clearfix " + overall + "banner" + extra + "\">" + this.ModelSummaryNameTypeHeader() + "</div>");
                htmlWriter.Write("\r\n<div class=\"" + overall + "content" + ((extra != "") ? extra : "") + "\">");

                return htmlWriter.ToString();
            }
        }

        ///<inheritdoc/>
        public string ModelSummaryInnerClosingTags(bool formatForParentControl)
        {
            return "";
        }

        ///<inheritdoc/>
        public string ModelSummaryInnerOpeningTags(bool formatForParentControl)
        {
            return "";
        }

        ///<inheritdoc/>
        public string ModelSummaryInnerOpeningTagsBeforeSummary()
        {
            return "";
        }

        ///<inheritdoc/>
        public string ModelSummaryNameTypeHeader()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("<div class=\"namediv\">" + this.Name + ((!this.Enabled) ? " - DISABLED!" : "") + "</div>");
                if (this.GetType().IsSubclassOf(typeof(CLEMActivityBase)))
                {
                    htmlWriter.Write("<div class=\"partialdiv\"");
                    htmlWriter.Write(">");
                    htmlWriter.Write("</div>");
                }
                htmlWriter.Write("<div class=\"typediv\">" + this.GetType().Name + "</div>");
                return htmlWriter.ToString();
            }
        }

        ///<inheritdoc/>
        public string ModelSummaryNameTypeHeaderText()
        {
            return this.Name;
        }
        #endregion

    }
}
