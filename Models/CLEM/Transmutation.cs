﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Models.Core.Attributes;
using Models.CLEM.Interfaces;
using System.IO;

namespace Models.CLEM
{
    ///<summary>
    /// Resource transmutation
    /// Will convert one resource into another (e.g. $ => labour) 
    /// These transmutations are defined under each ResourceType in the Resources section of the UI tree
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(IResourceType))]
    [Description("This Transmutation will convert any other resource into the current resource where there is a shortfall. This is placed under any resource type where you need to provide a transmutation. For example to convert Finance Type (money) into a Animal Food Store Type (Lucerne) or effectively purchase fodder when low.")]
    [Version(2, 0, 1, "Full reworking of transmute resource (B) to shortfall resource (A)")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Transmutation/Transmutation.htm")]
    public class Transmutation: CLEMModel, IValidatableObject
    {
        /// <summary>
        /// Resource in shortfall (A)
        /// </summary>
        [Description("Resource in shortfall (A)")]
        [Core.Display(Type = DisplayType.FieldName)]
        public string ResourceInShortfall { get { return (Parent as CLEMModel).NameWithParent; } private set {; } }

        /// <summary>
        /// Amount of resource in shortfall per transmutation packet
        /// </summary>
        [Description("Transmutation packet size (amount of A)")]
        [Required, GreaterThanEqualValue(0)]
        public double TransmutationPacketSize { get; set; }

        /// <summary>
        /// Enforce transmutation in whole packets
        /// </summary>
        [Description("Use whole packets")]
        public bool UseWholePackets { get; set; }

        /// <summary>
        /// Label to assign each transaction created by this activity in ledgers
        /// </summary>
        [Description("Category for transactions")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Category for transactions required")]
        public string TransactionCategory { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Transmutation()
        {
            base.ModelSummaryStyle = HTMLSummaryStyle.SubResourceLevel2;
            TransactionCategory = "Transmutation";
        }

        #region validation

        /// <summary>
        /// Validate this object
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            if (!this.FindAllChildren<ITransmute>().Where(a => (a as IModel).Enabled).Any()) //   Apsim.Children (this, typeof(TransmutationCost)).Count() == 0)
            {
                string[] memberNames = new string[] { "Transmutes" };
                results.Add(new ValidationResult("No transmute components provided under this transmutation", memberNames));
            }
            return results;
        }
        #endregion

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("<div class=\"activityentry\">");

                var pricing = this.FindAllChildren<ITransmute>().Where(a => a.TransmuteStyle == TransmuteStyle.UsePricing);
                var direct = this.FindAllChildren<ITransmute>().Where(a => a.TransmuteStyle == TransmuteStyle.Direct);

                htmlWriter.Write($"The following resources (B) will transmute ");
                if (pricing.Any())
                {
                    htmlWriter.Write($"using the resource purchase price ");
                    var transmuteResourcePrice = ((this.FindAncestor<ResourcesHolder>()).FindResourceType<ResourceBaseWithTransactions, IResourceType>(this, ResourceInShortfall, OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore))?.Price(PurchaseOrSalePricingStyleType.Purchase);
                    if (transmuteResourcePrice != null)
                    {
                        htmlWriter.Write("found");
                    }
                    else
                    {
                        htmlWriter.Write($"<span class=\"errorlink\">not found</span>");
                    }
                }
                htmlWriter.WriteLine(" to provide this shortfall resource (A)");


                if (direct.Any())
                    htmlWriter.Write($" in {(UseWholePackets ? " whole" : "")} packets of <span class=\"setvalue\">{TransmutationPacketSize:#,##0.##}</span>");

                if (pricing.Count() + direct.Count() > 1)
                    htmlWriter.Write($" (or the largest packet size needed the individual transmutes)");

                htmlWriter.WriteLine("</div>");

                if (!this.FindAllChildren<ITransmute>().Any())
                {
                    htmlWriter.Write("<div class=\"errorbanner\">");
                    htmlWriter.Write("No Transmute components provided");
                    htmlWriter.WriteLine("</div>");
                }
                return htmlWriter.ToString();
            }
        }

        #endregion
    }

}
