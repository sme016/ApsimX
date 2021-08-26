﻿using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace Models.CLEM.Activities
{
    /// <summary>
    /// Activity timer based on monthly interval
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ResourcePricing))]
    [ValidParent(ParentType = typeof(GrazeFoodStoreFertilityLimiter))]
    [Description("This activity timer defines a range between months upon which to perform activities.")]
    [HelpUri(@"Content/Features/Timers/MonthRange.htm")]
    [Version(1, 0, 1, "")]
    public class ActivityTimerMonthRange: CLEMModel, IActivityTimer, IActivityPerformedNotifier
    {
        [Link]
        private Clock clock = null;

        private int startMonth;
        private int endMonth;

        /// <summary>
        /// Notify CLEM that this Timer was performed
        /// </summary>
        public event EventHandler ActivityPerformed;

        /// <summary>
        /// Start month of annual period to perform activities
        /// </summary>
        [Description("Start month of annual period to perform activity")]
        [System.ComponentModel.DefaultValueAttribute(1)]
        [Required, Month]
        public MonthsOfYear StartMonth { get; set; }

        /// <summary>
        /// End month of annual period to perform activities
        /// </summary>
        [Description("End month of annual period to perform activity")]
        [Required, Month]
        [System.ComponentModel.DefaultValueAttribute(12)]
        public MonthsOfYear EndMonth { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ActivityTimerMonthRange()
        {
            this.SetDefaults();
        }

        /// <inheritdoc/>
        public bool ActivityDue
        {
            get
            {
                return IsMonthInRange(clock.Today);
            }
        }

        /// <inheritdoc/>
        public bool Check(DateTime dateToCheck)
        {
            return IsMonthInRange(dateToCheck);
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("StartOfSimulation")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            startMonth = (int)StartMonth;
            endMonth = (int)EndMonth;
        }

        private bool IsMonthInRange(DateTime date)
        {
            bool due = false;
            if (startMonth <= endMonth)
            {
                if ((date.Month >= startMonth) & (date.Month <= endMonth))
                    due = true;
            }
            else
            {
                if ((date.Month <= endMonth) | (date.Month >= startMonth))
                    due = true;
            }
            return due;
        }

        /// <inheritdoc/>
        public virtual void OnActivityPerformed(EventArgs e)
        {
            ActivityPerformed?.Invoke(this, e);
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"filter\">");
                htmlWriter.Write("Perform between ");
                if (StartMonth == 0)
                    htmlWriter.Write("<span class=\"errorlink\">NOT SET</span>");
                else
                {
                    htmlWriter.Write("<span class=\"setvalueextra\">");
                    htmlWriter.Write(StartMonth.ToString() + "</span>");
                }
                htmlWriter.Write(" and <span class=\"setvalueextra\">");
                if (EndMonth == 0)
                    htmlWriter.Write("<span class=\"errorlink\">NOT SET</span>");
                else
                {
                    htmlWriter.Write("<span class=\"setvalueextra\">");
                    htmlWriter.Write(EndMonth.ToString() + "</span>");
                }
                htmlWriter.Write("</div>");
                if (!this.Enabled)
                    htmlWriter.Write(" - DISABLED!");
                return htmlWriter.ToString(); 
            }
        }

        /// <inheritdoc/>
        public override string ModelSummaryClosingTags(bool formatForParentControl)
        {
            return "</div>";
        }

        /// <inheritdoc/>
        public override string ModelSummaryOpeningTags(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("<div class=\"filtername\">");
                if (!this.Name.Contains(this.GetType().Name.Split('.').Last()))
                    htmlWriter.Write(this.Name);
                htmlWriter.Write($"</div>");
                htmlWriter.Write("\r\n<div class=\"filterborder clearfix\" style=\"opacity: " + SummaryOpacity(formatForParentControl).ToString() + "\">");
                return htmlWriter.ToString(); 
            }
        } 
        #endregion
    }
}
