﻿using Models.Core;
using Models.CLEM.Activities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Models.Core.Attributes;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Models.CLEM.Resources;
using System.IO;

namespace Models.CLEM.Groupings
{
    ///<summary>
    /// Contains a group of filters to identify individual ruminants for feeding
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(RuminantActivityFeed))]
    [Description("Set feeding value for specified individual ruminants")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Filters/Groups/RuminantFeedGroup.htm")]
    public class RuminantFeedGroup : FilterGroup<Ruminant>
    {
        /// <summary>
        /// Value to supply for each month
        /// </summary>
        [Description("Value to supply")]
        [GreaterThanValue(0)]
        public double Value { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public RuminantFeedGroup()
        {
            base.ModelSummaryStyle = HTMLSummaryStyle.SubActivity;
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                if (this.Parent.GetType() != typeof(RuminantActivityFeed))
                {
                    return "<div class=\"warningbanner\">This Ruminant Feed Group must be placed beneath a Ruminant Activity Feed component</div>";
                }

                RuminantFeedActivityTypes ft = (this.Parent as RuminantActivityFeed).FeedStyle;
                htmlWriter.Write("\r\n<div class=\"activityentry\">");
                switch (ft)
                {
                    case RuminantFeedActivityTypes.SpecifiedDailyAmount:
                    case RuminantFeedActivityTypes.SpecifiedDailyAmountPerIndividual:
                        htmlWriter.Write("<span class=\"" + ((Value <= 0) ? "errorlink" : "setvalue") + "\">" + Value.ToString() + "kg</span>");
                        break;
                    case RuminantFeedActivityTypes.ProportionOfFeedAvailable:
                    case RuminantFeedActivityTypes.ProportionOfWeight:
                    case RuminantFeedActivityTypes.ProportionOfPotentialIntake:
                    case RuminantFeedActivityTypes.ProportionOfRemainingIntakeRequired:
                        if (Value != 1)
                        {
                            htmlWriter.Write("<span class=\"" + ((Value <= 0) ? "errorlink" : "setvalue") + "\">" + Value.ToString("0.##%") + "</span>");
                        }
                        break;
                    default:
                        break;
                }

                string starter = " of ";
                if (Value == 1)
                {
                    starter = "The ";
                }

                bool overfeed = false;
                htmlWriter.Write("<span class=\"setvalue\">");
                switch (ft)
                {
                    case RuminantFeedActivityTypes.ProportionOfFeedAvailable:
                        htmlWriter.Write(" of the available food supply");
                        overfeed = true;
                        break;
                    case RuminantFeedActivityTypes.SpecifiedDailyAmountPerIndividual:
                        htmlWriter.Write(" per individual per day");
                        overfeed = true;
                        break;
                    case RuminantFeedActivityTypes.SpecifiedDailyAmount:
                        overfeed = true;
                        htmlWriter.Write(" per day");
                        break;
                    case RuminantFeedActivityTypes.ProportionOfWeight:
                        overfeed = true;
                        htmlWriter.Write(starter + "live weight");
                        break;
                    case RuminantFeedActivityTypes.ProportionOfPotentialIntake:
                        htmlWriter.Write(starter + "potential intake");
                        break;
                    case RuminantFeedActivityTypes.ProportionOfRemainingIntakeRequired:
                        htmlWriter.Write(starter + "remaining intake");
                        break;
                    default:
                        break;
                }
                htmlWriter.Write("</span> ");

                switch (ft)
                {
                    case RuminantFeedActivityTypes.ProportionOfFeedAvailable:
                        htmlWriter.Write("will be fed to all individuals that match the following conditions:");
                        break;
                    case RuminantFeedActivityTypes.SpecifiedDailyAmount:
                        htmlWriter.Write("combined is fed to all individuals that match the following conditions:");
                        break;
                    case RuminantFeedActivityTypes.SpecifiedDailyAmountPerIndividual:
                        htmlWriter.Write("is fed to each individual that matches the following conditions:");
                        break;
                    default:
                        htmlWriter.Write("is fed to the individuals that match the following conditions:");
                        break;
                }
                htmlWriter.Write("</div>");

                if (overfeed)
                {
                    htmlWriter.Write("\r\n<div class=\"activityentry\">");
                    htmlWriter.Write("Individual's intake will be limited to Potential intake x the modifer for max overfeeding");
                    if (!(this.Parent as RuminantActivityFeed).StopFeedingWhenSatisfied)
                    {
                        htmlWriter.Write(", with excess food still utilised but wasted");
                    }
                    htmlWriter.Write("</div>");

                }
                if (ft == RuminantFeedActivityTypes.SpecifiedDailyAmount)
                {
                    htmlWriter.Write("<div class=\"warningbanner\">Note: This is a specified daily amount fed to the entire herd. If insufficient provided, each individual's potential intake will not be met</div>");
                }

                return htmlWriter.ToString(); 
            }
        }

        #endregion

    }
}
